using System.Text.Json;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Evaluation;

/// <summary>
/// Variante évaluée : modèle + niveau de raisonnement, compléments de contexte facultatifs (« +local » : analyse locale comme
/// le conseiller en direct ; « +danger » : en plus, le danger adverse visible) et étiquette de répétition (« #2 »).
/// </summary>
public sealed record BenchVariant(string Model, string Effort, string? Repeat = null, string? Extras = null)
{
    public string Key => $"{Model}:{Effort}{(Extras is null ? "" : "+" + Extras)}{(Repeat is null ? "" : "#" + Repeat)}";

    public bool Has(string extra) => Extras?.Split('+').Contains(extra, StringComparer.Ordinal) == true;

    /// <summary>« modèle:effort », « modèle:effort+local+danger », « modèle:effort#répétition ».</summary>
    public static BenchVariant Parse(string text)
    {
        text = text.Trim();
        string? repeat = null;
        var hash = text.IndexOf('#');
        if (hash >= 0) { repeat = text[(hash + 1)..].Trim(); text = text[..hash]; }
        string? extras = null;
        var plus = text.IndexOf('+');
        if (plus >= 0) { extras = text[(plus + 1)..].Trim(); text = text[..plus]; }
        var i = text.LastIndexOf(':');
        return i <= 0
            ? new BenchVariant(text.Trim(), "medium", repeat, extras)
            : new BenchVariant(text[..i].Trim(), text[(i + 1)..].Trim(), repeat, extras);
    }

    /// <summary>Description du champ « opponentThreat » ajoutée aux instructions de la variante « +danger ».</summary>
    public const string DangerInstructions = "\n\nlocalAnalysis.opponentThreat : dégâts que l'adversaire peut t'infliger au prochain tour avec ce qui est déjà visible (serviteurs pouvant attaquer, arme, pouvoir héroïque), comparés à tes PV + armure + Provocations (margin ≤ 0 : létal adverse visible), quelques actions légales qui retirent ces dégâts, et les cartes de ta main qui aideraient aussi l'adversaire. Sa main, ses tirages et ses Secrets ne sont pas chiffrés. Pèse la survie contre la pression ; un létal certain pour toi reste prioritaire.";
}

