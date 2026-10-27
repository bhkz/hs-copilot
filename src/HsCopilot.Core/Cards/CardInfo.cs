using System.Text.Json.Serialization;

namespace HsCopilot.Core.Cards;

/// <summary>Définition statique d'une carte (valeurs de base imprimées sur la carte).</summary>
/// <remarks>Format JSON compatible avec HearthstoneJSON (<c>id</c>, <c>dbfId</c>, <c>name</c>, <c>cost</c>, <c>mechanics</c>…).</remarks>
public sealed class CardInfo
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("dbfId")] public int DbfId { get; init; }
    /// <summary>Nom dans la langue de la base (français si disponible).</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("nameEn")] public string? NameEn { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("cost")] public int? Cost { get; init; }
    [JsonPropertyName("attack")] public int? Attack { get; init; }
    [JsonPropertyName("health")] public int? Health { get; init; }
    [JsonPropertyName("durability")] public int? Durability { get; init; }
    [JsonPropertyName("cardClass")] public string? CardClass { get; init; }
    [JsonPropertyName("races")] public List<string>? Races { get; init; }
    [JsonPropertyName("rarity")] public string? Rarity { get; init; }
    [JsonPropertyName("set")] public string? Set { get; init; }
    [JsonPropertyName("collectible")] public bool Collectible { get; init; }
    [JsonPropertyName("mechanics")] public List<string>? Mechanics { get; init; }
    [JsonPropertyName("spellSchool")] public string? SpellSchool { get; init; }

    public bool HasMechanic(string mechanic) => Mechanics?.Contains(mechanic, StringComparer.Ordinal) == true;
    public bool HasRace(string race) => Races?.Contains(race, StringComparer.Ordinal) == true;

    /// <summary>Nom affichable : français, sinon anglais, sinon identifiant.</summary>
    [JsonIgnore] public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name! : !string.IsNullOrWhiteSpace(NameEn) ? NameEn! : Id;

    /// <summary>Texte nettoyé du balisage (gras, espaces insécables, pluriels).</summary>
    [JsonIgnore] public string PlainText => CardText.Clean(Text);

    /// <summary>Carte de type Secret (le contenu d'un Secret adverse est une information cachée).</summary>
    [JsonIgnore] public bool IsSecret => HasMechanic("SECRET");
}

public interface ICardDatabase
{
    CardInfo? Get(string? cardId);
    int Count { get; }
    /// <summary>Numéro de build des données (attribut <c>build</c> de CardDefs, ou version HearthstoneJSON).</summary>
    string Build { get; }
    string Locale { get; }
    /// <summary>Origine des données (chemin du fichier chargé).</summary>
    string Source { get; }
}

public sealed class CardDatabase : ICardDatabase
{
    private readonly Dictionary<string, CardInfo> _cards;

    public CardDatabase(IEnumerable<CardInfo> cards, string build, string locale, string source)
    {
        _cards = new Dictionary<string, CardInfo>(StringComparer.Ordinal);
        foreach (var c in cards) _cards[c.Id] = c;
        Build = build;
        Locale = locale;
        Source = source;
    }

    public static CardDatabase Empty { get; } = new(Array.Empty<CardInfo>(), "none", "none", "aucune base de cartes chargée");

    public CardInfo? Get(string? cardId) =>
        !string.IsNullOrEmpty(cardId) && _cards.TryGetValue(cardId, out var c) ? c : null;

    public int Count => _cards.Count;
    public string Build { get; }
    public string Locale { get; }
    public string Source { get; }
    public IEnumerable<CardInfo> All => _cards.Values;
}
