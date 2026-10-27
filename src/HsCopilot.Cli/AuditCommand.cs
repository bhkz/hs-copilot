using System.Globalization;
using System.Text.Json;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Cli;

/// <summary>
/// Audit d'une partie jouée avec le copilote : rejoue les journaux de la session avec le même moteur (mêmes révisions)
/// et confronte chaque entrée de la trace du conseiller à l'état réel : pioche intégrée ou non, options à jour, âge du
/// conseil, coup réellement joué ensuite.
/// </summary>
public static class AuditCommand
{
    private sealed record RevInfo(int Revision, int Line, TimeSpan Time);

    public static int Run(Args a)
    {
        if (a.Positional.Count < 2) throw new ArgumentException("Usage : advisor-audit <trace.jsonl> <journal1> [journal2…] [--initial-revision 1]");
        var (_, cards) = Commands.LoadEnvironment(a, quiet: true);
        var trace = ReadShared(a.Positional[0]).Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Deserialize<AdvisorTraceEntry>(l, Json.Read)!).ToList();
        var gameId = trace.Select(t => t.GameId).LastOrDefault(g => g is not null && g != "aucune");
        var lines = a.Positional.Skip(1).SelectMany(ReadShared).ToList();
        var engine = new GameEngine(new EngineOptions { Cards = cards, InitialRevision = a.GetInt("initial-revision") ?? 1 });
        var projector = new GameStateProjector(cards);

