using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Danger adverse visible au prochain tour, sur les états réels des deux défaites « en un tour » (anonymisées) :
/// Rexxar le 26/10 à 0 h 44 (25 dégâts visibles contre 25 PV) et Tyrande le 25/10 à 22 h 53 (Nozdormu l'Intemporel donne
/// 10 cristaux à un adversaire qui a 8 cartes en main).
/// </summary>
public class ThreatAssessmentTests
{
    private const string Hunter = "ranked-warrior-vs-hunter-visible-lethal.log";
    private const string Priest = "ranked-warrior-vs-priest-nozdormu.log";

    /// <summary>
    /// État d'une décision : le dernier bloc d'options du serveur avant la ligne de changement de tour <paramref name="turnLine"/>,
    /// ou le bloc n° <paramref name="index"/> (à partir de 0) qui la suit.
    /// </summary>
    private static DecisionContext Decision(string fixture, string turnLine, bool lastBefore, int index = 0)
    {
        var lines = TestData.Lines(fixture).ToList();
        var marker = lines.FindIndex(l => l.Contains("GameState.DebugPrintPower()") && l.Contains($"TAG_CHANGE Entity=GameEntity tag=TURN value={turnLine}"));
        Assert.True(marker > 0);
        var headers = Enumerable.Range(0, lines.Count).Where(i => lines[i].Contains("GameState.DebugPrintOptions() - id=")).ToList();
        var header = lastBefore ? headers.Last(i => i < marker) : headers.Where(i => i > marker).Skip(index).First();
        var engine = TestData.NewEngine();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > header && !lines[i].Contains("DebugPrintOptions")) break;
            if (PowerLogLineParser.TryParse(lines[i]) is { } p) engine.Process(p);
        }
        return DecisionContextBuilder.Build(TestData.Project(engine))!;
    }

    [Fact]
    public void The_visible_lethal_of_the_Rexxar_loss_is_detected_at_the_end_of_the_turn()
    {
        var r = ThreatAssessment.Assess(Decision(Hunter, "32", lastBefore: true))!;
        Assert.Equal(25, r.VisibleDamage);          // Prédateur en chasse 4 + Serpent éternel 7 + Ursol 9 + arme 3 + pouvoir 2
        Assert.Equal(25, r.EffectiveHealth);        // 23 PV + 2 armure
        Assert.Equal(0, r.Margin);
        Assert.Equal(ThreatReport.VisibleLethal, r.Level);
        Assert.Contains(r.Sources, x => x.StartsWith("Ursol 9"));
        Assert.Contains("pouvoir héroïque 2", r.Sources);
    }

    [Fact]
    public void At_the_start_of_that_turn_killing_the_Serpent_is_offered_as_a_defence_with_its_cost()
    {
        var ctx = Decision(Hunter, "31", lastBefore: false);
        var r = ThreatAssessment.Assess(ctx)!;
        Assert.True(r.Margin <= 0);
        var serpent = r.Defenses.First(d => d.Text.Contains("Serpent éternel"));
        Assert.Equal(7, serpent.Removed);
        Assert.Contains(r.Defenses, d => d.Action.Kind == "hero-power" && d.Removed == 2);
        var payload = System.Text.Json.JsonSerializer.Serialize(ThreatAssessment.Payload(r));
        Assert.Contains("margin", payload);
    }

    [Fact]
    public void Nozdormu_is_flagged_as_giving_ten_mana_to_an_opponent_with_eight_cards()
    {
        var r = ThreatAssessment.Assess(Decision(Priest, "13", lastBefore: false))!;
        var gift = Assert.Single(r.Gifts, g => g.Card == "Nozdormu l’Intemporel");
        Assert.Contains("10 cristaux au prochain tour au lieu de 7", gift.Effect);
        Assert.Contains("8 cartes en main", gift.Effect);
        // Le danger visible lui-même restait modéré : 12 dégâts (Devin 6, Auxiliaire 2, arme 4) contre 21 PV.
        Assert.Equal(12, r.VisibleDamage);
        Assert.Equal(ThreatReport.Low, r.Level);
    }

    // ------------------------------------------------------------------ Affichage (faits locaux, jamais envoyés au modèle)

    private static List<string> Facts(DecisionContext ctx) => QuickAnalysis.Analyze(ctx).Select(f => f.Text).ToList();

    [Fact]
    public void The_danger_fact_is_shown_at_the_start_of_the_fatal_turn_with_the_defence()
    {
        var facts = Facts(Decision(Hunter, "31", lastBefore: false));
        var danger = Assert.Single(facts, f => f.StartsWith("Danger au prochain tour"));
        Assert.Contains("létal adverse visible", danger);
        Assert.Contains("pouvoir héroïque 2", danger);
        Assert.Contains("Serpent éternel", danger);
        Assert.DoesNotContain(facts, f => f.StartsWith("Menace"));
    }

    [Fact]
    public void The_danger_fact_stays_after_the_Spirit_speaker_dies_where_the_old_alert_disappeared()
    {
        // 0 h 44 m 23 s : le Gardien a tué le Parle-esprit ; l'ancienne alerte (23 contre 25, sans pouvoir héroïque) s'effaçait.
        var ctx = Decision(Hunter, "31", lastBefore: false, index: 2);
        Assert.DoesNotContain(ctx.State.Opponent!.Board, m => m.Name == "Parle-esprit");
        var danger = Assert.Single(Facts(ctx), f => f.StartsWith("Danger au prochain tour"));
        Assert.Contains("25 dégâts visibles", danger);   // pouvoir héroïque compris : toujours 25 contre 25
        Assert.Contains("létal adverse visible", danger);
    }

    [Fact]
    public void Nozdormu_shows_its_cost_for_the_opponent_without_a_false_danger_alert()
    {
        var facts = Facts(Decision(Priest, "13", lastBefore: false));
        Assert.Contains(facts, f => f.StartsWith("Nozdormu l’Intemporel aide aussi l'adversaire : l'adversaire aurait 10 cristaux au prochain tour au lieu de 7"));
        Assert.DoesNotContain(facts, f => f.StartsWith("Danger au prochain tour"));   // 12 visibles contre 21 PV : marge 9
    }

    [Fact]
    public void A_nearly_empty_opponent_deck_is_signalled_as_fatigue()
    {
        var facts = Facts(Decision(Hunter, "32", lastBefore: true));
        Assert.Contains(facts, f => f.StartsWith("Fatigue proche : l'adversaire n'a plus que 1 carte(s)"));
    }

    // ------------------------------------------------------------------ Overlay : l'alerte reste à l'écran avec le conseil

    private static AdvisorUpdate Update(DecisionContext ctx, string phase, LegalAction? partial = null, Recommendation? rec = null, LethalAssessment? lethal = null) => new()
    {
        Phase = phase,
        State = ctx.State,
        DecisionKind = ctx.Kind,
        Facts = QuickAnalysis.Analyze(ctx),
        Lethal = lethal ?? QuickAnalysis.AssessLethal(ctx),
        PartialAction = partial,
        Recommendation = rec,
    };

    private static Recommendation Advice(DecisionContext ctx, LegalAction action) =>
        RecommendationValidator.Validate(new StrategyOutput { ActionId = action.Id, Summary = "Joue ceci.", Confidence = "moyenne" }, ctx, "t", null, 0, null).Recommendation!;

    [Fact]
    public void The_danger_alert_stays_on_the_caution_line_when_the_advice_arrives()
    {
        // Décision fatale contre Rexxar : parmi les faits, l'alerte disparaissait dès le début de réponse, le conseil ou la suite du plan.
        var ctx = Decision(Hunter, "31", lastBefore: false, index: 2);
        var attack = ctx.LegalActions.First(a => a.Kind == "attack");
        var plan = Advice(ctx, attack);
        plan.Origin = AdviceOrigins.Plan;
        foreach (var u in new[]
        {
            Update(ctx, AdvisorPhases.Analyzing),
            Update(ctx, AdvisorPhases.Partial, partial: attack),
            Update(ctx, AdvisorPhases.Ready, rec: Advice(ctx, attack)),
            Update(ctx, AdvisorPhases.Ready, rec: plan),
        })
        {
            var view = AdviceDisplay.Build(u, DateTime.UtcNow);
            Assert.StartsWith("Danger au prochain tour : 25 dégâts visibles", view.CautionText);
            Assert.DoesNotContain(view.Facts, f => f.StartsWith("Danger"));   // pas en double parmi les faits
        }
    }

    [Fact]
    public void The_advice_to_play_Nozdormu_no_longer_erases_its_cost_for_the_opponent()
    {
        var ctx = Decision(Priest, "13", lastBefore: false);
        var nozdormu = ctx.LegalActions.First(a => a.Kind == "play" && a.SourceName == "Nozdormu l’Intemporel");
        var view = AdviceDisplay.Build(Update(ctx, AdvisorPhases.Partial, partial: nozdormu), DateTime.UtcNow);
        Assert.StartsWith("Nozdormu l’Intemporel aide aussi l'adversaire : l'adversaire aurait 10 cristaux", view.CautionText);
    }

    [Fact]
    public void A_demonstrated_lethal_hides_the_danger_alert()
    {
        var ctx = Decision(Hunter, "31", lastBefore: false, index: 2);
        var certain = new LethalAssessment { Level = LethalLevels.Certain, Needed = 1, MaxFaceDamage = 1 };
        Assert.Equal("", AdviceDisplay.Build(Update(ctx, AdvisorPhases.Analyzing, lethal: certain), DateTime.UtcNow).CautionText);
    }
}
