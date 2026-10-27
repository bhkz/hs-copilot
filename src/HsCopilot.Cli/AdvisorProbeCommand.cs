using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Cli;

/// <summary>
/// Comparaison avant / après sur quelques décisions réelles, à coût plafonné : la même décision est envoyée au modèle
/// sans puis avec les informations ajoutées (Sombres dons des propositions, probabilités de pioche, tour suivant).
/// Seules ces informations diffèrent entre les deux messages.
/// </summary>
public static class AdvisorProbeCommand
{
    private sealed record Case(string Label, GameStateSnapshot State, DecisionContext Context);

    public static async Task<int> RunAsync(Args a)
    {
        if (a.Positional.Count == 0 || a.Get("at") is not string at)
            throw new ArgumentException("Usage : advisor-probe <log> --at [partie@]hh:mm:ss[,…] [--deckstring CODE] [--budget 0.45] [--out fichier] [--dry-run]");
        var (cfg, cards) = Commands.LoadEnvironment(a, quiet: true);
        var deck = a.Get("deckstring") is string code && cards is CardDatabase db ? DeckString.ToDecklist(code, db.All, "code de deck") : cfg.GetActiveDecklist();
        var games = ReplayFile.SplitGames(ReplayFile.ReadAllLines(a.Positional[0]));
        var cases = new List<Case>();
        foreach (var item in at.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = item.Split('@');
            var game = parts.Length == 2 ? games.First(g => g.Index == int.Parse(parts[0], CultureInfo.InvariantCulture)) : games[^1];
            var time = TimeSpan.Parse(parts[^1], CultureInfo.InvariantCulture);
            cases.Add(Find(game, time, cards, deck, item));
        }

        var notes = PromptBuilder.LoadStrategyNotes(cfg.Advisor.StrategyFile);
        var after = PromptBuilder.BuildInstructions(notes);
        var before = WithoutGiftInstructions(after);
        var budget = double.TryParse(a.Get("budget"), NumberStyles.Float, CultureInfo.InvariantCulture, out var b) ? b : 0.45;
        var options = cfg.Advisor.ToOpenAiOptions();
        var providerAfter = new OpenAiResponsesProvider(options, after);
        var providerBefore = new OpenAiResponsesProvider(options, before);
        var spent = 0.0;
        var sb = new StringBuilder();
        var json = new JsonArray();
        sb.AppendLine($"# Comparaison avant / après ({options.Model}, effort {options.ReasoningEffort})");
        sb.AppendLine();
        foreach (var c in cases)
        {
            var payloadAfter = PromptBuilder.BuildUserPayload(c.Context);
            var payloadBefore = PayloadVariants.WithoutGiftsDeckAndNextTurn(payloadAfter);
            sb.AppendLine($"## {c.Label}");
            sb.AppendLine();
            sb.AppendLine(Describe(c));
            sb.AppendLine();
            sb.AppendLine($"Message : {payloadBefore.Length} caractères avant, {payloadAfter.Length} après (+{payloadAfter.Length - payloadBefore.Length}).");
            sb.AppendLine();
            var entry = new JsonObject { ["case"] = c.Label, ["payloadBefore"] = payloadBefore.Length, ["payloadAfter"] = payloadAfter.Length };
            foreach (var (name, provider, payload) in new[] { ("avant", providerBefore, payloadBefore), ("après", providerAfter, payloadAfter) })
            {
                if (a.Has("dry-run")) { sb.AppendLine($"- {name} : (non envoyé, --dry-run)"); continue; }
                if (spent + 0.04 > budget) { sb.AppendLine($"- {name} : non envoyé, budget de {budget:0.00} $ atteint"); continue; }
                var r = await provider.ProposeWithPayloadAsync(payload, CancellationToken.None);
                spent += r.CostUsd ?? 0;
                var o = r.Output;
                var actionLabel = o is null ? "?" : c.Context.LegalActions.FirstOrDefault(x => x.Id == o.ActionId)?.Label ?? $"(id inconnu {o.ActionId})";
                sb.AppendLine($"- **{name}** : {actionLabel} — {o?.Summary}");
                sb.AppendLine($"  - pourquoi : {o?.Why}");
                sb.AppendLine($"  - raisonnement : {o?.Rationale}");
                if (o?.PlanNextTurn is { } plan) sb.AppendLine($"  - tour suivant : {plan}");
                sb.AppendLine($"  - coût {r.CostUsd ?? 0:0.0000} $, {r.InputTokens} jetons en entrée ({r.CachedInputTokens} en cache), {r.OutputTokens} en sortie, {r.LatencyMs / 1000:0.0} s" + (r.Error is null ? "" : $", erreur : {r.Error}"));
                entry[name] = new JsonObject
                {
                    ["action"] = actionLabel, ["summary"] = o?.Summary, ["why"] = o?.Why, ["rationale"] = o?.Rationale, ["plan"] = o?.PlanNextTurn,
                    ["costUsd"] = r.CostUsd, ["inputTokens"] = r.InputTokens, ["outputTokens"] = r.OutputTokens,
                };
            }
            sb.AppendLine();
            json.Add(entry);
        }
        sb.AppendLine($"Coût total : {spent:0.0000} $ (plafond {budget:0.00} $).");
        var report = sb.ToString();
        if (a.Get("out") is string outPath)
        {
            File.WriteAllText(outPath, report, new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        }
        Console.WriteLine(report);
        return 0;
    }

    /// <summary>Premier point de décision (action ou choix) à partir de l'heure donnée, une fois les options complètes.</summary>
    private static Case Find(LoggedGame game, TimeSpan time, ICardDatabase cards, Decklist? deck, string label)
    {
        var engine = new GameEngine(new EngineOptions { Cards = cards });
        var projector = new GameStateProjector(cards);
        foreach (var raw in game.Lines)
        {
            var p = PowerLogLineParser.TryParse(raw);
            if (p is null) continue;
            engine.Process(p);
            if (p.Time < time || p.Method is "DebugPrintOptions" or "DebugPrintEntityChoices" || engine.LocalPlayerId is null) continue;
            var s = projector.Project(engine, new ProjectionContext { Transport = "probe", Decklist = deck });
            if (DecisionContextBuilder.IsChoicePoint(s))
                return new Case($"{label} — {s.PendingChoice!.SourceName ?? "choix"} (tour {s.Game.TurnNumber})", s, DecisionContextBuilder.BuildChoice(s)!);
            if (DecisionContextBuilder.IsDecisionPoint(s))
            {
                var ctx = DecisionContextBuilder.Build(s)!;
                ctx.LocalAnalysis = QuickAnalysis.LocalAnalysisPayload(ctx, QuickAnalysis.AssessLethal(ctx));
                return new Case($"{label} — action (tour {s.Game.TurnNumber}, {s.Local!.Mana.Available} mana)", s, ctx);
            }
        }
        throw new InvalidOperationException($"aucune décision après {time}");
    }

    private static string Describe(Case c)
    {
        var s = c.State;
        var sb = new StringBuilder();
        sb.Append($"Main : {string.Join(", ", s.Local!.Hand.Cards.Select(x => $"{x.Name} ({x.Cost})"))}. ");
        sb.Append($"Adversaire : {s.Opponent?.Hero?.Health} PV + {s.Opponent?.Hero?.Armor} armure. ");
        if (s.PendingChoice is { } pc)
            sb.Append("Propositions : " + string.Join(" | ", pc.Offered.Select(o => $"{o.Name} {o.Attack}/{o.Health} ({o.Cost})" + (o.DarkGift is null ? "" : $" + {DarkGifts.Summary(o)}"))));
        return sb.ToString();
    }

    private static string WithoutGiftInstructions(string instructions) =>
        Regex.Replace(instructions, @"\s*- Découverte avec Sombre don.*?pas les cartes seules\.", ".", RegexOptions.Singleline).Replace(";.", ".");
}
