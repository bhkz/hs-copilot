using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Continuité du plan quand tu joues l'action du début de réponse avant la fin de l'appel (parties classées du 25/10/2026 :
/// « Jouer La pièce » joué 3,5 s après le début de réponse, appel annulé, nouvel appel pour la carte suivante).
/// Changement attendu par le plan : l'appel continue et sa suite est reprise sans nouvel appel si elle est légale.
/// Changement imprévu : annulation et nouvel appel, comme avant.
/// </summary>
public class PlanContinuityTests
{
    private const string CoinGame = "ai-visual-lag-two-streams.log";
    private const string DepthsGame = "ai-depths-wyrm-lethal.log";
    private static readonly Dictionary<string, List<GameStateSnapshot>> Cache = new();

    private static List<GameStateSnapshot> Decisions(string fixture)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(fixture, out var list)) return list;
            var game = ReplayFile.SplitGames(TestData.Lines(fixture)).Last();
            list = DatasetBuilder.BuildGame(fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 12, Decklist = TestData.DragonWarrior })
                .Select(c => c.LoadSnapshot()).ToList();
            Cache[fixture] = list;
            return list;
        }
    }

    private static DecisionContext Ctx(GameStateSnapshot s) => DecisionContextBuilder.Build(s)!;

    private static LegalAction Coin(GameStateSnapshot s) => Ctx(s).LegalActions.First(a => a.Kind == "play" && CardRules.IsCoin(a.SourceCardId));

    /// <summary>Partie de 18 h 34 : premier tour, La pièce jouable ; puis le même tour après La pièce, Gardien des couvées jouable.</summary>
    private static (GameStateSnapshot Before, GameStateSnapshot After, int Keeper) CoinThenKeeper()
    {
        var all = Decisions(CoinGame);
        var before = all.First(s => Ctx(s).LegalActions.Any(a => a.Kind == "play" && CardRules.IsCoin(a.SourceCardId)));
        var coin = Coin(before);
        var after = all.First(s => s.Revision > before.Revision && s.Game.GameTurn == before.Game.GameTurn
            && s.History.Any(h => h.Kind == "play" && h.SourceEntityId == coin.SourceEntityId));
        var keeper = Ctx(after).LegalActions.First(a => a.Kind == "play" && a.SourceName == "Gardien des couvées").SourceEntityId!.Value;
        return (before, after, keeper);
    }

    /// <summary>Partie du 25/10 à 16 h 37, tour 7 : Profondeurs Sanguines posées, puis activables sur le Wyrm.</summary>
    private static (GameStateSnapshot Before, GameStateSnapshot After) DepthsThenWyrm()
    {
        var all = Decisions(DepthsGame);
        var before = all.First(s => s.Game.TurnNumber == 7);
        var after = all.First(s => s.Game.TurnNumber == 7 && Ctx(s).LegalActions.Any(a => a.Kind == "location" && a.TargetName == "Wyrm du pic venteux"));
        return (before, after);
    }

    /// <summary>État intermédiaire : l'action est jouée, les nouvelles options ne sont pas encore arrivées.</summary>
    private static GameStateSnapshot WithoutOptions(GameStateSnapshot s) => Mutate(s, n =>
    {
        n["revision"] = s.Revision - 1;
        n["legalOptions"]!["fresh"] = false;
    });

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

    /// <summary>Fournisseur en flux : l'action arrive tout de suite (début de réponse), la réponse complète à l'ouverture de la porte.</summary>
    private sealed class StreamingProvider : IStrategyProvider, IStreamingStrategyProvider
    {
        public required Func<DecisionContext, StrategyOutput> Answer { get; init; }
        public readonly List<TaskCompletionSource> Gates = new();
        public int Calls;
        public string Name => "test";

        public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken) =>
            ProposeStreamingAsync(context, _ => { }, cancellationToken);

        public async Task<StrategyResult> ProposeStreamingAsync(DecisionContext context, Action<StrategyPartial> onPartial, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (Gates) Gates.Add(gate);
            var output = Answer(context);
            onPartial(new StrategyPartial { ActionId = output.ActionId, ElapsedMs = 4000 });
            await gate.Task.WaitAsync(cancellationToken);
            return new StrategyResult { Output = output, Model = "test", CostUsd = 0.01, LatencyMs = 8000 };
        }

        public void Release(int call) { lock (Gates) Gates[call].TrySetResult(); }
    }

    private static (StrategyCoordinator Coordinator, List<AdvisorUpdate> Updates, MetricsRegistry Metrics) Start(StreamingProvider provider, bool keepCall = true)
    {
        var metrics = new MetricsRegistry();
        var coordinator = new StrategyCoordinator(provider, new StrategyOptions { KeepCallWhenPartialPlayed = keepCall }, () => metrics);
        var updates = new List<AdvisorUpdate>();
        coordinator.Updated += u => { lock (updates) updates.Add(u); };
        return (coordinator, updates, metrics);
    }

    private static StreamingProvider CoinProvider(int keeper) => new()
    {
        // Comme la réponse complète de 18 h 35 : La pièce, puis Gardien des couvées.
        Answer = ctx => ctx.LegalActions.FirstOrDefault(a => a.Kind == "play" && CardRules.IsCoin(a.SourceCardId)) is { } coin
            ? new StrategyOutput
            {
                ActionId = coin.Id, Summary = "Joue La pièce.", Why = "4 cristaux pour Gardien des couvées", Confidence = "moyenne",
                FollowUp = new() { new FollowUpStep { Text = "Jouer Gardien des couvées.", SourceEntityId = keeper } },
            }
            : new StrategyOutput { ActionId = ctx.LegalActions.First(a => a.Kind != "end-turn").Id, Summary = "Nouvel appel.", Confidence = "moyenne" },
    };

    // ------------------------------------------------------------------ Changement attendu par le plan

    [Fact]
    public async Task Playing_the_coin_from_the_partial_answer_keeps_the_call_and_shows_the_next_card_without_a_new_call()
    {
        var (before, after, keeper) = CoinThenKeeper();
        Assert.True(StrategyCoordinator.NothingNew(before, after));
        var provider = CoinProvider(keeper);
        var (coordinator, updates, metrics) = Start(provider);
        await using var _c = coordinator;

        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        Assert.True(CardRules.IsCoin(coordinator.Current.PartialAction!.SourceCardId));

        // Tu joues La pièce sur le début de réponse : l'appel n'est pas annulé…
        coordinator.OnSnapshot(WithoutOptions(after));
        coordinator.OnSnapshot(after);
        Assert.Equal(1, metrics.Counter("stratégie : appels conservés (début de réponse joué)"));
        Assert.Equal(0, metrics.Counter("stratégie : appels annulés (décision passée)"));
        Assert.Equal(AdvisorPhases.Analyzing, coordinator.Current.Phase);
        Assert.Contains("suite du plan attendue", coordinator.Current.Message);

        // … et sa réponse complète donne la carte suivante, vérifiée sur les nouvelles options, sans nouvel appel.
        provider.Release(0);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision);
        var next = coordinator.Board.Current!;
        Assert.Equal(1, provider.Calls);
        Assert.Equal(AdviceOrigins.Plan, next.Origin);
        Assert.Equal(keeper, next.Action.SourceEntityId);
        Assert.Contains(Ctx(after).LegalActions, a => a.Id == next.Action.Id);
        Assert.Equal(1, metrics.Counter("stratégie : suites de plan affichées sans appel"));
        // La réponse dont l'action est déjà jouée n'est jamais affichée comme conseil.
        lock (updates) Assert.DoesNotContain(updates, u => u.Phase == AdvisorPhases.Ready && u.Recommendation is { } r && CardRules.IsCoin(r.Action.SourceCardId));
    }

    [Fact]
    public async Task The_same_holds_when_the_answer_arrives_before_the_new_options()
    {
        var (before, after, keeper) = CoinThenKeeper();
        var provider = CoinProvider(keeper);
        var (coordinator, _, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        provider.Release(0);
        await Task.Delay(150);
        coordinator.OnSnapshot(after);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(AdviceOrigins.Plan, coordinator.Board.Current!.Origin);
        Assert.Equal(keeper, coordinator.Board.Current.Action.SourceEntityId);
    }

    [Fact]
    public async Task A_preparatory_card_played_from_the_partial_answer_also_keeps_its_plan()
    {
        // Profondeurs Sanguines puis activation sur le Wyrm (tour 7 de la partie de 16 h 37).
        var (before, after) = DepthsThenWyrm();
        var wyrm = after.Local!.Board.First(m => m.Name == "Wyrm du pic venteux").EntityId;
        var provider = new StreamingProvider
        {
            Answer = ctx => ctx.LegalActions.FirstOrDefault(a => a.Kind == "play" && a.SourceCardId is "CORE_REV_990" or "REV_990") is { } depths
                ? new StrategyOutput
                {
                    ActionId = depths.Id, Summary = "Pose Profondeurs Sanguines.", Confidence = "moyenne",
                    FollowUp = new() { new FollowUpStep { Text = "Active Profondeurs sur le Wyrm.", SourceEntityId = depths.SourceEntityId, TargetEntityId = wyrm } },
                }
                : new StrategyOutput { ActionId = ctx.LegalActions.First(a => a.Kind != "end-turn").Id, Summary = "Nouvel appel.", Confidence = "moyenne" },
        };
        var (coordinator, _, _) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        coordinator.OnSnapshot(after);
        provider.Release(0);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision);
        var next = coordinator.Board.Current!;
        Assert.Equal(1, provider.Calls);
        Assert.Equal(AdviceOrigins.Plan, next.Origin);
        Assert.Equal("location", next.Action.Kind);
        Assert.Equal(wyrm, next.Action.TargetEntityId);
    }

    // ------------------------------------------------------------------ Changement imprévu : nouvelle décision

    [Fact]
    public async Task Another_action_than_the_partial_one_cancels_the_call_and_asks_again()
    {
        var (before, after, keeper) = CoinThenKeeper();
        var provider = new StreamingProvider
        {
            // Le début de réponse propose autre chose que La pièce ; tu joues quand même La pièce.
            // Au premier tour, la seule autre action est de finir le tour.
            Answer = ctx => new StrategyOutput
            {
                ActionId = (ctx.LegalActions.FirstOrDefault(a => a.Kind != "end-turn" && !CardRules.IsCoin(a.SourceCardId)) ?? ctx.LegalActions.First(a => a.Kind == "end-turn")).Id,
                Summary = "Autre coup.", Confidence = "moyenne",
            },
        };
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        Assert.Equal(1, metrics.Counter("stratégie : appels annulés (décision passée)"));
        coordinator.OnSnapshot(after);
        await WaitUntil(() => provider.Calls == 2);
        provider.Release(1);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(AdviceOrigins.Model, coordinator.Board.Current!.Origin);
    }

    [Fact]
    public async Task A_new_card_in_hand_after_the_partial_action_is_unexpected_and_triggers_a_new_call()
    {
        var (before, after, keeper) = CoinThenKeeper();
        // Carte apparue en main (pioche, carte générée) : information que le plan ne connaissait pas.
        var drawn = Mutate(after, n =>
        {
            var cards = n["local"]!["hand"]!["cards"]!.AsArray();
            var copy = JsonNode.Parse(cards[0]!.ToJsonString())!.AsObject();
            copy["entityId"] = 9999;
            cards.Add(copy);
        });
        Assert.False(StrategyCoordinator.NothingNew(before, drawn));
        var provider = CoinProvider(keeper);
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(drawn));
        coordinator.OnSnapshot(drawn);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(1, metrics.Counter("stratégie : appels annulés (décision passée)"));
        Assert.Equal(0, metrics.Counter("stratégie : appels conservés (début de réponse joué)"));
    }

    [Fact]
    public async Task A_secret_triggered_by_the_partial_action_is_unexpected_and_triggers_a_new_call()
    {
        var (before, after, keeper) = CoinThenKeeper();
        // L'adversaire avait un Secret ; La pièce l'a déclenché (il a quitté le jeu) : le plan ne l'avait pas prévu.
        var secret = new JsonObject { ["entityId"] = 9998, ["cardClass"] = "MAGE", ["known"] = false, ["enteredTurn"] = 1 };
        var withSecret = Mutate(before, n => n["opponent"]!["secrets"]!.AsArray().Add(secret));
        Assert.False(StrategyCoordinator.NothingNew(withSecret, after));
        var provider = CoinProvider(keeper);
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(withSecret);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        coordinator.OnSnapshot(after);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(0, metrics.Counter("stratégie : appels conservés (début de réponse joué)"));
    }

    [Fact]
    public async Task A_kept_call_without_a_usable_follow_up_leads_to_a_normal_call_for_the_new_decision()
    {
        var (before, after, _) = CoinThenKeeper();
        var provider = new StreamingProvider
        {
            // La pièce sans suite : rien à reprendre, la décision suivante est analysée normalement.
            Answer = ctx => new StrategyOutput
            {
                ActionId = (ctx.LegalActions.FirstOrDefault(a => a.Kind == "play" && CardRules.IsCoin(a.SourceCardId)) ?? ctx.LegalActions.First(a => a.Kind != "end-turn")).Id,
                Summary = "Conseil.", Confidence = "moyenne",
            },
        };
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        coordinator.OnSnapshot(after);
        provider.Release(0);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, metrics.Counter("stratégie : réponses sans suite après le coup joué"));
        provider.Release(1);
        await WaitUntil(() => coordinator.Board.Current?.Revision == after.Revision);
        Assert.Equal(AdviceOrigins.Model, coordinator.Board.Current!.Origin);
    }

    [Fact]
    public async Task Previous_behaviour_cancels_the_call_when_the_partial_action_is_played()
    {
        var (before, after, keeper) = CoinThenKeeper();
        var provider = CoinProvider(keeper);
        var (coordinator, _, metrics) = Start(provider, keepCall: false);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(after));
        coordinator.OnSnapshot(after);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, metrics.Counter("stratégie : appels annulés (décision passée)"));
    }

    // ------------------------------------------------------------------ Effets non prévus : légal ne veut pas dire pertinent

    private static GameStateSnapshot Change(GameStateSnapshot s, Action<JsonObject> change) => Mutate(s, change);

    private static JsonObject Minion(JsonObject n, string side, int index) => n[side]!["board"]!.AsArray()[index]!.AsObject();

    private static int Int(JsonObject o, string key) => o[key]?.GetValue<int>() ?? 0;

    private static LegalAction DepthsPlay(GameStateSnapshot s) => Ctx(s).LegalActions.First(a => a.Kind == "play" && a.SourceCardId is "CORE_REV_990" or "REV_990");

    [Fact]
    public void Effects_on_other_entities_are_detected_without_simulating_the_card()
    {
        var (before, after) = DepthsThenWyrm();
        var depths = DepthsPlay(before);
        Assert.Null(StrategyCoordinator.UnplannedEffect(before, after, depths));   // la vraie partie : rien d'imprévu
        Assert.NotEmpty(after.Local!.Board);

        // Un serviteur blessé par un effet que le plan ignorait (déclencheur adverse, effet aléatoire).
        var hurt = Change(after, n => { var m = Minion(n, "local", 0); m["damage"] = Int(m, "damage") + 2; m["health"] = Int(m, "health") - 2; });
        Assert.Contains("dégâts", StrategyCoordinator.UnplannedEffect(before, hurt, depths));
        // Un statut perdu ou gagné (Gel, Bouclier divin…).
        var frozen = Change(after, n => Minion(n, "local", 0)["keywords"]!.AsArray().Add("FROZEN"));
        Assert.Contains("statut", StrategyCoordinator.UnplannedEffect(before, frozen, depths));
        // Le héros adverse gagne de l'armure : un létal prévu peut ne plus passer.
        var armored = Change(after, n => n["opponent"]!["hero"]!["armor"] = Int(n["opponent"]!["hero"]!.AsObject(), "armor") + 2);
        Assert.Contains("héros adverse", StrategyCoordinator.UnplannedEffect(before, armored, depths));
        // Ton héros blessé : imprévu. Ton héros cuirassé (Wyrm du pic venteux : « vous gagnez 5 points d'armure ») : accepté.
        var hit = Change(after, n => n["local"]!["hero"]!["health"] = Int(n["local"]!["hero"]!.AsObject(), "health") - 2);
        Assert.Contains("ton héros", StrategyCoordinator.UnplannedEffect(before, hit, depths));
        var gained = Change(after, n => n["local"]!["hero"]!["armor"] = Int(n["local"]!["hero"]!.AsObject(), "armor") + 5);
        Assert.Null(StrategyCoordinator.UnplannedEffect(before, gained, depths));
    }

    [Fact]
    public void An_aura_change_is_accepted_only_when_a_minion_of_the_action_arrives_or_leaves()
    {
        // Partie classée 3, tour 4 : Gorgonzormu tue l'Oracle sinistrécaille (« vos autres Murlocs ont +1 ATQ ») ; le Fouisseur
        // merrant passe de 5/2 à 4/2. Reproduit ici : une attaque tue sa cible et un autre serviteur perd 1 ATQ.
        // Première situation réelle avec une attaque de serviteur sur un serviteur adverse et un troisième serviteur en jeu.
        var (after, attack, bystander) = Decisions(DepthsGame).Concat(Decisions(CoinGame))
            .SelectMany(s => Ctx(s).LegalActions
                .Where(a => a.Kind == "attack" && a.TargetEntityId is int t && s.Opponent!.Board.Any(m => m.EntityId == t) && s.Local!.Board.Any(m => m.EntityId == a.SourceEntityId))
                .Select(a => (s, a, s.Local!.Board.Concat(s.Opponent!.Board).FirstOrDefault(m => m.EntityId != a.SourceEntityId && m.EntityId != a.TargetEntityId))))
            .First(x => x.Item3 is not null);
        void Weaker(JsonObject n)
        {
            foreach (var side in new[] { "local", "opponent" })
                foreach (var m in n[side]!["board"]!.AsArray())
                    if (Int(m!.AsObject(), "entityId") == bystander!.EntityId) m["attack"] = Int(m.AsObject(), "attack") - 1;
        }
        var auraGone = Change(after, n =>
        {
            var board = n["opponent"]!["board"]!.AsArray();
            board.Remove(board.First(m => Int(m!.AsObject(), "entityId") == attack.TargetEntityId));
            Weaker(n);
        });
        Assert.Null(StrategyCoordinator.UnplannedEffect(after, auraGone, attack));
        // Même perte d'attaque sans aucun départ ni arrivée : rien ne l'explique dans l'action.
        var unexplained = Change(after, Weaker);
        Assert.NotNull(StrategyCoordinator.UnplannedEffect(after, unexplained, attack));
    }

    [Fact]
    public async Task A_kept_call_whose_action_caused_unplanned_damage_is_cancelled_and_a_new_call_is_made()
    {
        var (before, after) = DepthsThenWyrm();
        var wyrm = after.Local!.Board.First(m => m.Name == "Wyrm du pic venteux").EntityId;
        // Le Wyrm, cible prévue de l'étape suivante, a été blessé par un effet imprévu : l'étape reste légale, plus forcément bonne.
        var hurt = Change(after, n =>
        {
            var m = n["local"]!["board"]!.AsArray().First(x => Int(x!.AsObject(), "entityId") == wyrm)!.AsObject();
            m["damage"] = Int(m, "damage") + 2; m["health"] = Int(m, "health") - 2;
        });
        var provider = new StreamingProvider
        {
            Answer = ctx => ctx.LegalActions.FirstOrDefault(a => a.Kind == "play" && a.SourceCardId is "CORE_REV_990" or "REV_990") is { } depths
                ? new StrategyOutput
                {
                    ActionId = depths.Id, Summary = "Pose Profondeurs Sanguines.", Confidence = "moyenne",
                    FollowUp = new() { new FollowUpStep { Text = "Active Profondeurs sur le Wyrm.", SourceEntityId = depths.SourceEntityId, TargetEntityId = wyrm } },
                }
                : new StrategyOutput { ActionId = ctx.LegalActions.First(a => a.Kind != "end-turn").Id, Summary = "Nouvel appel.", Confidence = "moyenne" },
        };
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        coordinator.OnSnapshot(WithoutOptions(hurt));
        coordinator.OnSnapshot(hurt);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(1, metrics.Counter("stratégie : appels annulés (décision passée)"));
        Assert.Equal(0, metrics.Counter("stratégie : appels conservés (début de réponse joué)"));
    }

    [Fact]
    public async Task A_complete_advice_whose_action_caused_unplanned_damage_is_not_continued()
    {
        // Même garde pour la reprise du plan après un conseil complet (comportement déjà présent, désormais plus strict).
        var (before, after) = DepthsThenWyrm();
        var wyrm = after.Local!.Board.First(m => m.Name == "Wyrm du pic venteux").EntityId;
        // Le Wyrm, cible prévue de l'étape suivante, a été blessé par un effet imprévu : l'étape reste légale, plus forcément bonne.
        var hurt = Change(after, n =>
        {
            var m = n["local"]!["board"]!.AsArray().First(x => Int(x!.AsObject(), "entityId") == wyrm)!.AsObject();
            m["damage"] = Int(m, "damage") + 2; m["health"] = Int(m, "health") - 2;
        });
        var provider = new StreamingProvider
        {
            Answer = ctx => ctx.LegalActions.FirstOrDefault(a => a.Kind == "play" && a.SourceCardId is "CORE_REV_990" or "REV_990") is { } depths
                ? new StrategyOutput
                {
                    ActionId = depths.Id, Summary = "Pose Profondeurs Sanguines.", Confidence = "moyenne",
                    FollowUp = new() { new FollowUpStep { Text = "Active Profondeurs sur le Wyrm.", SourceEntityId = depths.SourceEntityId, TargetEntityId = wyrm } },
                }
                : new StrategyOutput { ActionId = ctx.LegalActions.First(a => a.Kind != "end-turn").Id, Summary = "Nouvel appel.", Confidence = "moyenne" },
        };
        var (coordinator, _, metrics) = Start(provider);
        await using var _c = coordinator;
        coordinator.OnSnapshot(before);
        await WaitUntil(() => coordinator.Current.Phase == AdvisorPhases.Partial);
        provider.Release(0);
        await WaitUntil(() => coordinator.Board.Current?.Revision == before.Revision);
        coordinator.OnSnapshot(hurt);
        await WaitUntil(() => provider.Calls == 2);
        Assert.Equal(2, provider.Calls);
        Assert.Equal(0, metrics.Counter("stratégie : suites de plan affichées sans appel"));
    }
}
