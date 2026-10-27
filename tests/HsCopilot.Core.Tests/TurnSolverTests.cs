using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Solveur des enchaînements, sur les situations exactes de la deuxième vraie partie (25/10/2026, anonymisée) :
/// Profondeurs Sanguines avant l'attaque du Wyrm au tour 7, létal conditionnel puis certain au tour 8.
/// </summary>
public class TurnSolverTests
{
    private const string Fixture = "ai-depths-wyrm-lethal.log";

    private static readonly Lazy<List<GameStateSnapshot>> Decisions = new(() =>
    {
        var game = ReplayFile.SplitGames(TestData.Lines(Fixture)).Last();
        return DatasetBuilder.BuildGame(Fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 12, Decklist = TestData.DragonWarrior })
            .Select(c => c.LoadSnapshot()).ToList();
    });

    private static DecisionContext Ctx(GameStateSnapshot s) => DecisionContextBuilder.Build(s)!;

    /// <summary>Tour 7, après avoir posé Profondeurs : Wyrm 8/5 prêt, lieu activable sur lui, adversaire à 18 PV.</summary>
    private static GameStateSnapshot Turn7AfterDepths() => Decisions.Value.First(s =>
        s.Game.TurnNumber == 7 && Ctx(s).LegalActions.Any(a => a.Kind == "location" && a.TargetName == "Wyrm du pic venteux"));

    private static GameStateSnapshot Turn8Start() => Decisions.Value.First(s => s.Game.TurnNumber == 8);

    /// <summary>Tour 8, arme 3/3 équipée, Glissedrake a déjà frappé : adversaire à 5 PV.</summary>
    private static GameStateSnapshot Turn8Lethal() => Decisions.Value.First(s =>
        s.Game.TurnNumber == 8 && s.Local!.Weapon is not null && s.Opponent!.Hero!.Health == 5);

    [Fact]
    public void Turn7_buffing_the_wyrm_before_it_attacks_is_found()
    {
        var s = Turn7AfterDepths();
        Assert.Equal(18, s.Opponent!.Hero!.Health + s.Opponent.Hero.Armor);
        var r = TurnSolver.Solve(Ctx(s));
        Assert.Equal(8, r.AttacksOnlyDamage);
        Assert.Equal(12, r.MaxFaceDamage);                     // Profondeurs (+2) puis Wyrm 10, et Suffusion 2
        Assert.Equal(LethalLevels.None, r.Level);                // Animatrice : il faudrait une arme d'au moins 6 ATQ, trop rare
        Assert.Contains(r.LongShots, x => x.Contains("au moins 6 ATQ"));
        Assert.NotNull(r.Sequence[0].Action);                  // première étape jouable tout de suite
        var activate = r.Sequence.FindIndex(x => x.Kind == "location" && x.TargetEntityId == r.Sequence.Last(y => y.Label.Contains("Wyrm")).SourceEntityId);
        Assert.True(activate >= 0 && activate < r.Sequence.FindIndex(x => x.Kind == "attack"));
        Assert.NotNull(r.Sequence[activate].Action);           // Profondeurs déjà en jeu : activable dans cet état
        Assert.Contains(r.OrderingHints, h => h.Contains("Wyrm du pic venteux"));
    }

    [Fact]
    public void Turn7_start_plans_to_play_and_activate_depths_before_the_attack()
    {
        var s = Decisions.Value.First(x => x.Game.TurnNumber == 7);
        var r = TurnSolver.Solve(Ctx(s));
        Assert.Equal(12, r.MaxFaceDamage);
        Assert.Contains(r.Sequence, x => x.Label.StartsWith("Jouer Profondeurs Sanguines"));
        var activate = r.Sequence.FindIndex(x => x.Kind == "location");
        var attack = r.Sequence.FindIndex(x => x.Kind == "attack" && x.Label.Contains("Wyrm"));
        Assert.True(activate >= 0 && attack > activate);
    }

    [Fact]
    public void Turn8_start_is_only_a_conditional_lethal_through_the_random_weapon()
    {
        var s = Turn8Start();
        Assert.Equal(10, s.Opponent!.Hero!.Health + s.Opponent.Hero.Armor);
        var r = TurnSolver.Solve(Ctx(s));
        Assert.Equal(9, r.MaxFaceDamage);                      // épée du Gardien 2, Suffusion 2, Glissedrake 5
        Assert.Equal(LethalLevels.Conditional, r.Level);
        Assert.Contains(r.Conditions, c => c.Contains("au moins 3 ATQ"));
    }

    [Fact]
    public void Turn8_weapon_plus_suffusion_is_a_certain_lethal()
    {
        var s = Turn8Lethal();
        var r = TurnSolver.Solve(Ctx(s));
        Assert.Equal(LethalLevels.Certain, r.Level);
        Assert.True(r.MaxFaceDamage >= 5);
        Assert.Contains(r.Sequence, x => x.Label.Contains("Suffusion"));
        Assert.Contains(r.Sequence, x => x.Label.Contains("ton héros"));
        Assert.All(r.Sequence, x => Assert.NotNull(x.Action)); // toutes les étapes sont des actions légales de cet état
    }

    [Fact]
    public void A_secret_turns_a_certain_lethal_into_a_conditional_one()
    {
        var s = Turn8Lethal();
        var withSecret = SnapshotWithSecret(s);
        var r = TurnSolver.Solve(Ctx(withSecret));
        Assert.Equal(LethalLevels.Conditional, r.Level);
        Assert.Contains(r.Conditions, c => c.Contains("Secret"));
    }

    private static GameStateSnapshot SnapshotWithSecret(GameStateSnapshot s)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(s, HsCopilot.Core.Util.Json.Compact))!.AsObject();
        node["opponent"]!["secrets"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["entityId"] = 999, ["cardClass"] = "WARRIOR", ["known"] = false, ["enteredTurn"] = 1, ["hypotheses"] = new System.Text.Json.Nodes.JsonArray() });
        return System.Text.Json.JsonSerializer.Deserialize<GameStateSnapshot>(node, HsCopilot.Core.Util.Json.Read)!;
    }

    [Fact]
    public void Drake_terrestre_end_of_turn_damage_counts_toward_lethal()
    {
        // Tour 4 : le modèle conseillait Drake terrestre « pour infliger 4 dégâts au héros adverse en fin de tour ».
        var s = Decisions.Value.First(x => x.Game.TurnNumber == 4 && Ctx(x).LegalActions.Any(a => a.Kind == "play" && a.SourceName == "Drake terrestre"));
        var r = TurnSolver.Solve(Ctx(s));
        Assert.Contains(r.Sequence, x => x.Label.StartsWith("Jouer Drake terrestre"));
        var end = Assert.Single(r.Sequence, x => x.Kind == "end-turn");
        Assert.Equal(4, end.FaceDamage);
        Assert.Equal(LethalLevels.None, r.Level);               // 27 PV : loin du létal

        // Même état avec l'adversaire à portée : létal démontré, fin de tour comprise.
        var low = WithOpponentHealth(s, r.MaxFaceDamage);
        var lethal = TurnSolver.Solve(Ctx(low));
        Assert.Equal(LethalLevels.Certain, lethal.Level);
        Assert.Equal("end-turn", lethal.Sequence[^1].Kind);
        Assert.NotNull(lethal.Sequence[^1].Action);
        var under = TurnSolver.Solve(Ctx(WithOpponentHealth(s, r.MaxFaceDamage + 1)));
        Assert.NotEqual(LethalLevels.Certain, under.Level);
    }

    private static GameStateSnapshot WithOpponentHealth(GameStateSnapshot s, int health)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(s, HsCopilot.Core.Util.Json.Compact))!.AsObject();
        node["opponent"]!["hero"]!["health"] = health;
        node["opponent"]!["hero"]!["armor"] = 0;
        return System.Text.Json.JsonSerializer.Deserialize<GameStateSnapshot>(node, HsCopilot.Core.Util.Json.Read)!;
    }

    [Fact]
    public void Each_turn_start_decision_already_contains_the_card_drawn_that_turn()
    {
        // Synchronisation avec la pioche : à chaque premier point de décision d'un tour (après le tour 1), la main contient
        // une carte entrée en main pendant ce tour.
        foreach (var first in Decisions.Value.GroupBy(s => s.Game.GameTurn).Select(g => g.First()).Where(s => s.Game.TurnNumber > 1))
            Assert.Contains(first.Local!.Hand.Cards, c => c.EnteredTurn == first.Game.GameTurn);
    }
}
