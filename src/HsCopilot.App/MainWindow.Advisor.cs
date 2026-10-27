using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using HsCopilot.Core.Config;
using HsCopilot.Core.Session;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;

namespace HsCopilot.App;

/// <summary>Conseiller en direct : réglages, test du fournisseur, overlay et raccourcis.</summary>
public partial class MainWindow
{
    private AdvisorHost? _advisor;
    private OverlayWindow? _overlay;
    private readonly OverlayViewModel _overlayVm = new();
    private readonly AdvisorPanelViewModel _panel = new();
    private AdvisorUpdate? _lastAdvisorUpdate;
    private DispatcherTimer? _overlayTimer;
    private string? _configPath;
    private bool _overlayMoved;
    private string _reanalyzeHotkey = "Ctrl+Alt+R";

    private void InitAdvisor(CopilotSession session, string? configPath)
    {
        _configPath = configPath;
        AdvisorTab.DataContext = _panel;
        FillAdvisorControls(_config.Advisor);
        RefreshKeyStatus();
        _advisor = new AdvisorHost(_config.Advisor, () => session.CurrentMetrics, traceDir: System.IO.Path.Combine(CopilotConfig.DefaultDataDir, "advisor"));
        _advisor.Updated += u => Dispatcher.BeginInvoke(() => ApplyAdvisorUpdate(u));
        session.SnapshotUpdated += s => _advisor?.OnSnapshot(s);
        // Horloge de l'affichage (PowerTaskList) : un conseil n'est présenté comme jouable qu'une fois ton tour affiché.
        session.VisualProgressChanged += p => _advisor?.OnVisualProgress(p);
        ApplyAdvisorUpdate(_advisor.DisabledReason is string why
            ? new AdvisorUpdate { Phase = AdvisorPhases.Disabled, Message = why }
            : new AdvisorUpdate { Phase = AdvisorPhases.Idle, Message = "En attente d'une partie : lance Hearthstone puis une partie d'entraînement contre l'IA." });
        if (_config.Advisor.OverlayEnabled) ShowOverlay();
        _overlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _overlayTimer.Tick += (_, _) =>
        {
            if (_lastAdvisorUpdate is { Phase: AdvisorPhases.Analyzing or AdvisorPhases.Partial } u) _overlayVm.Apply(View(u));
        };
        _overlayTimer.Start();
        Closed += async (_, _) =>
        {
            _overlayTimer?.Stop();
            _overlay?.Close();
            if (_overlayMoved)
            {
                try { _config.Save(_configPath); } catch { /* position non enregistrée */ }
            }
            if (_advisor is not null) await _advisor.DisposeAsync();
        };
        UpdateOverlayHeader();
    }

    private void ApplyAdvisorUpdate(AdvisorUpdate u)
    {
        _lastAdvisorUpdate = u;
        _overlayVm.Apply(View(u));
        _overlayVm.Paused = u.Paused;
        _panel.Apply(u);
        UpdateOverlayHeader();
    }

    /// <summary>Texte de l'overlay, avec le raccourci de réanalyse réellement actif.</summary>
    private AdviceView View(AdvisorUpdate u)
    {
        var v = AdviceDisplay.Build(u, DateTime.UtcNow);
        return _reanalyzeHotkey == "Ctrl+Alt+R" ? v : v with { StatusText = v.StatusText.Replace("Ctrl+Alt+R", _reanalyzeHotkey) };
    }

    private void UpdateOverlayHeader()
    {
        var s = _advisor?.Settings ?? _config.Advisor;
        var demo = s.IsSimulated;
        _overlayVm.HeaderText = (demo ? "HS Copilot · mode démo, latence simulée, aucune stratégie" : $"HS Copilot · {s.EffectiveModel} · {s.EffectiveEffort}")
                                + (_overlayVm.Paused ? " · en pause" : "");
        _panel.EffectiveText = $"En vigueur : {s.EffectiveModel} ({s.ModelSource}), effort {s.EffectiveEffort} ({s.EffortSource}), " +
                               $"coût estimé ≈ {s.EstimatedCallCostUsd:0.000} $ par analyse, au plus {s.MaxCallsPerTurn} analyses automatiques par tour.";
    }

