using System.Diagnostics;
using System.Text.Json;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Non-régression sur la deuxième vraie partie avec le copilote (25/10/2026, anonymisée) : ordre Profondeurs → Wyrm au
/// tour 7, létal démontré au tour 8 affiché sans appel, suite du plan sans appel, mise en garde d'ordre, diagnostics.
/// </summary>
public class RealGameRegressionTests
{
    private const string Fixture = "ai-depths-wyrm-lethal.log";

    private static readonly Lazy<List<GameStateSnapshot>> Decisions = new(() =>
    {
        var game = ReplayFile.SplitGames(TestData.Lines(Fixture)).Last();
        return DatasetBuilder.BuildGame(Fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 12, Decklist = TestData.DragonWarrior })
            .Select(c => c.LoadSnapshot()).ToList();
    });

    private static DecisionContext Ctx(GameStateSnapshot s) => DecisionContextBuilder.Build(s)!;

    private static GameStateSnapshot Turn7Start() => Decisions.Value.First(s => s.Game.TurnNumber == 7);

    private static GameStateSnapshot Turn7AfterDepths() => Decisions.Value.First(s =>
        s.Game.TurnNumber == 7 && Ctx(s).LegalActions.Any(a => a.Kind == "location" && a.TargetName == "Wyrm du pic venteux"));

    private static GameStateSnapshot Turn8Lethal() => Decisions.Value.First(s =>
        s.Game.TurnNumber == 8 && s.Local!.Weapon is not null && s.Opponent!.Hero!.Health == 5);

    private static MinionView Wyrm(GameStateSnapshot s) => s.Local!.Board.First(m => m.Name == "Wyrm du pic venteux");

    private sealed class Provider : IStrategyProvider
    {
        public Func<DecisionContext, StrategyOutput>? Answer { get; init; }
        public int Calls;
        public List<string> Payloads { get; } = new();
        public string Name => "test";

        public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            lock (Payloads) Payloads.Add(PromptBuilder.BuildUserPayload(context));
            var output = Answer?.Invoke(context) ?? new StrategyOutput
            {
                ActionId = context.LegalActions.First(a => a.Kind != "end-turn").Id,
                Summary = "Conseil de test.",
                Confidence = "moyenne",
            };
            return Task.FromResult(new StrategyResult { Output = output, Model = "test", CostUsd = 0.01, LatencyMs = 1 });
        }
    }

    private static (StrategyCoordinator Coordinator, List<AdvisorUpdate> Updates) Start(Provider provider, StrategyOptions? options = null)
    {
        var coordinator = new StrategyCoordinator(provider, options ?? new StrategyOptions { UseStreaming = false }, () => new MetricsRegistry());
        var updates = new List<AdvisorUpdate>();
        coordinator.Updated += u => { lock (updates) updates.Add(u); };
        return (coordinator, updates);
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(15);
    }

    // ------------------------------------------------------------------ Validateur

    [Fact]
    public void Turn7_wyrm_attack_before_depths_gets_an_ordering_caution()
    {
        // Rév. 152 de la partie : le modèle conseillait « Attaquer avec Wyrm → héros (8) » alors que Profondeurs Sanguines
        // était activable sur le Wyrm (+2 ATQ). Le joueur a activé le lieu d'abord et infligé 10.
        var s = Turn7AfterDepths();
        var ctx = Ctx(s);
        var wyrm = Wyrm(s);
        var attack = ctx.LegalActions.First(a => a.Kind == "attack" && a.SourceEntityId == wyrm.EntityId && a.TargetEntityId == s.Opponent!.Hero!.EntityId);
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = attack.Id, Summary = "Wyrm au visage.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.NotNull(rec.Caution);
        Assert.Contains("Avant cette attaque", rec.Caution);
        Assert.Contains("Profondeurs Sanguines", rec.Caution);
        Assert.Contains("10 au lieu de 8", rec.Caution);
        Assert.Equal(LethalLevels.None, rec.Lethal);

        var depths = ctx.LegalActions.First(a => a.Kind == "location" && a.TargetEntityId == wyrm.EntityId);
        var good = RecommendationValidator.Validate(new StrategyOutput { ActionId = depths.Id, Summary = "Profondeurs sur le Wyrm.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.Null(good.Caution);
    }

    [Fact]
    public void Turn7_quick_facts_show_max_damage_and_ordering()
    {
        var ctx = Ctx(Turn7AfterDepths());
        var facts = QuickAnalysis.Analyze(ctx).Select(f => f.Text).ToList();
        Assert.Contains(facts, f => f.StartsWith("Dégâts maximum au visage ce tour : 12 contre 18 PV"));
        Assert.Contains(facts, f => f.StartsWith("Ordre : Profondeurs Sanguines sur Wyrm du pic venteux avant son attaque"));
        Assert.Contains(facts, f => f.StartsWith("Peu probable") && f.Contains("6 ATQ"));
        Assert.DoesNotContain(facts, f => f.StartsWith("Létal"));
    }

    [Fact]
    public void A_model_that_misses_a_proven_lethal_is_flagged_and_the_lethal_is_shown()
    {
        var s = Turn8Lethal();
        var ctx = Ctx(s);
        var other = ctx.LegalActions.First(a => a.Kind == "end-turn");
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = other.Id, Summary = "Fin de tour.", Confidence = "faible" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.Equal(LethalLevels.Certain, rec.Lethal);
        Assert.Contains("Létal démontré par le calcul local", rec.Caution);
        Assert.Contains(rec.Warnings, w => w.Contains("ne suit pas le létal"));
    }

    [Fact]
    public void Legacy_visible_claims_are_read_as_certain_and_capped_by_the_solver()
    {
        Assert.Equal(LethalLevels.Certain, LethalLevels.Normalize("visible"));
        Assert.Equal(LethalLevels.None, LethalLevels.Normalize("n'importe quoi"));
        var ctx = Ctx(Turn7AfterDepths());
        var rec = RecommendationValidator.Validate(new StrategyOutput { ActionId = ctx.LegalActions[0].Id, Summary = "Létal.", Lethal = "visible", Confidence = "élevée" }, ctx, "t", null, 0, null).Recommendation!;
        Assert.Equal(LethalLevels.Plausible, rec.Lethal);
        Assert.Contains(rec.Warnings, w => w.Contains("ramené"));
    }

    // ------------------------------------------------------------------ Coordinateur

    [Fact]
    public async Task A_proven_lethal_is_shown_instantly_without_any_call()
    {
        var provider = new Provider();
        var (coordinator, updates) = Start(provider);
        await using var _c = coordinator;
        var s = Turn8Lethal();
        coordinator.OnSnapshot(s);
        await WaitUntil(() => coordinator.Board.Current is not null, 2000);
        var rec = coordinator.Board.Current!;
        Assert.Equal(0, provider.Calls);
        Assert.Equal(AdviceOrigins.Solver, rec.Origin);
        Assert.Equal(LethalLevels.Certain, rec.Lethal);
        Assert.Equal(0, rec.CostUsd);
        Assert.Contains(Ctx(s).LegalActions, a => a.Id == rec.Action.Id);
        Assert.All(rec.FollowUp, f => Assert.Equal("calculé", f.Status));

        var view = AdviceDisplay.Build(updates.Last(u => u.Phase == AdvisorPhases.Ready), DateTime.UtcNow);
        Assert.StartsWith("Létal certain (démontré)", view.LethalText);
        Assert.Equal(LethalLevels.Certain, view.LethalTone);
        Assert.Contains("calcul local", view.StatusText);
    }

    [Fact]
    public async Task The_next_planned_step_is_shown_without_a_call_when_nothing_new_happened()
    {
        var start = Turn7Start();
        var after = Turn7AfterDepths();
        var wyrm = Wyrm(after);
        var oppHero = after.Opponent!.Hero!.EntityId;
        var provider = new Provider
        {
            Answer = ctx =>
            {
                var depths = ctx.LegalActions.First(a => a.Kind == "play" && a.SourceCardId is "CORE_REV_990" or "REV_990");
                return new StrategyOutput
                {
                    ActionId = depths.Id,
                    Summary = "Pose Profondeurs Sanguines.",
                    Why = "prépare le Wyrm",
                    Confidence = "moyenne",
                    FollowUp = new()
                    {
                        new FollowUpStep { Text = "Active Profondeurs sur le Wyrm.", SourceEntityId = depths.SourceEntityId, TargetEntityId = wyrm.EntityId },
                        new FollowUpStep { Text = "Attaque le héros avec le Wyrm.", SourceEntityId = wyrm.EntityId, TargetEntityId = oppHero },
                    },
                };
            },
        };
        var (coordinator, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(start);
        await WaitUntil(() => coordinator.Board.Current is not null);
        Assert.Equal(1, provider.Calls);
        coordinator.OnSnapshot(after);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision, 2000);
        var next = coordinator.Board.Current!;
        Assert.Equal(1, provider.Calls);                       // aucun nouvel appel
        Assert.Equal(AdviceOrigins.Plan, next.Origin);
        Assert.Equal("location", next.Action.Kind);
        Assert.Equal(wyrm.EntityId, next.Action.TargetEntityId);
        Assert.Single(next.FollowUp);
    }

    [Fact]
    public void Plan_continuation_requires_that_nothing_new_appeared()
    {
        var start = Turn7Start();
        var after = Turn7AfterDepths();
        Assert.True(StrategyCoordinator.NothingNew(start, after));
        // La pioche du tour suivant est une information nouvelle : nouvel appel.
        var next = Decisions.Value.First(s => s.Game.TurnNumber == 8);
        Assert.False(StrategyCoordinator.NothingNew(after, next));
    }

    [Fact]
    public async Task Mid_turn_calls_carry_the_local_analysis_and_the_previous_advice()
    {
        var provider = new Provider();
        var (coordinator, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(Turn7Start());
        await WaitUntil(() => coordinator.Board.Current is not null);
        coordinator.OnSnapshot(Turn7AfterDepths());
        await WaitUntil(() => provider.Calls == 2);
        string payload;
        lock (provider.Payloads) payload = provider.Payloads[1];
        using var doc = JsonDocument.Parse(payload);
        var local = doc.RootElement.GetProperty("localAnalysis");
        Assert.Equal(12, local.GetProperty("maxFaceDamageThisTurn").GetInt32());
        Assert.Equal("aucun", local.GetProperty("lethal").GetString());
        Assert.Contains(local.GetProperty("ordering").EnumerateArray(), e => e.GetString()!.Contains("Wyrm"));
        var previous = doc.RootElement.GetProperty("previousAdvice");
        Assert.Equal(JsonValueKind.Object, previous.ValueKind);
        Assert.False(string.IsNullOrEmpty(previous.GetProperty("action").GetString()));
        using var first = JsonDocument.Parse(provider.Payloads[0]);   // début de tour : pas de conseil précédent
        Assert.False(first.RootElement.TryGetProperty("previousAdvice", out var p0) && p0.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task Diagnostics_show_the_analysed_revision_and_the_drawn_card()
    {
        var provider = new Provider();
        var (coordinator, updates) = Start(provider);
        await using var _c = coordinator;
        var s = Turn7Start();
        coordinator.OnSnapshot(s);
        await WaitUntil(() => coordinator.Board.Current is not null);
        var ready = updates.Last(u => u.Phase == AdvisorPhases.Ready);
        var d = ready.Diagnostics!;
        Assert.Equal(s.Revision, d.AnalyzedRevision);
        Assert.Equal(s.LastLogTime, d.StateLogTime);
        var drawn = s.Local!.Hand.Cards.Where(c => c.EnteredTurn == s.Game.GameTurn).Select(c => c.Name ?? "?").ToList();
        Assert.NotEmpty(drawn);
        Assert.Equal(drawn, d.DrawnThisTurn);
        Assert.NotNull(d.SentUtc);
        Assert.True(d.AgeMs >= 0);
        var entry = AdvisorTrace.ToEntry(ready);
        Assert.Equal(s.Revision, entry.AnalyzedRevision);
        Assert.Equal(AdviceOrigins.Model, entry.Origin);
        Assert.Equal(drawn, entry.DrawnThisTurn);
    }

    // ------------------------------------------------------------------ Format de réponse

    [Fact]
    public void Follow_up_steps_parse_both_the_new_and_the_legacy_format()
    {
        var modern = JsonSerializer.Deserialize<StrategyOutput>("""{"actionId":"o1.2","summary":"x","followUp":[{"text":"Active le lieu","sourceEntityId":40,"targetEntityId":12},{"text":"Fin","sourceEntityId":null,"targetEntityId":null}]}""")!;
        Assert.Equal(40, modern.FollowUp[0].SourceEntityId);
        Assert.Equal(12, modern.FollowUp[0].TargetEntityId);
        Assert.Null(modern.FollowUp[1].SourceEntityId);
        var legacy = JsonSerializer.Deserialize<StrategyOutput>("""{"actionId":"o1.2","summary":"x","followUp":["Attaque le héros"]}""")!;
        Assert.Equal("Attaque le héros", legacy.FollowUp[0].Text);
        var schema = HsCopilot.Core.Strategy.Providers.OpenAiResponsesProvider.OutputSchema().ToJsonString();
        Assert.Contains("\"certain\"", schema);
        Assert.DoesNotContain("\"visible\"", schema);
        Assert.Contains("sourceEntityId", schema);
    }

    [Fact]
    public void The_solver_is_fast_enough_for_every_decision_of_the_game()
    {
        var worst = 0.0;
        foreach (var s in Decisions.Value)
        {
            var ctx = DecisionContextBuilder.Build(s);
            if (ctx is null) continue;
            worst = Math.Max(worst, TurnSolver.Solve(ctx).ElapsedMs);
        }
        Assert.True(worst < 250, $"pire cas {worst:0} ms");
    }
}
