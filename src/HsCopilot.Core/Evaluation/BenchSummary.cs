using System.Globalization;
using System.Text;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Core.Evaluation;

public sealed class FlagExample
{
    public required string CaseId { get; init; }
    public int TurnNumber { get; init; }
    public required string Variant { get; init; }
    public required string Code { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public string? ActionLabel { get; init; }
    public string? Summary { get; init; }
}

public sealed class VariantSummary
{
    public required string Variant { get; init; }
    public int Calls { get; init; }
    public int ApiErrors { get; init; }
    public List<string> ErrorSamples { get; init; } = new();
    public int Responded { get; init; }
    public int Legal { get; init; }
    public int Accepted { get; init; }
    public List<string> IllegalSamples { get; init; } = new();
    public int WithActual { get; init; }
    public int AgreeExact { get; init; }
    public int AgreeSameCard { get; init; }
    public int AgreeSameKind { get; init; }
    public int WithActualRanked { get; init; }
    public int AgreeExactRanked { get; init; }
    public int AgreeSameCardRanked { get; init; }
    public int WithDubious { get; init; }
    public int WithCheck { get; init; }
    public Dictionary<string, int> FlagCounts { get; init; } = new();
    public List<FlagExample> FlagExamples { get; init; } = new();
    public int LethalCases { get; init; }
    public int LethalTaken { get; init; }
    public Dictionary<string, int> Confidence { get; init; } = new();
    public int CertaintyWarnings { get; init; }
    public double LatencyP50Ms { get; init; }
    public double LatencyP95Ms { get; init; }
    public double LatencyMaxMs { get; init; }
    public double MeanInputTokens { get; init; }
    public double MeanCachedTokens { get; init; }
    public double MeanOutputTokens { get; init; }
    public double MeanReasoningTokens { get; init; }
    public double MeanCostUsd { get; init; }
    public double TotalCostUsd { get; init; }
    public int Within20s { get; init; }
    public Dictionary<string, int> Annotations { get; init; } = new();
}

public sealed class PairAgreement
{
    public required string A { get; init; }
    public required string B { get; init; }
    public int Common { get; init; }
    public int SameAction { get; init; }
    public int SameCard { get; init; }
}

/// <summary>Écriture de la synthèse d'un dossier d'exécution (report.md et summary.json).</summary>
public static class BenchReport
{
    public static (BenchSummary Summary, string MarkdownPath) Write(string runDir)
    {
        var cases = DatasetBuilder.Load(Path.Combine(runDir, BenchRunner.CasesFile));
        var results = BenchRunner.LoadResults(Path.Combine(runDir, BenchRunner.ResultsFile));
        var annotations = AnnotationStore.Load(Path.Combine(runDir, BenchRunner.AnnotationsFile));
        var summary = BenchSummary.Compute(cases, results, annotations, BenchRunner.LoadManifest(runDir));
        File.WriteAllText(Path.Combine(runDir, "summary.json"), System.Text.Json.JsonSerializer.Serialize(summary, HsCopilot.Core.Util.Json.Pretty), new UTF8Encoding(false));
        var md = Path.Combine(runDir, "report.md");
        File.WriteAllText(md, summary.ToMarkdown(), new UTF8Encoding(false));
        return (summary, md);
    }
}

/// <summary>Synthèse d'une exécution du banc d'essai. Les « accords » mesurent la ressemblance avec ton jeu, pas la qualité.</summary>
public sealed class BenchSummary
{
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
    public BenchRunManifest? Manifest { get; init; }
    public int Cases { get; init; }
    public Dictionary<string, int> TagCounts { get; init; } = new();
    public int LethalCases { get; init; }
    public int RankedCases { get; init; }
    public int ActualMapped { get; init; }
    public int ActualWithDubious { get; init; }
    public Dictionary<string, int> ActualFlagCounts { get; init; } = new();
    public List<FlagExample> ActualFlagExamples { get; init; } = new();
    public int ActualLethalTaken { get; init; }
    public Dictionary<string, int> ActualAnnotations { get; init; } = new();
    public List<VariantSummary> Variants { get; init; } = new();
    public List<PairAgreement> Pairs { get; init; } = new();
    public double TotalCostUsd { get; init; }

