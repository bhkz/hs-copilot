using System.Text.Json;
using System.Text.Json.Serialization;
using HsCopilot.Core.State;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Strategy;

// Contrats du Strategy Engine (préparation du MVP 3). Quatre couches strictement séparées :
//   1. état observable + hypothèses étiquetées (DecisionContext.State / Hypotheses)
//   2. actions autorisées maintenant par le serveur (DecisionContext.LegalActions)
//   3. séquences vérifiées par notre moteur de règles (DecisionContext.VerifiedSequences : vide tant qu'il n'existe pas)
//   4. recommandation produite par un fournisseur (LLM) : StrategyOutput, puis Recommendation après validation.
// Un fournisseur ne peut désigner qu'une action par son identifiant ; la partie, la révision et les options sont
// imposées par le coordinateur, jamais recopiées depuis la réponse du modèle.

/// <summary>Action acceptée par le serveur dans la situation actuelle (couche 2).</summary>
public sealed class LegalAction
{
    /// <summary>Identifiant stable dans la révision : <c>o{options}.{option}[.s{sous-option}][.t{cible}]</c>, par ex. <c>o9.5.t21</c>.</summary>
    public required string Id { get; init; }
    public required int OptionsId { get; init; }
    public required int OptionIndex { get; init; }
    public int? SubOptionIndex { get; init; }
    public int? TargetEntityId { get; init; }
    /// <summary>play, attack, hero-power, location, end-turn, other ; pour un choix : choose, mulligan-replace, mulligan-keep.</summary>
    public required string Kind { get; init; }
    public int? SourceEntityId { get; init; }
    public string? SourceCardId { get; init; }
    public string? SourceName { get; init; }
    public string? SubOptionName { get; init; }
    public string? TargetCardId { get; init; }
    public string? TargetName { get; init; }
    /// <summary>local, opponent ou unknown.</summary>
    public string? TargetSide { get; init; }
    /// <summary>Coût affiché pour une carte de la main.</summary>
    public int? ManaCost { get; init; }
    /// <summary>Libellé français lisible, par ex. « Activer Profondeurs Sanguines → Chevalier floramer ».</summary>
    public required string Label { get; init; }
}

/// <summary>Hypothèse explicitement étiquetée (jamais une certitude).</summary>
public sealed class Hypothesis
{
    public required string Subject { get; init; }
    public required string Statement { get; init; }
    public required string Basis { get; init; }
}

/// <summary>Séquence d'actions dont la légalité a été démontrée par le moteur de règles (couche 3). Vide au MVP 1.</summary>
public sealed class VerifiedSequence
{
    public required IReadOnlyList<string> ActionIds { get; init; }
    public required string Verifier { get; init; }
}

/// <summary>Tout ce qu'un fournisseur de stratégie reçoit pour décider.</summary>
public sealed class DecisionContext
{
    public const string SequenceVerificationUnavailable =
        "indisponible : moteur de règles multi-actions non implémenté ; seule la première action est garantie légale par le serveur";

    public const string KindAction = "action";
    public const string KindMulligan = "mulligan";
    public const string KindChoice = "choice";

