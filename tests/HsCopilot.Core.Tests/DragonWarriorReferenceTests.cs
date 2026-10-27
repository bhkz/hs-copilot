using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Tests de référence du Guerrier Dragons. Chaque règle est vérifiée comme un invariant sur toutes les parties réelles
/// disponibles (et non sur un seul exemple), avec le nombre d'observations exigé.
/// </summary>
public class DragonWarriorReferenceTests
{
    private const string Depths = "CORE_REV_990";
    private const string Grommash = "CORE_EX1_414";
    private const string Strike = "TIME_750";
    private const string Suffusion = "FIR_939";
    private const string Seer = "END_022";
    private const string Wyrm = "TLC_600";
    private const string Slitherdrake = "END_033";

    private static readonly string[] AllFixtures =
    {
        TestData.PriestGame, TestData.RogueGame, TestData.RewindGame, TestData.GrommashGame, TestData.SeerGame,
    };

    // ------------------------------------------------------------------ Grommash + Profondeurs Sanguines

    [Fact]
    public void Grommash_with_smith_buff_and_sanguine_depths_hits_for_14_then_18()
    {
        // Partie réelle contre l'IA : Grommash (6 ATQ grâce au Forgeron dimensionnel) reçoit les Profondeurs : 6 + 2 = 8,
        // blessé : + 6 = 14, attaque le héros adverse. Tour suivant : une seconde activation l'amène à 18.
        var engine = TestData.NewEngine();
        var lines = TestData.Lines(TestData.GrommashGame);
        var seen = 0;
        var observations = new List<string>();
        (int Atk, int Grom)? pendingActivation = null;
        (int HeroHp, int Grom, int Atk)? pendingAttack = null;
        foreach (var raw in lines)
        {
            var line = PowerLogLineParser.TryParse(raw);
            if (line is null) continue;
            engine.Process(line);
            while (seen < engine.History.Count)
            {
                var h = engine.History[seen++];
                if (h.PlayerId != engine.LocalPlayerId || h.Rewound) continue;
                var target = h.TargetId is int t ? engine.Entities.GetValueOrDefault(t) : null;
                var source = h.SourceId is int s ? engine.Entities.GetValueOrDefault(s) : null;
                if (h.Kind == HistoryKind.LocationActivation && source?.CardId == Depths && target?.CardId == Grommash)
                    pendingActivation = (target.GetInt("ATK"), target.Id);
                if (h.Kind == HistoryKind.Attack && source?.CardId == Grommash && target?.CardType == "HERO")
                    pendingAttack = (target.GetInt("HEALTH") - target.GetInt("DAMAGE") + target.GetInt("ARMOR"), source.Id, source.GetInt("ATK"));
            }
            if (engine.BlockDepth != 0) continue;
            if (pendingActivation is { } pa)
            {
                var g = engine.Entities[pa.Grom];
                observations.Add($"activation {pa.Atk}->{g.GetInt("ATK")} dégâts {g.GetInt("DAMAGE")}");
                pendingActivation = null;
            }
            if (pendingAttack is { } pk)
            {
                var hero = engine.Entities.Values.First(e => e.CardType == "HERO" && e.Zone == "PLAY" && e.Controller != engine.LocalPlayerId);
                var hp = hero.GetInt("HEALTH") - hero.GetInt("DAMAGE") + hero.GetInt("ARMOR");
                observations.Add($"attaque {pk.Atk} : héros {pk.HeroHp}->{hp}");
                pendingAttack = null;
            }
        }
        Assert.Contains("activation 6->14 dégâts 1", observations);
        Assert.Contains("activation 16->18 dégâts 2", observations);
        Assert.Contains(observations, o => o.StartsWith("attaque 14 : héros", StringComparison.Ordinal) && Delta(o) == 14);
        Assert.Contains(observations, o => o.StartsWith("attaque 18 : héros", StringComparison.Ordinal) && Delta(o) == 18);
    }

    private static int Delta(string attackObservation)
    {
        var part = attackObservation[(attackObservation.IndexOf("héros ", StringComparison.Ordinal) + 6)..].Split("->");
        return int.Parse(part[0]) - int.Parse(part[1]);
    }

