using System.Collections.Concurrent;

namespace HsCopilot.Core.Metrics;

/// <summary>Noms des mesures (affichés tels quels dans les rapports).</summary>
public static class MetricNames
{
    /// <summary>Horodatage écrit par Hearthstone dans le log → réception par l'application (direct uniquement).</summary>
    public const string LogToReceive = "log → réception";
    /// <summary>Début de ton tour côté serveur (GameState) → exécuté par le client (PowerTaskList), en ms.</summary>
    public const string TurnServerToScreen = "début de tour serveur → écran";
    /// <summary>Envoi par le plugin → réception par l'application (transport pipe).</summary>
    public const string PluginToReceive = "plugin → réception";
    /// <summary>Réception d'un lot de lignes → état publié (moteur + projection).</summary>
    public const string ReceiveToState = "réception → état publié";
    /// <summary>État publié → affiché dans la fenêtre.</summary>
    public const string StateToDisplay = "état publié → affichage";
    /// <summary>Horodatage du log → affichage (bout en bout, direct uniquement).</summary>
    public const string LogToDisplay = "log → affichage";
    /// <summary>Action envoyée par le joueur → nouvelles actions légales reçues.</summary>
    public const string ActionToFreshOptions = "action → nouvelles options";
    /// <summary>RESET_GAME (Rembobinage) → nouvelles actions légales reçues.</summary>
    public const string RewindToStable = "Rembobinage → état stable";
    /// <summary>Point de décision (options fraîches) → recommandation validée publiée.</summary>
    public const string DecisionToAdvice = "décision → conseil";
    /// <summary>Début du tour local → premier conseil validé.</summary>
    public const string TurnStartToAdvice = "début de tour → premier conseil";
    /// <summary>Durée d'un appel au fournisseur de stratégie (LLM).</summary>
    public const string StrategyCall = "appel stratégie (LLM)";
    /// <summary>Appel en flux : envoi → premier fragment de réponse.</summary>
    public const string StrategyFirstToken = "appel stratégie → premier fragment";
    /// <summary>Point de décision → première action affichable (début de réponse ou conseil complet).</summary>
    public const string DecisionToFirstShown = "décision → action affichée";
    /// <summary>Début du tour local → première action affichable du tour.</summary>
    public const string TurnStartToFirstShown = "début de tour → première action affichée";
}

public sealed class LatencyStats
{
    public required string Name { get; init; }
    public int Count { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double Max { get; init; }
    public double Mean { get; init; }

    public override string ToString() => Count == 0 ? $"{Name} : aucune mesure" : $"{Name} : P50 {P50:0} ms, P95 {P95:0} ms, max {Max:0} ms ({Count} mesures)";
}

/// <summary>Série de mesures en millisecondes (garde les 20 000 dernières).</summary>
public sealed class LatencySeries
{
    private const int Capacity = 20000;
    private readonly List<double> _values = new();
    private readonly object _lock = new();

    public void Add(double ms)
    {
        if (double.IsNaN(ms) || double.IsInfinity(ms)) return;
        lock (_lock)
        {
            if (_values.Count >= Capacity) _values.RemoveAt(0);
            _values.Add(ms);
        }
    }

    public LatencyStats Stats(string name)
    {
        double[] data;
        lock (_lock) data = _values.ToArray();
        if (data.Length == 0) return new LatencyStats { Name = name };
        Array.Sort(data);
        return new LatencyStats
        {
            Name = name,
            Count = data.Length,
            P50 = Percentile(data, 0.50),
            P95 = Percentile(data, 0.95),
            Max = data[^1],
            Mean = data.Average(),
        };
    }

    /// <summary>Percentile par rang le plus proche (méthode « nearest-rank »).</summary>
    public static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        var rank = (int)Math.Ceiling(p * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }
}

/// <summary>Registre de mesures et de compteurs, sûr entre fils.</summary>
public sealed class MetricsRegistry
{
    private readonly ConcurrentDictionary<string, LatencySeries> _series = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly ConcurrentDictionary<string, double> _sums = new();

    public void Record(string name, double milliseconds) => _series.GetOrAdd(name, _ => new LatencySeries()).Add(milliseconds);

    public void Increment(string name, long by = 1) => _counters.AddOrUpdate(name, by, (_, v) => v + by);

    public void AddAmount(string name, double amount) => _sums.AddOrUpdate(name, amount, (_, v) => v + amount);

    public long Counter(string name) => _counters.GetValueOrDefault(name);

    public double Amount(string name) => _sums.GetValueOrDefault(name);

    public LatencyStats Stats(string name) => _series.TryGetValue(name, out var s) ? s.Stats(name) : new LatencyStats { Name = name };

    public IReadOnlyList<LatencyStats> AllStats() => _series.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value.Stats(kv.Key)).ToList();

    public IReadOnlyDictionary<string, long> Counters => _counters.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);

    public IReadOnlyDictionary<string, double> Amounts => _sums.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Différence entre l'heure locale et une heure de log (sans date), corrigée autour de minuit.</summary>
    public static double MillisecondsSinceLogTime(TimeSpan logTimeOfDay, DateTime nowLocal)
    {
        var diff = nowLocal.TimeOfDay - logTimeOfDay;
        if (diff < TimeSpan.FromHours(-12)) diff += TimeSpan.FromDays(1);
        if (diff > TimeSpan.FromHours(12)) diff -= TimeSpan.FromDays(1);
        return diff.TotalMilliseconds;
    }
}
