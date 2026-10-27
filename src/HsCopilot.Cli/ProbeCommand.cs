using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Transport;

namespace HsCopilot.Cli;

/// <summary>
/// Outil d'exploration : relève dans des logs réels les faits nécessaires aux tests de mécanique du deck
/// (recharge des lieux, Frappe anticipée, Voyante blessée, cibles du Wyrm, Grommash + Profondeurs).
/// </summary>
public static class ProbeCommand
{
    private const string Depths = "CORE_REV_990";
    private const string Strike = "TIME_750";
    private const string Seer = "END_022";
    private const string Wyrm = "TLC_600";
    private const string Grommash = "CORE_EX1_414";

    public static int Run(Args a)
    {
        var (cfg, cards) = Commands.LoadEnvironment(a, quiet: true);
        foreach (var file in a.Positional)
        {
            var lines = ReplayFile.ReadAllLines(file);
            foreach (var game in ReplayFile.SplitGames(lines))
                ProbeGame(Path.GetFileName(file), game, cards, cfg.LocalPlayerName);
        }
        return 0;
    }

    private sealed class PendingPlay
    {
        public required HistoryEntry Entry { get; init; }
        public required string CardId { get; init; }
        public required Dictionary<int, (string Card, int Cost, int BaseCost)> HandBefore { get; init; }
        public int? SeerSpellPower { get; init; }
        public int? TargetHealthBefore { get; init; }
    }

