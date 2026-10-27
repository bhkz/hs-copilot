using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;

namespace HsCopilot.Cli;

/// <summary>
/// Danger au prochain tour adverse avant et après chaque coup conseillé (modèle, suite de plan, solveur) et chaque coup joué,
/// sur des journaux rejoués. Valide la simulation contre l'état réel suivant et relève l'issue du tour adverse.
/// Diagnostic hors ligne : aucun appel, rien d'affiché en direct.
/// </summary>
public static class LineDangerCommand
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private sealed class Decision
    {
        public required string Game { get; init; }
        public required string GameType { get; init; }
        public required int GameTurn { get; init; }
        public required int Turn { get; init; }
        public required int Rank { get; init; }
        public required DecisionContext Ctx { get; init; }
        public LegalAction? Actual { get; init; }
        public (string Label, string Origin, string Phase)? Advice { get; set; }
        public LegalAction? Advised { get; set; }
        public LineDangerReport? AdviceReport { get; set; }
        public LineDangerReport? ActualReport { get; set; }
        public string? LethalLevel { get; set; }
        public int? RealNextMargin { get; set; }
        public int? RealNextBest { get; set; }
    }

    private sealed class GameOutcome
    {
        public string? Result { get; set; }
        public string? Conceded { get; set; }
        public int? EndedOnGameTurn { get; set; }
        public bool EndedOnLocalTurn { get; set; }
        public bool LocalHeroDead { get; set; }
        public Dictionary<int, int> EndOfTurnMargin { get; } = new();
    }

    public static int Run(Args a)
    {
        if (a.Positional.Count == 0)
            throw new ArgumentException("Usage : line-danger <journal1> [journal2…] [--traces <dossier>] [--out rapport.md] [--jsonl details.jsonl]");
        var (_, cards) = Commands.LoadEnvironment(a, quiet: true);
        var tracesDir = a.Get("traces") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HsCopilot", "advisor");
        var advice = LoadAdvice(tracesDir);

        var decisions = new List<Decision>();
        var outcomes = new Dictionary<string, GameOutcome>();
        foreach (var file in a.Positional.OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            foreach (var game in ReplayFile.SplitGames(ReadShared(file)))
                Replay(game, cards, decisions, outcomes);

        // Conseil affiché en premier pour l'état de la décision (même heure de log), relié à l'action légale par son libellé.
        foreach (var d in decisions)
        {
            if (!advice.TryGetValue((d.Game, d.Ctx.State.LastLogTime ?? ""), out var adv)) continue;
            d.Advice = adv;
            d.Advised = d.Ctx.LegalActions.FirstOrDefault(x => AdviceDisplay.Label(x, d.Ctx.State) == adv.Label);
        }

        var sw = Stopwatch.StartNew();
        var timings = new List<double>();
        foreach (var d in decisions)
        {
            var lethal = QuickAnalysis.AssessLethal(d.Ctx);
            d.LethalLevel = lethal?.Level;
            if (d.Advised is { } adv)
            {
                d.AdviceReport = LineDanger.Check(d.Ctx, adv, lethal);
                if (d.AdviceReport is { } r) timings.Add(r.ElapsedMs);
            }
            if (d.Actual is { } act) d.ActualReport = d.Advised?.Id == act.Id ? d.AdviceReport : LineDanger.Check(d.Ctx, act, lethal);
        }
        // État réel suivant dans le même tour : validation de la simulation du coup joué.
        for (int i = 0; i + 1 < decisions.Count; i++)
        {
            var d = decisions[i];
            var next = decisions[i + 1];
            if (next.Game != d.Game || next.GameTurn != d.GameTurn || d.ActualReport?.MarginAfterAction is null) continue;
            d.RealNextMargin = ThreatAssessment.Assess(next.Ctx)?.Margin;
            d.RealNextBest = LineDanger.Position(next.Ctx)?.Best;
        }
        var totalMs = sw.Elapsed.TotalMilliseconds;

        var report = Render(decisions, outcomes, timings, totalMs, a.Positional.Count, tracesDir);
        if (a.Get("cases") is string casesPath) report += FrozenCases(casesPath);
        var outPath = a.Get("out") ?? "line-danger.md";
        File.WriteAllText(outPath, report, new UTF8Encoding(false));
        if (a.Get("jsonl") is string jsonl) WriteJsonl(jsonl, decisions, outcomes);
        Console.WriteLine(string.Join('\n', report.Split('\n').Take(30)));
        Console.WriteLine($"… rapport complet : {outPath}");
        return 0;
    }

    // ------------------------------------------------------------------ Lecture

    private static void Replay(LoggedGame game, Core.Cards.ICardDatabase cards, List<Decision> decisions, Dictionary<string, GameOutcome> outcomes)
    {
        var engine = new GameEngine(new EngineOptions { Cards = cards });
        var projector = new GameStateProjector(cards);
        var lines = game.Lines;
        var perTurn = new Dictionary<int, int>();
        var mine = new List<Decision>();
        string? gameId = null;
        var outcome = new GameOutcome();
        for (int i = 0; i < lines.Count; i++)
        {
            var parsed = PowerLogLineParser.TryParse(lines[i]);
            if (parsed is null) continue;
            var localTurn = engine.LocalPlayerId is not null && engine.CurrentPlayerId == engine.LocalPlayerId;
            var turnBefore = engine.GameTurn;
            engine.Process(parsed);

            // Fin de ton tour : état réel transmis au tour adverse (effets de fin de tour compris).
            if (localTurn && engine.LocalPlayerId is not null && engine.CurrentPlayerId != engine.LocalPlayerId && !outcome.EndOfTurnMargin.ContainsKey(turnBefore))
            {
                var s = projector.Project(engine, new ProjectionContext { Transport = "replay-file" });
                if (ThreatAssessment.Assess(Bare(s)) is { } t) outcome.EndOfTurnMargin[turnBefore] = t.Margin;
            }
            if (outcome.EndedOnGameTurn is null && engine.HasGame && engine.LocalPlayerId is not null)
            {
                // Fin de partie : qui jouait, et ton héros était-il mort ?
                if (parsed.Body.Contains("PLAYSTATE", StringComparison.Ordinal) && (parsed.Body.Contains("value=WON") || parsed.Body.Contains("value=LOST") || parsed.Body.Contains("value=CONCEDED")))
                {
                    var s = projector.Project(engine, new ProjectionContext { Transport = "replay-file" });
                    if (s.Game.Result is not null)
                    {
                        outcome.EndedOnGameTurn = s.Game.GameTurn;
                        outcome.EndedOnLocalTurn = s.Game.ActivePlayer == "local";
                        outcome.LocalHeroDead = s.Local?.Hero is { } h && h.Health <= 0;
                    }
                }
            }

            if (parsed.Method != "DebugPrintOptions") continue;
            if (i + 1 < lines.Count && lines[i + 1].Contains("GameState.DebugPrintOptions()", StringComparison.Ordinal)) continue;
            if (engine.LocalPlayerId is null || engine.CurrentPlayerId != engine.LocalPlayerId || !engine.OptionsFresh) continue;
            var snapshot = projector.Project(engine, new ProjectionContext { Transport = "replay-file" });
            if (snapshot.Local?.HeroClass != "WARRIOR") return;
            gameId ??= snapshot.GameId;
            if (gameId is null || outcomes.ContainsKey(gameId) && !ReferenceEquals(outcomes[gameId], outcome)) return;   // partie déjà lue (renvoi de HDT)
            outcomes[gameId] = outcome;
            var ctx = DecisionContextBuilder.Build(snapshot);
            if (ctx is null || ctx.Kind != DecisionContext.KindAction || ctx.LegalActions.All(x => x.Kind == "end-turn")) continue;
            var rank = perTurn.GetValueOrDefault(engine.GameTurn) + 1;
            perTurn[engine.GameTurn] = rank;
            var actual = DatasetBuilder.FindActual(lines, i + 1, engine.CurrentOptions!, ctx);
            mine.Add(new Decision
            {
                Game = gameId, GameType = snapshot.Game.GameType ?? "?", GameTurn = snapshot.Game.GameTurn, Turn = snapshot.Game.TurnNumber, Rank = rank, Ctx = ctx,
                Actual = actual?.ActionId is string id ? ctx.LegalActions.FirstOrDefault(x => x.Id == id) : null,
            });
        }
        if (gameId is null) return;
        var final = projector.Project(engine, new ProjectionContext { Transport = "replay-file" });
        outcome.Result = final.Game.Result;
        outcome.Conceded = final.Game.Conceded;
        decisions.AddRange(mine);
    }

    private static DecisionContext Bare(GameStateSnapshot s) => new()
    {
        GameId = s.GameId ?? "?", Revision = s.Revision, OptionsId = 0, GameTurn = s.Game.GameTurn, ResetCount = s.Game.ResetCount, HistorySeq = 0, State = s,
    };

    private static Dictionary<(string Game, string Time), (string Label, string Origin, string Phase)> LoadAdvice(string dir)
    {
        var map = new Dictionary<(string, string), (string, string, string)>();
        if (!Directory.Exists(dir)) return map;
        foreach (var file in Directory.GetFiles(dir, "*.jsonl").OrderBy(f => f, StringComparer.Ordinal))
            foreach (var line in ReadShared(file))
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var r = doc.RootElement;
                    string? Str(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    var phase = Str("phase");
                    if (phase is not (AdvisorPhases.Partial or AdvisorPhases.Ready) || Str("action") is not string action || Str("gameId") is not string g || Str("stateLogTime") is not string t) continue;
                    if (Str("kind") is string kind && kind != DecisionContext.KindAction) continue;
                    map.TryAdd((g, t), (action, Str("origin") ?? AdviceOrigins.Model, phase));
                }
                catch (JsonException) { }
            }
        return map;
    }

    private static List<string> ReadShared(string path)
    {
        if (path.EndsWith(".hdtreplay", StringComparison.OrdinalIgnoreCase)) return ReplayFile.ReadAllLines(path);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(fs, Encoding.UTF8);
        var result = new List<string>();
        string? line;
        while ((line = r.ReadLine()) is not null) result.Add(line);
        return result;
    }

    // ------------------------------------------------------------------ Rapport

    private static string Outcome(Decision d, Dictionary<string, GameOutcome> outcomes)
    {
        var o = outcomes[d.Game];
        var result = o.Result == "WON" ? "gagnée" : o.Conceded == "local" ? "abandonnée" : o.Result == "LOST" ? "perdue" : "?";
        var next = o.EndedOnGameTurn == d.GameTurn + 1 && !o.EndedOnLocalTurn && o.LocalHeroDead && o.Result == "LOST" ? "mort au tour adverse suivant"
            : o.EndedOnGameTurn == d.GameTurn + 1 && o.Conceded == "local" ? "abandon au tour adverse suivant"
            : o.EndedOnGameTurn == d.GameTurn && o.Result == "WON" ? "victoire ce tour"
            : o.EndedOnGameTurn == d.GameTurn + 2 && o.Result == "WON" ? "victoire au tour suivant"
            : o.EndedOnGameTurn is int e && e > d.GameTurn + 1 ? "survit au tour adverse" : "?";
        return $"{next} ; partie {result}";
    }

    /// <summary>Tu as passé le tour adverse suivant : la partie continue après, ou tu l'as gagnée entre-temps.</summary>
    private static bool Survived(Decision d, Dictionary<string, GameOutcome> outcomes)
    {
        var o = outcomes[d.Game];
        if (o.EndedOnGameTurn is not int e) return false;
        return e > d.GameTurn + 1 || o.Result == "WON";
    }

    private static string Pct(int n, int d) => d == 0 ? "—" : string.Format(Fr, "{0} sur {1} ({2:0} %)", n, d, 100.0 * n / d);

    private static string Render(List<Decision> all, Dictionary<string, GameOutcome> outcomes, List<double> timings, double totalMs, int files, string tracesDir)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Danger au prochain tour adverse, avant et après chaque coup");
        sb.AppendLine();
        sb.AppendLine($"Journaux : {files} ; parties : {outcomes.Count} ; décisions d'action : {all.Count} ; traces : `{tracesDir}`.");
        sb.AppendLine();
        foreach (var type in all.Select(d => d.GameType).Distinct().OrderBy(x => x))
        {
            var g = all.Where(d => d.GameType == type).ToList();
            sb.AppendLine($"## {type}");
            sb.AppendLine();
            var advised = g.Where(d => d.AdviceReport is not null).ToList();
            sb.AppendLine($"- Décisions : {g.Count} ; avec un conseil affiché relié à une action légale : {advised.Count} (libellé non relié : {g.Count(d => d.Advice is not null && d.Advised is null)}).");
            sb.AppendLine($"- Origine du conseil : {string.Join(", ", advised.GroupBy(d => d.Advice!.Value.Origin).Select(x => $"{x.Key} {x.Count()}"))}.");
            sb.AppendLine();
            sb.AppendLine("### Verdicts sur les conseils affichés");
            sb.AppendLine();
            sb.AppendLine("| Verdict | Conseils | complète | partielle | non simulable | modèle | plan | solveur |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var v in advised.GroupBy(d => d.AdviceReport!.Verdict).OrderByDescending(x => x.Count()))
                sb.AppendLine($"| {v.Key} | {v.Count()} | {v.Count(d => d.AdviceReport!.Status == LineDangerReport.Complete)} | {v.Count(d => d.AdviceReport!.Status == LineDangerReport.Partial)} | {v.Count(d => d.AdviceReport!.Status == LineDangerReport.NotSimulable)} | {v.Count(d => d.Advice!.Value.Origin == AdviceOrigins.Model)} | {v.Count(d => d.Advice!.Value.Origin == AdviceOrigins.Plan)} | {v.Count(d => d.Advice!.Value.Origin == AdviceOrigins.Solver)} |");
            sb.AppendLine();
            var played = g.Where(d => d.ActualReport is not null).ToList();
            sb.AppendLine("### Verdicts sur les coups joués");
            sb.AppendLine();
            sb.AppendLine("| Verdict | Coups | complète | partielle | non simulable | coup = conseil |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var v in played.GroupBy(d => d.ActualReport!.Verdict).OrderByDescending(x => x.Count()))
                sb.AppendLine($"| {v.Key} | {v.Count()} | {v.Count(d => d.ActualReport!.Status == LineDangerReport.Complete)} | {v.Count(d => d.ActualReport!.Status == LineDangerReport.Partial)} | {v.Count(d => d.ActualReport!.Status == LineDangerReport.NotSimulable)} | {v.Count(d => d.Advised?.Id == d.Actual?.Id)} |");
            sb.AppendLine();

            // Validation : coup joué simulé contre l'état réel suivant.
            var val = g.Where(d => d.RealNextMargin is not null && d.ActualReport?.MarginAfterAction is not null).ToList();
            sb.AppendLine("### Validation de la simulation contre l'état réel suivant (coup joué, même tour)");
            sb.AppendLine();
            sb.AppendLine("| Statut du coup | Transitions | marge identique | meilleure marge identique |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var st in new[] { LineDangerReport.Complete, LineDangerReport.Partial })
            {
                var v = val.Where(d => d.ActualReport!.Status == st).ToList();
                sb.AppendLine($"| {st} | {v.Count} | {Pct(v.Count(d => d.RealNextMargin == d.ActualReport!.MarginAfterAction), v.Count)} | {Pct(v.Count(d => d.RealNextBest == d.ActualReport!.BestAfter), v.Count)} |");
            }
            sb.AppendLine();
            var wrong = val.Where(d => d.ActualReport!.Status == LineDangerReport.Complete && d.RealNextMargin != d.ActualReport!.MarginAfterAction).Take(12).ToList();
            if (wrong.Count > 0)
            {
                sb.AppendLine("Écarts sur des transitions « complètes » (à expliquer) :");
                sb.AppendLine();
                foreach (var d in wrong)
                    sb.AppendLine($"- {d.Game} tour {d.Turn} : {d.Actual!.Label} ; simulé {d.ActualReport!.MarginAfterAction}, réel {d.RealNextMargin}");
                sb.AppendLine();
            }
        }

        // Signalements stricts et nets, conseils puis coups joués.
        void Flags(string title, Func<Decision, LineDangerReport?> pick, Func<Decision, LegalAction?> action, string verdict)
        {
            var f = all.Where(d => pick(d)?.Verdict == verdict).ToList();
            sb.AppendLine($"## {title} : {f.Count}");
            sb.AppendLine();
            if (f.Count == 0) return;
            sb.AppendLine("| Partie | Type | Tour | Coup | Origine | Joué | Marge maintenant → après | Meilleure défense avant → après | Ligne défensive avant | Non simulé | Issue |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var d in f)
            {
                var r = pick(d)!;
                var a = action(d)!;
                sb.AppendLine($"| {d.Game} | {d.GameType.Replace("GT_", "")} | {d.Turn}.{d.Rank} | {a.Label} | {(d.Advised?.Id == a.Id ? d.Advice?.Origin : "—")} | {(d.Actual?.Id == a.Id ? "oui" : d.Actual?.Label ?? "?")} | {r.MarginNow} → {r.MarginAfterAction} | {r.BestBefore} → {r.BestAfter} | {string.Join(", puis ", r.BestLineBefore.Select(x => x.Label))} | {string.Join(" ; ", r.Unmodeled)} | {Outcome(d, outcomes)} |");
            }
            sb.AppendLine();
        }
        Flags("Conseils qui perdent la dernière défense simulée", d => d.AdviceReport, d => d.Advised, LineDangerReport.LosesLastDefense);
        Flags("Coups joués qui perdent la dernière défense simulée", d => d.Advised?.Id == d.Actual?.Id ? null : d.ActualReport, d => d.Actual, LineDangerReport.LosesLastDefense);
        Flags("Conseils qui aggravent nettement le danger (critère large, non retenu)", d => d.AdviceReport, d => d.Advised, LineDangerReport.Worsens);

        // Fin de tour réelle : défense disponible au début du tour, létal visible à la fin.
        sb.AppendLine("## Tours finis avec un létal adverse visible");
        sb.AppendLine();
        sb.AppendLine("| Partie | Type | Tour | Meilleure défense au début du tour | Marge réelle en fin de tour | Coup qui la perd | Conseillé | Issue |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        var turns = all.GroupBy(d => (d.Game, d.GameTurn)).ToList();
        int lostTurns = 0, endLethal = 0, endLethalSurvived = 0;
        foreach (var t in turns)
        {
            var first = t.First();
            if (!outcomes[t.Key.Game].EndOfTurnMargin.TryGetValue(t.Key.GameTurn, out var end) || end > 0) continue;
            endLethal++;
            if (Survived(first, outcomes)) endLethalSurvived++;
            var startBest = first.ActualReport?.BestBefore ?? first.AdviceReport?.BestBefore;
            var culprit = t.FirstOrDefault(d => d.ActualReport?.Verdict == LineDangerReport.LosesLastDefense);
            if (startBest > 0) lostTurns++;
            sb.AppendLine($"| {t.Key.Game} | {first.GameType.Replace("GT_", "")} | {first.Turn} | {startBest?.ToString() ?? "?"} | {end} | {culprit?.Actual?.Label ?? (startBest > 0 ? "non attribué (coup non simulable ou effet adverse)" : "—")} | {(culprit is null ? "" : culprit.Advised?.Id == culprit.Actual?.Id ? culprit.Advice?.Origin ?? "sans conseil" : culprit.Advice is null ? "sans conseil" : "non")} | {Outcome(first, outcomes)} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Tours finis avec un létal visible : {endLethal}, dont {lostTurns} avec une défense simulée disponible au début du tour ; survécu au tour adverse malgré tout : {endLethalSurvived}.");
        sb.AppendLine();

        // Séquence du tour : meilleure défense simulée au début, marge réelle à la fin (le conseil est suivi dans l'immense majorité des coups).
        sb.AppendLine("## Tours avec danger : meilleure défense au début, marge réelle à la fin");
        sb.AppendLine();
        sb.AppendLine("| Partie | Type | Tour | Marge au début | Meilleure défense au début | Marge réelle en fin de tour | Perte | Coups conseillés suivis | Issue |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        int dangerTurns = 0, lossTurns = 0;
        foreach (var t in turns)
        {
            var first = t.First();
            var r0 = first.ActualReport ?? first.AdviceReport;
            if (r0 is null || r0.Verdict == LineDangerReport.LethalThisTurn || outcomes[t.Key.Game] is { Result: "WON" } o0 && o0.EndedOnGameTurn == t.Key.GameTurn) continue;
            var hasEnd = outcomes[t.Key.Game].EndOfTurnMargin.TryGetValue(t.Key.GameTurn, out var end);
            if (r0.MarginNow > 5 && r0.BestBefore > 5 && (!hasEnd || end > 5)) continue;
            dangerTurns++;
            var loss = hasEnd ? r0.BestBefore - end : (int?)null;
            if (loss >= 3 && end <= 2) lossTurns++;
            var followed = t.Count(d => d.Advised is not null && d.Advised.Id == d.Actual?.Id);
            sb.AppendLine($"| {t.Key.Game} | {first.GameType.Replace("GT_", "")} | {first.Turn} | {r0.MarginNow} | {r0.BestBefore} | {(hasEnd ? end.ToString() : "fin de partie")} | {loss?.ToString() ?? "?"} | {followed} sur {t.Count()} | {Outcome(first, outcomes)} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Tours avec danger : {dangerTurns} ; perte d'au moins 3 de marge pour finir à 2 ou moins : {lossTurns}.");
        sb.AppendLine();

        // Contre-exemples : danger élevé, coup offensif conseillé, issue.
        sb.AppendLine("## Danger élevé mais non létal (marge 1 à 5) et conseil offensif");
        sb.AppendLine();
        var high = all.Where(d => d.AdviceReport is { MarginNow: >= 1 and <= 5 } && d.Advised is { } adv && adv.Kind == "attack" && adv.TargetSide == "opponent" && adv.TargetEntityId == d.Ctx.State.Opponent?.Hero?.EntityId).ToList();
        sb.AppendLine($"Conseils d'attaque au visage avec une marge de 1 à 5 : {high.Count} ; survécu au tour adverse : {high.Count(d => Survived(d, outcomes))} ; verdicts : {string.Join(", ", high.GroupBy(d => d.AdviceReport!.Verdict).Select(x => $"{x.Key} {x.Count()}"))}.");
        sb.AppendLine();

        // Toutes les décisions conseillées où le danger compte : revue au cas par cas.
        sb.AppendLine("## Revue : décisions conseillées avec un danger simulé");
        sb.AppendLine();
        sb.AppendLine("| Partie | Type | Tour | Conseil | Origine | Joué | Verdict | Statut | Marge maintenant → après | Meilleure défense avant → après | Défense simulée avant | Non simulé | Issue |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var d in all.Where(d => d.AdviceReport is { } r && r.Verdict is not (LineDangerReport.NoDanger or LineDangerReport.LethalThisTurn)))
        {
            var r = d.AdviceReport!;
            sb.AppendLine($"| {d.Game} | {d.GameType.Replace("GT_", "")} | {d.Turn}.{d.Rank} | {d.Advised!.Label} | {d.Advice?.Origin} | {(d.Actual?.Id == d.Advised.Id ? "oui" : d.Actual?.Label ?? "?")} | {r.Verdict} | {r.Status} | {r.MarginNow} → {r.MarginAfterAction?.ToString() ?? "?"} | {r.BestBefore} → {r.BestAfter?.ToString() ?? "?"} | {string.Join(", puis ", r.BestLineBefore.Select(x => x.Label))} | {string.Join(" ; ", r.Unmodeled)} | {Outcome(d, outcomes)} |");
        }
        sb.AppendLine();

        // Défaites : les deux derniers tours.
        sb.AppendLine("## Défaites : verdicts des deux derniers tours");
        sb.AppendLine();
        foreach (var (game, o) in outcomes.Where(kv => kv.Value.Result == "LOST"))
        {
            var last = all.Where(d => d.Game == game).Select(d => d.GameTurn).DefaultIfEmpty().Max();
            sb.AppendLine($"- **{game}** ({(o.Conceded == "local" ? "abandon" : o.LocalHeroDead ? "mort" : "défaite")} au tour de jeu {o.EndedOnGameTurn}) ; marge réelle en fin de tes deux derniers tours : {string.Join(", ", o.EndOfTurnMargin.Where(kv => kv.Key >= last - 2).OrderBy(kv => kv.Key).Select(kv => kv.Value.ToString()))}");
            foreach (var d in all.Where(d => d.Game == game && d.GameTurn >= last - 2))
            {
                var r = d.ActualReport;
                sb.AppendLine($"  - tour {d.Turn}.{d.Rank} : joué « {d.Actual?.Label ?? "?"} » ({(d.Advice is null ? "sans conseil" : d.Advised?.Id == d.Actual?.Id ? $"conseil suivi, {d.Advice.Value.Origin}" : $"conseil : {d.Advised?.Label}")}) → {r?.Verdict ?? "?"}, {r?.Status}, marge {r?.MarginNow} → {r?.MarginAfterAction?.ToString() ?? "?"}, meilleure défense {r?.BestBefore} → {r?.BestAfter?.ToString() ?? "?"}{(r is { Unmodeled.Count: > 0 } ? " ; non simulé : " + string.Join(" ; ", r.Unmodeled.Take(3)) : "")}");
            }
        }
        sb.AppendLine();

        // Contre-exemples sur les coups joués : visage malgré une marge faible.
        var faceLow = all.Where(d => d.Actual is { Kind: "attack" } act && act.TargetEntityId == d.Ctx.State.Opponent?.Hero?.EntityId && d.ActualReport is { MarginNow: <= 5 } && d.LethalLevel != LethalLevels.Certain).ToList();
        sb.AppendLine($"## Attaques au visage jouées avec une marge de 5 ou moins (hors létal certain) : {faceLow.Count}");
        sb.AppendLine();
        foreach (var d in faceLow)
        {
            var r = d.ActualReport!;
            sb.AppendLine($"- {d.Game} tour {d.Turn}.{d.Rank} ({d.GameType.Replace("GT_", "")}) : « {d.Actual!.Label} » ({(d.Advice is null ? "sans conseil" : d.Advised?.Id == d.Actual.Id ? "conseillé" : "non conseillé")}) ; marge {r.MarginNow} → {r.MarginAfterAction}, meilleure défense {r.BestBefore} → {r.BestAfter} ; {r.Verdict}, {r.Status} ; {Outcome(d, outcomes)}");
        }
        sb.AppendLine();

        // Coût.
        timings.Sort();
        double P(double q) => timings.Count == 0 ? 0 : timings[Math.Min(timings.Count - 1, (int)(q * timings.Count))];
        sb.AppendLine("## Coût du calcul");
        sb.AppendLine();
        var reports = all.SelectMany(d => new[] { d.AdviceReport, d.ActualReport }).Where(r => r is not null).Distinct().ToList();
        sb.AppendLine(string.Format(Fr, "Par conseil : médiane {0:0.00} ms, P95 {1:0.00} ms, maximum {2:0.0} ms ; recherche tronquée : {3} ; total de l'analyse : {4:0} ms.",
            P(0.5), P(0.95), timings.LastOrDefault(), reports.Count(r => r!.Truncated), totalMs));
        return sb.ToString();
    }

    /// <summary>Positions figées (bench build) : coup joué seulement, sans conseil ni issue ; mesure la fréquence des verdicts.</summary>
    private static string FrozenCases(string path)
    {
        var sb = new StringBuilder();
        var cases = DatasetBuilder.Load(path);
        var rows = new List<(DecisionCase Case, LineDangerReport Report, LegalAction Played)>();
        var sw = Stopwatch.StartNew();
        foreach (var c in cases)
        {
            DecisionContext ctx;
            try { ctx = BenchRunner.BuildContext(c); } catch { continue; }
            if (ctx.LegalActions.FirstOrDefault(x => x.Id == c.Actual?.ActionId) is not { } played) continue;
            if (LineDanger.Check(ctx, played) is { } r) rows.Add((c, r, played));
        }
        sb.AppendLine();
        sb.AppendLine($"## Positions figées : {Path.GetFileName(path)} ({rows.Count} coups joués, {sw.Elapsed.TotalMilliseconds:0} ms)");
        sb.AppendLine();
        foreach (var type in rows.Select(r => r.Case.GameType ?? "?").Distinct().OrderBy(x => x))
        {
            var g = rows.Where(r => (r.Case.GameType ?? "?") == type).ToList();
            sb.AppendLine($"- {type} : {g.Count} coups ; " + string.Join(" ; ", g.GroupBy(r => r.Report.Verdict).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()} (complète {x.Count(r => r.Report.Status == LineDangerReport.Complete)})")));
        }
        sb.AppendLine();
        foreach (var r in rows.Where(r => r.Report.Verdict is LineDangerReport.LosesLastDefense or LineDangerReport.Worsens))
            sb.AppendLine($"- {r.Case.CaseId} ({r.Case.GameType}, tour {r.Case.TurnNumber}) : « {r.Played.Label} » → {r.Report.Verdict}, {r.Report.Status} ; marge {r.Report.MarginNow} → {r.Report.MarginAfterAction}, meilleure défense {r.Report.BestBefore} → {r.Report.BestAfter} ; défense : {string.Join(", puis ", r.Report.BestLineBefore.Select(x => x.Label))} ; non simulé : {string.Join(" ; ", r.Report.Unmodeled)}");
        return sb.ToString();
    }

    private static void WriteJsonl(string path, List<Decision> all, Dictionary<string, GameOutcome> outcomes)
    {
        object? R(LineDangerReport? r) => r is null ? null : new
        {
            verdict = r.Verdict, status = r.Status, marginNow = r.MarginNow, bestBefore = r.BestBefore, marginAfter = r.MarginAfterAction, bestAfter = r.BestAfter,
            lineBefore = r.BestLineBefore.Select(x => x.Label), lineAfter = r.BestLineAfter.Select(x => x.Label), unmodeled = r.Unmodeled, ms = Math.Round(r.ElapsedMs, 3), nodes = r.Nodes,
        };
        File.WriteAllLines(path, all.Select(d => JsonSerializer.Serialize(new
        {
            game = d.Game, gameType = d.GameType, gameTurn = d.GameTurn, turn = d.Turn, rank = d.Rank, logTime = d.Ctx.State.LastLogTime, lethal = d.LethalLevel,
            advice = d.Advice?.Label, origin = d.Advice?.Origin, advised = d.Advised?.Label, actual = d.Actual?.Label, followed = d.Advised is not null && d.Advised.Id == d.Actual?.Id,
            adviceReport = R(d.AdviceReport), actualReport = d.Advised?.Id == d.Actual?.Id ? null : R(d.ActualReport),
            realNextMargin = d.RealNextMargin, realNextBest = d.RealNextBest, outcome = Outcome(d, outcomes),
            endOfTurnMargin = outcomes[d.Game].EndOfTurnMargin.TryGetValue(d.GameTurn, out var e) ? e : (int?)null,
        }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
    }
}