    private void RefreshKeyStatus()
    {
        var model = _config.Advisor.EffectiveModel;
        var variable = StrategyProviders.KeyVariable(model);
        var (present, hint) = StrategyProviders.KeyStatus(model);
        _panel.KeyText = present
            ? $"✔ Clé {StrategyProviders.ProviderName(model)} détectée ({hint}) dans la variable d'environnement {variable}. Rien à configurer."
            : $"✖ Clé {StrategyProviders.ProviderName(model)} absente. Dans PowerShell : setx {variable} \"ta-clé\", puis relance l'application.";
        _panel.KeyColor = present ? Palette.Good : Palette.Bad;
    }

    private void FillAdvisorControls(AdvisorSettings s)
    {
        ModelCombo.ItemsSource = ModelPricing.KnownModels.OrderBy(m => m).ToList();
        ModelCombo.Text = s.EffectiveModel;
        EffortCombo.ItemsSource = AdvisorSettings.Efforts;
        EffortCombo.SelectedItem = AdvisorSettings.Efforts.Contains(s.EffectiveEffort) ? s.EffectiveEffort : AdvisorSettings.DefaultEffort;
        CallsBox.Text = s.MaxCallsPerTurn.ToString(CultureInfo.InvariantCulture);
        GameBudgetBox.Text = s.MaxCostPerGameUsd.ToString("0.00", CultureInfo.InvariantCulture);
        SessionBudgetBox.Text = s.MaxCostPerSessionUsd.ToString("0.00", CultureInfo.InvariantCulture);
        TimeoutBox.Text = s.TimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        EnabledCheck.IsChecked = s.Enabled;
        StreamCheck.IsChecked = s.Streaming;
        ChoicesCheck.IsChecked = s.AdviseChoices;
        RankedCheck.IsChecked = s.AllowRanked;
    }

    private static double ParseDouble(string text, double fallback) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v >= 0 ? v : fallback;

    private static int ParseInt(string text, int fallback) => int.TryParse(text, out var v) && v > 0 ? v : fallback;

    private AdvisorSettings ReadAdvisorControls()
    {
        var old = _config.Advisor;
        var model = string.IsNullOrWhiteSpace(ModelCombo.Text) ? null : ModelCombo.Text.Trim();
        return new AdvisorSettings
        {
            Enabled = EnabledCheck.IsChecked == true,
            Provider = old.Provider,
            SimulatedLatencyMs = old.SimulatedLatencyMs,
            Model = model,
            Effort = EffortCombo.SelectedItem as string ?? old.Effort,
            TimeoutSeconds = ParseInt(TimeoutBox.Text, old.TimeoutSeconds),
            MaxCallsPerTurn = ParseInt(CallsBox.Text, old.MaxCallsPerTurn),
            MaxCallsPerGame = old.MaxCallsPerGame,
            MaxCostPerGameUsd = ParseDouble(GameBudgetBox.Text, old.MaxCostPerGameUsd),
            MaxCostPerSessionUsd = ParseDouble(SessionBudgetBox.Text, old.MaxCostPerSessionUsd),
            DebounceMs = old.DebounceMs,
            MidTurnDebounceMs = old.MidTurnDebounceMs,
            RefundCancelledCalls = old.RefundCancelledCalls,
            Streaming = StreamCheck.IsChecked == true,
            AdviseChoices = ChoicesCheck.IsChecked == true,
            AllowRanked = RankedCheck.IsChecked == true,
            HistoryItems = old.HistoryItems,
            MaxOutputTokens = old.MaxOutputTokens,
            StrategyFile = old.StrategyFile,
            OverlayEnabled = old.OverlayEnabled,
            OverlayCompact = _overlayVm.Compact,
            OverlayLeft = old.OverlayLeft,
            OverlayTop = old.OverlayTop,
            OverlayOpacity = old.OverlayOpacity,
        };
    }

