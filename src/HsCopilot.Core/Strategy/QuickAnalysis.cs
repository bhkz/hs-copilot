using HsCopilot.Core.Evaluation;

namespace HsCopilot.Core.Strategy;

public static class LethalLevels
{
    public const string None = "aucun";
    /// <summary>Létal démontré par le solveur sur l'état visible (mécaniques simulées), aucun Secret adverse en jeu.</summary>
    public const string Certain = "certain";
    /// <summary>Ancien nom de <see cref="Certain"/> (réponses du modèle antérieures au solveur).</summary>
    public const string Visible = "visible";
    /// <summary>Dégâts suffisants, mais un élément inconnu peut l'empêcher ou te punir (Secret adverse…).</summary>
    public const string Conditional = "conditionnel";
    /// <summary>Repose sur une carte de la main ou plusieurs étapes que rien ne vérifie.</summary>
    public const string Plausible = "plausible";

    public static readonly string[] All = { None, Certain, Conditional, Plausible };

    /// <summary>Normalise une réponse du modèle (« visible » vaut « certain »).</summary>
    public static string Normalize(string? level) => level == Visible ? Certain : All.Contains(level) ? level! : None;

    public static int Rank(string? level) => Normalize(level) switch { Certain => 3, Conditional => 2, Plausible => 1, _ => 0 };
}

/// <summary>Évaluation du létal par le solveur déterministe sur l'état observable (voir <see cref="TurnSolver"/>).</summary>
public sealed class LethalAssessment
{
    public string Level { get; init; } = LethalLevels.None;
    public int Needed { get; init; }
    /// <summary>Dégâts au visage démontrés ce tour, préparations comprises (mécaniques simulées uniquement).</summary>
    public int MaxFaceDamage { get; init; }
    /// <summary>Dégâts des seules attaques disponibles maintenant.</summary>
    public int AttacksOnlyDamage { get; init; }
    public List<string> Conditions { get; init; } = new();
    public SolverResult? Solver { get; init; }

    public string? Label => Level switch
    {
        LethalLevels.Certain => $"Létal certain (démontré) : {Solver?.SequenceText} = {MaxFaceDamage} dégâts contre {Needed} PV.",
        LethalLevels.Conditional when MaxFaceDamage >= Needed =>
            $"Létal conditionnel : {Solver?.SequenceText} = {MaxFaceDamage} dégâts contre {Needed} PV ; {string.Join(" ; ", Conditions)}.",
        LethalLevels.Conditional =>
            $"Létal conditionnel : {string.Join(" ; ", Conditions)}. Séquence : {string.Join(", puis ", Solver?.ConditionalSequence.Select(x => x.Label) ?? Array.Empty<string>())}.",
        LethalLevels.Plausible =>
            $"Létal plausible, non démontré : {Solver?.DamageIfFaceOpen} dégâts si les Provocations tombent, contre {Needed} PV ; {string.Join(" ; ", Conditions)}.",
        _ => null,
    };
}

public sealed class QuickFact
{
    /// <summary>info, alerte, danger (tour adverse, mis en avant sur la ligne de mise en garde de l'overlay) ou létal.</summary>
    public required string Severity { get; init; }
    public required string Text { get; init; }
}

/// <summary>
/// Analyse instantanée, sans IA, affichée dès qu'une décision se présente : létal et dégâts maximum calculés par le solveur,
/// ordre des actions, menace adverse, actions encore possibles. Uniquement l'état observable.
/// </summary>
public static class QuickAnalysis
{
    public static LethalAssessment AssessLethal(DecisionContext ctx)
    {
        if (ctx.Kind != DecisionContext.KindAction || ctx.State.Opponent?.Hero is null || ctx.State.Local?.Hero is null) return new LethalAssessment();
        var r = TurnSolver.Solve(ctx);
        return new LethalAssessment
        {
            Level = r.Level,
            Needed = r.Needed,
            MaxFaceDamage = r.MaxFaceDamage,
            AttacksOnlyDamage = r.AttacksOnlyDamage,
            Conditions = r.Conditions,
            Solver = r,
        };
    }

