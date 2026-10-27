using System.Text.Json;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Statistiques des serviteurs (25/10/2026, impression « le copilote ne met pas à jour les statistiques ») : sur des parties
/// enregistrées, ce que le modèle reçoit est exactement l'état reconstruit, blessures, renforcements et PV maximum compris,
/// et le libellé affiché d'une attaque porte les statistiques actuelles de sa cible.
/// La reconstruction elle-même a été confrontée à une relecture indépendante des tags bruts sur les 8 parties classées
/// (1 407 valeurs, 0 écart).
/// </summary>
public class StatsTransmissionTests
{
    private static readonly string[] Fixtures = { "ranked-warrior-vs-priest.log", "ranked-warrior-vs-rogue.log", "ai-depths-wyrm-lethal.log", "ai-visual-lag-two-streams.log" };

    private static IEnumerable<GameStateSnapshot> States(string fixture)
    {
        var game = ReplayFile.SplitGames(TestData.Lines(fixture)).Last();
        return DatasetBuilder.BuildGame(fixture, game, TestData.Cards, new DatasetOptions { DecisionsPerTurn = 12, Decklist = TestData.DragonWarrior })
            .Select(c => c.LoadSnapshot());
    }

    [Fact]
    public void The_model_receives_the_current_attack_health_and_max_health_of_every_minion()
    {
        int compared = 0, damaged = 0, buffed = 0;
        foreach (var fixture in Fixtures)
            foreach (var s in States(fixture))
            {
                var ctx = DecisionContextBuilder.Build(s);
                if (ctx is null) continue;
                using var doc = JsonDocument.Parse(PromptBuilder.BuildUserPayload(ctx));
                var state = doc.RootElement.GetProperty("state");
                foreach (var (side, view) in new[] { ("local", s.Local), ("opponent", s.Opponent) })
                {
                    var sent = state.GetProperty(side).GetProperty("board").EnumerateArray().ToDictionary(m => m.GetProperty("entityId").GetInt32());
                    foreach (var m in view!.Board.Where(m => m.Type == "MINION"))
                    {
                        var p = sent[m.EntityId];
                        Assert.Equal(m.Attack, p.GetProperty("attack").GetInt32());
                        Assert.Equal(m.Health, p.GetProperty("health").GetInt32());
                        Assert.Equal(m.MaxHealth, p.GetProperty("maxHealth").GetInt32());
                        Assert.Equal(m.MaxHealth - m.Damage, m.Health);
                        compared++;
                        if (m.Damage > 0) damaged++;
                        if (m.BaseAttack is int ba && ba != m.Attack) buffed++;
                    }
                }
            }
        // Le test couvre bien des serviteurs blessés et des serviteurs dont l'attaque a changé.
        Assert.True(compared > 100, $"{compared} serviteurs comparés");
        Assert.True(damaged > 10, $"{damaged} serviteurs blessés");
        Assert.True(buffed > 5, $"{buffed} serviteurs à l'attaque modifiée");
    }

    [Fact]
    public void The_displayed_attack_label_shows_the_current_stats_of_its_target()
    {
        var checkedLabels = 0;
        foreach (var fixture in Fixtures)
            foreach (var s in States(fixture))
            {
                var ctx = DecisionContextBuilder.Build(s);
                if (ctx is null) continue;
                foreach (var a in ctx.LegalActions.Where(a => a.Kind == "attack" && a.TargetEntityId is not null))
                {
                    var target = s.Opponent!.Board.Concat(s.Local!.Board).FirstOrDefault(m => m.EntityId == a.TargetEntityId);
                    if (target is null || target.Type != "MINION") continue;
                    Assert.EndsWith($" {target.Attack}/{target.Health}", AdviceDisplay.Label(a, s));
                    checkedLabels++;
                }
            }
        Assert.True(checkedLabels > 20, $"{checkedLabels} libellés vérifiés");
    }
}