    public required string GameId { get; init; }
    public required int Revision { get; init; }
    /// <summary>Identifiant du bloc d'options (action) ou du choix (mulligan, Découverte, Rembobinage).</summary>
    public required int OptionsId { get; init; }
    public required int GameTurn { get; init; }
    public required int ResetCount { get; init; }
    public required int HistorySeq { get; init; }
    public required GameStateSnapshot State { get; init; }
    /// <summary>action (tour normal), mulligan ou choice (Découverte, Rembobinage, autre choix proposé par le jeu).</summary>
    public string Kind { get; init; } = KindAction;
    /// <summary>Type de choix du jeu (MULLIGAN, GENERAL…) pour un mulligan ou un choix.</summary>
    public string? ChoiceType { get; init; }
    public string? ChoiceSourceName { get; init; }
    public List<Hypothesis> Hypotheses { get; init; } = new();
    public List<LegalAction> LegalActions { get; init; } = new();
    public List<VerifiedSequence> VerifiedSequences { get; init; } = new();
    public string SequenceVerification { get; init; } = SequenceVerificationUnavailable;
    /// <summary>Vrai si une information adverse est inconnue (main cachée, Secret non révélé) : toute certitude est interdite.</summary>
    public bool OpponentInformationHidden { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    /// <summary>
    /// Calcul local joint par le coordinateur (solveur des dégâts, ordre, létal) : sur l'état visible uniquement, aucune
    /// information cachée. Null hors tour normal.
    /// </summary>
    public object? LocalAnalysis { get; set; }
    /// <summary>Conseil précédent du même tour (action, plan) pour que le modèle reste cohérent avec lui ou explique l'écart.</summary>
    public object? PreviousAdvice { get; set; }

    public bool IsRanked => State.Game.GameType == "GT_RANKED";

    /// <summary>Message JSON qui serait envoyé à un fournisseur externe (sans l'historique complet, limité aux 30 dernières actions).</summary>
    public string ToPayloadJson()
    {
        var payload = new
        {
            gameId = GameId,
            revision = Revision,
            optionsId = OptionsId,
            decisionKind = Kind,
            choiceType = ChoiceType,
            choiceSource = ChoiceSourceName,
            state = State,
            hypotheses = Hypotheses,
            legalActions = LegalActions,
            verifiedSequences = VerifiedSequences,
            sequenceVerification = SequenceVerification,
            opponentInformationHidden = OpponentInformationHidden,
            localAnalysis = LocalAnalysis,
            previousAdvice = PreviousAdvice,
            rules = new[]
            {
                "Choisir exactement une action parmi legalActions par son id.",
                "Ne jamais présenter une suite de plusieurs actions comme vérifiée.",
                "Aucune certitude sur les cartes adverses inconnues.",
            },
        };
        return JsonSerializer.Serialize(payload, Json.Compact);
    }
}

/// <summary>Réponse attendue d'un fournisseur (contrat de sortie structurée du LLM, voir docs/strategy-output.schema.json).</summary>
public sealed class StrategyOutput
{
    [JsonPropertyName("actionId")] public string ActionId { get; set; } = "";
    /// <summary>Raison courte (15 mots au plus) affichée sous l'action dans l'overlay.</summary>
    [JsonPropertyName("why")] public string? Why { get; set; }
    /// <summary>aucun, visible (attaques sur l'état visible, aucun Secret), conditionnel (Secret, carte cachée, hasard) ou plausible (plusieurs cartes, non vérifié).</summary>
    [JsonPropertyName("lethal")] public string Lethal { get; set; } = "aucun";
    /// <summary>Mulligan seulement : identifiants des cartes à remplacer (vide : garder toute la main).</summary>
    [JsonPropertyName("replaceIds")] public List<string> ReplaceIds { get; set; } = new();
    /// <summary>« élevée », « moyenne » ou « faible » : confiance du fournisseur dans son choix.</summary>
    [JsonPropertyName("confidence")] public string Confidence { get; set; } = "faible";
    [JsonPropertyName("uncertaintyReasons")] public List<string> UncertaintyReasons { get; set; } = new();
    /// <summary>Conseil principal, 20 mots au plus.</summary>
    [JsonPropertyName("summary")] public string Summary { get; set; } = "";
    [JsonPropertyName("rationale")] public string Rationale { get; set; } = "";
    [JsonPropertyName("rejectedAlternativeId")] public string? RejectedAlternativeId { get; set; }
    [JsonPropertyName("rejectedAlternativeReason")] public string? RejectedAlternativeReason { get; set; }
    [JsonPropertyName("risk")] public string? Risk { get; set; }
    /// <summary>Plan du prochain tour, une ligne.</summary>
    [JsonPropertyName("planNextTurn")] public string? PlanNextTurn { get; set; }
    /// <summary>Étapes envisagées après l'action immédiate : toujours considérées comme non vérifiées.</summary>
    [JsonPropertyName("followUp")] public List<FollowUpStep> FollowUp { get; set; } = new();
}

/// <summary>
/// Étape envisagée après l'action immédiate. Les entités désignent la carte ou le serviteur source et la cible (null si
/// aucune) : elles permettent de reprendre le plan sans nouvel appel quand rien de nouveau n'est apparu.
/// </summary>
[JsonConverter(typeof(FollowUpStepConverter))]
public sealed class FollowUpStep
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("sourceEntityId")] public int? SourceEntityId { get; set; }
    [JsonPropertyName("targetEntityId")] public int? TargetEntityId { get; set; }

    public static implicit operator FollowUpStep(string text) => new() { Text = text };
    public override string ToString() => Text;
}

