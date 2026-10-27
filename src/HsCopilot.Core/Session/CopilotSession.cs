using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading.Channels;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.Validation;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using HsCopilot.Shared;

namespace HsCopilot.Core.Session;

public enum SourceKind
{
    None,
    /// <summary>Lignes relayées par le plugin HDT (source principale).</summary>
    HdtPlugin,
    /// <summary>Lecture directe du Power.log (repli).</summary>
    LogTail,
}

/// <summary>Résultat du contrôle croisé entre l'état reconstruit et la vue de HDT.</summary>
public sealed class HdtCrossCheck
{
    public required DateTime At { get; init; }
    /// <summary>Vrai si aucune ligne n'est arrivée depuis au moins <see cref="CopilotSession.QuietPeriod"/> : HDT a fini ses animations.</summary>
    public required bool Stable { get; init; }
    public List<(string Item, bool Ok, string Detail)> Items { get; } = new();
    public bool AllOk => Items.All(i => i.Ok);
}

public sealed class SessionStatus
{
    public SourceKind ActiveSource { get; init; }
    public string ConfiguredSource { get; init; } = "auto";
    public bool PluginConnected { get; init; }
    public string? PluginVersion { get; init; }
    public string? HdtVersion { get; init; }
    public string? TailPath { get; init; }
    public long LinesReceived { get; init; }
    public long LinesIgnored { get; init; }
    public DateTime? LastLineAt { get; init; }
    public string? JournalPath { get; init; }
    public string? DeckName { get; init; }
    public string CardDbMessage { get; init; } = "";
    public HdtCrossCheck? LastCheck { get; init; }
    public long PluginGaps { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
    /// <summary>Rapport de validation de la partie en cours (reconstruit au plus une fois par seconde).</summary>
    public LiveValidationReport? LiveReport { get; init; }
    public string? LastReportPath { get; init; }
}

/// <summary>
/// Session temps réel : une seule source active à la fois (plugin HDT, ou lecture du Power.log en repli),
/// un seul fil de traitement pour le moteur, publication d'états versionnés.
/// </summary>
/// <remarks>
/// Changement de source = moteur neuf reconstruit depuis le début de la partie (le plugin renvoie la partie en cours,
/// le lecteur de log reprend à la dernière CREATE_GAME). Les deux flux ne sont jamais mélangés.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class CopilotSession : IAsyncDisposable
{
    public static readonly TimeSpan QuietPeriod = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(100);

    private abstract record Work;
    private sealed record LinesWork(SourceKind Source, IReadOnlyList<string> Lines, bool Restart, long ReceivedTimestamp, bool Backlog) : Work;
    private sealed record PluginConnection(bool Connected) : Work;
    private sealed record PluginMessage(PipeMessage Message, long ReceivedTimestamp, DateTime ReceivedUtc) : Work;
    private sealed record DisplayedWork(GameStateSnapshot Snapshot, long DisplayedTimestamp, DateTime DisplayedLocal) : Work;
    private sealed record Tick : Work;
    /// <summary>Lignes PowerTaskList lues dans le Power.log : horloge de l'affichage et journal (audit), jamais le moteur.</summary>
    private sealed record VisualLinesWork(IReadOnlyList<string> Lines) : Work;

    private readonly CopilotConfig _config;
    private readonly ICardDatabase _cards;
    private readonly string _cardDbMessage;
    private readonly GameStateProjector _projector;
    private readonly Channel<Work> _work = Channel.CreateUnbounded<Work>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly List<string> _log = new();
    private readonly object _statusLock = new();

    private GameEngine _engine;
    private LiveValidator _validator;
    /// <summary>Filtre des lignes renvoyées une seconde fois (HDT relit la partie après un Rembobinage).</summary>
    private ReplayGuard _guard = new();
    private long _pendingSinceTimestamp;
    private bool _lastBatchLive;
    private LiveValidationReport? _liveReport;
    private DateTime _lastReportBuild = DateTime.MinValue;
    private string? _lastReportPath;
    private PipeServer? _pipe;
    private PowerLogTailer? _tailer;
    /// <summary>Lecture des lignes PowerTaskList du Power.log, pour l'horloge de l'affichage uniquement.</summary>
    private PowerLogTailer? _visualTailer;
    private readonly VisualClock _visualClock = new();
    private SourceKind _active = SourceKind.None;
    private Task? _worker;
    private Task? _ticker;
    private DateTime _startedAt;
    private DateTime? _lastLineAt;
    private long _linesReceived, _linesIgnored, _gaps;
    private string? _pluginVersion, _hdtVersion;
    private Decklist? _hdtDeck;
    private HdtCrossCheck? _lastCheck;
    private PipeMessage? _pendingCheck;
    private StreamWriter? _journal;
    private string? _journalPath;
    private DateTime _lastPublish = DateTime.MinValue;
    private int _publishedRevision = -1;
    private bool _publishPending;
    private long _ticks;

    public CopilotSession(CopilotConfig config, ICardDatabase cards, string cardDbMessage = "")
    {
        _config = config;
        _cards = cards;
        _cardDbMessage = cardDbMessage;
        _projector = new GameStateProjector(cards);
        _engine = NewEngine(0);
        _validator = NewValidator(_engine);
    }

    public event Action<GameStateSnapshot>? SnapshotUpdated;
    /// <summary>Le client Hearthstone a exécuté un nouveau tour ou une nouvelle étape (PowerTaskList).</summary>
    public event Action<VisualProgress>? VisualProgressChanged;
    public VisualClock VisualClock => _visualClock;
    public event Action<SessionStatus>? StatusChanged;

    public GameStateSnapshot? Latest { get; private set; }

    /// <summary>Mesures de la partie en cours (remplacées à chaque nouvelle partie ou changement de source).</summary>
    public MetricsRegistry CurrentMetrics => _validator.Metrics;

    public SessionStatus Status
    {
        get
        {
            lock (_statusLock)
            {
                return new SessionStatus
                {
                    ActiveSource = _active,
                    ConfiguredSource = _config.Source,
                    PluginConnected = _pipe?.IsConnected == true,
                    PluginVersion = _pluginVersion,
                    HdtVersion = _hdtVersion,
                    TailPath = _tailer?.CurrentPath,
                    LinesReceived = _linesReceived,
                    LinesIgnored = _linesIgnored,
                    LastLineAt = _lastLineAt,
                    JournalPath = _journalPath,
                    DeckName = (_hdtDeck ?? _config.GetActiveDecklist())?.Name,
                    CardDbMessage = _cardDbMessage,
                    LastCheck = _lastCheck,
                    PluginGaps = _gaps,
                    Log = _log.TakeLast(30).ToList(),
                    LiveReport = _liveReport,
                    LastReportPath = _lastReportPath,
                };
            }
        }
    }

    private GameEngine NewEngine(int initialRevision) =>
        new(new EngineOptions { Cards = _cards, LocalPlayerName = _config.LocalPlayerName, InitialRevision = initialRevision });

    private LiveValidator NewValidator(GameEngine engine)
    {
        var v = new LiveValidator(engine, _cards, () => _active switch { SourceKind.HdtPlugin => "hdt-plugin", SourceKind.LogTail => "log-tail", _ => "aucune" });
        v.ReportReady += SaveReport;
        return v;
    }

    /// <summary>Moteur neuf (changement de source ou fichier recréé) : révisions toujours croissantes, validateur neuf.</summary>
    private void ReplaceEngine()
    {
        _validator.ReportReady -= SaveReport;
        _validator.Dispose();
        _engine = NewEngine(_engine.Revision + 1);
        _validator = NewValidator(_engine);
        _guard = new ReplayGuard();
        lock (_statusLock) _liveReport = null;
    }

    private void SaveReport(LiveValidationReport report)
    {
        if (!_config.ReportsEnabled || report.Lines < 50) return;
        try
        {
            var path = LiveValidator.Save(report, _config.EffectiveReportsDir);
            lock (_statusLock) _lastReportPath = path;
            Note($"Rapport de validation ({report.Verdict}) : {path}");
        }
        catch (Exception ex)
        {
            Note($"Rapport non enregistré : {ex.Message}");
        }
    }

    /// <summary>À appeler par l'interface quand un état vient d'être affiché (mesure de latence d'affichage).</summary>
    public void RecordDisplayed(GameStateSnapshot snapshot) => Post(new DisplayedWork(snapshot, Stopwatch.GetTimestamp(), DateTime.Now));

    public void Start()
    {
        _startedAt = DateTime.UtcNow;
        _worker = Task.Run(() => WorkerAsync(_cts.Token));
        _ticker = Task.Run(() => TickerAsync(_cts.Token));
        var mode = _config.Source.ToLowerInvariant();
        _visualClock.Changed += p => VisualProgressChanged?.Invoke(p);
        if (_config.VisualClockFromLog && Directory.Exists(_config.HearthstoneLogsDir))
        {
            _visualTailer = new PowerLogTailer(_config.HearthstoneLogsDir, filter: VisualClock.IsPowerTaskList, startAtEnd: true);
            _visualTailer.LinesReceived += (lines, _) => { if (lines.Count > 0) Post(new VisualLinesWork(lines)); };
            _visualTailer.Start();
        }
        if (mode is "auto" or "hdt") StartPipe();
        if (mode == "log") Post(new Tick()); // le ticker démarre la lecture du log
        Note($"Session démarrée (source : {mode}). {_cardDbMessage}");
    }

    private void StartPipe()
    {
        _pipe = new PipeServer(_config.PipeName);
        _pipe.Status += Note;
        _pipe.ConnectionChanged += c => Post(new PluginConnection(c));
        _pipe.MessageReceived += m => Post(new PluginMessage(m, Stopwatch.GetTimestamp(), DateTime.UtcNow));
        _pipe.Start();
    }

    private void Post(Work w) => _work.Writer.TryWrite(w);

    private void Note(string message)
    {
        lock (_statusLock)
        {
            _log.Add($"{DateTime.Now:HH:mm:ss} {message}");
            if (_log.Count > 200) _log.RemoveRange(0, _log.Count - 200);
        }
        StatusChanged?.Invoke(Status);
    }

    private async Task TickerAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // 50 ms : un état retenu par la limitation de publication (100 ms) part au plus tard 50 ms après.
                await Task.Delay(50, ct).ConfigureAwait(false);
                Post(new Tick());
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task WorkerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var w in _work.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    Handle(w);
                }
                catch (Exception ex)
                {
                    Note($"Erreur de traitement : {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    // ------------------------------------------------------------------ Traitement (fil unique)

    private void Handle(Work w)
    {
        switch (w)
        {
            case LinesWork lw:
                OnLines(lw);
                break;
            case PluginConnection pc:
                OnPluginConnection(pc.Connected);
                break;
            case PluginMessage pm:
                OnPluginMessage(pm);
                break;
            case DisplayedWork dw:
                OnDisplayed(dw);
                break;
            case VisualLinesWork vw:
                foreach (var l in vw.Lines)
                    if (_visualClock.OnLine(l) && _engine.HasGame) WriteJournal(l);
                break;
            case Tick:
                OnTick();
                break;
        }
    }

    private void SwitchSource(SourceKind source, string reason)
    {
        if (_active == source) return;
        if (source != SourceKind.LogTail && _tailer is not null)
        {
            _tailer.Dispose();
            _tailer = null;
        }
        _active = source;
        ReplaceEngine();
        CloseJournal();
        _hdtDeck = source == SourceKind.HdtPlugin ? _hdtDeck : null;
        Note($"Source active : {source} ({reason}). État reconstruit depuis le début de la partie.");
        Publish(force: true);
    }

    private void StartTailer()
    {
        if (_tailer is not null) return;
        _tailer = new PowerLogTailer(_config.HearthstoneLogsDir);
        _tailer.Status += Note;
        _tailer.LinesReceived += (lines, restart) => Post(new LinesWork(SourceKind.LogTail, lines, restart, Stopwatch.GetTimestamp(), restart));
        _tailer.Start();
    }

    private void OnTick()
    {
        var mode = _config.Source.ToLowerInvariant();
        var pluginConnected = _pipe?.IsConnected == true;
        if (mode == "log" && _active != SourceKind.LogTail)
        {
            SwitchSource(SourceKind.LogTail, "configuration");
            StartTailer();
        }
        else if (mode == "auto" && !pluginConnected && _active != SourceKind.LogTail &&
                 DateTime.UtcNow - _startedAt > TimeSpan.FromSeconds(_config.AutoFallbackSeconds))
        {
            SwitchSource(SourceKind.LogTail, "plugin HDT absent");
            StartTailer();
        }
        if (_pendingCheck is not null) EvaluateCheck(_pendingCheck);
        if (_publishPending) Publish(force: false);
        if (++_ticks % 20 == 0) _journal?.Flush();
    }

    private void OnPluginConnection(bool connected)
    {
        if (connected)
        {
            SwitchSource(SourceKind.HdtPlugin, "plugin HDT connecté");
        }
        else if (_active == SourceKind.HdtPlugin)
        {
            Note("Plugin HDT déconnecté.");
            if (_config.Source.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                SwitchSource(SourceKind.LogTail, "repli après déconnexion du plugin");
                StartTailer();
            }
        }
    }

    private void OnPluginMessage(PluginMessage pm)
    {
        var m = pm.Message;
        switch (m.Type)
        {
            case PipeProtocol.TypeHello:
                _pluginVersion = m.PluginVersion;
                _hdtVersion = m.HdtVersion;
                Note($"Plugin {m.PluginVersion} (HDT {m.HdtVersion}, protocole {m.Protocol}).");
                if (m.Protocol != PipeProtocol.Version) Note($"Version de protocole inattendue : {m.Protocol} (attendue {PipeProtocol.Version}).");
                break;
            case PipeProtocol.TypeLines when m.Lines is not null:
                if (m.Backlog != true && DateTime.TryParse(m.SentAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sentAt))
                    _validator.Metrics.Record(MetricNames.PluginToReceive, (pm.ReceivedUtc - sentAt.ToUniversalTime()).TotalMilliseconds);
                OnLines(new LinesWork(SourceKind.HdtPlugin, m.Lines, false, pm.ReceivedTimestamp, m.Backlog == true));
                break;
            case PipeProtocol.TypeDeck when m.Deck is not null:
                _hdtDeck = new Decklist
                {
                    Source = "HDT",
                    Name = m.Deck.Name,
                    Cards = m.Deck.Cards.Where(c => c.Count > 0 && c.Id.Length > 0).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.Sum(c => c.Count)),
                };
                Note($"Deck actif HDT : {m.Deck.Name} ({_hdtDeck.Total} cartes).");
                Publish(force: true);
                break;
            case PipeProtocol.TypeHdtCheck when m.Check is not null:
                _pendingCheck = m;
                break;
            case PipeProtocol.TypeGap:
                _gaps += m.Dropped ?? 0;
                Note($"Le plugin signale {m.Dropped} ligne(s) perdue(s) : état potentiellement incomplet.");
                break;
            case PipeProtocol.TypeHdtEvent:
                Note($"HDT : {m.Event}{(m.Detail is null ? "" : " — " + m.Detail)}");
                break;
        }
    }

    private void OnLines(LinesWork lw)
    {
        if (lw.Source != _active)
        {
            _linesIgnored += lw.Lines.Count;
            return; // jamais deux sources de vérité
        }
        if (lw.Restart && _engine.LinesInGame > 0)
        {
            ReplaceEngine();
            CloseJournal();
        }
        PowerLogLine? lastParsed = null;
        var skipped = 0;
        var episodes = _guard.Episodes;
        foreach (var line in lw.Lines)
        {
            var parsed = PowerLogLineParser.TryParse(line);
            if (parsed is null)
            {
                // PowerTaskList (rejeu, rejouage ou plugin qui les transmet) : horloge de l'affichage seulement, jamais le moteur.
                if (_visualClock.OnLine(line)) WriteJournal(line);
                continue;
            }
            var create = ReplayFile.IsCreateGame(line);
            if (!_guard.Accept(parsed, line, _engine.HasGame, create))
            {
                skipped++;
                continue;
            }
            if (create) CloseJournal();
            _validator.BeforeLine(parsed);
            _engine.Process(parsed);
            _validator.AfterLine(parsed);
            WriteJournal(line);
            lastParsed = parsed;
        }
        if (_guard.Episodes > episodes)
            Note($"Le flux renvoie des lignes déjà traitées (HDT relit la partie, par exemple après un Rembobinage) : elles sont ignorées jusqu'à la première ligne nouvelle.");
        if (skipped > 0) _validator.Metrics.Increment("flux : lignes renvoyées ignorées", skipped);
        if (_guard.ResumedAt is TimeSpan resumed && !_guard.Skipping && skipped > 0)
            Note($"Reprise du flux à {resumed:hh\\:mm\\:ss\\.fff} après {skipped} ligne(s) renvoyée(s) ignorée(s).");
        if (lastParsed is null && skipped > 0) return; // lot entièrement renvoyé : aucun changement d'état
        // Latence côté jeu : horodatage écrit par Hearthstone → réception (flux direct uniquement, pas les tampons).
        _lastBatchLive = !lw.Backlog && !lw.Restart;
        if (_lastBatchLive && lastParsed is not null && lw.ReceivedTimestamp != 0)
        {
            var receivedLocal = DateTime.Now - Stopwatch.GetElapsedTime(lw.ReceivedTimestamp);
            _validator.Metrics.Record(MetricNames.LogToReceive, MetricsRegistry.MillisecondsSinceLogTime(lastParsed.Time, receivedLocal));
        }
        if (_pendingSinceTimestamp == 0) _pendingSinceTimestamp = lw.ReceivedTimestamp != 0 ? lw.ReceivedTimestamp : Stopwatch.GetTimestamp();
        _linesReceived += lw.Lines.Count;
        if (lw.Lines.Count > 0) _lastLineAt = DateTime.UtcNow;
        _publishPending = true;
        Publish(force: false);
    }

    // ------------------------------------------------------------------ Contrôle croisé HDT

    private void EvaluateCheck(PipeMessage m)
    {
        var stable = _lastLineAt is null || DateTime.UtcNow - _lastLineAt > QuietPeriod;
        if (!stable) return; // HDT suit les animations : on attend que tout soit calme
        _pendingCheck = null;
        var c = m.Check!;
        var check = new HdtCrossCheck { At = DateTime.Now, Stable = true };
        var e = _engine;
        var local = e.LocalPlayerId;
        if (local is null)
        {
            check.Items.Add(("joueur local", false, "non identifié par le moteur"));
        }
        else
        {
            var opp = e.Players.Keys.FirstOrDefault(p => p != local);
            check.Items.Add(("joueur local", c.Local.PlayerId == local, $"HDT P{c.Local.PlayerId}, moteur P{local}"));
            ComparePlayer(check, "vous", c.Local, local.Value, e);
            ComparePlayer(check, "adversaire", c.Opponent, opp, e);
        }
        lock (_statusLock) _lastCheck = check;
        _validator.OnHdtCheck(check.AllOk, check.Items.Where(i => !i.Ok).Select(i => $"{i.Item} ({i.Detail})"));
        if (!check.AllOk) Note("Contrôle HDT : écart(s) — " + string.Join(" ; ", check.Items.Where(i => !i.Ok).Select(i => $"{i.Item} ({i.Detail})")));
        Publish(force: true);
    }

    private static void ComparePlayer(HdtCrossCheck check, string who, HdtPlayerCheck hdt, int playerId, GameEngine e)
    {
        var live = e.Entities.Values.Where(x => !x.Orphaned && x.Controller == playerId).ToList();
        var hand = live.Where(x => x.Zone == "HAND").OrderBy(x => x.ZonePosition).Select(x => x.Id).ToList();
        var board = live.Where(x => x.Zone == "PLAY" && x.CardType is "MINION" or "LOCATION").OrderBy(x => x.ZonePosition).Select(x => x.Id).ToList();
        var deck = live.Count(x => x.Zone == "DECK");
        var pe = e.PlayerEntity(playerId);
        var hero = pe is null ? null : e.Entities.GetValueOrDefault(pe.GetInt("HERO_ENTITY"));
        var hp = hero is null ? 0 : hero.GetInt("HEALTH") - hero.GetInt("DAMAGE");
        check.Items.Add(($"{who} : main", hand.Count == hdt.HandCount && (hdt.Hand.Count == 0 || hand.SequenceEqual(hdt.Hand)), $"HDT {hdt.HandCount}, moteur {hand.Count}"));
        check.Items.Add(($"{who} : plateau", board.ToHashSet().SetEquals(hdt.Board), $"HDT [{string.Join(",", hdt.Board)}], moteur [{string.Join(",", board)}]"));
        check.Items.Add(($"{who} : deck", deck == hdt.DeckCount, $"HDT {hdt.DeckCount}, moteur {deck}"));
        check.Items.Add(($"{who} : PV héros", hp == hdt.HeroHealth, $"HDT {hdt.HeroHealth}, moteur {hp}"));
        check.Items.Add(($"{who} : armure", (hero?.GetInt("ARMOR") ?? 0) == hdt.HeroArmor, $"HDT {hdt.HeroArmor}, moteur {hero?.GetInt("ARMOR") ?? 0}"));
    }

    // ------------------------------------------------------------------ Publication

    private void Publish(bool force)
    {
        if (!force && (DateTime.UtcNow - _lastPublish < PublishInterval || _engine.Revision == _publishedRevision))
        {
            _publishPending = _engine.Revision != _publishedRevision;
            // Lot sans changement d'état : rien à publier, il ne doit pas compter dans la latence « réception → état publié ».
            if (!_publishPending) _pendingSinceTimestamp = 0;
            return;
        }
        var flags = new List<UncertaintyFlag>();
        if (_gaps > 0) flags.Add(new UncertaintyFlag { Code = "SOURCE_GAP", Severity = "critical", Message = $"{_gaps} ligne(s) perdue(s) par le plugin.", Count = (int)_gaps });
        if (_lastCheck is { AllOk: false } chk)
            flags.Add(new UncertaintyFlag
            {
                Code = "HDT_MISMATCH",
                Severity = "warning",
                Message = "Écart avec HDT au repos : " + string.Join(" ; ", chk.Items.Where(i => !i.Ok).Select(i => $"{i.Item} ({i.Detail})")),
            });
        var snapshot = _projector.Project(_engine, new ProjectionContext
        {
            Transport = _active switch { SourceKind.HdtPlugin => "hdt-plugin", SourceKind.LogTail => "log-tail", _ => "aucune" },
            TransportDetail = _active == SourceKind.LogTail ? _tailer?.CurrentPath : _hdtVersion is null ? null : $"HDT {_hdtVersion}",
            Decklist = _hdtDeck ?? _config.GetActiveDecklist(),
            ExtraFlags = flags,
        });
        snapshot.PublishedTimestamp = Stopwatch.GetTimestamp();
        snapshot.LiveTimestamps = _lastBatchLive && _active != SourceKind.None;
        if (_pendingSinceTimestamp != 0)
        {
            _validator.Metrics.Record(MetricNames.ReceiveToState, Stopwatch.GetElapsedTime(_pendingSinceTimestamp).TotalMilliseconds);
            _pendingSinceTimestamp = 0;
        }
        _validator.OnSnapshot(snapshot);
        if (DateTime.UtcNow - _lastReportBuild > TimeSpan.FromSeconds(1) || snapshot.Game.GameOver)
        {
            var report = _validator.BuildReport();
            lock (_statusLock) _liveReport = report;
            _lastReportBuild = DateTime.UtcNow;
        }
        Latest = snapshot;
        _publishedRevision = _engine.Revision;
        _lastPublish = DateTime.UtcNow;
        _publishPending = false;
        SnapshotUpdated?.Invoke(snapshot);
        StatusChanged?.Invoke(Status);
    }

    private void OnDisplayed(DisplayedWork dw)
    {
        var s = dw.Snapshot;
        if (s.PublishedTimestamp != 0)
            _validator.Metrics.Record(MetricNames.StateToDisplay, (dw.DisplayedTimestamp - s.PublishedTimestamp) * 1000.0 / Stopwatch.Frequency);
        // Première apparition de cette révision seulement : une republication du même état (contrôle HDT, deck) plusieurs
        // secondes après la dernière ligne n'est pas un affichage en retard.
        if (s.LiveTimestamps && s.Revision > _lastLogToDisplayRevision)
        {
            _lastLogToDisplayRevision = s.Revision;
            _validator.Metrics.Record(MetricNames.LogToDisplay, MetricsRegistry.MillisecondsSinceLogTime(s.LastLogTimeOfDay, dw.DisplayedLocal));
        }
    }

    private int _lastLogToDisplayRevision = -1;

    /// <summary>Rapport de la partie en cours, construit à la demande (à appeler une fois la session arrêtée ou depuis les tests).</summary>
    public LiveValidationReport BuildReportNow() => _validator.BuildReport();

    // ------------------------------------------------------------------ Journal brut

    private void WriteJournal(string line)
    {
        if (!_config.JournalEnabled) return;
        if (_journal is null)
        {
            try
            {
                Directory.CreateDirectory(_config.EffectiveJournalDir);
                var source = _active == SourceKind.HdtPlugin ? "hdt" : "log";
                _journalPath = Path.Combine(_config.EffectiveJournalDir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{source}.log");
                _journal = new StreamWriter(new FileStream(_journalPath, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = false };
                Note($"Journal : {_journalPath}");
            }
            catch (Exception ex)
            {
                Note($"Journal désactivé : {ex.Message}");
                _config.JournalEnabled = false;
                return;
            }
        }
        _journal.WriteLine(line);
    }

    private void CloseJournal()
    {
        _journal?.Flush();
        _journal?.Dispose();
        _journal = null;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_pipe is not null) await _pipe.DisposeAsync().ConfigureAwait(false);
        _tailer?.Dispose();
        _visualTailer?.Dispose();
        try { if (_worker is not null) await _worker.ConfigureAwait(false); } catch { /* arrêt */ }
        try { if (_ticker is not null) await _ticker.ConfigureAwait(false); } catch { /* arrêt */ }
        // Partie interrompue : rapport partiel, utile pour la vérification en direct.
        if (_engine.HasGame && _engine.LinesInGame >= 50 && _liveReport is { Finished: false }) SaveReport(_validator.BuildReport());
        _validator.Dispose();
        CloseJournal();
        _cts.Dispose();
    }
}
