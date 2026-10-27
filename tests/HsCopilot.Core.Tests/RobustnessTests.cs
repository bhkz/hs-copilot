using System.Text.Json;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.State;
using HsCopilot.Core.Verification;

namespace HsCopilot.Core.Tests;

/// <summary>Flux synthétiques : informations cachées, ordre inattendu, lignes manquantes, rejeu en double.</summary>
public class RobustnessTests
{
    private sealed class Synth
    {
        private readonly List<string> _lines = new();
        private int _seconds;

        public Synth Power(string body, int indent = 0) => Add("DebugPrintPower", body, indent);
        public Synth Game(string body) => Add("DebugPrintGame", body, 0);
        public Synth Options(string body, int indent = 0) => Add("DebugPrintOptions", body, indent);
        public Synth PowerList() => Add("DebugPrintPowerList", "Count=1", 0);
        public Synth EntityChoices(string body, int indent = 0) => Add("DebugPrintEntityChoices", body, indent);

        private Synth Add(string method, string body, int indent)
        {
            _seconds++;
            var t = TimeSpan.FromHours(10) + TimeSpan.FromSeconds(_seconds);
            _lines.Add($"D {t:hh\\:mm\\:ss}.0000000 GameState.{method}() - {new string(' ', indent)}{body}");
            return this;
        }

        public IReadOnlyList<string> Lines => _lines;

        /// <summary>Partie minimale : P1 local (une carte connue en main), P2 adverse (une carte cachée en main).</summary>
        public static Synth MinimalGame()
        {
            var s = new Synth();
            s.PowerList()
             .Power("CREATE_GAME")
             .Power("GameEntity EntityID=1", 4).Power("tag=CARDTYPE value=GAME", 8).Power("tag=ZONE value=PLAY", 8).Power("tag=GAME_SEED value=42", 8)
             .Power("Player EntityID=2 PlayerID=1 GameAccountId=[hi=0 lo=0]", 4).Power("tag=CONTROLLER value=1", 8).Power("tag=CARDTYPE value=PLAYER", 8).Power("tag=HERO_ENTITY value=10", 8).Power("tag=FIRST_PLAYER value=1", 8)
             .Power("Player EntityID=3 PlayerID=2 GameAccountId=[hi=0 lo=0]", 4).Power("tag=CONTROLLER value=2", 8).Power("tag=CARDTYPE value=PLAYER", 8).Power("tag=HERO_ENTITY value=11", 8)
             .Power("FULL_ENTITY - Creating ID=10 CardID=HERO_01", 4).Power("tag=CONTROLLER value=1", 8).Power("tag=CARDTYPE value=HERO", 8).Power("tag=ZONE value=PLAY", 8).Power("tag=HEALTH value=30", 8)
             .Power("FULL_ENTITY - Creating ID=11 CardID=HERO_08", 4).Power("tag=CONTROLLER value=2", 8).Power("tag=CARDTYPE value=HERO", 8).Power("tag=ZONE value=PLAY", 8).Power("tag=HEALTH value=30", 8)
             .Power("FULL_ENTITY - Creating ID=20 CardID=END_033", 4).Power("tag=CONTROLLER value=1", 8).Power("tag=ZONE value=HAND", 8).Power("tag=CARDTYPE value=MINION", 8).Power("tag=COST value=7", 8).Power("tag=ZONE_POSITION value=1", 8)
             .Power("FULL_ENTITY - Creating ID=30 CardID=", 4).Power("tag=CONTROLLER value=2", 8).Power("tag=ZONE value=HAND", 8).Power("tag=ZONE_POSITION value=1", 8)
             .Game("PlayerID=1, PlayerName=Moi#1234")
             .Game("PlayerID=2, PlayerName=UNKNOWN HUMAN PLAYER")
             .PowerList()
             .Power("TAG_CHANGE Entity=GameEntity tag=STEP value=BEGIN_MULLIGAN");
            return s;
        }
    }

    private static CardDatabase SecretCards() => new(new[]
    {
        new CardInfo { Id = "TEST_SECRET", Name = "Secret de test", Type = "SPELL", Cost = 2, Mechanics = new() { "SECRET" } },
        new CardInfo { Id = "TEST_QUEST", Name = "Quête de test", Type = "SPELL", Cost = 1, Mechanics = new() { "QUEST" } },
        new CardInfo { Id = "END_033", Name = "Glissedrake visionnaire", Type = "MINION", Cost = 7 },
    }, "test", "frFR", "test");

