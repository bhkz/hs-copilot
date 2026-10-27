using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;

namespace HsCopilot.Core.Config;

/// <summary>
/// Réglages du conseiller en direct (section "advisor" de copilot.json). Aucune clé n'y figure : elles sont lues dans les
/// variables d'environnement OPENAI_API_KEY ou ANTHROPIC_API_KEY (modèle « claude-… »). Modèle et effort : valeur du
/// fichier, sinon HSCOPILOT_OPENAI_MODEL et HSCOPILOT_OPENAI_EFFORT, sinon gpt-6.1-sol et low.
/// </summary>
public sealed class AdvisorSettings
{
    public const string DefaultModel = "gpt-6.1-sol";
    public const string DefaultEffort = "low";
    public static readonly string[] Efforts = { "low", "medium", "high" };

    public bool Enabled { get; set; } = true;
    /// <summary>
    /// openai (par défaut : le fournisseur suit le modèle, « claude-… » pour Anthropic), ou simulated : latence simulée sans
    /// appel ni stratégie, pour essayer l'overlay gratuitement.
    /// </summary>
    public string Provider { get; set; } = "openai";
    /// <summary>Mode démo : latence simulée moyenne en millisecondes.</summary>
    public int SimulatedLatencyMs { get; set; } = 10000;

    public bool IsSimulated => string.Equals(Provider, "simulated", StringComparison.OrdinalIgnoreCase);
    public string? Model { get; set; }
    public string? Effort { get; set; }
    /// <summary>Délai maximal d'une analyse ; au-delà, aucun conseil pour cette situation.</summary>
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxCallsPerTurn { get; set; } = 3;
    public int MaxCallsPerGame { get; set; } = 50;
    public double MaxCostPerGameUsd { get; set; } = 0.75;
    public double MaxCostPerSessionUsd { get; set; } = 3.0;
    /// <summary>Attente avant un appel, pour absorber les rafales d'options du serveur.</summary>
    public int DebounceMs { get; set; } = 300;
    /// <summary>Attente avant d'analyser une décision qui n'est pas la première du tour (enchaînements rapides).</summary>
    public int MidTurnDebounceMs { get; set; } = 2500;
    /// <summary>Afficher l'action dès qu'elle est lisible dans la réponse en flux.</summary>
    public bool Streaming { get; set; } = true;
    /// <summary>Conseiller aussi le mulligan, les Découvertes et les Rembobinages.</summary>
    public bool AdviseChoices { get; set; } = true;
    /// <summary>Conseils en temps réel en partie classée : désactivés par défaut, modifiable uniquement dans le fichier.</summary>
    public bool AllowRanked { get; set; }
    public int HistoryItems { get; set; } = PromptBuilder.DefaultHistoryItems;
    public int MaxOutputTokens { get; set; } = 6000;
    /// <summary>Fichier de stratégie (vide : config/strategy/guerrier-dragons.md).</summary>
    public string? StrategyFile { get; set; }

    public bool OverlayEnabled { get; set; } = true;
    public bool OverlayCompact { get; set; }
    public double? OverlayLeft { get; set; }
    public double? OverlayTop { get; set; }
    public double OverlayOpacity { get; set; } = 0.93;
    /// <summary>Comportement de la version testée le 25/10/2026 (aucune priorité, analyse possible pendant un rattrapage) : mesure avant/après uniquement.</summary>
    public bool LegacyQuota { get; set; }
    /// <summary>Un appel annulé rend son quota par tour : un peu plus de décisions couvertes, environ 45 % d'appels en plus.</summary>
    public bool RefundCancelledCalls { get; set; }
    /// <summary>
    /// Comportement de la version de la 2e partie réelle (25/10/2026, 16 h 37) : début de tour, choix et réanalyses comptés
    /// dans le quota par tour, ni létal local ni suite du plan sans appel. Mesure avant/après uniquement.
    /// </summary>
    public bool PreviousVersion { get; set; }
    /// <summary>Annuler l'appel en cours quand tu joues l'action de son début de réponse (comportement avant le 25/10/2026 au soir).</summary>
    public bool DropCallWhenPartialPlayed { get; set; }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    public string EffectiveModel => Model ?? Env("HSCOPILOT_OPENAI_MODEL") ?? DefaultModel;
    public string EffectiveEffort => Effort ?? Env("HSCOPILOT_OPENAI_EFFORT") ?? DefaultEffort;