    /// <summary>
    /// Ce qui reste faisable maintenant, d'après les seules options du serveur : attaques (et si le héros adverse est une
    /// cible), pouvoir héroïque, cartes jouables. Gratuit, recalculé à chaque état : rien n'est oublié en fin de tour.
    /// </summary>
    public static List<string> RemainingOpportunities(DecisionContext ctx)
    {
        var result = new List<string>();
        if (ctx.Kind != DecisionContext.KindAction) return result;
        var s = ctx.State;
        var oppHero = s.Opponent?.Hero?.EntityId;
        foreach (var g in ctx.LegalActions.Where(a => a.Kind == "attack").GroupBy(a => a.SourceEntityId))
        {
            var first = g.First();
            var attack = BoardMath.AttackValue(s, first.SourceEntityId, countWindfury: false);
            var face = g.Any(a => a.TargetEntityId == oppHero);
            result.Add($"attaque de {first.SourceName ?? "?"} ({attack} ATQ){(face ? ", héros adverse possible" : ", serviteurs seulement")}");
        }
        if (ctx.LegalActions.FirstOrDefault(a => a.Kind == "hero-power") is { } hp)
        {
            var cost = s.Local?.HeroPower?.Cost;
            result.Add($"pouvoir héroïque {hp.SourceName ?? ""}{(cost is null ? "" : $" ({cost})")}".Replace("  ", " "));
        }
        var cards = ctx.LegalActions.Where(a => a.Kind is "play").Select(a => a.SourceEntityId).Distinct().Count();
        if (cards > 0) result.Add($"{cards} carte(s) jouable(s) avec {s.Local?.Mana.Available ?? 0} mana");
        var locations = ctx.LegalActions.Where(a => a.Kind == "location").Select(a => a.SourceEntityId).Distinct().Count();
        if (locations > 0) result.Add($"{locations} lieu(x) activable(s)");
        return result;
    }

    /// <summary>
    /// Calcul local joint à l'appel du modèle (champ « localAnalysis ») : uniquement l'état visible et nos propres cartes.
    /// </summary>
    public static object? LocalAnalysisPayload(DecisionContext ctx, LethalAssessment? lethal)
    {
        if (ctx.Kind != DecisionContext.KindAction || lethal?.Solver is not { } r) return null;
        return new
        {
            lethal = lethal.Level,
            opponentHealthAndArmor = r.Needed,
            maxFaceDamageThisTurn = r.MaxFaceDamage,
            attacksOnlyDamage = r.AttacksOnlyDamage,
            bestSequence = r.Sequence.Select(x => x.Label).ToList(),
            conditionalSequence = r.ConditionalSequence.Count == 0 ? null : r.ConditionalSequence.Select(x => x.Label).ToList(),
            conditions = r.Conditions,
            ordering = r.OrderingHints,
            unlikely = r.LongShots,
            notSimulated = r.Unmodeled.Distinct().ToList(),
            remaining = RemainingOpportunities(ctx),
            note = "dégâts au héros adverse uniquement ; échanges, survie et valeur ne sont pas évalués par ce calcul",
            deck = DeckOdds.Payload(ctx, onlyIfRelevant: true),
            nextTurn = NextTurnPayload(ctx),
        };
    }

    private const string GrommashId = "CORE_EX1_414";

    /// <summary>Faits certains sur le tour suivant (mana, Parenté, recharge d'un lieu) et potentiel supposé si le plateau survit.</summary>
    public static object? NextTurnPayload(DecisionContext ctx)
    {
        var (mana, kindred, locations) = NextTurnFacts(ctx);
        // Le potentiel supposé n'est joint que s'il atteint les PV adverses actuels (préparer un létal au tour suivant).
        var potential = TurnSolver.SolveNextTurn(ctx);
        object? ifBoard = potential.MaxFaceDamage > 0 && potential.MaxFaceDamage >= potential.Needed
            ? new
            {
                maxFaceDamage = potential.MaxFaceDamage,
                opponentHealthAndArmorNow = potential.Needed,
                sequence = potential.Sequence.Select(x => x.Label).ToList(),
                assumptions = "hypothèse : ton plateau survit, tu ne joues plus rien ce tour, sans la pioche, PV et Provocations adverses inchangés",
            }
            : null;
        if (kindred.Count == 0 && locations.Count == 0 && ifBoard is null) return null;
        return new { mana, kindred = kindred.Count == 0 ? null : kindred, locations = locations.Count == 0 ? null : locations, ifBoardSurvives = ifBoard };
    }

