namespace HsCopilot.Core.Cards;

/// <summary>Règles de cartes partagées par le moteur, le conseiller et les outils.</summary>
public static class CardRules
{
    /// <summary>
    /// La pièce (GAME_005 et ses 65 apparences *_COIN* de la base 255130). Au mulligan du second joueur, elle figure parmi
    /// les cartes proposées mais n'est jamais remplacée (vérifié le 25/10/2026 : non choisie, elle reste en main).
    /// </summary>
    public static bool IsCoin(string? cardId) =>
        cardId is not null && (cardId == "GAME_005" || cardId.Contains("COIN", StringComparison.Ordinal)) && !cardId.EndsWith('e');
}
