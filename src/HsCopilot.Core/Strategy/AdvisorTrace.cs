using HsCopilot.Core.State;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Strategy;

/// <summary>Une ligne de la trace du conseiller : ce que l'overlay a montré, quand, et pour quel état.</summary>
public sealed class AdvisorTraceEntry
{
    public DateTime Utc { get; init; }
    public required string Phase { get; init; }
    public string? Message { get; init; }
    public string? GameId { get; init; }
    public int Revision { get; init; }
    public int TurnNumber { get; init; }
    public string? ActivePlayer { get; init; }
    public string? Kind { get; init; }
    public string? Action { get; init; }
    public string? Summary { get; init; }
    public string? Why { get; init; }
    public string? Plan { get; init; }
    public List<string>? FollowUp { get; init; }
    public string? Lethal { get; init; }
    public string? Caution { get; init; }
    public List<string>? Warnings { get; init; }
    public List<string>? Facts { get; init; }
    public double? LatencyMs { get; init; }
    public double? CostUsd { get; init; }
    public int CallsThisTurn { get; init; }
    public double CostThisGameUsd { get; init; }
    /// <summary>modèle, solveur ou plan (conseil sans appel).</summary>
    public string? Origin { get; init; }
    /// <summary>Raison d'un état sans conseil (quota, budget, fin-de-tour…).</summary>
    public string? Reason { get; init; }
    public int? AnalyzedRevision { get; init; }
    public string? StateLogTime { get; init; }
    public List<string>? DrawnThisTurn { get; init; }
    public int? HandCount { get; init; }
    public DateTime? SentUtc { get; init; }
    public double? AgeMs { get; init; }
    /// <summary>Conseil calculé sur ton tour mais Hearthstone affichait encore le tour adverse (horloge PowerTaskList).</summary>
    public bool? AwaitingDisplay { get; init; }
    /// <summary>Faux si l'horloge de l'affichage n'était pas suivie.</summary>
    public bool? DisplayTracked { get; init; }
    /// <summary>Choix : propositions telles qu'envoyées au modèle (carte, Sombre don et résultat).</summary>
    public List<string>? Offered { get; init; }
    /// <summary>Résultat du mulligan (phase « résultat-mulligan ») : cartes que le conseil remplaçait.</summary>
    public List<string>? MulliganAdvisedReplace { get; init; }
    public List<string>? MulliganKept { get; init; }
    public List<string>? MulliganReplaced { get; init; }
    /// <summary>Cartes arrivées en main à la place des cartes remplacées.</summary>
    public List<string>? MulliganReceived { get; init; }
    /// <summary>Vrai si les cartes remplacées sont exactement celles du conseil ; null sans conseil de mulligan.</summary>
    public bool? MulliganFollowed { get; init; }
}

/// <summary>Résultat d'un mulligan lu dans l'état : cartes gardées, remplacées et reçues.</summary>
public static class MulliganResult
{
    public const string Phase = "résultat-mulligan";

    /// <summary>
    /// Entrée de trace pour le mulligan du joueur local, dès qu'il apparaît dans l'historique ; null avant. Les cartes
    /// gardées et remplacées viennent de l'historique (choix envoyé au serveur, vérifié par les changements de zone) ; les
    /// cartes reçues sont celles de la main qui ne sont ni gardées ni La pièce.
    /// </summary>
    public static AdvisorTraceEntry? From(GameStateSnapshot s, Recommendation? advised)
    {
        var item = s.History.LastOrDefault(h => h.Kind == "mulligan" && h.Player == "local");
        if (item is null) return null;
        var kept = item.Related.Where(r => r.Note == "gardée").ToList();
        var replaced = item.Related.Where(r => r.Note == "remplacée").ToList();
        var keptIds = kept.Select(k => k.EntityId).ToHashSet();
        // Attendre que le serveur ait appliqué l'échange : cartes remplacées sorties de la main, cartes gardées encore là.
        var hand = s.Local?.Hand.Cards.Select(c => c.EntityId).ToHashSet() ?? new HashSet<int>();
        if (kept.Count + replaced.Count == 0 || replaced.Any(r => hand.Contains(r.EntityId)) || kept.Any(k => !hand.Contains(k.EntityId))) return null;
        var received = s.Local?.Hand.Cards.Where(c => !keptIds.Contains(c.EntityId) && !HsCopilot.Core.Cards.CardRules.IsCoin(c.CardId)).Select(c => c.Name ?? "?").ToList() ?? new();
        bool? followed = null;
        List<string>? advisedNames = null;
        if (advised is { Kind: DecisionContext.KindMulligan } rec && rec.GameId == s.GameId)
        {
            advisedNames = rec.ReplaceActions.Select(a => a.SourceName ?? "?").ToList();
            followed = rec.ReplaceActions.Select(a => a.SourceEntityId ?? -1).ToHashSet().SetEquals(replaced.Select(r => r.EntityId));
        }
        return new AdvisorTraceEntry
        {
            Utc = DateTime.UtcNow,
            Phase = Phase,
            GameId = s.GameId,
            Revision = s.Revision,
            TurnNumber = s.Game.TurnNumber,
            Kind = DecisionContext.KindMulligan,
            Message = $"gardées : {string.Join(", ", kept.Select(k => k.Name))} ; remplacées : {string.Join(", ", replaced.Select(r => r.Name))} ; reçues : {string.Join(", ", received)}"
                      + (followed is null ? "" : followed.Value ? " ; conseil suivi" : " ; différent du conseil"),
            MulliganAdvisedReplace = advisedNames,
            MulliganKept = kept.Select(k => k.Name ?? "?").ToList(),
            MulliganReplaced = replaced.Select(r => r.Name ?? "?").ToList(),
            MulliganReceived = received,
            MulliganFollowed = followed,
        };
    }
}

