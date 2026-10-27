using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>Banc d'essai hors ligne (MVP 3A) : extraction des cas, absence de fuite, détecteur, fournisseur OpenAI simulé, exécution et synthèse.</summary>
public class BenchTests
{
    private static readonly string[] Fixtures = { TestData.PriestGame, TestData.RogueGame, TestData.RewindGame, TestData.GrommashGame, TestData.SeerGame };

    private static readonly Lazy<List<DecisionCase>> LazyCases = new(() =>
        Fixtures.SelectMany(f => ReplayFile.SplitGames(TestData.Lines(f))
                .SelectMany(g => DatasetBuilder.BuildGame(f, g, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 3, Decklist = TestData.DragonWarrior })))
            .ToList());

    private static List<DecisionCase> AllCases => LazyCases.Value;

    private static DecisionCase With(DecisionCase c, Action<JsonObject> mutate, List<string>? forbidden = null)
    {
        var node = JsonNode.Parse(c.Snapshot.GetRawText())!.AsObject();
        mutate(node);
        return new DecisionCase
        {
            CaseId = c.CaseId + "~mod", Source = c.Source, GameIndex = c.GameIndex, LineIndex = c.LineIndex, GameId = c.GameId, Revision = c.Revision,
            GameTurn = c.GameTurn, TurnNumber = c.TurnNumber, DecisionInTurn = c.DecisionInTurn, GameType = c.GameType, OpponentClass = c.OpponentClass,
            Tags = c.Tags, Snapshot = JsonSerializer.SerializeToElement(node), ForbiddenCardIds = forbidden ?? c.ForbiddenCardIds, Actual = c.Actual,
        };
    }

    private static JsonObject MinionNode(JsonObject snapshot, int entityId) =>
        snapshot["local"]!["board"]!.AsArray().Concat(snapshot["opponent"]!["board"]!.AsArray())
            .Select(n => n!.AsObject()).First(m => m["entityId"]!.GetValue<int>() == entityId);

    // ---------- Extraction des cas ----------

    [Fact]
    public void Dataset_contains_warrior_decision_points_with_labelled_real_moves()
    {
        Assert.True(AllCases.Count >= 40, $"{AllCases.Count} cas seulement");
        Assert.All(AllCases, c => Assert.Equal("WARRIOR", c.LoadSnapshot().Local!.HeroClass));
        var mapped = AllCases.Count(c => c.Actual?.ActionId is not null);
        Assert.True(mapped >= AllCases.Count * 0.95, $"coups réels associés : {mapped}/{AllCases.Count}");
        Assert.Equal(AllCases.Count, AllCases.Select(c => c.CaseId).Distinct().Count());
    }

    [Fact]
    public void Decision_snapshot_depends_only_on_lines_before_the_decision()
    {
        // Rejouer uniquement les lignes 0..LineIndex doit redonner exactement le même état : aucune ligne postérieure n'y contribue.
        foreach (var c in AllCases.Where((_, i) => i % 7 == 0))
        {
            var game = ReplayFile.SplitGames(TestData.Lines(c.Source)).Single(g => g.Index == c.GameIndex);
            var engine = TestData.NewEngine();
            for (int i = 0; i <= c.LineIndex; i++) engine.Process(game.Lines[i]);
            var snapshot = new GameStateProjector(TestData.Cards).Project(engine,
                new ProjectionContext { Transport = "replay-file", TransportDetail = c.Source, Decklist = TestData.DragonWarrior });
            Assert.Equal(JsonSerializer.Serialize(snapshot, Json.Compact), c.Snapshot.GetRawText());
        }
    }

    [Fact]
    public void Snapshot_round_trip_preserves_the_decision_context()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hscopilot-cases-{Guid.NewGuid():N}.jsonl");
        try
        {
            DatasetBuilder.Save(AllCases, path);
            var loaded = DatasetBuilder.Load(path);
            Assert.Equal(AllCases.Count, loaded.Count);
            for (int i = 0; i < loaded.Count; i++)
            {
                var a = BenchRunner.BuildContext(AllCases[i]);
                var b = BenchRunner.BuildContext(loaded[i]);
                Assert.Equal(a.LegalActions.Select(x => x.Id), b.LegalActions.Select(x => x.Id));
                Assert.Equal(PromptBuilder.BuildUserPayload(a), PromptBuilder.BuildUserPayload(b));
                Assert.Equal(AllCases[i].ForbiddenCardIds, loaded[i].ForbiddenCardIds);
                Assert.Equal(AllCases[i].Actual?.ActionId, loaded[i].Actual?.ActionId);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Real_move_is_the_next_sent_option_mapped_to_a_legal_action()
    {
        foreach (var c in AllCases.Where(c => c.Actual?.ActionId is not null))
        {
            var ctx = BenchRunner.BuildContext(c);
            var action = ctx.LegalActions.Single(a => a.Id == c.Actual!.ActionId);
            Assert.Equal(c.Actual!.OptionIndex, action.Kind == "end-turn" ? c.Actual.OptionIndex : action.OptionIndex);
            if (c.Actual.TargetEntityId > 0) Assert.Equal(c.Actual.TargetEntityId, action.TargetEntityId);
            // Le coup réel n'apparaît jamais dans le message envoyé au modèle.
            Assert.DoesNotContain("actual", PromptBuilder.BuildUserPayload(ctx), StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FindActual_maps_end_turn_and_stops_at_the_next_options_block()
    {
        var c = AllCases.First(x => BenchRunner.BuildContext(x).LegalActions.Any(a => a.Kind == "end-turn"));
        var ctx = BenchRunner.BuildContext(c);
        var options = new OptionsSet { Id = ctx.OptionsId, Time = TimeSpan.Zero };
        options.Options.Add(new OptionEntry { Index = 0, Type = "END_TURN", Error = "NONE" });
        const string send = "D 01:00:00.0000000 GameState.SendOption() - selectedOption=0 selectedSubOption=-1 selectedTarget=0 selectedPosition=0";
        const string header = "D 01:00:00.0000000 GameState.DebugPrintOptions() - id=999";

        var actual = DatasetBuilder.FindActual(new[] { send }, 0, options, ctx);
        Assert.Equal($"o{ctx.OptionsId}.fin", actual?.ActionId);
        Assert.Null(DatasetBuilder.FindActual(new[] { header, send }, 0, options, ctx));
    }

    [Fact]
    public void Selection_is_deterministic_and_spreads_over_games()
    {
        var a = DatasetBuilder.Select(AllCases, 12);
        var b = DatasetBuilder.Select(AllCases.AsEnumerable().Reverse().ToList(), 12);
        Assert.Equal(12, a.Count);
        Assert.Equal(a.Select(c => c.CaseId), b.Select(c => c.CaseId));
        Assert.True(a.Select(c => (c.Source, c.GameIndex)).Distinct().Count() >= 4);
        Assert.Contains(a, c => c.GameType == "GT_RANKED");
    }

    // ---------- Message envoyé au modèle ----------

    [Fact]
    public void Payloads_have_no_hidden_cards_trimmed_history_and_no_redundant_fields()
    {
        foreach (var c in AllCases)
        {
            var ctx = BenchRunner.BuildContext(c);
            var (payload, leaks) = BenchRunner.BuildPayload(c, ctx, PromptBuilder.DefaultHistoryItems);
            Assert.Empty(leaks);
            var node = JsonNode.Parse(payload)!;
            var state = node["state"]!.AsObject();
            Assert.True(state["history"]!.AsArray().Count <= PromptBuilder.DefaultHistoryItems);
            Assert.False(state.ContainsKey("legalOptions"));
            Assert.False(state.ContainsKey("source"));
            Assert.DoesNotContain("\"updatedAtRevision\"", payload);
            Assert.All(state["history"]!.AsArray(), h => Assert.False(h!.AsObject().ContainsKey("time")));
            Assert.NotEmpty(node["legalActions"]!.AsArray());
        }
    }

    [Fact]
    public async Task Runner_blocks_a_payload_that_contains_a_forbidden_card_without_calling_the_provider()
    {
        var c = AllCases.First();
        var visible = BenchRunner.BuildContext(c).LegalActions.First(a => a.SourceCardId is not null).SourceCardId!;
        var poisoned = With(c, _ => { }, new List<string> { visible });
        var provider = new FakeProvider();
        var r = await BenchRunner.RunOneAsync(poisoned, new BenchVariant("fake", "low"), Options(Path.GetTempPath(), provider), CancellationToken.None);
        Assert.StartsWith("envoi bloqué", r.Error);
        Assert.Equal(0, provider.Calls);
    }

    // ---------- Détecteur de coups douteux ----------

    [Fact]
    public void Lethal_by_attacks_ignored_is_flagged_and_taking_it_is_not()
    {
        var c = AllCases.First(x =>
        {
            var ctx = BenchRunner.BuildContext(x);
            return ctx.LegalActions.Any(a => a.Kind == "attack" && BoardMath.IsOpponentHero(ctx.State, a.TargetEntityId)) && ctx.LegalActions.Any(a => a.Kind == "end-turn");
        });
        var lethal = With(c, s => { s["opponent"]!["hero"]!["health"] = 1; s["opponent"]!["hero"]!["armor"] = 0; });
        var ctx = BenchRunner.BuildContext(lethal);
        Assert.Contains("létal-attaques", CaseTagger.Tags(ctx, 1));
        var endTurn = ctx.LegalActions.Single(a => a.Kind == "end-turn");
        var face = ctx.LegalActions.First(a => a.Kind == "attack" && BoardMath.IsOpponentHero(ctx.State, a.TargetEntityId));

        var flags = DubiousMoveDetector.Analyze(ctx, endTurn);
        Assert.Contains(flags, f => f.Code == "LETAL_IGNORE" && f.Severity == "douteux");
        Assert.Contains(flags, f => f.Code == "FIN_DE_TOUR_PREMATUREE");
        Assert.DoesNotContain(DubiousMoveDetector.Analyze(ctx, face), f => f.Code == "LETAL_IGNORE");
    }

    [Fact]
    public void Lethal_with_a_damage_card_from_hand_is_tagged_and_flagged_when_ignored()
    {
        var c = AllCases.First(x =>
        {
            var ctx = BenchRunner.BuildContext(x);
            return BoardMath.BestHandFaceDamage(ctx).Damage > 0 && ctx.LegalActions.Any(a => a.Kind == "end-turn");
        });
        var ctx0 = BenchRunner.BuildContext(c);
        var (damage, card) = BoardMath.BestHandFaceDamage(ctx0);
        var face = BoardMath.FaceDamageAvailable(ctx0);
        var lethal = With(c, s => { s["opponent"]!["hero"]!["health"] = face + damage; s["opponent"]!["hero"]!["armor"] = 0; });
        var ctx = BenchRunner.BuildContext(lethal);

        var tags = CaseTagger.Tags(ctx, 1);
        Assert.Contains("létal-avec-main", tags);
        Assert.DoesNotContain("létal-attaques", tags);
        Assert.Contains(DubiousMoveDetector.Analyze(ctx, ctx.LegalActions.Single(a => a.Kind == "end-turn")), f => f.Code == "LETAL_AVEC_MAIN_IGNORE");
        Assert.DoesNotContain(DubiousMoveDetector.Analyze(ctx, ctx.LegalActions.Single(a => a.Id == card!.Id)), f => f.Code == "LETAL_AVEC_MAIN_IGNORE");
    }

    [Fact]
    public void Losing_trade_is_flagged_with_the_numbers()
    {
        var c = AllCases.First(x =>
        {
            var ctx = BenchRunner.BuildContext(x);
            return ctx.LegalActions.Any(a => a.Kind == "attack" && BoardMath.Minion(ctx.State, a.SourceEntityId) is not null && BoardMath.Minion(ctx.State, a.TargetEntityId) is not null);
        });
        var ctx0 = BenchRunner.BuildContext(c);
        var attack = ctx0.LegalActions.First(a => a.Kind == "attack" && BoardMath.Minion(ctx0.State, a.SourceEntityId) is not null && BoardMath.Minion(ctx0.State, a.TargetEntityId) is not null);
        var modified = With(c, s =>
        {
            var attacker = MinionNode(s, attack.SourceEntityId!.Value);
            attacker["health"] = 1; attacker["attack"] = 1; attacker["keywords"] = new JsonArray();
            var target = MinionNode(s, attack.TargetEntityId!.Value);
            target["attack"] = 5; target["health"] = 9; target["keywords"] = new JsonArray();
        });
        var ctx = BenchRunner.BuildContext(modified);
        var flag = Assert.Single(DubiousMoveDetector.Analyze(ctx, ctx.LegalActions.Single(a => a.Id == attack.Id)), f => f.Code == "ECHANGE_PERDANT");
        Assert.Contains("1/1", flag.Message);
        Assert.Contains("5/9", flag.Message);
    }

    [Fact]
    public void Alternative_identical_to_the_choice_is_flagged()
    {
        var ctx = BenchRunner.BuildContext(AllCases.First());
        var a = ctx.LegalActions.First();
        Assert.Contains(DubiousMoveDetector.Analyze(ctx, a, new StrategyOutput { ActionId = a.Id, RejectedAlternativeId = a.Id }), f => f.Code == "ALTERNATIVE_IDENTIQUE");
    }

    [Fact]
    public void Detector_runs_on_every_real_decision_and_real_move_without_error()
    {
        var flagged = 0;
        foreach (var c in AllCases.Where(c => c.Actual?.ActionId is not null))
        {
            var ctx = BenchRunner.BuildContext(c);
            var played = ctx.LegalActions.Single(a => a.Id == c.Actual!.ActionId);
            var flags = DubiousMoveDetector.Analyze(ctx, played);
            Assert.All(flags, f => Assert.False(string.IsNullOrWhiteSpace(f.Message)));
            if (flags.Count > 0) flagged++;
        }
        Assert.True(flagged < AllCases.Count, "le détecteur ne doit pas signaler tous les coups réels");
    }

    // ---------- Fournisseur OpenAI (HTTP simulé) ----------

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body)> _responses;
        public List<(HttpRequestMessage Request, string Body)> Requests { get; } = new();
        public FakeHandler(params (HttpStatusCode, string)[] responses) => _responses = new(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request, await request.Content!.ReadAsStringAsync(cancellationToken)));
            var (code, body) = _responses.Dequeue();
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static string ResponseJson(string actionId, string status = "completed", string? reason = null)
    {
        var output = new StrategyOutput { ActionId = actionId, Confidence = "moyenne", Summary = "Jouer la carte", Rationale = "test" };
        var root = new JsonObject
        {
            ["id"] = "resp_test",
            ["status"] = status,
            ["model"] = "gpt-6.1-sol-2026-09-30",
            ["output"] = new JsonArray(
                new JsonObject { ["type"] = "reasoning", ["summary"] = new JsonArray() },
                new JsonObject { ["type"] = "message", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = JsonSerializer.Serialize(output) }) }),
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 1000,
                ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 400 },
                ["output_tokens"] = 500,
                ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 300 },
            },
        };
        if (reason is not null) root["incomplete_details"] = new JsonObject { ["reason"] = reason };
        return root.ToJsonString();
    }

    private static OpenAiResponsesProvider Provider(FakeHandler handler, string? key = "test-key") =>
        new(new OpenAiOptions { Model = "gpt-6.1-sol", ReasoningEffort = "high", RetryBaseDelay = TimeSpan.Zero, BaseUrl = "https://example.invalid/v1" },
            "instructions de test", new HttpClient(handler), () => key);

    [Fact]
    public async Task OpenAi_response_is_parsed_with_usage_and_cost()
    {
        var handler = new FakeHandler((HttpStatusCode.OK, ResponseJson("o1.2")));
        var r = await Provider(handler).ProposeWithPayloadAsync("{\"x\":1}", CancellationToken.None);
        Assert.Null(r.Error);
        Assert.Equal("o1.2", r.Output!.ActionId);
        Assert.Equal((1000, 400, 500, 300), (r.InputTokens, r.CachedInputTokens, r.OutputTokens, r.ReasoningTokens));
        // gpt-6.1-sol : 600 × 2 $ + 400 × 0,10 $ + 500 × 10 $ par million de jetons.
        Assert.Equal(0.00624, r.CostUsd!.Value, 8);
        Assert.Equal("resp_test", r.ResponseId);
        Assert.Equal(0, r.Retries);
    }

    [Fact]
    public async Task OpenAi_request_uses_strict_schema_and_keeps_the_key_out_of_the_body()
    {
        var handler = new FakeHandler((HttpStatusCode.OK, ResponseJson("o1.2")));
        await Provider(handler).ProposeWithPayloadAsync("{\"x\":1}", CancellationToken.None);
        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://example.invalid/v1/responses", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.DoesNotContain("test-key", body);
        var json = JsonNode.Parse(body)!;
        Assert.Equal("gpt-6.1-sol", json["model"]!.GetValue<string>());
        Assert.Equal("high", json["reasoning"]!["effort"]!.GetValue<string>());
        Assert.True(json["text"]!["format"]!["strict"]!.GetValue<bool>());
        Assert.False(json["store"]!.GetValue<bool>());
        var schema = json["text"]!["format"]!["schema"]!;
        Assert.Equal(schema["properties"]!.AsObject().Count, schema["required"]!.AsArray().Count);
    }

    [Fact]
    public async Task OpenAi_retries_on_rate_limit_then_succeeds()
    {
        var handler = new FakeHandler((HttpStatusCode.TooManyRequests, "{\"error\":{\"message\":\"rate limit\"}}"), (HttpStatusCode.OK, ResponseJson("o1.fin")));
        var r = await Provider(handler).ProposeWithPayloadAsync("{}", CancellationToken.None);
        Assert.Null(r.Error);
        Assert.Equal(1, r.Retries);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task OpenAi_incomplete_response_is_an_error_but_its_cost_is_kept()
    {
        var handler = new FakeHandler((HttpStatusCode.OK, ResponseJson("o1.2", status: "incomplete", reason: "max_output_tokens")));
        var r = await Provider(handler).ProposeWithPayloadAsync("{}", CancellationToken.None);
        Assert.Null(r.Output);
        Assert.Contains("max_output_tokens", r.Error);
        Assert.True(r.CostUsd > 0);
    }

    [Fact]
    public async Task OpenAi_error_and_missing_key_are_reported_without_output()
    {
        var handler = new FakeHandler((HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key\"}}"));
        var r = await Provider(handler).ProposeWithPayloadAsync("{}", CancellationToken.None);
        Assert.Contains("HTTP 401", r.Error);
        Assert.Null(r.Output);

        var none = new FakeHandler();
        var missing = await Provider(none, key: null).ProposeWithPayloadAsync("{}", CancellationToken.None);
        Assert.Contains("OPENAI_API_KEY", missing.Error);
        Assert.Empty(none.Requests);
    }

    [Fact]
    public void Documented_output_schema_matches_the_strict_schema_sent_to_openai()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "HsCopilot.sln"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        var doc = JsonNode.Parse(File.ReadAllText(Path.Combine(root!, "docs", "strategy-output.schema.json")))!;
        var sent = OpenAiResponsesProvider.OutputSchema();
        Assert.Equal(sent["required"]!.AsArray().Select(x => x!.GetValue<string>()).Order(), doc["required"]!.AsArray().Select(x => x!.GetValue<string>()).Order());
        Assert.Equal(sent["properties"]!.AsObject().Select(kv => kv.Key).Order(), doc["properties"]!.AsObject().Select(kv => kv.Key).Order());
    }

    [Fact]
    public void Prices_match_dated_model_names_and_unknown_models_have_no_cost()
    {
        Assert.NotNull(ModelPricing.Find("gpt-6.1-sol-2026-09-30"));
        Assert.Null(ModelPricing.Cost("modele-inconnu", 1000, 0, 1000));
    }

    // ---------- Exécution et synthèse ----------

    private sealed class FakeProvider : IStrategyProvider
    {
        public int Calls;
        public double Cost { get; init; } = 0.01;
        public Func<DecisionContext, string>? Choose { get; init; }
        public string Name => "fake";

        public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            var id = Choose?.Invoke(context) ?? context.LegalActions[0].Id;
            return Task.FromResult(new StrategyResult
            {
                Output = new StrategyOutput { ActionId = id, Confidence = "moyenne", Summary = "Conseil de test" },
                Model = "fake", CostUsd = Cost, LatencyMs = 1000, InputTokens = 100, OutputTokens = 10,
            });
        }
    }

    private static BenchRunOptions Options(string dir, IStrategyProvider provider, double budget = 10, params BenchVariant[] variants) => new()
    {
        OutDir = dir,
        Variants = variants.Length == 0 ? new[] { new BenchVariant("fake", "low") } : variants,
        ProviderFactory = _ => provider,
        Concurrency = 1,
        BudgetUsd = budget,
        InitialCallEstimateUsd = 0.1,
    };

    private static string TempDir() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"hscopilot-bench-{Guid.NewGuid():N}")).FullName;

    [Fact]
    public async Task Runner_writes_results_and_resumes_without_repeating_calls()
    {
        var dir = TempDir();
        try
        {
            var cases = DatasetBuilder.Select(AllCases, 3);
            var provider = new FakeProvider();
            var variants = new[] { BenchVariant.Parse("fake:low"), BenchVariant.Parse("fake:high") };
            var first = await BenchRunner.RunAsync(cases, Options(dir, provider, 10, variants), CancellationToken.None);
            Assert.Equal(6, first.Executed);
            Assert.Equal(6, BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile)).Count);
            Assert.True(File.Exists(Path.Combine(dir, BenchRunner.ManifestFile)));
            Assert.True(File.Exists(Path.Combine(dir, BenchRunner.CasesFile)));

            var second = await BenchRunner.RunAsync(cases, Options(dir, provider, 10, variants), CancellationToken.None);
            Assert.Equal(0, second.Executed);
            Assert.Equal(6, second.AlreadyDone);
            Assert.Equal(6, provider.Calls);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Runner_stops_before_exceeding_the_budget()
    {
        var dir = TempDir();
        try
        {
            var provider = new FakeProvider { Cost = 0.5 };
            var report = await BenchRunner.RunAsync(DatasetBuilder.Select(AllCases, 5), Options(dir, provider, budget: 1.0), CancellationToken.None);
            Assert.True(report.StoppedByBudget);
            Assert.True(report.SpentUsd <= 1.0, $"dépense {report.SpentUsd}");
            Assert.Equal(1, provider.Calls);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Summary_counts_agreement_latency_and_pairwise_agreement()
    {
        var dir = TempDir();
        try
        {
            var cases = AllCases.Where(c => c.Actual?.ActionId is not null && BenchRunner.BuildContext(c).LegalActions.Count >= 2).Take(2).ToList();
            var actualOf = cases.ToDictionary(c => c.GameId + c.Revision, c => c.Actual!.ActionId!);
            string Actual(DecisionContext ctx) => actualOf[ctx.GameId + ctx.Revision];
            string Other(DecisionContext ctx) => ctx.LegalActions.First(a => a.Id != Actual(ctx)).Id;
            var second = cases[1].GameId + cases[1].Revision;

            await BenchRunner.RunAsync(cases, Options(dir, new FakeProvider { Choose = Actual }, 10, BenchVariant.Parse("fake:low")), CancellationToken.None);
            await BenchRunner.RunAsync(cases, Options(dir, new FakeProvider { Choose = ctx => ctx.GameId + ctx.Revision == second ? Other(ctx) : Actual(ctx) }, 10, BenchVariant.Parse("fake:high")), CancellationToken.None);

            var summary = BenchSummary.Compute(cases, BenchRunner.LoadResults(Path.Combine(dir, BenchRunner.ResultsFile)));
            var low = summary.Variants.Single(v => v.Variant == "fake:low");
            var high = summary.Variants.Single(v => v.Variant == "fake:high");
            Assert.Equal((2, 2), (low.AgreeExact, low.WithActual));
            Assert.Equal((1, 2), (high.AgreeExact, high.WithActual));
            Assert.Equal(1000, low.LatencyP50Ms);
            Assert.Equal(0.02, low.TotalCostUsd, 6);
            var pair = Assert.Single(summary.Pairs);
            Assert.Equal((2, 1), (pair.Common, pair.SameAction));
            Assert.Equal(2, summary.ActualMapped);
            Assert.Contains("ton jeu (référence)", summary.ToMarkdown());
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Variants_parse_model_effort_and_repeat()
    {
        Assert.Equal(new BenchVariant("gpt-6.1-sol", "high"), BenchVariant.Parse("gpt-6.1-sol:high"));
        var rep = BenchVariant.Parse(" gpt-6.1-sol:medium#2 ");
        Assert.Equal(("gpt-6.1-sol", "medium", "2"), (rep.Model, rep.Effort, rep.Repeat));
        Assert.Equal("gpt-6.1-sol:medium#2", rep.Key);
        Assert.Equal("medium", BenchVariant.Parse("gpt-6-luna").Effort);
        // Compléments de contexte (étude stratégique du 26/10/2026) : clé stable, anciennes clés inchangées.
        var danger = BenchVariant.Parse("gpt-6.1-sol:medium+local+danger#2");
        Assert.Equal(("gpt-6.1-sol", "medium", "local+danger", "2"), (danger.Model, danger.Effort, danger.Extras, danger.Repeat));
        Assert.True(danger.Has("danger") && danger.Has("local") && !danger.Has("dang"));
        Assert.Equal("gpt-6.1-sol:medium+local+danger#2", danger.Key);
        Assert.Equal("gpt-6.1-sol:high", BenchVariant.Parse("gpt-6.1-sol:high").Key);
    }

    [Fact]
    public void The_local_variant_adds_the_live_local_analysis_and_the_danger_variant_adds_the_opponent_threat()
    {
        var c = AllCases.First(x => BenchRunner.BuildContext(x).Kind == DecisionContext.KindAction);
        var (plain, _) = BenchRunner.BuildPayload(c, BenchRunner.BuildContext(c), PromptBuilder.DefaultHistoryItems);
        var (local, _) = BenchRunner.BuildPayload(c, BenchRunner.BuildContext(c), PromptBuilder.DefaultHistoryItems, BenchVariant.Parse("m:medium+local"));
        var (danger, leaks) = BenchRunner.BuildPayload(c, BenchRunner.BuildContext(c), PromptBuilder.DefaultHistoryItems, BenchVariant.Parse("m:medium+local+danger"));
        Assert.DoesNotContain("maxFaceDamageThisTurn", plain);
        Assert.Contains("maxFaceDamageThisTurn", local);
        Assert.DoesNotContain("opponentThreat", local);
        Assert.Contains("opponentThreat", danger);
        Assert.Empty(leaks);
    }

    [Fact]
    public void Annotations_round_trip_and_reject_unknown_verdicts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hscopilot-ann-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AnnotationStore();
            store.Set("cas1", "gpt-6.1-sol:low", "discutable", "trop passif");
            store.Set("cas1", AnnotationStore.RealMove, "bon", null);
            store.Save(path);
            var loaded = AnnotationStore.Load(path);
            Assert.Equal("trop passif", loaded.Get("cas1", "gpt-6.1-sol:low")!.Note);
            Assert.Equal("bon", loaded.Get("cas1", AnnotationStore.RealMove)!.Verdict);
            loaded.Set("cas1", "gpt-6.1-sol:low", null, null);
            Assert.Null(loaded.Get("cas1", "gpt-6.1-sol:low"));
            Assert.Throws<ArgumentException>(() => loaded.Set("cas1", "x", "parfait", null));
        }
        finally { File.Delete(path); }
    }
}
