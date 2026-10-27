using System.Globalization;

namespace HsCopilot.Core.Logs;

/// <summary>Ligne "GameState.*" du Power.log, découpée.</summary>
/// <remarks>
/// Format observé (Hearthstone build 254477, octobre 2026) :
/// <c>D 11:10:18.2348071 GameState.DebugPrintPower() -     TAG_CHANGE Entity=38 tag=COST value=4</c>.
/// Seules les lignes de l'espace de noms <c>GameState.</c> sont retenues : ce sont les données telles que
/// reçues du serveur. Les lignes <c>PowerTaskList.</c> portent la même information, rejouée au rythme des animations.
/// </remarks>
public sealed class PowerLogLine
{
    public required TimeSpan Time { get; init; }
    /// <summary>Nom de la méthode cliente, par exemple <c>DebugPrintPower</c> ou <c>DebugPrintOptions</c>.</summary>
    public required string Method { get; init; }
    /// <summary>Nombre d'espaces d'indentation après le séparateur " - ".</summary>
    public required int Indent { get; init; }
    /// <summary>Contenu sans indentation ni espaces finaux.</summary>
    public required string Body { get; init; }
    public required string Raw { get; init; }

    public override string ToString() => Raw;
}

public static class PowerLogLineParser
{
    private const string Prefix = "GameState.";

    /// <summary>Découpe une ligne brute. Renvoie null pour toute ligne qui n'est pas une ligne GameState valide.</summary>
    public static PowerLogLine? TryParse(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var line = raw.AsSpan().TrimEnd("\r\n");
        // "D " / "W " / "E " puis horodatage
        if (line.Length < 12 || line[1] != ' ' || (line[0] != 'D' && line[0] != 'W' && line[0] != 'E')) return null;
        var tsEnd = line[2..].IndexOf(' ');
        if (tsEnd <= 0) return null;
        tsEnd += 2;
        if (!TimeSpan.TryParse(line[2..tsEnd], CultureInfo.InvariantCulture, out var time)) return null;
        var rest = line[(tsEnd + 1)..];
        if (!rest.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        rest = rest[Prefix.Length..];
        var paren = rest.IndexOf("()", StringComparison.Ordinal);
        if (paren <= 0) return null;
        var method = rest[..paren].ToString();
        rest = rest[(paren + 2)..];

        int indent = 0;
        ReadOnlySpan<char> body;
        if (rest.StartsWith(" -", StringComparison.Ordinal))
        {
            body = rest[2..];
            // un espace sépare le tiret du contenu ; les suivants sont l'indentation
            if (body.Length > 0 && body[0] == ' ') body = body[1..];
            while (indent < body.Length && body[indent] == ' ') indent++;
            body = body[indent..];
        }
        else
        {
            body = rest;
        }

        return new PowerLogLine
        {
            Time = time,
            Method = method,
            Indent = indent,
            Body = body.TrimEnd().ToString(),
            Raw = line.ToString(),
        };
    }
}
