using System.Globalization;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>Texte prêt à afficher dans l'overlay. Sans conseil valable, les champs d'action restent vides.</summary>
public sealed record AdviceView
{
    /// <summary>ok (conseil valable), busy (analyse), warn (périmé, ignoré, pause), error, idle.</summary>
    public string Tone { get; init; } = "idle";
    public string StatusText { get; init; } = "";
    public bool HasAdvice { get; init; }
    public string ActionTitle { get; init; } = "ACTION IMMÉDIATE";
    public string ActionText { get; init; } = "";
    /// <summary>Ce qui est garanti pour l'action affichée (vide sans action).</summary>
    public string VerifiedText { get; init; } = "";
    public string SummaryText { get; init; } = "";
    public string PlanText { get; init; } = "";
    public string WhyText { get; init; } = "";
    public IReadOnlyList<string> Next { get; init; } = Array.Empty<string>();
    /// <summary>Mise en garde locale (fin de tour alors que des attaques ou le pouvoir héroïque restent possibles).</summary>
    public string CautionText { get; init; } = "";
    public string? LethalText { get; init; }
    /// <summary>certain, conditionnel ou plausible.</summary>
    public string? LethalTone { get; init; }
    public IReadOnlyList<string> Facts { get; init; } = Array.Empty<string>();
    public string FooterText { get; init; } = "";
}

