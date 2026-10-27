using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Verification;

public sealed class VisibilityReport
{
    public int SnapshotsChecked { get; internal set; }
    public int OpponentHiddenSlotsChecked { get; internal set; }
    public int Violations { get; internal set; }
    public List<string> Examples { get; } = new();
}

/// <summary>
/// Contrôle « aucune information cachée » : à chaque frontière de paquet, on projette l'état et on vérifie que
/// chaque identité de carte adverse exposée en main, deck ou Secret a été affichée publiquement auparavant.
/// </summary>
/// <remarks>
/// La preuve de publicité est établie indépendamment du moteur : à partir des références imprimées par le client
/// (entité adverse imprimée en PLAY, GRAVEYARD ou REMOVEDFROMGAME avec son identifiant) et des révélations explicites
/// (SHOW_ENTITY suivi du tag REVEALED=1 ou d'une zone publique).
/// </remarks>
public static class VisibilityAudit
{
    public static VisibilityReport Run(IEnumerable<string> rawLines, ICardDatabase cards, EngineOptions? options = null)
    {
        var engine = new GameEngine(options ?? new EngineOptions { Cards = cards });
        var auditor = new VisibilityAuditor(engine, cards);
        foreach (var raw in rawLines)
        {
            var line = PowerLogLineParser.TryParse(raw);
            if (line is null) continue;
            auditor.BeforeLine(line);
            engine.Process(line);
        }
        auditor.CheckNow();
        return auditor.Report;
    }
}

/// <summary>Version incrémentale de l'audit : <see cref="BeforeLine"/> doit être appelé avant que le moteur traite la ligne.</summary>
public sealed class VisibilityAuditor
{
    private readonly GameEngine _engine;
    private readonly GameStateProjector _projector;
    private readonly Dictionary<int, HashSet<string>> _publiclySeen = new();
    private int? _showEntity;
    private string? _showCard;
    /// <summary>
    /// Entités révélées en ce moment (REVEALED=1) : une transformation pendant la révélation est vue elle aussi. Partie classée
    /// du 25/10/2026 à 21 h 31 : Galakrond adverse montré dans son deck pendant une Invocation, amélioré en « l'Apocalypse »,
    /// puis caché (HIDE_ENTITY, REVEALED=0).
    /// </summary>
    private readonly HashSet<int> _revealed = new();
    private int _gameNumber;

    public VisibilityAuditor(GameEngine engine, ICardDatabase cards)
    {
        _engine = engine;
        _projector = new GameStateProjector(cards);
        _gameNumber = engine.GameNumber;
    }

    public VisibilityReport Report { get; private set; } = new();

    public void BeforeLine(PowerLogLine line)
    {
        if (ReplayFileIsCreateGame(line))
        {
            _publiclySeen.Clear();
            _revealed.Clear();
        }
        if (_engine.GameNumber != _gameNumber)
        {
            _gameNumber = _engine.GameNumber;
            Report = new VisibilityReport();
        }

        // Preuves de publicité indépendantes du moteur.
        foreach (var r in PrintedRefs(line.Body))
        {
            if (r.Id is int id && r.PrintedZone is "PLAY" or "GRAVEYARD" or "REMOVEDFROMGAME" && !string.IsNullOrEmpty(r.PrintedCardId))
                Seen(id, r.PrintedCardId!);
        }
        if (line.Method == "DebugPrintPower")
        {
            if (PowerStatements.TryUpdatingHeader(line.Body, "SHOW_ENTITY - Updating Entity=", out var se) && se.Entity.Id is int sid)
            {
                _showEntity = sid;
                _showCard = se.CardId;
            }
            else if (PowerStatements.TryTagLine(line.Body, out var tl))
            {
                if (_showEntity is int s && !string.IsNullOrEmpty(_showCard) &&
                    ((tl.Tag == "REVEALED" && tl.Value == "1") || (tl.Tag == "ZONE" && tl.Value is "PLAY" or "GRAVEYARD" or "REMOVEDFROMGAME")))
                    Seen(s, _showCard!);
                if (_showEntity is int r && tl.Tag == "REVEALED") { if (tl.Value == "1") _revealed.Add(r); else _revealed.Remove(r); }
            }
            else
            {
                _showEntity = null;
                _showCard = null;
                if (PowerStatements.TryUpdatingHeader(line.Body, "CHANGE_ENTITY - Updating Entity=", out var ce) && ce.Entity.Id is int cid
                    && _revealed.Contains(cid) && !string.IsNullOrEmpty(ce.CardId))
                    Seen(cid, ce.CardId);
                else if (PowerStatements.TryTagChange(line.Body, out var tc) && tc.Entity.Id is int tid && tc.Tag == "REVEALED")
                {
                    if (tc.Value == "1") _revealed.Add(tid); else _revealed.Remove(tid);
                }
                else if (PowerStatements.TryHideEntity(line.Body, out var he) && he.Entity.Id is int hid)
                    _revealed.Remove(hid);
            }
        }

        var boundary = line.Method is "DebugPrintPowerList" || (line.Method == "DebugPrintOptions" && line.Body.StartsWith("id=", StringComparison.Ordinal));
        if (boundary && _engine.HasGame) CheckNow();
    }

    private static bool ReplayFileIsCreateGame(PowerLogLine line) => line.Method == "DebugPrintPower" && line.Body == "CREATE_GAME";

    private void Seen(int id, string card)
    {
        if (!_publiclySeen.TryGetValue(id, out var set)) _publiclySeen[id] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(card);
    }

    /// <summary>Vérifie l'état projeté courant.</summary>
    public void CheckNow()
    {
        if (!_engine.HasGame) return;
        CheckSnapshot(_projector.Project(_engine));
    }

    /// <summary>Vérifie un état déjà projeté (évite une seconde projection en direct).</summary>
    public void CheckSnapshot(GameStateSnapshot snap)
    {
        var report = Report;
        report.SnapshotsChecked++;
        var opp = snap.Opponent;
        if (opp is null) return;

        void Verify(int entityId, string? cardId, string where)
        {
            report.OpponentHiddenSlotsChecked++;
            if (cardId is null) return;
            if (_publiclySeen.TryGetValue(entityId, out var set) && set.Contains(cardId)) return;
            report.Violations++;
            if (report.Examples.Count < 20)
                report.Examples.Add($"rév. {snap.Revision} : {where} #{entityId} = {cardId} sans preuve de publicité");
        }

        foreach (var c in opp.Hand.Cards) Verify(c.EntityId, c.CardId, "main adverse");
        foreach (var c in opp.Deck.KnownAdded) Verify(c.EntityId, c.CardId, "deck adverse");
        foreach (var s in opp.Secrets) Verify(s.EntityId, s.CardId, "secret adverse");
        if (snap.PendingChoice is not null)
            foreach (var c in snap.PendingChoice.Offered)
                if (_engine.Entities.TryGetValue(c.EntityId, out var e) && e.Controller != _engine.LocalPlayerId) Verify(c.EntityId, c.CardId, "choix");
    }

    private static IEnumerable<EntityRef> PrintedRefs(string body)
    {
        var i = 0;
        while (true)
        {
            var start = body.IndexOf("[entityName=", i, StringComparison.Ordinal);
            if (start < 0) yield break;
            var end = body.IndexOf(" player=", start, StringComparison.Ordinal);
            if (end < 0) yield break;
            var close = body.IndexOf(']', end);
            if (close < 0) yield break;
            yield return EntityRef.Parse(body[start..(close + 1)]);
            i = close + 1;
        }
    }
}
