using System.Text;
using System.Text.RegularExpressions;

namespace HsCopilot.Core.Cards;

/// <summary>Nettoyage du balisage des textes de cartes Hearthstone.</summary>
/// <remarks>
/// Balisage rencontré : <c>&lt;b&gt;…&lt;/b&gt;</c>, <c>[x]</c> en tête (mise en page), <c>_</c> (espace insécable),
/// <c>$3</c> / <c>#3</c> (valeur modifiable par les dégâts des sorts / soins), <c>|4(point,points)</c> (pluriel),
/// <c>&amp;</c> (coupure de ligne facultative) et <c>@</c> (valeur dynamique).
/// </remarks>
public static partial class CardText
{
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex HtmlTag();
    [GeneratedRegex(@"[\$#](\d+)")] private static partial Regex ScaledNumber();
    [GeneratedRegex(@"(\d+)(\)?)(\s*)\|4\(([^,()]*),([^,()]*)\)")] private static partial Regex PluralAfterNumber();
    [GeneratedRegex(@"\|4\(([^,()]*),([^,()]*)\)")] private static partial Regex PluralAlone();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();

    public static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = text.Replace("[x]", "", StringComparison.Ordinal);
        s = HtmlTag().Replace(s, "");
        s = ScaledNumber().Replace(s, "$1");
        // Le chiffre et "|4(" peuvent être séparés par "_" (espace insécable).
        s = s.Replace("_|4(", " |4(", StringComparison.Ordinal);
        // "3 |4(point,points)" ou "(3) |4(cristal,cristaux)" : accord avec le nombre qui précède.
        s = PluralAfterNumber().Replace(s, m =>
        {
            var n = int.Parse(m.Groups[1].Value);
            var word = n > 1 ? m.Groups[5].Value : m.Groups[4].Value;
            return m.Groups[1].Value + m.Groups[2].Value + " " + word;
        });
        s = PluralAlone().Replace(s, m => m.Groups[2].Value);
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            sb.Append(ch switch
            {
                '_' => ' ',
                '&' => ' ',
                '\n' => ' ',
                ' ' => ' ',
                _ => ch,
            });
        }
        return Spaces().Replace(sb.ToString(), " ").Replace(" ,", ",").Replace(" .", ".").Trim();
    }
}
