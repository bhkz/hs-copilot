using System.Text;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;

namespace HsCopilot.Cli;

/// <summary>
/// Audit des Découvertes avec Sombre don : pour chaque choix du joueur local, les trois propositions telles que le moteur
/// les projette (carte, don lu dans le log, résultat calculé), le libellé envoyé au modèle, la carte choisie, puis ses
/// statistiques réelles quand le joueur reprend la main. Vérifie la table des dons sur de vraies parties.
/// </summary>
public static class DiscoverAuditCommand
{
    private static readonly string[] KeywordTags = { "CHARGE", "TAUNT", "DIVINE_SHIELD", "WINDFURY", "LIFESTEAL", "ELUSIVE", "REBORN", "DEATHRATTLE", "RUSH" };

    private sealed class Row
    {
        public required string File { get; init; }
        public int Game { get; init; }
        public TimeSpan Time { get; init; }
        public string? Source { get; init; }
        public required List<CardRef> Offered { get; init; }
        public required List<string> Labels { get; init; }
        public int? Chosen { get; set; }
        public string? Actual { get; set; }
        public string? Check { get; set; }
    }

    public static int Run(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Usage : discover-audit <Power.log ou dossier>… [--out fichier.md] [--all-choices]");
        var files = a.Positional.SelectMany(p => Directory.Exists(p)
            ? Directory.EnumerateFiles(p, "Power*.log", SearchOption.AllDirectories)
            : new[] { p }).Distinct().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        var (_, cards) = Commands.LoadEnvironment(a, quiet: true);
        var allChoices = a.Has("all-choices");
        var rows = new List<Row>();
        foreach (var file in files)
        {
            foreach (var game in ReplayFile.SplitGames(ReplayFile.ReadAllLines(file)))
            {
                var engine = new GameEngine(new EngineOptions { Cards = cards });
                var projector = new GameStateProjector(cards);
                var recorded = new HashSet<(int, int)>();   // (choix, Rembobinages) : un Rembobinage réutilise les numéros de choix
                var waiting = new Dictionary<int, Row>();
                var resets = 0;   // choix résolu -> ligne, en attente des statistiques réelles
                var byChoice = new Dictionary<int, Row>();   // remis à zéro à chaque Rembobinage
                foreach (var line in game.Lines)
                {
                    var p = PowerLogLineParser.TryParse(line);
                    if (p is null) continue;
                    engine.Process(p);
                    var local = engine.LocalPlayerId;
                    if (local is null) continue;
                    if (engine.ResetCount != resets) { resets = engine.ResetCount; byChoice.Clear(); waiting.Clear(); }
                    // Choix complet (la liste des propositions est terminée) : projection comme pour le conseiller.
                    if (p.Method != "DebugPrintEntityChoices")
                    {
                        foreach (var c in engine.Choices.Where(c => !c.Resolved && c.PlayerId == local && c.ChoiceType != "MULLIGAN" && c.Offered.Count > 0 && !recorded.Contains((c.Id, engine.ResetCount))).ToList())
                        {
                            recorded.Add((c.Id, engine.ResetCount));
                            var snap = projector.Project(engine, new ProjectionContext { Transport = "audit" });
                            if (snap.PendingChoice is not { } pc || pc.Id != c.Id) continue;
                            if (!allChoices && pc.Offered.All(o => o.DarkGift is null)) continue;
                            var ctx = DecisionContextBuilder.BuildChoice(snap);
                            string? source = null;
                            if (c.SourceId is int sid && engine.Entities.TryGetValue(sid, out var se) && se.GetInt("CREATOR") is var cr and > 0
                                && engine.Entities.TryGetValue(cr, out var maker) && maker.CardId is { } mid)
                                source = cards.Get(mid)?.Name ?? mid;
                            var row = new Row
                            {
                                File = Path.GetFileName(Path.GetDirectoryName(file) ?? file),
                                Game = game.Index,
                                Time = p.Time,
                                Source = source ?? pc.SourceName,
                                Offered = pc.Offered,
                                Labels = ctx?.LegalActions.Select(x => x.Label).ToList() ?? new(),
                            };
                            rows.Add(row);
                            byChoice[c.Id] = row;
                        }
                        foreach (var c in engine.Choices.Where(c => c.Resolved && c.Chosen!.Count > 0 && byChoice.ContainsKey(c.Id) && byChoice[c.Id].Chosen is null))
                        {
                            byChoice[c.Id].Chosen = c.Chosen!.FirstOrDefault();
                            waiting[c.Id] = byChoice[c.Id];
                        }
                    }
                    // Le joueur reprend la main : statistiques réelles de la carte choisie, don appliqué.
                    if (p.Method == "DebugPrintOptions" && waiting.Count > 0)
                    {
                        foreach (var (id, row) in waiting.ToList())
                        {
                            waiting.Remove(id);
                            if (row.Chosen is not int chosen || !engine.Entities.TryGetValue(chosen, out var e)) continue;
                            var kws = KeywordTags.Where(e.Flag).ToList();
                            row.Actual = $"{e.GetInt("ATK")}/{e.GetInt("HEALTH")} pour {e.GetInt("COST")}, zone {e.Zone}" + (kws.Count > 0 ? ", " + string.Join(" ", kws) : "");
                            var offered = row.Offered.FirstOrDefault(o => o.EntityId == chosen);
                            row.Check = Compare(offered, e, kws);
                        }
                    }
                }
            }
        }

        var sb = new StringBuilder();
        var gifted = rows.Where(r => r.Offered.Any(o => o.DarkGift is not null)).ToList();
        var offeredCount = gifted.Sum(r => r.Offered.Count);
        var withGift = gifted.Sum(r => r.Offered.Count(o => o.DarkGift is not null));
        var known = gifted.Sum(r => r.Offered.Count(o => o.DarkGift?.Result is not null));
        var checks = rows.Where(r => r.Check is not null).ToList();
        sb.AppendLine("# Audit des Découvertes avec Sombre don");
        sb.AppendLine();
        sb.AppendLine($"- Fichiers : {files.Count} ; Découvertes avec Sombre don : {gifted.Count} ; propositions : {offeredCount}");
        sb.AppendLine($"- Don identifié par le moteur (tag DARK_GIFT_ENTITY) : {withGift}/{offeredCount} ; effet traduit par la table : {known}/{withGift}");
        sb.AppendLine($"- Carte choisie vérifiée après application du don : {checks.Count(r => r.Check!.StartsWith("conforme"))} conformes, {checks.Count(r => !r.Check!.StartsWith("conforme"))} écarts");
        sb.AppendLine();
        sb.AppendLine("| Fichier | Partie | Heure | Source | Propositions (carte + don → résultat) | Choisie | Réel après le choix | Contrôle |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var r in rows)
        {
            var offered = string.Join("<br>", r.Offered.Select(o => $"{o.Name}{(o.Type == "MINION" ? $" {o.Attack}/{o.Health}" : "")} ({o.Cost})" + (o.DarkGift is { } g ? $" + {DarkGifts.Summary(o)}" : " (sans don)")));
            var chosen = r.Offered.FirstOrDefault(o => o.EntityId == r.Chosen)?.Name ?? "?";
            sb.AppendLine($"| {r.File} | {r.Game} | {r.Time:hh\\:mm\\:ss} | {r.Source} | {offered.Replace("|", "/")} | {chosen} | {r.Actual} | {r.Check} |");
        }
        var report = sb.ToString();
        if (a.Get("out") is string outPath) File.WriteAllText(outPath, report, new UTF8Encoding(false));
        Console.WriteLine(report);
        return 0;
    }

    private static string Compare(CardRef? offered, EntityState actual, List<string> kws)
    {
        if (offered?.DarkGift is not { } g) return "sans don";
        if (g.Result is not { } r) return $"don hors table ({g.CardId})";
        var diffs = new List<string>();
        string? note = null;
        if (r.Attack is int a && a != actual.GetInt("ATK")) diffs.Add($"ATQ prévue {a}");
        if (r.Health is int h && h != actual.GetInt("HEALTH")) diffs.Add($"PV prévus {h}");
        var ownCostEffect = (offered.Text ?? "").Contains("Coûte", StringComparison.OrdinalIgnoreCase);
        if (r.Cost is int c && c != actual.GetInt("COST"))
        {
            if (ownCostEffect) note = $"coût {actual.GetInt("COST")} au lieu de {c} : effet propre de la carte (« {offered.Text} »)";
            else diffs.Add($"coût prévu {c}");
        }
        foreach (var k in r.AddedKeywords.Where(k => !kws.Contains(k))) diffs.Add($"{k} absent");
        if (g.CardId == "EDR_100t8" && actual.Zone != "DECK") diffs.Add($"zone {actual.Zone} au lieu de DECK");
        return diffs.Count == 0 ? (note is null ? "conforme" : $"conforme pour le don ; {note}") : "écart : " + string.Join(", ", diffs);
    }
}
