using System.IO.Compression;
using System.Text;

namespace HsCopilot.Core.Transport;

/// <summary>Une partie délimitée dans un fichier de log (de sa ligne CREATE_GAME à la suivante).</summary>
public sealed class LoggedGame
{
    public required int Index { get; init; }
    public required int StartLine { get; init; }
    public required int EndLine { get; init; }
    public required IReadOnlyList<string> Lines { get; init; }
}

/// <summary>Lecture des enregistrements : .hdtreplay (zip HDT contenant output_log.txt), Power.log, ou journal texte HsCopilot.</summary>
public static class ReplayFile
{
    public static List<string> ReadAllLines(string path)
    {
        if (IsZip(path))
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals("output_log.txt", StringComparison.OrdinalIgnoreCase))
                        ?? zip.Entries.FirstOrDefault(e => e.Length > 0)
                        ?? throw new InvalidDataException("Archive vide : " + path);
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            return ReadLines(reader);
        }
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs, Encoding.UTF8);
        return ReadLines(sr);
    }

    private static List<string> ReadLines(TextReader reader)
    {
        var list = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null) list.Add(line);
        return list;
    }

    private static bool IsZip(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".hdtreplay", StringComparison.OrdinalIgnoreCase) || ext.Equals(".zip", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> head = stackalloc byte[4];
            return fs.Read(head) == 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsCreateGame(string line) =>
        line.Contains("GameState.DebugPrintPower() - CREATE_GAME", StringComparison.Ordinal);

    /// <summary>Découpe un log en parties. Les lignes précédant la première CREATE_GAME forment une partie « partielle » d'index 0 si elles contiennent des données.</summary>
    public static List<LoggedGame> SplitGames(IReadOnlyList<string> lines)
    {
        var starts = new List<int>();
        for (int i = 0; i < lines.Count; i++)
            if (IsCreateGame(lines[i])) starts.Add(i);
        var games = new List<LoggedGame>();
        if (starts.Count == 0)
        {
            if (lines.Count > 0) games.Add(new LoggedGame { Index = 0, StartLine = 0, EndLine = lines.Count, Lines = lines });
            return games;
        }
        for (int g = 0; g < starts.Count; g++)
        {
            var start = starts[g];
            var end = g + 1 < starts.Count ? starts[g + 1] : lines.Count;
            games.Add(new LoggedGame { Index = g + 1, StartLine = start, EndLine = end, Lines = lines.Skip(start).Take(end - start).ToList() });
        }
        return games;
    }
}
