using System.Text;

namespace HsCopilot.Core.Transport;

/// <summary>
/// Lecture directe du Power.log de Hearthstone (solution de repli quand le plugin HDT n'est pas connecté).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Dossier actif : le sous-dossier <c>Hearthstone_*</c> le plus récent de <c>Logs</c> (comme HDT).</item>
/// <item>Au démarrage, reprend à la dernière ligne CREATE_GAME du fichier pour reconstruire la partie en cours.</item>
/// <item>Ne déplace ni ne supprime jamais le fichier (HDT, lui, le renomme en Power_old.log) ; ouverture partagée.</item>
/// <item>Fichier raccourci ou recréé : relecture depuis le début. Ligne incomplète en fin de fichier : attendue au tour suivant.</item>
/// <item>Seules les lignes <c>GameState.</c> sont transmises.</item>
/// </list>
/// </remarks>
public sealed class PowerLogTailer : IDisposable
{
    private readonly string _logsDir;
    private readonly TimeSpan _poll;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;
    private string? _currentPath;
    private string? _activePath;
    private long _offset = -1;
    private readonly StringBuilder _partial = new();
    private readonly Func<string, bool> _filter;
    private readonly bool _startAtEnd;

    /// <param name="filter">Lignes transmises (par défaut : <c>GameState.</c>).</param>
    /// <param name="startAtEnd">Ignorer le contenu déjà écrit (horloge de l'affichage : seul le présent compte).</param>
    public PowerLogTailer(string logsDir, TimeSpan? poll = null, Func<string, bool>? filter = null, bool startAtEnd = false)
    {
        _logsDir = logsDir;
        _poll = poll ?? TimeSpan.FromMilliseconds(100);
        _filter = filter ?? (l => l.Contains(" GameState.", StringComparison.Ordinal));
        _startAtEnd = startAtEnd;
    }

    /// <summary>Lignes GameState lues. Le booléen vaut vrai quand la lecture repart d'un nouveau fichier (l'état doit être reconstruit).</summary>
    public event Action<IReadOnlyList<string>, bool>? LinesReceived;
    public event Action<string>? Status;

    public string? CurrentPath => _currentPath;
    public long LinesRead { get; private set; }

    public void Start() => _loop ??= Task.Run(() => RunAsync(_cts.Token));

    /// <summary>Fichier Power.log de la session de jeu la plus récente, ou d'un dossier Logs à plat (anciennes versions).</summary>
    public static string? FindActivePowerLog(string logsDir)
    {
        if (!Directory.Exists(logsDir)) return null;
        var latest = new DirectoryInfo(logsDir).GetDirectories("Hearthstone_*")
            .OrderByDescending(d => d.CreationTimeUtc)
            .FirstOrDefault();
        if (latest is not null) return Path.Combine(latest.FullName, "Power.log");
        var flat = Path.Combine(logsDir, "Power.log");
        return File.Exists(flat) ? flat : null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var lastDirCheck = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (DateTime.UtcNow - lastDirCheck > TimeSpan.FromSeconds(5) || _activePath is null)
                {
                    lastDirCheck = DateTime.UtcNow;
                    var path = FindActivePowerLog(_logsDir);
                    if (path != _currentPath)
                        Status?.Invoke(path is null ? $"Aucun dossier de logs Hearthstone dans {_logsDir}" : $"Lecture de {path}");
                    _activePath = path;
                }
                if (_activePath is not null && File.Exists(_activePath)) ReadNew(_activePath);
            }
            catch (IOException ex)
            {
                Status?.Invoke($"Lecture du log : {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Status?.Invoke($"Accès au log refusé : {ex.Message}");
            }
            try { await Task.Delay(_poll, ct).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Lit les octets ajoutés depuis la dernière lecture (public pour les tests).</summary>
    public void ReadNew(string path)
    {
        if (!string.Equals(path, _currentPath, StringComparison.OrdinalIgnoreCase))
        {
            _currentPath = path;
            _offset = -1; // nouveau fichier : reprise à la dernière CREATE_GAME
            _partial.Clear();
        }
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var length = fs.Length;
        var restart = false;
        if (_offset < 0)
        {
            _offset = _startAtEnd ? length : FindLastCreateGame(fs);
            restart = true;
        }
        else if (length < _offset)
        {
            Status?.Invoke("Power.log raccourci ou recréé : relecture depuis le début");
            _offset = 0;
            _partial.Clear();
            restart = true;
        }
        if (length == _offset && !restart) return;

        fs.Seek(_offset, SeekOrigin.Begin);
        var buffer = new byte[length - _offset];
        var read = 0;
        while (read < buffer.Length)
        {
            var n = fs.Read(buffer, read, buffer.Length - read);
            if (n == 0) break;
            read += n;
        }
        _offset += read;
        _partial.Append(Encoding.UTF8.GetString(buffer, 0, read));

        var text = _partial.ToString();
        var lastNewline = text.LastIndexOf('\n');
        if (lastNewline < 0)
        {
            if (restart) LinesReceived?.Invoke(Array.Empty<string>(), true);
            return;
        }
        var complete = text[..lastNewline];
        _partial.Clear();
        _partial.Append(text[(lastNewline + 1)..]);

        var lines = new List<string>();
        foreach (var raw in complete.Split('\n'))
        {
            var l = raw.TrimEnd('\r');
            if (_filter(l)) lines.Add(l);
        }
        LinesRead += lines.Count;
        if (lines.Count > 0 || restart) LinesReceived?.Invoke(lines, restart);
    }

    /// <summary>Position (en octets) du début de la dernière ligne CREATE_GAME, ou 0 si le fichier n'en contient pas.</summary>
    public static long FindLastCreateGame(FileStream fs)
    {
        var marker = Encoding.UTF8.GetBytes("GameState.DebugPrintPower() - CREATE_GAME");
        var buf = new byte[1 << 20];
        var carry = Array.Empty<byte>();
        long basePos = 0, lastHit = -1;
        int n;
        fs.Seek(0, SeekOrigin.Begin);
        while ((n = fs.Read(buf, 0, buf.Length)) > 0)
        {
            var chunk = new byte[carry.Length + n];
            Buffer.BlockCopy(carry, 0, chunk, 0, carry.Length);
            Buffer.BlockCopy(buf, 0, chunk, carry.Length, n);
            var idx = 0;
            while ((idx = IndexOf(chunk, marker, idx)) >= 0)
            {
                lastHit = basePos - carry.Length + idx;
                idx += marker.Length;
            }
            var keep = Math.Min(marker.Length - 1, chunk.Length);
            carry = chunk[^keep..];
            basePos += n;
        }
        if (lastHit < 0) return 0;
        var lineStart = lastHit;
        while (lineStart > 0)
        {
            fs.Seek(lineStart - 1, SeekOrigin.Begin);
            if (fs.ReadByte() == 0x0A) break; // '\n'
            lineStart--;
        }
        return lineStart;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from)
    {
        for (int i = from; i <= haystack.Length - needle.Length; i++)
        {
            var ok = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(2)); } catch { /* arrêt */ }
        _cts.Dispose();
    }
}