/// <summary>Résultat d'une variante sur un cas (une ligne de results.jsonl).</summary>
public sealed record BenchResult
{
    public required string CaseId { get; init; }
    public required string Variant { get; init; }
    public string? Model { get; init; }
    public string? Effort { get; init; }
    public DateTime StartedUtc { get; init; }
    /// <summary>Erreur d'appel ou de lecture (null si le modèle a répondu).</summary>
    public string? Error { get; init; }
    /// <summary>actionId présent dans les actions légales.</summary>
    public bool Legal { get; init; }
    /// <summary>Réponse acceptée par le validateur complet (action légale et conseil non vide).</summary>
    public bool Accepted { get; init; }
    public List<string> ValidationReasons { get; init; } = new();
    public string? ActionId { get; init; }
    public string? ActionLabel { get; init; }
    public string? ActionKind { get; init; }
    public string? Confidence { get; init; }
    public string? Uncertainty { get; init; }
    public List<string> UncertaintyReasons { get; init; } = new();
    public string? Summary { get; init; }
    public string? Rationale { get; init; }
    public string? PlanNextTurn { get; init; }
    public string? Risk { get; init; }
    public string? RejectedAlternativeId { get; init; }
    public string? RejectedAlternativeLabel { get; init; }
    public string? RejectedAlternativeReason { get; init; }
    public List<string> FollowUp { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    /// <summary>Coup réellement joué (étiquette, jamais transmise au modèle).</summary>
    public string? ActualActionId { get; init; }
    /// <summary>exact, même-carte, même-type, différent, inconnu, sans-réponse.</summary>
    public string? Agreement { get; init; }
    public List<DubiousFlag> Flags { get; init; } = new();
    public double LatencyMs { get; init; }
    public int InputTokens { get; init; }
    public int CachedInputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int ReasoningTokens { get; init; }
    public double? CostUsd { get; init; }
    public int Retries { get; init; }
    public string? ResponseId { get; init; }
    public int PayloadChars { get; init; }
    public string? RawOutput { get; init; }
}

/// <summary>Description d'une exécution (run.json) : tout ce qui est nécessaire pour la reproduire.</summary>
public sealed class BenchRunManifest
{
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public string? CasesSource { get; set; }
    public string? CasesFingerprint { get; set; }
    public int CaseCount { get; set; }
    public List<string> Variants { get; set; } = new();
    public string? InstructionsFingerprint { get; set; }
    public string? StrategyFingerprint { get; set; }
    public int HistoryItems { get; set; }
    public int Concurrency { get; set; }
    public double BudgetUsd { get; set; }
    public int MaxOutputTokens { get; set; }
    public Dictionary<string, ModelPrice> Prices { get; set; } = new();
    public string Note { get; set; } = "Le coup réellement joué sert d'étiquette d'évaluation et n'est jamais transmis au modèle.";
}

public sealed class BenchRunOptions
{
    public required string OutDir { get; init; }
    public required IReadOnlyList<BenchVariant> Variants { get; init; }
    public required Func<BenchVariant, IStrategyProvider> ProviderFactory { get; init; }
    public int Concurrency { get; init; } = 4;
    public double BudgetUsd { get; init; } = 8.0;
    public int HistoryItems { get; init; } = PromptBuilder.DefaultHistoryItems;
    public string? Instructions { get; init; }
    public string? StrategyNotes { get; init; }
    public string? CasesSource { get; init; }
    public int MaxOutputTokens { get; init; }
    /// <summary>Rejouer les couples (cas, variante) dont le dernier résultat est une erreur.</summary>
    public bool RetryErrors { get; init; }
    /// <summary>Coût réservé par appel tant qu'aucune mesure n'existe pour la variante.</summary>
    public double InitialCallEstimateUsd { get; init; } = 0.10;
    public Action<string>? Log { get; init; }
}

public sealed class BenchRunReport
{
    public int Planned { get; init; }
    public int AlreadyDone { get; init; }
    public int Executed { get; init; }
    public bool StoppedByBudget { get; init; }
    public double SpentUsd { get; init; }
}

/// <summary>Exécute des variantes de fournisseur sur des cas figés ; reprise possible, plafond de dépense, résultats en JSONL.</summary>
public static class BenchRunner
{
    public const string ResultsFile = "results.jsonl";
    public const string ManifestFile = "run.json";
    public const string CasesFile = "cases.jsonl";
    public const string InstructionsFile = "instructions.txt";
    public const string AnnotationsFile = "annotations.json";

    public static DecisionContext BuildContext(DecisionCase c) =>
        DecisionContextBuilder.Build(c.LoadSnapshot()) ?? throw new InvalidDataException($"cas sans options fraîches : {c.CaseId}");

