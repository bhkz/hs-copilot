using System.Text.RegularExpressions;

namespace HsCopilot.Core.Util;

/// <summary>Remplace les BattleTags et identifiants de compte d'un log par des valeurs neutres (pour partager des fixtures).</summary>
public static partial class Anonymizer
{
    [GeneratedRegex(@"PlayerName=(?<n>.+)$")] private static partial Regex PlayerNameRx();
    [GeneratedRegex(@" Player=(?<n>.+?) (?:TaskList|EntitiesCount|ChoiceType)=")] private static partial Regex ChoicePlayerRx();
    [GeneratedRegex(@"GameAccountId=\[hi=\d+ lo=\d+\]")] private static partial Regex AccountRx();

    public static List<string> Anonymize(IReadOnlyList<string> lines)
    {
        var names = new List<string>();
        foreach (var l in lines)
        {
            var m = PlayerNameRx().Match(l);
            if (m.Success) Add(m.Groups["n"].Value.Trim());
            m = ChoicePlayerRx().Match(l);
            if (m.Success) Add(m.Groups["n"].Value.Trim());
        }

        void Add(string n)
        {
            if (n.StartsWith("UNKNOWN", StringComparison.Ordinal) || names.Contains(n)) return;
            names.Add(n);
        }

        // Noms complets d'abord (les plus longs en premier), puis noms courts dans leurs contextes connus.
        var map = new List<(string From, string To)>();
        var shortMap = new List<(string From, string To)>();
        var index = 0;
        foreach (var n in names.OrderByDescending(n => n.Length))
        {
            index++;
            var hash = n.IndexOf('#');
            var anonymShort = $"Joueur{index}";
            map.Add((n, hash >= 0 ? $"{anonymShort}#{1000 + index}" : anonymShort));
            if (hash > 0) shortMap.Add((n[..hash], anonymShort));
        }

        var result = new List<string>(lines.Count);
        foreach (var raw in lines)
        {
            var l = AccountRx().Replace(raw, "GameAccountId=[hi=0 lo=0]");
            foreach (var (from, to) in map) l = l.Replace(from, to, StringComparison.Ordinal);
            foreach (var (from, to) in shortMap)
            {
                l = l.Replace($"Updating {from} CardID=", $"Updating {to} CardID=", StringComparison.Ordinal);
                l = l.Replace($"Entity={from} tag=", $"Entity={to} tag=", StringComparison.Ordinal);
                l = l.Replace($"Player={from} ", $"Player={to} ", StringComparison.Ordinal);
            }
            result.Add(l);
        }
        return result;
    }
}