        // Rejeu ligne à ligne : révisions atteintes, pioches du joueur local, envois du client.
        var revisions = new Dictionary<int, RevInfo>();
        var snapshots = new Dictionary<int, GameStateSnapshot>();
        var wanted = trace.Where(t => t.GameId == gameId).Select(t => t.Revision).ToHashSet();
        if (a.Get("dump") is string extra) foreach (var r in extra.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse)) wanted.Add(r);
        var draws = new List<(int Turn, TimeSpan Time, int Revision, int EntityId, string? CardId)>();
        var sends = new List<(TimeSpan Time, int Revision, string Body)>();
        var choices = new List<(int Revision, List<int> Chosen)>();
        var zones = new Dictionary<int, string>();
        var drawTurn = -1;
        // Synchronisation avec l'écran : début de ton tour côté serveur (GameState) et exécution par le client (PowerTaskList).
        var serverTurn = new Dictionary<int, TimeSpan>();
        // Une nouvelle partie côté client (CREATE_GAME dans PowerTaskList) ouvre un segment : les numéros de tour repartent.
        // Un Power.log complet (--visual) ou plusieurs journaux contiennent plusieurs parties.
        var screenGames = new List<Dictionary<int, TimeSpan>> { new() };
        var clock = new VisualClock();
        clock.Changed += vp =>
        {
            if (vp.Turn is null && vp.Step is null) { if (screenGames[^1].Count > 0) screenGames.Add(new()); return; }
            if (vp.Turn is int vt && vp.Step == "MAIN_ACTION") screenGames[^1].TryAdd(vt, vp.LogTime);
        };
        if (a.Get("visual") is string visualPath)
            foreach (var l in ReadShared(visualPath)) clock.OnLine(l);
        var drawPending = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var p = PowerLogLineParser.TryParse(lines[i]);
            if (p is null) { if (a.Get("visual") is null) clock.OnLine(lines[i]); continue; }
            var before = engine.Revision;
            engine.Process(p);
            if (p.Method == "SendOption" && p.Body.StartsWith("selectedOption=", StringComparison.Ordinal)) sends.Add((p.Time, engine.Revision, p.Body));
            if (p.Method == "SendChoices")
            {
                if (p.Body.StartsWith("id=", StringComparison.Ordinal)) choices.Add((engine.Revision, new List<int>()));
                else if (choices.Count > 0 && p.Body.Contains("m_chosenEntities", StringComparison.Ordinal))
                {
                    var idx = p.Body.IndexOf(" id=", StringComparison.Ordinal);
                    if (idx > 0 && int.TryParse(new string(p.Body[(idx + 4)..].TakeWhile(char.IsDigit).ToArray()), out var eid)) choices[^1].Chosen.Add(eid);
                }
            }
            if (engine.Revision != before) revisions.TryAdd(engine.Revision, new RevInfo(engine.Revision, i, p.Time));
            // Suivi des zones à chaque ligne (une révision peut regrouper l'échange du mulligan et la pioche du tour).
            if (engine.GameId == gameId && engine.LocalPlayerId is int local)
            {
                if (engine.CurrentPlayerId == local && engine.Step == "MAIN_ACTION") serverTurn.TryAdd(engine.GameTurn, p.Time);
                // Pioche du tour : même règle que le validateur en direct. Au début d'un tour local (mulligan terminé), la
                // première carte qui passe de DECK à HAND est la pioche ; les cartes reçues au mulligan ne sont pas comptées.
                if (engine.CurrentPlayerId == local && engine.GameTurn != drawTurn && engine.Step is not (null or "BEGIN_MULLIGAN" or "INVALID"))
                {
                    drawTurn = engine.GameTurn;
                    drawPending = true;
                }
                foreach (var e in engine.Entities.Values.Where(e => e.Controller == local))
                {
                    var z = e.Zone;
                    if (drawPending && zones.TryGetValue(e.Id, out var prev) && prev == "DECK" && z == "HAND")
                    {
                        draws.Add(((engine.GameTurn + 1) / 2, p.Time, engine.Revision, e.Id, e.CardId));
                        drawPending = false;
                    }
                    zones[e.Id] = z;
                }
            }
            // Dernier état de chaque révision suivie (les options du serveur arrivent sans changer de révision).
            if (wanted.Contains(engine.Revision) && engine.GameId == gameId && (p.Method is "DebugPrintOptions" or "DebugPrintEntityChoices" || engine.Revision != before))
                snapshots[engine.Revision] = projector.Project(engine, new ProjectionContext { Transport = "audit" });
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Audit du conseiller : partie {gameId}");
        sb.AppendLine();
        sb.AppendLine("Heures locales du log. « Âge » : délai entre la dernière ligne de l'état analysé et l'affichage.");
        sb.AppendLine();
        sb.AppendLine("| Heure | Tour | Rév. | Phase | Contenu | Pioche du tour intégrée | Âge |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var t in trace.Where(t => t.GameId == gameId && t.Phase is "analyse" or "partiel" or "prêt" or "ignoré"))
        {
            var local = t.Utc.ToLocalTime().TimeOfDay;
            revisions.TryGetValue(t.Revision, out var info);
            snapshots.TryGetValue(t.Revision, out var snap);
            var turnDraws = draws.Where(d => d.Turn == t.TurnNumber).ToList();
            string drawn = "n/a";
            if (snap is not null && turnDraws.Count > 0 && t.Kind is null or "action")
            {
                var first = turnDraws[0];
                var inState = first.Revision <= t.Revision;
                drawn = inState ? $"oui ({CardName(cards, first.CardId)} à {first.Time:hh\\:mm\\:ss\\.f})" : $"NON ({CardName(cards, first.CardId)} piochée à {first.Time:hh\\:mm\\:ss\\.f}, après l'état analysé)";
            }
            var age = info is null ? "?" : $"{(local - info.Time).TotalSeconds:0.0} s";
            var content = t.Phase switch
            {
                "prêt" => $"{t.Action} : {t.Summary}",
                "partiel" => $"(début de réponse) {t.Action}",
                _ => t.Message ?? "",
            };
            sb.AppendLine($"| {local:hh\\:mm\\:ss\\.f} | {t.TurnNumber} | {t.Revision} | {t.Phase} | {content.Replace("|", "/")} | {(t.Phase == "analyse" ? drawn : "")} | {(t.Phase is "prêt" or "partiel" ? age : "")} |");
        }
        sb.AppendLine();
        sb.AppendLine("## Coups joués après chaque conseil complet");
        sb.AppendLine();
        // Un conseil retenu puis republié quand ton tour s'affiche apparaît deux fois dans la trace : une seule ligne ici.
        foreach (var t in trace.Where(t => t.GameId == gameId && t.Phase == "prêt").DistinctBy(t => (t.Revision, t.Action)))
        {
            snapshots.TryGetValue(t.Revision, out var snap);
            string played;
            if (t.Kind is "mulligan" or "choice")
            {
                var c = choices.FirstOrDefault(x => x.Revision >= t.Revision);
                string NameOf(int id) => snap?.PendingChoice?.Offered.FirstOrDefault(o => o.EntityId == id)?.Name ?? $"#{id}";
                if (c.Chosen is null) played = "?";
                else if (t.Kind == "mulligan")
                {
                    // Au mulligan, les entités « choisies » envoyées au serveur sont les cartes GARDÉES : les autres
                    // retournent dans le deck (vérifié par les changements de zone HAND → DECK).
                    var offered = snap?.PendingChoice?.Offered.Where(o => !HsCopilot.Core.Cards.CardRules.IsCoin(o.CardId)).Select(o => o.EntityId).ToList() ?? new();
                    played = $"gardées : {string.Join(", ", c.Chosen.Select(NameOf))} ; remplacées : {string.Join(", ", offered.Where(o => !c.Chosen.Contains(o)).Select(NameOf))}";
                }
                else played = "choisi : " + string.Join(", ", c.Chosen.Select(NameOf));
            }
            else
            {
                var next = sends.FirstOrDefault(s => s.Revision >= t.Revision);
                played = snap is null || next.Body is null ? "?" : DescribeSend(snap, next.Body);
            }
            sb.AppendLine($"- Tour {t.TurnNumber}, rév. {t.Revision} : conseil « {t.Action} » ; joué ensuite : {played}");
        }
        // Suites du plan affichées sans appel : la règle actuelle (rien de nouveau, aucun effet non prévu) les accepte-t-elle ?
        var planned = trace.Where(t => t.GameId == gameId && t.Phase == "prêt" && t.Origin == AdviceOrigins.Plan).DistinctBy(t => t.Revision).ToList();
        if (planned.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Suites du plan affichées sans appel");
            sb.AppendLine();
            sb.AppendLine("| Tour | Rév. | Étape affichée | Après le conseil | Règle actuelle |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var t in planned)
            {
                // Référence : le dernier conseil affiché avant l'étape, complet ou début de réponse (appel conservé, dont la
                // réponse complète n'est jamais affichée), et l'action réellement envoyée au serveur ensuite.
                var prev = trace.LastOrDefault(r => r.GameId == gameId && r.Phase is "prêt" or "partiel" && r.Revision < t.Revision && r.Kind is null or "action");
                var before = prev is null ? null : snapshots.GetValueOrDefault(prev.Revision);
                snapshots.TryGetValue(t.Revision, out var now);
                var prevAction = before is null ? null : SentAction(before, sends.FirstOrDefault(x => x.Revision >= prev!.Revision).Body)
                    ?? DecisionContextBuilder.Build(before)?.LegalActions.FirstOrDefault(x => AdviceDisplay.Label(x, before) == prev!.Action);
                var verdict = before is null || now is null || prevAction is null ? "?"
                    : !StrategyCoordinator.NothingNew(before, now) ? "refusée : information nouvelle"
                    : StrategyCoordinator.UnplannedEffect(before, now, prevAction) is { } effect ? "refusée : effet non prévu, " + effect
                    : "acceptée";
                sb.AppendLine($"| {t.TurnNumber} | {t.Revision} | {t.Action} | {prev?.Action} (rév. {prev?.Revision}) | {verdict} |");
            }
        }
        // Débuts de réponse sans conseil complet : l'appel a été annulé. Si tu as joué leur action telle quelle et que rien
        // d'imprévu n'est apparu, l'appel est désormais conservé et sa suite du plan reprise sans nouvel appel si elle est légale.
        var partialOnly = trace.Where(t => t.GameId == gameId && t.Phase == "partiel" && t.Kind is null or "action"
                && !trace.Any(r => r.GameId == gameId && r.Phase == "prêt" && r.Revision == t.Revision))
            .DistinctBy(t => t.Revision).ToList();
        if (partialOnly.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Débuts de réponse sans conseil complet");
            sb.AppendLine();
            sb.AppendLine("L'appel a été annulé avant sa réponse complète. « Attendu » : tu as joué l'action du début de réponse et rien d'imprévu n'est apparu (même règle que le conseiller) ; l'appel est désormais conservé et sa suite du plan reprise sans nouvel appel si elle est légale.");
            sb.AppendLine();
            sb.AppendLine("| Tour | Rév. | Début de réponse | Décision suivante | Changement | Coup joué ensuite |");
            sb.AppendLine("|---|---|---|---|---|---|");
            var expected = 0;
            var newCalls = 0;
            foreach (var t in partialOnly)
            {
                snapshots.TryGetValue(t.Revision, out var snap);
                var analysed = snap is null ? null : DecisionContextBuilder.Build(snap);
                var advised = analysed?.LegalActions.FirstOrDefault(x => AdviceDisplay.Label(x, snap) == t.Action);
                var next = trace.FirstOrDefault(r => r.GameId == gameId && r.Utc > t.Utc && r.Revision > t.Revision && r.Phase is "analyse" or "ignoré");
                var nextSnap = next is null ? null : snapshots.GetValueOrDefault(next.Revision);
                var verdict = analysed is null || advised is null || nextSnap is null ? "?" : StrategyCoordinator.UnexpectedChange(analysed, advised, nextSnap) ?? "attendu";
                if (verdict == "attendu")
                {
                    expected++;
                    if (next!.Phase == "analyse") { newCalls++; verdict = "attendu : nouvel appel évité si la suite est légale"; }
                    else verdict = "attendu (il ne restait qu'à finir le tour)";
                }
                var send = next is null ? default : sends.FirstOrDefault(x => x.Revision >= next.Revision);
                var then = nextSnap is null || send.Body is null ? "" : DescribeSend(nextSnap, send.Body);
                sb.AppendLine($"| {t.TurnNumber} | {t.Revision} | {t.Action} | {(next is null ? "—" : $"rév. {next.Revision} ({next.Phase})")} | {verdict} | {then} |");
            }
            sb.AppendLine();
            sb.AppendLine($"Débuts de réponse joués tels quels, sans imprévu : {expected} sur {partialOnly.Count}, dont {newCalls} suivis d'un nouvel appel.");
        }
        // Segment de l'écran de la partie auditée : celui dont les tours s'affichent 0 à 2 min après leur début côté serveur.
        static bool Follows(TimeSpan screen, TimeSpan server)
        {
            var d = (screen - server).TotalSeconds;
            if (d < -43_200) d += 86_400;
            return d is >= 0 and <= 120;
        }
        int Matches(Dictionary<int, TimeSpan> g) => serverTurn.Count(kv => g.TryGetValue(kv.Key, out var sc) && Follows(sc, kv.Value));
        var screenTurn = screenGames.MaxBy(Matches) is { } best && Matches(best) > 0 ? best : new Dictionary<int, TimeSpan>();
        // Synchronisation avec l'écran, si des lignes PowerTaskList sont disponibles (journal récent ou --visual Power.log).
        if (screenTurn.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Synchronisation avec l'écran");
            sb.AppendLine();
            sb.AppendLine("« Tour affiché » : le client exécute l'étape MAIN_ACTION de ton tour (PowerTaskList), au plus près de la fin des animations du tour adverse. L'instant exact où l'image change n'est pas dans les logs.");
            sb.AppendLine();
            sb.AppendLine("| Tour | Début serveur | Analyse lancée | Premier affichage | Conseil complet | Tour affiché | Affiché avant l'écran | Conseil disponible après l'écran |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            foreach (var (gt, server) in serverTurn.OrderBy(kv => kv.Key))
            {
                var tn = (gt + 1) / 2;
                var turnRows = trace.Where(t => t.GameId == gameId && t.TurnNumber == tn && t.Kind is null or "action").ToList();
                var first = turnRows.FirstOrDefault(t => t.Phase is "analyse" or "partiel" or "prêt");
                var shown = turnRows.FirstOrDefault(t => t.Phase is "partiel" or "prêt");
                var ready = turnRows.FirstOrDefault(t => t.Phase == "prêt");
                TimeSpan? L(AdvisorTraceEntry? e) => e?.Utc.ToLocalTime().TimeOfDay;
                screenTurn.TryGetValue(gt, out var screen);
                string F(TimeSpan? x) => x is TimeSpan v ? v.ToString(@"hh\:mm\:ss\.f") : "—";
                var early = L(shown) is TimeSpan sh && screen != default ? $"{(screen - sh).TotalSeconds:0.0} s" : "—";
                var after = L(ready) is TimeSpan rd && screen != default ? $"{Math.Max(0, (rd - screen).TotalSeconds):0.0} s" : "—";
                sb.AppendLine($"| {tn} | {F(server)} | {F(L(first))} | {F(L(shown))} | {F(L(ready))} | {F(screen == default ? null : screen)} | {early} | {after} |");
            }
        }

        // Calcul local (solveur) à chaque point de décision du tour : ce que la version actuelle affiche, et la mise en garde
        // qu'elle ajouterait au conseil donné à l'époque.
        sb.AppendLine();
        sb.AppendLine("## Calcul local aux points de décision (version actuelle)");
        sb.AppendLine();
        sb.AppendLine("| Tour | Rév. | PV adverses | Attaques seules | Dégâts max | Létal | Séquence du solveur | Conseil de l'IA à l'époque | Mise en garde ajoutée | Calcul |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var rev in trace.Where(t => t.GameId == gameId && t.Phase is "analyse" or "ignoré").Select(t => t.Revision).Distinct().OrderBy(r => r))
        {
            if (!snapshots.TryGetValue(rev, out var snap) || DecisionContextBuilder.Build(snap) is not { } ctx || ctx.Kind != DecisionContext.KindAction) continue;
            var lethal = QuickAnalysis.AssessLethal(ctx);
            var r = lethal.Solver!;
            var advised = trace.LastOrDefault(t => t.GameId == gameId && t.Revision == rev && t.Phase is "prêt" or "partiel")?.Action;
            string caution = "";
            if (advised is not null && ctx.LegalActions.FirstOrDefault(x => advised.StartsWith(x.Label, StringComparison.Ordinal)) is { } la)
            {
                var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = la.Id, Summary = "audit", Confidence = "moyenne" }, ctx, "audit", null, 0, null, lethal).Recommendation;
                caution = rec?.Caution ?? "";
            }
            var shown = StrategyCoordinator.LocalLethalRecommendation(ctx, lethal) is { } local ? $"létal affiché sans appel : {local.Action.Label}" : "";
            var seq = r.Level == LethalLevels.Conditional && r.ConditionalSequence.Count > 0 ? string.Join(", puis ", r.ConditionalSequence.Select(x => x.Label)) : r.SequenceText;
            var level = r.Level + (r.Conditions.Count > 0 ? $" ({string.Join(" ; ", r.Conditions)})" : "") + (r.LongShots.Count > 0 ? $" ; peu probable : {string.Join(" ; ", r.LongShots)}" : "");
            sb.AppendLine($"| {snap.Game.TurnNumber} | {rev} | {r.Needed} | {r.AttacksOnlyDamage} | {r.MaxFaceDamage} | {level.Replace("|", "/")} | {seq.Replace("|", "/")} | {(advised ?? "(aucun)").Replace("|", "/")} | {(shown.Length > 0 ? shown : caution).Replace("|", "/")} | {r.ElapsedMs:0.0} ms |");
        }

        if (a.Get("dump") is string dump)
        {
            foreach (var r in dump.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
            {
                if (!snapshots.TryGetValue(r, out var snap)) { wanted.Add(r); continue; }
                sb.AppendLine();
                sb.AppendLine($"## État à la révision {r}");
                sb.AppendLine();
                sb.AppendLine("```text");
                sb.AppendLine(StateText.Render(snap, 6));
                var ctx = DecisionContextBuilder.Build(snap) ?? DecisionContextBuilder.BuildChoice(snap);
                if (ctx is not null) foreach (var la in ctx.LegalActions) sb.AppendLine($"  {la.Id,-14} {la.Label}");
                sb.AppendLine("```");
            }
        }
        var report = sb.ToString();
        if (a.Get("out") is string outPath) File.WriteAllText(outPath, report, new System.Text.UTF8Encoding(false));
        Console.WriteLine(report);
        return 0;
    }

    /// <summary>Lecture d'un fichier encore ouvert en écriture par l'application (trace, journal).</summary>
    private static List<string> ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(fs, System.Text.Encoding.UTF8);
        var result = new List<string>();
        string? line;
        while ((line = r.ReadLine()) is not null) result.Add(line);
        return result;
    }

    private static string CardName(HsCopilot.Core.Cards.ICardDatabase cards, string? id) => id is null ? "carte" : cards.Get(id)?.Name ?? id;

    /// <summary>Action légale correspondant à un SendOption (index d'option, cible, sous-option), null si introuvable.</summary>
    private static LegalAction? SentAction(GameStateSnapshot snap, string? body)
    {
        if (body is null || DecisionContextBuilder.Build(snap) is not { } ctx) return null;
        int Read(string key) => int.TryParse(PowerStatements.ReadField(body, key), out var v) ? v : -1;
        var opt = Read("selectedOption");
        var target = Read("selectedTarget");
        var sub = Read("selectedSubOption");
        return ctx.LegalActions.FirstOrDefault(x => x.OptionIndex == opt && (target <= 0 || x.TargetEntityId == target) && (sub < 0 || x.SubOptionIndex == sub));
    }

    private static string DescribeSend(GameStateSnapshot snap, string body)
    {
        var ctx = DecisionContextBuilder.Build(snap) ?? DecisionContextBuilder.BuildChoice(snap);
        if (ctx is null) return body;
        int Read(string key) => int.TryParse(PowerStatements.ReadField(body, key), out var v) ? v : -1;
        var opt = Read("selectedOption");
        var target = Read("selectedTarget");
        var sub = Read("selectedSubOption");
        var action = ctx.LegalActions.FirstOrDefault(x => x.OptionIndex == opt && (target <= 0 || x.TargetEntityId == target) && (sub < 0 || x.SubOptionIndex == sub))
                     ?? ctx.LegalActions.FirstOrDefault(x => x.OptionIndex == opt);
        return action?.Label ?? body;
    }
}
