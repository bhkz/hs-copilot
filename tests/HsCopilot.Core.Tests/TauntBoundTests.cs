using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Partie classée du 26/10/2026 à 0 h 09 (anonymisée), tour 9 : le roi-liche à 15 PV derrière cinq Provocations (quatre
/// Fantassins ressuscités 1/3 et une Commandante de la garde d'os 10/10). Le calcul local affichait « Létal plausible : 20 dégâts
/// si la Provocation tombe, contre 15 PV », alors qu'il faut 22 dégâts rien que pour retirer les Provocations.
/// </summary>
public class TauntBoundTests
{
    private const string Fixture = "ranked-warrior-vs-deathknight-taunts.log";

    /// <summary>État de la décision du tour 9 : juste après le bloc d'options n° 70 du serveur.</summary>
    private static GameStateSnapshot Turn9()
    {
        var engine = TestData.NewEngine();
        var inOptions = false;
        foreach (var raw in TestData.Lines(Fixture))
        {
            var p = PowerLogLineParser.TryParse(raw);
            if (p is null) continue;
            if (p.Method == "DebugPrintOptions" && p.Body == "id=70") inOptions = true;
            else if (inOptions && p.Method != "DebugPrintOptions") break;
            engine.Process(p);
        }
        return TestData.Project(engine);
    }

    private static DecisionContext Ctx(GameStateSnapshot s) => DecisionContextBuilder.Build(s)!;

    /// <summary>L'Animatrice de stade en main (arme aléatoire) est retirée : sans elle, rien ne peut dégager les Provocations autrement.</summary>
    private static GameStateSnapshot Turn9WithoutRandomCards()
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Turn9(), Json.Compact))!.AsObject();
        var cards = node["local"]!["hand"]!["cards"]!.AsArray();
        foreach (var c in cards.Where(c => c!["name"]?.GetValue<string>() == "Animatrice de stade").ToList()) cards.Remove(c);
        return node.Deserialize<GameStateSnapshot>(Json.Read)!;
    }

    [Fact]
    public void The_real_turn_keeps_plausible_because_of_a_random_weapon_but_states_the_taunt_health_to_remove()
    {
        // Animatrice de stade (« les deux adversaires s'équipent d'une arme aléatoire ») : effet aléatoire, prudence.
        var s = Turn9();
        Assert.Contains(s.Local!.Hand.Cards, c => c.Name == "Animatrice de stade");
        var result = TurnSolver.Solve(Ctx(s));
        Assert.Equal(LethalLevels.Plausible, result.Level);
        Assert.Contains(result.Conditions, c => c.Contains("les 5 Provocations (22 PV au total)"));
        var facts = QuickAnalysis.Analyze(Ctx(s)).Select(f => f.Text).ToList();
        Assert.Contains(facts, f => f.StartsWith("Létal plausible, non démontré : 20 dégâts si les Provocations tombent, contre 15 PV") && f.Contains("22 PV au total"));
    }

    [Fact]
    public void A_lethal_behind_taunts_that_cost_more_than_the_available_damage_is_not_called_plausible()
    {
        var s = Turn9WithoutRandomCards();
        Assert.Equal(15, s.Opponent!.Hero!.Health);
        Assert.Equal(5, s.Opponent.Board.Count(m => m.Keywords.Contains("TAUNT")));
        var result = TurnSolver.Solve(Ctx(s));
        Assert.Equal(LethalLevels.None, result.Level);
        var shot = Assert.Single(result.LongShots, x => x.Contains("Provocations"));
        Assert.Contains("il en faudrait 37 (15 pour le héros, 22 pour retirer les Provocations)", shot);
        var facts = QuickAnalysis.Analyze(Ctx(s)).Select(f => f.Text).ToList();
        Assert.DoesNotContain(facts, f => f.StartsWith("Létal plausible"));
        Assert.Contains(facts, f => f.StartsWith("Peu probable : létal derrière les 5 Provocations"));
    }

    [Theory]
    [InlineData("Détruit un serviteur adverse.")]
    // Le Grand Dracorex, partie classée du 25/10/2026 à 23 h 26 : deux Provocations retirées d'une attaque, létal dans le tour.
    [InlineData("Ruée. Une fois que ce serviteur a attaqué un serviteur adverse, il inflige des dégâts à TOUS les autres serviteurs adverses.")]
    public void With_a_way_to_clear_taunts_other_than_point_by_point_the_lethal_stays_plausible(string text)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(Turn9WithoutRandomCards(), Json.Compact))!.AsObject();
        node["local"]!["hand"]!["cards"]!.AsArray()[0]!["text"] = text;
        var changed = node.Deserialize<GameStateSnapshot>(Json.Read)!;
        Assert.Equal(LethalLevels.Plausible, TurnSolver.Solve(Ctx(changed)).Level);
    }
}
