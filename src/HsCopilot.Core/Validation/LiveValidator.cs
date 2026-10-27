using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Util;
using HsCopilot.Core.Verification;

namespace HsCopilot.Core.Validation;

public enum CheckStatus
{
    Ok,
    /// <summary>La situation ne s'est pas produite pendant la partie : rien n'a pu être vérifié.</summary>
    NotObserved,
    /// <summary>Vérification partielle ou seuil indicatif dépassé : à examiner.</summary>
    Review,
    Failed,
}

public sealed class CheckItem
{
    public required string Name { get; init; }
    public required CheckStatus Status { get; init; }
    public string Detail { get; init; } = "";
}

/// <summary>Rapport de validation d'une partie traitée en direct (ou simulée).</summary>
public sealed class LiveValidationReport
{
    public string GameId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public bool Finished { get; init; }
    public string? GameType { get; init; }
    public string? Result { get; init; }
    public long Lines { get; init; }
    public int FirstRevision { get; init; }
    public int LastRevision { get; init; }
    public List<CheckItem> Checks { get; init; } = new();
    public OracleReport Oracle { get; init; } = new();
    public VisibilityReport Visibility { get; init; } = new();
    public int Snapshots { get; init; }
    public int IncompleteSnapshots { get; init; }
    public double IncompleteSeconds { get; init; }
    public Dictionary<string, int> FlagsSeen { get; init; } = new();
    public List<string> HdtMismatches { get; init; } = new();
    public List<LatencyStats> Latencies { get; init; } = new();
    public Dictionary<string, long> Counters { get; init; } = new();
    public Dictionary<string, double> Amounts { get; init; } = new();
    /// <summary>VALIDÉ, À COMPLÉTER (éléments non observés ou à examiner) ou ÉCHEC.</summary>
    public string Verdict { get; init; } = "";

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Rapport de validation : partie {GameId}");
        sb.AppendLine();
        sb.AppendLine($"- Verdict : **{Verdict}**");
        sb.AppendLine($"- Source : {Source} · type {GameType ?? "?"} · résultat {Result ?? "?"} · {(Finished ? "partie terminée" : "partie non terminée")}");
        sb.AppendLine($"- Début {StartedAt:yyyy-MM-dd HH:mm:ss}{(EndedAt is DateTime e ? $" · fin {e:HH:mm:ss}" : "")} · {Lines} lignes · révisions {FirstRevision}–{LastRevision}");
        sb.AppendLine();
        sb.AppendLine("| Contrôle | Statut | Détail |");
        sb.AppendLine("|---|---|---|");
        foreach (var c in Checks) sb.AppendLine($"| {c.Name} | {StatusText(c.Status)} | {c.Detail.Replace("|", "/")} |");
        sb.AppendLine();
        sb.AppendLine("## Latences");
        sb.AppendLine();
        sb.AppendLine("| Mesure | Nombre | P50 (ms) | P95 (ms) | Max (ms) |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var l in Latencies) sb.AppendLine($"| {l.Name} | {l.Count} | {l.P50:0} | {l.P95:0} | {l.Max:0} |");
        if (Counters.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Compteurs");
            sb.AppendLine();
            foreach (var (k, v) in Counters) sb.AppendLine($"- {k} : {v}");
            foreach (var (k, v) in Amounts) sb.AppendLine($"- {k} : {v:0.####}");
        }
        if (Oracle.Examples.Count > 0 || Visibility.Examples.Count > 0 || HdtMismatches.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Écarts relevés");
            sb.AppendLine();
            foreach (var x in Oracle.Examples.Concat(Oracle.ManaExamples).Concat(Visibility.Examples).Concat(HdtMismatches)) sb.AppendLine($"- {x}");
        }
        if (FlagsSeen.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Indicateurs d'incertitude rencontrés");
            sb.AppendLine();
            foreach (var (k, v) in FlagsSeen) sb.AppendLine($"- {k} : présent dans {v} état(s) publié(s)");
        }
        return sb.ToString();
    }

    public static string StatusText(CheckStatus s) => s switch
    {
        CheckStatus.Ok => "OK",
        CheckStatus.NotObserved => "non observé",
        CheckStatus.Review => "à examiner",
        _ => "ÉCHEC",
    };
}