    /// <summary>Message envoyé au modèle pour un cas, et identifiants cachés qu'il contiendrait (doit être vide).</summary>
    public static (string Payload, IReadOnlyList<string> Leaks) BuildPayload(DecisionCase c, DecisionContext ctx, int historyItems, BenchVariant? variant = null)
    {
        if (variant is not null && (variant.Has("local") || variant.Has("danger")) && ctx.Kind == DecisionContext.KindAction)
        {
            // Comme le conseiller en direct (StrategyCoordinator) : analyse locale jointe aux décisions d'action.
            var local = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(QuickAnalysis.LocalAnalysisPayload(ctx, QuickAnalysis.AssessLethal(ctx)), Json.Compact))!.AsObject();
            if (variant.Has("danger") && ThreatAssessment.Payload(ThreatAssessment.Assess(ctx)) is { } threat)
                local["opponentThreat"] = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(threat, Json.Compact));
            ctx.LocalAnalysis = local;
        }
        var payload = PromptBuilder.BuildUserPayload(ctx, historyItems);
        var leaks = PayloadLeakGuard.FindLeaks(payload, c.ForbiddenCardIds.ToHashSet(StringComparer.Ordinal));
        return (payload, leaks);
    }

    public static string Agreement(LegalAction? chosen, DecisionContext ctx, ActualMove? actual)
    {
        if (actual?.ActionId is null) return "inconnu";
        if (chosen is null) return "sans-réponse";
        if (chosen.Id == actual.ActionId) return "exact";
        var played = ctx.LegalActions.FirstOrDefault(a => a.Id == actual.ActionId);
        if (played is null) return "inconnu";
        if (chosen.Kind == played.Kind && chosen.SourceEntityId == played.SourceEntityId) return "même-carte";
        if (chosen.Kind == played.Kind) return "même-type";
        return "différent";
    }

    public static List<BenchResult> LoadResults(string path)
    {
        var list = new List<BenchResult>();
        if (!File.Exists(path)) return list;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { if (JsonSerializer.Deserialize<BenchResult>(line, Json.Read) is { } r) list.Add(r); }
            catch (JsonException) { /* ligne tronquée par une interruption : ignorée, le couple sera rejoué */ }
        }
        return list;
    }

    /// <summary>Dernier résultat de chaque couple (cas, variante).</summary>
    public static List<BenchResult> Latest(IEnumerable<BenchResult> results) =>
        results.GroupBy(r => (r.CaseId, r.Variant)).Select(g => g.Last()).ToList();

    public static async Task<BenchRunReport> RunAsync(IReadOnlyList<DecisionCase> cases, BenchRunOptions o, CancellationToken ct)
    {
        Directory.CreateDirectory(o.OutDir);
        var resultsPath = Path.Combine(o.OutDir, ResultsFile);
        var existing = LoadResults(resultsPath);
        var latest = Latest(existing).ToDictionary(r => (r.CaseId, r.Variant));
        var spent = existing.Sum(r => r.CostUsd ?? 0);
        WriteManifest(cases, o);

        var todo = new List<(DecisionCase Case, BenchVariant Variant)>();
        var already = 0;
        foreach (var c in cases)
            foreach (var v in o.Variants)
            {
                if (latest.TryGetValue((c.CaseId, v.Key), out var prev) && (prev.Error is null || !o.RetryErrors)) { already++; continue; }
                todo.Add((c, v));
            }

        var gate = new object();
        var reserved = 0.0;
        var executed = 0;
        var stopped = false;
        var costByVariant = existing.Where(r => r.CostUsd is not null).GroupBy(r => r.Variant)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CostUsd!.Value).ToList());
        double Estimate(BenchVariant v) =>
            costByVariant.TryGetValue(v.Key, out var costs) && costs.Count > 0 ? costs.Max() * 1.25 : o.InitialCallEstimateUsd;

        using var sem = new SemaphoreSlim(Math.Max(1, o.Concurrency));
        using var writer = new StreamWriter(resultsPath, append: true, new System.Text.UTF8Encoding(false));
        var tasks = new List<Task>();
        foreach (var (c, v) in todo)
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            double estimate;
            lock (gate)
            {
                estimate = Estimate(v);
                if (spent + reserved + estimate > o.BudgetUsd)
                {
                    stopped = true;
                    sem.Release();
                    break;
                }
                reserved += estimate;
            }
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var r = await RunOneAsync(c, v, o, ct).ConfigureAwait(false);
                    lock (gate)
                    {
                        reserved -= estimate;
                        spent += r.CostUsd ?? 0;
                        if (r.CostUsd is double cost)
                        {
                            if (!costByVariant.TryGetValue(v.Key, out var list)) costByVariant[v.Key] = list = new List<double>();
                            list.Add(cost);
                        }
                        executed++;
                        writer.WriteLine(JsonSerializer.Serialize(r, Json.Compact));
                        writer.Flush();
                        o.Log?.Invoke($"[{executed}/{todo.Count}] {v.Key} {c.CaseId} : " +
                                      (r.Error is not null ? "erreur " + r.Error : $"{r.ActionLabel} ({r.Agreement}, {r.LatencyMs / 1000:0.0} s, {r.CostUsd:0.0000} $)") +
                                      $" | dépense {spent:0.000} $");
                    }
                }
                finally { sem.Release(); }
            }, ct));
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
        if (stopped) o.Log?.Invoke($"Arrêt : plafond de dépense atteint ({o.BudgetUsd:0.00} $, dépensé {spent:0.000} $).");
        return new BenchRunReport { Planned = todo.Count + already, AlreadyDone = already, Executed = executed, StoppedByBudget = stopped, SpentUsd = spent };
    }

    public static async Task<BenchResult> RunOneAsync(DecisionCase c, BenchVariant v, BenchRunOptions o, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        DecisionContext ctx;
        try { ctx = BuildContext(c); }
        catch (Exception ex) { return new BenchResult { CaseId = c.CaseId, Variant = v.Key, Effort = v.Effort, StartedUtc = started, Error = "cas illisible : " + ex.Message }; }

        var (payload, leaks) = BuildPayload(c, ctx, o.HistoryItems, v);
        if (leaks.Count > 0)
            return new BenchResult
            {
                CaseId = c.CaseId, Variant = v.Key, Effort = v.Effort, StartedUtc = started, PayloadChars = payload.Length,
                Error = "envoi bloqué : identifiants cachés présents dans le message (" + string.Join(", ", leaks) + ")",
            };

        var provider = o.ProviderFactory(v);
        StrategyResult res;
        try
        {
            res = provider is IPayloadStrategyProvider p
                ? await p.ProposeWithPayloadAsync(payload, ct).ConfigureAwait(false)
                : await provider.ProposeAsync(ctx, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            res = new StrategyResult { Error = "délai dépassé", Model = v.Model, LatencyMs = (DateTime.UtcNow - started).TotalMilliseconds };
        }
        catch (HttpRequestException ex)
        {
            res = new StrategyResult { Error = "réseau : " + ex.Message, Model = v.Model, LatencyMs = (DateTime.UtcNow - started).TotalMilliseconds };
        }

        var actualId = c.Actual?.ActionId;
        if (res.Output is null)
            return new BenchResult
            {
                CaseId = c.CaseId, Variant = v.Key, Model = res.Model, Effort = v.Effort, StartedUtc = started, Error = res.Error ?? "aucune sortie",
                ActualActionId = actualId, Agreement = "sans-réponse", LatencyMs = res.LatencyMs, InputTokens = res.InputTokens,
                CachedInputTokens = res.CachedInputTokens, OutputTokens = res.OutputTokens, ReasoningTokens = res.ReasoningTokens,
                CostUsd = res.CostUsd, Retries = res.Retries, ResponseId = res.ResponseId, PayloadChars = payload.Length, RawOutput = res.RawOutput,
            };

        var outcome = RecommendationValidator.Validate(res.Output, ctx, provider.Name, res.Model, res.LatencyMs, res.CostUsd);
        var chosen = ctx.LegalActions.FirstOrDefault(a => a.Id == res.Output.ActionId);
        var alt = res.Output.RejectedAlternativeId is string altId ? ctx.LegalActions.FirstOrDefault(a => a.Id == altId) : null;
        return new BenchResult
        {
            CaseId = c.CaseId,
            Variant = v.Key,
            Model = res.Model,
            Effort = v.Effort,
            StartedUtc = started,
            Legal = chosen is not null,
            Accepted = outcome.Accepted,
            ValidationReasons = outcome.Reasons,
            ActionId = res.Output.ActionId,
            ActionLabel = chosen?.Label ?? $"(id illégal : {res.Output.ActionId})",
            ActionKind = chosen?.Kind,
            Confidence = res.Output.Confidence,
            Uncertainty = outcome.Recommendation?.Uncertainty.ToString(),
            UncertaintyReasons = res.Output.UncertaintyReasons,
            Summary = res.Output.Summary,
            Rationale = res.Output.Rationale,
            PlanNextTurn = res.Output.PlanNextTurn,
            Risk = res.Output.Risk,
            RejectedAlternativeId = res.Output.RejectedAlternativeId,
            RejectedAlternativeLabel = alt?.Label ?? (res.Output.RejectedAlternativeId is null ? null : $"(id illégal : {res.Output.RejectedAlternativeId})"),
            RejectedAlternativeReason = res.Output.RejectedAlternativeReason,
            FollowUp = res.Output.FollowUp.Select(f => f.Text).ToList(),
            Warnings = outcome.Recommendation?.Warnings ?? new List<string>(),
            ActualActionId = actualId,
            Agreement = Agreement(chosen, ctx, c.Actual),
            Flags = chosen is null ? new List<DubiousFlag>() : DubiousMoveDetector.Analyze(ctx, chosen, res.Output),
            LatencyMs = res.LatencyMs,
            InputTokens = res.InputTokens,
            CachedInputTokens = res.CachedInputTokens,
            OutputTokens = res.OutputTokens,
            ReasoningTokens = res.ReasoningTokens,
            CostUsd = res.CostUsd,
            Retries = res.Retries,
            ResponseId = res.ResponseId,
            PayloadChars = payload.Length,
            RawOutput = res.RawOutput,
        };
    }

    private static void WriteManifest(IReadOnlyList<DecisionCase> cases, BenchRunOptions o)
    {
        var casesPath = Path.Combine(o.OutDir, CasesFile);
        if (!File.Exists(casesPath)) DatasetBuilder.Save(cases, casesPath);
        if (o.Instructions is not null) File.WriteAllText(Path.Combine(o.OutDir, InstructionsFile), o.Instructions, new System.Text.UTF8Encoding(false));
        var path = Path.Combine(o.OutDir, ManifestFile);
        var m = File.Exists(path)
            ? JsonSerializer.Deserialize<BenchRunManifest>(File.ReadAllText(path), Json.Read) ?? new BenchRunManifest { CreatedUtc = DateTime.UtcNow }
            : new BenchRunManifest { CreatedUtc = DateTime.UtcNow };
        m.UpdatedUtc = DateTime.UtcNow;
        m.CasesSource = o.CasesSource ?? m.CasesSource;
        m.CasesFingerprint = PromptBuilder.Fingerprint(File.ReadAllText(casesPath));
        m.CaseCount = cases.Count;
        foreach (var v in o.Variants) if (!m.Variants.Contains(v.Key)) m.Variants.Add(v.Key);
        if (o.Instructions is not null) m.InstructionsFingerprint = PromptBuilder.Fingerprint(o.Instructions);
        if (o.StrategyNotes is not null) m.StrategyFingerprint = PromptBuilder.Fingerprint(o.StrategyNotes);
        m.HistoryItems = o.HistoryItems;
        m.Concurrency = o.Concurrency;
        m.BudgetUsd = o.BudgetUsd;
        m.MaxOutputTokens = o.MaxOutputTokens;
        foreach (var model in o.Variants.Select(v => v.Model).Distinct())
            if (ModelPricing.Find(model) is { } price) m.Prices[model] = price;
        File.WriteAllText(path, JsonSerializer.Serialize(m, Json.Pretty), new System.Text.UTF8Encoding(false));
    }

    public static BenchRunManifest? LoadManifest(string runDir)
    {
        var path = Path.Combine(runDir, ManifestFile);
        return File.Exists(path) ? JsonSerializer.Deserialize<BenchRunManifest>(File.ReadAllText(path), Json.Read) : null;
    }
}

