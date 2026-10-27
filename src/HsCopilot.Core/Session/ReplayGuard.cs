using HsCopilot.Core.Logs;

namespace HsCopilot.Core.Session;

/// <summary>
/// Filtre les lignes d'une partie renvoyées une seconde fois dans le flux en direct.
/// </summary>
/// <remarks>
/// Cas observé le 25/10/2026 sur une vraie partie : après un Rembobinage (RESET_GAME), HDT relit la partie depuis
/// CREATE_GAME et renvoie toutes ses lignes au plugin, en retirant le passage rembobiné, y compris la réinitialisation
/// complète envoyée par le serveur. Reconstruire l'état depuis ce renvoi donne un état faux (cartes recréées inconnues,
/// main et PV erronés). Le moteur en cours, lui, avait déjà appliqué la réinitialisation correctement : il faut ignorer le
/// renvoi et reprendre à la première ligne réellement nouvelle.
/// Règles, pendant une partie en cours : une ligne plus ancienne que la dernière ligne traitée est un renvoi ; un
/// CREATE_GAME identique à celui de la partie en cours aussi ; une ligne de même horodatage déjà traitée, pendant un
/// renvoi, aussi. Un CREATE_GAME d'un autre horodatage est une nouvelle partie : toujours accepté.
/// </remarks>
public sealed class ReplayGuard
{
    private static readonly TimeSpan HalfDay = TimeSpan.FromHours(12);
    private TimeSpan? _lastTime;
    private TimeSpan? _gameCreateTime;
    private readonly Dictionary<string, int> _atLastTime = new(StringComparer.Ordinal);
    private bool _skipping;

    /// <summary>Lignes ignorées depuis la création du filtre.</summary>
    public long Skipped { get; private set; }
    /// <summary>Nombre de renvois détectés.</summary>
    public int Episodes { get; private set; }
    public bool Skipping => _skipping;
    /// <summary>Horodatage de la première ligne acceptée après le dernier renvoi.</summary>
    public TimeSpan? ResumedAt { get; private set; }

    /// <summary>Écart signé entre deux heures du log, en tenant compte du passage de minuit.</summary>
    public static TimeSpan Delta(TimeSpan time, TimeSpan reference)
    {
        var d = time - reference;
        if (d < -HalfDay) d += TimeSpan.FromDays(1);
        else if (d > HalfDay) d -= TimeSpan.FromDays(1);
        return d;
    }

    /// <summary>Faut-il traiter cette ligne ? <paramref name="inGame"/> : une partie est en cours dans le moteur.</summary>
    public bool Accept(PowerLogLine parsed, string raw, bool inGame, bool isCreateGame)
    {
        var key = raw.TrimEnd();
        if (inGame && _lastTime is TimeSpan last)
        {
            if (isCreateGame)
            {
                if (_gameCreateTime == parsed.Time) return Skip();
            }
            else
            {
                var d = Delta(parsed.Time, last);
                if (d < TimeSpan.Zero) return Skip();
                if (d == TimeSpan.Zero && _skipping && _atLastTime.TryGetValue(key, out var n) && n > 0)
                {
                    _atLastTime[key] = n - 1;
                    return Skip();
                }
            }
        }
        if (_skipping)
        {
            _skipping = false;
            ResumedAt = parsed.Time;
        }
        if (isCreateGame) _gameCreateTime = parsed.Time;
        if (_lastTime != parsed.Time)
        {
            _lastTime = parsed.Time;
            _atLastTime.Clear();
        }
        _atLastTime[key] = _atLastTime.GetValueOrDefault(key) + 1;
        return true;
    }

    private bool Skip()
    {
        if (!_skipping)
        {
            _skipping = true;
            Episodes++;
        }
        Skipped++;
        return false;
    }
}
