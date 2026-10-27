using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

public sealed class ValidationOutcome
{
    public required bool Accepted { get; init; }
    public Recommendation? Recommendation { get; init; }
    public List<string> Reasons { get; init; } = new();
}

/// <summary>
/// Transforme une réponse de fournisseur en <see cref="Recommendation"/> ou la rejette.
/// Règles : l'action doit exister dans les actions légales du contexte ; la partie, la révision et les options viennent du
/// contexte ; les étapes suivantes sont toujours « non vérifiées » ; aucune certitude si une information adverse est cachée.
/// </summary>
public static class RecommendationValidator
{
    public const int MaxSummaryWords = 20;
    public const int MaxWhyWords = 15;

    /// <summary>Formulations de certitude (mots entiers, insensible à la casse).</summary>
    private static readonly System.Text.RegularExpressions.Regex CertaintyWords = new(
        @"(?<![\p{L}\d])(certain|certaine|certitude|garanti|garantie|assuré|assurée|infaillible|sans aucun doute|100\s?%)(?![\p{L}\d])",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly string[] VerifiedSequenceClaims = { "séquence vérifiée", "sequence verifiee", "séquence validée", "légalement validé" };

    public static ValidationOutcome Validate(StrategyOutput output, DecisionContext ctx, string provider, string? model, double latencyMs, double? costUsd, LethalAssessment? lethal = null)
    {
        var reasons = new List<string>();
        var action = ctx.LegalActions.FirstOrDefault(a => a.Id == output.ActionId);
        if (action is null)
        {
            reasons.Add($"action « {output.ActionId} » absente des actions légales de la révision {ctx.Revision}");
            return new ValidationOutcome { Accepted = false, Reasons = reasons };
        }
        if (string.IsNullOrWhiteSpace(output.Summary))
        {
            reasons.Add("conseil principal vide");
            return new ValidationOutcome { Accepted = false, Reasons = reasons };
        }

        var warnings = new List<string>();
        var replace = new List<LegalAction>();
        if (ctx.Kind == DecisionContext.KindMulligan)
        {
            foreach (var id in output.ReplaceIds.Distinct())
            {
                var card = ctx.LegalActions.FirstOrDefault(a => a.Id == id && a.Kind == "mulligan-replace");
                if (card is null)
                {
                    reasons.Add($"carte à remplacer « {id} » absente des cartes proposées au mulligan");
                    return new ValidationOutcome { Accepted = false, Reasons = reasons };
                }
                replace.Add(card);
            }
            if (action.Kind == "mulligan-replace" && !replace.Contains(action)) replace.Insert(0, action);
            if (action.Kind == "mulligan-keep" && replace.Count > 0) warnings.Add("« garder toute la main » contredit replaceIds : replaceIds retenu");
        }
        else if (output.ReplaceIds.Count > 0)
        {
            warnings.Add("replaceIds ignoré hors mulligan");
        }

        // Létal : le niveau annoncé par le modèle ne peut pas dépasser ce que le solveur démontre ; un létal démontré par le
        // solveur est toujours affiché, même si le modèle ne l'annonce pas.
        lethal ??= QuickAnalysis.AssessLethal(ctx);
        var claimed = LethalLevels.Normalize(output.Lethal);
        var lethalLevel = claimed;
        if (claimed == LethalLevels.Certain && lethal.Level != LethalLevels.Certain)
        {
            lethalLevel = lethal.Level == LethalLevels.Conditional ? LethalLevels.Conditional : LethalLevels.Plausible;
            warnings.Add($"létal annoncé « certain » ramené à « {lethalLevel} » : le solveur ne le démontre pas sur l'état visible");
        }
        else if (claimed == LethalLevels.Conditional && lethal.Level == LethalLevels.None)
        {
            lethalLevel = LethalLevels.Plausible;
        }
        if (lethal.Level is LethalLevels.Certain or LethalLevels.Conditional && LethalLevels.Rank(lethal.Level) > LethalLevels.Rank(lethalLevel))
        {
            warnings.Add($"le solveur voit un létal « {lethal.Level} » que le conseil n'annonce pas");
            lethalLevel = lethal.Level;
        }
        var summary = output.Summary.Trim();
        var words = summary.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxSummaryWords)
        {
            summary = string.Join(' ', words.Take(MaxSummaryWords)) + "…";
            warnings.Add($"conseil tronqué à {MaxSummaryWords} mots");
        }

        var level = output.Confidence.Trim().ToLowerInvariant() switch
        {
            "élevée" or "elevee" or "haute" or "high" => UncertaintyLevel.Faible,
            "moyenne" or "medium" => UncertaintyLevel.Moyenne,
            _ => UncertaintyLevel.Elevee,
        };
        var uncertaintyReasons = new List<string>(output.UncertaintyReasons);
        var allText = string.Join(' ', new[] { summary, output.Rationale, output.Risk ?? "", output.PlanNextTurn ?? "" }.Concat(output.FollowUp.Where(f => f is not null).Select(f => f.Text)));
        if (ctx.OpponentInformationHidden)
        {
            if (level == UncertaintyLevel.Faible) level = UncertaintyLevel.Moyenne;
            uncertaintyReasons.Add("informations adverses inconnues (main, deck ou Secrets)");
            if (CertaintyWords.IsMatch(allText))
            {
                level = UncertaintyLevel.Elevee;
                warnings.Add("affirmation de certitude retirée : des informations adverses sont inconnues");
            }
        }
        if (ctx.State.Uncertainty.Flags.Any(f => f.Severity == "warning"))
        {
            if (level == UncertaintyLevel.Faible) level = UncertaintyLevel.Moyenne;
            uncertaintyReasons.Add("état reconstruit avec avertissement(s)");
        }
        var followUp = output.FollowUp.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => new PlannedStep { Description = x.Text.Trim(), SourceEntityId = x.SourceEntityId, TargetEntityId = x.TargetEntityId }).ToList();
        if (VerifiedSequenceClaims.Any(w => allText.Contains(w, StringComparison.OrdinalIgnoreCase)))
            warnings.Add("mention de vérification ignorée : seule l'action immédiate est garantie légale");