    public static BenchSummary Compute(IReadOnlyList<DecisionCase> cases, IReadOnlyList<BenchResult> allResults, AnnotationStore? annotations = null, BenchRunManifest? manifest = null)
    {
        var latest = BenchRunner.Latest(allResults);
        var caseIds = latest.Select(r => r.CaseId).ToHashSet();
        var used = cases.Where(c => caseIds.Contains(c.CaseId)).ToList();
        var byId = used.ToDictionary(c => c.CaseId);
        var contexts = used.ToDictionary(c => c.CaseId, BenchRunner.BuildContext);
        // Étiquettes, signalements et accords recalculés à partir des réponses enregistrées : une correction du détecteur
        // s'applique aux exécutions passées sans nouvel appel.
        var tags = used.ToDictionary(c => c.CaseId, c => CaseTagger.Tags(contexts[c.CaseId], c.DecisionInTurn));
        var results = latest.Where(r => byId.ContainsKey(r.CaseId)).Select(r => Recheck(r, byId[r.CaseId], contexts[r.CaseId])).ToList();
        var ranked = used.Where(c => c.GameType == "GT_RANKED").Select(c => c.CaseId).ToHashSet();

        // Référence : le détecteur appliqué à tes propres coups (calibre le taux de signalements).
        var actualFlags = new Dictionary<string, int>();
        var actualExamples = new List<FlagExample>();
        int actualMapped = 0, actualDubious = 0, actualLethal = 0;
        foreach (var c in used)
        {
            var ctx = contexts[c.CaseId];
            var played = c.Actual?.ActionId is string id ? ctx.LegalActions.FirstOrDefault(a => a.Id == id) : null;
            if (played is null) continue;
            actualMapped++;
            var flags = DubiousMoveDetector.Analyze(ctx, played);
            if (flags.Any(f => f.Severity == "douteux")) actualDubious++;
            foreach (var f in flags)
            {
                actualFlags[f.Code] = actualFlags.GetValueOrDefault(f.Code) + 1;
                if (actualExamples.Count(e => e.Code == f.Code) < 3)
                    actualExamples.Add(new FlagExample { CaseId = c.CaseId, TurnNumber = c.TurnNumber, Variant = AnnotationStore.RealMove, Code = f.Code, Severity = f.Severity, Message = f.Message, ActionLabel = played.Label });
            }
            if (IsLethalCase(tags[c.CaseId]) && TakesLethal(ctx, played)) actualLethal++;
        }

        var variants = new List<VariantSummary>();
        foreach (var g in results.GroupBy(r => r.Variant).OrderBy(g => VariantOrder(g.Key)))
        {
            var list = g.ToList();
            var ok = list.Where(r => r.Error is null).ToList();
            var withActual = ok.Where(r => r.Agreement is not ("inconnu" or "sans-réponse" or null)).ToList();
            var flagCounts = new Dictionary<string, int>();
            var examples = new List<FlagExample>();
            foreach (var r in ok)
                foreach (var f in r.Flags)
                {
                    flagCounts[f.Code] = flagCounts.GetValueOrDefault(f.Code) + 1;
                    if (examples.Count(e => e.Code == f.Code) < 3)
                        examples.Add(new FlagExample
                        {
                            CaseId = r.CaseId, TurnNumber = byId.TryGetValue(r.CaseId, out var cc) ? cc.TurnNumber : 0, Variant = r.Variant,
                            Code = f.Code, Severity = f.Severity, Message = f.Message, ActionLabel = r.ActionLabel, Summary = r.Summary,
                        });
                }
            var lethalCases = ok.Where(r => IsLethalCase(tags[r.CaseId])).ToList();
            var lethalTaken = lethalCases.Count(r => r.ActionId is string aid && contexts[r.CaseId].LegalActions.FirstOrDefault(a => a.Id == aid) is { } a && TakesLethal(contexts[r.CaseId], a));
            var lat = ok.Select(r => r.LatencyMs).OrderBy(x => x).ToArray();
            var ann = annotations?.Items.Where(a => a.Variant == g.Key && caseIds.Contains(a.CaseId)).GroupBy(a => a.Verdict).ToDictionary(x => x.Key, x => x.Count()) ?? new();
            variants.Add(new VariantSummary
            {
                Variant = g.Key,
                Calls = list.Count,
                ApiErrors = list.Count - ok.Count,
                ErrorSamples = list.Where(r => r.Error is not null).Select(r => $"{r.CaseId} : {r.Error}").Take(5).ToList(),
                Responded = ok.Count,
                Legal = ok.Count(r => r.Legal),
                Accepted = ok.Count(r => r.Accepted),
                IllegalSamples = ok.Where(r => !r.Accepted).Select(r => $"{r.CaseId} : {r.ActionId} ({string.Join(" ; ", r.ValidationReasons)})").Take(5).ToList(),
                WithActual = withActual.Count,
                AgreeExact = withActual.Count(r => r.Agreement == "exact"),
                AgreeSameCard = withActual.Count(r => r.Agreement is "exact" or "même-carte"),
                AgreeSameKind = withActual.Count(r => r.Agreement is "exact" or "même-carte" or "même-type"),
                WithActualRanked = withActual.Count(r => ranked.Contains(r.CaseId)),
                AgreeExactRanked = withActual.Count(r => ranked.Contains(r.CaseId) && r.Agreement == "exact"),
                AgreeSameCardRanked = withActual.Count(r => ranked.Contains(r.CaseId) && r.Agreement is "exact" or "même-carte"),
                WithDubious = ok.Count(r => r.Flags.Any(f => f.Severity == "douteux")),
                WithCheck = ok.Count(r => r.Flags.All(f => f.Severity != "douteux") && r.Flags.Any(f => f.Severity == "à vérifier")),
                FlagCounts = flagCounts,
                FlagExamples = examples,
                LethalCases = lethalCases.Count,
                LethalTaken = lethalTaken,
                Confidence = ok.GroupBy(r => r.Confidence ?? "?").ToDictionary(x => x.Key, x => x.Count()),
                CertaintyWarnings = ok.Count(r => r.Warnings.Any(w => w.Contains("certitude", StringComparison.OrdinalIgnoreCase))),
                LatencyP50Ms = LatencySeries.Percentile(lat, 0.50),
                LatencyP95Ms = LatencySeries.Percentile(lat, 0.95),
                LatencyMaxMs = lat.Length == 0 ? 0 : lat[^1],
                MeanInputTokens = ok.Count == 0 ? 0 : ok.Average(r => r.InputTokens),
                MeanCachedTokens = ok.Count == 0 ? 0 : ok.Average(r => r.CachedInputTokens),
                MeanOutputTokens = ok.Count == 0 ? 0 : ok.Average(r => r.OutputTokens),
                MeanReasoningTokens = ok.Count == 0 ? 0 : ok.Average(r => r.ReasoningTokens),
                MeanCostUsd = list.Count == 0 ? 0 : list.Average(r => r.CostUsd ?? 0),
                TotalCostUsd = list.Sum(r => r.CostUsd ?? 0),
                Within20s = ok.Count(r => r.LatencyMs <= 20_000),
                Annotations = ann,
            });
        }

        var pairs = new List<PairAgreement>();
        var names = variants.Select(v => v.Variant).ToList();
        for (int i = 0; i < names.Count; i++)
            for (int j = i + 1; j < names.Count; j++)
            {
                var a = results.Where(r => r.Variant == names[i] && r.Legal).ToDictionary(r => r.CaseId);
                var b = results.Where(r => r.Variant == names[j] && r.Legal).ToDictionary(r => r.CaseId);
                var common = a.Keys.Intersect(b.Keys).ToList();
                pairs.Add(new PairAgreement
                {
                    A = names[i], B = names[j], Common = common.Count,
                    SameAction = common.Count(id => a[id].ActionId == b[id].ActionId),
                    SameCard = common.Count(id => SameCard(contexts[id], a[id].ActionId, b[id].ActionId)),
                });
            }

        return new BenchSummary
        {
            Manifest = manifest,
            Cases = used.Count,
            TagCounts = tags.Values.SelectMany(t => t).GroupBy(t => t).OrderByDescending(t => t.Count()).ToDictionary(t => t.Key, t => t.Count()),
            LethalCases = tags.Values.Count(IsLethalCase),
            RankedCases = ranked.Count,
            ActualMapped = actualMapped,
            ActualWithDubious = actualDubious,
            ActualFlagCounts = actualFlags,
            ActualFlagExamples = actualExamples,
            ActualLethalTaken = actualLethal,
            ActualAnnotations = annotations?.Items.Where(a => a.Variant == AnnotationStore.RealMove && caseIds.Contains(a.CaseId)).GroupBy(a => a.Verdict).ToDictionary(x => x.Key, x => x.Count()) ?? new(),
            Variants = variants,
            Pairs = pairs,
            TotalCostUsd = results.Sum(r => r.CostUsd ?? 0),
        };
    }

