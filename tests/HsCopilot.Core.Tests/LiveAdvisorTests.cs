using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Config;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>Copilote en direct (MVP 3B) : mulligan et choix, déclenchement, plafonds, flux, affichage, réglages.</summary>
public class LiveAdvisorTests
{
    // ------------------------------------------------------------------ Outils

    private static GameStateSnapshot SnapshotBefore(string fixture, Func<string, bool> stopAt)
    {
        var engine = TestData.NewEngine();
        foreach (var line in TestData.Lines(fixture))
        {
            if (stopAt(line)) break;
            engine.Process(line);
        }
        return TestData.Project(engine);
    }

    private static GameStateSnapshot Mulligan() =>
        SnapshotBefore(TestData.GrommashGame, l => l.Contains("SendChoices() - id=1 ChoiceType=MULLIGAN", StringComparison.Ordinal));

    private static GameStateSnapshot GeneralChoice() =>
        SnapshotBefore(TestData.SeerGame, l => l.Contains("SendChoices() - id=3 ChoiceType=GENERAL", StringComparison.Ordinal));

    /// <summary>Points de décision d'une partie contre l'IA, regroupés par tour (plusieurs par tour).</summary>
    private static List<List<GameStateSnapshot>> DecisionsByTurn(string fixture = TestData.GrommashGame)
    {
        var game = ReplayFile.SplitGames(TestData.Lines(fixture)).Single();
        return DatasetBuilder.BuildGame(fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 6, Decklist = TestData.DragonWarrior })
            .GroupBy(c => c.GameTurn).Select(g => g.Select(c => c.LoadSnapshot()).ToList()).ToList();
    }

    private static GameStateSnapshot Mutate(GameStateSnapshot s, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(s, Json.Compact))!.AsObject();
        change(node);
        return node.Deserialize<GameStateSnapshot>(Json.Read)!;
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(15);
    }

    private sealed class FakeProvider : IStrategyProvider, IStreamingStrategyProvider
    {
        public Func<DecisionContext, StrategyOutput>? Answer { get; init; }
        public double Cost { get; init; } = 0.01;
        public bool EmitPartial { get; init; }
        public string? PartialActionId { get; init; }
        public TaskCompletionSource? Gate { get; set; }
        public int Calls;
        public string Name => "fake";

        public static string Useful(DecisionContext ctx) =>
            (ctx.LegalActions.FirstOrDefault(a => a.Kind is not ("end-turn" or "mulligan-keep")) ?? ctx.LegalActions[0]).Id;

        public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken) =>
            ProposeStreamingAsync(context, _ => { }, cancellationToken);

        public async Task<StrategyResult> ProposeStreamingAsync(DecisionContext context, Action<StrategyPartial> onPartial, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            var output = Answer?.Invoke(context) ?? new StrategyOutput { ActionId = Useful(context), Summary = "Conseil de test.", Why = "raison de test", Confidence = "moyenne" };
            if (EmitPartial) onPartial(new StrategyPartial { ActionId = PartialActionId ?? output.ActionId, Summary = output.Summary, ElapsedMs = 5 });
            if (Gate is { } gate) await gate.Task.WaitAsync(cancellationToken);
            return new StrategyResult { Output = output, Model = "fake", CostUsd = Cost, LatencyMs = 1 };
        }
    }

    private static (StrategyCoordinator Coordinator, List<AdvisorUpdate> Updates, MetricsRegistry Metrics) Start(FakeProvider provider, StrategyOptions? options = null)
    {
        var metrics = new MetricsRegistry();
        var coordinator = new StrategyCoordinator(provider, options ?? new StrategyOptions(), () => metrics);
        var updates = new List<AdvisorUpdate>();
        coordinator.Updated += u => { lock (updates) updates.Add(u); };
        return (coordinator, updates, metrics);
    }

    private static bool Has(List<AdvisorUpdate> updates, Func<AdvisorUpdate, bool> predicate)
    {
        lock (updates) return updates.Any(predicate);
    }

    // ------------------------------------------------------------------ Mulligan et choix

    [Fact]
    public void Mulligan_context_lists_each_offered_card_and_keep_all()
    {
        var s = Mulligan();
        Assert.Equal("MULLIGAN", s.PendingChoice!.ChoiceType);
        var ctx = DecisionContextBuilder.BuildChoice(s)!;
        Assert.Equal(DecisionContext.KindMulligan, ctx.Kind);
        Assert.Equal(s.PendingChoice.Offered.Count + 1, ctx.LegalActions.Count);
        Assert.Single(ctx.LegalActions, a => a.Kind == "mulligan-keep");
        Assert.All(ctx.LegalActions.Where(a => a.Kind == "mulligan-replace"), a => Assert.StartsWith("Remplacer ", a.Label));
        Assert.All(s.PendingChoice.Offered, o => Assert.False(string.IsNullOrEmpty(o.Text)));
        var payload = PromptBuilder.BuildUserPayload(ctx);
        Assert.Contains("\"decisionKind\":\"mulligan\"", payload);
        Assert.Empty(PayloadLeakGuard.FindLeaks(payload, s.ForbiddenCardIds));
    }

    [Fact]
    public void Mulligan_answer_is_validated_card_by_card()
    {
        var ctx = DecisionContextBuilder.BuildChoice(Mulligan())!;
        var replace = ctx.LegalActions.Where(a => a.Kind == "mulligan-replace").Take(2).Select(a => a.Id).ToList();
        var ok = RecommendationValidator.Validate(new StrategyOutput { ActionId = replace[0], ReplaceIds = replace, Summary = "Remplace les cartes chères.", Confidence = "moyenne" }, ctx, "t", null, 0, null);
        Assert.True(ok.Accepted);
        Assert.Equal(replace, ok.Recommendation!.ReplaceActions.Select(a => a.Id));
        Assert.Equal(DecisionContext.KindMulligan, ok.Recommendation.Kind);

        var bad = RecommendationValidator.Validate(new StrategyOutput { ActionId = replace[0], ReplaceIds = new() { "c1.999" }, Summary = "x", Confidence = "moyenne" }, ctx, "t", null, 0, null);
        Assert.False(bad.Accepted);

        var keep = ctx.LegalActions.Single(a => a.Kind == "mulligan-keep").Id;
        var all = RecommendationValidator.Validate(new StrategyOutput { ActionId = keep, Summary = "Garde tout.", Confidence = "élevée" }, ctx, "t", null, 0, null);
        Assert.Empty(all.Recommendation!.ReplaceActions);
        Assert.Equal("Garder toute la main", AdviceDisplay.Build(new AdvisorUpdate { Phase = AdvisorPhases.Ready, Recommendation = all.Recommendation }, DateTime.UtcNow).ActionText);
    }

    [Fact]
    public void Choice_advice_stays_valid_until_the_choice_is_made()
    {
        var s = GeneralChoice();
        var ctx = DecisionContextBuilder.BuildChoice(s)!;
        Assert.Equal(DecisionContext.KindChoice, ctx.Kind);
        Assert.All(ctx.LegalActions, a => Assert.Equal("choose", a.Kind));
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = ctx.LegalActions[0].Id, Summary = "Choisis la première.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.True(RecommendationValidator.CheckStillValid(rec, s).Valid);
        var resolved = Mutate(s, n => n.Remove("pendingChoice"));
        Assert.Equal((false, InvalidationReasons.ChoiceResolved), RecommendationValidator.CheckStillValid(rec, resolved));
    }

    // ------------------------------------------------------------------ Létal

    [Fact]
    public void Lethal_is_certain_only_without_secret_and_conditional_with_one()
    {
        var s = DecisionsByTurn().SelectMany(t => t).First(x =>
        {
            var c = DecisionContextBuilder.Build(x)!;
            return BoardMath.FaceDamageAvailable(c) > 0;
        });
        var face = BoardMath.FaceDamageAvailable(DecisionContextBuilder.Build(s)!);
        var low = Mutate(s, n => { n["opponent"]!["hero"]!["health"] = face; n["opponent"]!["hero"]!["armor"] = 0; n["opponent"]!["secrets"] = new JsonArray(); });
        Assert.Equal(LethalLevels.Certain, QuickAnalysis.AssessLethal(DecisionContextBuilder.Build(low)!).Level);

        var withSecret = Mutate(low, n =>
        {
            n["opponent"]!["secrets"] = new JsonArray(new JsonObject { ["entityId"] = 999, ["cardClass"] = "HUNTER", ["known"] = false, ["enteredTurn"] = 1, ["hypotheses"] = new JsonArray() });
            n["local"]!["hero"]!["health"] = 2;
            n["local"]!["hero"]!["armor"] = 0;
        });
        var assessment = QuickAnalysis.AssessLethal(DecisionContextBuilder.Build(withSecret)!);
        Assert.Equal(LethalLevels.Conditional, assessment.Level);
        Assert.Contains(assessment.Conditions, c => c.Contains("Piège explosif"));
    }

    [Fact]
    public void Model_cannot_claim_a_visible_lethal_the_board_does_not_show()
    {
        var s = DecisionsByTurn()[0][0];
        var ctx = DecisionContextBuilder.Build(s)!;
        Assert.Equal(LethalLevels.None, QuickAnalysis.AssessLethal(ctx).Level);
        var outcome = RecommendationValidator.Validate(new StrategyOutput { ActionId = FakeProvider.Useful(ctx), Summary = "Va au visage.", Lethal = "visible", Confidence = "élevée" }, ctx, "t", null, 0, null);
        Assert.Equal(LethalLevels.Plausible, outcome.Recommendation!.Lethal);
        Assert.Contains(outcome.Recommendation.Warnings, w => w.Contains("ramené"));
    }

    // ------------------------------------------------------------------ Coordinateur

    [Fact]
    public async Task Per_turn_cap_skips_automatic_calls_but_manual_reanalysis_goes_through()
    {
        // Le début de tour ne compte pas dans le quota de milieu de tour : avec 1 appel autorisé, la 2e décision passe,
        // la 3e est refusée avec un titre explicite.
        var turn = DecisionsByTurn().First(t => t.Count >= 3);
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { MaxCallsPerTurn = 1 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(turn[1]);
        await WaitUntil(() => provider.Calls == 2 && coordinator.Board.Current?.Revision == turn[1].Revision);
        coordinator.OnSnapshot(turn[2]);
        await WaitUntil(() => Has(updates, u => u.Phase == AdvisorPhases.Skipped));
        Assert.Equal(2, provider.Calls);
        var skipped = updates.Last(u => u.Phase == AdvisorPhases.Skipped);
        Assert.Equal(AdvisorReasons.Quota, skipped.Reason);
        Assert.Contains("Ctrl+Alt+R", skipped.Message);
        var view = AdviceDisplay.Build(skipped, DateTime.UtcNow);
        Assert.Equal("ANALYSES AUTO ÉPUISÉES", view.ActionTitle);
        Assert.Contains("1/1 analyses automatiques de milieu de tour", view.FooterText);
        Assert.NotEmpty(view.Facts);                            // l'analyse rapide reste affichée

        Assert.True(coordinator.RequestAnalysis());
        await WaitUntil(() => provider.Calls == 3 && coordinator.Board.Current?.Revision == turn[2].Revision);
        Assert.Equal(turn[2].Revision, coordinator.Board.Current!.Revision);
    }

    [Fact]
    public async Task Cost_cap_stops_calls_before_the_budget_is_exceeded()
    {
        var turns = DecisionsByTurn();
        var provider = new FakeProvider { Cost = 0.5 };
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { MaxCostPerGameUsd = 0.6, ReservePerCallUsd = 0.2 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turns[0][0]);
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(turns[1][0]);
        await WaitUntil(() => Has(updates, u => u.Message.Contains("budget")));
        Assert.Equal(1, provider.Calls);
        Assert.Equal(0.5, coordinator.CostSessionUsd, 6);
    }

    [Fact]
    public async Task Pause_blocks_automatic_calls_and_manual_reanalysis_still_works()
    {
        var turns = DecisionsByTurn();
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.SetPaused(true);
        coordinator.OnSnapshot(turns[0][0]);
        await Task.Delay(100);
        Assert.Equal(0, provider.Calls);
        Assert.True(Has(updates, u => u.Phase == AdvisorPhases.Paused));
        Assert.True(coordinator.RequestAnalysis());
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task No_call_when_ending_the_turn_is_the_only_option()
    {
        var s = Mutate(DecisionsByTurn()[0][0], n => n["legalOptions"]!["playable"] = new JsonArray());
        Assert.All(DecisionContextBuilder.Build(s)!.LegalActions, a => Assert.Equal("end-turn", a.Kind));
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await Task.Delay(100);
        Assert.Equal(0, provider.Calls);
        Assert.True(Has(updates, u => u.Message.Contains("finir le tour")));
    }

    [Fact]
    public async Task Identical_options_resent_under_a_new_id_reuse_the_advice_without_a_call()
    {
        var s = DecisionsByTurn()[0][0];
        var resent = Mutate(s, n =>
        {
            n["revision"] = s.Revision + 1;
            n["legalOptions"]!["optionsId"] = s.LegalOptions!.OptionsId + 100;
        });
        var provider = new FakeProvider();
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(resent);
        await WaitUntil(() => coordinator.Board.Current?.Revision == resent.Revision);
        Assert.Equal(1, provider.Calls);
        Assert.True(coordinator.Board.Current!.Reused);
        Assert.StartsWith($"o{resent.LegalOptions!.OptionsId}.", coordinator.Board.Current.Action.Id);
        Assert.Equal(1, metrics.Counter("stratégie : conseils réutilisés sans appel"));
    }

    [Fact]
    public async Task Streaming_shows_a_legal_action_early_and_never_an_invented_one()
    {
        var s = DecisionsByTurn()[0][0];
        var gate = new TaskCompletionSource();
        var provider = new FakeProvider { EmitPartial = true, Gate = gate };
        var (coordinator, updates, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await WaitUntil(() => Has(updates, u => u.Phase == AdvisorPhases.Partial));
        var partial = updates.Last(u => u.Phase == AdvisorPhases.Partial);
        Assert.NotNull(partial.PartialAction);
        Assert.Null(coordinator.Board.Current);
        Assert.Contains("en cours", AdviceDisplay.Build(partial, DateTime.UtcNow).VerifiedText);
        gate.SetResult();
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.Equal(1, metrics.Stats(MetricNames.DecisionToFirstShown).Count);

        var invented = new FakeProvider { EmitPartial = true, PartialActionId = "o9.99.t999", Gate = new TaskCompletionSource() };
        var (c2, u2, _) = Start(invented);
        await using var _c2 = c2;
        c2.OnSnapshot(s);
        await WaitUntil(() => invented.Calls == 1);
        await Task.Delay(100);
        Assert.False(Has(u2, u => u.Phase == AdvisorPhases.Partial));
        invented.Gate!.SetResult();
    }

    [Fact]
    public async Task Call_is_cancelled_as_soon_as_the_decision_has_passed()
    {
        var s = DecisionsByTurn()[0][0];
        var opponentTurn = Mutate(s, n =>
        {
            n["revision"] = s.Revision + 5;
            n["game"]!["activePlayer"] = "opponent";
            n["legalOptions"]!["fresh"] = false;
        });
        var provider = new FakeProvider { Gate = new TaskCompletionSource() };
        var (coordinator, updates, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await WaitUntil(() => provider.Calls == 1);
        coordinator.OnSnapshot(opponentTurn);
        Assert.Equal(1, metrics.Counter("stratégie : appels annulés (décision passée)"));
        Assert.True(Has(updates, u => u.Phase == AdvisorPhases.Idle && u.Message.Contains("Tour adverse")));
        await Task.Delay(100);
        Assert.Null(coordinator.Board.Current);
    }

    [Fact]
    public async Task Quick_mid_turn_actions_send_no_call_for_intermediate_states()
    {
        var turn = DecisionsByTurn().First(t => t.Count >= 3);
        var provider = new FakeProvider();
        var (coordinator, _, _) = Start(provider, new StrategyOptions { MidTurnDebounce = TimeSpan.FromMilliseconds(400) });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);                       // début de tour : appel immédiat
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(turn[1]);                       // action rapide : attente de 400 ms…
        await Task.Delay(100);
        coordinator.OnSnapshot(turn[2]);                       // … interrompue par l'action suivante : aucun appel pour turn[1]
        await WaitUntil(() => coordinator.Board.Current?.Revision == turn[2].Revision);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task A_cancelled_call_counts_its_estimated_cost_against_the_budget()
    {
        var s = DecisionsByTurn()[0][0];
        var opponentTurn = Mutate(s, n =>
        {
            n["revision"] = s.Revision + 5;
            n["game"]!["activePlayer"] = "opponent";
            n["legalOptions"]!["fresh"] = false;
        });
        var provider = new FakeProvider { Gate = new TaskCompletionSource() };
        var (coordinator, _, metrics) = Start(provider, new StrategyOptions { CancelledCallCostUsd = 0.015 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await WaitUntil(() => provider.Calls == 1);
        coordinator.OnSnapshot(opponentTurn);
        await WaitUntil(() => metrics.Amount("stratégie : coût estimé des appels annulés (USD)") > 0);
        Assert.Equal(0.015, metrics.Amount("stratégie : coût estimé des appels annulés (USD)"), 6);
        Assert.Equal(0.015, coordinator.CostSessionUsd, 6);
    }

    // ------------------------------------------------------------------ Corrections après la première vraie partie

    private static GameStateSnapshot AsChoice(GameStateSnapshot s, int choiceId)
    {
        var card = s.Local!.Hand.Cards.First(c => c.CardId is not null);
        return Mutate(s, n =>
        {
            n["revision"] = s.Revision + 1;
            n["pendingChoice"] = new JsonObject
            {
                ["id"] = choiceId,
                ["choiceType"] = "GENERAL",
                ["countMin"] = 1,
                ["countMax"] = 1,
                ["offered"] = new JsonArray(new JsonObject { ["entityId"] = card.EntityId, ["cardId"] = card.CardId, ["name"] = card.Name }),
            };
        });
    }

    [Fact]
    public async Task No_analysis_from_a_catch_up_state_but_manual_reanalysis_works()
    {
        var s = DecisionsByTurn()[0][0];
        s.LiveTimestamps = false;
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { RequireLiveState = true });
        await using var _c = coordinator;
        coordinator.OnSnapshot(s);
        await Task.Delay(100);
        Assert.Equal(0, provider.Calls);
        Assert.True(Has(updates, u => u.Message.Contains("rattrapage")));
        Assert.True(coordinator.RequestAnalysis());
        await WaitUntil(() => coordinator.Board.Current is not null);
        var live = DecisionsByTurn()[1][0];
        live.LiveTimestamps = true;
        coordinator.OnSnapshot(live);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task A_choice_is_analysed_even_when_the_turn_quota_is_used()
    {
        var turn = DecisionsByTurn().First(t => t.Count >= 2);
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { MaxCallsPerTurn = 0 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);                       // début de tour : prioritaire
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(turn[1]);
        await WaitUntil(() => Has(updates, u => u.Reason == AdvisorReasons.Quota));
        Assert.Equal(1, provider.Calls);
        coordinator.OnSnapshot(AsChoice(turn[1], 77));
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task Turn_start_and_choices_do_not_consume_the_mid_turn_quota()
    {
        // Partie réelle du 25/10/2026 : au tour 5, Découverte et début de tour avaient épuisé le quota ; la décision suivante
        // restait sans conseil, affichée « EN ATTENTE » pendant 70 s.
        var turn = DecisionsByTurn().First(t => t.Count >= 2);
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { MaxCallsPerTurn = 1 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(AsChoice(turn[0], 78));
        await WaitUntil(() => provider.Calls == 2 && coordinator.Board.Current?.Kind == DecisionContext.KindChoice);
        coordinator.OnSnapshot(turn[1]);
        await WaitUntil(() => provider.Calls == 3);
        await WaitUntil(() => coordinator.Board.Current?.Revision == turn[1].Revision);
        Assert.Equal(1, updates.Last().MidTurnCallsThisTurn);
        Assert.Equal(3, updates.Last().CallsThisTurn);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task A_cancelled_call_gives_back_its_turn_quota_unless_legacy_mode(bool legacy, int expectedCalls)
    {
        var turn = DecisionsByTurn().First(t => t.Count >= 3);
        var passed = Mutate(turn[1], n => { n["revision"] = turn[1].Revision + 1; n["legalOptions"]!["fresh"] = false; });
        var provider = new FakeProvider { Cost = 0 };
        var (coordinator, _, metrics) = Start(provider, new StrategyOptions { MaxCallsPerTurn = 1, CancelledCallCostUsd = 0.01, CancelledCallsUseTurnCap = legacy });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);                       // début de tour : prioritaire, hors quota
        await WaitUntil(() => coordinator.Board.Current is not null);
        provider.Gate = new TaskCompletionSource();
        coordinator.OnSnapshot(turn[1]);                       // milieu de tour : appel…
        await WaitUntil(() => provider.Calls == 2);
        coordinator.OnSnapshot(passed);                        // … annulé : tu as joué
        await WaitUntil(() => metrics.Amount("stratégie : coût estimé des appels annulés (USD)") > 0);
        provider.Gate = null;
        coordinator.OnSnapshot(turn[2]);                       // décision suivante du même tour
        await Task.Delay(150);
        Assert.Equal(expectedCalls + 1, provider.Calls);
        Assert.Equal(0.01, coordinator.CostSessionUsd, 6);     // le coût estimé reste compté dans tous les cas
    }

    [Fact]
    public void Ending_the_turn_with_hero_power_or_attacks_left_carries_a_local_caution()
    {
        var s = DecisionsByTurn().SelectMany(t => t).First(x =>
        {
            var c = DecisionContextBuilder.Build(x)!;
            return c.LegalActions.Any(a => a.Kind == "hero-power") && c.LegalActions.Any(a => a.Kind == "end-turn");
        });
        var ctx = DecisionContextBuilder.Build(s)!;
        Assert.Contains(QuickAnalysis.RemainingOpportunities(ctx), o => o.StartsWith("pouvoir héroïque"));
        var endTurn = ctx.LegalActions.Single(a => a.Kind == "end-turn");
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = endTurn.Id, Summary = "Finis le tour.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.Contains("pouvoir héroïque", rec.Caution);
        Assert.Contains(rec.Warnings, w => w.Contains("fin de tour conseillée"));
        var view = AdviceDisplay.Build(new AdvisorUpdate { Phase = AdvisorPhases.Ready, Recommendation = rec, State = s }, DateTime.UtcNow);
        Assert.StartsWith("Avant de finir le tour", view.CautionText);

        var other = ctx.LegalActions.First(a => a.Kind != "end-turn");
        var fine = RecommendationValidator.Validate(new StrategyOutput { ActionId = other.Id, Summary = "Joue ceci.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.Null(fine.Caution);
    }

    [Fact]
    public void Quick_facts_name_the_attacks_still_possible()
    {
        var s = DecisionsByTurn().SelectMany(t => t).First(x => DecisionContextBuilder.Build(x)!.LegalActions.Any(a => a.Kind == "attack"));
        var ctx = DecisionContextBuilder.Build(s)!;
        var fact = QuickAnalysis.Analyze(ctx).Last();
        Assert.StartsWith("Encore possible : attaque de ", fact.Text);
    }

    [Fact]
    public async Task Advisor_trace_records_each_displayed_advice_once()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"hscopilot-trace-{Guid.NewGuid():N}");
        try
        {
            var s = DecisionsByTurn()[0][0];
            s.LiveTimestamps = true;
            await using (var host = new AdvisorHost(new AdvisorSettings { DebounceMs = 0 }, () => new MetricsRegistry(), _ => new FakeProvider { EmitPartial = true }, dir))
            {
                host.OnSnapshot(s);
                await WaitUntil(() => host.Coordinator!.Board.Current is not null);
                await Task.Delay(50);
            }
            var file = Assert.Single(Directory.GetFiles(dir, "*.jsonl"));
            var entries = File.ReadAllLines(file).Select(l => JsonSerializer.Deserialize<AdvisorTraceEntry>(l, Json.Read)!).ToList();
            Assert.Single(entries, e => e.Phase == AdvisorPhases.Partial);
            var ready = Assert.Single(entries, e => e.Phase == AdvisorPhases.Ready);
            Assert.Equal(s.Revision, ready.Revision);
            Assert.False(string.IsNullOrEmpty(ready.Action));
            Assert.Equal(s.GameId, ready.GameId);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Ranked_advice_works_once_enabled_in_the_config_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hscopilot-ranked-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ \"advisor\": { \"allowRanked\": true } }");
            var (config, _) = CopilotConfig.Load(path);
            Assert.True(config.Advisor.AllowRanked);
            var provider = new FakeProvider();
            var (coordinator, _, _) = Start(provider, config.Advisor.ToStrategyOptions());
            await using var _c = coordinator;
            var ranked = TestData.AtLocalTurn(TestData.PriestGame, 3).Snapshot;
            ranked.LiveTimestamps = true;
            coordinator.OnSnapshot(ranked);
            await WaitUntil(() => coordinator.Board.Current is not null, 8000);
            Assert.Equal(1, provider.Calls);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task A_withdrawn_advice_is_never_displayed_as_valid()
    {
        var turn = DecisionsByTurn().First(t => t.Count >= 2);
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider, new StrategyOptions { MaxCallsPerTurn = 0 });
        await using var _c = coordinator;
        coordinator.OnSnapshot(turn[0]);
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(turn[1]);
        await WaitUntil(() => Has(updates, u => u.Phase == AdvisorPhases.Stale));
        var stale = updates.First(u => u.Phase == AdvisorPhases.Stale);
        var view = AdviceDisplay.Build(stale, DateTime.UtcNow);
        Assert.False(view.HasAdvice);
        Assert.Equal("", view.ActionText);
        Assert.Equal("", view.VerifiedText);
        Assert.Equal("CONSEIL RETIRÉ", view.ActionTitle);
        Assert.Null(coordinator.Board.Current);
    }

    [Fact]
    public async Task Mulligan_is_advised_through_the_coordinator()
    {
        var provider = new FakeProvider
        {
            Answer = ctx =>
            {
                var first = ctx.LegalActions.First(a => a.Kind == "mulligan-replace").Id;
                return new StrategyOutput { ActionId = first, ReplaceIds = new() { first }, Summary = "Remplace la carte la plus chère.", Confidence = "moyenne" };
            },
        };
        var (coordinator, _, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(Mulligan());
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.Equal(DecisionContext.KindMulligan, coordinator.Board.Current!.Kind);
        Assert.Single(coordinator.Board.Current.ReplaceActions);
    }

    [Fact]
    public async Task Ranked_games_stay_disabled_by_default()
    {
        var ranked = TestData.AtLocalTurn(TestData.PriestGame, 3).Snapshot;
        var provider = new FakeProvider();
        var (coordinator, updates, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(ranked);
        await Task.Delay(100);
        Assert.Equal(0, provider.Calls);
        Assert.True(Has(updates, u => u.Phase == AdvisorPhases.Disabled && u.Message.Contains("classée")));
    }

    // ------------------------------------------------------------------ Affichage

    [Fact]
    public void Display_shows_the_target_stats_and_what_is_verified()
    {
        var s = DecisionsByTurn().SelectMany(t => t).First(x => DecisionContextBuilder.Build(x)!.LegalActions.Any(a => a.TargetEntityId is not null && a.TargetSide == "opponent" && BoardMath.Minion(x, a.TargetEntityId) is not null));
        var ctx = DecisionContextBuilder.Build(s)!;
        var action = ctx.LegalActions.First(a => a.TargetSide == "opponent" && BoardMath.Minion(s, a.TargetEntityId) is not null);
        var target = BoardMath.Minion(s, action.TargetEntityId)!;
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = action.Id, Summary = "Élimine la menace.", Why = "retire une menace", PlanNextTurn = "Prépare le Wyrm.", Confidence = "moyenne", FollowUp = new() { "a", "b", "c" } }, ctx, "t", "m", 1200, 0.002).Recommendation!;
        var view = AdviceDisplay.Build(new AdvisorUpdate { Phase = AdvisorPhases.Ready, Recommendation = rec, State = s }, DateTime.UtcNow);
        Assert.True(view.HasAdvice);
        Assert.EndsWith($" {target.Attack}/{target.Health}", view.ActionText);
        Assert.Contains("serveur", view.VerifiedText);
        Assert.Equal("retire une menace", view.WhyText);
        Assert.Equal("Prépare le Wyrm.", view.PlanText);
        Assert.Equal(3, view.Next.Count);                 // deux étapes affichées, puis la mention des autres
        Assert.StartsWith("2. ", view.Next[0]);
        Assert.Contains("+1 autre", view.Next[2]);
    }

    // ------------------------------------------------------------------ Fournisseur en flux

    private sealed class StreamHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _body;
        public string? RequestBody;
        public StreamHandler(HttpStatusCode code, string body) { _code = code; _body = body; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_code) { Content = new StringContent(_body, Encoding.UTF8, _code == HttpStatusCode.OK ? "text/event-stream" : "application/json") };
        }
    }

    private static string Sse(string type, JsonObject data)
    {
        data["type"] = type;
        return $"event: {type}\ndata: {data.ToJsonString()}\n\n";
    }

    [Fact]
    public async Task Streaming_response_gives_partial_fields_then_usage_and_cost()
    {
        var output = new StrategyOutput { ActionId = "o1.2", Summary = "Joue \"la\" carte", Why = "tempo", Lethal = "aucun", Confidence = "moyenne", Rationale = "r" };
        var full = JsonSerializer.Serialize(output);
        var cut1 = full.IndexOf("\"summary\"", StringComparison.Ordinal) + 14;
        var final = new JsonObject
        {
            ["id"] = "resp_s",
            ["status"] = "completed",
            ["model"] = "gpt-6.1-sol",
            ["output"] = new JsonArray(new JsonObject { ["type"] = "message", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = full }) }),
            ["usage"] = new JsonObject { ["input_tokens"] = 1000, ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 0 }, ["output_tokens"] = 100, ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 20 } },
        };
        var body = Sse("response.created", new JsonObject())
                   + Sse("response.output_text.delta", new JsonObject { ["delta"] = full[..cut1] })
                   + Sse("response.output_text.delta", new JsonObject { ["delta"] = full[cut1..] })
                   + Sse("response.completed", new JsonObject { ["response"] = final });
        var handler = new StreamHandler(HttpStatusCode.OK, body);
        var provider = new OpenAiResponsesProvider(new OpenAiOptions { Model = "gpt-6.1-sol", BaseUrl = "https://example.invalid/v1" }, "i", new HttpClient(handler), () => "test-key");
        var partials = new List<StrategyPartial>();
        var r = await provider.ProposeWithPayloadStreamingAsync("{}", partials.Add, CancellationToken.None);

        Assert.True(JsonNode.Parse(handler.RequestBody!)!["stream"]!.GetValue<bool>());
        Assert.Null(r.Error);
        Assert.Equal("o1.2", r.Output!.ActionId);
        Assert.Equal(0.003, r.CostUsd!.Value, 6);
        Assert.NotNull(r.FirstTokenMs);
        Assert.NotNull(r.PartialMs);
        Assert.Equal("o1.2", partials[0].ActionId);
        Assert.Null(partials[0].Summary);
        Assert.Equal("Joue \"la\" carte", partials.Last().Summary);
        Assert.Equal("tempo", partials.Last().Why);
    }

    [Fact]
    public async Task Streaming_http_error_is_reported_without_partial()
    {
        var handler = new StreamHandler(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Incorrect API key\"}}");
        var provider = new OpenAiResponsesProvider(new OpenAiOptions { MaxRetries = 0 }, "i", new HttpClient(handler), () => "k");
        var partials = new List<StrategyPartial>();
        var r = await provider.ProposeWithPayloadStreamingAsync("{}", partials.Add, CancellationToken.None);
        Assert.Contains("HTTP 401", r.Error);
        Assert.Empty(partials);
    }

    /// <summary>Renvoie les corps de réponse dans l'ordre, un par requête (flux SSE).</summary>
    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<string> _bodies;
        public int Calls;
        public SequenceHandler(params string[] bodies) => _bodies = new Queue<string>(bodies);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_bodies.Dequeue(), Encoding.UTF8, "text/event-stream") });
        }
    }

    private const string Overloaded = "Our servers are currently overloaded. Please try again later.";

    private static string SuccessStream()
    {
        var full = JsonSerializer.Serialize(new StrategyOutput { ActionId = "o1.2", Summary = "Joue la carte", Confidence = "moyenne", Rationale = "r" });
        var final = new JsonObject
        {
            ["status"] = "completed",
            ["output"] = new JsonArray(new JsonObject { ["type"] = "message", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = full }) }),
        };
        return Sse("response.output_text.delta", new JsonObject { ["delta"] = full }) + Sse("response.completed", new JsonObject { ["response"] = final });
    }

    private static OpenAiResponsesProvider StreamProvider(HttpMessageHandler handler) =>
        new(new OpenAiOptions { MaxRetries = 1, RetryBaseDelay = TimeSpan.Zero }, "i", new HttpClient(handler), () => "k");

    [Fact]
    public async Task An_overload_announced_in_the_stream_before_any_text_is_retried_once()
    {
        // 26/10/2026, 15 h 49 à 16 h 05 : « Our servers are currently overloaded » arrivait dans le flux, en 1 à 5 s.
        var handler = new SequenceHandler(Sse("error", new JsonObject { ["message"] = Overloaded }), SuccessStream());
        var partials = new List<StrategyPartial>();
        var r = await StreamProvider(handler).ProposeWithPayloadStreamingAsync("{}", partials.Add, CancellationToken.None);
        Assert.Null(r.Error);
        Assert.Equal("o1.2", r.Output!.ActionId);
        Assert.Equal(2, handler.Calls);
        Assert.Equal(1, r.Retries);
    }

    [Fact]
    public async Task A_second_overload_is_reported_in_plain_french()
    {
        var handler = new SequenceHandler(Sse("error", new JsonObject { ["message"] = Overloaded }), Sse("error", new JsonObject { ["message"] = Overloaded }));
        var r = await StreamProvider(handler).ProposeWithPayloadStreamingAsync("{}", null, CancellationToken.None);
        Assert.Equal(2, handler.Calls);
        Assert.StartsWith("flux : OpenAI surchargé, erreur de leur côté", r.Error);
        Assert.Contains(Overloaded, r.Error);
    }

    [Fact]
    public async Task An_error_after_the_advice_started_is_not_retried()
    {
        // Un début de conseil déjà affiché ne doit pas être remplacé par une autre réponse.
        var handler = new SequenceHandler(Sse("response.output_text.delta", new JsonObject { ["delta"] = "{\"actionId\":\"o1.2\",\"summary\":\"Jo" })
                                          + Sse("error", new JsonObject { ["message"] = Overloaded }), SuccessStream());
        var partials = new List<StrategyPartial>();
        var r = await StreamProvider(handler).ProposeWithPayloadStreamingAsync("{}", partials.Add, CancellationToken.None);
        Assert.Equal(1, handler.Calls);
        Assert.NotNull(r.Error);
        Assert.Equal("o1.2", Assert.Single(partials).ActionId);
    }

    [Fact]
    public async Task Exhausted_credit_is_never_retried()
    {
        var handler = new SequenceHandler(Sse("error", new JsonObject { ["message"] = "You have no credits remaining. Add credits to continue using the API." }), SuccessStream());
        var r = await StreamProvider(handler).ProposeWithPayloadStreamingAsync("{}", null, CancellationToken.None);
        Assert.Equal(1, handler.Calls);
        Assert.Contains("no credits remaining", r.Error);
    }

    [Fact]
    public void Partial_extraction_ignores_unfinished_strings()
    {
        Assert.Null(OpenAiResponsesProvider.ExtractPartial("{\"actionId\":\"o1.2"));
        var p = OpenAiResponsesProvider.ExtractPartial("{\"actionId\":\"o1.2\",\"summary\":\"Jou");
        Assert.Equal("o1.2", p!.ActionId);
        Assert.Null(p.Summary);
    }

    [Fact]
    public async Task Ping_reports_model_latency_and_cost()
    {
        var json = "{\"id\":\"r\",\"status\":\"completed\",\"model\":\"gpt-6.1-sol\",\"output\":[],\"usage\":{\"input_tokens\":20,\"output_tokens\":30}}";
        var provider = new OpenAiResponsesProvider(new OpenAiOptions(), "", new HttpClient(new StreamHandler(HttpStatusCode.OK, json)), () => "k");
        var r = await provider.PingAsync(CancellationToken.None);
        Assert.True(r.Ok);
        Assert.Equal("gpt-6.1-sol", r.Model);
        Assert.True(r.CostUsd > 0);
        var missing = await new OpenAiResponsesProvider(new OpenAiOptions(), "", new HttpClient(new StreamHandler(HttpStatusCode.OK, json)), () => null).PingAsync(CancellationToken.None);
        Assert.False(missing.Ok);
    }

    // ------------------------------------------------------------------ Réglages

    [Fact]
    public void Advisor_settings_round_trip_and_map_to_coordinator_options()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hscopilot-config-{Guid.NewGuid():N}.json");
        try
        {
            var config = new CopilotConfig();
            config.Advisor.Model = "gpt-6-luna";
            config.Advisor.Effort = "medium";
            config.Advisor.MaxCallsPerTurn = 2;
            config.Advisor.MaxCostPerGameUsd = 0.4;
            config.Save(path);
            var (loaded, from) = CopilotConfig.Load(path);
            Assert.Equal(path, from);
            Assert.Equal("gpt-6-luna", loaded.Advisor.EffectiveModel);
            Assert.Equal("réglages", loaded.Advisor.ModelSource);
            var options = loaded.Advisor.ToStrategyOptions();
            Assert.Equal(2, options.MaxCallsPerTurn);
            Assert.Equal(0.4, options.MaxCostPerGameUsd);
            Assert.False(options.AllowRanked);
            Assert.True(options.ReservePerCallUsd > 0);
            Assert.Equal("medium", loaded.Advisor.ToOpenAiOptions().ReasoningEffort);
            Assert.DoesNotContain("sk-", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Host_without_key_reports_why_instead_of_calling()
    {
        var settings = new AdvisorSettings();
        await using var host = new AdvisorHost(settings, () => new MetricsRegistry(), _ => new FakeProvider());
        Assert.NotNull(host.Coordinator);
        var disabled = new AdvisorSettings { Enabled = false };
        await using var off = new AdvisorHost(disabled, () => new MetricsRegistry());
        Assert.Null(off.Coordinator);
        Assert.Contains("désactivé", off.DisabledReason);
    }
}