    public string ModelSource => Model is not null ? "réglages" : Env("HSCOPILOT_OPENAI_MODEL") is not null ? "variable HSCOPILOT_OPENAI_MODEL" : "par défaut";
    public string EffortSource => Effort is not null ? "réglages" : Env("HSCOPILOT_OPENAI_EFFORT") is not null ? "variable HSCOPILOT_OPENAI_EFFORT" : "par défaut";

    /// <summary>Coût estimé d'un appel (≈ 9 000 jetons d'entrée et 1 500 de sortie), réservé avant chaque envoi.</summary>
    public double EstimatedCallCostUsd => ModelPricing.Cost(EffectiveModel, 9000, 1600, 1500) ?? 0.05;

    /// <summary>Coût estimé d'un appel interrompu : les jetons d'entrée seulement.</summary>
    public double EstimatedCancelledCallCostUsd => ModelPricing.Cost(EffectiveModel, 9000, 1600, 0) ?? 0.02;

    public StrategyOptions ToStrategyOptions() => new()
    {
        Timeout = TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds)),
        AllowRanked = AllowRanked,
        Debounce = TimeSpan.FromMilliseconds(Math.Max(0, DebounceMs)),
        MidTurnDebounce = TimeSpan.FromMilliseconds(Math.Max(0, MidTurnDebounceMs)),
        CancelledCallCostUsd = IsSimulated ? 0 : EstimatedCancelledCallCostUsd,
        MaxCallsPerTurn = Math.Max(1, MaxCallsPerTurn),
        MaxCallsPerGame = Math.Max(1, MaxCallsPerGame),
        MaxCostPerGameUsd = MaxCostPerGameUsd,
        MaxCostPerSessionUsd = MaxCostPerSessionUsd,
        ReservePerCallUsd = IsSimulated ? 0 : EstimatedCallCostUsd,
        AdviseChoices = AdviseChoices,
        UseStreaming = Streaming,
        RequireLiveState = !LegacyQuota,
        CancelledCallsUseTurnCap = LegacyQuota || !RefundCancelledCalls,
        PriorityDecisionsBypassTurnCap = !LegacyQuota,
        PriorityCallsUseTurnCap = LegacyQuota || PreviousVersion,
        LocalLethal = !LegacyQuota && !PreviousVersion,
        ContinuePlan = !LegacyQuota && !PreviousVersion,
        KeepCallWhenPartialPlayed = !LegacyQuota && !PreviousVersion && !DropCallWhenPartialPlayed,
    };

    public AnthropicOptions ToAnthropicOptions() => new()
    {
        Model = EffectiveModel,
        Effort = EffectiveEffort,
        BaseUrl = Env("HSCOPILOT_ANTHROPIC_BASE_URL") ?? "https://api.anthropic.com/v1",
        Timeout = TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds) + 10),
        // La réflexion compte dans max_tokens et reste active sur Claude Opus 5.5 : plafond plus large qu'avec OpenAI.
        MaxTokens = Math.Max(MaxOutputTokens, 16000),
        MaxRetries = 1,
        RetryBaseDelay = TimeSpan.FromSeconds(1),
        HistoryItems = HistoryItems,
    };

    public OpenAiOptions ToOpenAiOptions() => new()
    {
        Model = EffectiveModel,
        ReasoningEffort = EffectiveEffort,
        BaseUrl = Env("HSCOPILOT_OPENAI_BASE_URL") ?? "https://api.openai.com/v1",
        Timeout = TimeSpan.FromSeconds(Math.Max(5, TimeoutSeconds) + 10),
        MaxOutputTokens = MaxOutputTokens,
        MaxRetries = 1,
        RetryBaseDelay = TimeSpan.FromSeconds(1),
        HistoryItems = HistoryItems,
    };
}
