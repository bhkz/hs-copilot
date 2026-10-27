using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

public sealed class AdviceEvent
{
    public required DateTime AtUtc { get; init; }
    /// <summary>published, invalidated, rejected, discarded, timeout, error, skipped.</summary>
    public required string Kind { get; init; }
    public required string Detail { get; init; }
    public int Revision { get; init; }
}

/// <summary>
/// Tableau des conseils : au plus une recommandation courante, invalidée dès que l'état ne correspond plus
/// (action jouée, Rembobinage, fin de tour, nouvelle partie, toute autre modification).
/// </summary>
public sealed class AdviceBoard
{
    private readonly object _lock = new();
    private readonly List<AdviceEvent> _events = new();
    private readonly Dictionary<string, int> _invalidations = new();
    private Recommendation? _current;

    /// <summary>Changement de la recommandation courante (null = plus de conseil affichable) et raison.</summary>
    public event Action<Recommendation?, string?>? Changed;

    public Recommendation? Current { get { lock (_lock) return _current; } }
    public int Published { get; private set; }
    public IReadOnlyDictionary<string, int> Invalidations { get { lock (_lock) return new Dictionary<string, int>(_invalidations); } }
    public IReadOnlyList<AdviceEvent> Events { get { lock (_lock) return _events.ToList(); } }

    public void Publish(Recommendation rec)
    {
        lock (_lock)
        {
            _current = rec;
            Published++;
            Add("published", $"{rec.Action.Label} — {rec.Summary}", rec.Revision);
        }
        Changed?.Invoke(rec, null);
    }

    public void Record(string kind, string detail, int revision)
    {
        lock (_lock) Add(kind, detail, revision);
    }

    /// <summary>Invalide la recommandation courante si elle ne correspond plus à l'état fourni.</summary>
    public void OnSnapshot(GameStateSnapshot snapshot)
    {
        string? reason;
        lock (_lock)
        {
            if (_current is null) return;
            var (valid, why) = RecommendationValidator.CheckStillValid(_current, snapshot);
            if (valid) return;
            reason = why ?? InvalidationReasons.StateChanged;
            _invalidations[reason] = _invalidations.GetValueOrDefault(reason) + 1;
            Add("invalidated", $"{_current.Action.Label} : {reason}", snapshot.Revision);
            _current = null;
        }
        Changed?.Invoke(null, reason);
    }

    private void Add(string kind, string detail, int revision)
    {
        _events.Add(new AdviceEvent { AtUtc = DateTime.UtcNow, Kind = kind, Detail = detail, Revision = revision });
        if (_events.Count > 500) _events.RemoveAt(0);
    }
}

/// <summary>Garde-fou : aucun message sortant ne doit contenir un identifiant de carte cachée connu du moteur.</summary>
public static class PayloadLeakGuard
{
    /// <summary>Identifiants interdits présents dans le message (comparaison sur des identifiants entiers, pas des préfixes).</summary>
    public static IReadOnlyList<string> FindLeaks(string payload, IReadOnlySet<string> forbiddenCardIds)
    {
        var leaks = new List<string>();
        foreach (var id in forbiddenCardIds)
        {
            var start = 0;
            while ((start = payload.IndexOf(id, start, StringComparison.Ordinal)) >= 0)
            {
                var before = start == 0 ? ' ' : payload[start - 1];
                var afterIndex = start + id.Length;
                var after = afterIndex >= payload.Length ? ' ' : payload[afterIndex];
                if (!IsIdChar(before) && !IsIdChar(after))
                {
                    leaks.Add(id);
                    break;
                }
                start = afterIndex;
            }
        }
        return leaks;
    }

    private static bool IsIdChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
