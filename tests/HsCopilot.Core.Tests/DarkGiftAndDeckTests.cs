using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Sombres dons, contenu du deck et tour suivant, sur de vraies parties anonymisées :
/// Découverte du 25/10/2026 (Concert promo-Drake + Somnambule, Drake terrestre + Courtes griffes, Seigneur Aile de mort +
/// Cauchemar vivant), Beaux rêves du 24/10/2026 (Nozdormu placé sur le dessus du deck), mulligan, Parenté, recharge.
/// </summary>
public class DarkGiftAndDeckTests
{
    private const string Wyrm = "ai-depths-wyrm-lethal.log";
    private const string TopOfDeck = "ai-darkgift-top-of-deck.log";

    private static readonly Dictionary<string, List<GameStateSnapshot>> DecisionCache = new();

    private static List<GameStateSnapshot> Decisions(string fixture)
    {
        lock (DecisionCache)
        {
            if (DecisionCache.TryGetValue(fixture, out var list)) return list;
            var game = ReplayFile.SplitGames(TestData.Lines(fixture)).Last();
            list = DatasetBuilder.BuildGame(fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 12, Decklist = TestData.DragonWarrior })
                .Select(c => c.LoadSnapshot()).ToList();
            DecisionCache[fixture] = list;
            return list;
        }
    }

    /// <summary>Rejoue la partie ligne à ligne et renvoie le premier état projeté qui satisfait la condition.</summary>
    private static GameStateSnapshot FirstState(string fixture, Func<GameStateSnapshot, bool> predicate, Func<PowerLogLine, bool>? when = null)
    {
        var game = ReplayFile.SplitGames(TestData.Lines(fixture)).Last();
        var engine = new GameEngine(new EngineOptions { Cards = TestData.Cards });
        var projector = new GameStateProjector(TestData.Cards);
        var revision = -1;
        foreach (var raw in game.Lines)
        {
            var p = PowerLogLineParser.TryParse(raw);
            if (p is null) continue;
            engine.Process(p);
            var check = when?.Invoke(p) ?? engine.Revision != revision;
            revision = engine.Revision;
            if (!check || engine.LocalPlayerId is null) continue;
            var s = projector.Project(engine, new ProjectionContext { Transport = "test", Decklist = TestData.DragonWarrior });
            if (predicate(s)) return s;
        }
        throw new InvalidOperationException("état introuvable");
    }

    private static GameStateSnapshot DiscoverChoice() =>
        FirstState(Wyrm, s => s.PendingChoice?.SourceCardId == "EDR_102t" && s.PendingChoice.Offered.Count == 3, p => p.Method != "DebugPrintEntityChoices");

    private static GameStateSnapshot Mutate(GameStateSnapshot s, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(s, Json.Compact))!.AsObject();
        change(node);
        return JsonSerializer.Deserialize<GameStateSnapshot>(node, Json.Read)!;
    }

    // ------------------------------------------------------------------ Sombres dons

    [Fact]
    public void Each_discover_offer_carries_its_own_dark_gift_read_from_the_log()
    {
        var s = DiscoverChoice();
        var o = s.PendingChoice!.Offered;
        Assert.Equal(new[] { "Concert promo-Drake", "Drake terrestre", "Seigneur Aile de mort" }, o.Select(x => x.Name));
        Assert.Equal(new[] { "EDR_100t6", "EDR_100t2", "EDR_100t5" }, o.Select(x => x.DarkGift?.CardId));
        Assert.Equal(new[] { "Somnambule", "Courtes griffes", "Cauchemar vivant" }, o.Select(x => x.DarkGift?.Name));

        var charge = o[0].DarkGift!.Result!;           // 8/8 pour 8 avec Charge
        Assert.Equal((8, 8, 8), (charge.Cost!.Value, charge.Attack!.Value, charge.Health!.Value));
        Assert.Contains("CHARGE", charge.AddedKeywords);
        var cheap = o[1].DarkGift!.Result!;            // Drake 4/4 pour 5 → 2/4 pour 3
        Assert.Equal((3, 2, 4), (cheap.Cost!.Value, cheap.Attack!.Value, cheap.Health!.Value));
        var copy = o[2].DarkGift!.Result!;             // Aile de mort inchangé, copie 2/2
        Assert.Equal((10, 12, 12), (copy.Cost!.Value, copy.Attack!.Value, copy.Health!.Value));
        Assert.Contains("copie 2/2", copy.Note);
    }

    [Fact]
    public void The_model_receives_the_three_complete_results_and_gift_labels()
    {
        var ctx = DecisionContextBuilder.BuildChoice(DiscoverChoice())!;
        Assert.Equal(new[] { "Choisir Concert promo-Drake + Somnambule", "Choisir Drake terrestre + Courtes griffes", "Choisir Seigneur Aile de mort + Cauchemar vivant" },
            ctx.LegalActions.Select(a => a.Label));
        var offered = JsonNode.Parse(PromptBuilder.BuildUserPayload(ctx))!["state"]!["pendingChoice"]!["offered"]!.AsArray();
        Assert.Equal("Somnambule", offered[0]!["darkGift"]!["name"]!.GetValue<string>());
        Assert.Contains("CHARGE", offered[0]!["darkGift"]!["result"]!["addedKeywords"]!.ToJsonString());
        Assert.Equal(3, offered[1]!["darkGift"]!["result"]!["cost"]!.GetValue<int>());
        Assert.Contains("Charge", PromptBuilder.BuildUserPayload(ctx));
        Assert.Contains("darkGift", PromptBuilder.BaseInstructions);
    }

    [Fact]
    public void The_trace_records_the_offers_exactly_as_sent_for_live_verification()
    {
        var s = DiscoverChoice();
        var update = new AdvisorUpdate { Phase = AdvisorPhases.Analyzing, GameId = s.GameId, Revision = s.Revision, DecisionKind = DecisionContext.KindChoice, State = s };
        var offered = AdvisorTrace.ToEntry(update).Offered!;
        Assert.Equal(3, offered.Count);
        Assert.StartsWith("Concert promo-Drake 8/8 (8) + Somnambule : 8/8 pour 8, Charge", offered[0]);
        Assert.Contains("Courtes griffes : 2/4 pour 3", offered[1]);
    }

    [Fact]
    public void After_the_choice_the_hand_card_shows_the_gift_applied_by_the_server()
    {
        var s = FirstState(Wyrm, x => x.Local?.Hand.Cards.Any(c => c.CardId == "CATA_999") == true);
        var drake = s.Local!.Hand.Cards.First(c => c.CardId == "CATA_999");
        Assert.Equal(3, drake.Cost);
        Assert.Contains(drake.Enchantments, e => e.CardId == "EDR_100t2e");
    }

    [Fact]
    public void A_choice_without_gift_never_gets_an_invented_one()
    {
        // Choix du Rembobinage (ligne temporelle) : aucun tag DARK_GIFT_ENTITY.
        var s = FirstState(Wyrm, x => x.PendingChoice is { ChoiceType: "GENERAL" } pc && pc.SourceCardId != "EDR_102t" && pc.Offered.Count > 0, p => p.Method != "DebugPrintEntityChoices");
        Assert.All(s.PendingChoice!.Offered, o => Assert.Null(o.DarkGift));
        Assert.All(DecisionContextBuilder.BuildChoice(s)!.LegalActions, a => Assert.DoesNotContain("+", a.Label));
    }

    [Fact]
    public void Unknown_gift_ids_keep_their_official_text_but_no_computed_result()
    {
        Assert.Null(DarkGifts.Apply("EDR_100t99", 5, 4, 4));
        Assert.Equal(12, DarkGifts.KnownGiftIds.Count);
        var top = DarkGifts.Apply("EDR_100t8", 3, 8, 8)!;            // Beaux rêves sur Nozdormu 8/8 : 12/13, vérifié dans le log
        Assert.Equal((12, 13), (top.Attack!.Value, top.Health!.Value));
        Assert.Contains("dessus de ton deck", top.Note);
    }

    [Fact]
    public void Beaux_reves_makes_the_next_draw_publicly_known()
    {
        var s = FirstState(TopOfDeck, x => x.Local?.Deck.KnownTop.Count > 0);
        var top = Assert.Single(s.Local!.Deck.KnownTop);
        Assert.Equal("Nozdormu, le seigneur du temps", top.Name);
        var ctx = DecisionContextBuilder.Build(s) ?? new DecisionContext
        {
            GameId = s.GameId, Revision = s.Revision, OptionsId = 0, GameTurn = s.Game.GameTurn, ResetCount = 0, HistorySeq = 0, State = s,
        };
        var deck = JsonNode.Parse(JsonSerializer.Serialize(DeckOdds.Payload(ctx), Json.Compact))!;
        Assert.Contains("connue : Nozdormu", deck["nextDraw"]!.GetValue<string>());
        // Pioché ensuite : il quitte le dessus du deck connu.
        var drawn = FirstState(TopOfDeck, x => x.Local?.Hand.Cards.Any(c => c.EntityId == top.EntityId) == true);
        Assert.Empty(drawn.Local!.Deck.KnownTop);
    }

    // ------------------------------------------------------------------ Deck et pioche

    [Fact]
    public void The_users_deck_code_matches_the_deck_selected_in_hdt()
    {
        // Code fourni par le joueur le 25/10/2026 ; liste « Guerrier dragons » de PlayerDecks.xml (HDT).
        const string code = "AAECAQcEi6AEqKAEhJ0H8a8HDePmBqr8Bqv8BveDB+iHB9KXB5KkB7etB5iwB4+xB9OyB9ayB+yyBwAA";
        var decoded = DeckString.Decode(code);
        Assert.Equal("Standard", decoded.FormatName);
        Assert.Equal(30, decoded.Total);
        var list = DeckString.ToDecklist(code, ((HsCopilot.Core.Cards.CardDatabase)TestData.Cards).All);
        Assert.Equal(TestData.DragonWarrior.Cards.OrderBy(kv => kv.Key), list.Cards.OrderBy(kv => kv.Key));
        // Partie du 25/10 rejouée avec cette liste : aucune carte hors liste, aucune carte du deck inconnue.
        Assert.All(Decisions(Wyrm), s => { Assert.Null(s.Local!.Deck.Warning); Assert.Equal(0, s.Local.Deck.UnknownCount); });
    }

    [Fact]
    public void Draw_odds_are_exact_fractions_of_the_known_deck()
    {
        var s = Decisions(Wyrm).First(x => x.Game.TurnNumber == 7);
        var ctx = DecisionContextBuilder.Build(s)!;
        var d = s.Local!.Deck;
        Assert.True(d.DecklistKnown);
        Assert.Equal(0, d.UnknownCount);
        var dragons = d.KnownRemaining.Where(c => c.Races?.Contains("DRAGON") == true).Sum(c => c.Count);
        var node = JsonNode.Parse(JsonSerializer.Serialize(DeckOdds.Payload(ctx), Json.Compact))!;
        Assert.StartsWith("exact", node["basis"]!.GetValue<string>());
        Assert.StartsWith($"{dragons}/{d.Count} ", node["nextDraw"]!["dragon"]!.GetValue<string>());
    }

    [Fact]
    public void Avant_garde_lists_every_minion_it_can_draw_with_its_probability()
    {
        var s = Decisions(Wyrm).First(x => x.Game.TurnNumber == 3);
        var node = JsonNode.Parse(JsonSerializer.Serialize(DeckOdds.Payload(DecisionContextBuilder.Build(s)!), Json.Compact))!;
        var draw = node["filteredDraws"]!.AsArray().Single(x => x!["card"]!.GetValue<string>() == "Avant-garde du portail")!;
        var minions = s.Local!.Deck.KnownRemaining.Where(c => c.Type == "MINION").Sum(c => c.Count);
        Assert.Equal(minions, draw["candidates"]!.GetValue<int>());
        Assert.Null(draw["condition"]);                                  // pas de condition sur cette carte
        var outcomes = draw["outcomes"]!.ToJsonString();
        Assert.Contains($"1/{minions} chacun", outcomes);
        Assert.Contains("Grommash Hurlenfer", outcomes);
        Assert.Contains("second tirage du Rembobinage", outcomes);
    }

    [Fact]
    public void Draw_odds_are_sent_to_the_model_only_when_a_draw_matters()
    {
        var withVanguard = DecisionContextBuilder.Build(Decisions(Wyrm).First(x => x.Game.TurnNumber == 3))!;
        Assert.NotNull(DeckOdds.Payload(withVanguard, onlyIfRelevant: true));
        var noDrawCard = DecisionContextBuilder.Build(Decisions(Wyrm).First(x => x.Game.TurnNumber == 7))!;
        Assert.Null(DeckOdds.Payload(noDrawCard, onlyIfRelevant: true));
        Assert.NotNull(DeckOdds.Payload(noDrawCard));
    }

    [Fact]
    public void Unidentified_deck_cards_turn_odds_into_estimates()
    {
        var s = Decisions(Wyrm).First(x => x.Game.TurnNumber == 7);
        var more = Mutate(s, n => n["local"]!["deck"]!["count"] = s.Local!.Deck.Count + 2);
        var node = JsonNode.Parse(JsonSerializer.Serialize(DeckOdds.Payload(DecisionContextBuilder.Build(more)!), Json.Compact))!;
        Assert.StartsWith("estimation : 2 carte(s)", node["basis"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Tour suivant

    [Fact]
    public void Kindred_tells_the_next_turn_cost_of_the_wyrm_and_the_dragons_playable_now()
    {
        var s = Decisions(Wyrm).First(x => x.Local!.Hand.Cards.Any(c => c.CardId == "TLC_600"));
        var (mana, kindred, _) = QuickAnalysis.NextTurnFacts(DecisionContextBuilder.Build(s)!);
        Assert.Equal(Math.Min(10, s.Local!.Mana.Max + 1), mana);
        var line = Assert.Single(kindred);
        Assert.StartsWith("Wyrm du pic venteux coûtera", line);
        if (s.Local.ThisTurn?.TypesPlayed.Contains("DRAGON") == true) Assert.Contains("coûtera 6 au prochain tour", line);
        else Assert.Contains("ou 6 si tu joues un Dragon ce tour", line);
    }

    [Fact]
    public void Activating_depths_with_grommash_in_hand_warns_about_next_turn()
    {
        var s = Decisions(TestData.GrommashGame).First(x =>
            x.Local!.Hand.Cards.Any(c => c.CardId == "CORE_EX1_414") && DecisionContextBuilder.Build(x)!.LegalActions.Any(a => a.Kind == "location"));
        var (_, _, locations) = QuickAnalysis.NextTurnFacts(DecisionContextBuilder.Build(s)!);
        Assert.Contains(locations, l => l.Contains("indisponible à ton prochain tour") && l.Contains("Grommash"));
    }

    [Fact]
    public void Next_turn_potential_is_a_labelled_hypothesis_never_a_lethal()
    {
        var s = Decisions(Wyrm).First(x => x.Game.TurnNumber == 7);
        var r = TurnSolver.SolveNextTurn(DecisionContextBuilder.Build(s)!);
        Assert.Equal(LethalLevels.None, r.Level);
        Assert.True(r.MaxFaceDamage >= 10);                              // Wyrm prêt, Profondeurs, Suffusion
        Assert.All(r.Sequence, x => Assert.Null(x.Action));               // rien n'est une action légale aujourd'hui
        // Joint au message seulement s'il atteint les PV adverses : 18 PV ici, non ; à 10 PV, oui.
        var far = JsonNode.Parse(JsonSerializer.Serialize(QuickAnalysis.NextTurnPayload(DecisionContextBuilder.Build(s)!), Json.Compact));
        Assert.Null(far?["ifBoardSurvives"]);
        var low = Mutate(s, n => { n["opponent"]!["hero"]!["health"] = 10; n["opponent"]!["hero"]!["armor"] = 0; });
        var near = JsonNode.Parse(JsonSerializer.Serialize(QuickAnalysis.NextTurnPayload(DecisionContextBuilder.Build(low)!), Json.Compact))!;
        Assert.StartsWith("hypothèse", near["ifBoardSurvives"]!["assumptions"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Prudence du solveur

    private static GameStateSnapshot Turn8Lethal() => Decisions(Wyrm).First(s =>
        s.Game.TurnNumber == 8 && s.Local!.Weapon is not null && s.Opponent!.Hero!.Health == 5);

    private static GameStateSnapshot Turn7AfterDepths() => Decisions(Wyrm).First(s =>
        s.Game.TurnNumber == 7 && DecisionContextBuilder.Build(s)!.LegalActions.Any(a => a.Kind == "location" && a.TargetName == "Wyrm du pic venteux"));

    private static JsonObject Minion(int id, string cardId, string name, int attack, int health, string text, params string[] keywords) => new()
    {
        ["entityId"] = id, ["cardId"] = cardId, ["name"] = name, ["type"] = "MINION", ["position"] = 1,
        ["attack"] = attack, ["health"] = health, ["maxHealth"] = health, ["text"] = text,
        ["keywords"] = new JsonArray(keywords.Select(k => (JsonNode)JsonValue.Create(k)!).ToArray()),
        ["state"] = new JsonObject { ["exhausted"] = false, ["attacksThisTurn"] = 0 },
    };

    [Fact]
    public void A_known_secret_also_keeps_a_lethal_conditional()
    {
        var s = Mutate(Turn8Lethal(), n => n["opponent"]!["secrets"] = new JsonArray(new JsonObject
        {
            ["entityId"] = 999, ["cardClass"] = "MAGE", ["cardId"] = "CORE_EX1_295", ["name"] = "Bloc de glace", ["known"] = true, ["enteredTurn"] = 1, ["hypotheses"] = new JsonArray(),
        }));
        var r = TurnSolver.Solve(DecisionContextBuilder.Build(s)!);
        Assert.Equal(LethalLevels.Conditional, r.Level);
        Assert.Contains(r.Conditions, c => c.Contains("Bloc de glace"));
    }

    [Fact]
    public void An_opponent_damage_prevention_effect_keeps_a_lethal_conditional()
    {
        var s = Mutate(Turn8Lethal(), n => n["opponent"]!["board"]!.AsArray().Add(Minion(998, "TEST_1", "Gardien test", 0, 5, "Votre héros est Insensible tant que ce serviteur est en vie.")));
        var r = TurnSolver.Solve(DecisionContextBuilder.Build(s)!);
        Assert.Equal(LethalLevels.Conditional, r.Level);
        Assert.Contains(r.Conditions, c => c.Contains("Gardien test") && c.Contains("non simulé"));

        // Arme qui absorbe les dégâts du héros (formulation du Rempart d'Azzinoth).
        var weapon = Mutate(Turn8Lethal(), n => n["opponent"]!["weapon"] = new JsonObject
        {
            ["entityId"] = 993, ["cardId"] = "TEST_3", ["name"] = "Arme test", ["attack"] = 0, ["durability"] = 3, ["maxDurability"] = 3,
            ["text"] = "Chaque fois que votre héros devrait subir des dégâts, cette arme perd 1 point de durabilité à la place.",
        });
        Assert.Equal(LethalLevels.Conditional, TurnSolver.Solve(DecisionContextBuilder.Build(weapon)!).Level);
    }

    [Fact]
    public void Killing_a_deathrattle_taunt_to_open_the_face_is_never_certain()
    {
        // Tour 7 réel, adversaire ramené à 10 PV derrière une Provocation 1/1 : Profondeurs la détruit, Wyrm 8 + Suffusion 2.
        GameStateSnapshot With(params string[] keywords) => Mutate(Turn7AfterDepths(), n =>
        {
            n["opponent"]!["hero"]!["health"] = 10;
            n["opponent"]!["hero"]!["armor"] = 0;
            n["opponent"]!["board"]!.AsArray().Add(Minion(997, "TEST_2", "Provocation test", 1, 1, "", keywords));
        });
        DecisionContext Blocked(GameStateSnapshot s)
        {
            var ctx = DecisionContextBuilder.Build(s)!;
            var face = ctx.LegalActions.Where(a => a.Kind == "attack" && a.TargetEntityId == s.Opponent!.Hero!.EntityId).ToList();
            ctx.LegalActions.RemoveAll(face.Contains);                                                  // la Provocation bloque le héros…
            foreach (var a in face)                                                                     // … les attaquants ne visent qu'elle
                ctx.LegalActions.Add(new LegalAction { Id = a.Id + ".p", OptionsId = a.OptionsId, OptionIndex = a.OptionIndex, Kind = "attack", SourceEntityId = a.SourceEntityId, TargetEntityId = 997, Label = "attaque test" });
            return ctx;
        }
        var plain = TurnSolver.Solve(Blocked(With("TAUNT")));
        Assert.Equal(LethalLevels.Certain, plain.Level);
        Assert.Contains(plain.Sequence, x => x.Label.Contains("détruit la Provocation"));
        var risky = TurnSolver.Solve(Blocked(With("TAUNT", "DEATHRATTLE")));
        Assert.Equal(LethalLevels.Conditional, risky.Level);
        Assert.Contains(risky.Conditions, c => c.Contains("Râle d'agonie"));
    }

    [Fact]
    public void A_silenced_drake_deals_no_end_of_turn_damage()
    {
        var baseline = TurnSolver.Solve(DecisionContextBuilder.Build(Turn8Lethal())!).MaxFaceDamage;
        GameStateSnapshot WithDrake(params string[] keywords) => Mutate(Turn8Lethal(), n => n["local"]!["board"]!.AsArray().Add(
            Minion(996, "CATA_999", "Drake terrestre", 4, 4, "À la fin de votre tour, inflige 4 points de dégâts au héros ou à l’héroïne adverse.", keywords)));
        Assert.Equal(baseline + 4, TurnSolver.Solve(DecisionContextBuilder.Build(WithDrake())!).MaxFaceDamage);
        Assert.Equal(baseline, TurnSolver.Solve(DecisionContextBuilder.Build(WithDrake("SILENCED"))!).MaxFaceDamage);
    }

    [Fact]
    public void A_charge_dark_gift_in_hand_is_counted_only_with_the_server_enchantment()
    {
        GameStateSnapshot WithCard(bool gift) => Mutate(Turn8Lethal(), n => n["local"]!["hand"]!["cards"]!.AsArray().Add(new JsonObject
        {
            ["entityId"] = 995, ["cardId"] = "ETC_099", ["name"] = "Concert promo-Drake", ["type"] = "MINION", ["cost"] = 0, ["attack"] = 8, ["health"] = 8,
            ["knowledge"] = "own", ["text"] = "Échangeable Final : détruit un serviteur adverse.",
            ["enchantments"] = gift ? new JsonArray(new JsonObject { ["entityId"] = 994, ["cardId"] = "EDR_100t6e", ["name"] = "Somnambulisme sournois" }) : new JsonArray(),
        }));
        DecisionContext Playable(GameStateSnapshot s)
        {
            var ctx = DecisionContextBuilder.Build(s)!;
            ctx.LegalActions.Add(new LegalAction { Id = "test.play", OptionsId = ctx.OptionsId, OptionIndex = 99, Kind = "play", SourceEntityId = 995, SourceCardId = "ETC_099", Label = "Jouer Concert promo-Drake" });
            return ctx;
        }
        var baseline = TurnSolver.Solve(DecisionContextBuilder.Build(Turn8Lethal())!).MaxFaceDamage;
        Assert.Equal(baseline + 8, TurnSolver.Solve(Playable(WithCard(gift: true))).MaxFaceDamage);
        Assert.Equal(baseline, TurnSolver.Solve(Playable(WithCard(gift: false))).MaxFaceDamage);
    }

    [Fact]
    public void Brood_keeper_sword_is_not_counted_when_the_client_says_its_condition_fails()
    {
        var start = Decisions(Wyrm).First(x => x.Game.TurnNumber == 8);
        Assert.Contains(TurnSolver.Solve(DecisionContextBuilder.Build(start)!).Sequence, x => x.Label.Contains("épée"));
        var keeperId = start.Local!.Hand.Cards.First(c => c.CardId == "EDR_457").EntityId;
        var refused = Mutate(start, n =>
        {
            foreach (var c in n["local"]!["hand"]!["cards"]!.AsArray())
                if (c!["entityId"]!.GetValue<int>() == keeperId) c["poweredUp"] = false;
        });
        Assert.DoesNotContain(TurnSolver.Solve(DecisionContextBuilder.Build(refused)!).Sequence, x => x.Label.Contains("épée"));
    }

    // ------------------------------------------------------------------ Mulligan

    [Fact]
    public void Mulligan_result_distinguishes_advice_choice_and_received_cards()
    {
        // 25/10/2026 : conseil « remplacer Chronoseigneur Époque et Suffusion d'ombreflamme, garder Colporteur de pétales ».
        var offered = FirstState(Wyrm, x => x.PendingChoice?.ChoiceType == "MULLIGAN" && x.PendingChoice.Offered.Count == 3, p => p.Method != "DebugPrintEntityChoices");
        var ctx = DecisionContextBuilder.BuildChoice(offered)!;
        var replace = ctx.LegalActions.Where(a => a.SourceName is "Chronoseigneur Époque" or "Suffusion d’ombreflamme").ToList();
        var advice = RecommendationValidator.Validate(new StrategyOutput
        {
            ActionId = replace[0].Id, Summary = "Remplace Chronoseigneur et Suffusion.", Confidence = "moyenne", ReplaceIds = replace.Select(a => a.Id).ToList(),
        }, ctx, "test", null, 0, null).Recommendation!;

        // Premier état où l'échange est appliqué (avant, l'historique contient le choix mais la main n'a pas encore changé).
        var after = FirstState(Wyrm, x => MulliganResult.From(x, null) is not null);
        Assert.Equal(3, after.Local!.Hand.Cards.Count);                                             // pioche du tour 1 pas encore arrivée
        var entry = MulliganResult.From(after, advice)!;
        Assert.Equal(MulliganResult.Phase, entry.Phase);
        Assert.Equal(new[] { "Colporteur de pétales" }, entry.MulliganKept);
        Assert.Equal(new[] { "Suffusion d’ombreflamme", "Chronoseigneur Époque" }.Order(), entry.MulliganReplaced!.Order());
        Assert.Equal(new[] { "Forgeron dimensionnel", "Sombre chevaucheur" }.Order(), entry.MulliganReceived!.Order());
        Assert.True(entry.MulliganFollowed);
        Assert.Contains("conseil suivi", entry.Message);
    }
}