    [Fact]
    public void Grommash_in_hand_shows_smith_buff_and_depths_is_ready_on_the_combo_turn()
    {
        var (_, s) = TestData.AtLocalTurn(TestData.GrommashGame, 9);
        var grom = Assert.Single(s.Local!.Hand.Cards, c => c.CardId == Grommash);
        Assert.Equal(6, grom.Attack);
        Assert.Equal(4, grom.BaseAttack);
        Assert.NotEmpty(grom.Enchantments);
        Assert.Contains(s.LegalOptions!.Playable, o => o.CardId == Grommash && o.Kind == "play");
        Assert.Contains(s.Local.Board, m => m.CardId == Depths && m.State.ServerAllowsAction == true);
    }

    // ------------------------------------------------------------------ Recharge des lieux

    [Fact]
    public void Location_is_refused_on_the_next_player_turn_and_accepted_afterwards_in_every_game()
    {
        int sameTurn = 0, nextTurn = 0, later = 0;
        var contradictions = new List<string>();
        foreach (var fixture in AllFixtures)
        {
            var engine = TestData.NewEngine();
            var lastUse = new Dictionary<int, int>();
            var seen = 0;
            foreach (var raw in TestData.Lines(fixture))
            {
                var line = PowerLogLineParser.TryParse(raw);
                if (line is null) continue;
                engine.Process(line);
                while (seen < engine.History.Count)
                {
                    var h = engine.History[seen++];
                    if (h.Kind == HistoryKind.LocationActivation && !h.Rewound && h.PlayerId == engine.LocalPlayerId && h.SourceId is int sid)
                        lastUse[sid] = h.GameTurn;
                }
                if (line.Method != "DebugPrintOptions" || !line.Body.StartsWith("option ", StringComparison.Ordinal)) continue;
                var opt = engine.CurrentOptions?.Options.LastOrDefault();
                if (opt?.EntityId is not int eid || !lastUse.TryGetValue(eid, out var used)) continue;
                var e = engine.Entities[eid];
                if (e.CardId != Depths || e.Zone != "PLAY" || engine.CurrentPlayerId != engine.LocalPlayerId) continue;
                var gap = engine.GameTurn - used;
                switch (gap)
                {
                    case 0 when opt.Error == "REQ_NOT_EXHAUSTED_LOCATION": sameTurn++; break;
                    case 2 when opt.Error == "REQ_NOT_EXHAUSTED_LOCATION": nextTurn++; break;
                    case >= 4 when opt.Error != "REQ_NOT_EXHAUSTED_LOCATION": later++; break;
                    default: contradictions.Add($"{fixture} #{eid} écart {gap} : {opt.Error}"); break;
                }
            }
        }
        Assert.Empty(contradictions);
        Assert.True(nextTurn >= 5, $"refus au tour suivant observés : {nextTurn}");
        Assert.True(later >= 3, $"acceptations ensuite : {later}");
        Assert.True(sameTurn >= 1);
    }

    // ------------------------------------------------------------------ Parenté, Glissedrake

    private static IEnumerable<(string Fixture, GameStateSnapshot Snapshot)> LocalDecisionPoints()
    {
        foreach (var fixture in AllFixtures)
        {
            for (int turn = 1; turn <= 20; turn++)
            {
                var engine = TestData.NewEngine();
                if (ReplayRunner.RunUntilLocalTurn(engine, TestData.Lines(fixture), turn) < 0) break;
                yield return (fixture, TestData.Project(engine));
            }
        }
    }

    [Fact]
    public void Kindred_reduction_matches_a_dragon_played_on_the_previous_turn()
    {
        var observations = 0;
        foreach (var (fixture, s) in LocalDecisionPoints())
        {
            foreach (var wyrm in s.Local!.Hand.Cards.Where(c => c.CardId == Wyrm && c.Cost is 6 or 8))
            {
                observations++;
                var dragonLastTurn = s.Local.LastTurn?.TypesPlayed.Contains("DRAGON") == true;
                Assert.True((wyrm.Cost == 6) == dragonLastTurn, $"{fixture} TURN={s.Game.GameTurn} : Wyrm à {wyrm.Cost}, Dragon au dernier tour : {dragonLastTurn}");
            }
        }
        Assert.True(observations >= 5, $"observations : {observations}");
    }