    private static (GameEngine, GameStateSnapshot) Run(IEnumerable<string> lines, ICardDatabase? cards = null)
    {
        cards ??= SecretCards();
        var engine = new GameEngine(new EngineOptions { Cards = cards });
        foreach (var l in lines) engine.Process(l);
        return (engine, new GameStateProjector(cards).Project(engine));
    }

    [Fact]
    public void A_card_transformed_while_revealed_counts_as_seen_and_a_hidden_one_stays_hidden()
    {
        // Partie classée du 25/10/2026 à 21:31:38 : pendant une Invocation, le serveur montre Galakrond dans le deck adverse
        // (SHOW_ENTITY, REVEALED=1), l'améliore (CHANGE_ENTITY DRG_660t2) puis le cache (HIDE_ENTITY, REVEALED=0).
        // L'amélioration est affichée à l'écran : ni fuite, ni violation. Le contrôle signalait 28 violations.
        const string Hidden = "[entityName=UNKNOWN ENTITY [cardType=INVALID] id=40 zone=DECK zonePos=0 cardId= player=2]";
        var cards = new CardDatabase(new[]
        {
            new CardInfo { Id = "END_033", Name = "Glissedrake visionnaire", Type = "MINION", Cost = 7 },
            new CardInfo { Id = "DRG_660", Name = "Galakrond, l’Indicible", Type = "HERO", Cost = 7 },
            new CardInfo { Id = "DRG_660t2", Name = "Galakrond, l’Apocalypse", Type = "HERO", Cost = 7 },
            new CardInfo { Id = "TEST_HIDDEN", Name = "Carte cachée", Type = "SPELL", Cost = 1 },
        }, "test", "frFR", "test");
        var synth = Synth.MinimalGame()
            .Power("FULL_ENTITY - Creating ID=40 CardID=").Power("tag=CONTROLLER value=2", 4).Power("tag=ZONE value=DECK", 4)
            .Power("FULL_ENTITY - Creating ID=41 CardID=").Power("tag=CONTROLLER value=2", 4).Power("tag=ZONE value=DECK", 4)
            .PowerList()
            .Power("SHOW_ENTITY - Updating Entity=" + Hidden + " CardID=DRG_660").Power("tag=ZONE value=DECK", 4).Power("tag=REVEALED value=1", 4)
            .Power("CHANGE_ENTITY - Updating Entity=" + Hidden + " CardID=DRG_660t2").Power("tag=ZONE value=DECK", 4)
            .Power("HIDE_ENTITY - Entity=" + Hidden + " tag=ZONE value=DECK")
            .Power("TAG_CHANGE Entity=" + Hidden + " tag=REVEALED value=0")
            // Transformation d'une carte jamais révélée : elle doit rester cachée.
            .Power("CHANGE_ENTITY - Updating Entity=[entityName=UNKNOWN ENTITY [cardType=INVALID] id=41 zone=DECK zonePos=0 cardId= player=2] CardID=TEST_HIDDEN")
            .Power("tag=ZONE value=DECK", 4)
            .PowerList();
        var report = VisibilityAudit.Run(synth.Lines, cards);
        Assert.Equal(0, report.Violations);
        var (_, s) = Run(synth.Lines, cards);
        Assert.Contains(s.Opponent!.Deck.KnownAdded, c => c.EntityId == 40 && c.CardId == "DRG_660t2");
        Assert.DoesNotContain(s.Opponent.Deck.KnownAdded, c => c.EntityId == 41);
        Assert.DoesNotContain("TEST_HIDDEN", JsonSerializer.Serialize(s));
    }