public static class AdviceDisplay
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Au-delà, si l'horloge de l'affichage ne progresse plus, le conseil est montré normalement avec une mise en garde.</summary>
    public static readonly TimeSpan MaxDisplayWait = TimeSpan.FromSeconds(40);

    /// <summary>Ton tour est commencé côté serveur, mais Hearthstone affiche encore le tour adverse.</summary>
    private static bool Awaiting(AdvisorUpdate u, DateTime nowUtc) =>
        u.AwaitingDisplay && (u.AwaitingSinceUtc is not DateTime since || nowUtc - since < MaxDisplayWait);

    public static AdviceView Build(AdvisorUpdate u, DateTime nowUtc)
    {
        var view = BuildCore(u, nowUtc);
        if (!Awaiting(u, nowUtc))
        {
            if (u.AwaitingDisplay)   // horloge figée : on ne retient plus, mais on le dit
                return view with { StatusText = view.StatusText + " · affichage de Hearthstone non confirmé" };
            return view;
        }
        // Le conseil porte sur ton vrai tour (pioche comprise) mais Hearthstone n'a pas fini d'afficher le tour adverse.
        return view with
        {
            ActionTitle = view.HasAdvice || u.Phase == AdvisorPhases.Analyzing ? "TON TOUR ARRIVE" : view.ActionTitle,
            VerifiedText = view.HasAdvice ? "✔ calculé sur ton tour, pioche comprise · Hearthstone affiche encore la fin du tour adverse" : view.VerifiedText,
            StatusText = view.HasAdvice
                ? "À jouer dès que ton tour apparaît : le serveur l'a déjà commencé, l'écran rejoue encore le tour adverse."
                : u.Phase == AdvisorPhases.Analyzing ? "Analyse de ton tour, déjà commencé côté serveur ; Hearthstone termine l'affichage du tour adverse." : view.StatusText,
        };
    }

    private static AdviceView BuildCore(AdvisorUpdate u, DateTime nowUtc)
    {
        var facts = u.Facts.Where(f => f.Severity is not ("létal" or "danger")).Select(f => f.Text).ToList();
        var (lethalText, lethalTone) = Lethal(u);
        // Danger au prochain tour adverse : sur la ligne de mise en garde, visible dans toutes les vues et en mode compact. Parmi
        // les faits, il n'était affiché que pendant l'analyse et disparaissait dès qu'un conseil arrivait (contre-expertise du
        // 26/10/2026 : contre Tyrande, « Jouer Nozdormu » aurait effacé l'alerte Nozdormu). Inutile quand le létal est démontré.
        var danger = lethalTone == LethalLevels.Certain ? "" : string.Join("\n", u.Facts.Where(f => f.Severity == "danger").Select(f => f.Text));
        var elapsed = u.AnalysisStartedUtc is DateTime t ? (nowUtc - t).TotalSeconds : 0;
        switch (u.Phase)
        {
            case AdvisorPhases.Ready when u.Recommendation is { } rec:
                return new AdviceView
                {
                    Tone = "ok",
                    HasAdvice = true,
                    ActionTitle = rec.Kind switch { DecisionContext.KindMulligan => "MULLIGAN", DecisionContext.KindChoice => "CHOIX", _ => "ACTION IMMÉDIATE" },
                    ActionText = rec.Kind == DecisionContext.KindMulligan ? MulliganText(rec, u.State) : Label(rec.Action, u.State),
                    VerifiedText = rec.Kind == DecisionContext.KindAction ? "✔ action acceptée par le serveur à cet instant" : "✔ option proposée par le jeu",
                    SummaryText = rec.Summary,
                    PlanText = rec.PlanNextTurn ?? "",
                    WhyText = rec.Why ?? "",
                    Next = NextSteps(rec),
                    CautionText = string.Join("\n", new[] { danger, rec.Caution }.Where(c => !string.IsNullOrEmpty(c))),
                    LethalText = lethalText,
                    LethalTone = lethalTone,
                    StatusText = rec.Origin switch
                    {
                        AdviceOrigins.Solver => "Létal démontré par le calcul local, sans IA ni coût · séquence recalculée à chaque action",
                        AdviceOrigins.Plan => $"Suite du plan précédent, sans nouvel appel : rien de nouveau depuis · confiance {Confidence(rec.Uncertainty)}",
                        _ => $"Conseil valable pour l'état actuel · confiance {Confidence(rec.Uncertainty)}" + (rec.Reused ? " · repris sans nouvel appel" : ""),
                    },
                    FooterText = string.Format(Fr, "Révision {0} · {1:0.0} s · {2:0.000} $ · partie {3:0.00} $ · {4} analyse(s) ce tour · {5}",
                        rec.Revision, rec.LatencyMs / 1000, rec.CostUsd ?? 0, u.CostThisGameUsd, u.CallsThisTurn, rec.Model ?? rec.Provider),
                };
            case AdvisorPhases.Partial when u.PartialAction is { } action:
                return new AdviceView
                {
                    Tone = "busy",
                    HasAdvice = true,
                    ActionTitle = u.DecisionKind == DecisionContext.KindChoice ? "CHOIX" : "ACTION IMMÉDIATE",
                    ActionText = Label(action, u.State),
                    VerifiedText = u.DecisionKind == DecisionContext.KindAction ? "✔ action acceptée par le serveur · conseil en cours de rédaction" : "✔ option proposée par le jeu · conseil en cours",
                    SummaryText = u.Partial?.Summary ?? "",
                    PlanText = u.Partial?.PlanNextTurn ?? "",
                    WhyText = u.Partial?.Why ?? "",
                    LethalText = lethalText,
                    LethalTone = lethalTone,
                    CautionText = danger,
                    StatusText = string.Format(Fr, "Réponse en cours… {0:0.0} s", elapsed),
                    FooterText = string.Format(Fr, "{0} analyse(s) ce tour · partie {1:0.00} $", u.CallsThisTurn, u.CostThisGameUsd),
                };
            case AdvisorPhases.Analyzing:
                return new AdviceView
                {
                    Tone = "busy",
                    ActionTitle = "ANALYSE",
                    ActionText = string.Format(Fr, "{0} {1:0.0} s", u.Manual ? "Réanalyse en cours…" : "Analyse en cours…", elapsed),
                    LethalText = lethalText,
                    LethalTone = lethalTone,
                    CautionText = danger,
                    Facts = facts,
                    StatusText = "Analyse rapide affichée en attendant le conseil.",
                    FooterText = string.Format(Fr, "{0} analyse(s) ce tour · partie {1:0.00} $", u.CallsThisTurn, u.CostThisGameUsd),
                };
            default:
                return new AdviceView
                {
                    Tone = u.Phase switch
                    {
                        AdvisorPhases.Error => "error",
                        AdvisorPhases.Stale or AdvisorPhases.Skipped or AdvisorPhases.Paused or AdvisorPhases.Disabled => "warn",
                        _ => "idle",
                    },
                    ActionTitle = Title(u),
                    ActionText = "",
                    LethalText = lethalText,
                    LethalTone = lethalTone,
                    CautionText = danger,
                    Facts = facts,
                    StatusText = u.Message,
                    FooterText = u.Reason == AdvisorReasons.Quota
                        ? string.Format(Fr, "{0}/{1} analyses automatiques de milieu de tour · début de tour, choix et réanalyse manuelle toujours possibles · partie {2:0.00} $", u.MidTurnCallsThisTurn, u.MaxCallsPerTurn, u.CostThisGameUsd)
                        : u.CostSessionUsd > 0 ? string.Format(Fr, "Session : {0:0.000} $", u.CostSessionUsd) : "",
                };
        }
    }

    /// <summary>Titre d'un état sans conseil : dit pourquoi, plutôt qu'un « en attente » générique.</summary>
    public static string Title(AdvisorUpdate u) => u.Phase switch
    {
        AdvisorPhases.Stale => "CONSEIL RETIRÉ",
        AdvisorPhases.Paused => "PAUSE",
        AdvisorPhases.Disabled => u.Reason == AdvisorReasons.Ranked ? "DÉSACTIVÉ EN CLASSÉ" : "DÉSACTIVÉ",
        AdvisorPhases.Error => u.Reason switch
        {
            AdvisorReasons.Timeout => "PAS DE RÉPONSE",
            AdvisorReasons.Leak => "ENVOI BLOQUÉ",
            _ => "ERREUR",
        },
        AdvisorPhases.Skipped => u.Reason switch
        {
            AdvisorReasons.Quota => "ANALYSES AUTO ÉPUISÉES",
            AdvisorReasons.Budget or AdvisorReasons.GameLimit => "BUDGET ATTEINT",
            AdvisorReasons.EndTurnOnly => "FIN DE TOUR",
            AdvisorReasons.CatchUp => "RATTRAPAGE",
            AdvisorReasons.Incomplete => "ÉTAT INCOMPLET",
            AdvisorReasons.NoAction => "AUCUNE ACTION",
            _ => "SANS CONSEIL",
        },
        _ => u.Reason switch
        {
            AdvisorReasons.OpponentTurn => "TOUR ADVERSE",
            AdvisorReasons.GameOver => "PARTIE TERMINÉE",
            _ => "EN ATTENTE",
        },
    };

    private static IReadOnlyList<string> NextSteps(Recommendation rec)
    {
        var shown = rec.Origin == AdviceOrigins.Solver ? 4 : 2;
        return rec.FollowUp.Take(shown).Select((f, i) => $"{i + 2}. {f.Description}")
            .Concat(rec.FollowUp.Count > shown ? new[] { $"(+{rec.FollowUp.Count - shown} autre(s) étape(s) dans l'onglet « Conseiller »)" } : Array.Empty<string>()).ToList();
    }

    private static (string? Text, string? Tone) Lethal(AdvisorUpdate u)
    {
        if (u.Phase == AdvisorPhases.Ready && u.Recommendation is { } rec && LethalLevels.Normalize(rec.Lethal) is var level && level != LethalLevels.None)
        {
            var title = level switch
            {
                LethalLevels.Certain => "Létal certain",
                LethalLevels.Conditional => "Létal conditionnel",
                _ => "Létal plausible, non démontré",
            };
            return (rec.LethalNote ?? $"{title} : annoncé par le conseil, non démontré par le calcul local.", level);
        }
        if (u.Lethal is { } l && l.Level != LethalLevels.None && l.Label is string label) return (label, l.Level);
        return (null, null);
    }

    private static string Confidence(UncertaintyLevel level) => level switch
    {
        UncertaintyLevel.Faible => "élevée",
        UncertaintyLevel.Moyenne => "moyenne",
        _ => "faible",
    };

    /// <summary>Libellé de l'action avec les statistiques actuelles de la cible (« → Arcanologue 2/3 »).</summary>
    public static string Label(LegalAction action, GameStateSnapshot? state)
    {
        if (action.TargetEntityId is int id && state is not null)
        {
            var m = (state.Local?.Board ?? new()).Concat(state.Opponent?.Board ?? new()).FirstOrDefault(x => x.EntityId == id);
            if (m is not null && m.Type == "MINION") return $"{action.Label} {m.Attack}/{m.Health}";
            var hero = state.Opponent?.Hero?.EntityId == id ? state.Opponent.Hero : state.Local?.Hero?.EntityId == id ? state.Local.Hero : null;
            if (hero is not null) return $"{action.Label} ({hero.Health} PV{(hero.Armor > 0 ? $" + {hero.Armor} armure" : "")})";
        }
        return action.Label;
    }

    private static string MulliganText(Recommendation rec, GameStateSnapshot? state)
    {
        if (rec.ReplaceActions.Count == 0) return "Garder toute la main";
        var replaced = rec.ReplaceActions.Select(a => a.SourceName ?? "?").ToList();
        var offered = state?.PendingChoice?.Offered.Select(o => (o.EntityId, Name: o.Name ?? "?")).ToList() ?? new();
        var kept = offered.Where(o => rec.ReplaceActions.All(a => a.SourceEntityId != o.EntityId)).Select(o => o.Name).ToList();
        return $"Remplacer : {string.Join(", ", replaced)}" + (kept.Count > 0 ? $"\nGarder : {string.Join(", ", kept)}" : "");
    }
}
