using System.Text;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Cli;

/// <summary>
/// Danger adverse visible sur des positions figées (cas JSONL de `bench build`) : marge au prochain tour adverse, options
/// défensives légales, et comparaison avec le coup réellement joué. Diagnostic hors ligne, aucun appel.
/// </summary>
public static class ThreatAuditCommand
{
    public static int Run(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Usage : threat-audit <cas.jsonl> [--out fichier.md]");
        var cases = DatasetBuilder.Load(a.Positional[0]);
        var sb = new StringBuilder();
        sb.AppendLine("# Danger adverse visible sur les positions figées");
        sb.AppendLine();
        var rows = new List<(DecisionCase Case, ThreatReport Report, bool PlayedDefense, string Played, bool Lethal)>();
        foreach (var c in cases)
        {
            DecisionContext ctx;
            try { ctx = BenchRunner.BuildContext(c); } catch { continue; }
            if (ThreatAssessment.Assess(ctx) is not { } r) continue;
            var played = ctx.LegalActions.FirstOrDefault(x => x.Id == c.Actual?.ActionId);
            var lethal = QuickAnalysis.AssessLethal(ctx)?.Level == LethalLevels.Certain;
            rows.Add((c, r, played is not null && r.Defenses.Any(d => d.Action.Id == played.Id), played?.Label ?? c.Actual?.Label ?? "?", lethal));
        }
        sb.AppendLine($"Positions analysées : {rows.Count}.");
        foreach (var level in new[] { ThreatReport.VisibleLethal, ThreatReport.High, ThreatReport.Low })
        {
            var g = rows.Where(r => r.Report.Level == level).ToList();
            sb.AppendLine($"- {level} : {g.Count} positions, dont {g.Count(r => r.Lethal)} avec ton létal certain, coup joué défensif : {g.Count(r => r.PlayedDefense)}");
        }
        var gifts = rows.Where(r => r.Report.Gifts.Count > 0).ToList();
        sb.AppendLine($"- cadeau à l'adversaire possible en main : {gifts.Count} positions ; joué : {gifts.Count(r => r.Report.Gifts.Any(gf => r.Played.Contains(gf.Card)))}");
        sb.AppendLine();
        sb.AppendLine("| Cas | Tour | Niveau | Visible / PV | Sources | Ton létal certain | Coup joué | Défensif | Meilleure défense |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in rows.Where(r => r.Report.Level != ThreatReport.Low || r.Report.Gifts.Count > 0).OrderBy(r => r.Case.CaseId))
            sb.AppendLine($"| {r.Case.CaseId} | {r.Case.TurnNumber} | {r.Report.Level}{(r.Report.Gifts.Count > 0 ? " · cadeau : " + string.Join(", ", r.Report.Gifts.Select(g => g.Card)) : "")} | {r.Report.VisibleDamage} / {r.Report.EffectiveHealth}{(r.Report.TauntShield > 0 ? $" (+{r.Report.TauntShield} Provocations)" : "")} | {string.Join(" + ", r.Report.Sources)} | {(r.Lethal ? "oui" : "")} | {r.Played} | {(r.PlayedDefense ? "oui" : "")} | {r.Report.Defenses.FirstOrDefault()?.Text} |");
        if (a.Get("jsonl") is string jsonl)
            File.WriteAllLines(jsonl, rows.Select(r => System.Text.Json.JsonSerializer.Serialize(new
            {
                caseId = r.Case.CaseId, gameId = r.Case.GameId, gameType = r.Case.GameType, turn = r.Case.TurnNumber, decisionInTurn = r.Case.DecisionInTurn,
                tags = r.Case.Tags, level = r.Report.Level, margin = r.Report.Margin, visible = r.Report.VisibleDamage, hp = r.Report.EffectiveHealth,
                lethal = r.Lethal, gifts = r.Report.Gifts.Select(g => g.Card), defenses = r.Report.Defenses.Count,
                actions = BenchRunner.BuildContext(r.Case).LegalActions.Count(x => x.Kind != "end-turn"), played = r.Played, playedDefense = r.PlayedDefense,
            })));
        var text = sb.ToString();
        if (a.Get("out") is string outPath) File.WriteAllText(outPath, text, new UTF8Encoding(false));
        Console.WriteLine(text.Split('\n').Take(8).Aggregate((x, y) => x + "\n" + y));
        return 0;
    }
}
