using HsCopilot.Core.Engine;
using HsCopilot.Core.State;
using HsCopilot.Core.Verification;

namespace HsCopilot.Core.Tests;

/// <summary>Tests sur des parties réelles (Hearthstone build 254477, octobre 2026), BattleTags anonymisés.</summary>
public class RealGameTests
{
    public static TheoryData<string> AllFixtures => new() { TestData.PriestGame, TestData.RogueGame, TestData.RewindGame };

    // ------------------------------------------------------------------ Preuves globales

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Every_client_printed_view_matches_a_reconstructed_state(string fixture)
    {
        var report = ConsistencyOracle.Run(TestData.NewEngine(), TestData.Lines(fixture));
        Assert.True(report.Checks > 200, $"trop peu de contrôles ({report.Checks})");
        Assert.Equal(0, report.Mismatches);
        Assert.True(report.MaxLagRevisions <= 20, $"retard d'animation inattendu : {report.MaxLagRevisions}");
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Displayed_costs_and_available_mana_agree_with_server_legality(string fixture)
    {
        var report = ConsistencyOracle.Run(TestData.NewEngine(), TestData.Lines(fixture));
        Assert.True(report.ManaChecks > 40);
        Assert.Equal(0, report.ManaMismatches);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Never_exposes_hidden_opponent_cards(string fixture)
    {
        var report = VisibilityAudit.Run(TestData.Lines(fixture), TestData.Cards);
        Assert.True(report.OpponentHiddenSlotsChecked > 100);
        Assert.Equal(0, report.Violations);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Parses_every_line_without_errors(string fixture)
    {
        var engine = TestData.Replay(fixture);
        Assert.Equal(0, engine.UnparsedCount);
        Assert.DoesNotContain(engine.Warnings, w => w.Code is "PARSER_EXCEPTION" or "BLOCK_END_WITHOUT_START" or "LOCAL_PLAYER_CONFLICT" or "UNRESOLVED_PLAYER_NAME");
        Assert.Equal(0, engine.BlockDepth);
        Assert.Equal(LocalPlayerEvidence.SentByClient, engine.LocalEvidence);
    }

    [Theory]
    [MemberData(nameof(AllFixtures))]
    public void Decklist_accounting_is_consistent_at_every_local_turn(string fixture)
    {
        for (int turn = 1; turn <= 12; turn++)
        {
            var engine = TestData.NewEngine();
            if (ReplayRunner.RunUntilLocalTurn(engine, TestData.Lines(fixture), turn) < 0) break;
            var deck = TestData.Project(engine).Local!.Deck;
            Assert.Null(deck.Warning);
            Assert.Equal(deck.Count, deck.KnownRemaining.Sum(c => c.Count) + deck.KnownAdded.Count + deck.UnknownCount);
        }
    }

    // ------------------------------------------------------------------ Métadonnées (comparées aux statistiques HDT : DeckStats.xml)

    [Fact]
    public void Priest_game_metadata_matches_hdt_record()
    {
        // HDT : Result=Loss, Turns=5, Coin=false (le joueur a commencé), adversaire Prêtre.
        var s = TestData.Project(TestData.Replay(TestData.PriestGame));
        Assert.Equal("GT_RANKED", s.Game.GameType);
        Assert.Equal("FT_WILD", s.Game.FormatType);
        Assert.Equal(2, s.Local!.PlayerId);
        Assert.Equal("local", s.Game.FirstPlayer);
        Assert.Equal(5, s.Game.TurnNumber);
        Assert.Equal("LOST", s.Game.Result);
        Assert.Equal("local", s.Game.Conceded);
        Assert.Equal("WARRIOR", s.Local.HeroClass);
        Assert.Equal("PRIEST", s.Opponent!.HeroClass);
    }

    [Fact]
    public void Rogue_game_metadata_matches_hdt_record()
    {
        // HDT : Result=Loss, Turns=9, Coin=true (l'adversaire a commencé), adversaire Voleur.
        var engine = TestData.Replay(TestData.RogueGame);
        var s = TestData.Project(engine);
        Assert.Equal(1, s.Local!.PlayerId);
        Assert.Equal("opponent", s.Game.FirstPlayer);
        Assert.Equal(9, s.Game.TurnNumber);
        Assert.Equal("LOST", s.Game.Result);
        Assert.Equal("ROGUE", s.Opponent!.HeroClass);
        Assert.Contains(s.History, h => h.Kind == "play" && h.Player == "local" && h.SourceCardId == "GAME_005");
    }

    // ------------------------------------------------------------------ Tour de référence vérifié à la main

    /// <summary>
    /// Partie classée contre Prêtre, tour 3 du joueur local (TURN=5), au moment où le serveur envoie les options.
    /// Valeurs vérifiées dans le log brut : PV adverses 40 (Briseuse d'âme Azalina), armure 2 (pouvoir héroïque du tour 2),
    /// RESOURCES=3 et RESOURCES_USED=0.
    /// </summary>
    [Fact]
    public void Reconstructs_real_turn_3_of_priest_game()
    {
        var (_, s) = TestData.AtLocalTurn(TestData.PriestGame, 3);
        Assert.Equal(5, s.Game.GameTurn);
        Assert.Equal("local", s.Game.ActivePlayer);
        Assert.Equal("MAIN_ACTION", s.Game.Step);
        Assert.False(s.Uncertainty.Incomplete);

        var me = s.Local!;
        Assert.Equal(30, me.Hero!.Health);
        Assert.Equal(2, me.Hero.Armor);
        Assert.Equal(3, me.Mana.Available);
        Assert.Equal(3, me.Mana.Max);
        Assert.Equal(new[] { "END_033", "TIME_045", "TIME_714", "END_021", "FIR_939" }, me.Hand.Cards.Select(c => c.CardId));
        Assert.Equal(new int?[] { 4, 3, 6, 3, 2 }, me.Hand.Cards.Select(c => c.Cost));

        var slither = me.Hand.Cards[0];
        Assert.Equal(7, slither.BaseCost);
        Assert.True(slither.CostModified);
        Assert.True(slither.PoweredUp);

        var location = Assert.Single(me.Board);
        Assert.Equal("CORE_REV_990", location.CardId);
        Assert.Equal("LOCATION", location.Type);
        Assert.Equal(3, location.Health);
        Assert.True(location.State.ServerAllowsAction);

        Assert.Equal(24, me.Deck.Count);
        Assert.Equal(24, me.Deck.KnownRemaining.Sum(c => c.Count));
        Assert.Null(me.Deck.Warning);

        var opp = s.Opponent!;
        Assert.Equal(40, opp.Hero!.Health);
        Assert.Equal("EDR_852", Assert.Single(opp.Board).CardId);
        Assert.Equal(5, opp.Hand.Count);
        Assert.All(opp.Hand.Cards, c => Assert.Null(c.CardId));
        Assert.Single(opp.Hand.Cards, c => c.Hypothesis is not null);
        Assert.Equal(34, opp.Deck.Count);
        Assert.Equal(new[] { "TLC_817t", "TLC_817t2" }, opp.PublicSecretZone.Select(q => q.CardId).OrderBy(x => x));

        // Chronoseigneur Époque : serviteurs réellement joués par l'adversaire à son dernier tour.
        Assert.Equal(4, opp.LastTurn!.GameTurn);
        Assert.Equal("EDR_852", Assert.Single(opp.LastTurn.MinionsPlayed).CardId);

        var options = s.LegalOptions!;
        Assert.True(options.Fresh);
        Assert.True(options.CanEndTurn);
        Assert.Equal(5, options.Playable.Count);
        Assert.Contains(options.Playable, o => o.Kind == "hero-power");
        var activate = Assert.Single(options.Playable, o => o.Kind == "location");
        Assert.Equal("EDR_852", Assert.Single(activate.Targets).CardId);
        Assert.DoesNotContain(options.Playable, o => o.CardId is "END_033" or "TIME_714"); // 4 et 6 mana : trop cher
    }

    // ------------------------------------------------------------------ Mécaniques ciblées

    [Fact]
    public void Mulligan_records_kept_and_replaced_cards()
    {
        var s = TestData.Project(TestData.Replay(TestData.PriestGame));
        var mine = Assert.Single(s.History, h => h.Kind == "mulligan" && h.Player == "local");
        Assert.Equal(new[] { "END_033" }, mine.Related.Where(r => r.Note == "gardée").Select(r => r.CardId));
        Assert.Equal(new[] { "END_021", "TIME_045" }, mine.Related.Where(r => r.Note == "remplacée").Select(r => r.CardId).OrderBy(x => x));
        var theirs = Assert.Single(s.History, h => h.Kind == "mulligan" && h.Player == "opponent");
        Assert.Empty(theirs.Related); // cartes adverses : seulement les nombres
        // Second joueur : 4 cartes et La pièce proposées, 4 « choisies ». La carte non choisie est La pièce (entité 79), restée
        // en main et jouée à 11:13:47 : elle n'est jamais remplacée. Aucune carte adverse ne retourne dans le deck.
        Assert.Equal("5 gardée(s), 0 remplacée(s)", theirs.Detail);
    }

    [Fact]
    public void Turn_start_draw_is_in_hand_with_its_turn()
    {
        var (_, s) = TestData.AtLocalTurn(TestData.PriestGame, 3);
        var drawn = Assert.Single(s.Local!.Hand.Cards, c => c.EnteredTurn == 5);
        Assert.False(drawn.Created);
    }

    [Fact]
    public void Kindred_cost_reduction_is_visible_in_hand()
    {
        // Tour 4 : un Dragon (Dragonnet du Vol infini) a été joué au tour précédent → Wyrm du pic venteux coûte 8 - 2.
        var (_, s) = TestData.AtLocalTurn(TestData.PriestGame, 4);
        var wyrm = Assert.Single(s.Local!.Hand.Cards, c => c.CardId == "TLC_600");
        Assert.Equal(6, wyrm.Cost);
        Assert.Equal(8, wyrm.BaseCost);
        Assert.True(wyrm.PoweredUp);
        Assert.Contains("DRAGON", s.Local.LastTurn!.TypesPlayed);
    }

    [Fact]
    public void Stat_buff_in_hand_and_its_enchantment_are_reported()
    {
        // Avant-garde du portail : pioche un serviteur et lui donne +2/+2 (Voyante distordue 1/3 → 3/5).
        var (_, s) = TestData.AtLocalTurn(TestData.RogueGame, 6);
        var seers = s.Local!.Hand.Cards.Where(c => c.CardId == "END_022").ToList();
        var buffed = Assert.Single(seers, c => c.Attack == 3);
        Assert.Equal(5, buffed.Health);
        Assert.Equal(1, buffed.BaseAttack);
        Assert.Contains(buffed.Enchantments, e => e.CardId == "TIME_003e");
        Assert.Single(seers, c => c.Attack == 1 && c.Health == 3);
    }

    [Fact]
    public void Weapon_durability_uses_health_tag()
    {
        var (_, s) = TestData.AtLocalTurn(TestData.RogueGame, 6);
        Assert.True(s.Opponent!.Weapon!.Durability >= 0);
        Assert.True(s.Local!.Weapon!.Durability >= 0);
        Assert.Equal(s.Local.Hero!.Attack, s.Local.Weapon.Attack);
    }

    [Fact]
    public void Location_recharge_is_observed_over_two_player_turns()
    {
        // Activé au tour 4 (TURN=7) : DAMAGE 1, EXHAUSTED 1, LOCATION_ACTION_COOLDOWN 1.
        // Tour 5 (TURN=9) : cooldown 0 mais le serveur refuse encore (REQ_NOT_EXHAUSTED_LOCATION).
        var (_, s4) = TestData.AtLocalTurn(TestData.PriestGame, 4);
        Assert.True(Assert.Single(s4.Local!.Board, m => m.Type == "LOCATION").State.ServerAllowsAction);
        Assert.Contains(TestData.Project(TestData.Replay(TestData.PriestGame)).History, h => h.Kind == "location" && h.TurnNumber == 4);

        var (_, s5) = TestData.AtLocalTurn(TestData.PriestGame, 5);
        var depths = Assert.Single(s5.Local!.Board, m => m.EntityId == 42);
        Assert.Equal(2, depths.Health);
        Assert.Equal(0, depths.State.LocationCooldown);
        Assert.True(depths.State.Exhausted);
        Assert.False(depths.State.ServerAllowsAction);
    }

    [Fact]
    public void Attacks_and_end_of_turn_triggers_are_in_history()
    {
        var s = TestData.Project(TestData.Replay(TestData.RogueGame));
        Assert.Contains(s.History, h => h.Kind == "attack" && h.Player == "local" && h.TargetEntityId is not null);
        Assert.Contains(s.History, h => h.Kind == "attack" && h.Player == "opponent");
        // Colporteur de pétales : « À la fin de votre tour, donne +1/+1 à un autre Dragon allié aléatoire ».
        Assert.Contains(s.History, h => h.Kind == "trigger" && h.SourceCardId == "EDR_889");
    }

    [Fact]
    public void Taunt_keyword_is_extracted_when_present()
    {
        var engine = TestData.NewEngine();
        var lines = TestData.Lines(TestData.RogueGame);
        var found = false;
        foreach (var l in lines)
        {
            engine.Process(l);
            if (!l.Contains("tag=TAUNT value=1", StringComparison.Ordinal)) continue;
            var s = TestData.Project(engine);
            found |= (s.Local?.Board ?? new()).Concat(s.Opponent?.Board ?? new()).Any(m => m.Keywords.Contains("TAUNT"));
            if (found) break;
        }
        Assert.True(found);
    }

    // ------------------------------------------------------------------ Rembobinage

    [Fact]
    public void Rewind_resets_are_applied_from_server_snapshot()
    {
        var engine = TestData.Replay(TestData.RewindGame);
        Assert.Equal(2, engine.ResetCount);
        var s = TestData.Project(engine);
        Assert.Equal(2, s.History.Count(h => h.Kind == "rewind"));
        Assert.Contains(s.History, h => h.Rewound);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "GAME_RESET_APPLIED");
        Assert.DoesNotContain(engine.Warnings, w => w.Code == "DUPLICATE_ENTITY_CREATE");
        // Les entités annulées n'apparaissent nulle part.
        var orphans = engine.Entities.Values.Where(e => e.Orphaned).Select(e => e.Id).ToHashSet();
        Assert.NotEmpty(orphans);
        Assert.DoesNotContain(s.Local!.Hand.Cards, c => orphans.Contains(c.EntityId));
        Assert.DoesNotContain(s.Local.Board, m => orphans.Contains(m.EntityId));
    }

    [Fact]
    public void Rewind_choice_offers_current_and_past_timelines()
    {
        var engine = TestData.NewEngine();
        foreach (var l in TestData.Lines(TestData.RewindGame))
        {
            engine.Process(l);
            if (l.Contains("DebugPrintEntityChoices() -   Entities[1]=", StringComparison.Ordinal) && l.Contains("TIME_000tb", StringComparison.Ordinal))
                break;
        }
        var choice = TestData.Project(engine).PendingChoice;
        Assert.NotNull(choice);
        Assert.Equal(new[] { "TIME_000ta", "TIME_000tb" }, choice!.Offered.Select(o => o.CardId));
    }
}