/// <summary>
/// Trace du conseiller, un fichier JSONL par partie (%LocalAppData%\HsCopilot\advisor). Elle permet de retrouver après
/// coup les conseils affichés, retirés ou non lancés. Elle ne contient ni clé, ni BattleTag, ni état complet.
/// </summary>
public sealed class AdvisorTrace : IDisposable
{
    private readonly string _dir;
    private readonly object _lock = new();
    private StreamWriter? _writer;
    private string? _gameId;
    private (int Revision, string? Action)? _lastPartial;

    public AdvisorTrace(string dir) => _dir = dir;

    public string? CurrentPath { get; private set; }

    public static AdvisorTraceEntry ToEntry(AdvisorUpdate u)
    {
        var rec = u.Phase == AdvisorPhases.Ready ? u.Recommendation : null;
        return new AdvisorTraceEntry
        {
            Utc = DateTime.UtcNow,
            Phase = u.Phase,
            Message = u.Message,
            GameId = u.GameId,
            Revision = u.Revision,
            TurnNumber = u.State?.Game.TurnNumber ?? 0,
            ActivePlayer = u.State?.Game.ActivePlayer,
            Kind = u.DecisionKind,
            Action = rec is not null ? AdviceDisplay.Label(rec.Action, u.State) : u.PartialAction is { } pa ? AdviceDisplay.Label(pa, u.State) : null,
            Summary = rec?.Summary ?? u.Partial?.Summary,
            Why = rec?.Why ?? u.Partial?.Why,
            Plan = rec?.PlanNextTurn,
            FollowUp = rec?.FollowUp.Select(f => f.Description).ToList(),
            Lethal = rec?.Lethal,
            Caution = rec?.Caution,
            Warnings = rec?.Warnings.Count > 0 ? rec.Warnings : null,
            Facts = u.Facts.Count > 0 ? u.Facts.Select(f => f.Text).ToList() : null,
            LatencyMs = rec?.LatencyMs,
            CostUsd = rec?.CostUsd,
            CallsThisTurn = u.CallsThisTurn,
            CostThisGameUsd = u.CostThisGameUsd,
            Origin = rec?.Origin,
            Reason = u.Reason,
            AnalyzedRevision = u.Diagnostics?.AnalyzedRevision,
            StateLogTime = u.Diagnostics?.StateLogTime,
            DrawnThisTurn = u.Diagnostics?.DrawnThisTurn is { Count: > 0 } d ? d : null,
            HandCount = u.Diagnostics?.HandCount,
            SentUtc = u.Diagnostics?.SentUtc,
            AgeMs = u.Diagnostics?.AgeMs,
            AwaitingDisplay = u.AwaitingDisplay ? true : null,
            DisplayTracked = u.DisplayTracked,
            Offered = u.Phase == AdvisorPhases.Analyzing && u.State?.PendingChoice is { } pc && u.DecisionKind is DecisionContext.KindChoice or DecisionContext.KindMulligan
                ? pc.Offered.Select(o => o.Name + (o.Type == "MINION" ? $" {o.Attack}/{o.Health}" : "") + (o.Cost is int c ? $" ({c})" : "") + (o.DarkGift is null ? "" : $" + {DarkGifts.Summary(o)}")).ToList()
                : null,
        };
    }

    public void Write(AdvisorUpdate u)
    {
        if (u.GameId is null or "aucune") return; // pas de partie en cours : rien à tracer
        lock (_lock)
        {
            if (u.Phase == AdvisorPhases.Partial)
            {
                // Une ligne par réponse partielle (pas une par fragment).
                var key = (u.Revision, u.PartialAction?.Id);
                if (_lastPartial == key) return;
                _lastPartial = key;
            }
            WriteLocked(ToEntry(u));
        }
    }

    /// <summary>Écrit une entrée déjà construite (résultat du mulligan…).</summary>
    public void Write(AdvisorTraceEntry entry)
    {
        if (entry.GameId is null or "aucune") return;
        lock (_lock) WriteLocked(entry);
    }

    private void WriteLocked(AdvisorTraceEntry entry)
    {
        try
        {
            if (_gameId != entry.GameId || _writer is null)
            {
                _writer?.Dispose();
                Directory.CreateDirectory(_dir);
                _gameId = entry.GameId;
                CurrentPath = Path.Combine(_dir, $"{DateTime.Now:yyyyMMdd_HHmmss}_{entry.GameId}.jsonl");
                _writer = new StreamWriter(new FileStream(CurrentPath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
            }
            _writer.WriteLine(JsonSerializer.Serialize(entry, Json.Compact));
            _writer.Flush();
        }
        catch (IOException) { /* trace facultative : jamais bloquante */ }
        catch (UnauthorizedAccessException) { }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