/// <summary>
/// Validation continue d'une partie : applique en direct les contrôles déjà utilisés hors ligne (oracle des vues client,
/// mana, visibilité), suit les transitions (début, mulligan, pioche, actions, fin de tour, fin de partie, Rembobinage),
/// les états incomplets, le contrôle croisé HDT et les latences, puis produit un <see cref="LiveValidationReport"/>.
/// </summary>
/// <remarks>Appels attendus sur le fil du moteur : <see cref="BeforeLine"/>, traitement par le moteur, <see cref="AfterLine"/>.</remarks>
public sealed class LiveValidator : IDisposable
{
    /// <summary>Seuils indicatifs (pas des exigences du jeu) au-delà desquels la latence est signalée « à examiner ».</summary>
    public const double LogToReceiveTargetP95Ms = 1000;
    public const double StateToDisplayTargetP95Ms = 250;

    private readonly GameEngine _engine;
    private readonly ClientViewOracle _oracle;
    private readonly VisibilityAuditor _visibility;
    private readonly Func<string> _sourceName;
    private int _gameNumber = -1;

    // État par partie
    private DateTime _startedAt;
    private DateTime? _endedAt;
    private int _firstRevision;
    private long _actionTimestamp;
    private long _rewindTimestamp;
    private int _snapshots, _incompleteSnapshots;
    private long _incompleteSince;
    private double _incompleteSeconds;
    private readonly Dictionary<string, int> _flags = new();
    private readonly List<string> _hdtMismatches = new();
    private int _hdtOk, _hdtBad;
    private int _localTurns, _opponentTurns, _draws, _decisionPoints, _rewinds, _actions;
    private int _lastTurnSeen = -1;
    private int _deckCountAtTurnStart = -1;
    private bool _drawPending;
    private GameStateSnapshot? _last;
    private bool _finishedReported;

    public LiveValidator(GameEngine engine, ICardDatabase cards, Func<string> sourceName)
    {
        _engine = engine;
        _oracle = new ClientViewOracle(engine);
        _visibility = new VisibilityAuditor(engine, cards);
        _sourceName = sourceName;
    }

    public MetricsRegistry Metrics { get; private set; } = new();

    /// <summary>Déclenché quand une partie se termine (état « fin de partie ») ou qu'une nouvelle commence.</summary>
    public event Action<LiveValidationReport>? ReportReady;

    public void BeforeLine(PowerLogLine line)
    {
        if (line.Method == "DebugPrintPower" && line.Body == "CREATE_GAME" && _gameNumber >= 0 && !_finishedReported && _engine.HasGame)
            ReportReady?.Invoke(BuildReport());
        _visibility.BeforeLine(line);
    }

    public void AfterLine(PowerLogLine line)
    {
        if (_engine.GameNumber != _gameNumber) ResetForNewGame();
        _oracle.AfterLine(line);
        TrackTurnsAndDraws();

        switch (line.Method)
        {
            case "SendOption":
                _actions++;
                // La fin de tour n'attend pas de nouvelles options avant le tour suivant : exclue de la mesure.
                _actionTimestamp = _engine.SentOptions.Count > 0 && _engine.SentOptions[^1].OptionType == "END_TURN" ? 0 : Stopwatch.GetTimestamp();
                break;
            case "DebugPrintPower" when line.Body == "RESET_GAME":
                _rewinds++;
                _rewindTimestamp = Stopwatch.GetTimestamp();
                break;
            case "DebugPrintOptions" when line.Body.StartsWith("id=", StringComparison.Ordinal):
                _decisionPoints++;
                if (_actionTimestamp != 0)
                {
                    Metrics.Record(MetricNames.ActionToFreshOptions, Stopwatch.GetElapsedTime(_actionTimestamp).TotalMilliseconds);
                    _actionTimestamp = 0;
                }
                if (_rewindTimestamp != 0)
                {
                    Metrics.Record(MetricNames.RewindToStable, Stopwatch.GetElapsedTime(_rewindTimestamp).TotalMilliseconds);
                    _rewindTimestamp = 0;
                }
                break;
        }
    }

