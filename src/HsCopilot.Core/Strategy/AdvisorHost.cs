using HsCopilot.Core.Config;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy.Providers;

namespace HsCopilot.Core.Strategy;

/// <summary>
/// Relie la session en direct au coordinateur : construit le fournisseur OpenAI à partir des réglages, relaie chaque état
/// publié, et permet de reconfigurer à chaud (modèle, effort, plafonds) sans redémarrer la session.
/// </summary>
public sealed class AdvisorHost : IAsyncDisposable
{
    private readonly Func<MetricsRegistry> _metrics;
    private readonly Func<AdvisorSettings, IStrategyProvider>? _providerFactory;
    private readonly object _lock = new();
    private StrategyCoordinator? _coordinator;
    private GameStateSnapshot? _latest;
    private Recommendation? _mulliganAdvice;
    private string? _mulliganTracedFor;

    public AdvisorHost(AdvisorSettings settings, Func<MetricsRegistry> metrics, Func<AdvisorSettings, IStrategyProvider>? providerFactory = null, string? traceDir = null)
    {
        _metrics = metrics;
        _providerFactory = providerFactory;
        if (traceDir is not null)
        {
            Trace = new AdvisorTrace(traceDir);
            Updated += Trace.Write;
            Updated += u =>
            {
                if (u.Phase == AdvisorPhases.Ready && u.Recommendation is { Kind: DecisionContext.KindMulligan } rec)
                    lock (_lock) _mulliganAdvice = rec;
            };
        }
        Settings = settings;
        _coordinator = Create(settings, out var why);
        DisabledReason = why;
    }

    public AdvisorSettings Settings { get; private set; }
    /// <summary>Raison pour laquelle aucun conseil n'est produit (désactivé, clé absente), ou null.</summary>
    public string? DisabledReason { get; private set; }
    public StrategyCoordinator? Coordinator { get { lock (_lock) return _coordinator; } }

    public event Action<AdvisorUpdate>? Updated;

    /// <summary>Trace JSONL des conseils (un fichier par partie), ou null.</summary>
    public AdvisorTrace? Trace { get; }

    private StrategyCoordinator? Create(AdvisorSettings settings, out string? disabledReason)
    {
        disabledReason = null;
        if (!settings.Enabled)
        {
            disabledReason = "Conseiller désactivé dans les réglages.";
            return null;
        }
        IStrategyProvider provider;
        if (_providerFactory is not null) provider = _providerFactory(settings);
        else if (settings.IsSimulated)
            provider = new SimulatedLatencyProvider(TimeSpan.FromMilliseconds(Math.Max(200, settings.SimulatedLatencyMs)), TimeSpan.FromMilliseconds(Math.Max(0, settings.SimulatedLatencyMs) / 4.0));
        else
        {
            var model = settings.EffectiveModel;
            if (!StrategyProviders.KeyStatus(model).Present)
            {
                disabledReason = $"Clé {StrategyProviders.ProviderName(model)} absente : définis la variable d'environnement {StrategyProviders.KeyVariable(model)} puis relance l'application.";
                return null;
            }
            var notes = PromptBuilder.LoadStrategyNotes(settings.StrategyFile);
            provider = StrategyProviders.Create(settings, PromptBuilder.BuildInstructions(notes));
        }
        var coordinator = new StrategyCoordinator(provider, settings.ToStrategyOptions(), _metrics);
        coordinator.Updated += u => Updated?.Invoke(u);
        return coordinator;
    }

    public void OnSnapshot(GameStateSnapshot s)
    {
        StrategyCoordinator? c;
        AdvisorTraceEntry? mulligan = null;
        lock (_lock)
        {
            _latest = s;
            c = _coordinator;
            // Résultat du mulligan, une fois par partie : conseil, cartes gardées, remplacées et reçues.
            if (Trace is not null && _mulliganTracedFor != s.GameId && MulliganResult.From(s, _mulliganAdvice) is { } entry)
            {
                _mulliganTracedFor = s.GameId;
                mulligan = entry;
            }
        }
        if (mulligan is not null) Trace!.Write(mulligan);
        c?.OnSnapshot(s);
    }

    public bool RequestAnalysis() => Coordinator?.RequestAnalysis() ?? false;

    /// <summary>Horloge de l'affichage (PowerTaskList) : transmise au coordinateur.</summary>
    public void OnVisualProgress(HsCopilot.Core.Session.VisualProgress p) => Coordinator?.OnVisualProgress(p.Turn, p.Step, p.LogTime);

    public void SetPaused(bool paused) => Coordinator?.SetPaused(paused);

    /// <summary>Applique de nouveaux réglages : l'ancien coordinateur est arrêté (appel en cours annulé), un nouveau prend le relais.</summary>
    public async Task ReconfigureAsync(AdvisorSettings settings)
    {
        StrategyCoordinator? old;
        StrategyCoordinator? created;
        GameStateSnapshot? latest;
        lock (_lock)
        {
            old = _coordinator;
            Settings = settings;
            created = Create(settings, out var why);
            DisabledReason = why;
            _coordinator = created;
            latest = _latest;
        }
        if (old is not null) await old.DisposeAsync().ConfigureAwait(false);
        if (created is null)
            Updated?.Invoke(new AdvisorUpdate { Phase = AdvisorPhases.Disabled, Message = DisabledReason ?? "Conseiller inactif." });
        else if (latest is not null)
            created.OnSnapshot(latest);
    }

    public async ValueTask DisposeAsync()
    {
        StrategyCoordinator? c;
        lock (_lock)
        {
            c = _coordinator;
            _coordinator = null;
        }
        if (c is not null) await c.DisposeAsync().ConfigureAwait(false);
        Trace?.Dispose();
    }
}
