using HsCopilot.Core.State;

namespace HsCopilot.Core.Cards;

/// <summary>
/// Code de deck Hearthstone (base64 de varints : 0, version, format, héros, cartes ×1, cartes ×2, cartes ×n). Sert à
/// vérifier qu'une liste annoncée correspond au deck sélectionné dans HDT, et à fournir une liste aux outils hors ligne.
/// </summary>
public static class DeckString
{
    public sealed record Decoded(int Version, int Format, IReadOnlyList<int> HeroDbfIds, IReadOnlyDictionary<int, int> Cards)
    {
        /// <summary>1 : Wild, 2 : Standard, 3 : Classique, 4 : Twist.</summary>
        public string FormatName => Format switch { 1 => "Wild", 2 => "Standard", 3 => "Classique", 4 => "Twist", _ => $"format {Format}" };
        public int Total => Cards.Values.Sum();
    }

    public static Decoded Decode(string code)
    {
        var bytes = Convert.FromBase64String(code.Trim());
        var i = 0;
        int Next()
        {
            int value = 0, shift = 0;
            while (true)
            {
                if (i >= bytes.Length) throw new FormatException("code de deck tronqué");
                var b = bytes[i++];
                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) return value;
                shift += 7;
            }
        }
        if (Next() != 0) throw new FormatException("code de deck invalide (octet de tête)");
        var version = Next();
        var format = Next();
        var heroes = Enumerable.Range(0, Next()).Select(_ => Next()).ToList();
        var cards = new Dictionary<int, int>();
        foreach (var dbf in Enumerable.Range(0, Next()).Select(_ => Next()).ToList()) cards[dbf] = 1;
        foreach (var dbf in Enumerable.Range(0, Next()).Select(_ => Next()).ToList()) cards[dbf] = 2;
        var multi = Next();
        for (var k = 0; k < multi; k++)
        {
            var dbf = Next();
            cards[dbf] = Next();
        }
        return new Decoded(version, format, heroes, cards);
    }

    /// <summary>Liste du moteur (identifiants de cartes) ; lève une exception si une carte est absente de la base.</summary>
    public static Decklist ToDecklist(string code, IEnumerable<CardInfo> cards, string? name = null)
    {
        var decoded = Decode(code);
        var byDbf = cards.Where(c => c.DbfId > 0).GroupBy(c => c.DbfId).ToDictionary(g => g.Key, g => g.First().Id);
        var list = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (dbf, count) in decoded.Cards)
        {
            if (!byDbf.TryGetValue(dbf, out var id)) throw new InvalidDataException($"carte dbfId {dbf} absente de la base de cartes");
            list[id] = count;
        }
        return new Decklist { Source = "code de deck", Name = name, Cards = list };
    }
}
