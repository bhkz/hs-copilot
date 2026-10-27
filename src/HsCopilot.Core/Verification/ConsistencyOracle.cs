using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;

namespace HsCopilot.Core.Verification;

/// <summary>Résultat de la comparaison entre l'état reconstruit et la vue que le client imprime lui-même.</summary>
public sealed class OracleReport
{
    public int OptionBlocks { get; internal set; }
    public int Checks { get; internal set; }
    /// <summary>La vue imprimée est identique à l'état courant du moteur.</summary>
    public int ExactMatches { get; internal set; }
    /// <summary>La vue imprimée est identique à un état récent du moteur (client en retard d'animation).</summary>
    public int LaggedMatches { get; internal set; }
    /// <summary>La vue imprimée ne correspond à aucun état récent : écart réel.</summary>
    public int Mismatches { get; internal set; }
    public int MissingEntities { get; internal set; }
    /// <summary>Plus grand retard observé, en révisions du moteur.</summary>
    public int MaxLagRevisions { get; internal set; }
    /// <summary>Répartition des retards acceptés (révisions → nombre de vues).</summary>
    public SortedDictionary<int, int> LagHistogram { get; } = new();
    public List<string> Examples { get; } = new();

    /// <summary>Cartes de la main locale proposées par le serveur : coût affiché comparé au mana disponible reconstruit.</summary>
    public int ManaChecks { get; internal set; }
    /// <summary>Blocs d'options (envoyés uniquement au joueur dont c'est le tour) : le tour doit être attribué au joueur local.</summary>
    public int TurnOwnerChecks { get; internal set; }
    public int TurnOwnerMismatches { get; internal set; }
    public int ManaMismatches { get; internal set; }
    public List<string> ManaExamples { get; } = new();

    public double AgreementRate => Checks == 0 ? 0 : (double)(ExactMatches + LaggedMatches) / Checks;
}

/// <summary>
/// Oracle indépendant du parseur d'état. À chaque bloc d'options légales et à chaque choix envoyé, le client
/// imprime pour chaque entité concernée sa zone, sa position, son identifiant de carte et son propriétaire.
/// On vérifie que cette vue est exactement un état par lequel le moteur est passé.
/// </summary>
/// <remarks>
/// Le client imprime sa vue « animée » : elle peut être en retard de quelques paquets sur les données serveur
/// (constaté : une carte que le serveur vient de faire piocher est encore imprimée en DECK). Une vue est acceptée
/// si elle correspond à l'état courant, ou à un état resté valide jusqu'à au plus <see cref="ClientViewOracle.LagWindow"/>
/// révisions avant la révision courante, en comparant le quadruplet complet (zone, position, carte, propriétaire).
/// Limite : les deux côtés proviennent du même flux serveur (données reçues contre vue animée du client) ;
/// ce n'est pas une comparaison avec l'écran.
/// </remarks>
public static class ConsistencyOracle
{
    public static OracleReport Run(GameEngine engine, IEnumerable<string> rawLines)
    {
        using var oracle = new ClientViewOracle(engine);
        foreach (var raw in rawLines)
        {
            var line = PowerLogLineParser.TryParse(raw);
            if (line is null) continue;
            engine.Process(line);
            oracle.AfterLine(line);
        }
        return oracle.Report;
    }
}

/// <summary>Version incrémentale de l'oracle : à appeler après chaque ligne traitée par le moteur (utilisable en direct).</summary>
public sealed class ClientViewOracle : IDisposable
{
    /// <summary>Fenêtre de retard tolérée. Retard maximal observé sur 28 parties réelles : 13 révisions.</summary>
    public const int LagWindow = 20;

    private readonly record struct View(int Revision, string Zone, int Position, string CardId, int Controller);

    private readonly GameEngine _engine;
    private readonly Dictionary<int, List<View>> _timeline = new();
    private readonly HashSet<int> _touched = new();
    private int _gameNumber;

    public ClientViewOracle(GameEngine engine)
    {
        _engine = engine;
        _gameNumber = engine.GameNumber;
        _engine.EntityTouched += OnTouched;
    }

    public OracleReport Report { get; private set; } = new();

    private void OnTouched(EntityState e) => _touched.Add(e.Id);

    public void AfterLine(PowerLogLine line)
    {
        if (_engine.GameNumber != _gameNumber)
        {
            _timeline.Clear();
            _gameNumber = _engine.GameNumber;
            Report = new OracleReport();
        }
        foreach (var id in _touched)
        {
            if (!_engine.Entities.TryGetValue(id, out var e)) continue;
            var v = ViewOf(e, _engine.Revision);
            if (!_timeline.TryGetValue(id, out var list)) _timeline[id] = list = new List<View>();
            if (list.Count == 0 || !SameState(list[^1], v)) list.Add(v);
        }
        _touched.Clear();

        if (line.Method == "DebugPrintOptions")
        {
            var b = line.Body;
            if (b.StartsWith("id=", StringComparison.Ordinal))
            {
                Report.OptionBlocks++;
                // Le serveur n'envoie des options qu'au joueur dont c'est le tour : le moteur doit attribuer ce tour au joueur local.
                if (_engine.LocalPlayerId is int lp && _engine.MulliganStarted && _engine.Step is not ("BEGIN_MULLIGAN" or null))
                {
                    Report.TurnOwnerChecks++;
                    if (_engine.CurrentPlayerId != lp)
                    {
                        Report.TurnOwnerMismatches++;
                        Example(Report, $"options #{b} reçues alors que le moteur attribue le tour à P{_engine.CurrentPlayerId} — {line.Time}");
                    }
                }
                return;
            }
            var isOption = b.StartsWith("option ", StringComparison.Ordinal);
            var refText = ExtractRef(b, isOption ? "mainEntity=" : "entity=");
            if (refText is null) return;
            var r = EntityRef.Parse(refText);
            Compare(r, line);
            if (isOption) CheckMana(r, b, line);
        }
        else if (line.Method == "SendChoices" && line.Body.StartsWith("m_chosenEntities[", StringComparison.Ordinal))
        {
            var eq = line.Body.IndexOf("]=", StringComparison.Ordinal);
            if (eq > 0) Compare(EntityRef.Parse(line.Body[(eq + 2)..]), line);
        }
    }

