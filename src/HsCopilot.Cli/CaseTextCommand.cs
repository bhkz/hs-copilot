using System.Text;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;

namespace HsCopilot.Cli;

/// <summary>
/// Positions figées en texte lisible (état visible au moment de la décision et actions légales), une par fichier, pour une
/// évaluation humaine ou par un autre modèle. Ni résultat de partie, ni carte cachée : seulement le contenu du cas.
/// </summary>
public static class CaseTextCommand
{
    public static int Run(Args a)
    {
        if (a.Positional.Count == 0 || a.Get("out") is not string outDir) throw new ArgumentException("Usage : case-text <cas.jsonl> --out <dossier>");
        Directory.CreateDirectory(outDir);
        var n = 0;
        foreach (var c in DatasetBuilder.Load(a.Positional[0]))
        {
            var ctx = BenchRunner.BuildContext(c);
            var sb = new StringBuilder();
            sb.AppendLine($"Position {PositionId(c.CaseId)} — tour {c.TurnNumber}");
            sb.AppendLine();
            sb.AppendLine(StateText.Render(ctx.State, 12));
            sb.AppendLine();
            sb.AppendLine("Actions légales :");
            foreach (var x in ctx.LegalActions) sb.AppendLine($"  - {HsCopilot.Core.Strategy.AdviceDisplay.Label(x, ctx.State)}");
            File.WriteAllText(Path.Combine(outDir, PositionId(c.CaseId) + ".txt"), sb.ToString(), new UTF8Encoding(false));
            n++;
        }
        Console.WriteLine($"{n} positions écrites dans {outDir}");
        return 0;
    }

    /// <summary>Identifiant neutre (empreinte du cas) : ne révèle ni la partie, ni l'heure.</summary>
    public static string PositionId(string caseId) => "P" + HsCopilot.Core.Strategy.PromptBuilder.Fingerprint(caseId)[..8];
}