    /// <summary>Mana du tour suivant, Parenté des cartes en main, recharge des lieux quand Grommash attend en main.</summary>
    public static (int Mana, List<string> Kindred, List<string> Locations) NextTurnFacts(DecisionContext ctx)
    {
        var kindred = new List<string>();
        var locations = new List<string>();
        var me = ctx.State.Local;
        if (me is null || ctx.Kind != DecisionContext.KindAction) return (0, kindred, locations);
        var mana = Math.Max(0, Math.Min(10, me.Mana.Max + 1) - me.Mana.OverloadOwed);
        var typesThisTurn = me.ThisTurn?.TypesPlayed ?? new List<string>();
        foreach (var c in me.Hand.Cards.Where(c => TurnSolver.KindredReduction(c.Text) > 0 && c.BaseCost is not null && c.Races is { Count: > 0 })
                     .GroupBy(c => c.CardId).Select(g => g.First()))
        {
            var red = TurnSolver.KindredReduction(c.Text);
            var types = c.Races!;
            var typeName = types.Contains("DRAGON") ? "Dragon" : string.Join("/", types);
            if (types.Any(typesThisTurn.Contains))
            {
                kindred.Add($"{c.Name} coûtera {c.BaseCost - red} au prochain tour (Parenté : un {typeName} a déjà été joué ce tour) ; tu auras {mana} mana");
                continue;
            }
            var playable = ctx.LegalActions.Where(a => a.Kind == "play")
                .Select(a => me.Hand.Cards.FirstOrDefault(h => h.EntityId == a.SourceEntityId))
                .Where(h => h?.Races?.Any(types.Contains) == true).Select(h => h!.Name ?? "?").Distinct().ToList();
            kindred.Add($"{c.Name} coûtera {c.BaseCost} au prochain tour, ou {c.BaseCost - red} si tu joues un {typeName} ce tour (Parenté) ; tu auras {mana} mana ; "
                        + (playable.Count == 0 ? $"aucun {typeName} jouable maintenant" : $"{typeName}s jouables maintenant : {string.Join(", ", playable)}"));
        }
        if (me.Hand.Cards.Any(c => c.CardId == GrommashId))
        {
            foreach (var loc in me.Board.Where(m => m.Type == "LOCATION" && ctx.LegalActions.Any(a => a.Kind == "location" && a.SourceEntityId == m.EntityId)))
                locations.Add($"{loc.Name} ({loc.Health} charge(s)) : l'activer ce tour le rend indisponible à ton prochain tour (recharge vérifiée sur tes parties) ; "
                              + $"Grommash en main, {(mana >= 8 ? "jouable" : $"pas encore jouable ({mana} mana)")} au prochain tour : le combo Grommash + {loc.Name} au prochain tour exige de ne pas l'activer maintenant");
        }
        return (mana, kindred, locations);
    }

