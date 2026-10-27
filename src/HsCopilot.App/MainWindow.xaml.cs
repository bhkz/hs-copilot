using System.IO;
using System.Windows;
using System.Windows.Controls;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using Microsoft.Win32;

namespace HsCopilot.App;

public partial class MainWindow : Window
{
    private sealed record ReplayPoint(string Label, int LineIndex)
    {
        public override string ToString() => Label;
    }

    private readonly StartupOptions _startup;
    private readonly MainViewModel _vm = new();
    private readonly object _lock = new();
    private CopilotConfig _config = new();
    private ICardDatabase _cards = CardDatabase.Empty;
    private CopilotSession? _session;
    private GameStateSnapshot? _latestLive;
    private SessionStatus? _latestStatus;
    private bool _snapshotScheduled, _statusScheduled;

    private bool _fileMode;
    private string? _filePath;
    private List<LoggedGame> _games = new();

    public MainWindow(StartupOptions startup)
    {
        _startup = startup;
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await InitAsync();
        Closed += async (_, _) =>
        {
            if (_session is not null) await _session.DisposeAsync();
        };
    }

    private async Task InitAsync()
    {
        _vm.SourceText = "Chargement de la configuration et de la base de cartes…";
        string cardMessage = "";
        string? configPath = null;
        try
        {
            await Task.Run(() =>
            {
                (_config, configPath) = CopilotConfig.Load(_startup.ConfigPath);
                if (_startup.Source is not null) _config.Source = _startup.Source;
                var (db, msg) = CardDbLocator.LoadBest(_config.CardDbPath, _config.HdtCardDefsDir);
                _cards = db;
                cardMessage = msg;
            });
        }
        catch (Exception ex)
        {
            _vm.SourceText = "Erreur de démarrage : " + ex.Message;
            return;
        }

        _session = new CopilotSession(_config, _cards, cardMessage);
        _session.SnapshotUpdated += OnLiveSnapshot;
        _session.StatusChanged += OnStatus;
        InitAdvisor(_session, configPath);
        _session.Start();
        _vm.ModeText = $"Direct · configuration : {configPath ?? "par défaut"}";

        if (_startup.ReplayPath is not null) await OpenReplayAsync(_startup.ReplayPath, _startup.Turn);
    }

    // ------------------------------------------------------------------ Direct