    private async void SaveAdvisor_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadAdvisorControls();
        _config.Advisor = settings;
        try
        {
            _configPath = _config.Save(_configPath);
            SaveText.Text = $"Enregistré dans {_configPath}";
        }
        catch (Exception ex)
        {
            SaveText.Text = "Enregistrement impossible : " + ex.Message;
        }
        if (_advisor is not null) await _advisor.ReconfigureAsync(settings);
        RefreshKeyStatus();
        UpdateOverlayHeader();
    }

    private async void Ping_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReadAdvisorControls();
        _panel.PingText = $"Test de {settings.EffectiveModel} en cours…";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var r = await StrategyProviders.PingAsync(settings, cts.Token);
            _panel.PingText = r.Ok ? $"✔ {r.Message} Coût du test : {r.CostUsd ?? 0:0.00000} $." : $"✖ {r.Message}";
        }
        catch (OperationCanceledException)
        {
            _panel.PingText = "✖ Pas de réponse en 60 s.";
        }
    }

    // ------------------------------------------------------------------ Overlay

    private void ShowOverlay()
    {
        if (_overlay is null)
        {
            _overlayVm.Compact = _config.Advisor.OverlayCompact;
            _overlay = new OverlayWindow(_overlayVm);
            var area = SystemParameters.WorkArea;
            var left = _config.Advisor.OverlayLeft;
            var top = _config.Advisor.OverlayTop;
            var onScreen = left is double l && top is double t && l >= area.Left - 50 && l <= area.Right - 100 && t >= area.Top - 20 && t <= area.Bottom - 60;
            _overlay.Left = onScreen ? left!.Value : area.Right - _overlay.Width - 24;
            _overlay.Top = onScreen ? top!.Value : area.Top + 90;
            _overlay.Opacity = Math.Clamp(_config.Advisor.OverlayOpacity, 0.4, 1.0);
            _overlay.ReanalyzeRequested += Reanalyze;
            _overlay.PauseToggleRequested += TogglePause;
            _overlay.CompactToggleRequested += () => _overlayVm.Compact = !_overlayVm.Compact;
            _overlay.VisibilityToggleRequested += ToggleOverlay;
            _overlay.Moved += (x, y) =>
            {
                _config.Advisor.OverlayLeft = x;
                _config.Advisor.OverlayTop = y;
                _overlayMoved = true;
            };
            _overlay.Show();
            _panel.HotkeysText = $"Raccourcis : {_overlay.HotkeyFor(1)} afficher/masquer · {_overlay.HotkeyFor(2)} réanalyser · {_overlay.HotkeyFor(3)} pause. Glisse l'en-tête pour déplacer le panneau."
                + (_overlay.HotkeyFallbacks.Count > 0 ? $" ({string.Join(" ; ", _overlay.HotkeyFallbacks)}.)" : "")
                + (_overlay.HotkeyErrors.Count > 0 ? $" Indisponibles : {string.Join(", ", _overlay.HotkeyErrors)} ; les boutons du panneau restent utilisables." : "");
            _reanalyzeHotkey = _overlay.HotkeyFor(2);
            return;
        }
        _overlay.Show();
    }

    private void ToggleOverlay()
    {
        if (_overlay is null || !_overlay.IsVisible) ShowOverlay();
        else _overlay.Hide();
    }

    private void Reanalyze()
    {
        if (_advisor is null || !_advisor.RequestAnalysis())
            _overlayVm.StatusText = _advisor?.DisabledReason ?? "Aucune décision à analyser pour l'instant (attends ton tour ou un choix).";
    }

    private void TogglePause()
    {
        var paused = !(_advisor?.Coordinator?.Paused ?? false);
        _advisor?.SetPaused(paused);
        _overlayVm.Paused = paused;
        UpdateOverlayHeader();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e) => ToggleOverlay();
    private void ToggleCompact_Click(object sender, RoutedEventArgs e) => _overlayVm.Compact = !_overlayVm.Compact;
    private void TogglePause_Click(object sender, RoutedEventArgs e) => TogglePause();
    private void Reanalyze_Click(object sender, RoutedEventArgs e) => Reanalyze();
}
