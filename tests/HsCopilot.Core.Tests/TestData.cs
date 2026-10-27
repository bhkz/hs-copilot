using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;

namespace HsCopilot.Core.Tests;

/// <summary>Accès aux fixtures (parties réelles anonymisées) et à la base de cartes réduite.</summary>
public static class TestData
{
    public const string PriestGame = "ranked-warrior-vs-priest.log";
    public const string RogueGame = "ranked-warrior-vs-rogue.log";
    public const string RewindGame = "ai-warrior-vs-hunter-rewind.log";
    /// <summary>Partie contre l'IA : Grommash (Forgeron) + Profondeurs Sanguines, 14 puis 18 dégâts.</summary>
    public const string GrommashGame = "ai-grommash-depths.log";
    /// <summary>Partie contre l'IA : Voyante blessée, Frappe anticipée à 5 et Suffusion à 4, un Rembobinage.</summary>
    public const string SeerGame = "ai-seer-spelldamage.log";

    private static readonly Lazy<CardDatabase> LazyCards = new(() =>
        HearthstoneJsonLoader.Load(Path.Combine(FixtureDir, "cards.subset.json")));

    private static readonly Dictionary<string, List<string>> Cache = new();

    public static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static CardDatabase Cards => LazyCards.Value;

    public static IReadOnlyList<string> Lines(string fixture)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(fixture, out var lines))
                Cache[fixture] = lines = ReplayFile.ReadAllLines(Path.Combine(FixtureDir, fixture));
            return lines;
        }
    }

    /// <summary>Liste « Guerrier dragons » telle qu'enregistrée dans HDT (PlayerDecks.xml), identique à config/copilot.example.json.</summary>
    public static Decklist DragonWarrior { get; } = new()
    {
        Source = "test",
        Name = "Guerrier dragons",
        Cards = new Dictionary<string, int>
        {
            ["CORE_REV_990"] = 2, ["EDR_456"] = 2, ["END_022"] = 2, ["EDR_889"] = 2, ["TIME_750"] = 2, ["EDR_457"] = 2,
            ["CORE_EX1_189"] = 1, ["FIR_939"] = 2, ["TIME_003"] = 2, ["TIME_045"] = 2, ["END_021"] = 2, ["TIME_034"] = 2,
            ["TIME_714"] = 1, ["END_033"] = 2, ["CORE_EX1_414"] = 1, ["DINO_401"] = 1, ["TLC_600"] = 2,
        },
    };

    public static GameEngine NewEngine() => new(new EngineOptions { Cards = Cards });

    public static GameEngine Replay(string fixture)
    {
        var engine = NewEngine();
        foreach (var l in Lines(fixture)) engine.Process(l);
        return engine;
    }

    public static (GameEngine Engine, GameStateSnapshot Snapshot) AtLocalTurn(string fixture, int playerTurn)
    {
        var engine = NewEngine();
        var next = ReplayRunner.RunUntilLocalTurn(engine, Lines(fixture), playerTurn);
        Assert.True(next >= 0, $"tour {playerTurn} non atteint dans {fixture}");
        return (engine, Project(engine));
    }

    public static GameStateSnapshot Project(GameEngine engine, Decklist? deck = null) =>
        new GameStateProjector(Cards).Project(engine, new ProjectionContext { Transport = "test", Decklist = deck ?? DragonWarrior });
}