    public static List<QuickFact> Analyze(DecisionContext ctx, LethalAssessment? lethal = null)
    {
        var facts = new List<QuickFact>();
        var s = ctx.State;
        if (ctx.Kind == DecisionContext.KindMulligan)
        {
            facts.Add(new QuickFact { Severity = "info", Text = $"Mulligan : {ctx.LegalActions.Count(a => a.Kind == "mulligan-replace")} cartes proposées." });
            return facts;
        }
        if (ctx.Kind == DecisionContext.KindChoice)
        {
            facts.Add(new QuickFact { Severity = "info", Text = (ctx.ChoiceSourceName is null ? "Choix proposé" : $"Choix proposé par {ctx.ChoiceSourceName}") + $" : {string.Join(", ", ctx.LegalActions.Select(a => a.SourceName ?? "?"))}." });
            return facts;
        }
        lethal ??= AssessLethal(ctx);
        if (lethal.Label is string label) facts.Add(new QuickFact { Severity = "létal", Text = label });
        if (lethal.Solver is { } solver)
        {
            if (lethal.Level == LethalLevels.None && solver.MaxFaceDamage > solver.AttacksOnlyDamage)
                facts.Add(new QuickFact { Severity = "info", Text = $"Dégâts maximum au visage ce tour : {solver.MaxFaceDamage} contre {solver.Needed} PV ({solver.SequenceText})." });
            foreach (var hint in solver.OrderingHints)
                facts.Add(new QuickFact { Severity = "alerte", Text = $"Ordre : {hint}." });
            foreach (var shot in solver.LongShots)
                facts.Add(new QuickFact { Severity = "info", Text = $"Peu probable : {shot}." });
            var (_, kindred, locations) = NextTurnFacts(ctx);
            foreach (var k in kindred) facts.Add(new QuickFact { Severity = "info", Text = $"Prochain tour : {k}." });
            foreach (var l in locations) facts.Add(new QuickFact { Severity = "alerte", Text = $"Prochain tour : {l}." });
            if (lethal.Level != LethalLevels.Certain && solver.Unmodeled.Count > 0)
                facts.Add(new QuickFact { Severity = "info", Text = $"Non simulé par le calcul : {string.Join(", ", solver.Unmodeled.Distinct())}." });
        }
        // Danger au prochain tour adverse, information publique seulement (affiché, jamais envoyé au modèle). Remplace l'ancienne
        // alerte « Menace » : sans pouvoir héroïque ni Provocations, affichée seulement au-delà du seuil, elle avait disparu à la
        // décision fatale contre Rexxar (26/10/2026, 0 h 44 : 25 dégâts visibles contre 25 PV).
        if (ctx.Kind == DecisionContext.KindAction && ThreatAssessment.Assess(ctx) is { } danger)
        {
            if (danger.Margin <= 5)
            {
                var verdict = danger.Margin <= 0 ? "létal adverse visible" : $"marge de {danger.Margin} PV";
                var defenses = danger.Defenses.Take(2).Select(d => d.Text).ToList();
                facts.Add(new QuickFact
                {
                    Severity = "danger",
                    Text = $"Danger au prochain tour : {danger.VisibleDamage} dégâts visibles ({string.Join(" + ", danger.Sources)}) contre tes {danger.EffectiveHealth} PV"
                        + (danger.TauntShield > 0 ? $" + {danger.TauntShield} de Provocations" : "") + $", {verdict}"
                        + (defenses.Count > 0 ? " ; défenses : " + string.Join(" ; ", defenses) : "") + ".",
                });
            }
            // Seul l'effet chiffrable est signalé (Nozdormu l'Intemporel, 25/10/2026 22 h 53) : le motif large signalait surtout
            // l'arme aléatoire d'Animatrice de stade, carte de base du deck.
            foreach (var gift in danger.Gifts.Where(g => g.Effect.Contains("10 cristaux", StringComparison.Ordinal)))
                facts.Add(new QuickFact { Severity = "danger", Text = $"{gift.Card} aide aussi l'adversaire : {gift.Effect}." });
        }
        if (s.Opponent?.Deck is { Count: <= 2 } oppDeck)
            facts.Add(new QuickFact { Severity = "info", Text = $"Fatigue proche : l'adversaire n'a plus que {oppDeck.Count} carte(s) dans son deck ; survivre le fera bientôt subir des dégâts de fatigue." });
        var remaining = RemainingOpportunities(ctx);
        facts.Add(new QuickFact
        {
            Severity = "info",
            Text = remaining.Count == 0 ? "Aucune action possible à part finir le tour." : "Encore possible : " + string.Join(" ; ", remaining) + ".",
        });
        return facts;
    }
}