    /// <summary>Suivi des tours et des pioches sur l'état du moteur, ligne par ligne (indépendant du rythme de publication).</summary>
    private void TrackTurnsAndDraws()
    {
        var turn = _engine.GameTurn;
        if (turn != _lastTurnSeen && turn > 0 && MulliganDone())
        {
            _lastTurnSeen = turn;
            var current = _engine.CurrentPlayerId;
            if (current is not null && current == _engine.LocalPlayerId)
            {
                _localTurns++;
                _deckCountAtTurnStart = LocalDeckCount();
                _drawPending = true;
            }
            else if (current is not null)
            {
                _opponentTurns++;
                _drawPending = false;
            }
        }
        if (_drawPending && _engine.CurrentPlayerId == _engine.LocalPlayerId && LocalDeckCount() < _deckCountAtTurnStart)
        {
            _draws++;
            _drawPending = false;
        }
    }

    private bool MulliganDone() =>
        _engine.Players.Count == 2 && _engine.Players.Keys.All(p => _engine.PlayerEntity(p)?.GetTag("MULLIGAN_STATE") == "DONE");

    private int LocalDeckCount() =>
        _engine.LocalPlayerId is int lp ? _engine.Entities.Values.Count(e => !e.Orphaned && e.Controller == lp && e.Zone == "DECK") : -1;

    private void ResetForNewGame()
    {
        _gameNumber = _engine.GameNumber;
        _startedAt = DateTime.Now;
        _endedAt = null;
        _firstRevision = _engine.Revision;
        _actionTimestamp = _rewindTimestamp = 0;
        _snapshots = _incompleteSnapshots = 0;
        _incompleteSince = 0;
        _incompleteSeconds = 0;
        _flags.Clear();
        _hdtMismatches.Clear();
        _hdtOk = _hdtBad = 0;
        _localTurns = _opponentTurns = _draws = _decisionPoints = _rewinds = _actions = 0;
        _lastTurnSeen = -1;
        _deckCountAtTurnStart = -1;
        _drawPending = false;
        _last = null;
        _finishedReported = false;
        Metrics = new MetricsRegistry();
    }

    /// <summary>À appeler pour chaque état publié.</summary>
    public void OnSnapshot(GameStateSnapshot s)
    {
        if (_gameNumber != _engine.GameNumber) ResetForNewGame();
        _snapshots++;
        // Avant la distribution des cartes, le joueur local ne peut pas encore être identifié : seuls les états
        // incomplets à partir du mulligan comptent.
        if (s.Uncertainty.Incomplete && s.Game.MulliganDone)
        {
            _incompleteSnapshots++;
            if (_incompleteSince == 0) _incompleteSince = Stopwatch.GetTimestamp();
        }
        else if (_incompleteSince != 0)
        {
            _incompleteSeconds += Stopwatch.GetElapsedTime(_incompleteSince).TotalSeconds;
            _incompleteSince = 0;
        }
        foreach (var f in s.Uncertainty.Flags) _flags[f.Code] = _flags.GetValueOrDefault(f.Code) + 1;

        if (s.Game.GameOver && _endedAt is null) _endedAt = DateTime.Now;
        _last = s;
        if (s.Game.GameOver && !_finishedReported && s.Game.Result is not null)
        {
            _finishedReported = true;
            ReportReady?.Invoke(BuildReport());
        }
    }

    public void OnHdtCheck(bool allOk, IEnumerable<string> mismatches)
    {
        if (allOk) _hdtOk++;
        else
        {
            _hdtBad++;
            foreach (var m in mismatches)
                if (_hdtMismatches.Count < 30) _hdtMismatches.Add($"{DateTime.Now:HH:mm:ss} HDT : {m}");
        }
    }

