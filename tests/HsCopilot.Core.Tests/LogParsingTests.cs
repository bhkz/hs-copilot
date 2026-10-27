using HsCopilot.Core.Logs;

namespace HsCopilot.Core.Tests;

public class LogParsingTests
{
    [Fact]
    public void Parses_gamestate_line_with_indentation()
    {
        var l = PowerLogLineParser.TryParse("D 11:10:18.2348071 GameState.DebugPrintPower() -     TAG_CHANGE Entity=38 tag=COST value=4");
        Assert.NotNull(l);
        Assert.Equal("DebugPrintPower", l!.Method);
        Assert.Equal(4, l.Indent);
        Assert.Equal("TAG_CHANGE Entity=38 tag=COST value=4", l.Body);
        Assert.Equal(new TimeSpan(0, 11, 10, 18, 234) + TimeSpan.FromTicks(8071), l.Time);
    }

    [Theory]
    [InlineData("D 23:07:08.5306971 PowerTaskList.DebugPrintPower() -     TAG_CHANGE Entity=1 tag=TURN value=2")]
    [InlineData("D 23:07:08.6306870 PowerProcessor.EndCurrentTaskList() - m_currentTaskList=244")]
    [InlineData("n'importe quoi")]
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_non_gamestate_lines(string? raw) => Assert.Null(PowerLogLineParser.TryParse(raw));

    [Fact]
    public void Parses_bracketed_ref_with_nested_brackets()
    {
        var r = EntityRef.Parse("[entityName=UNKNOWN ENTITY [cardType=INVALID] id=12 zone=DECK zonePos=0 cardId= player=1]");
        Assert.Equal(12, r.Id);
        Assert.Equal("DECK", r.PrintedZone);
        Assert.Equal("", r.PrintedCardId);
        Assert.Equal(1, r.PrintedPlayer);
        Assert.Equal("UNKNOWN ENTITY [cardType=INVALID]", r.PrintedName);
    }

    [Theory]
    [InlineData("GameEntity", null, null, true)]
    [InlineData("38", 38, null, false)]
    [InlineData("UNKNOWN HUMAN PLAYER", null, "UNKNOWN HUMAN PLAYER", false)]
    [InlineData("Joueur1#1001", null, "Joueur1#1001", false)]
    public void Parses_simple_refs(string text, int? id, string? name, bool game)
    {
        var r = EntityRef.Parse(text);
        Assert.Equal(id, r.Id);
        Assert.Equal(name, r.PlayerName);
        Assert.Equal(game, r.IsGameEntity);
    }

    [Fact]
    public void Parses_tag_change_with_player_name_containing_spaces()
    {
        Assert.True(PowerStatements.TryTagChange("TAG_CHANGE Entity=UNKNOWN HUMAN PLAYER tag=MULLIGAN_STATE value=INPUT", out var tc));
        Assert.Equal("UNKNOWN HUMAN PLAYER", tc.Entity.PlayerName);
        Assert.Equal("MULLIGAN_STATE", tc.Tag);
        Assert.Equal("INPUT", tc.Value);
    }

    [Fact]
    public void Parses_def_change_suffix()
    {
        Assert.True(PowerStatements.TryTagChange("TAG_CHANGE Entity=104 tag=TAG_SCRIPT_DATA_NUM_1 value=31 DEF CHANGE", out var tc));
        Assert.Equal("31", tc.Value);
        Assert.True(tc.DefChange);
    }

    [Fact]
    public void Parses_block_start_without_suboption()
    {
        Assert.True(PowerStatements.TryBlockStart(
            "BLOCK_START BlockType=GAME_RESET Entity=[entityName=Avant-garde du portail id=31 zone=PLAY zonePos=2 cardId=TIME_003 player=1] EffectCardId=System.Collections.Generic.List`1[System.String] EffectIndex=-1 Target=0",
            out var bs));
        Assert.Equal("GAME_RESET", bs.BlockType);
        Assert.Equal(31, bs.Entity.Id);
        Assert.True(bs.Target.IsEmpty);
        Assert.Equal(-1, bs.EffectIndex);
    }

    [Fact]
    public void Parses_block_start_with_target_and_trigger_keyword()
    {
        Assert.True(PowerStatements.TryBlockStart(
            "BLOCK_START BlockType=TRIGGER Entity=[entityName=Dissipation des ténèbres id=91 zone=SECRET zonePos=0 cardId=TLC_817t player=1] EffectCardId=System.Collections.Generic.List`1[System.String] EffectIndex=-1 Target=[entityName=X id=7 zone=PLAY zonePos=1 cardId=Y player=2] SubOption=-1 TriggerKeyword=4297",
            out var bs));
        Assert.Equal("TRIGGER", bs.BlockType);
        Assert.Equal(91, bs.Entity.Id);
        Assert.Equal(7, bs.Target.Id);
        Assert.Equal("4297", bs.TriggerKeyword);
    }

    [Fact]
    public void Parses_entity_headers()
    {
        Assert.True(PowerStatements.TryFullEntityCreating("FULL_ENTITY - Creating ID=161 CardID=TIME_003e", out var id, out var card));
        Assert.Equal(161, id);
        Assert.Equal("TIME_003e", card);
        Assert.True(PowerStatements.TryUpdatingHeader("FULL_ENTITY - Updating Joueur1 CardID=", "FULL_ENTITY - Updating ", out var fu));
        Assert.Equal("Joueur1", fu.Entity.PlayerName);
        Assert.Equal("", fu.CardId);
        Assert.True(PowerStatements.TryHideEntity("HIDE_ENTITY - Entity=[entityName=Forgeron dimensionnel id=36 zone=HAND zonePos=2 cardId=END_021 player=2] tag=ZONE value=DECK", out var he));
        Assert.Equal(36, he.Entity.Id);
        Assert.Equal("DECK", he.Value);
    }

    [Fact]
    public void Reads_fields_with_spaces_in_player_names()
    {
        const string body = "id=1 Player=UNKNOWN HUMAN PLAYER TaskList=7 ChoiceType=MULLIGAN CountMin=0 CountMax=5";
        Assert.Equal("UNKNOWN HUMAN PLAYER", PowerStatements.ReadField(body, "Player", "TaskList", "ChoiceType", "CountMin", "CountMax"));
        Assert.Equal("MULLIGAN", PowerStatements.ReadField(body, "ChoiceType"));
        Assert.Equal("5", PowerStatements.ReadField(body, "CountMax"));
    }
}
