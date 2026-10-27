// Agent E : le TurnSolver rate-t-il des létaux faute d'échanges ? Aucun appel réseau.
// Pour chaque position figée dont le visage est bloqué par une Provocation et dont le solveur ne dit pas « certain »,
// on énumère des séquences d'échanges DÉTERMINISTES contre les Provocations (attaques de tes serviteurs et de ton héros,
// sorts et Cris de guerre « Inflige N » ciblés, serviteurs à Ruée joués depuis la main), on applique leur issue
// (ATQ contre PV, Bouclier divin, Toxicité, riposte, Grommash blessé), puis on rappelle le VRAI TurnSolver (copie de
// HsCopilot.Core.dll) sur l'état obtenu. Une Provocation à Râle d'agonie, Réincarnation ou déclencheur « subit des dégâts »
// rend la ligne « à risque ».
// Usage : harness trades <positions.jsonl> <sortie.jsonl>
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Util;

Console.OutputEncoding = System.Text.Encoding.UTF8;
var mode = args[0];
var cases = DatasetBuilder.Load(args[1]);
if (mode == "display")
{
    // Ce que l'overlay afficherait (AdviceDisplay.Build réel) sur une position, selon la phase du conseiller.
    var c = cases.First(x => x.CaseId.EndsWith(args[2], StringComparison.Ordinal));
    var ctx = BenchRunner.BuildContext(c);
    var lethal = QuickAnalysis.AssessLethal(ctx);
    var facts = QuickAnalysis.Analyze(ctx, lethal);
    var act = ctx.LegalActions.First(a => a.Label == c.Actual!.Label);
    var rec = new Recommendation
    {
        GameId = ctx.GameId, Revision = ctx.Revision, OptionsId = ctx.OptionsId, GameTurn = ctx.GameTurn, ResetCountAtCreation = 0,
        HistorySeqAtCreation = 0, Action = act, Uncertainty = UncertaintyLevel.Moyenne, Summary = "(conseil fictif)", Provider = "test",
    };
    var phases = new (string Name, AdvisorUpdate U)[]
    {
        ("analyse", new AdvisorUpdate { Phase = AdvisorPhases.Analyzing, State = ctx.State, Facts = facts, Lethal = lethal }),
        ("partiel", new AdvisorUpdate { Phase = AdvisorPhases.Partial, State = ctx.State, Facts = facts, Lethal = lethal, PartialAction = act }),
        ("prêt (modèle)", new AdvisorUpdate { Phase = AdvisorPhases.Ready, State = ctx.State, Facts = facts, Lethal = lethal, Recommendation = rec }),
        ("prêt (suite du plan)", new AdvisorUpdate { Phase = AdvisorPhases.Ready, State = ctx.State, Facts = facts, Lethal = lethal, Recommendation = new Recommendation
            { GameId = rec.GameId, Revision = rec.Revision, OptionsId = rec.OptionsId, GameTurn = rec.GameTurn, ResetCountAtCreation = 0, HistorySeqAtCreation = 0, Action = act, Uncertainty = UncertaintyLevel.Moyenne, Summary = "(étape de plan)", Provider = "test", Origin = AdviceOrigins.Plan } }),
    };
    Console.WriteLine($"faits calculés ({facts.Count}) :");
    foreach (var f in facts) Console.WriteLine($"  [{f.Severity}] {f.Text}");
    foreach (var (name, u) in phases)
    {
        var v = AdviceDisplay.Build(u, DateTime.UtcNow);
        var danger = v.Facts.Any(f => f.StartsWith("Danger au prochain tour", StringComparison.Ordinal));
        Console.WriteLine($"phase {name} : titre « {v.ActionTitle} », action « {v.ActionText} », faits affichés {v.Facts.Count}, alerte de danger visible : {(danger ? "OUI" : "NON")}");
    }
    return;
}
if (mode == "dubious")
{
    // Détecteur de coups douteux existant (DubiousMoveDetector) appliqué au conseil réellement affiché en partie.
    var advice = File.ReadLines(args[2]).Select(l => JsonNode.Parse(l)!.AsObject()).ToDictionary(o => (string)o["caseId"]!);
    using var dw = new StreamWriter(args[3]);
    int matched = 0, flagged = 0; var codes = new Dictionary<string, int>();
    foreach (var c in cases)
    {
        if (!advice.TryGetValue(c.CaseId, out var adv)) continue;
        DecisionContext ctx; try { ctx = BenchRunner.BuildContext(c); } catch { continue; }
        var label = (string)adv["label"]!;
        var act = ctx.LegalActions.Where(a => label == a.Label || (label.StartsWith(a.Label + " ", StringComparison.Ordinal) && Regex.IsMatch(label[(a.Label.Length + 1)..], @"^(\d+/\d+|\(\d+ PV.*\))$")))
            .OrderByDescending(a => a.Label.Length).FirstOrDefault();
        if (act is null) continue;
        matched++;
        var flags = DubiousMoveDetector.Analyze(ctx, act);
        if (flags.Count > 0) flagged++;
        foreach (var f in flags) codes[$"{f.Code} ({f.Severity}) / {(string?)adv["origin"]}"] = codes.GetValueOrDefault($"{f.Code} ({f.Severity}) / {(string?)adv["origin"]}") + 1;
        dw.WriteLine(new JsonObject { ["caseId"] = c.CaseId, ["origin"] = (string?)adv["origin"], ["action"] = act.Label, ["flags"] = new JsonArray(flags.Select(f => (JsonNode)$"{f.Code} ({f.Severity}) : {f.Message}").ToArray()) }.ToJsonString(Json.Compact));
    }
    Console.WriteLine($"conseils appariés à une action légale : {matched} ; signalés : {flagged}");
    foreach (var kv in codes.OrderByDescending(kv => kv.Value)) Console.WriteLine($"  {kv.Value,4}  {kv.Key}");
    return;
}
var outPath = args[2];
var direct = new Regex(@"^(?:Cri de guerre\s*:\s*)?Inflige (\d+) points? de dégâts", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
var risky = new Regex(@"Râle d’agonie|Râle d'agonie|Réincarnation|subi des dégâts|subit des dégâts|survit|Une fois que ce serviteur", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
using var w = new StreamWriter(outPath);
int n = 0, withTaunt = 0, found = 0, foundSafe = 0;
var levels = new Dictionary<string, int>();
foreach (var c in cases)
{
    n++;
    var root = JsonNode.Parse(c.Snapshot.GetRawText())!;
    var baseRes = SolveNode(root);
    if (baseRes is null) continue;
    levels[baseRes.Level] = levels.GetValueOrDefault(baseRes.Level) + 1;
    var oppTaunts = Taunts(root).Count;
    var rec = new JsonObject
    {
        ["caseId"] = c.CaseId, ["turn"] = c.TurnNumber, ["decisionInTurn"] = c.DecisionInTurn,
        ["level"] = baseRes.Level, ["needed"] = baseRes.Needed, ["maxFace"] = baseRes.MaxFaceDamage, ["taunts"] = oppTaunts,
        ["played"] = c.Actual?.Label,
    };
    if (mode == "trades" && oppTaunts > 0 && baseRes.Level != LethalLevels.Certain)
    {
        withTaunt++;
        var memo = new HashSet<string>();
        (List<string> Steps, bool Risk, SolverResult Res)? best = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int nodes = 0;
        void Dfs(JsonNode s, List<string> steps, bool risk, int depth)
        {
            if (sw.ElapsedMilliseconds > 20000 || nodes > 20000) return;
            var key = Key(s);
            if (!memo.Add(key)) return;
            nodes++;
            if (depth > 0)
            {
                var r = SolveNode(s);
                if (r is not null && r.MaxFaceDamage >= r.Needed && r.Level is "certain" or "conditionnel")
                {
                    var thisRisk = risk || r.Level != LethalLevels.Certain;
                    if (best is null || (!thisRisk && best.Value.Risk) || (thisRisk == best.Value.Risk && steps.Count < best.Value.Steps.Count))
                        best = (steps.ToList(), thisRisk, r);
                    return;
                }
            }
            if (depth >= 4 || Taunts(s).Count == 0) return;
            foreach (var (label, next, rk) in TradeMoves(s))
            {
                steps.Add(label);
                Dfs(next, steps, risk || rk, depth + 1);
                steps.RemoveAt(steps.Count - 1);
            }
        }
        Dfs(root, new List<string>(), false, 0);
        rec["nodes"] = nodes;
        if (best is { } b)
        {
            found++; if (!b.Risk) foundSafe++;
            rec["tradeLethal"] = b.Risk ? "à risque" : "démontré";
            rec["tradeSteps"] = new JsonArray(b.Steps.Select(x => (JsonNode)x).ToArray());
            rec["afterLevel"] = b.Res.Level;
            rec["afterSequence"] = b.Res.SequenceText;
            rec["afterDamage"] = b.Res.MaxFaceDamage;
            rec["afterConditions"] = string.Join(" ; ", b.Res.Conditions);
        }
    }
    w.WriteLine(rec.ToJsonString(Json.Compact));
}
Console.WriteLine($"{n} cas ; niveaux du solveur : {string.Join(", ", levels.Select(kv => $"{kv.Key} {kv.Value}"))}");
Console.WriteLine($"positions bloquées par une Provocation, sans létal certain : {withTaunt} ; létal trouvé par échanges : {found} (dont démontrés sans risque : {foundSafe})");

SolverResult? SolveNode(JsonNode snap)
{
    var s = snap.Deserialize<GameStateSnapshot>(Json.Read);
    if (s is null) return null;
    var ctx = DecisionContextBuilder.Build(s);
    if (ctx is null) return null;
    return TurnSolver.Solve(ctx);
}

List<JsonObject> Taunts(JsonNode s) => s["opponent"]!["board"]!.AsArray().Select(x => x!.AsObject())
    .Where(m => (string?)m["type"] == "MINION" && Kw(m).Contains("TAUNT") && !Kw(m).Contains("STEALTH")).ToList();

HashSet<string> Kw(JsonObject m) => (m["keywords"]?.AsArray() ?? new JsonArray()).Select(k => (string)k!).ToHashSet();

string Key(JsonNode s)
{
    var opp = string.Join(",", s["opponent"]!["board"]!.AsArray().Select(m => $"{m!["entityId"]}:{m["health"]}:{string.Join("", Kw(m.AsObject()).Where(k => k is "DIVINE_SHIELD"))}"));
    var mine = string.Join(",", s["local"]!["board"]!.AsArray().Select(m => $"{m!["entityId"]}:{m["attack"]}:{m["health"]}:{m["state"]?["attacksThisTurn"]}"));
    var opts = string.Join(",", s["legalOptions"]!["playable"]!.AsArray().Select(o => $"{o!["entityId"]}"));
    return $"{opp}|{mine}|{opts}|{s["local"]!["mana"]!["available"]}|{s["local"]!["hero"]!["attacksThisTurn"]}";
}

IEnumerable<(string, JsonNode, bool)> TradeMoves(JsonNode s)
{
    var taunts = Taunts(s).ToDictionary(t => (int)t["entityId"]!);
    var playable = s["legalOptions"]!["playable"]!.AsArray();
    var hand = s["local"]!["hand"]!["cards"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (int)x["entityId"]!);
    var myBoard = s["local"]!["board"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (int)x["entityId"]!);
    var heroId = (int)s["local"]!["hero"]!["entityId"]!;
    var spellPower = myBoard.Values.SelectMany(m => Kw(m)).Where(k => k.StartsWith("SPELLPOWER+")).Sum(k => int.TryParse(k["SPELLPOWER+".Length..], out var v) ? v : 0);
    foreach (var o in playable)
    {
        var kind = (string)o!["kind"]!;
        var src = (int?)o["entityId"];
        if (src is null) continue;
        var targets = o["targets"]!.AsArray().Select(t => (int)t!["entityId"]!).Where(taunts.ContainsKey).ToList();
        if (kind == "attack")
        {
            foreach (var t in targets)
            {
                var n2 = s.DeepClone();
                var risk = Attack(n2, src.Value, t, heroId);
                yield return ($"{o["name"]} attaque {taunts[t]["name"]} {taunts[t]["attack"]}/{taunts[t]["health"]}", n2, risk);
            }
        }
        else if (kind == "play" && hand.TryGetValue(src.Value, out var card))
        {
            var text = (string?)card["text"] ?? "";
            var cost = (int?)card["cost"] ?? 99;
            var mana = (int)s["local"]!["mana"]!["available"]!;
            if (cost > mana) continue;
            var m = direct.Match(text);
            if (m.Success)
            {
                var dmg = int.Parse(m.Groups[1].Value) + ((string?)card["type"] == "SPELL" ? spellPower : 0);
                foreach (var t in targets)
                {
                    var n2 = s.DeepClone();
                    var risk = Damage(n2, t, dmg, false);
                    PlayCard(n2, src.Value, cost, (string?)card["type"] == "MINION" ? card : null, false);
                    yield return ($"{card["name"]} ({dmg}) → {taunts[t]["name"]}", n2, risk);
                }
            }
            else if ((string?)card["type"] == "MINION" && text.TrimStart().StartsWith("Ruée", StringComparison.Ordinal))
            {
                var n2 = s.DeepClone();
                PlayCard(n2, src.Value, cost, card, true);
                yield return ($"jouer {card["name"]} (Ruée)", n2, false);
            }
        }
    }
}

// Retire la carte de la main, paie son coût ; un serviteur arrive sur le plateau (à Ruée : il peut attaquer les serviteurs).
void PlayCard(JsonNode s, int id, int cost, JsonObject? minion, bool rush)
{
    var local = s["local"]!;
    var cards = local["hand"]!["cards"]!.AsArray();
    var idx = cards.ToList().FindIndex(x => (int)x!["entityId"]! == id);
    if (idx >= 0) cards.RemoveAt(idx);
    local["hand"]!["count"] = cards.Count;
    local["mana"]!["available"] = (int)local["mana"]!["available"]! - cost;
    var playable = s["legalOptions"]!["playable"]!.AsArray();
    var pi = playable.ToList().FindIndex(x => (int?)x!["entityId"] == id);
    if (pi >= 0) playable.RemoveAt(pi);
    if (minion is null) return;
    var board = local["board"]!.AsArray();
    var kws = new JsonArray();
    if (rush) kws.Add("RUSH");
    var atk = (int?)minion["attack"] ?? 0;
    if (((string?)minion["text"] ?? "").Contains("double l’ATQ", StringComparison.Ordinal)) atk *= 2;
    board.Add(new JsonObject
    {
        ["entityId"] = id, ["cardId"] = (string?)minion["cardId"], ["name"] = (string?)minion["name"], ["type"] = "MINION",
        ["position"] = board.Count + 1, ["attack"] = atk, ["health"] = (int?)minion["health"] ?? 1, ["maxHealth"] = (int?)minion["health"] ?? 1,
        ["damage"] = 0, ["keywords"] = kws, ["enchantments"] = new JsonArray(),
        ["state"] = new JsonObject { ["exhausted"] = !rush, ["attacksThisTurn"] = 0, ["justPlayed"] = true, ["frozen"] = false, ["dormant"] = false, ["cantAttack"] = false },
        ["text"] = (string?)minion["text"],
    });
    if (rush)
    {
        var targets = new JsonArray(s["opponent"]!["board"]!.AsArray().Where(m => (string?)m!["type"] == "MINION")
            .Select(m => (JsonNode)new JsonObject { ["entityId"] = (int)m!["entityId"]!, ["name"] = (string?)m["name"], ["side"] = "opponent" }).ToArray());
        playable.Add(new JsonObject { ["index"] = 90 + playable.Count, ["kind"] = "attack", ["entityId"] = id, ["name"] = (string?)minion["name"], ["targets"] = targets, ["subOptions"] = new JsonArray() });
    }
}

// Dégâts sur un serviteur adverse ; renvoie vrai si la Provocation a un effet non simulé.
bool Damage(JsonNode s, int target, int dmg, bool poison)
{
    var board = s["opponent"]!["board"]!.AsArray();
    var t = board.First(x => (int)x!["entityId"]! == target)!.AsObject();
    var risk = risky.IsMatch((string?)t["text"] ?? "") || Kw(t).Overlaps(new[] { "DEATHRATTLE", "REBORN" });
    var kws = t["keywords"]!.AsArray();
    var shield = kws.ToList().FindIndex(k => (string)k! == "DIVINE_SHIELD");
    if (shield >= 0) { kws.RemoveAt(shield); return risk; }
    if (dmg <= 0) return risk;
    var hp = (int)t["health"]! - dmg;
    if (poison) hp = 0;
    t["health"] = hp;
    if (hp <= 0) RemoveEntity(s, target);
    return risk;
}

bool Attack(JsonNode s, int src, int target, int heroId)
{
    var oppBoard = s["opponent"]!["board"]!.AsArray();
    var t = oppBoard.First(x => (int)x!["entityId"]! == target)!.AsObject();
    var tAtk = (int?)t["attack"] ?? 0;
    var tPoison = Kw(t).Overlaps(new[] { "POISONOUS", "VENOMOUS" });
    bool risk;
    if (src == heroId)
    {
        var hero = s["local"]!["hero"]!;
        risk = Damage(s, target, (int)hero["attack"]!, false);
        var armor = (int?)hero["armor"] ?? 0; var hit = Math.Max(0, tAtk - armor);
        hero["armor"] = Math.Max(0, armor - tAtk); hero["health"] = (int)hero["health"]! - hit;
        hero["attacksThisTurn"] = (int)hero["attacksThisTurn"]! + 1;
        if (s["local"]!["weapon"] is JsonObject wpn) wpn["durability"] = (int)wpn["durability"]! - 1;
        RemoveOption(s, src);
        return risk;
    }
    var me = s["local"]!["board"]!.AsArray().First(x => (int)x!["entityId"]! == src)!.AsObject();
    var myPoison = Kw(me).Overlaps(new[] { "POISONOUS", "VENOMOUS" });
    risk = Damage(s, target, (int)me["attack"]!, myPoison);
    if (risky.IsMatch((string?)me["text"] ?? "") && ((string?)me["text"] ?? "").Contains("attaqu")) risk = true;
    // Riposte.
    var kws = me["keywords"]!.AsArray();
    var shield = kws.ToList().FindIndex(k => (string)k! == "DIVINE_SHIELD");
    if (shield >= 0) kws.RemoveAt(shield);
    else if (tAtk > 0)
    {
        var hp = (int)me["health"]! - tAtk; if (tPoison) hp = 0;
        if ((string?)me["cardId"] == "CORE_EX1_414" && (int)me["health"]! >= ((int?)me["maxHealth"] ?? 0)) me["attack"] = (int)me["attack"]! + 6;
        me["health"] = hp; me["damage"] = ((int?)me["damage"] ?? 0) + tAtk;
        if (hp <= 0) { RemoveEntity(s, src); return risk; }
    }
    var st = me["state"]!.AsObject();
    st["attacksThisTurn"] = ((int?)st["attacksThisTurn"] ?? 0) + 1;
    var wf = Kw(me).Contains("MEGA_WINDFURY") ? 4 : Kw(me).Contains("WINDFURY") ? 2 : 1;
    if ((int)st["attacksThisTurn"]! >= wf) RemoveOption(s, src);
    return risk;
}

void RemoveOption(JsonNode s, int src)
{
    var playable = s["legalOptions"]!["playable"]!.AsArray();
    for (int i = playable.Count - 1; i >= 0; i--)
        if ((string)playable[i]!["kind"]! == "attack" && (int?)playable[i]!["entityId"] == src) playable.RemoveAt(i);
}

// Retire une entité morte du plateau et des cibles ; plus de Provocation : les attaquants restants peuvent viser le héros.
void RemoveEntity(JsonNode s, int id)
{
    foreach (var side in new[] { "opponent", "local" })
    {
        var b = s[side]!["board"]!.AsArray();
        var i = b.ToList().FindIndex(x => (int)x!["entityId"]! == id);
        if (i >= 0) b.RemoveAt(i);
    }
    var playable = s["legalOptions"]!["playable"]!.AsArray();
    for (int i = playable.Count - 1; i >= 0; i--)
    {
        if ((int?)playable[i]!["entityId"] == id && (string)playable[i]!["kind"]! == "attack") { playable.RemoveAt(i); continue; }
        var ts = playable[i]!["targets"]!.AsArray();
        for (int j = ts.Count - 1; j >= 0; j--) if ((int)ts[j]!["entityId"]! == id) ts.RemoveAt(j);
    }
    if (Taunts(s).Count > 0) return;
    var opp = s["opponent"]!["hero"]!;
    var oppId = (int)opp["entityId"]!;
    var heroId = (int)s["local"]!["hero"]!["entityId"]!;
    var myBoard = s["local"]!["board"]!.AsArray().Select(x => x!.AsObject()).ToDictionary(x => (int)x["entityId"]!);
    foreach (var o in playable)
    {
        if ((string)o!["kind"]! != "attack") continue;
        var src = (int?)o["entityId"];
        var ts = o["targets"]!.AsArray();
        if (ts.Any(t => (int)t!["entityId"]! == oppId)) continue;
        if (src != heroId && src is int sid && myBoard.TryGetValue(sid, out var m))
        {
            var k = Kw(m);
            if ((bool?)m["state"]?["justPlayed"] == true && k.Contains("RUSH") && !k.Contains("CHARGE")) continue;
            if (((string?)m["text"] ?? "").Contains("ne peut pas attaquer les héros", StringComparison.OrdinalIgnoreCase)) continue;
        }
        ts.Add(new JsonObject { ["entityId"] = oppId, ["name"] = (string?)opp["name"], ["side"] = "opponent" });
    }
}