    public LiveValidationReport BuildReport()
    {
        var s = _last;
        var o = _oracle.Report;
        var v = _visibility.Report;
        var history = s?.History ?? new List<HistoryItem>();
        int Plays(string side) => history.Count(h => h.Kind is "play" or "hero-power" or "location" && h.Player == side && !h.Rewound);
        int Attacks(string side) => history.Count(h => h.Kind == "attack" && h.Player == side && !h.Rewound);
        var incompleteSeconds = _incompleteSeconds + (_incompleteSince != 0 ? Stopwatch.GetElapsedTime(_incompleteSince).TotalSeconds : 0);

        var checks = new List<CheckItem>
        {
            Check("Début de partie détecté (CREATE_GAME)", _engine.HasGame && !_engine.JoinedMidGame, _engine.HasGame,
                _engine.JoinedMidGame ? "partie rejointe en cours : début manquant" : $"partie {_engine.GameId}"),
            Check("Joueur local confirmé par le serveur ou le client", _engine.LocalEvidence >= LocalPlayerEvidence.ServerOptions, _engine.LocalPlayerId is not null,
                $"preuve : {_engine.LocalEvidence}"),
            Observed("Mulligan terminé", s?.Game.MulliganDone == true, s is not null, ""),
            Observed("Tours observés (vous / adversaire)", _localTurns > 0 && _opponentTurns > 0, true, $"{_localTurns} / {_opponentTurns}"),
            Observed("Pioche en début de tour local", _draws > 0, _localTurns > 0, $"{_draws} pioche(s) détectée(s) sur {_localTurns} tour(s)"),
            Observed("Actions jouées (cartes, pouvoirs, lieux, attaques)", Plays("local") + Attacks("local") > 0, true,
                $"vous : {Plays("local")} jouées, {Attacks("local")} attaques · adversaire : {Plays("opponent")} jouées, {Attacks("opponent")} attaques · {_actions} options envoyées"),
            Observed("Fin de tour (changement de joueur)", _localTurns + _opponentTurns >= 2, true, $"{_localTurns + _opponentTurns} début(s) de tour"),
            Observed("Fin de partie et résultat", s?.Game.GameOver == true && s.Game.Result is not null, true, s?.Game.Result ?? "partie non terminée"),
            new CheckItem
            {
                Name = "Rembobinage appliqué",
                Status = _rewinds == 0 ? CheckStatus.NotObserved : (s?.Game.ResetCount ?? 0) == _rewinds ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{_rewinds} RESET_GAME reçu(s), {s?.Game.ResetCount ?? 0} appliqué(s)",
            },
            new CheckItem
            {
                Name = "Vues du client = état reconstruit",
                Status = o.Checks == 0 ? CheckStatus.NotObserved : o.Mismatches == 0 ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{o.Checks} vues, {o.ExactMatches} identiques, {o.LaggedMatches} en retard (max {o.MaxLagRevisions} rév.), {o.Mismatches} écart(s)",
            },
            new CheckItem
            {
                Name = "Tour attribué au joueur local quand le serveur envoie ses options",
                Status = o.TurnOwnerChecks == 0 ? CheckStatus.NotObserved : o.TurnOwnerMismatches == 0 ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{o.TurnOwnerChecks} blocs d'options, {o.TurnOwnerMismatches} écart(s)",
            },
            new CheckItem
            {
                Name = "Références de joueur toutes associées",
                Status = _engine.PendingPlayerNameChanges == 0 ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{_engine.PendingPlayerNameChanges} changement(s) en attente",
            },
            new CheckItem
            {
                Name = "Coût et mana = légalité serveur",
                Status = o.ManaChecks == 0 ? CheckStatus.NotObserved : o.ManaMismatches == 0 ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{o.ManaChecks} cartes jugées, {o.ManaMismatches} désaccord(s)",
            },
            new CheckItem
            {
                Name = "Aucune carte adverse cachée exposée",
                Status = v.SnapshotsChecked == 0 ? CheckStatus.NotObserved : v.Violations == 0 ? CheckStatus.Ok : CheckStatus.Failed,
                Detail = $"{v.SnapshotsChecked} états, {v.OpponentHiddenSlotsChecked} emplacements, {v.Violations} violation(s)",
            },
            new CheckItem
            {
                Name = "Concordance avec HDT au repos",
                Status = _hdtOk + _hdtBad == 0 ? CheckStatus.NotObserved : _hdtBad == 0 ? CheckStatus.Ok : CheckStatus.Review,
                Detail = _hdtOk + _hdtBad == 0 ? "aucun relevé (plugin HDT absent ou partie trop rapide)" : $"{_hdtOk} relevé(s) identique(s), {_hdtBad} avec écart",
            },
            new CheckItem
            {
                Name = "États incomplets après la mise en place",
                Status = _incompleteSnapshots == 0 ? CheckStatus.Ok : CheckStatus.Review,
                Detail = $"{_incompleteSnapshots} état(s) incomplet(s) sur {_snapshots}, {incompleteSeconds:0.0} s",
            },
        };

        var logToReceive = Metrics.Stats(MetricNames.LogToReceive);
        checks.Add(new CheckItem
        {
            Name = $"Latence log → réception (P95 < {LogToReceiveTargetP95Ms:0} ms, seuil indicatif)",
            Status = logToReceive.Count == 0 ? CheckStatus.NotObserved : logToReceive.P95 <= LogToReceiveTargetP95Ms ? CheckStatus.Ok : CheckStatus.Review,
            Detail = logToReceive.ToString(),
        });
        var display = Metrics.Stats(MetricNames.StateToDisplay);
        checks.Add(new CheckItem
        {
            Name = $"Latence état → affichage (P95 < {StateToDisplayTargetP95Ms:0} ms, seuil indicatif)",
            Status = display.Count == 0 ? CheckStatus.NotObserved : display.P95 <= StateToDisplayTargetP95Ms ? CheckStatus.Ok : CheckStatus.Review,
            Detail = display.ToString(),
        });

        var verdict = checks.Any(c => c.Status == CheckStatus.Failed) ? "ÉCHEC"
            : checks.Any(c => c.Status is CheckStatus.NotObserved or CheckStatus.Review) ? "À COMPLÉTER"
            : "VALIDÉ";

        return new LiveValidationReport
        {
            GameId = _engine.GameId ?? "aucune",
            Source = _sourceName(),
            StartedAt = _startedAt,
            EndedAt = _endedAt,
            Finished = s?.Game.GameOver == true,
            GameType = s?.Game.GameType,
            Result = s?.Game.Result,
            Lines = _engine.LinesInGame,
            FirstRevision = _firstRevision,
            LastRevision = _engine.Revision,
            Checks = checks,
            Oracle = o,
            Visibility = v,
            Snapshots = _snapshots,
            IncompleteSnapshots = _incompleteSnapshots,
            IncompleteSeconds = incompleteSeconds,
            FlagsSeen = new Dictionary<string, int>(_flags),
            HdtMismatches = new List<string>(_hdtMismatches),
            Latencies = Metrics.AllStats().ToList(),
            Counters = Metrics.Counters.ToDictionary(kv => kv.Key, kv => kv.Value),
            Amounts = Metrics.Amounts.ToDictionary(kv => kv.Key, kv => kv.Value),
            Verdict = verdict,
        };
    }

    private static CheckItem Check(string name, bool ok, bool partiallyOk, string detail) =>
        new() { Name = name, Status = ok ? CheckStatus.Ok : partiallyOk ? CheckStatus.Review : CheckStatus.Failed, Detail = detail };

    private static CheckItem Observed(string name, bool ok, bool applicable, string detail) =>
        new() { Name = name, Status = ok ? CheckStatus.Ok : applicable ? CheckStatus.NotObserved : CheckStatus.NotObserved, Detail = detail };

    public void Dispose() => _oracle.Dispose();

    /// <summary>Écrit le rapport en JSON et en Markdown. Renvoie le chemin du fichier Markdown.</summary>
    public static string Save(LiveValidationReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var safeId = string.Concat(report.GameId.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
        var basePath = Path.Combine(directory, $"{report.StartedAt:yyyyMMdd_HHmmss}_{safeId}");
        File.WriteAllText(basePath + ".json", JsonSerializer.Serialize(report, Json.Pretty), new UTF8Encoding(false));
        File.WriteAllText(basePath + ".md", report.ToMarkdown(), new UTF8Encoding(false));
        return basePath + ".md";
    }
}
