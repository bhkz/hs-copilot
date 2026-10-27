using System.Text;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Cli;

/// <summary>
/// Analyse d'une exécution du banc d'essai sur des positions de danger, avec des critères déterministes fixés à l'avance :
/// en danger (marge ≤ 5), le conseil est-il une défense légale qui retire des dégâts visibles, ou un létal certain ? Avec un
/// létal certain, le conseil est-il une étape de la séquence de létal ? Sur les témoins, la variante devient-elle défensive ?
/// Aucun appel.
/// </summary>
public static class DangerCheckCommand
{
    public static int Run(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Usage : danger-check <dossier d'exécution> [--out fichier.md]");
        var dir = a.Positional[0];
        var cases = DatasetBuilder.Load(Path.Combine(dir, BenchRunner.CasesFile)).ToDictionary(c => c.CaseId);
        var results = BenchRunner.Latest(BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile)));
        var variants = results.Select(r => r.Variant).Distinct().OrderBy(v => v.Length).ToList();
        var sb = new StringBuilder();
        sb.AppendLine("# Danger adverse : contrôle déterministe des réponses du banc d'essai");
        sb.AppendLine();
        sb.AppendLine("| Position | Marge | Ton létal | " + string.Join(" | ", variants.Select(v => $"{v} : action | défense / létal")) + " | Même action |");
        sb.AppendLine("|---|---|---|" + string.Concat(variants.Select(_ => "---|---|")) + "---|");
        var stats = variants.ToDictionary(v => v, _ => new Dictionary<string, (int N, int Good)>());
        foreach (var (id, c) in cases.OrderBy(x => x.Key))
        {
            var ctx = BenchRunner.BuildContext(c);
            var threat = ThreatAssessment.Assess(ctx);
            var lethal = QuickAnalysis.AssessLethal(ctx);
            var lethalIds = lethal?.Level == LethalLevels.Certain && lethal.Solver is { } sol ? sol.Sequence.Where(x => x.Action is not null).Select(x => x.Action!.Id).ToHashSet() : new HashSet<string>();
            var defenseIds = threat?.Defenses.Select(d => d.Action.Id).ToHashSet() ?? new HashSet<string>();
            var category = lethalIds.Count > 0 ? "létal" : threat is { Margin: <= 5 } ? "danger" : "témoin";
            var cells = new List<string>();
            var actions = new List<string?>();
            foreach (var v in variants)
            {
                var r = results.FirstOrDefault(x => x.CaseId == id && x.Variant == v);
                var act = r?.ActionId;
                actions.Add(act);
                var label = act is null ? "(aucune)" : ctx.LegalActions.FirstOrDefault(x => x.Id == act) is { } la ? AdviceDisplay.Label(la, ctx.State) : act;
                var good = category switch
                {
                    "létal" => act is not null && lethalIds.Contains(act),
                    "danger" => act is not null && defenseIds.Contains(act),
                    _ => act is not null && !defenseIds.Contains(act) || threat is null,
                };
                var verdict = category switch { "létal" => good ? "étape du létal" : "HORS létal", "danger" => good ? "défense" : "non défensif", _ => good ? "" : "défensif" };
                cells.Add($"{label.Replace("|", "/")} | {verdict}");
                var s = stats[v];
                s[category] = (s.GetValueOrDefault(category).N + 1, s.GetValueOrDefault(category).Good + (good ? 1 : 0));
            }
            sb.AppendLine($"| {CaseTextCommand.PositionId(id)} | {threat?.Margin} | {(lethalIds.Count > 0 ? "oui" : "")} | {string.Join(" | ", cells)} | {(actions.Distinct().Count() == 1 ? "oui" : "non")} |");
        }
        sb.AppendLine();
        sb.AppendLine("| Variante | Létal : étape du létal | Danger : défense | Témoins : non défensif | Coût | Réponse complète (médiane, sans flux) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var v in variants)
        {
            var rs = results.Where(r => r.Variant == v).ToList();
            string Cell(string k) => stats[v].TryGetValue(k, out var x) ? $"{x.Good}/{x.N}" : "—";
            static double Med(IEnumerable<double> xs) { var l = xs.OrderBy(x => x).ToList(); return l.Count == 0 ? 0 : l[l.Count / 2]; }
            sb.AppendLine($"| {v} | {Cell("létal")} | {Cell("danger")} | {Cell("témoin")} | {rs.Sum(r => r.CostUsd ?? 0):0.000} $ | {Med(rs.Select(r => r.LatencyMs)) / 1000:0.0} s |");
        }
        var text = sb.ToString();
        if (a.Get("out") is string outPath) File.WriteAllText(outPath, text, new UTF8Encoding(false));
        Console.WriteLine(text);
        return 0;
    }
}