/// <summary>Accepte l'ancien format (chaîne seule) et le nouveau (objet texte + entités).</summary>
public sealed class FollowUpStepConverter : JsonConverter<FollowUpStep>
{
    public override FollowUpStep? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String) return new FollowUpStep { Text = reader.GetString() ?? "" };
        if (reader.TokenType == JsonTokenType.Null) return null;
        using var doc = JsonDocument.ParseValue(ref reader);
        var e = doc.RootElement;
        int? Int(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;
        return new FollowUpStep
        {
            Text = e.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "",
            SourceEntityId = Int("sourceEntityId"),
            TargetEntityId = Int("targetEntityId"),
        };
    }

    public override void Write(Utf8JsonWriter writer, FollowUpStep value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("text", value.Text);
        if (value.SourceEntityId is int s) writer.WriteNumber("sourceEntityId", s); else writer.WriteNull("sourceEntityId");
        if (value.TargetEntityId is int t) writer.WriteNumber("targetEntityId", t); else writer.WriteNull("targetEntityId");
        writer.WriteEndObject();
    }
}

public enum UncertaintyLevel
{
    Faible,
    Moyenne,
    Elevee,
}

public sealed class PlannedStep
{
    public required string Description { get; init; }
    /// <summary>Carte ou serviteur source de l'étape (null si le modèle ne l'a pas désigné).</summary>
    public int? SourceEntityId { get; init; }
    public int? TargetEntityId { get; init; }
    /// <summary>« non vérifié » pour une étape du modèle ; « calculé » pour une étape du solveur local.</summary>
    public string Status { get; init; } = "non vérifié";
}