    private void OnLiveSnapshot(GameStateSnapshot s)
    {
        lock (_lock)
        {
            _latestLive = s;
            if (_snapshotScheduled) return;
            _snapshotScheduled = true;
        }
        Dispatcher.BeginInvoke(() =>
        {
            GameStateSnapshot? snap;
            lock (_lock)
            {
                snap = _latestLive;
                _snapshotScheduled = false;
            }
            if (!_fileMode && snap is not null)
            {
                _vm.Apply(snap);
                // Mesure « état publié → affichage » : rappel après le rendu de la mise à jour.
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => _session?.RecordDisplayed(snap));
            }
        });
    }

    private void OnStatus(SessionStatus st)
    {
        lock (_lock)
        {
            _latestStatus = st;
            if (_statusScheduled) return;
            _statusScheduled = true;
        }
        Dispatcher.BeginInvoke(() =>
        {
            SessionStatus? status;
            lock (_lock)
            {
                status = _latestStatus;
                _statusScheduled = false;
            }
            if (status is not null) _vm.ApplyStatus(status, _fileMode);
        });
    }

    private void Live_Click(object sender, RoutedEventArgs e)
    {
        _fileMode = false;
        GameCombo.Visibility = PointCombo.Visibility = PrevButton.Visibility = NextButton.Visibility = LiveButton.Visibility = System.Windows.Visibility.Collapsed;
        _vm.ModeText = "Direct";
        var live = _session?.Latest;
        if (live is not null) _vm.Apply(live);
        if (_session is not null) _vm.ApplyStatus(_session.Status, false);
    }

    // ------------------------------------------------------------------ Fichier (simulation hors ligne)

    private async void OpenReplay_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir un enregistrement de partie",
            Filter = "Replays HDT (*.hdtreplay)|*.hdtreplay|Power.log / journaux (*.log;*.txt)|*.log;*.txt|Tous les fichiers (*.*)|*.*",
            InitialDirectory = Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "Replays"))
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HearthstoneDeckTracker", "Replays")
                : null,
        };
        if (dialog.ShowDialog(this) == true) await OpenReplayAsync(dialog.FileName, null);
    }

    private async Task OpenReplayAsync(string path, int? turn)
    {
        try
        {
            var lines = await Task.Run(() => ReplayFile.ReadAllLines(path));
            _games = ReplayFile.SplitGames(lines);
            if (_games.Count == 0)
            {
                MessageBox.Show(this, "Aucune ligne GameState dans ce fichier.", "HS Copilot");
                return;
            }
            _filePath = path;
            _fileMode = true;
            GameCombo.Visibility = PointCombo.Visibility = PrevButton.Visibility = NextButton.Visibility = LiveButton.Visibility = System.Windows.Visibility.Visible;
            _vm.SourceText = "● Fichier : " + Path.GetFileName(path);
            _vm.SourceColor = System.Windows.Media.Brushes.LightSkyBlue;
            _vm.ModeText = "Hors ligne (le direct continue en arrière-plan)";
            GameCombo.ItemsSource = _games.Select(g => $"Partie {g.Index} (lignes {g.StartLine + 1}–{g.EndLine})").ToList();
            _pendingTurn = turn;
            GameCombo.SelectedIndex = _games.Count - 1;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Lecture impossible");
        }
    }

    private int? _pendingTurn;

    private void GameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GameCombo.SelectedIndex < 0 || GameCombo.SelectedIndex >= _games.Count) return;
        var game = _games[GameCombo.SelectedIndex];
        var points = ComputePoints(game.Lines);
        PointCombo.ItemsSource = points;
        var target = _pendingTurn is int t ? points.FindIndex(p => p.Label.StartsWith($"Tour {t} — vous", StringComparison.Ordinal)) : -1;
        _pendingTurn = null;
        PointCombo.SelectedIndex = target >= 0 ? target : points.Count - 1;
    }

    /// <summary>Points de navigation : mulligan, début de décision de chaque tour local, début de chaque tour adverse, fin.</summary>
    private List<ReplayPoint> ComputePoints(IReadOnlyList<string> lines)
    {
        var points = new List<ReplayPoint>();
        var engine = new GameEngine(new EngineOptions { Cards = _cards, LocalPlayerName = _config.LocalPlayerName });
        var lastLocalTurn = -1;
        var lastOppTurn = -1;
        var mulligan = false;
        for (int i = 0; i < lines.Count; i++)
        {
            var parsed = PowerLogLineParser.TryParse(lines[i]);
            if (parsed is null) continue;
            engine.Process(parsed);
            if (!mulligan && parsed.Method == "DebugPrintEntityChoices" && parsed.Body.Contains("ChoiceType=MULLIGAN", StringComparison.Ordinal))
            {
                mulligan = true;
                points.Add(new ReplayPoint("Mulligan", Math.Min(i + 8, lines.Count)));
            }
            var turn = (engine.GameTurn + 1) / 2;
            var localTurn = engine.LocalPlayerId is not null && engine.CurrentPlayerId == engine.LocalPlayerId;
            if (parsed.Method == "DebugPrintOptions" && parsed.Body.StartsWith("id=", StringComparison.Ordinal) && localTurn && engine.GameTurn != lastLocalTurn)
            {
                lastLocalTurn = engine.GameTurn;
                var j = i + 1;
                while (j < lines.Count && lines[j].Contains("GameState.DebugPrintOptions()", StringComparison.Ordinal)) j++;
                points.Add(new ReplayPoint($"Tour {turn} — vous (début de décision)", j));
            }
            else if (!localTurn && engine.GameTurn != lastOppTurn && engine.Step == "MAIN_ACTION" && engine.LocalPlayerId is not null)
            {
                lastOppTurn = engine.GameTurn;
                points.Add(new ReplayPoint($"Tour {turn} — adversaire (début)", i + 1));
            }
        }
        points.Add(new ReplayPoint("Fin de l'enregistrement", lines.Count));
        return points;
    }

    private void PointCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PointCombo.SelectedItem is not ReplayPoint point || GameCombo.SelectedIndex < 0) return;
        var game = _games[GameCombo.SelectedIndex];
        var engine = new GameEngine(new EngineOptions { Cards = _cards, LocalPlayerName = _config.LocalPlayerName });
        ReplayRunner.RunRange(engine, game.Lines, 0, point.LineIndex);
        var snapshot = new GameStateProjector(_cards).Project(engine, new ProjectionContext
        {
            Transport = "replay-file",
            TransportDetail = Path.GetFileName(_filePath),
            Decklist = _config.GetActiveDecklist(),
        });
        _vm.Apply(snapshot);
    }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (PointCombo.SelectedIndex > 0) PointCombo.SelectedIndex--;
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (PointCombo.SelectedIndex < PointCombo.Items.Count - 1) PointCombo.SelectedIndex++;
    }

    private void OpenReports_Click(object sender, RoutedEventArgs e)
    {
        var dir = _config.EffectiveReportsDir;
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    private void CopyJson_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_vm.Json)) Clipboard.SetText(_vm.Json);
    }

    private void Bench_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Dossier d'exécution du banc d'essai (contient results.jsonl)" };
        if (dialog.ShowDialog(this) == true) new BenchWindow(dialog.FolderName) { Owner = this }.Show();
    }
}