    /// <summary>Signalements et accord recalculés à partir de la réponse enregistrée (détecteur courant).</summary>
    public static BenchResult Recheck(BenchResult r, DecisionCase c, DecisionContext ctx)
    {
        if (r.Error is not null) return r;
        var chosen = r.ActionId is string id ? ctx.LegalActions.FirstOrDefault(a => a.Id == id) : null;
        var flags = chosen is null ? new List<DubiousFlag>() : DubiousMoveDetector.Analyze(ctx, chosen, new StrategyOutput { RejectedAlternativeId = r.RejectedAlternativeId });
        return r with { Flags = flags, Agreement = BenchRunner.Agreement(chosen, ctx, c.Actual), ActualActionId = c.Actual?.ActionId };
    }

    private static bool IsLethalCase(IReadOnlyCollection<string> tags) => tags.Contains("létal-attaques") || tags.Contains("létal-avec-main");

    /// <summary>Le coup engage la ligne létale : il vise le héros adverse ou joue la carte de la main qui complète le létal.</summary>
    private static bool TakesLethal(DecisionContext ctx, LegalAction a) =>
        BoardMath.IsOpponentHero(ctx.State, a.TargetEntityId) || BoardMath.BestHandFaceDamage(ctx).Action?.Id == a.Id;

    private static bool SameCard(DecisionContext ctx, string? x, string? y)
    {
        var a = ctx.LegalActions.FirstOrDefault(l => l.Id == x);
        var b = ctx.LegalActions.FirstOrDefault(l => l.Id == y);
        return a is not null && b is not null && a.Kind == b.Kind && a.SourceEntityId == b.SourceEntityId;
    }

