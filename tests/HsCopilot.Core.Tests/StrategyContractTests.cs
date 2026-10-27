using System.Diagnostics;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Core.Tests;

/// <summary>Contrats du Strategy Engine : actions légales identifiées, liaison à la révision, invalidation, garde-fous.</summary>
public class StrategyContractTests
{
    private static GameStateSnapshot PriestTurn3() => TestData.AtLocalTurn(TestData.PriestGame, 3).Snapshot;

    private sealed class ScriptedProvider : IStrategyProvider
    {
        private readonly Func<DecisionContext, CancellationToken, Task<StrategyOutput>> _answer;
        public ScriptedProvider(Func<DecisionContext, CancellationToken, Task<StrategyOutput>> answer) => _answer = answer;
        public string Name => "test";
        public int Calls;
        public async Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return new StrategyResult { Output = await _answer(context, cancellationToken), Model = "test", InputTokens = 100, OutputTokens = 20, CostUsd = 0.001 };
        }
    }

    private static StrategyOutput Choose(string actionId, string summary = "Joue la carte proposée maintenant.") =>
        new() { ActionId = actionId, Confidence = "élevée", Summary = summary, Rationale = "test", FollowUp = new() { "étape suivante" } };

    // ------------------------------------------------------------------ Couches 1 et 2

    [Fact]
    public void Legal_actions_are_expanded_from_server_options_with_stable_ids()
    {
        var ctx = DecisionContextBuilder.Build(PriestTurn3())!;
        Assert.Equal(22, ctx.Revision);
        Assert.Equal(9, ctx.OptionsId);
        // Dragonnet, Forgeron, Suffusion × 3 cibles, pouvoir héroïque, lieu → Chevalier floramer, fin de tour.
        Assert.Equal(8, ctx.LegalActions.Count);
        Assert.Contains(ctx.LegalActions, a => a.Id == "o9.5.t21" && a.Kind == "location" && a.Label == "Activer Profondeurs Sanguines → Chevalier floramer");
        Assert.Equal(3, ctx.LegalActions.Count(a => a.SourceCardId == "FIR_939"));
        Assert.Equal(2, ctx.LegalActions.Single(a => a.SourceCardId == "FIR_939" && a.TargetEntityId == 21).ManaCost);
        Assert.Contains(ctx.LegalActions, a => a.Kind == "end-turn");
        Assert.Empty(ctx.VerifiedSequences);
        Assert.Contains("non implémenté", ctx.SequenceVerification);
        Assert.Contains(ctx.Hypotheses, h => h.Statement.Contains("La pièce"));
        Assert.True(ctx.OpponentInformationHidden);
    }

    [Fact]
    public void No_decision_context_without_fresh_server_options()
    {
        var engine = TestData.NewEngine();
        var lines = TestData.Lines(TestData.PriestGame);
        var next = ReplayRunner.RunUntilLocalTurn(engine, lines, 3);
        // Le joueur agit : l'état change, les options deviennent périmées.
        while (engine.OptionsFresh && next < lines.Count) engine.Process(lines[next++]);
        var s = TestData.Project(engine);
        Assert.False(DecisionContextBuilder.IsDecisionPoint(s));
        Assert.Null(DecisionContextBuilder.Build(s));
    }

    // ------------------------------------------------------------------ Couche 4 : validation

    [Fact]
    public void A_legal_choice_is_bound_to_game_revision_and_options()
    {
        var ctx = DecisionContextBuilder.Build(PriestTurn3())!;
        var outcome = RecommendationValidator.Validate(Choose("o9.5.t21"), ctx, "test", "m", 10, null);
        Assert.True(outcome.Accepted);
        var rec = outcome.Recommendation!;
        Assert.Equal(("111018-695039071", 22, 9), (rec.GameId, rec.Revision, rec.OptionsId));
        Assert.Equal("CORE_REV_990", rec.Action.SourceCardId);
        Assert.Contains("étapes suivantes non vérifiées", rec.Verification);
        Assert.All(rec.FollowUp, f => Assert.Equal("non vérifié", f.Status));
        // Main adverse cachée : jamais d'incertitude « faible ».
        Assert.NotEqual(UncertaintyLevel.Faible, rec.Uncertainty);
    }

    [Theory]
    [InlineData("o9.99")]          // option inexistante
    [InlineData("o8.5.t21")]       // options d'une autre révision
    [InlineData("o9.5.t54")]       // cible non autorisée par le serveur
    [InlineData("")]
    public void Actions_not_offered_by_the_server_are_rejected(string actionId)
    {
        var ctx = DecisionContextBuilder.Build(PriestTurn3())!;
        var outcome = RecommendationValidator.Validate(Choose(actionId), ctx, "test", null, 0, null);
        Assert.False(outcome.Accepted);
        Assert.Contains(outcome.Reasons, r => r.Contains("absente des actions légales"));
    }

    [Fact]
    public void Certainty_claims_are_downgraded_when_opponent_information_is_hidden()
    {
        var ctx = DecisionContextBuilder.Build(PriestTurn3())!;
        var outcome = RecommendationValidator.Validate(Choose("o9.5.t21", "Létal certain au prochain tour."), ctx, "test", null, 0, null);
        Assert.True(outcome.Accepted);
        Assert.Equal(UncertaintyLevel.Elevee, outcome.Recommendation!.Uncertainty);
        Assert.Contains(outcome.Recommendation.Warnings, w => w.Contains("certitude"));
        // « certains » n'est pas une affirmation de certitude.
        var neutral = RecommendationValidator.Validate(Choose("o9.5.t21", "Certains serviteurs adverses menacent ton héros."), ctx, "test", null, 0, null);
        Assert.Equal(UncertaintyLevel.Moyenne, neutral.Recommendation!.Uncertainty);
    }

    [Fact]
    public void Summary_is_limited_to_twenty_words_and_sequence_claims_are_flagged()
    {
        var ctx = DecisionContextBuilder.Build(PriestTurn3())!;
        var longText = string.Join(' ', Enumerable.Range(1, 30).Select(i => "mot" + i));
        var output = Choose("o9.5.t21", longText);
        output.FollowUp = new() { "séquence vérifiée : Grommash puis lieu" };
        var rec = RecommendationValidator.Validate(output, ctx, "test", null, 0, null).Recommendation!;
        Assert.Equal(20, rec.Summary.TrimEnd('…').Split(' ').Length);
        Assert.Contains(rec.Warnings, w => w.Contains("seule l'action immédiate"));
        Assert.All(rec.FollowUp, f => Assert.Equal("non vérifié", f.Status));
    }

    // ------------------------------------------------------------------ Invalidation

    private static (Recommendation Rec, GameEngine Engine, int NextLine, IReadOnlyList<string> Lines) RecAtTurn(string fixture, int turn, Func<DecisionContext, string> pick)
    {
        var engine = TestData.NewEngine();
        var lines = TestData.Lines(fixture);
        var next = ReplayRunner.RunUntilLocalTurn(engine, lines, turn);
        var ctx = DecisionContextBuilder.Build(TestData.Project(engine))!;
        var rec = RecommendationValidator.Validate(Choose(pick(ctx)), ctx, "test", null, 0, null).Recommendation!;
        return (rec, engine, next, lines);
    }

    [Fact]
    public void Recommendation_is_invalidated_after_the_player_acts_and_at_turn_end()
    {
        var (rec, engine, next, lines) = RecAtTurn(TestData.PriestGame, 3, ctx => ctx.LegalActions.First(a => a.Kind != "end-turn").Id);
        Assert.True(RecommendationValidator.CheckStillValid(rec, TestData.Project(engine)).Valid);
        var reasons = new List<string>();
        for (int i = next; i < lines.Count && reasons.Count < 50; i++)
        {
            engine.Process(lines[i]);
            if (!lines[i].Contains("DebugPrintPowerList", StringComparison.Ordinal)) continue;
            var (valid, reason) = RecommendationValidator.CheckStillValid(rec, TestData.Project(engine));
            if (!valid) reasons.Add(reason!);
        }
        Assert.Contains(reasons, r => r is InvalidationReasons.Followed or InvalidationReasons.OtherAction or InvalidationReasons.StateChanged);
        Assert.Contains(InvalidationReasons.TurnEnded, reasons);
    }

    [Fact]
    public void Rewind_invalidates_a_pending_recommendation()
    {
        var engine = TestData.NewEngine();
        var lines = TestData.Lines(TestData.RewindGame);
        Recommendation? rec = null;
        string? reasonAfterReset = null;
        foreach (var raw in lines)
        {
            var line = PowerLogLineParser.TryParse(raw);
            if (line is null) continue;
            if (line.Body == "RESET_GAME" && rec is null) Assert.Fail("aucune recommandation avant le premier Rembobinage");
            engine.Process(line);
            if (engine.ResetCount == 0 && line.Method == "DebugPrintOptions" && engine.OptionsFresh && engine.CurrentPlayerId == engine.LocalPlayerId)
            {
                var s = TestData.Project(engine);
                if (DecisionContextBuilder.Build(s) is { } ctx && ctx.LegalActions.Count > 1)
                    rec = RecommendationValidator.Validate(Choose(ctx.LegalActions[0].Id), ctx, "test", null, 0, null).Recommendation;
            }
            if (engine.ResetCount == 1 && reasonAfterReset is null && engine.BlockDepth == 0)
                reasonAfterReset = RecommendationValidator.CheckStillValid(rec!, TestData.Project(engine)).Reason;
        }
        Assert.Equal(InvalidationReasons.Rewind, reasonAfterReset);
    }

    // ------------------------------------------------------------------ Coordinateur

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(20);
    }

    private static List<GameStateSnapshot> DecisionSnapshots(string fixture, int count)
    {
        var result = new List<GameStateSnapshot>();
        for (int turn = 1; result.Count < count && turn < 20; turn++)
        {
            var engine = TestData.NewEngine();
            if (ReplayRunner.RunUntilLocalTurn(engine, TestData.Lines(fixture), turn) < 0) break;
            result.Add(TestData.Project(engine));
        }
        return result;
    }

    [Fact]
    public async Task Legal_answer_is_published_with_metrics()
    {
        var metrics = new MetricsRegistry();
        var provider = new ScriptedProvider((ctx, _) => Task.FromResult(Choose(ctx.LegalActions[0].Id)));
        await using var coordinator = new StrategyCoordinator(provider, new StrategyOptions { AllowRanked = true }, () => metrics);
        coordinator.OnSnapshot(PriestTurn3());
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.NotNull(coordinator.Board.Current);
        Assert.Equal(22, coordinator.Board.Current!.Revision);
        Assert.Equal(1, metrics.Counter("stratégie : actions proposées légales"));
        Assert.Equal(1, metrics.Stats(MetricNames.DecisionToAdvice).Count);
        Assert.Equal(0.001, metrics.Amount("stratégie : coût API (USD)"), 6);
    }

    [Fact]
    public async Task Ranked_games_get_no_realtime_advice_by_default()
    {
        var provider = new ScriptedProvider((ctx, _) => Task.FromResult(Choose(ctx.LegalActions[0].Id)));
        await using var coordinator = new StrategyCoordinator(provider);
        coordinator.OnSnapshot(PriestTurn3()); // partie classée
        await Task.Delay(200);
        Assert.Equal(0, provider.Calls);
        Assert.Contains(coordinator.Board.Events, e => e.Kind == "skipped" && e.Detail.Contains("classée"));
    }

    [Fact]
    public async Task State_change_during_a_request_discards_the_stale_answer()
    {
        var metrics = new MetricsRegistry();
        var release = new TaskCompletionSource();
        // Fournisseur qui ignore l'annulation : sa réponse arrive après le changement d'état.
        var provider = new ScriptedProvider(async (ctx, _) => { await release.Task; return Choose(ctx.LegalActions[0].Id); });
        await using var coordinator = new StrategyCoordinator(provider, new StrategyOptions { AllowRanked = true }, () => metrics);
        var snapshots = DecisionSnapshots(TestData.RogueGame, 2);
        coordinator.OnSnapshot(snapshots[0]);
        await WaitUntil(() => provider.Calls == 1);
        coordinator.OnSnapshot(snapshots[1]); // nouvelle révision pendant l'appel
        release.SetResult();
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.Equal(snapshots[1].Revision, coordinator.Board.Current!.Revision);
        Assert.True(metrics.Counter("stratégie : appels annulés (état modifié)") >= 1);
        Assert.Contains(coordinator.Board.Events, e => e.Kind == "discarded" && e.Revision == snapshots[0].Revision);
    }

    [Fact]
    public async Task Timeout_gives_no_advice_and_is_counted()
    {
        var metrics = new MetricsRegistry();
        var provider = new ScriptedProvider(async (ctx, ct) => { await Task.Delay(5000, ct); return Choose(ctx.LegalActions[0].Id); });
        await using var coordinator = new StrategyCoordinator(provider, new StrategyOptions { AllowRanked = true, Timeout = TimeSpan.FromMilliseconds(150) }, () => metrics);
        coordinator.OnSnapshot(PriestTurn3());
        await WaitUntil(() => metrics.Counter("stratégie : délais dépassés") == 1);
        Assert.Equal(1, metrics.Counter("stratégie : délais dépassés"));
        Assert.Null(coordinator.Board.Current);
        Assert.Contains(coordinator.Board.Events, e => e.Kind == "timeout");
    }

    [Fact]
    public async Task Illegal_answer_is_rejected_and_never_displayed()
    {
        var metrics = new MetricsRegistry();
        var provider = new ScriptedProvider((_, _) => Task.FromResult(Choose("o9.42.t999")));
        await using var coordinator = new StrategyCoordinator(provider, new StrategyOptions { AllowRanked = true }, () => metrics);
        coordinator.OnSnapshot(PriestTurn3());
        await WaitUntil(() => metrics.Counter("stratégie : actions proposées illégales ou invalides") == 1);
        Assert.Null(coordinator.Board.Current);
        Assert.Contains(coordinator.Board.Events, e => e.Kind == "rejected");
    }

    [Fact]
    public async Task OnSnapshot_never_blocks_even_with_a_slow_provider()
    {
        var provider = new ScriptedProvider(async (ctx, ct) => { await Task.Delay(3000, ct); return Choose(ctx.LegalActions[0].Id); });
        await using var coordinator = new StrategyCoordinator(provider, new StrategyOptions { AllowRanked = true });
        var sw = Stopwatch.StartNew();
        coordinator.OnSnapshot(PriestTurn3());
        Assert.True(sw.ElapsedMilliseconds < 200, $"OnSnapshot a pris {sw.ElapsedMilliseconds} ms");
    }

    // ------------------------------------------------------------------ Garde-fou anti-fuite

    [Fact]
    public void Leak_guard_matches_whole_card_ids_only()
    {
        var forbidden = new HashSet<string> { "CS2_101", "TEST_SECRET" };
        Assert.Empty(PayloadLeakGuard.FindLeaks("{\"cardId\":\"CS2_101t\"}", forbidden));
        Assert.Equal(new[] { "TEST_SECRET" }, PayloadLeakGuard.FindLeaks("{\"cardId\":\"TEST_SECRET\"}", forbidden));
    }

    [Fact]
    public void No_outgoing_payload_contains_a_hidden_card_at_any_real_decision_point()
    {
        var checkedPoints = 0;
        var forbiddenSeen = 0;
        foreach (var fixture in new[] { TestData.PriestGame, TestData.RogueGame, TestData.RewindGame, TestData.GrommashGame, TestData.SeerGame })
        {
            foreach (var s in DecisionSnapshots(fixture, 25))
            {
                var ctx = DecisionContextBuilder.Build(s);
                if (ctx is null) continue;
                checkedPoints++;
                forbiddenSeen += s.ForbiddenCardIds.Count;
                Assert.Empty(PayloadLeakGuard.FindLeaks(ctx.ToPayloadJson(), s.ForbiddenCardIds));
            }
        }
        Assert.True(checkedPoints >= 30, $"points de décision contrôlés : {checkedPoints}");
    }

    [Fact]
    public void Hidden_card_known_to_the_engine_is_listed_as_forbidden_and_absent_from_payload()
    {
        // Le bloc de reset d'un Rembobinage renvoie l'identifiant d'une carte de la main adverse (cas synthétique).
        var lines = new List<string>(TestData.Lines(TestData.PriestGame).Take(1600));
        var engine = TestData.NewEngine();
        foreach (var l in lines) engine.Process(l);
        var opponentHidden = engine.Entities.Values.First(e => e.Controller != engine.LocalPlayerId && e.Zone == "HAND");
        engine.Process($"D 11:11:46.0000000 GameState.DebugPrintPower() - SHOW_ENTITY - Updating Entity={opponentHidden.Id} CardID=GVG_001");
        engine.Process($"D 11:11:46.0000000 GameState.DebugPrintPower() -     tag=ZONE value=HAND");
        var s = TestData.Project(engine);
        Assert.Contains("GVG_001", s.ForbiddenCardIds);
        Assert.DoesNotContain("GVG_001", Core.Util.Json.Compact is var o ? System.Text.Json.JsonSerializer.Serialize(s, o) : "");
    }

    [Fact]
    public void Percentiles_use_nearest_rank()
    {
        var series = new LatencySeries();
        foreach (var v in Enumerable.Range(1, 100)) series.Add(v);
        var st = series.Stats("x");
        Assert.Equal(50, st.P50);
        Assert.Equal(95, st.P95);
        Assert.Equal(100, st.Max);
    }
}
