using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;

namespace HsCopilot.Core.State;

/// <summary>Sombre don attaché à une carte proposée par une Découverte (tag DARK_GIFT_ENTITY du serveur).</summary>
public sealed class DarkGiftView
{
    public required string CardId { get; init; }
    public string? Name { get; init; }
    /// <summary>Texte officiel du don (base de cartes).</summary>
    public string? Text { get; init; }
    /// <summary>La carte telle qu'elle sera avec ce don, pour les dons de la table ; null pour un don inconnu.</summary>
    public GiftedCardView? Result { get; init; }
}

/// <summary>Coût, statistiques et mots-clés d'une carte proposée une fois son Sombre don appliqué.</summary>
public sealed class GiftedCardView
{
    public int? Cost { get; init; }
    public int? Attack { get; init; }
    public int? Health { get; init; }
    /// <summary>Mots-clés ajoutés par le don (CHARGE, TAUNT…).</summary>
    public List<string> AddedKeywords { get; init; } = new();
    /// <summary>Effet sans traduction en statistiques (Cri de guerre doublé, copie, placement dans le deck…).</summary>
    public string? Note { get; init; }
}

/// <summary>
/// Sombres dons (extension Into the Emerald Dream). Le don de chaque carte proposée est lu dans le log : le serveur relie la
/// carte à une entité « don » par le tag DARK_GIFT_ENTITY, et cette entité porte la carte EDR_100t… du don. Rien n'est
/// déduit du nom du serviteur. La table ci-dessous traduit le texte officiel de chaque don (base de cartes 255130) ; elle a
/// été vérifiée sur les Découvertes réelles des journaux du joueur (statistiques de la carte une fois arrivée en main).
/// </summary>
public static class DarkGifts
{
    private sealed record Effect(int Cost = 0, int Attack = 0, int Health = 0, string[]? Keywords = null, string? Note = null);

    private static readonly Dictionary<string, Effect> Known = new(StringComparer.Ordinal)
    {
        ["EDR_100t"] = new(Attack: 3, Keywords: new[] { "LIFESTEAL" }),                       // Terreur en éveil
        ["EDR_100t1"] = new(Attack: 2, Health: 2, Keywords: new[] { "ELUSIVE" }),             // Repos serein
        ["EDR_100t2"] = new(Cost: -2, Attack: -2),                                            // Courtes griffes
        ["EDR_100t3"] = new(Health: 4, Keywords: new[] { "TAUNT" }),                          // Posture de défense
        ["EDR_100t4"] = new(Keywords: new[] { "DEATHRATTLE" }, Note: "Râle d'agonie : tu pioches 2 cartes"), // Démons intérieurs
        ["EDR_100t5"] = new(Note: "quand tu le joues, une copie 2/2 est aussi invoquée"),     // Cauchemar vivant
        ["EDR_100t6"] = new(Keywords: new[] { "CHARGE" }),                                    // Somnambule
        ["EDR_100t7"] = new(Note: "ses Cris de guerre se déclenchent deux fois"),             // Réveil brutal
        ["EDR_100t8"] = new(Attack: 4, Health: 5, Note: "placé sur le dessus de ton deck, pas dans ta main : ce sera ta prochaine pioche"), // Beaux rêves
        ["EDR_100t9"] = new(Keywords: new[] { "REBORN" }, Note: "réincarné avec tous ses PV et enchantements"), // Horreur persistante
        ["EDR_100t10"] = new(Keywords: new[] { "DIVINE_SHIELD" }, Note: "Bouclier divin qui se brise après 3 coups"), // Écailles du Cauchemar
        ["EDR_100t13"] = new(Keywords: new[] { "DIVINE_SHIELD", "WINDFURY" }),                // Serres de harpie
    };

    /// <summary>Dons dont l'effet est traduit en statistiques ou en note.</summary>
    public static IReadOnlyCollection<string> KnownGiftIds => Known.Keys;

    /// <summary>
    /// Don de l'entité proposée, s'il existe : identité lue dans le log, texte de la base de cartes, résultat calculé pour un
    /// don connu. Null si le serveur n'a relié aucun don à cette carte.
    /// </summary>
    public static DarkGiftView? Describe(GameEngine engine, EntityState offered, int? localPlayerId, ICardDatabase cards, int? cost, int? attack, int? health)
    {
        if (!int.TryParse(offered.GetTag("DARK_GIFT_ENTITY"), out var giftId) || giftId <= 0) return null;
        if (!engine.Entities.TryGetValue(giftId, out var gift)) return null;
        var giftCard = Visibility.VisibleCardId(gift, localPlayerId).CardId;
        if (giftCard is null) return new DarkGiftView { CardId = "?", Name = "Sombre don non identifié" };
        var info = cards.Get(giftCard);
        return new DarkGiftView
        {
            CardId = giftCard,
            Name = info?.Name,
            Text = info?.PlainText is { Length: > 0 } t ? t : null,
            Result = Apply(giftCard, cost, attack, health),
        };
    }

    /// <summary>Résultat d'un don connu sur des statistiques de départ ; null pour un don hors de la table.</summary>
    public static GiftedCardView? Apply(string giftCardId, int? cost, int? attack, int? health)
    {
        if (!Known.TryGetValue(giftCardId, out var e)) return null;
        return new GiftedCardView
        {
            Cost = cost is int c ? Math.Max(0, c + e.Cost) : null,
            Attack = attack is int a ? Math.Max(0, a + e.Attack) : null,
            Health = health is int h ? h + e.Health : null,
            AddedKeywords = e.Keywords?.ToList() ?? new(),
            Note = e.Note,
        };
    }

    /// <summary>Résumé lisible : « 8/8 pour 8, Charge ».</summary>
    public static string Summary(CardRef offered)
    {
        if (offered.DarkGift is not { } g) return "";
        if (g.Result is not { } r) return $"{g.Name ?? g.CardId} : {g.Text ?? "effet non décrit"}";
        var parts = new List<string>();
        if (r.Attack is int a && r.Health is int h) parts.Add($"{a}/{h}");
        if (r.Cost is int c) parts.Add($"pour {c}");
        var head = string.Join(" ", parts);
        var extra = r.AddedKeywords.Select(KeywordName).Concat(r.Note is null ? Array.Empty<string>() : new[] { r.Note });
        return $"{g.Name} : {string.Join(", ", new[] { head }.Concat(extra).Where(x => x.Length > 0))}";
    }

    private static string KeywordName(string k) => k switch
    {
        "CHARGE" => "Charge",
        "TAUNT" => "Provocation",
        "DIVINE_SHIELD" => "Bouclier divin",
        "WINDFURY" => "Furie des vents",
        "LIFESTEAL" => "Vol de vie",
        "ELUSIVE" => "Insaisissable",
        "REBORN" => "Réincarnation",
        "DEATHRATTLE" => "Râle d'agonie",
        _ => k,
    };
}