/// <summary>Annotation manuelle d'une réponse (ou du coup réellement joué, variante « réel »).</summary>
public sealed class Annotation
{
    public required string CaseId { get; init; }
    public required string Variant { get; init; }
    /// <summary>bon, discutable ou mauvais.</summary>
    public required string Verdict { get; set; }
    public string? Note { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

public sealed class AnnotationStore
{
    public const string RealMove = "réel";
    public static readonly string[] Verdicts = { "bon", "discutable", "mauvais" };
    public List<Annotation> Items { get; init; } = new();

    public Annotation? Get(string caseId, string variant) => Items.FirstOrDefault(a => a.CaseId == caseId && a.Variant == variant);

    public void Set(string caseId, string variant, string? verdict, string? note)
    {
        var existing = Get(caseId, variant);
        if (verdict is null)
        {
            if (existing is not null) Items.Remove(existing);
            return;
        }
        if (!Verdicts.Contains(verdict)) throw new ArgumentException($"verdict inconnu : {verdict}");
        if (existing is null) Items.Add(new Annotation { CaseId = caseId, Variant = variant, Verdict = verdict, Note = note, UpdatedUtc = DateTime.UtcNow });
        else { existing.Verdict = verdict; existing.Note = note; existing.UpdatedUtc = DateTime.UtcNow; }
    }

    public static AnnotationStore Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<AnnotationStore>(File.ReadAllText(path), Json.Read) ?? new AnnotationStore() : new AnnotationStore();

    public void Save(string path)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json.Pretty), new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
    }
}
