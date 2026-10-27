using HsCopilot.Core.Evaluation;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Danger au prochain tour adverse après un coup (hors ligne). Validé sur toutes les parties réelles des fixtures : la marge
/// de départ est celle de l'alerte affichée, et chaque coup simulé « complet » donne l'état réel suivant du log.
/// </summary>
public class LineDangerTests
{
    private const string Hunter = "ranked-warrior-vs-hunter-visible-lethal.log";
    private const string Priest = "ranked-warrior-vs-priest-nozdormu.log";

    /// <summary>Décisions d'action d'une partie, dans l'ordre, avec le coup réellement joué ensuite.</summary>
    private static List<(DecisionContext Ctx, LegalAction? Played)> AllDecisions(string fixture)
    {
        var lines = TestData.Lines(fixture);
        var engine = TestData.NewEngine();
        var list = new List<(DecisionContext, LegalAction?)>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (PowerLogLineParser.TryParse(lines[i]) is not { } p) continue;
            engine.Process(p);
            if (p.Method != "DebugPrintOptions" || i + 1 < lines.Count && lines[i + 1].Contains("GameState.DebugPrintOptions()")) continue;
            if (engine.LocalPlayerId is null || engine.CurrentPlayerId != engine.LocalPlayerId || !engine.OptionsFresh) continue;
            if (DecisionContextBuilder.Build(TestData.Project(engine)) is not { Kind: DecisionContext.KindAction } ctx) continue;
            var actual = DatasetBuilder.FindActual(lines, i + 1, engine.CurrentOptions!, ctx);
            list.Add((ctx, ctx.LegalActions.FirstOrDefault(a => a.Id == actual?.ActionId)));
        }
        return list;
    }

    private static IEnumerable<string> Fixtures() =>
        Directory.GetFiles(TestData.FixtureDir, "*.log").Select(Path.GetFileName)!;

    /// <summary>État d'une décision : le bloc d'options n° <paramref name="index"/> après la ligne de changement de tour.</summary>
    private static DecisionContext Decision(string fixture, string turnLine, int index)
    {
        var lines = TestData.Lines(fixture).ToList();
        var marker = lines.FindIndex(l => l.Contains("GameState.DebugPrintPower()") && l.Contains($"TAG_CHANGE Entity=GameEntity tag=TURN value={turnLine}"));
        var header = Enumerable.Range(0, lines.Count).Where(i => lines[i].Contains("GameState.DebugPrintOptions() - id=") && i > marker).Skip(index).First();
        var engine = TestData.NewEngine();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > header && !lines[i].Contains("DebugPrintOptions")) break;
            if (PowerLogLineParser.TryParse(lines[i]) is { } p) engine.Process(p);
        }
        return DecisionContextBuilder.Build(TestData.Project(engine))!;
    }

    [Fact]
    public void The_starting_margin_is_the_one_of_the_displayed_alert_on_every_real_decision()
    {
        var checkedCount = 0;
        foreach (var fixture in Fixtures())
            foreach (var (ctx, _) in AllDecisions(fixture))
            {
                var endTurn = ctx.LegalActions.First(a => a.Kind == "end-turn");
                Assert.Equal(ThreatAssessment.Assess(ctx)!.Margin, LineDanger.Check(ctx, endTurn)!.MarginNow);
                checkedCount++;
            }
        Assert.True(checkedCount > 300, $"{checkedCount} décisions");
    }

    [Fact]
    public void Every_complete_simulated_move_gives_the_real_next_state_of_the_log()
    {
        var compared = 0;
        foreach (var fixture in Fixtures())
        {
            var all = AllDecisions(fixture);
            for (int i = 0; i + 1 < all.Count; i++)
            {
                var (ctx, played) = all[i];
                var next = all[i + 1].Ctx;
                if (played is null || next.GameTurn != ctx.GameTurn) continue;
                if (LineDanger.Check(ctx, played) is not { Status: LineDangerReport.Complete, MarginAfterAction: int simulated }) continue;
                Assert.True(simulated == ThreatAssessment.Assess(next)!.Margin, $"{fixture} tour {ctx.GameTurn} : {played.Label} ; simulé {simulated}, réel {ThreatAssessment.Assess(next)!.Margin}");
                compared++;
            }
        }
        Assert.True(compared > 100, $"{compared} transitions comparées");
    }

    [Fact]
    public void Against_Rexxar_the_face_attack_throws_away_the_defence_and_ending_the_turn_would_lose_it()
    {
        // 0 h 44 m 23 s : le Parle-esprit est mort ; 25 dégâts visibles contre 25 PV, le Serpent éternel 7/3 encore à tuer.
        var ctx = Decision(Hunter, "31", index: 2);
        var face = ctx.LegalActions.Single(a => a.Kind == "attack" && a.SourceName == "Glissedrake visionnaire" && a.TargetEntityId == ctx.State.Opponent!.Hero!.EntityId);
        var r = LineDanger.Check(ctx, face)!;
        Assert.Equal(0, r.MarginNow);
        Assert.Equal(9, r.BestBefore);                           // Serpent éternel tué (−7) et armure (+2)
        Assert.Equal(2, r.BestAfter);                            // il ne reste que l'armure
        Assert.Equal(LineDangerReport.Worsens, r.Verdict);
        Assert.Equal(LineDangerReport.Partial, r.Status);        // un Secret inconnu : jamais « ligne sûre »
        Assert.Contains(r.Unmodeled, u => u.StartsWith("Secret adverse inconnu"));

        var serpent = ctx.LegalActions.Single(a => a.Kind == "attack" && a.SourceName == "Glissedrake visionnaire" && a.TargetName == "Serpent éternel");
        Assert.Equal(LineDangerReport.Defends, LineDanger.Check(ctx, serpent)!.Verdict);

        var end = LineDanger.Check(ctx, ctx.LegalActions.Single(a => a.Kind == "end-turn"))!;
        Assert.Equal(LineDangerReport.LosesLastDefense, end.Verdict);
    }

    [Fact]
    public void A_dying_deathrattle_minion_makes_the_evaluation_partial()
    {
        // Tour 15 contre Rexxar : tuer l'Ours des montagnes invoque deux Provocations, non simulées.
        var ctx = Decision(Hunter, "29", index: 0);
        var siamat = ctx.LegalActions.First(a => a.Kind == "attack" && a.SourceName == "Siamat" && a.TargetName == "Ours des montagnes");
        var r = LineDanger.Check(ctx, siamat)!;
        Assert.Equal(LineDangerReport.Partial, r.Status);
        Assert.Contains("Ours des montagnes meurt : Râle d'agonie non simulé", r.Unmodeled);
    }

    [Fact]
    public void Nozdormu_is_not_simulable_and_its_gift_is_named_instead_of_calling_the_line_safe()
    {
        var ctx = Decision(Priest, "13", index: 0);
        var nozdormu = ctx.LegalActions.First(a => a.Kind == "play" && a.SourceName == "Nozdormu l’Intemporel");
        var r = LineDanger.Check(ctx, nozdormu)!;
        Assert.Equal(LineDangerReport.NotSimulable, r.Status);
        Assert.Contains(r.Unmodeled, u => u.Contains("l'adversaire en profite aussi"));
        Assert.Null(r.BestAfter);
    }

    [Fact]
    public void A_demonstrated_lethal_is_never_reported_as_dangerous()
    {
        var lethal = 0;
        foreach (var fixture in Fixtures())
            foreach (var (ctx, played) in AllDecisions(fixture))
            {
                if (played is null || QuickAnalysis.AssessLethal(ctx)?.Level != LethalLevels.Certain) continue;
                Assert.Equal(LineDangerReport.LethalThisTurn, LineDanger.Check(ctx, played)!.Verdict);
                lethal++;
            }
        Assert.True(lethal > 0);
    }
}