    private static void ProbeGame(string file, LoggedGame game, ICardDatabase cards, string? localName)
    {
        var engine = new GameEngine(new EngineOptions { Cards = cards, LocalPlayerName = localName });
        var seenHistory = 0;
        PendingPlay? pending = null;
        var locationUses = new List<(int Entity, int GameTurn)>();
        var reportedWyrmOptions = new HashSet<int>();
        var tag = $"{file}#{game.Index}";

        foreach (var raw in game.Lines)
        {
            var line = PowerLogLineParser.TryParse(raw);
            if (line is null) continue;
            engine.Process(line);
            var local = engine.LocalPlayerId;

            // Nouvelles entrées d'historique.
            while (seenHistory < engine.History.Count)
            {
                var h = engine.History[seenHistory++];
                if (h.Rewound || local is null || h.PlayerId != local) continue;
                var src = h.SourceId is int s ? engine.Entities.GetValueOrDefault(s) : null;
                var cid = src?.CardId ?? "";
                if (h.Kind == HistoryKind.LocationActivation && cid == Depths)
                {
                    locationUses.Add((src!.Id, h.GameTurn));
                    var target = h.TargetId is int t ? engine.Entities.GetValueOrDefault(t) : null;
                    Console.WriteLine($"{tag} LIEU activé #{src.Id} TURN={h.GameTurn} cible={target?.CardId} (ATQ {target?.GetInt("ATK")}, dégâts {target?.GetInt("DAMAGE")})");
                }
                if (h.Kind == HistoryKind.Play && cid is Strike or "FIR_939")
                {
                    var hand = engine.Entities.Values
                        .Where(e => !e.Orphaned && e.Controller == local && e.Zone == "HAND" && e.Id != src!.Id && e.CardType == "MINION")
                        .ToDictionary(e => e.Id, e => (e.CardId, e.GetInt("COST"), cards.Get(e.CardId)?.Cost ?? -1));
                    var seer = engine.Entities.Values.FirstOrDefault(e => !e.Orphaned && e.Controller == local && e.Zone == "PLAY" && e.CardId == Seer);
                    var target = h.TargetId is int t ? engine.Entities.GetValueOrDefault(t) : null;
                    pending = new PendingPlay
                    {
                        Entry = h,
                        CardId = cid,
                        HandBefore = hand,
                        SeerSpellPower = seer is null ? null : seer.GetInt("SPELLPOWER") * 100 + seer.GetInt("DAMAGE"),
                        TargetHealthBefore = target is null ? null : target.GetInt("HEALTH") - target.GetInt("DAMAGE") + target.GetInt("ARMOR"),
                    };
                }
            }

            // Fin du bloc de la carte suivie.
            if (pending is not null && engine.BlockDepth == 0 && line.Method == "DebugPrintPower")
            {
                var h = pending.Entry;
                var handAfter = engine.Entities.Values.Where(e => !e.Orphaned && e.Controller == local && e.Zone == "HAND").Select(e => e.Id).ToHashSet();
                var drawnMinions = handAfter.Except(pending.HandBefore.Keys)
                    .Select(id => engine.Entities[id]).Where(e => e.CardType == "MINION" && e.InitialZone == "DECK").Select(e => e.CardId).ToList();
                var bigByCurrent = pending.HandBefore.Values.Where(v => v.Cost >= 5).Select(v => $"{v.Card}({v.Cost})").ToList();
                var bigByBase = pending.HandBefore.Values.Where(v => v.BaseCost >= 5).Select(v => $"{v.Card}(base {v.BaseCost}, actuel {v.Cost})").ToList();
                var target = h.TargetId is int t ? engine.Entities.GetValueOrDefault(t) : null;
                var targetHealthAfter = target is null ? (int?)null : target.GetInt("HEALTH") - target.GetInt("DAMAGE") + target.GetInt("ARMOR");
                var seerInfo = pending.SeerSpellPower is int sp ? $" Voyante en jeu (SPELLPOWER={sp / 100}, dégâts={sp % 100})" : "";
                if (pending.CardId == Strike)
                    Console.WriteLine($"{tag} FRAPPE TURN={h.GameTurn} serviteurs coût actuel>=5 : [{string.Join(", ", bigByCurrent)}] ; coût de base>=5 : [{string.Join(", ", bigByBase)}] ; serviteurs piochés : [{string.Join(", ", drawnMinions)}]{seerInfo} ; cible {target?.CardId} PV {pending.TargetHealthBefore}->{targetHealthAfter}");
                else if (pending.SeerSpellPower is not null)
                    Console.WriteLine($"{tag} SUFFUSION TURN={h.GameTurn}{seerInfo} ; cible {target?.CardId} PV {pending.TargetHealthBefore}->{targetHealthAfter}");
                pending = null;
            }

            // Voyante blessée : valeur du tag SPELLPOWER.
            if (line.Method == "DebugPrintPower" && line.Body.Contains("tag=SPELLPOWER", StringComparison.Ordinal))
            {
                foreach (var e in engine.Entities.Values.Where(e => e.CardId == Seer && e.Zone == "PLAY"))
                    Console.WriteLine($"{tag} VOYANTE #{e.Id} TURN={engine.GameTurn} dégâts={e.GetInt("DAMAGE")} SPELLPOWER={e.GetInt("SPELLPOWER")}");
            }

            // Options : recharge des lieux et cibles du Wyrm.
            if (line.Method == "DebugPrintOptions" && line.Body.StartsWith("option ", StringComparison.Ordinal) && engine.CurrentOptions is { } opts)
            {
                var last = opts.Options.LastOrDefault();
                if (last?.EntityId is int eid && engine.Entities.TryGetValue(eid, out var ent) && ent.Controller == local && engine.CurrentPlayerId == local)
                {
                    if (ent.CardId == Depths && ent.Zone == "PLAY")
                    {
                        var lastUse = locationUses.LastOrDefault(u => u.Entity == eid);
                        if (lastUse != default)
                            Console.WriteLine($"{tag} LIEU #{eid} TURN={engine.GameTurn} (utilisé TURN={lastUse.GameTurn}, écart {engine.GameTurn - lastUse.GameTurn}) serveur={last.Error} cooldown={ent.GetInt("LOCATION_ACTION_COOLDOWN")} épuisé={ent.GetInt("EXHAUSTED")} charges={ent.GetInt("HEALTH") - ent.GetInt("DAMAGE")}");
                    }
                }
            }
            if (line.Method == "DebugPrintOptions" && !line.Body.StartsWith("option ", StringComparison.Ordinal) && !line.Body.StartsWith("id=", StringComparison.Ordinal))
                continue;
            // Cibles du Wyrm : lues une fois le bloc d'options complet (première ligne suivante hors options).
            if (line.Method != "DebugPrintOptions" && engine.CurrentOptions is { } done && engine.OptionsFresh && reportedWyrmOptions.Add(done.Id))
            {
                foreach (var o in done.Options.Where(o => o.EntityId is int id && engine.Entities.GetValueOrDefault(id)?.CardId == Wyrm && engine.Entities[id].Zone == "HAND"))
                {
                    var targets = o.Targets.Where(t => t.Playable).Select(t => engine.Entities.GetValueOrDefault(t.EntityId))
                        .Where(t => t is not null).Select(t => $"{t!.CardType}:{(t.Controller == local ? "allié" : "adverse")}").GroupBy(x => x).Select(g => $"{g.Key}×{g.Count()}");
                    Console.WriteLine($"{tag} WYRM option TURN={engine.GameTurn} {o.Error} coût={engine.Entities[o.EntityId!.Value].GetInt("COST")} cibles=[{string.Join(", ", targets)}]");
                }
            }

            // Grommash blessé en jeu : attaque.
            if (line.Method == "DebugPrintPower" && line.Body.Contains("tag=DAMAGE", StringComparison.Ordinal))
            {
                foreach (var e in engine.Entities.Values.Where(e => e.CardId == Grommash && e.Zone == "PLAY" && e.GetInt("DAMAGE") > 0))
                    Console.WriteLine($"{tag} GROMMASH #{e.Id} TURN={engine.GameTurn} dégâts={e.GetInt("DAMAGE")} ATQ={e.GetInt("ATK")} propriétaire={(e.Controller == local ? "vous" : "adversaire")}");
            }
        }
    }
}