    [Fact]
    public void Slitherdrake_reduction_matches_another_dragon_in_hand()
    {
        var observations = 0;
        foreach (var (fixture, s) in LocalDecisionPoints())
        {
            var hand = s.Local!.Hand.Cards;
            foreach (var drake in hand.Where(c => c.CardId == Slitherdrake && c.Cost is 4 or 7))
            {
                observations++;
                var otherDragon = hand.Any(c => c.EntityId != drake.EntityId && TestData.Cards.Get(c.CardId)?.HasRace("DRAGON") == true);
                Assert.True((drake.Cost == 4) == otherDragon, $"{fixture} TURN={s.Game.GameTurn} : Glissedrake à {drake.Cost}, autre Dragon : {otherDragon}");
                Assert.Equal(otherDragon, drake.PoweredUp);
            }
        }
        Assert.True(observations >= 5, $"observations : {observations}");
    }

    // ------------------------------------------------------------------ Frappe anticipée, Voyante blessée

    private sealed record SpellPlay(string Fixture, string CardId, List<int> HandMinionCosts, List<int> HandMinionBaseCosts, bool DrewMinion, int? MetaDamage, bool DamagedSeerOnBoard, bool SeerKeywordShown);

    private static List<SpellPlay> SpellPlays()
    {
        var result = new List<SpellPlay>();
        foreach (var fixture in AllFixtures)
        {
            var engine = TestData.NewEngine();
            var projector = new GameStateProjector(TestData.Cards);
            var seen = 0;
            HistoryEntry? pending = null;
            List<EntityState>? handBefore = null;
            int? metaDamage = null;
            var expectDamage = false;
            bool seerDamaged = false, seerKeyword = false;
            foreach (var raw in TestData.Lines(fixture))
            {
                var line = PowerLogLineParser.TryParse(raw);
                if (line is null) continue;
                if (pending is not null && line.Method == "DebugPrintPower")
                {
                    if (expectDamage && line.Body.StartsWith("Info[0]", StringComparison.Ordinal)) expectDamage = false;
                    if (metaDamage is null && line.Body.StartsWith("META_DATA - Meta=DAMAGE Data=", StringComparison.Ordinal))
                    {
                        metaDamage = int.Parse(PowerStatements.FirstToken(line.Body["META_DATA - Meta=DAMAGE Data=".Length..]));
                        expectDamage = true;
                    }
                }
                engine.Process(line);
                while (seen < engine.History.Count)
                {
                    var h = engine.History[seen++];
                    if (h.Kind != HistoryKind.Play || h.Rewound || h.PlayerId != engine.LocalPlayerId || h.SourceId is not int sid) continue;
                    var card = engine.Entities[sid].CardId;
                    if (card is not (Strike or Suffusion)) continue;
                    pending = h;
                    metaDamage = null;
                    handBefore = engine.Entities.Values.Where(e => !e.Orphaned && e.Controller == engine.LocalPlayerId && e.Zone == "HAND" && e.CardType == "MINION" && e.Id != sid).ToList();
                    var seer = engine.Entities.Values.FirstOrDefault(e => !e.Orphaned && e.Controller == engine.LocalPlayerId && e.Zone == "PLAY" && e.CardId == Seer && e.GetInt("DAMAGE") > 0);
                    seerDamaged = seer is not null;
                    seerKeyword = seer is not null && projector.Project(engine).Local!.Board.Single(m => m.EntityId == seer.Id).Keywords.Contains("SPELLPOWER+2");
                }
                if (pending is not null && engine.BlockDepth == 0 && line.Method == "DebugPrintPower")
                {
                    var handNow = engine.Entities.Values.Where(e => !e.Orphaned && e.Controller == engine.LocalPlayerId && e.Zone == "HAND").Select(e => e.Id).ToHashSet();
                    var drew = engine.Entities.Values.Any(e => handNow.Contains(e.Id) && e.CardType == "MINION" && e.InitialZone == "DECK" && !handBefore!.Any(b => b.Id == e.Id));
                    result.Add(new SpellPlay(fixture, engine.Entities[pending.SourceId!.Value].CardId,
                        handBefore!.Select(e => e.GetInt("COST")).ToList(),
                        handBefore!.Select(e => TestData.Cards.Get(e.CardId)?.Cost ?? -1).ToList(),
                        drew, metaDamage, seerDamaged, seerKeyword));
                    pending = null;
                }
            }
        }
        return result;
    }

