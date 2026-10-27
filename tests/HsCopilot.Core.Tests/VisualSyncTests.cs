using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Synchronisation des conseils avec l'écran, sur la partie réelle du 25/10/2026 à 18 h 34 (contre l'IA), anonymisée avec
/// ses deux flux : GameState (serveur) et PowerTaskList (exécution par le client, au rythme des animations).
/// Au tour 4, le serveur commence ton tour à 18:36:56.62 ; le client ne l'affiche qu'à 18:37:06.71.
/// </summary>
public class VisualSyncTests
{
    private const string Fixture = "ai-visual-lag-two-streams.log";

    private static TimeSpan T(string s) => TimeSpan.Parse(s, CultureInfo.InvariantCulture);

    private sealed class Provider : IStrategyProvider
    {
        public int Calls;
        public DecisionContext? LastContext;
        public string Name => "test";
        public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            LastContext = context;
            var action = context.LegalActions.FirstOrDefault(a => a.Kind is not ("end-turn" or "mulligan-keep")) ?? context.LegalActions[0];
            return Task.FromResult(new StrategyResult
            {
                Output = new StrategyOutput { ActionId = action.Id, Summary = "Conseil de test.", Confidence = "moyenne" },
                Model = "test", CostUsd = 0.01, LatencyMs = 1,
            });
        }
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(10);
    }

    /// <summary>Rejoue la partie dans l'ordre du fichier : GameState vers le moteur et le conseiller, PowerTaskList vers l'horloge.</summary>
    private sealed class Replay
    {
        public readonly Provider Provider = new();
        public readonly StrategyCoordinator Coordinator;
        public readonly List<AdvisorUpdate> Updates = new();
        public readonly VisualClock Clock = new();
        public readonly MetricsRegistry Metrics = new();
        private readonly GameEngine _engine = new(new EngineOptions { Cards = TestData.Cards });
        private readonly GameStateProjector _projector = new(TestData.Cards);
        private readonly IEnumerator<string> _lines;
        public GameStateSnapshot? Last;
        public TimeSpan Time;

        public Replay(bool withClock = true, string fixture = Fixture)
        {
            Coordinator = new StrategyCoordinator(Provider, new StrategyOptions { UseStreaming = false }, () => Metrics);
            Coordinator.Updated += u => { lock (Updates) Updates.Add(u); };
            if (withClock) Clock.Changed += p => Coordinator.OnVisualProgress(p.Turn, p.Step, p.LogTime);
            _lines = TestData.Lines(fixture).GetEnumerator();
        }

        public AdvisorUpdate Current => Coordinator.Current;

        /// <summary>
        /// Avance jusqu'à ce que la condition soit vraie. Comme la session en direct, un état est publié à chaque révision et dès
        /// la fin d'un bloc d'options ou de choix, même si la ligne suivante est une ligne PowerTaskList.
        /// </summary>
        public async Task RunUntil(Func<Replay, bool> stop)
        {
            while (_lines.MoveNext())
            {
                var raw = _lines.Current;
                var p = PowerLogLineParser.TryParse(raw);
                var inBlock = p?.Method is "DebugPrintOptions" or "DebugPrintEntityChoices";
                if (_pending && !inBlock && await Publish(stop)) return;
                if (p is null)
                {
                    if (Clock.OnLine(raw)) { Time = ParseTime(raw); if (stop(this)) return; }
                    continue;
                }
                var before = _engine.Revision;
                _engine.Process(p);
                Time = p.Time;
                if (inBlock) { _pending = true; continue; }
                if (_engine.Revision != before) _pending = true;
                if (_pending && await Publish(stop)) return;
                if (stop(this)) return;
            }
        }

        private bool _pending;

        private async Task<bool> Publish(Func<Replay, bool> stop)
        {
            _pending = false;
            Last = _projector.Project(_engine, new ProjectionContext { Transport = "test", Decklist = TestData.DragonWarrior });
            Last.LiveTimestamps = true;
            Coordinator.OnSnapshot(Last);
            await WaitUntil(() => Coordinator.Current.Phase != AdvisorPhases.Analyzing, 2000);
            return stop(this);
        }

        private static TimeSpan ParseTime(string raw) => TimeSpan.TryParse(raw.AsSpan(2, 16), CultureInfo.InvariantCulture, out var t) ? t : default;
    }

    // ------------------------------------------------------------------ Les deux flux

    [Fact]
    public void PowerTaskList_lines_never_reach_the_engine()
    {
        var all = TestData.Lines(Fixture);
        GameStateSnapshot Final(IEnumerable<string> lines)
        {
            var engine = new GameEngine(new EngineOptions { Cards = TestData.Cards });
            foreach (var l in lines) if (PowerLogLineParser.TryParse(l) is { } p) engine.Process(p);
            return new GameStateProjector(TestData.Cards).Project(engine, new ProjectionContext { Transport = "test" });
        }
        var both = Final(all);
        var gameStateOnly = Final(all.Where(l => l.Contains(" GameState.", StringComparison.Ordinal)));
        Assert.Equal(gameStateOnly.Revision, both.Revision);
        Assert.Equal(JsonSerializer.Serialize(gameStateOnly.History, Json.Compact), JsonSerializer.Serialize(both.History, Json.Compact));
        Assert.True(all.Count(VisualClock.IsPowerTaskList) > 5000);
    }

    [Fact]
    public void The_client_shows_turn_4_ten_seconds_after_the_server_started_it()
    {
        // GameState : TURN=8 (ton tour 4) et MAIN_ACTION à 18:36:56.62 ; PowerTaskList : MAIN_ACTION à 18:37:06.71.
        TimeSpan? server = null, screen = null;
        var clock = new VisualClock();
        clock.Changed += p => { if (p.Turn == 8 && p.Step == "MAIN_ACTION") screen ??= p.LogTime; };
        int? turn = null;
        foreach (var raw in TestData.Lines(Fixture))
        {
            if (clock.OnLine(raw)) continue;
            if (PowerLogLineParser.TryParse(raw) is not { } p) continue;
            if (p.Body.Contains("tag=TURN value=8", StringComparison.Ordinal) && p.Body.Contains("Entity=GameEntity", StringComparison.Ordinal)) turn = 8;
            if (turn == 8 && p.Body.Trim() == "TAG_CHANGE Entity=GameEntity tag=STEP value=MAIN_ACTION") server ??= p.Time;
        }
        Assert.Equal(T("18:36:56.6201603"), server);
        Assert.Equal(T("18:37:06.7102516"), screen);
        Assert.InRange((screen!.Value - server!.Value).TotalSeconds, 10.0, 10.2);
    }

    // ------------------------------------------------------------------ Conseils retenus jusqu'à l'affichage

    [Fact]
    public async Task Turn_4_advice_is_computed_on_the_real_turn_but_not_shown_as_immediate_before_the_screen()
    {
        var r = new Replay();
        await using var _c = r.Coordinator;
        // Décision du tour 4 : options du serveur reçues, Hearthstone rejoue encore le tour adverse.
        await r.RunUntil(x => x.Last?.Game.GameTurn == 8 && x.Coordinator.Board.Current is not null);
        var held = r.Current;
        Assert.Equal(AdvisorPhases.Ready, held.Phase);
        Assert.True(held.AwaitingDisplay);
        Assert.True(held.DisplayTracked);
        Assert.Contains(r.Last!.Local!.Hand.Cards, c => c.Name == "Profondeurs Sanguines" && c.EnteredTurn == 8);   // pioche du tour comprise
        // Le contexte réellement envoyé au modèle contient cette pioche et les options fraîches du tour 4.
        var sent = r.Provider.LastContext!;
        Assert.Equal(8, sent.GameTurn);
        Assert.Contains(sent.State.Local!.Hand.Cards, c => c.Name == "Profondeurs Sanguines" && c.EnteredTurn == 8);
        Assert.Contains(sent.LegalActions, a => a.Label.StartsWith("Jouer Profondeurs Sanguines"));
        Assert.True(r.Time < T("18:37:06"));
        var view = AdviceDisplay.Build(held, DateTime.UtcNow);
        Assert.Equal("TON TOUR ARRIVE", view.ActionTitle);
        Assert.DoesNotContain("à cet instant", view.VerifiedText);
        Assert.Contains("pioche comprise", view.VerifiedText);

        // Le client exécute ton tour 4 : la même recommandation devient « ACTION IMMÉDIATE », sans nouvel appel.
        var calls = r.Provider.Calls;
        await r.RunUntil(x => !x.Current.AwaitingDisplay);
        Assert.Equal(T("18:37:06.7102516"), r.Time);
        var shown = r.Current;
        Assert.Equal(held.Recommendation!.Action.Id, shown.Recommendation!.Action.Id);
        Assert.Equal("ACTION IMMÉDIATE", AdviceDisplay.Build(shown, DateTime.UtcNow).ActionTitle);
        Assert.Equal(calls, r.Provider.Calls);
        Assert.Contains("Ton tour est affiché", shown.Message);
        var lag = r.Metrics.Stats(MetricNames.TurnServerToScreen);
        Assert.True(lag.Count >= 1);
        Assert.InRange(lag.Max, 9_000, 15_000);
    }

    [Fact]
    public async Task When_you_play_first_your_turn_1_starts_after_the_mulligan_not_during_it()
    {
        // Partie classée du 25/10/2026 à 19 h 26, premier joueur : le serveur te donne la main au tour 1 dès le mulligan.
        // La mesure « début de tour serveur → écran » partait du mulligan (39,5 s au lieu de 12,1 s).
        var r = new Replay(withClock: false, fixture: "ai-darkgift-top-of-deck.log");
        await using var _c = r.Coordinator;
        await r.RunUntil(x => x.Last is { Game: { MulliganDone: false, ActivePlayer: "local", GameTurn: 1 } });
        Assert.NotNull(r.Last);
        Assert.False(r.Last!.Game.MulliganDone);
        await r.RunUntil(x => x.Last is { Game: { MulliganDone: true, ActivePlayer: "local", GameTurn: 1 } });
        Assert.True(r.Last!.Game.MulliganDone);
        var turnStart = r.Last.LastLogTimeOfDay;
        r.Coordinator.OnVisualProgress(1, "MAIN_ACTION", turnStart + TimeSpan.FromSeconds(2));
        var lag = r.Metrics.Stats(MetricNames.TurnServerToScreen);
        Assert.Equal(1, lag.Count);
        Assert.InRange(lag.Max, 1_999, 2_001);
    }

    [Fact]
    public async Task A_local_lethal_is_also_held_until_the_turn_is_on_screen()
    {
        // Tour 7 réel : « Létal démontré » publié à 18:39:24.0, tour affiché à 18:39:38.3.
        var r = new Replay();
        await using var _c = r.Coordinator;
        await r.RunUntil(x => x.Coordinator.Board.Current is { Origin: AdviceOrigins.Solver });
        Assert.True(r.Current.AwaitingDisplay);
        Assert.Equal("TON TOUR ARRIVE", AdviceDisplay.Build(r.Current, DateTime.UtcNow).ActionTitle);
        await r.RunUntil(x => !x.Current.AwaitingDisplay);
        Assert.Equal(AdviceOrigins.Solver, r.Current.Recommendation?.Origin);
        Assert.Equal("ACTION IMMÉDIATE", AdviceDisplay.Build(r.Current, DateTime.UtcNow).ActionTitle);
    }

    [Fact]
    public async Task The_mulligan_is_never_held()
    {
        var r = new Replay();
        await using var _c = r.Coordinator;
        await r.RunUntil(x => x.Coordinator.Board.Current is { Kind: DecisionContext.KindMulligan });
        Assert.False(r.Current.AwaitingDisplay);
        Assert.Equal("MULLIGAN", AdviceDisplay.Build(r.Current, DateTime.UtcNow).ActionTitle);
    }

    [Fact]
    public async Task Without_the_visual_clock_nothing_is_held_and_the_same_calls_are_made()
    {
        var withClock = new Replay();
        var without = new Replay(withClock: false);
        await using var _a = withClock.Coordinator;
        await using var _b = without.Coordinator;
        await withClock.RunUntil(_ => false);
        await without.RunUntil(_ => false);
        lock (without.Updates) Assert.All(without.Updates, u => { Assert.False(u.AwaitingDisplay); Assert.False(u.DisplayTracked); });
        Assert.Equal(without.Provider.Calls, withClock.Provider.Calls);   // l'horloge ne change ni le moment ni le nombre des appels
        lock (withClock.Updates) Assert.Contains(withClock.Updates, u => u.AwaitingDisplay);
    }

    [Fact]
    public async Task Advice_disappears_as_soon_as_the_server_ends_the_turn()
    {
        var r = new Replay();
        await using var _c = r.Coordinator;
        await r.RunUntil(x => x.Last?.Game.GameTurn == 8 && x.Coordinator.Board.Current is not null);
        await r.RunUntil(x => x.Last?.Game.ActivePlayer == "opponent");
        Assert.Null(r.Coordinator.Board.Current);
        Assert.NotEqual(AdvisorPhases.Ready, r.Current.Phase);
    }

    [Fact]
    public void A_frozen_clock_releases_the_advice_with_an_explicit_warning()
    {
        var rec = new Recommendation
        {
            GameId = "g", Revision = 1, OptionsId = 1, GameTurn = 8, ResetCountAtCreation = 0, HistorySeqAtCreation = 0,
            Action = new LegalAction { Id = "o1.1", OptionsId = 1, OptionIndex = 1, Kind = "play", Label = "Jouer Glissedrake visionnaire" },
            Uncertainty = UncertaintyLevel.Moyenne, Summary = "Joue Glissedrake.", Provider = "test",
        };
        var held = new AdvisorUpdate { Phase = AdvisorPhases.Ready, Recommendation = rec, AwaitingDisplay = true, DisplayTracked = true, AwaitingSinceUtc = DateTime.UtcNow };
        Assert.Equal("TON TOUR ARRIVE", AdviceDisplay.Build(held, DateTime.UtcNow).ActionTitle);
        var frozen = held with { AwaitingSinceUtc = DateTime.UtcNow - TimeSpan.FromSeconds(60) };
        var view = AdviceDisplay.Build(frozen, DateTime.UtcNow);
        Assert.Equal("ACTION IMMÉDIATE", view.ActionTitle);
        Assert.Contains("affichage de Hearthstone non confirmé", view.StatusText);
    }

    // ------------------------------------------------------------------ La pièce au mulligan

    [Fact]
    public async Task The_coin_is_never_offered_for_replacement_and_the_mulligan_result_is_right()
    {
        var r = new Replay();
        await using var _c = r.Coordinator;
        await r.RunUntil(x => x.Last?.PendingChoice is { ChoiceType: "MULLIGAN" } pc && pc.Offered.Count == 5);
        var ctx = DecisionContextBuilder.BuildChoice(r.Last!)!;
        Assert.DoesNotContain(ctx.LegalActions, a => a.Label == "Remplacer La pièce");
        Assert.Equal(4, ctx.LegalActions.Count(a => a.Kind == "mulligan-replace"));
        var replace = ctx.LegalActions.Where(a => a.SourceName is "Dragonnet du Vol infini" or "Suffusion d’ombreflamme").ToList();
        var advice = RecommendationValidator.Validate(new StrategyOutput
        {
            ActionId = replace[0].Id, Summary = "Remplace Dragonnet et Suffusion.", Confidence = "moyenne", ReplaceIds = replace.Select(a => a.Id).ToList(),
        }, ctx, "test", null, 0, null).Recommendation!;

        await r.RunUntil(x => x.Last is not null && MulliganResult.From(x.Last, null) is not null);
        var entry = MulliganResult.From(r.Last!, advice)!;
        Assert.Equal(new[] { "Colporteur de pétales", "Gardien des couvées", "La pièce" }, entry.MulliganKept!.Order());
        Assert.Equal(new[] { "Dragonnet du Vol infini", "Suffusion d’ombreflamme" }, entry.MulliganReplaced!.Order());
        Assert.Equal(new[] { "Frappe anticipée", "Glissedrake visionnaire" }, entry.MulliganReceived!.Order());   // avant la pioche du tour
        Assert.True(entry.MulliganFollowed);
    }
}