    private static string VariantOrder(string key)
    {
        var v = BenchVariant.Parse(key);
        var rank = Array.IndexOf(new[] { "none", "minimal", "low", "medium", "high", "xhigh", "max" }, v.Effort);
        return $"{v.Model}:{(rank < 0 ? 9 : rank)}:{v.Repeat}";
    }

    private static string Pct(int n, int d) => d == 0 ? "n/a" : $"{100.0 * n / d:0}% ({n}/{d})";
    private static string S(double ms) => (ms / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s";
    private static string Usd(double v) => v.ToString(v < 0.01 ? "0.0000" : "0.000", CultureInfo.InvariantCulture) + " $";

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Banc d'essai du conseiller : synthèse");
        sb.AppendLine();
        sb.AppendLine($"Généré le {GeneratedUtc:yyyy-MM-dd HH:mm} UTC. {Cases} cas. Coût total mesuré : {Usd(TotalCostUsd)}.");
        if (Manifest is { } m)
            sb.AppendLine($"Cas : empreinte `{m.CasesFingerprint}`. Instructions : `{m.InstructionsFingerprint}`. Stratégie : `{m.StrategyFingerprint}`. Historique transmis : {m.HistoryItems} actions.");
        sb.AppendLine();
        sb.AppendLine("Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;");
        sb.AppendLine("ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.");
        sb.AppendLine();

        sb.AppendLine("## Résultats par variante");
        sb.AppendLine();
        sb.AppendLine("| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (var v in Variants)
            sb.AppendLine($"| {v.Variant} | {v.Responded}/{v.Calls} | {Pct(v.Accepted, v.Responded)} | {Pct(v.AgreeExact, v.WithActual)} | {Pct(v.AgreeSameCard, v.WithActual)} | {Pct(v.AgreeSameKind, v.WithActual)} | {Pct(v.AgreeExactRanked, v.WithActualRanked)} | {Pct(v.WithDubious, v.Responded)} | {Pct(v.WithCheck, v.Responded)} | {Pct(v.LethalTaken, v.LethalCases)} |");
        sb.AppendLine($"| **ton jeu (référence)** | {ActualMapped} | n/a | n/a | n/a | n/a | n/a | {Pct(ActualWithDubious, ActualMapped)} | n/a | {Pct(ActualLethalTaken, LethalCases)} |");
        sb.AppendLine();
        sb.AppendLine("| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var v in Variants)
            sb.AppendLine($"| {v.Variant} | {S(v.LatencyP50Ms)} | {S(v.LatencyP95Ms)} | {S(v.LatencyMaxMs)} | {Pct(v.Within20s, v.Responded)} | {Usd(v.MeanCostUsd)} |");
        sb.AppendLine();

        sb.AppendLine("| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");
        foreach (var v in Variants)
            sb.AppendLine($"| {v.Variant} | {v.MeanInputTokens:0} | {v.MeanCachedTokens:0} | {v.MeanOutputTokens:0} | {v.MeanReasoningTokens:0} | {Usd(v.TotalCostUsd)} | {string.Join(", ", v.Confidence.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"))} | {(v.Annotations.Count == 0 ? "aucune" : string.Join(", ", v.Annotations.Select(k => $"{k.Key} {k.Value}")))} |");
        sb.AppendLine();

        if (Pairs.Count > 0)
        {
            sb.AppendLine("## Accord entre variantes");
            sb.AppendLine();
            sb.AppendLine("| A | B | Cas communs | Même action | Même carte |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var p in Pairs) sb.AppendLine($"| {p.A} | {p.B} | {p.Common} | {Pct(p.SameAction, p.Common)} | {Pct(p.SameCard, p.Common)} |");
            sb.AppendLine();
        }

        sb.AppendLine("## Signalements (coups légaux mais discutables)");
        sb.AppendLine();
        foreach (var v in Variants)
        {
            sb.AppendLine($"### {v.Variant}");
            if (v.FlagCounts.Count == 0) { sb.AppendLine("Aucun signalement."); sb.AppendLine(); continue; }
            sb.AppendLine(string.Join(", ", v.FlagCounts.OrderByDescending(k => k.Value).Select(k => $"{k.Key} : {k.Value}")));
            sb.AppendLine();
            foreach (var e in v.FlagExamples)
                sb.AppendLine($"- `{e.CaseId}` (tour {e.TurnNumber}, {e.Severity}) **{e.Code}** : {e.Message} Conseil donné : « {e.Summary} »");
            sb.AppendLine();
        }
        sb.AppendLine("### Ton jeu (même détecteur)");
        if (ActualFlagCounts.Count == 0) sb.AppendLine("Aucun signalement.");
        else
        {
            sb.AppendLine(string.Join(", ", ActualFlagCounts.OrderByDescending(k => k.Value).Select(k => $"{k.Key} : {k.Value}")));
            sb.AppendLine();
            foreach (var e in ActualFlagExamples) sb.AppendLine($"- `{e.CaseId}` (tour {e.TurnNumber}, {e.Severity}) **{e.Code}** : {e.Message}");
        }
        sb.AppendLine();

        var errors = Variants.Where(v => v.ErrorSamples.Count > 0 || v.IllegalSamples.Count > 0).ToList();
        if (errors.Count > 0)
        {
            sb.AppendLine("## Erreurs et réponses rejetées");
            sb.AppendLine();
            foreach (var v in errors)
            {
                foreach (var e in v.ErrorSamples) sb.AppendLine($"- {v.Variant} : {e}");
                foreach (var e in v.IllegalSamples) sb.AppendLine($"- {v.Variant} (rejetée) : {e}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Répartition des situations");
        sb.AppendLine();
        sb.AppendLine(string.Join(", ", TagCounts.Select(t => $"{t.Key} : {t.Value}")));
        return sb.ToString();
    }
}