/// <summary>Recommandation validée, liée à une version précise de l'état (couche 4).</summary>
public sealed class Recommendation
{
    /// <summary>action, mulligan ou choice (voir <see cref="DecisionContext.Kind"/>).</summary>
    public string Kind { get; init; } = DecisionContext.KindAction;
    public required string GameId { get; init; }
    public required int Revision { get; init; }
    public required int OptionsId { get; init; }
    public required int GameTurn { get; init; }
    public required int ResetCountAtCreation { get; init; }
    public required int HistorySeqAtCreation { get; init; }
    public required LegalAction Action { get; init; }
    public required UncertaintyLevel Uncertainty { get; init; }
    public List<string> UncertaintyReasons { get; init; } = new();
    public required string Summary { get; init; }
    /// <summary>Raison courte pour l'overlay.</summary>
    public string? Why { get; init; }
    /// <summary>Niveau de létal retenu après contrôle par le solveur : aucun, certain, conditionnel, plausible.</summary>
    public string Lethal { get; init; } = "aucun";
    /// <summary>Ce que le calcul déterministe voit (létal par attaques, Secret adverse…), affiché à côté du conseil.</summary>
    public string? LethalNote { get; init; }
    /// <summary>Mulligan : cartes à remplacer (vide : garder toute la main).</summary>
    public List<LegalAction> ReplaceActions { get; init; } = new();
    /// <summary>
    /// Mise en garde locale, sans IA, affichée sous le conseil : fin de tour alors que des attaques ou le pouvoir héroïque
    /// restent possibles, attaque avant un bonus d'ATQ disponible (Profondeurs Sanguines), létal démontré non suivi. Vide sinon.
    /// </summary>
    public string? Caution { get; init; }
    public string Rationale { get; init; } = "";
    public LegalAction? RejectedAlternative { get; init; }
    public string? RejectedAlternativeReason { get; init; }
    public string? Risk { get; init; }
    public string? PlanNextTurn { get; init; }
    public List<PlannedStep> FollowUp { get; init; } = new();
    /// <summary>Ce qui est garanti : uniquement la légalité de l'action immédiate, à cette révision.</summary>
    public string Verification => Kind == DecisionContext.KindAction
        ? $"action immédiate acceptée par le serveur (options #{OptionsId}, révision {Revision}) ; étapes suivantes non vérifiées"
        : $"option proposée par le jeu (choix #{OptionsId}, révision {Revision})";
    public List<string> Warnings { get; init; } = new();
    public required string Provider { get; init; }
    public string? Model { get; init; }
    public double LatencyMs { get; init; }
    public double? CostUsd { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    /// <summary>Vrai si la recommandation a été reportée sur un état équivalent sans nouvel appel au modèle.</summary>
    public bool Reused { get; init; }
    /// <summary>modèle, solveur (létal démontré localement) ou plan (étape suivante du conseil précédent, sans appel).</summary>
    public string Origin { get; set; } = "modèle";

    /// <summary>
    /// Même conseil rattaché à un nouveau contexte strictement équivalent (mêmes actions, même plateau) : le serveur a renvoyé
    /// des options identiques sous un nouvel identifiant. Renvoie null si l'action n'existe plus telle quelle.
    /// </summary>
    public Recommendation? RebindTo(DecisionContext ctx)
    {
        LegalAction? Find(LegalAction? a) => a is null ? null : ctx.LegalActions.FirstOrDefault(x =>
            x.Kind == a.Kind && x.SourceEntityId == a.SourceEntityId && x.SubOptionIndex == a.SubOptionIndex && x.TargetEntityId == a.TargetEntityId);
        var action = Find(Action);
        if (action is null || ctx.Kind != Kind) return null;
        return new Recommendation
        {
            Kind = Kind, GameId = ctx.GameId, Revision = ctx.Revision, OptionsId = ctx.OptionsId, GameTurn = ctx.GameTurn,
            ResetCountAtCreation = ctx.ResetCount, HistorySeqAtCreation = ctx.HistorySeq, Action = action, Uncertainty = Uncertainty,
            UncertaintyReasons = UncertaintyReasons, Summary = Summary, Why = Why, Lethal = Lethal, LethalNote = LethalNote, Caution = Caution,
            ReplaceActions = ReplaceActions.Select(Find).Where(x => x is not null).Select(x => x!).ToList(), Rationale = Rationale,
            RejectedAlternative = Find(RejectedAlternative), RejectedAlternativeReason = RejectedAlternativeReason, Risk = Risk,
            PlanNextTurn = PlanNextTurn, FollowUp = FollowUp, Warnings = Warnings, Provider = Provider, Model = Model,
            LatencyMs = 0, CostUsd = 0, Reused = true, Origin = Origin,
        };
    }
}

public sealed record StrategyResult
{
    public StrategyOutput? Output { get; init; }
    public string? Error { get; init; }
    public string? Model { get; init; }
    public int InputTokens { get; init; }
    /// <summary>Jetons d'entrée servis depuis le cache du fournisseur (facturés moins cher).</summary>
    public int CachedInputTokens { get; init; }
    /// <summary>Jetons de sortie facturés, raisonnement compris.</summary>
    public int OutputTokens { get; init; }
    public int ReasoningTokens { get; init; }
    public double? CostUsd { get; init; }
    /// <summary>Durée de l'appel côté client (réseau compris).</summary>
    public double LatencyMs { get; init; }
    public int Retries { get; init; }
    public string? ResponseId { get; init; }
    /// <summary>Texte brut renvoyé par le modèle, pour l'examen.</summary>
    public string? RawOutput { get; init; }
    /// <summary>Flux : délai jusqu'au premier fragment de texte reçu.</summary>
    public double? FirstTokenMs { get; init; }
    /// <summary>Flux : délai jusqu'à ce que l'action et le conseil soient lisibles.</summary>
    public double? PartialMs { get; init; }
}

/// <summary>Début de réponse lu pendant le flux : de quoi afficher l'action avant la fin de la génération.</summary>
public sealed record StrategyPartial
{
    public required string ActionId { get; init; }
    public string? Summary { get; init; }
    public string? Why { get; init; }
    public string? Lethal { get; init; }
    public string? PlanNextTurn { get; init; }
    public double ElapsedMs { get; init; }
}

/// <summary>Fournisseur capable de transmettre le début de sa réponse pendant la génération (flux).</summary>
public interface IStreamingStrategyProvider
{
    Task<StrategyResult> ProposeStreamingAsync(DecisionContext context, Action<StrategyPartial> onPartial, CancellationToken cancellationToken);
}

/// <summary>Fournisseur de stratégie (LLM au MVP 3). Doit respecter l'annulation.</summary>
public interface IStrategyProvider
{
    string Name { get; }
    Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken);
}

/// <summary>Fournisseur capable de recevoir le message exact déjà contrôlé (aucune divergence entre message vérifié et message envoyé).</summary>
public interface IPayloadStrategyProvider
{
    Task<StrategyResult> ProposeWithPayloadAsync(string payload, CancellationToken cancellationToken);
}
