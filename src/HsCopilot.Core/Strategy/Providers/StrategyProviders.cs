using HsCopilot.Core.Config;

namespace HsCopilot.Core.Strategy.Providers;

/// <summary>
/// Choix du fournisseur selon le modèle : un modèle « claude-… » passe par l'API Anthropic (clé ANTHROPIC_API_KEY),
/// tout autre par l'API OpenAI (clé OPENAI_API_KEY). Message, schéma de sortie, validation et budgets sont les mêmes.
/// </summary>
public static class StrategyProviders
{
    public static bool IsAnthropic(string model) => model.StartsWith("claude-", StringComparison.OrdinalIgnoreCase);

    public static string ProviderName(string model) => IsAnthropic(model) ? "Anthropic" : "OpenAI";

    public static string KeyVariable(string model) => IsAnthropic(model) ? AnthropicOptions.KeyVariable : "OPENAI_API_KEY";

    public static (bool Present, string? Hint) KeyStatus(string model) => IsAnthropic(model) ? AnthropicOptions.KeyStatus() : OpenAiOptions.KeyStatus();

    public static IStrategyProvider Create(AdvisorSettings settings, string instructions) =>
        IsAnthropic(settings.EffectiveModel)
            ? new AnthropicMessagesProvider(settings.ToAnthropicOptions(), instructions)
            : new OpenAiResponsesProvider(settings.ToOpenAiOptions(), instructions);

    public static Task<PingResult> PingAsync(AdvisorSettings settings, CancellationToken cancellationToken) =>
        IsAnthropic(settings.EffectiveModel)
            ? new AnthropicMessagesProvider(settings.ToAnthropicOptions(), "").PingAsync(cancellationToken)
            : new OpenAiResponsesProvider(settings.ToOpenAiOptions(), "").PingAsync(cancellationToken);
}