    [Fact]
    public void A_discovered_minion_with_0_attack_is_offered_as_0_x()
    {
        // Partie classée du 25/10/2026 à 19:31:21 : Avocat commis d'office 0/7 proposé par un Sombre don. Le serveur n'envoie pas
        // ATK quand elle vaut 0 et la base n'a pas d'attaque pour les serviteurs à 0 ATQ : l'offre partait sans attaque (« /7 »).
        var cards = new CardDatabase(new[]
        {
            new CardInfo { Id = "END_033", Name = "Glissedrake visionnaire", Type = "MINION", Cost = 7 },
            new CardInfo { Id = "CFM_300", Name = "Avocat commis d’office", Type = "MINION", Cost = 2, Health = 7 },
            new CardInfo { Id = "VAC_341", Name = "Calamar pas cuit", Type = "MINION", Cost = 4, Attack = 3, Health = 4 },
        }, "test", "frFR", "test");
        var synth = Synth.MinimalGame()
            .Power("FULL_ENTITY - Creating ID=130 CardID=VAC_341").Power("tag=CONTROLLER value=1", 4).Power("tag=CARDTYPE value=MINION", 4)
            .Power("tag=COST value=4", 4).Power("tag=ATK value=3", 4).Power("tag=HEALTH value=4", 4).Power("tag=ZONE value=SETASIDE", 4)
            .Power("FULL_ENTITY - Creating ID=131 CardID=CFM_300").Power("tag=CONTROLLER value=1", 4).Power("tag=CARDTYPE value=MINION", 4)
            .Power("tag=COST value=2", 4).Power("tag=HEALTH value=7", 4).Power("tag=ZONE value=SETASIDE", 4)
            .EntityChoices("id=6 Player=Moi#1234 TaskList=384 ChoiceType=GENERAL CountMin=1 CountMax=1")
            .EntityChoices("Source=[entityName=Glissedrake visionnaire id=20 zone=HAND zonePos=1 cardId=END_033 player=1]", 2)
            .EntityChoices("Entities[0]=[entityName=Calamar pas cuit id=130 zone=SETASIDE zonePos=0 cardId=VAC_341 player=1]", 2)
            .EntityChoices("Entities[1]=[entityName=Avocat commis d’office id=131 zone=SETASIDE zonePos=0 cardId=CFM_300 player=1]", 2)
            .PowerList();
        var (_, s) = Run(synth.Lines, cards);
        var offered = s.PendingChoice!.Offered;
        Assert.Equal((3, 4), (offered[0].Attack, offered[0].Health));
        Assert.Equal((0, 7), (offered[1].Attack, offered[1].Health));
    }

