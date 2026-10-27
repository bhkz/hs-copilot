using HsCopilot.Core.Cards;

namespace HsCopilot.Core.Tests;

public class CardDbTests
{
    [Theory]
    [InlineData("Inflige $3_|4(point,points) de_dégâts.", "Inflige 3 points de dégâts.")]
    [InlineData("Inflige 1_point de dégâts à un serviteur et lui donne +2_ATQ.", "Inflige 1 point de dégâts à un serviteur et lui donne +2 ATQ.")]
    [InlineData("<b>Cri de guerre_:</b> si vous avez un Dragon en main,& vous équipe d’une épée_2/2.", "Cri de guerre : si vous avez un Dragon en main, vous équipe d’une épée 2/2.")]
    [InlineData("[x]<b>Insaisissable</b> Coûte (3)_|4(cristal,cristaux) de moins.", "Insaisissable Coûte (3) cristaux de moins.")]
    public void Cleans_card_text(string raw, string expected) => Assert.Equal(expected, CardText.Clean(raw));

    [Fact]
    public void Loads_hdt_carddefs_xml_with_french_locale()
    {
        var dir = Directory.CreateTempSubdirectory("hscopilot-carddefs");
        try
        {
            var basePath = Path.Combine(dir.FullName, "CardDefs.base.xml");
            var frPath = Path.Combine(dir.FullName, "CardDefs.frFR.xml");
            File.WriteAllText(basePath, """
                <?xml version="1.0" encoding="utf-8"?>
                <CardDefs build="255130">
                <Entity CardID="END_033" ID="121196" version="2">
                  <Tag enumID="185" name="CARDNAME" type="LocString"><enUS>Prescient Slitherdrake</enUS></Tag>
                  <Tag enumID="48" name="COST" type="Int" value="7"/>
                  <Tag enumID="47" name="ATK" type="Int" value="5"/>
                  <Tag enumID="45" name="HEALTH" type="Int" value="8"/>
                  <Tag enumID="202" name="CARDTYPE" type="Int" value="4"/>
                  <Tag enumID="200" name="CARDRACE" type="Int" value="24"/>
                  <Tag enumID="199" name="CLASS" type="Int" value="12"/>
                  <Tag enumID="321" name="COLLECTIBLE" type="Int" value="1"/>
                  <Tag enumID="9999" name="ELUSIVE" type="Int" value="1"/>
                </Entity>
                <Entity CardID="FAKE_SECRET" ID="1" version="2">
                  <Tag enumID="202" name="CARDTYPE" type="Int" value="5"/>
                  <Tag enumID="219" name="SECRET" type="Int" value="1"/>
                </Entity>
                </CardDefs>
                """);
            File.WriteAllText(frPath, """
                <?xml version="1.0" encoding="utf-8"?>
                <CardDefs build="255130">
                <Entity CardID="END_033" ID="121196" version="2">
                  <Tag enumID="185" name="CARDNAME" type="LocString"><frFR>Glissedrake visionnaire</frFR></Tag>
                  <Tag enumID="184" name="CARDTEXT" type="LocString"><frFR>&lt;b&gt;Insaisissable&lt;/b&gt;</frFR></Tag>
                </Entity>
                </CardDefs>
                """);
            var db = CardDefsXmlLoader.Load(basePath, frPath);
            Assert.Equal("255130", db.Build);
            var c = db.Get("END_033")!;
            Assert.Equal("Glissedrake visionnaire", c.Name);
            Assert.Equal("Prescient Slitherdrake", c.NameEn);
            Assert.Equal(7, c.Cost);
            Assert.Equal("MINION", c.Type);
            Assert.Equal("NEUTRAL", c.CardClass);
            Assert.True(c.HasRace("DRAGON"));
            Assert.True(c.HasMechanic("ELUSIVE"));
            Assert.Equal("Insaisissable", c.PlainText);
            Assert.True(db.Get("FAKE_SECRET")!.IsSecret);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Loads_hearthstonejson_array_and_roundtrips_cache()
    {
        var dir = Directory.CreateTempSubdirectory("hscopilot-hsjson");
        try
        {
            var path = Path.Combine(dir.FullName, "cards.json");
            File.WriteAllText(path, """
                [{"id":"CORE_EX1_414","dbfId":69643,"name":"Grommash Hurlenfer","type":"MINION","cost":8,"attack":4,"health":9,"cardClass":"WARRIOR","mechanics":["CHARGE"],"collectible":true},
                 {"id":"TIME_045","dbfId":120856,"name":"Dragonnet du Vol infini","type":"MINION","cost":3,"attack":1,"health":4,"race":"DRAGON","mechanics":["POISONOUS","REBORN"]}]
                """);
            var db = HearthstoneJsonLoader.Load(path, "test");
            Assert.Equal(2, db.Count);
            Assert.True(db.Get("CORE_EX1_414")!.HasMechanic("CHARGE"));
            Assert.True(db.Get("TIME_045")!.HasRace("DRAGON"));
            var cache = Path.Combine(dir.FullName, "cache.json");
            HearthstoneJsonLoader.SaveCache(db, cache);
            var back = HearthstoneJsonLoader.Load(cache);
            Assert.Equal("test", back.Build);
            Assert.Equal("Grommash Hurlenfer", back.Get("CORE_EX1_414")!.Name);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Fact]
    public void Fixture_card_subset_contains_dragon_warrior_cards()
    {
        foreach (var id in TestData.DragonWarrior.Cards.Keys)
            Assert.NotNull(TestData.Cards.Get(id));
        Assert.Equal("Glissedrake visionnaire", TestData.Cards.Get("END_033")!.Name);
        Assert.Equal(7, TestData.Cards.Get("END_033")!.Cost);
        Assert.Equal("LOCATION", TestData.Cards.Get("CORE_REV_990")!.Type);
    }
}
