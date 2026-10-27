using System.Globalization;
using System.Text.RegularExpressions;

namespace HsCopilot.Core.Session;

/// <summary>Tour et étape que le client Hearthstone a effectivement exécutés (et donc affichés), d'après PowerTaskList.</summary>
public sealed record VisualProgress(int? Turn, string? Step, TimeSpan LogTime, DateTime ReceivedUtc);

/// <summary>
/// Horloge de l'affichage : suit les lignes <c>PowerTaskList.*</c> du Power.log, que le client écrit quand il exécute
/// réellement les événements, au rythme de sa file d'animations. Les lignes <c>GameState.*</c> arrivent dès que le
/// serveur les envoie : contre l'IA, le début de ton tour y figure en médiane 9,9 s avant d'être exécuté par le client
/// (mesure sur 625 débuts de tour, docs/SYNCHRO-AFFICHAGE.md).
/// </summary>
/// <remarks>
/// Cette horloge ne sert qu'à décider quand un conseil peut être présenté comme jouable. Elle ne modifie jamais l'état
/// de la partie : le moteur ne lit que GameState, et les lignes PowerTaskList (mêmes événements, rejoués) n'y entrent
/// jamais, ce qui évite tout double traitement. Seuls le tour et l'étape de l'entité de jeu sont suivis.
/// </remarks>
public sealed class VisualClock
{
    private static readonly Regex Line = new(@"^D (\d\d:\d\d:\d\d\.\d+) PowerTaskList\.DebugPrintPower\(\) -\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex TurnOrStep = new(@"^TAG_CHANGE Entity=GameEntity tag=(TURN|STEP) value=(\w+)", RegexOptions.Compiled);
    private readonly object _lock = new();

    public int? Turn { get { lock (_lock) return _turn; } }
    public string? Step { get { lock (_lock) return _step; } }
    private int? _turn;
    private string? _step;
    private TimeSpan _lastLogTime;

    /// <summary>Changement de tour ou d'étape côté client.</summary>
    public event Action<VisualProgress>? Changed;

    public static bool IsPowerTaskList(string line) => line.Contains(" PowerTaskList.", StringComparison.Ordinal);

    /// <summary>Traite une ligne ; renvoie vrai si c'était une ligne PowerTaskList (même ignorée).</summary>
    public bool OnLine(string line)
    {
        if (!IsPowerTaskList(line)) return false;
        var m = Line.Match(line);
        if (!m.Success) return true;
        var body = m.Groups[2].Value.TrimEnd();
        var time = TimeSpan.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, out var t) ? t : TimeSpan.Zero;
        VisualProgress? changed = null;
        lock (_lock)
        {
            _lastLogTime = time;
            if (body.StartsWith("CREATE_GAME", StringComparison.Ordinal))
            {
                _turn = null;
                _step = null;
                changed = new VisualProgress(null, null, time, DateTime.UtcNow);
            }
            else if (TurnOrStep.Match(body) is { Success: true } tm)
            {
                if (tm.Groups[1].Value == "TURN" && int.TryParse(tm.Groups[2].Value, out var turn))
                {
                    if (turn != _turn) { _turn = turn; _step = null; changed = new VisualProgress(_turn, _step, time, DateTime.UtcNow); }
                }
                else if (tm.Groups[1].Value == "STEP" && tm.Groups[2].Value != _step)
                {
                    _step = tm.Groups[2].Value;
                    changed = new VisualProgress(_turn, _step, time, DateTime.UtcNow);
                }
            }
        }
        if (changed is not null) Changed?.Invoke(changed);
        return true;
    }

    /// <summary>
    /// Le client a-t-il affiché le tour <paramref name="gameTurn"/> jusqu'à la phase d'action ? Vrai si son tour est plus
    /// avancé, ou si c'est ce tour et que l'étape MAIN_ACTION (ou une étape de fin) a été exécutée.
    /// </summary>
    public static bool ShowsTurn(int? visibleTurn, string? visibleStep, int gameTurn) =>
        visibleTurn is int vt && (vt > gameTurn || (vt == gameTurn && visibleStep is "MAIN_ACTION" or "MAIN_END" or "MAIN_CLEANUP" or "MAIN_NEXT"));
}