    [Fact]
    public void Identifies_local_player_from_visible_hand_without_leaking_opponent()
    {
        var (engine, s) = Run(Synth.MinimalGame().Lines);
        Assert.Equal(1, engine.LocalPlayerId);
        Assert.Equal(LocalPlayerEvidence.HandHeuristic, engine.LocalEvidence);
        Assert.Equal("END_033", Assert.Single(s.Local!.Hand.Cards).CardId);
        Assert.Null(Assert.Single(s.Opponent!.Hand.Cards).CardId);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "LOCAL_PLAYER_HEURISTIC");
    }

    [Fact]
    public void Opponent_secret_stays_hidden_even_if_the_log_contains_its_id_until_revealed()
    {
        var synth = Synth.MinimalGame()
            .PowerList()
            .Power("SHOW_ENTITY - Updating Entity=30 CardID=TEST_SECRET").Power("tag=CONTROLLER value=2", 4).Power("tag=ZONE value=HAND", 4)
            .Power("TAG_CHANGE Entity=30 tag=ZONE value=SECRET")
            .Power("TAG_CHANGE Entity=30 tag=CLASS value=MAGE");
        var (_, hidden) = Run(synth.Lines);
        var secret = Assert.Single(hidden.Opponent!.Secrets);
        Assert.Null(secret.CardId);
        Assert.False(secret.Known);
        Assert.Equal("MAGE", secret.CardClass);
        Assert.DoesNotContain(hidden.History, h => h.SourceCardId == "TEST_SECRET");

        synth.PowerList().Power("TAG_CHANGE Entity=30 tag=ZONE value=GRAVEYARD");
        var (_, revealed) = Run(synth.Lines);
        Assert.Empty(revealed.Opponent!.Secrets);
        Assert.Contains(revealed.Opponent.Graveyard, c => c.CardId == "TEST_SECRET");
        Assert.Contains(revealed.Opponent.RevealedCards, c => c.CardId == "TEST_SECRET");
    }

    [Fact]
    public void Opponent_quest_in_secret_zone_is_public()
    {
        var synth = Synth.MinimalGame()
            .PowerList()
            .Power("SHOW_ENTITY - Updating Entity=30 CardID=TEST_QUEST").Power("tag=CONTROLLER value=2", 4).Power("tag=ZONE value=SECRET", 4);
        var (_, s) = Run(synth.Lines);
        Assert.Empty(s.Opponent!.Secrets);
        Assert.Equal("TEST_QUEST", Assert.Single(s.Opponent.PublicSecretZone).CardId);
    }

    [Fact]
    public void Card_ids_resent_in_a_reset_block_do_not_leak_opponent_hand()
    {
        var synth = Synth.MinimalGame()
            .PowerList()
            .Power("BLOCK_START BlockType=GAME_RESET Entity=20 EffectCardId=System.Collections.Generic.List`1[System.String] EffectIndex=-1 Target=0")
            .Power("RESET_GAME", 4)
            .Power("FULL_ENTITY - Updating [entityName=UNKNOWN ENTITY [cardType=INVALID] id=30 zone=HAND zonePos=1 cardId= player=2] CardID=TEST_SECRET", 4)
            .Power("tag=CONTROLLER value=2", 8).Power("tag=ZONE value=HAND", 8).Power("tag=ZONE_POSITION value=1", 8)
            .Power("BLOCK_END");
        var (engine, s) = Run(synth.Lines);
        Assert.Equal("TEST_SECRET", engine.Entities[30].CardId); // le moteur sait…
        Assert.Null(Assert.Single(s.Opponent!.Hand.Cards).CardId); // …mais rien ne sort.
        Assert.Equal(1, s.Game.ResetCount);
    }

    [Fact]
    public void Unknown_local_player_exposes_no_card_and_marks_state_incomplete()
    {
        var lines = Synth.MinimalGame().Lines.Select(l => l.Replace("CardID=END_033", "CardID=", StringComparison.Ordinal)).ToList();
        var (engine, s) = Run(lines);
        Assert.Null(engine.LocalPlayerId);
        Assert.True(s.Uncertainty.Incomplete);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "LOCAL_PLAYER_UNKNOWN");
        Assert.Null(s.Local);
        Assert.All(s.Opponent?.Hand.Cards ?? new(), c => Assert.Null(c.CardId));
    }

    [Fact]
    public void Server_options_confirm_local_player()
    {
        var synth = Synth.MinimalGame()
            .PowerList().Power("TAG_CHANGE Entity=2 tag=CURRENT_PLAYER value=1").Power("TAG_CHANGE Entity=GameEntity tag=TURN value=1")
            .Options("id=1")
            .Options("option 0 type=END_TURN mainEntity= error=INVALID errorParam=", 2)
            .Options("option 1 type=POWER mainEntity=[entityName=Glissedrake visionnaire id=20 zone=HAND zonePos=1 cardId=END_033 player=1] error=REQ_ENOUGH_MANA errorParam=", 2)
            .Options("option 2 type=POWER mainEntity=[entityName=Garrosh Hurlenfer id=10 zone=PLAY zonePos=0 cardId=HERO_01 player=1] error=NONE errorParam=", 2);
        var (engine, s) = Run(synth.Lines);
        Assert.Equal(LocalPlayerEvidence.ServerOptions, engine.LocalEvidence);
        Assert.True(s.LegalOptions!.Fresh);
        Assert.True(s.LegalOptions.CanEndTurn);
        Assert.Single(s.LegalOptions.Playable);

        // Une modification d'état après les options les rend périmées.
        synth.PowerList().Power("TAG_CHANGE Entity=10 tag=DAMAGE value=3");
        var (_, after) = Run(synth.Lines);
        Assert.False(after.LegalOptions!.Fresh);
        Assert.True(after.Revision > s.Revision);
        Assert.Contains(after.Uncertainty.Flags, f => f.Code == "OPTIONS_STALE");
    }

    [Fact]
    public void Joining_mid_game_is_flagged_and_does_not_crash()
    {
        var lines = TestData.Lines(TestData.PriestGame).Skip(1600).ToList();
        var engine = TestData.NewEngine();
        foreach (var l in lines) engine.Process(l);
        var s = TestData.Project(engine);
        Assert.True(engine.JoinedMidGame);
        Assert.True(s.Uncertainty.Incomplete);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "JOINED_MID_GAME");
        Assert.DoesNotContain(engine.Warnings, w => w.Code == "PARSER_EXCEPTION");
        Assert.NotNull(engine.LocalPlayerId); // retrouvé grâce aux choix/options envoyés
    }

    [Fact]
    public void Entity_referenced_before_creation_is_merged()
    {
        var synth = Synth.MinimalGame()
            .PowerList()
            .Power("TAG_CHANGE Entity=40 tag=ATK value=5")
            .Power("FULL_ENTITY - Creating ID=40 CardID=END_033").Power("tag=CONTROLLER value=1", 4).Power("tag=ZONE value=HAND", 4).Power("tag=ZONE_POSITION value=2", 4);
        var (engine, s) = Run(synth.Lines);
        Assert.False(engine.Entities[40].Placeholder);
        Assert.Equal(5, engine.Entities[40].GetInt("ATK"));
        Assert.Equal(2, s.Local!.Hand.Count);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "UNKNOWN_ENTITY_REFERENCED");
    }

    [Fact]
    public void Block_end_without_start_is_reported_not_fatal()
    {
        var synth = Synth.MinimalGame().PowerList().Power("BLOCK_END").Power("TAG_CHANGE Entity=10 tag=ARMOR value=2");
        var (engine, s) = Run(synth.Lines);
        Assert.Equal(2, s.Local!.Hero!.Armor);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "BLOCK_END_WITHOUT_START");
        Assert.Equal(0, engine.BlockDepth);
    }

    [Fact]
    public void Non_gamestate_and_garbage_lines_are_ignored()
    {
        var engine = new GameEngine();
        Assert.False(engine.Process("D 23:07:08.5306971 PowerTaskList.DebugPrintPower() -     TAG_CHANGE Entity=1 tag=TURN value=2"));
        Assert.False(engine.Process("garbage"));
        Assert.True(engine.Process("D 10:00:00.0000000 GameState.DebugPrintPower() - SOMETHING_NEW Entity=1"));
        Assert.Equal(1, engine.UnparsedCount);
        Assert.Contains(engine.Warnings, w => w.Code == "UNPARSED_LINES");
    }

    [Fact]
    public void Re_reading_the_same_game_rebuilds_identical_state_with_higher_revisions()
    {
        // HDT relit la partie depuis le début après un Rembobinage : le plugin renvoie alors les mêmes lignes.
        var lines = TestData.Lines(TestData.PriestGame);
        var engine = TestData.NewEngine();
        foreach (var l in lines) engine.Process(l);
        var first = TestData.Project(engine);
        foreach (var l in lines) engine.Process(l);
        var second = TestData.Project(engine);
        Assert.Equal(first.GameId, second.GameId);
        Assert.True(second.Revision > first.Revision);
        Assert.Equal(2, engine.GameNumber);
        Assert.Equal(first.Local!.Hand.Cards.Select(c => c.EntityId), second.Local!.Hand.Cards.Select(c => c.EntityId));
        Assert.Equal(first.Opponent!.Hero!.Health, second.Opponent!.Hero!.Health);
        Assert.Equal(first.History.Count, second.History.Count);
    }

    [Theory]
    [InlineData(1, 0.01)]
    [InlineData(2, 0.05)]
    [InlineData(3, 0.20)]
    public void Random_line_loss_never_breaks_the_engine(int seed, double dropRate)
    {
        var rng = new Random(seed);
        var lines = TestData.Lines(TestData.RogueGame).Where(_ => rng.NextDouble() >= dropRate).ToList();
        var engine = TestData.NewEngine();
        var projector = new GameStateProjector(TestData.Cards);
        for (int i = 0; i < lines.Count; i++)
        {
            engine.Process(lines[i]);
            if (i % 500 == 0) projector.Project(engine);
        }
        Assert.DoesNotContain(engine.Warnings, w => w.Code == "PARSER_EXCEPTION");
        Assert.NotNull(projector.Project(engine));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void Shuffled_neighbouring_lines_never_break_the_engine(int seed)
    {
        var rng = new Random(seed);
        var lines = TestData.Lines(TestData.RewindGame).ToList();
        for (int i = 1; i < lines.Count; i++)
            if (rng.NextDouble() < 0.02) (lines[i - 1], lines[i]) = (lines[i], lines[i - 1]);
        var engine = TestData.NewEngine();
        foreach (var l in lines) engine.Process(l);
        Assert.DoesNotContain(engine.Warnings, w => w.Code == "PARSER_EXCEPTION");
        Assert.NotNull(TestData.Project(engine));
    }

    [Fact]
    public void Revisions_are_deterministic_for_the_same_input()
    {
        var a = TestData.Replay(TestData.RogueGame);
        var b = TestData.Replay(TestData.RogueGame);
        Assert.Equal(a.Revision, b.Revision);
        Assert.Equal(a.GameId, b.GameId);
    }
}