    [Fact]
    public void Precursory_strike_draws_exactly_when_a_minion_costing_five_or_more_is_held()
    {
        var strikes = SpellPlays().Where(p => p.CardId == Strike).ToList();
        Assert.True(strikes.Count >= 3, $"Frappes observées : {strikes.Count}");
        foreach (var p in strikes)
        {
            var bigByCurrentCost = p.HandMinionCosts.Any(c => c >= 5);
            var bigByBaseCost = p.HandMinionBaseCosts.Any(c => c >= 5);
            if (bigByCurrentCost == bigByBaseCost)
                Assert.True(p.DrewMinion == bigByCurrentCost, $"{p.Fixture} : coûts {string.Join(",", p.HandMinionCosts)} pioche {p.DrewMinion}");
            // Cas discriminant (seul serviteur de base ≥ 5 réduit sous 5, ex. Glissedrake à 4) : absent des logs disponibles.
            // La règle exacte (coût actuel ou de base) reste à observer ; voir docs/CARTES-GUERRIER-DRAGONS.md.
        }
    }

    [Fact]
    public void Damaged_seer_adds_two_spell_damage_to_actual_damage_dealt()
    {
        var plays = SpellPlays().Where(p => p.DamagedSeerOnBoard && p.MetaDamage is not null).ToList();
        Assert.True(plays.Count >= 2, $"sorts lancés avec une Voyante blessée : {plays.Count}");
        foreach (var p in plays)
        {
            var baseDamage = p.CardId == Strike ? 3 : 2;
            Assert.Equal(baseDamage + 2, p.MetaDamage);
            Assert.True(p.SeerKeywordShown, "le plateau doit afficher Dégâts des sorts +2 sur la Voyante blessée");
        }
    }

    // ------------------------------------------------------------------ Wyrm du pic venteux

    [Fact]
    public void Windpeak_wyrm_can_target_any_character_including_both_heroes()
    {
        var observations = 0;
        foreach (var fixture in AllFixtures)
        {
            var engine = TestData.NewEngine();
            var lines = TestData.Lines(fixture);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = PowerLogLineParser.TryParse(lines[i]);
                if (line is null) continue;
                engine.Process(line);
                var next = i + 1 < lines.Count ? lines[i + 1] : "";
                if (line.Method != "DebugPrintOptions" || next.Contains("GameState.DebugPrintOptions()", StringComparison.Ordinal)) continue;
                foreach (var o in engine.CurrentOptions!.Options.Where(o => o.Playable && o.EntityId is int id && engine.Entities[id].CardId == Wyrm && engine.Entities[id].Zone == "HAND"))
                {
                    observations++;
                    var targets = o.Targets.Where(t => t.Playable).Select(t => engine.Entities[t.EntityId]).ToList();
                    Assert.Contains(targets, t => t.CardType == "HERO" && t.Controller == engine.LocalPlayerId);
                    Assert.Contains(targets, t => t.CardType == "HERO" && t.Controller != engine.LocalPlayerId);
                    var minionsInPlay = engine.Entities.Values.Count(e => !e.Orphaned && e.Zone == "PLAY" && e.CardType == "MINION");
                    Assert.True(targets.Count(t => t.CardType == "MINION") <= minionsInPlay);
                }
            }
        }
        Assert.True(observations >= 3, $"options du Wyrm jouables : {observations}");
    }
}
