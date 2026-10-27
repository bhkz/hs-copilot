using System.Text;
using System.Text.Json;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;
using HsCopilot.Core.Util;

namespace HsCopilot.Cli;

/// <summary>Banc d'essai hors ligne du conseiller (MVP 3A).</summary>
public static class BenchCommand
{
    public static async Task<int> RunAsync(Args a)
    {
        var sub = a.Positional.FirstOrDefault();
        var rest = a.Positional.Skip(1).ToList();
        return sub switch
        {
            "build" => Build(a, rest),
            "run" => await Run(a, rest),
            "report" => Report(a, rest),
            "show" => Show(a, rest),
            "compare" => Compare(rest),
            _ => throw new ArgumentException("Sous-commande attendue : build, run, report ou show."),
        };
    }

    private static List<string> CollectLogs(IEnumerable<string> targets)
    {
        var files = new List<string>();
        foreach (var t in targets)
        {
            if (Directory.Exists(t))
                files.AddRange(Directory.EnumerateFiles(t, "Power*.log", SearchOption.AllDirectories)
                    .Concat(Directory.EnumerateFiles(t, "*.hdtreplay", SearchOption.AllDirectories)));
            else if (File.Exists(t)) files.Add(t);
            else throw new FileNotFoundException("Introuvable", t);
        }
        return files.Distinct().OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int Build(Args a, List<string> targets)
    {
        if (targets.Count == 0) throw new ArgumentException("Indique des Power.log, .hdtreplay ou un dossier.");
        var (cfg, cards) = Commands.LoadEnvironment(a);
        var options = new DatasetOptions
        {
            DecisionsPerTurn = a.GetInt("per-turn") ?? 2,
            RequiredHeroClass = a.Has("all-classes") ? null : (a.Get("class") ?? "WARRIOR"),
            Decklist = Commands.DeckFor(a, cfg),
        };
        var files = CollectLogs(targets);
        var cases = DatasetBuilder.Build(files, cards, options);
        var outPath = a.Get("out") ?? Path.Combine("bench", "cases.jsonl");
        DatasetBuilder.Save(cases, outPath);

        var games = cases.Select(c => (c.Source, c.GameIndex)).Distinct().Count();
        var mapped = cases.Count(c => c.Actual?.ActionId is not null);
        var leaks = 0;
        var sizes = new List<int>();
        foreach (var c in cases)
        {
            var ctx = BenchRunner.BuildContext(c);
            var (payload, found) = BenchRunner.BuildPayload(c, ctx, PromptBuilder.DefaultHistoryItems);
            sizes.Add(payload.Length);
            if (found.Count > 0) { leaks++; Console.Error.WriteLine($"FUITE {c.CaseId} : {string.Join(", ", found)}"); }
        }
        Console.WriteLine($"{files.Count} fichier(s), {games} partie(s) retenue(s), {cases.Count} point(s) de décision → {outPath}");
        Console.WriteLine($"Coup réel retrouvé et associé à une action légale : {mapped}/{cases.Count}");
        foreach (var c in cases.Where(c => c.Actual is { ActionId: null }).Take(5)) Console.WriteLine($"  non associé : {c.CaseId} : {c.Actual!.Note}");
        Console.WriteLine($"Contrôle anti-fuite des messages : {leaks} cas en défaut");
        if (sizes.Count > 0) Console.WriteLine($"Taille du message : moyenne {sizes.Average():0} caractères, max {sizes.Max()}");
        Console.WriteLine("Situations : " + string.Join(", ", cases.SelectMany(c => c.Tags).GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
        return leaks == 0 ? 0 : 3;
    }

    private static async Task<int> Run(Args a, List<string> rest)
    {
        var outDir = a.Get("out") ?? throw new ArgumentException("--out <dossier> requis.");
        var runCases = Path.Combine(outDir, BenchRunner.CasesFile);
        List<DecisionCase> cases;
        if (File.Exists(runCases))
        {
            cases = DatasetBuilder.Load(runCases);
            Console.Error.WriteLine($"Reprise : {cases.Count} cas figés dans {runCases}.");
        }
        else
        {
            var source = a.Get("cases") ?? Path.Combine("bench", "cases.jsonl");
            var all = DatasetBuilder.Load(source);
            if (a.Get("case") is string one) all = all.Where(c => c.CaseId == one).ToList();
            if (a.Has("only-deck-match"))
            {
                // Parties dont une carte sortie du deck n'appartient pas à la liste configurée : liste fausse pour cette partie.
                var mismatched = all.Where(c => c.LoadSnapshot().Uncertainty.Flags.Any(f => f.Code == "DECKLIST_MISMATCH"))
                    .Select(c => (c.Source, c.GameIndex)).ToHashSet();
                all = all.Where(c => !mismatched.Contains((c.Source, c.GameIndex))).ToList();
                Console.Error.WriteLine($"{mismatched.Count} partie(s) écartée(s) : liste de deck non conforme.");
            }
            if (a.Get("same-cases-as") is string other)
            {
                var ids = DatasetBuilder.Load(Path.Combine(other, BenchRunner.CasesFile)).Select(c => c.CaseId).ToHashSet();
                cases = all.Where(c => ids.Contains(c.CaseId)).OrderBy(c => c.CaseId, StringComparer.Ordinal).ToList();
            }
            else cases = DatasetBuilder.Select(all, a.GetInt("limit") ?? 0);
            Console.Error.WriteLine($"{cases.Count} cas sélectionnés sur {all.Count} ({source}).");
        }

        var variants = (a.Get("variants") ?? "gpt-6.1-sol:medium").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(BenchVariant.Parse).ToList();
        var notes = PromptBuilder.LoadStrategyNotes(a.Get("strategy"));
        var instructions = PromptBuilder.BuildInstructions(notes);
        var history = a.GetInt("history") ?? PromptBuilder.DefaultHistoryItems;
        Console.Error.WriteLine($"Instructions : {instructions.Length} caractères (empreinte {PromptBuilder.Fingerprint(instructions)}), stratégie : {(notes is null ? "aucune" : PromptBuilder.Fingerprint(notes))}");

        if (a.Has("dry-run"))
        {
            Directory.CreateDirectory(outDir);
            if (!File.Exists(runCases)) DatasetBuilder.Save(cases, runCases);
            var leaks = 0;
            var dump = a.Has("dump") ? Path.Combine(outDir, "payloads") : null;
            if (dump is not null)
            {
                Directory.CreateDirectory(dump);
                File.WriteAllText(Path.Combine(dump, "instructions.txt"), instructions, new UTF8Encoding(false));
            }
            foreach (var c in cases)
                foreach (var v in variants)
                {
                    var (payload, found) = BenchRunner.BuildPayload(c, BenchRunner.BuildContext(c), history, v);
                    if (found.Count > 0) leaks++;
                    if (dump is not null)
                        File.WriteAllText(Path.Combine(dump, PromptBuilder.Fingerprint(c.CaseId + v.Key) + ".json"), payload, new UTF8Encoding(false));
                    Console.WriteLine($"{c.CaseId} [{v.Key}] : {payload.Length} caractères, fuite : {(found.Count == 0 ? "aucune" : string.Join(", ", found))}");
                }
            Console.WriteLine($"Essai à blanc : {cases.Count} cas × {variants.Count} variante(s), aucun appel envoyé, {leaks} fuite(s).");
            return leaks == 0 ? 0 : 3;
        }

        foreach (var v in variants)
            if (!StrategyProviders.KeyStatus(v.Model).Present)
                throw new InvalidOperationException($"{StrategyProviders.KeyVariable(v.Model)} absente de l'environnement (variante {v.Key}).");
        var envOptions = OpenAiOptions.FromEnvironment();
        var http = new HttpClient { Timeout = envOptions.Timeout };
        IStrategyProvider Factory(BenchVariant v)
        {
            var text = v.Has("danger") ? instructions + BenchVariant.DangerInstructions : instructions;
            return StrategyProviders.IsAnthropic(v.Model)
                ? new AnthropicMessagesProvider(AnthropicOptions.FromEnvironment(v.Model, v.Effort), text, http)
                : new OpenAiResponsesProvider(OpenAiOptions.FromEnvironment(v.Model, v.Effort), text, http);
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); Console.Error.WriteLine("Interruption demandée : les résultats déjà reçus sont conservés."); };
        var options = new BenchRunOptions
        {
            OutDir = outDir,
            Variants = variants,
            ProviderFactory = Factory,
            Concurrency = a.GetInt("concurrency") ?? 4,
            BudgetUsd = double.TryParse(a.Get("budget"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b) ? b : 8.0,
            HistoryItems = history,
            Instructions = instructions,
            StrategyNotes = notes,
            CasesSource = a.Get("cases"),
            MaxOutputTokens = envOptions.MaxOutputTokens,
            RetryErrors = a.Has("retry-errors"),
            Log = line => Console.WriteLine(line),
        };
        BenchRunReport report;
        try { report = await BenchRunner.RunAsync(cases, options, cts.Token); }
        catch (OperationCanceledException) { Console.Error.WriteLine("Exécution interrompue."); return 4; }
        Console.WriteLine($"Terminé : {report.Executed} appel(s) exécuté(s), {report.AlreadyDone} déjà présent(s), dépense cumulée {report.SpentUsd:0.000} $" +
                          (report.StoppedByBudget ? " (arrêt sur plafond)" : ""));
        WriteReport(outDir);
        return 0;
    }

    private static int Report(Args a, List<string> rest)
    {
        var dir = rest.FirstOrDefault() ?? a.Get("dir") ?? throw new ArgumentException("Dossier d'exécution manquant.");
        var path = WriteReport(dir);
        Console.WriteLine(File.ReadAllText(path));
        return 0;
    }

    public static string WriteReport(string dir)
    {
        var (_, md) = BenchReport.Write(dir);
        Console.Error.WriteLine($"Synthèse écrite : {md}");
        return md;
    }

    /// <summary>Compare deux variantes de deux exécutions sur leurs cas communs (même identifiant de cas).</summary>
    private static int Compare(List<string> rest)
    {
        if (rest.Count < 4) throw new ArgumentException("Usage : bench compare <dossierA> <varianteA> <dossierB> <varianteB>");
        BenchResult[] Load(string dir, string variant)
        {
            var cases = DatasetBuilder.Load(Path.Combine(dir, BenchRunner.CasesFile)).ToDictionary(c => c.CaseId);
            return BenchRunner.Latest(BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile)))
                .Where(r => r.Variant == variant && r.Legal && cases.ContainsKey(r.CaseId))
                .Select(r => BenchSummary.Recheck(r, cases[r.CaseId], BenchRunner.BuildContext(cases[r.CaseId]))).ToArray();
        }
        var a = Load(rest[0], rest[1]).ToDictionary(r => r.CaseId);
        var b = Load(rest[2], rest[3]).ToDictionary(r => r.CaseId);
        var common = a.Keys.Intersect(b.Keys).OrderBy(x => x, StringComparer.Ordinal).ToList();
        int same = common.Count(id => a[id].ActionId == b[id].ActionId);
        int exactA = common.Count(id => a[id].Agreement == "exact"), exactB = common.Count(id => b[id].Agreement == "exact");
        int dubA = common.Count(id => a[id].Flags.Any(f => f.Severity == "douteux")), dubB = common.Count(id => b[id].Flags.Any(f => f.Severity == "douteux"));
        Console.WriteLine($"Cas communs : {common.Count}");
        Console.WriteLine($"Même action : {same}/{common.Count}");
        Console.WriteLine($"Identique à ton coup : A {exactA}/{common.Count}, B {exactB}/{common.Count}");
        Console.WriteLine($"Signalés douteux : A {dubA}, B {dubB}");
        Console.WriteLine($"Coût moyen : A {common.Average(id => a[id].CostUsd ?? 0):0.0000} $, B {common.Average(id => b[id].CostUsd ?? 0):0.0000} $");
        foreach (var id in common.Where(id => a[id].ActionId != b[id].ActionId))
            Console.WriteLine($"  {id} : A « {a[id].ActionLabel} » [{a[id].Agreement}] / B « {b[id].ActionLabel} » [{b[id].Agreement}]");
        return 0;
    }

    /// <summary>Un appel réel sur un cas, avec la réponse complète, la latence, les jetons et le coût.</summary>
    public static async Task<int> ProviderTest(Args a)
    {
        // Le fournisseur suit le modèle : « claude-… » pour Anthropic, sinon OpenAI.
        var model = a.Get("model") ?? (Environment.GetEnvironmentVariable("HSCOPILOT_OPENAI_MODEL") is { Length: > 0 } envModel ? envModel : "gpt-6.1-sol");
        (IStrategyProvider Provider, string Effort) Make(string? effort, string instructions)
        {
            if (StrategyProviders.IsAnthropic(model))
            {
                var o = AnthropicOptions.FromEnvironment(model, effort);
                return (new AnthropicMessagesProvider(o, instructions), o.Effort);
            }
            var oo = OpenAiOptions.FromEnvironment(model, effort);
            return (new OpenAiResponsesProvider(oo, instructions), oo.ReasoningEffort);
        }
        if (a.Positional.FirstOrDefault() == "ping")
        {
            var ping = StrategyProviders.IsAnthropic(model)
                ? await new AnthropicMessagesProvider(AnthropicOptions.FromEnvironment(model, a.Get("effort")), "").PingAsync(CancellationToken.None)
                : await new OpenAiResponsesProvider(OpenAiOptions.FromEnvironment(model, a.Get("effort")), "").PingAsync(CancellationToken.None);
            Console.WriteLine(ping.Ok ? $"OK : {ping.Message} Coût : {ping.CostUsd ?? 0:0.00000} $" : $"ÉCHEC : {ping.Message}");
            return ping.Ok ? 0 : 5;
        }
        var source = a.Get("cases") ?? Path.Combine("bench", "cases.jsonl");
        var all = DatasetBuilder.Load(source);
        var c = a.Get("case") is string id ? all.First(x => x.CaseId == id) : DatasetBuilder.Select(all, 1)[0];
        if (a.Positional.FirstOrDefault() == "stream")
        {
            var sp = Make(a.Get("effort") ?? "low", PromptBuilder.BuildInstructions(PromptBuilder.LoadStrategyNotes(a.Get("strategy")))).Provider;
            var sctx = BenchRunner.BuildContext(c);
            Console.WriteLine($"Cas {c.CaseId} (tour {c.TurnNumber}), {sp.Name}, flux");
            var r0 = await ((IStreamingStrategyProvider)sp).ProposeStreamingAsync(sctx, p => Console.WriteLine($"  +{p.ElapsedMs / 1000:0.00} s partiel : action={p.ActionId} conseil={(p.Summary is null ? "-" : "oui")} raison={(p.Why is null ? "-" : "oui")} plan={(p.PlanNextTurn is null ? "-" : "oui")}"), CancellationToken.None);
            var v = r0.Output is null ? null : RecommendationValidator.Validate(r0.Output, sctx, sp.Name, r0.Model, r0.LatencyMs, r0.CostUsd);
            Console.WriteLine($"Fin : {r0.LatencyMs / 1000:0.00} s, premier fragment {r0.FirstTokenMs / 1000:0.00} s, action+conseil {r0.PartialMs / 1000:0.00} s, erreur={r0.Error ?? "aucune"}");
            Console.WriteLine($"Jetons : entrée {r0.InputTokens} (cache {r0.CachedInputTokens}), sortie {r0.OutputTokens} (raisonnement {r0.ReasoningTokens}), coût {r0.CostUsd ?? 0:0.0000} $");
            if (v?.Recommendation is { } rec) Console.WriteLine($"Conseil validé : {rec.Action.Label} | {rec.Summary} | pourquoi : {rec.Why} | létal : {rec.Lethal} | plan : {rec.PlanNextTurn}");
            else Console.WriteLine("Rejet : " + string.Join(" ; ", v?.Reasons ?? new List<string>()));
            return r0.Error is null ? 0 : 5;
        }
        var ctx = BenchRunner.BuildContext(c);
        var notes = PromptBuilder.LoadStrategyNotes(a.Get("strategy"));
        var (provider, effortUsed) = Make(a.Get("effort"), PromptBuilder.BuildInstructions(notes));
        var r = await BenchRunner.RunOneAsync(c, new BenchVariant(model, effortUsed),
            new BenchRunOptions { OutDir = ".", Variants = Array.Empty<BenchVariant>(), ProviderFactory = _ => provider }, CancellationToken.None);
        Console.WriteLine($"Cas {c.CaseId} (tour {c.TurnNumber}), {provider.Name}");
        Console.WriteLine(JsonSerializer.Serialize(r, Json.Pretty));
        return r.Error is null ? 0 : 5;
    }

    /// <summary>Affiche un cas : état, actions légales, coup réel et réponses de chaque variante.</summary>
    private static int Show(Args a, List<string> rest)
    {
        var dir = rest.FirstOrDefault() ?? throw new ArgumentException("Dossier d'exécution manquant.");
        var cases = DatasetBuilder.Load(Path.Combine(dir, BenchRunner.CasesFile));
        var results = BenchRunner.Latest(BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile)));
        var selected = a.Get("case") is string id ? cases.Where(c => c.CaseId == id).ToList()
            : a.Has("flagged") ? cases.Where(c => results.Any(r => r.CaseId == c.CaseId && r.Flags.Any(f => f.Severity == "douteux"))).ToList()
            : cases;
        foreach (var c in selected)
        {
            var ctx = BenchRunner.BuildContext(c);
            Console.WriteLine(new string('=', 100));
            Console.WriteLine($"{c.CaseId} | tour {c.TurnNumber} | décision {c.DecisionInTurn} du tour | {string.Join(", ", c.Tags)}");
            Console.WriteLine(StateText.Render(ctx.State, 8));
            Console.WriteLine("Actions légales :");
            foreach (var l in ctx.LegalActions)
            {
                var marks = new List<string>();
                if (l.Id == c.Actual?.ActionId) marks.Add("JOUÉ");
                marks.AddRange(results.Where(r => r.CaseId == c.CaseId && r.ActionId == l.Id).Select(r => r.Variant));
                Console.WriteLine($"  {l.Id,-16} {l.Label}{(marks.Count > 0 ? "   <= " + string.Join(", ", marks) : "")}");
            }
            foreach (var r in results.Where(r => r.CaseId == c.CaseId).OrderBy(r => r.Variant))
            {
                Console.WriteLine($"--- {r.Variant} : {(r.Error ?? r.ActionLabel)} [{r.Agreement}] {r.LatencyMs / 1000:0.0} s, {r.OutputTokens} jetons sortie ({r.ReasoningTokens} raisonnement), {r.CostUsd:0.0000} $, confiance {r.Confidence}");
                if (r.Error is not null) continue;
                Console.WriteLine($"    Conseil : {r.Summary}");
                Console.WriteLine($"    Raisonnement : {r.Rationale}");
                if (r.RejectedAlternativeLabel is not null) Console.WriteLine($"    Alternative écartée : {r.RejectedAlternativeLabel} : {r.RejectedAlternativeReason}");
                if (r.PlanNextTurn is not null) Console.WriteLine($"    Tour suivant : {r.PlanNextTurn}");
                if (r.FollowUp.Count > 0) Console.WriteLine($"    Suite envisagée (non vérifiée) : {string.Join(" | ", r.FollowUp)}");
                foreach (var f in r.Flags) Console.WriteLine($"    [{f.Severity}] {f.Code} : {f.Message}");
            }
        }
        return 0;
    }
}