        var rejected = output.RejectedAlternativeId is null ? null : ctx.LegalActions.FirstOrDefault(a => a.Id == output.RejectedAlternativeId);
        var cautions = new List<string>();
        if (ctx.Kind == DecisionContext.KindAction && OrderCaution(ctx, action, lethal) is { } order)
        {
            cautions.Add(order);
            warnings.Add("attaque conseillée avant un bonus d'ATQ disponible sur l'attaquant");
        }
        if (lethal.Level == LethalLevels.Certain && lethal.Solver is { } solver && !PartOf(action, solver.Sequence))
        {
            cautions.Add($"Létal démontré par le calcul local : {solver.SequenceText}.");
            warnings.Add("le conseil ne suit pas le létal démontré par le solveur");
        }
        if (action.Kind == "end-turn")
        {
            var left = QuickAnalysis.RemainingOpportunities(ctx).Where(o => o.StartsWith("attaque", StringComparison.Ordinal) || o.StartsWith("pouvoir", StringComparison.Ordinal)).ToList();
            if (left.Count > 0)
            {
                cautions.Add("Avant de finir le tour, encore possible : " + string.Join(" ; ", left) + ".");
                warnings.Add("fin de tour conseillée alors que des attaques ou le pouvoir héroïque restent possibles");
            }
        }
        var why = output.Why?.Trim();
        if (why is not null)
        {
            var w = why.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (w.Length > MaxWhyWords) why = string.Join(' ', w.Take(MaxWhyWords)) + "…";
        }
        var rec = new Recommendation
        {
            Kind = ctx.Kind,
            Why = string.IsNullOrWhiteSpace(why) ? null : why,
            Lethal = lethalLevel,
            LethalNote = lethal.Label,
            ReplaceActions = replace,
            Caution = cautions.Count == 0 ? null : string.Join(" ", cautions),
            GameId = ctx.GameId,
            Revision = ctx.Revision,
            OptionsId = ctx.OptionsId,
            GameTurn = ctx.GameTurn,
            ResetCountAtCreation = ctx.ResetCount,
            HistorySeqAtCreation = ctx.HistorySeq,
            Action = action,
            Uncertainty = level,
            UncertaintyReasons = uncertaintyReasons.Distinct().ToList(),
            Summary = summary,
            Rationale = output.Rationale,
            RejectedAlternative = rejected,
            RejectedAlternativeReason = output.RejectedAlternativeReason,
            Risk = output.Risk,
            PlanNextTurn = output.PlanNextTurn,
            FollowUp = followUp,
            Warnings = warnings,
            Provider = provider,
            Model = model,
            LatencyMs = latencyMs,
            CostUsd = costUsd,
        };
        return new ValidationOutcome { Accepted = true, Recommendation = rec, Reasons = warnings };
    }

    /// <summary>
    /// Attaque conseillée alors qu'un lieu donnant +2 ATQ (Profondeurs Sanguines) peut encore cibler l'attaquant ce tour :
    /// le bonus serait perdu pour cette attaque. Calcul local, sur les options du serveur.
    /// </summary>
    public static string? OrderCaution(DecisionContext ctx, LegalAction action, LethalAssessment? lethal = null)
    {
        if (action.Kind != "attack" || action.SourceEntityId is not int attacker) return null;
        var buff = ctx.LegalActions.FirstOrDefault(a => a.Kind == "location" && a.SourceCardId is "CORE_REV_990" or "REV_990" && a.TargetEntityId == attacker);
        if (buff is null) return null;
        var minion = ctx.State.Local?.Board.FirstOrDefault(m => m.EntityId == attacker);
        if (minion is null || (minion.Health <= 1 && !minion.Keywords.Contains("DIVINE_SHIELD"))) return null; // le lieu le tuerait
        var inSolver = lethal?.Solver?.Sequence.Any(x => x.Kind == "location" && x.TargetEntityId == attacker) == true;
        var face = action.TargetEntityId == ctx.State.Opponent?.Hero?.EntityId;
        if (!inSolver && !face) return null; // échange : le bonus n'est pas forcément utile
        return $"Avant cette attaque : {buff.Label} (+2 ATQ, soit {minion.Attack + 2} au lieu de {minion.Attack}).";
    }

    /// <summary>L'action fait-elle partie de la séquence calculée (même source, même cible) ?</summary>
    public static bool PartOf(LegalAction action, IEnumerable<SolverStep> sequence) =>
        sequence.Any(x => x.SourceEntityId == action.SourceEntityId && (x.TargetEntityId is null || x.TargetEntityId == action.TargetEntityId));

    /// <summary>Une recommandation reste affichable seulement pour la partie, la révision et les options exactes qui l'ont produite.</summary>
    public static (bool Valid, string? Reason) CheckStillValid(Recommendation rec, GameStateSnapshot current)
    {
        if (current.GameId != rec.GameId) return (false, InvalidationReasons.NewGame);
        if (current.Game.ResetCount > rec.ResetCountAtCreation) return (false, InvalidationReasons.Rewind);
        if (rec.Kind != DecisionContext.KindAction)
        {
            // Un conseil de mulligan ou de choix reste valable tant que ce choix précis attend une réponse.
            if (current.PendingChoice is { } pc && pc.Id == rec.OptionsId) return (true, null);
            return (false, current.Game.GameOver ? InvalidationReasons.GameOver : InvalidationReasons.ChoiceResolved);
        }
        if (current.Revision == rec.Revision && current.LegalOptions is { Fresh: true } o && o.OptionsId == rec.OptionsId) return (true, null);
        if (current.Game.GameOver) return (false, InvalidationReasons.GameOver);
        if (current.Game.GameTurn != rec.GameTurn || current.Game.ActivePlayer != "local") return (false, InvalidationReasons.TurnEnded);
        var newLocalActions = current.History.Where(h => h.Seq > rec.HistorySeqAtCreation && h.Player == "local" && h.Kind is "play" or "attack" or "hero-power" or "location").ToList();
        if (newLocalActions.Count > 0)
        {
            var followed = newLocalActions.Any(h => h.SourceEntityId == rec.Action.SourceEntityId && (rec.Action.TargetEntityId is null || h.TargetEntityId == rec.Action.TargetEntityId));
            return (false, followed ? InvalidationReasons.Followed : InvalidationReasons.OtherAction);
        }
        return (false, InvalidationReasons.StateChanged);
    }
}

public static class InvalidationReasons
{
    public const string NewGame = "nouvelle partie";
    public const string Rewind = "Rembobinage";
    public const string GameOver = "fin de partie";
    public const string TurnEnded = "fin de tour";
    public const string Followed = "conseil suivi";
    public const string OtherAction = "autre action jouée";
    public const string StateChanged = "état modifié";
    public const string ChoiceResolved = "choix effectué";
}