    private static View ViewOf(EntityState e, int revision)
    {
        var positional = e.Zone is "HAND" or "PLAY" && e.CardType is not ("HERO" or "HERO_POWER" or "WEAPON" or "ENCHANTMENT" or "PLAYER" or "GAME");
        return new View(revision, e.Zone, positional ? e.ZonePosition : 0, e.CardId, e.Controller);
    }

    private static bool SameState(View a, View b) =>
        a.Zone == b.Zone && a.Position == b.Position && a.CardId == b.CardId && a.Controller == b.Controller;

    private static string? ExtractRef(string body, string key)
    {
        var p = body.IndexOf(key, StringComparison.Ordinal);
        if (p < 0) return null;
        var start = p + key.Length;
        var end = body.IndexOf(" error=", start, StringComparison.Ordinal);
        var text = end < 0 ? body[start..] : body[start..end];
        return text.StartsWith('[') ? text : null;
    }

    private void Compare(EntityRef r, PowerLogLine line)
    {
        if (r.Id is not int id || r.PrintedZone is null) return;
        var report = Report;
        report.Checks++;
        if (!_engine.Entities.TryGetValue(id, out var e))
        {
            report.MissingEntities++;
            report.Mismatches++;
            Example(report, $"entité #{id} absente du moteur — {line.Raw}");
            return;
        }
        var current = ViewOf(e, _engine.Revision);
        var printed = new View(0, r.PrintedZone, r.PrintedZonePos ?? 0, r.PrintedCardId ?? "", r.PrintedPlayer ?? 0);
        if (Matches(current, printed, e))
        {
            report.ExactMatches++;
            Count(report, 0);
            return;
        }
        if (_timeline.TryGetValue(id, out var list))
        {
            // Un état passé est valide de sa révision jusqu'à la révision précédant l'état suivant.
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var past = list[i];
                var validUntil = i + 1 < list.Count ? list[i + 1].Revision - 1 : _engine.Revision;
                var lag = _engine.Revision - validUntil;
                if (lag > LagWindow) break;
                if (Matches(past, printed, e))
                {
                    report.LaggedMatches++;
                    report.MaxLagRevisions = Math.Max(report.MaxLagRevisions, lag);
                    Count(report, lag);
                    return;
                }
            }
        }
        report.Mismatches++;
        Example(report, $"#{id} client=({printed.Zone},{printed.Position},'{printed.CardId}',P{printed.Controller}) moteur=({current.Zone},{current.Position},'{current.CardId}',P{current.Controller}) rév. {_engine.Revision} — {line.Time}");
    }

    private static void Count(OracleReport report, int lag) =>
        report.LagHistogram[lag] = report.LagHistogram.GetValueOrDefault(lag) + 1;

    private static bool Matches(View engineView, View printed, EntityState e)
    {
        if (engineView.Zone != printed.Zone || engineView.CardId != printed.CardId || engineView.Controller != printed.Controller) return false;
        if (printed.Zone is "HAND" or "PLAY" && e.CardType is not ("HERO" or "HERO_POWER" or "WEAPON" or "ENCHANTMENT"))
            return engineView.Position == printed.Position;
        return true;
    }

    /// <summary>
    /// Le serveur refuse une carte de la main avec REQ_ENOUGH_MANA si son coût dépasse le mana disponible, et l'accepte (NONE)
    /// sinon. Vérifie que le coût (tag COST) et le mana reconstruits donnent la même conclusion.
    /// </summary>
    private void CheckMana(EntityRef r, string body, PowerLogLine line)
    {
        if (r.Id is not int id || !_engine.Entities.TryGetValue(id, out var e)) return;
        if (_engine.LocalPlayerId is not int lp || e.Controller != lp || e.Zone != "HAND") return;
        if (_engine.CurrentPlayerId != lp) return;
        var error = PowerStatements.ReadField(body, "error");
        if (error is not ("NONE" or "REQ_ENOUGH_MANA")) return;
        var pe = _engine.PlayerEntity(lp);
        if (pe is null) return;
        // Cartes payées en PV ou à coût alternatif : hors du périmètre de ce contrôle.
        if (e.Flag("CARD_COSTS_HEALTH") || e.Flag("CARD_COSTS_ARMOR") || e.Flag("CARD_COSTS_CORPSES")) return;
        var available = pe.GetInt("RESOURCES") + pe.GetInt("TEMP_RESOURCES") - pe.GetInt("RESOURCES_USED");
        var cost = e.GetInt("COST");
        var consistent = error == "NONE" ? cost <= available : cost > available;
        Report.ManaChecks++;
        if (consistent) return;
        Report.ManaMismatches++;
        if (Report.ManaExamples.Count < 15)
            Report.ManaExamples.Add($"#{id} {e.CardId} coût {cost}, mana dispo {available}, serveur {error} — {line.Time}");
    }

    private static void Example(OracleReport report, string text)
    {
        if (report.Examples.Count < 25) report.Examples.Add(text);
    }

    public void Dispose() => _engine.EntityTouched -= OnTouched;
}
