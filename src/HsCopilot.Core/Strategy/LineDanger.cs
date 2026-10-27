using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using HsCopilot.Core.Cards;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>Une étape d'une ligne simulée, avec ce que la simulation n'a pas pu modéliser.</summary>
public sealed record LineStep(string Label, IReadOnlyList<string> Unmodeled);

/// <summary>
/// Danger au prochain tour adverse avant et après un coup, pour la même mesure que <see cref="ThreatAssessment"/>.
/// « Meilleure marge » : la plus haute marge de fin de tour atteignable avec les défenses simulées (attaques et dégâts directs
/// sur les menaces, destruction, armure, Provocations jouées, Profondeurs Sanguines). Hors ligne : rien n'est affiché ni envoyé.
/// </summary>
public sealed record LineDangerReport
{
    public const string Complete = "complète";
    public const string Partial = "partielle";
    public const string NotSimulable = "non simulable";

    public const string LethalThisTurn = "létal ce tour";
    /// <summary>Danger visible faible (marges &gt; 5) : ne dit rien de la main adverse, ce n'est jamais « ligne sûre ».</summary>
    public const string NoDanger = "danger visible faible";
    public const string AlreadyLost = "aucune défense simulée";
    public const string LosesLastDefense = "perd la dernière défense";
    public const string Worsens = "aggrave nettement";
    public const string Defends = "défend";
    public const string Neutral = "neutre";
    public const string NotEvaluated = "non évalué";

    public required string Verdict { get; init; }
    /// <summary>complète : tout ce qui compte pour le verdict est simulé ; partielle : voir <see cref="Unmodeled"/> ; non simulable : le coup lui-même ne l'est pas.</summary>
    public required string Status { get; init; }
    /// <summary>Marge si le tour s'arrête maintenant (identique à <see cref="ThreatReport.Margin"/>).</summary>
    public int MarginNow { get; init; }
    public int BestBefore { get; init; }
    public List<LineStep> BestLineBefore { get; init; } = new();
    public int? MarginAfterAction { get; init; }
    public int? BestAfter { get; init; }
    public List<LineStep> BestLineAfter { get; init; } = new();
    public List<string> Unmodeled { get; init; } = new();
    public int Nodes { get; init; }
    public bool Truncated { get; init; }
    public double ElapsedMs { get; init; }
}

/// <summary>
/// Simulation locale et prudente d'un tour, limitée aux effets dont le résultat est certain : combats (Bouclier divin,
/// Toxicité, Vol de vie), dégâts directs ciblés, destruction du Calamar pas cuit, armure, corps de serviteurs à mots-clés seuls,
/// armes sans texte, La pièce, Profondeurs Sanguines. Tout le reste (Râle d'agonie, Réincarnation, déclencheurs, auras,
/// Secrets, Cris de guerre non chiffrés) rend l'évaluation partielle ou le coup non simulable : jamais « ligne sûre ».
/// </summary>
public static class LineDanger
{
    /// <summary>Marge attribuée à une ligne qui tue le héros adverse ce tour.</summary>
    public const int WonMargin = 1000;
    private const int NodeCap = 20000;
    private const string Depths = "CORE_REV_990";
    private const string DepthsOriginal = "REV_990";
    private const string BroodKeeper = "EDR_457";
    private const string Chronolord = "TIME_714";

    private const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex LeadingKeywords = new(@"^(?:(?:Provocation|Bouclier divin|Toxicité|Vol de vie|Ruée|Charge|Furie des vents|Insaisissable|Réincarnation|Camouflage|Rembobinage)[\s,.]+)*", Opt);
    /// <summary>
    /// Phrase sans effet sur le plateau pendant ton tour : pioche, Découverte, carte ajoutée à la main, bonus en main, coût,
    /// dégâts des sorts conditionnels, effet de fin ou de début de tour qui ne vise que tes serviteurs, ton armure ou l'adversaire.
    /// </summary>
    private static readonly Regex BoardNeutral = new(@"^(?:(?:Cri de guerre|Râle d.agonie)\s*:\s*)?(?:si .{0,100}?, )?(?:vous piochez|découvre|ajoute .{0,100}? dans votre main|vous obtenez|donne .{0,60}? aux serviteurs(?: et aux armes)? dans votre main|lui donne \+\d+/\+\d+)"
        + @"|^Coûte \(\d+\) cristaux? de moins|^Parenté\s*:\s*coûte|^Réduit leur coût|^Il s.améliore|^A \+\d+ aux dégâts des sorts tant qu.elle est blessée"
        + @"|^Une fois que .{0,80}?, vous piochez"
        + @"|^(?:À la fin|Au début) de votre tour, (?:donne|gagne|vous piochez|réduit|inflige \d+ points? de dégâts (?:au héros|à un (?:personnage|adversaire|serviteur) (?:adverse|ennemi)))", Opt);
    private static readonly Regex KeywordsOnly = new(@"^(?:(?:Provocation|Bouclier divin|Toxicité|Vol de vie|Ruée|Charge|Furie des vents|Insaisissable|Réincarnation|Camouflage)[\s,.]*)*$", Opt);
    private static readonly Regex DamageFirst = new(@"^(?:Cri de guerre\s*:\s*)?inflige (\d+) points? de dégâts(?: à un (?:serviteur|personnage)(?: adverse| ennemi)?)?(?: et vous gagnez (\d+) points? d.armure)?\.\s*", Opt);
    private static readonly Regex Harmless = new(@"^(?:(?:Si .{0,120}?, )?vous piochez|Découvre|Parenté\s*:\s*coûte|Coûte \(\d+\))", Opt);
    private static readonly Regex ArmorIfDies = new(@"^S.il meurt, vous gagnez (\d+) points? d.armure\.?$", Opt);
    private static readonly Regex DestroyByAttack = new(@"^Cri de guerre\s*:\s*détruit un serviteur adverse avec une Attaque inférieure ou égale à celle de ce serviteur\.?$", Opt);
    private static readonly Regex MayDefend = new(@"dégâts|détrui|Provocation|armure|Gel|gèle|Silence|renvoie|transform|Ruée|Charge|ATQ", Opt);
    private static readonly Regex DamagePrevention = new(@"Insensible|ne peut pas (?:subir|être blessé)", Opt);
    /// <summary>Déclencheur qui ne réagit qu'au début ou à la fin d'un tour : sans effet sur les coups simulés.</summary>
    private static readonly Regex TurnBoundary = new(@"^(?:(?:Provocation|Bouclier divin|Toxicité|Vol de vie|Ruée|Charge|Insaisissable|Réincarnation|Camouflage|Furie des vents)[\s,.]+){0,3}(?:À la fin|Au début) de (?:votre|chaque) tour", Opt);
    /// <summary>Attaque qui change selon le tour, ou serviteur qui n'attaquera pas le héros : dégâts visibles incertains.</summary>
    private static readonly Regex UncertainAttack = new(@"pendant (?:le tour de votre adversaire|votre tour)|ne peut pas attaquer (?:les|de) héros|ne peut pas attaquer", Opt);

    private enum CardKind { Unmodeled, Damage, Destroy, Body, Weapon, Coin, DepthsCard, Neutral, Chronolord }

    private sealed class HandCard
    {
        public int Id;
        public string Name = "?";
        public string Type = "";
        public int Cost;
        public int Atk;
        public int Hp;
        public CardKind Kind;
        public int Damage;
        public int Armor;
        public int ArmorIfDies;
        public bool Taunt, Shield, Poison, Lifesteal, RushOrCharge;
        public HashSet<int> Targets = new();
        public bool MayDefend;
        /// <summary>Gardien des couvées dont la condition est remplie : épée 2/2.</summary>
        public bool Sword;
        /// <summary>Pourquoi le coup reste non simulé (texte affiché).</summary>
        public string? WhyNot;
    }

    private sealed class Unit
    {
        public int Id;
        public string Name = "?";
        public int Atk;
        public int Hp;
        public bool Taunt, Shield, Poison, Lifesteal, Stealth, Untouchable;
        /// <summary>Ennemis : attaques au prochain tour adverse (0 : gelé, en sommeil ou ne peut pas attaquer).</summary>
        public int Mult;
        /// <summary>Ennemis : dégâts de fin de tour au héros.</summary>
        public int EndTurn;
        /// <summary>Les tiens : attaques encore possibles ce tour.</summary>
        public int AttacksLeft;
        /// <summary>Effet non simulé et ce qui le déclenche (« mort » ou « dégâts » : les deux pour un déclencheur).</summary>
        public string? Effect;
        public bool EffectOnDamage;
        /// <summary>Déclencheur qui réagit aux actions des autres (« une fois que votre héros a attaqué »…) : tout coup devient partiel.</summary>
        public bool Reactive;
        /// <summary>Aura (« les serviteurs adjacents ont +1 ATQ »…) : une mort sur le plateau peut changer les attaques.</summary>
        public bool Aura;
        public Unit Clone() => (Unit)MemberwiseClone();
    }

    private sealed class Info
    {
        public int HeroId, EnemyHeroId;
        public bool Secret;
        public Dictionary<int, HandCard> Cards = new();
        public HashSet<int> DepthsTargets = new();
        public int DepthsEntity;
        /// <summary>Serviteurs adverses joués au tour précédent (Chronoseigneur Époque).</summary>
        public HashSet<int> PlayedLastTurn = new();
        /// <summary>Réserves sur la mesure elle-même (attaque adverse qui dépend du tour…), ajoutées à tout verdict de danger.</summary>
        public List<string> Caveats = new();
    }

    private sealed class Sim
    {
        public int Hp, MaxHp, Armor, HeroAtk, WeaponAtk, WeaponDur, HeroAttacksLeft, Mana, PowerCost, PowerArmor, EnemyHp, EnemyWeapon, EnemyPower, SpellPower;
        public bool PowerReady, DepthsReady, DepthsAnyTarget, Won;
        /// <summary>Une arme jouée maintenant donnerait une attaque au héros (il n'a pas attaqué et n'est pas gelé).</summary>
        public bool HeroCanArm;
        public bool WeaponLifesteal;
        /// <summary>Effet non simulé de ton arme, déclenché quand ton héros attaque.</summary>
        public string? WeaponEffect;
        public List<Unit> Mine = new();
        public List<Unit> Theirs = new();
        public HashSet<int> Played = new();

        public Sim Clone()
        {
            var c = (Sim)MemberwiseClone();
            c.Mine = Mine.Select(u => u.Clone()).ToList();
            c.Theirs = Theirs.Select(u => u.Clone()).ToList();
            c.Played = new HashSet<int>(Played);
            return c;
        }

        public string Key()
        {
            var sb = new StringBuilder();
            sb.Append(Hp).Append(',').Append(Armor).Append(',').Append(HeroAtk).Append(',').Append(WeaponDur).Append(',').Append(HeroAttacksLeft).Append(',')
              .Append(Mana).Append(',').Append(PowerReady).Append(',').Append(DepthsReady).Append(',').Append(EnemyHp).Append('|');
            foreach (var u in Mine.OrderBy(u => u.Id)) sb.Append(u.Id).Append(':').Append(u.Atk).Append(':').Append(u.Hp).Append(':').Append(u.Shield).Append(':').Append(u.AttacksLeft).Append(';');
            sb.Append('|');
            foreach (var u in Theirs.OrderBy(u => u.Id)) sb.Append(u.Id).Append(':').Append(u.Atk).Append(':').Append(u.Hp).Append(':').Append(u.Shield).Append(';');
            sb.Append('|').Append(string.Join(",", Played.OrderBy(x => x)));
            return sb.ToString();
        }
    }

    private sealed record Move(string Kind, int Source, int Target, string Label);

    private sealed record Found(int Margin, List<LineStep> Line);

    // ------------------------------------------------------------------ API

    /// <summary>Danger avant et après <paramref name="action"/>, avec la meilleure défense simulée dans les deux cas.</summary>
    public static LineDangerReport? Check(DecisionContext ctx, LegalAction action, LethalAssessment? lethal = null)
    {
        if (ctx.Kind != DecisionContext.KindAction || Build(ctx) is not { } built) return null;
        var sw = Stopwatch.StartNew();
        var (info, root) = built;
        var nodes = 0;
        var truncated = false;
        var marginNow = Margin(root);
        var before = Search(info, root, new(), ref nodes, ref truncated);
        lethal ??= QuickAnalysis.AssessLethal(ctx);
        var lethalNow = lethal?.Level == LethalLevels.Certain;

        var move = FromLegal(info, root, action, out var whyNot);
        if (move is null)
        {
            // Coup non simulé : aucun verdict sur l'après, même quand le danger visible avant est faible.
            return new LineDangerReport
            {
                Verdict = lethalNow ? LineDangerReport.LethalThisTurn : LineDangerReport.NotEvaluated,
                Status = LineDangerReport.NotSimulable, MarginNow = marginNow, BestBefore = before.Margin, BestLineBefore = before.Line,
                Unmodeled = new() { whyNot ?? "coup non simulé" }, Nodes = nodes, Truncated = truncated, ElapsedMs = sw.Elapsed.TotalMilliseconds,
            };
        }

        var (after, stepReasons) = Apply(info, root, move);
        var marginAfter = Margin(after);
        var best = move.Kind == "end" ? new Found(marginAfter, new()) : Search(info, after, new(), ref nodes, ref truncated);

        string verdict;
        if (lethalNow) verdict = LineDangerReport.LethalThisTurn;
        else if (marginNow > 5 && before.Margin > 5 && marginAfter > 5 && best.Margin > 5) verdict = LineDangerReport.NoDanger;
        else if (before.Margin <= 0) verdict = LineDangerReport.AlreadyLost;
        else if (best.Margin <= 0) verdict = LineDangerReport.LosesLastDefense;
        else if (before.Margin - best.Margin >= 3 && best.Margin <= 2) verdict = LineDangerReport.Worsens;
        else if (marginAfter > marginNow) verdict = LineDangerReport.Defends;
        else verdict = LineDangerReport.Neutral;

        // Ce qui n'est pas simulé et pourrait changer ce verdict-là.
        var reasons = new List<string>(stepReasons);
        reasons.AddRange(best.Line.SelectMany(st => st.Unmodeled));
        if (verdict is LineDangerReport.LosesLastDefense or LineDangerReport.Worsens)
        {
            reasons.AddRange(before.Line.SelectMany(st => st.Unmodeled));   // la défense « disponible avant » est-elle réelle ?
            reasons.AddRange(UnmodeledDefenses(info, after));                 // une défense non simulée reste-t-elle possible ?
        }
        if (verdict == LineDangerReport.AlreadyLost) reasons.AddRange(UnmodeledDefenses(info, root));
        if (verdict is not (LineDangerReport.NoDanger or LineDangerReport.LethalThisTurn)) reasons.AddRange(info.Caveats);
        if (truncated) reasons.Add("recherche tronquée");
        reasons = reasons.Distinct().ToList();

        return new LineDangerReport
        {
            Verdict = verdict,
            Status = reasons.Count == 0 ? LineDangerReport.Complete : LineDangerReport.Partial,
            MarginNow = marginNow, BestBefore = before.Margin, BestLineBefore = before.Line,
            MarginAfterAction = marginAfter, BestAfter = best.Margin, BestLineAfter = best.Line,
            Unmodeled = reasons, Nodes = nodes, Truncated = truncated, ElapsedMs = sw.Elapsed.TotalMilliseconds,
        };
    }

    /// <summary>Marge actuelle et meilleure marge simulée d'une position réelle (validation contre les états suivants du log).</summary>
    public static (int MarginNow, int Best)? Position(DecisionContext ctx)
    {
        if (Build(ctx) is not { } built) return null;
        int nodes = 0; var truncated = false;
        return (Margin(built.Root), Search(built.Info, built.Root, new(), ref nodes, ref truncated).Margin);
    }

    // ------------------------------------------------------------------ Construction

    private static (Info Info, Sim Root)? Build(DecisionContext ctx)
    {
        var s = ctx.State;
        if (s.Local is not { Hero: { } hero } me || s.Opponent is not { Hero: { } enemyHero } opp) return null;
        var info = new Info { HeroId = hero.EntityId, EnemyHeroId = enemyHero.EntityId, Secret = opp.Secrets.Any(x => !x.Known) };
        var attackers = ctx.LegalActions.Where(a => a.Kind == "attack" && a.SourceEntityId is not null).Select(a => a.SourceEntityId!.Value).ToHashSet();

        var root = new Sim
        {
            Hp = hero.Health, MaxHp = Math.Max(hero.MaxHealth, hero.Health), Armor = hero.Armor, HeroAtk = hero.Attack,
            WeaponAtk = me.Weapon?.Attack ?? 0, WeaponDur = me.Weapon?.Durability ?? 0,
            HeroAttacksLeft = attackers.Contains(hero.EntityId) ? 1 : 0,
            HeroCanArm = hero.AttacksThisTurn == 0 && !hero.Frozen,
            WeaponLifesteal = (me.Weapon?.Text ?? "").Contains("Vol de vie", StringComparison.OrdinalIgnoreCase),
            WeaponEffect = me.Weapon is { } mw && !KeywordsOnly.IsMatch((mw.Text ?? "").Trim()) ? $"{mw.Name} : effet de l'arme non simulé" : null,
            Mana = me.Mana.Available,
            EnemyHp = enemyHero.Health + enemyHero.Armor,
            EnemyWeapon = opp.Weapon is { Attack: > 0, Durability: > 0 } w && !enemyHero.Frozen ? w.Attack : 0,
            EnemyPower = opp.HeroPower is { } ep && ThreatAssessment.HeroPowerDamage.Match(ep.Text ?? "") is { Success: true } pm ? int.Parse(pm.Groups[1].Value) : 0,
            SpellPower = me.Board.Sum(m => m.Keywords.Where(k => k.StartsWith("SPELLPOWER+", StringComparison.Ordinal)).Sum(k => int.TryParse(k["SPELLPOWER+".Length..], out var v) ? v : 0)),
        };
        if (ctx.LegalActions.Any(a => a.Kind == "hero-power") && me.HeroPower is { } power)
        {
            root.PowerReady = true;
            root.PowerCost = power.Cost;
            root.PowerArmor = ThreatAssessment.ArmorGain.Match(power.Text ?? "") is { Success: true } am ? int.Parse(am.Groups[1].Value) : 0;
        }

        foreach (var m in me.Board.Where(m => m.Type == "MINION"))
        {
            var u = UnitOf(m);
            var n = m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1;
            u.AttacksLeft = attackers.Contains(m.EntityId) ? Math.Max(1, n - m.State.AttacksThisTurn) : 0;
            root.Mine.Add(u);
        }
        foreach (var m in opp.Board.Where(m => m.Type == "MINION"))
        {
            var u = UnitOf(m);
            u.Mult = m.Attack <= 0 || m.State.Frozen || m.State.Dormant || m.State.CantAttack || m.Keywords.Contains("FROZEN") ? 0
                : m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1;
            u.EndTurn = ThreatAssessment.EndOfTurnDamage.Match(m.Text ?? "") is { Success: true } em ? int.Parse(em.Groups[1].Value) : 0;
            if (u.Mult > 0 && !m.Keywords.Contains("SILENCED") && UncertainAttack.IsMatch(m.Text ?? "")) info.Caveats.Add($"{u.Name} : attaque au prochain tour incertaine (texte)");
            root.Theirs.Add(u);
        }
        // Dégâts de fin de tour d'une entité adverse qui n'est pas un serviteur (lieu…) : comptés comme fixes, comme ThreatAssessment.
        root.EnemyPower += opp.Board.Where(m => m.Type != "MINION").Sum(m => ThreatAssessment.EndOfTurnDamage.Match(m.Text ?? "") is { Success: true } em ? int.Parse(em.Groups[1].Value) : 0);

        foreach (var m in opp.LastTurn?.MinionsPlayed ?? new()) info.PlayedLastTurn.Add(m.EntityId);
        var depths = me.Board.FirstOrDefault(m => m.CardId is Depths or DepthsOriginal);
        if (depths is not null && ctx.LegalActions.Any(a => a.Kind == "location" && a.SourceEntityId == depths.EntityId))
        {
            root.DepthsReady = true;
            info.DepthsEntity = depths.EntityId;
            foreach (var a in ctx.LegalActions.Where(a => a.Kind == "location" && a.SourceEntityId == depths.EntityId && a.TargetEntityId is not null))
                info.DepthsTargets.Add(a.TargetEntityId!.Value);
        }

        var hand = me.Hand.Cards.ToDictionary(c => c.EntityId);
        foreach (var group in ctx.LegalActions.Where(a => a.Kind == "play" && a.SourceEntityId is not null).GroupBy(a => a.SourceEntityId!.Value))
        {
            if (!hand.TryGetValue(group.Key, out var c)) continue;
            var card = new HandCard
            {
                Id = c.EntityId, Name = c.Name ?? "?", Type = c.Type ?? "", Cost = group.First().ManaCost ?? c.Cost ?? 0,
                Atk = c.Attack ?? 0, Hp = c.Health ?? 0,
            };
            foreach (var a in group) if (a.TargetEntityId is int t) card.Targets.Add(t);
            Classify(card, c);
            if (card.Kind == CardKind.Body && c.CardId == BroodKeeper)
                card.Sword = c.PoweredUp == true || c.PoweredUp is null && me.Hand.Cards.Any(x => x.EntityId != c.EntityId && (x.Races?.Contains("DRAGON") ?? false));
            info.Cards[card.Id] = card;
        }
        return (info, root);
    }

    private static Unit UnitOf(MinionView m)
    {
        var silenced = m.Keywords.Contains("SILENCED");
        var u = new Unit
        {
            Id = m.EntityId, Name = m.Name ?? "serviteur", Atk = Math.Max(0, m.Attack), Hp = m.Health,
            Taunt = m.Keywords.Contains("TAUNT"), Shield = m.Keywords.Contains("DIVINE_SHIELD"), Poison = m.Keywords.Contains("POISONOUS"),
            Lifesteal = m.Keywords.Contains("LIFESTEAL"), Stealth = m.Keywords.Contains("STEALTH"),
            Untouchable = m.Keywords.Contains("UNTOUCHABLE") || m.Keywords.Contains("IMMUNE") || DamagePrevention.IsMatch(m.Text ?? ""),
        };
        if (silenced) return u;
        u.Aura = m.Keywords.Contains("AURA");
        if (m.Keywords.Contains("TRIGGER_VISUAL"))
        {
            var text = m.Text ?? "";
            if (TurnBoundary.IsMatch(text)) return u;   // fin ou début de tour : rien pendant les coups
            u.Effect = "déclencheur";
            u.EffectOnDamage = true;
            u.Reactive = !text.Contains("ce serviteur", StringComparison.OrdinalIgnoreCase);
        }
        else if (m.Keywords.Contains("ENRAGED")) { u.Effect = "effet quand il est blessé"; u.EffectOnDamage = true; }
        else if (m.Keywords.Contains("DEATHRATTLE")) u.Effect = "Râle d'agonie";
        else if (m.Keywords.Contains("REBORN")) u.Effect = "Réincarnation";
        else if (m.Keywords.Contains("AURA")) u.Effect = "aura";
        return u;
    }

    private static void Classify(HandCard card, HandCardView c)
    {
        var text = (c.Text ?? "").Trim();
        card.MayDefend = MayDefend.IsMatch(text);
        if (CardRules.IsCoin(c.CardId)) { card.Kind = CardKind.Coin; return; }
        if (c.CardId is Depths or DepthsOriginal) { card.Kind = CardKind.DepthsCard; return; }
        if (c.CardId == Chronolord) { card.Kind = CardKind.Chronolord; return; }
        var keywords = LeadingKeywords.Match(text).Value;
        var rest = text[keywords.Length..].Trim();
        var neutral = rest.Length == 0 || Regex.Split(rest, @"(?<=[.!])\s+").Where(x => x.Length > 0).All(x => BoardNeutral.IsMatch(x));
        if (card.Type == "MINION" && (neutral || c.CardId == BroodKeeper))
        {
            card.Kind = CardKind.Body;
            card.Taunt = keywords.Contains("Provocation", StringComparison.OrdinalIgnoreCase);
            card.Shield = keywords.Contains("Bouclier divin", StringComparison.OrdinalIgnoreCase);
            card.Poison = keywords.Contains("Toxicité", StringComparison.OrdinalIgnoreCase);
            card.Lifesteal = keywords.Contains("Vol de vie", StringComparison.OrdinalIgnoreCase);
            card.RushOrCharge = keywords.Contains("Ruée", StringComparison.OrdinalIgnoreCase) || keywords.Contains("Charge", StringComparison.OrdinalIgnoreCase);
            return;
        }
        if (card.Type == "SPELL" && rest.Length > 0 && neutral && card.Targets.Count == 0) { card.Kind = CardKind.Neutral; return; }
        if (ThreatAssessment.Gift.IsMatch(text)) card.WhyNot = $"{card.Name} : l'adversaire en profite aussi (texte), danger non chiffré";
        if (card.Type == "WEAPON" && KeywordsOnly.IsMatch(text))
        {
            card.Kind = CardKind.Weapon;
            card.Lifesteal = text.Contains("Vol de vie", StringComparison.OrdinalIgnoreCase);
            return;
        }
        if (card.Type == "MINION" && DestroyByAttack.IsMatch(text) && card.Targets.Count > 0) { card.Kind = CardKind.Destroy; return; }
        // Dégâts directs ciblés : seul le premier effet compte, le reste doit être sans effet sur le plateau (pioche, Découverte…).
        if (card.Targets.Count > 0 && card.Type is "SPELL" or "MINION" && DamageFirst.Match(text) is { Success: true } dm)
        {
            var afterDamage = text[dm.Length..].Trim();
            var armorIfDies = 0;
            foreach (var sentence in Regex.Split(afterDamage, @"(?<=\.)\s+").Where(x => x.Length > 0))
            {
                if (ArmorIfDies.Match(sentence) is { Success: true } ad) armorIfDies = int.Parse(ad.Groups[1].Value);
                else if (!Harmless.IsMatch(sentence)) return;   // effet supplémentaire non simulé
            }
            card.Kind = CardKind.Damage;
            card.Damage = int.Parse(dm.Groups[1].Value);
            card.Armor = dm.Groups[2].Success ? int.Parse(dm.Groups[2].Value) : 0;
            card.ArmorIfDies = armorIfDies;
        }
    }

    // ------------------------------------------------------------------ Mesure

    /// <summary>Même calcul que <see cref="ThreatAssessment.Assess"/> : PV + armure + Provocations absorbées − dégâts visibles.</summary>
    private static int Margin(Sim s)
    {
        if (s.Won) return WonMargin;
        var minion = s.Theirs.Where(u => u.Hp > 0).Sum(u => u.Atk * u.Mult);
        var attack = minion + s.EnemyWeapon;
        var taunt = s.Mine.Where(u => u.Hp > 0 && u.Taunt).Sum(u => u.Hp);
        var visible = attack + s.EnemyPower + s.Theirs.Where(u => u.Hp > 0).Sum(u => u.EndTurn);
        return s.Hp + s.Armor + Math.Min(attack, taunt) - visible;
    }

    private static IEnumerable<string> UnmodeledDefenses(Info info, Sim s) =>
        info.Cards.Values.Where(c => c.Kind == CardKind.Unmodeled && c.MayDefend && !s.Played.Contains(c.Id) && c.Cost <= s.Mana)
            .Select(c => $"{c.Name} : effet non simulé qui pourrait défendre");

    // ------------------------------------------------------------------ Recherche

    private static Found Search(Info info, Sim s, Dictionary<string, Found> memo, ref int nodes, ref bool truncated)
    {
        var best = new Found(Margin(s), new());
        if (s.Won) return best;
        if (nodes >= NodeCap) { truncated = true; return best; }
        var key = s.Key();
        if (memo.TryGetValue(key, out var known)) return known;
        nodes++;
        foreach (var move in DefensiveMoves(info, s))
        {
            var (child, reasons) = Apply(info, s, move);
            var r = Search(info, child, memo, ref nodes, ref truncated);
            if (r.Margin > best.Margin || (r.Margin == best.Margin && r.Line.Count + 1 < best.Line.Count))
                best = new Found(r.Margin, new List<LineStep> { new(move.Label, reasons) }.Concat(r.Line).ToList());
        }
        memo[key] = best;
        return best;
    }

    /// <summary>Coups qui peuvent réduire le danger : on n'explore ni les attaques au visage, ni les cartes sans effet défensif simulé.</summary>
    private static IEnumerable<Move> DefensiveMoves(Info info, Sim s)
    {
        var alive = s.Theirs.Where(u => u.Hp > 0 && !u.Untouchable).ToList();
        var threats = alive.Where(u => u.Atk * u.Mult > 0 || u.EndTurn > 0).ToList();
        if (threats.Count == 0) yield break;
        var taunts = alive.Where(u => u.Taunt && !u.Stealth).ToList();
        var attackable = taunts.Count > 0 ? taunts : threats.Where(u => !u.Stealth).ToList();

        foreach (var a in s.Mine.Where(u => u.Hp > 0 && u.AttacksLeft > 0 && u.Atk > 0))
            foreach (var t in attackable)
                yield return new Move("attack", a.Id, t.Id, $"Attaquer avec {a.Name} → {t.Name} {t.Atk}/{t.Hp}");
        if (s.HeroAttacksLeft > 0 && s.HeroAtk > 0)
            foreach (var t in attackable)
                yield return new Move("attack", info.HeroId, t.Id, $"Attaquer avec ton héros → {t.Name} {t.Atk}/{t.Hp}");

        var targets = threats.Concat(taunts).Select(u => u.Id).ToHashSet();
        var slots = s.Mine.Count(u => u.Hp > 0) < 7;
        foreach (var c in info.Cards.Values.Where(c => !s.Played.Contains(c.Id) && c.Cost <= s.Mana))
        {
            switch (c.Kind)
            {
                case CardKind.Damage or CardKind.Destroy when c.Type != "MINION" || slots:
                    foreach (var t in s.Theirs.Where(u => u.Hp > 0 && targets.Contains(u.Id) && c.Targets.Contains(u.Id)))
                        if (c.Kind == CardKind.Damage || t.Atk <= c.Atk)
                            yield return new Move("card", c.Id, t.Id, $"Jouer {c.Name} → {t.Name} {t.Atk}/{t.Hp}");
                    break;
                case CardKind.Body when c.Taunt && slots:
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name} (Provocation {c.Hp} PV)");
                    break;
                case CardKind.Body when c.Sword && s.HeroCanArm && slots:
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name} (épée 2/2)");
                    break;
                case CardKind.Chronolord when slots && s.Theirs.Any(u => u.Hp > 0 && info.PlayedLastTurn.Contains(u.Id)):
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name} (détruit les serviteurs joués au tour précédent)");
                    break;
                case CardKind.Weapon when s.HeroCanArm:
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name}");
                    break;
                case CardKind.Coin:
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name}");
                    break;
                case CardKind.DepthsCard when !s.DepthsReady:
                    yield return new Move("card", c.Id, 0, $"Jouer {c.Name}");
                    break;
            }
        }
        if (s.PowerReady && s.PowerArmor > 0 && s.PowerCost <= s.Mana) yield return new Move("power", 0, 0, $"Pouvoir héroïque : +{s.PowerArmor} armure");
        if (s.DepthsReady)
        {
            foreach (var u in s.Mine.Where(u => u.Hp > 1 && u.AttacksLeft > 0 && (s.DepthsAnyTarget || info.DepthsTargets.Contains(u.Id))))
                yield return new Move("depths", 0, u.Id, $"Activer Profondeurs Sanguines → {u.Name}");
            foreach (var t in threats.Where(u => u.Hp == 1 && !u.Shield && (s.DepthsAnyTarget || info.DepthsTargets.Contains(u.Id))))
                yield return new Move("depths", 0, t.Id, $"Activer Profondeurs Sanguines → {t.Name} {t.Atk}/{t.Hp}");
        }
    }

    /// <summary>Traduit une action légale en coup simulé, ou null (avec la raison) si son effet n'est pas modélisé.</summary>
    private static Move? FromLegal(Info info, Sim s, LegalAction a, out string? whyNot)
    {
        whyNot = null;
        switch (a.Kind)
        {
            case "end-turn":
                return new Move("end", 0, 0, a.Label);
            case "attack" when a.SourceEntityId is int src && a.TargetEntityId is int tgt:
                var attackerKnown = src == info.HeroId || s.Mine.Any(u => u.Id == src);
                var targetKnown = tgt == info.EnemyHeroId || s.Theirs.Any(u => u.Id == tgt);
                if (attackerKnown && targetKnown) return new Move("attack", src, tgt, a.Label);
                whyNot = "attaque hors du plateau simulé";
                return null;
            case "play" when a.SourceEntityId is int card && info.Cards.TryGetValue(card, out var c):
                if (c.Kind == CardKind.Unmodeled) { whyNot = c.WhyNot ?? $"{c.Name} : effet non simulé"; return null; }
                if (c.Kind is CardKind.Damage or CardKind.Destroy && a.TargetEntityId is int t && t != info.EnemyHeroId && !s.Theirs.Any(u => u.Id == t))
                { whyNot = $"{c.Name} sur ton propre camp : non simulé"; return null; }
                return new Move("card", card, a.TargetEntityId ?? 0, a.Label);
            case "hero-power":
                if (s.PowerArmor > 0) return new Move("power", 0, 0, a.Label);
                whyNot = "pouvoir héroïque non simulé";
                return null;
            case "location" when a.SourceEntityId == info.DepthsEntity && info.DepthsEntity != 0 && a.TargetEntityId is int dt:
                return new Move("depths", 0, dt, a.Label);
            default:
                whyNot = $"{a.Label} : coup non simulé";
                return null;
        }
    }

    // ------------------------------------------------------------------ Effets

    private static (Sim Child, List<string> Unmodeled) Apply(Info info, Sim parent, Move m)
    {
        var s = parent.Clone();
        var why = new List<string>();
        if (m.Kind != "end" && !(m.Kind == "card" && info.Cards[m.Source].Kind == CardKind.Coin))
            foreach (var r in s.Mine.Concat(s.Theirs).Where(u => u.Reactive && u.Hp > 0 && u.Id != m.Source && u.Id != m.Target))
                why.Add($"{r.Name} peut réagir : déclencheur non simulé");
        switch (m.Kind)
        {
            case "attack":
                Attack(info, s, m.Source, m.Target, why);
                break;
            case "card":
            {
                var c = info.Cards[m.Source];
                s.Played.Add(c.Id);
                s.Mana -= c.Cost;
                if (info.Secret && c.Kind != CardKind.Coin) why.Add("Secret adverse inconnu : jouer une carte peut le déclencher");
                switch (c.Kind)
                {
                    case CardKind.Coin:
                        s.Mana += 1;
                        break;
                    case CardKind.DepthsCard:
                        s.DepthsReady = true;
                        s.DepthsAnyTarget = true;
                        break;
                    case CardKind.Weapon:
                        s.HeroAtk = s.HeroAtk - s.WeaponAtk + c.Atk;
                        s.WeaponAtk = c.Atk;
                        s.WeaponDur = c.Hp;
                        s.WeaponLifesteal = c.Lifesteal;
                        s.WeaponEffect = null;
                        if (s.HeroCanArm) s.HeroAttacksLeft = Math.Max(s.HeroAttacksLeft, 1);
                        break;
                    case CardKind.Body:
                        s.Mine.Add(new Unit { Id = c.Id, Name = c.Name, Atk = c.Atk, Hp = c.Hp, Taunt = c.Taunt, Shield = c.Shield, Poison = c.Poison, Lifesteal = c.Lifesteal });
                        if (c.RushOrCharge) why.Add($"{c.Name} peut attaquer ce tour (Ruée ou Charge) : attaque non simulée");
                        if (c.Sword && s.HeroCanArm)
                        {
                            s.HeroAtk = s.HeroAtk - s.WeaponAtk + 2;
                            s.WeaponAtk = 2;
                            s.WeaponDur = 2;
                            s.WeaponLifesteal = false;
                            s.WeaponEffect = null;
                            s.HeroAttacksLeft = Math.Max(s.HeroAttacksLeft, 1);
                        }
                        break;
                    case CardKind.Neutral:
                        break;
                    case CardKind.Chronolord:
                        foreach (var u in s.Theirs.Where(u => info.PlayedLastTurn.Contains(u.Id))) u.Hp = 0;
                        s.Mine.Add(new Unit { Id = c.Id, Name = c.Name, Atk = c.Atk, Hp = c.Hp });
                        break;
                    case CardKind.Damage:
                    {
                        var dmg = c.Damage + (c.Type == "SPELL" ? s.SpellPower : 0);
                        if (m.Target == info.EnemyHeroId) { s.EnemyHp -= dmg; if (s.EnemyHp <= 0) s.Won = true; }
                        else if (s.Theirs.FirstOrDefault(u => u.Id == m.Target && u.Hp > 0) is { } t)
                        {
                            Hit(t, dmg, poison: false, why);
                            if (t.Hp <= 0 && c.ArmorIfDies > 0) s.Armor += c.ArmorIfDies;
                        }
                        s.Armor += c.Armor;
                        if (c.Type == "MINION") s.Mine.Add(new Unit { Id = c.Id, Name = c.Name, Atk = c.Atk, Hp = c.Hp });
                        break;
                    }
                    case CardKind.Destroy:
                        if (s.Theirs.FirstOrDefault(u => u.Id == m.Target && u.Hp > 0) is { } d && d.Atk <= c.Atk) d.Hp = 0;
                        s.Mine.Add(new Unit { Id = c.Id, Name = c.Name, Atk = c.Atk, Hp = c.Hp });
                        break;
                }
                break;
            }
            case "power":
                s.Mana -= s.PowerCost;
                s.Armor += s.PowerArmor;
                s.PowerReady = false;
                break;
            case "depths":
            {
                s.DepthsReady = false;
                var u = s.Mine.Concat(s.Theirs).FirstOrDefault(x => x.Id == m.Target && x.Hp > 0);
                if (u is not null)
                {
                    Hit(u, 1, poison: false, why);
                    if (u.Hp > 0) u.Atk += 2;
                }
                break;
            }
            case "end":
                break;
        }
        Deaths(s, why);
        return (s, why);
    }

    private static void Attack(Info info, Sim s, int source, int target, List<string> why)
    {
        var heroAttacks = source == info.HeroId;
        var a = heroAttacks ? null : s.Mine.FirstOrDefault(u => u.Id == source && u.Hp > 0);
        if (!heroAttacks && a is null) return;
        var atk = heroAttacks ? s.HeroAtk : a!.Atk;
        if (info.Secret) why.Add("Secret adverse inconnu : une attaque peut le déclencher");
        if (a?.Effect is { } e && a.EffectOnDamage) why.Add($"{a.Name} : {e} non simulé");
        if (heroAttacks && s.WeaponEffect is { } we) why.Add(we);
        var lifesteal = heroAttacks ? s.WeaponLifesteal && s.WeaponDur > 0 : a!.Lifesteal;
        if (target == info.EnemyHeroId)
        {
            s.EnemyHp -= atk;
            if (lifesteal) s.Hp = Math.Min(s.MaxHp, s.Hp + atk);
            if (s.EnemyHp <= 0) s.Won = true;
        }
        else if (s.Theirs.FirstOrDefault(u => u.Id == target && u.Hp > 0) is { } t)
        {
            var dealt = Hit(t, atk, a?.Poison == true, why);
            if (lifesteal) s.Hp = Math.Min(s.MaxHp, s.Hp + dealt);
            if (heroAttacks)
            {
                var taken = t.Atk;
                var absorbed = Math.Min(s.Armor, taken);
                s.Armor -= absorbed;
                s.Hp -= taken - absorbed;
            }
            else Hit(a!, t.Atk, t.Poison, why);
        }
        if (heroAttacks)
        {
            s.HeroAttacksLeft--;
            s.HeroCanArm = false;
            if (s.WeaponDur > 0 && --s.WeaponDur == 0) { s.HeroAtk = Math.Max(0, s.HeroAtk - s.WeaponAtk); s.WeaponAtk = 0; s.WeaponLifesteal = false; s.WeaponEffect = null; }
        }
        else a!.AttacksLeft--;
    }

    /// <summary>Inflige des dégâts à un serviteur ; renvoie les dégâts réellement subis (0 si le Bouclier divin les absorbe).</summary>
    private static int Hit(Unit u, int damage, bool poison, List<string> why)
    {
        if (damage <= 0) return 0;
        if (u.Shield) { u.Shield = false; return 0; }
        u.Hp -= damage;
        if (poison && u.Hp > 0) u.Hp = 0;
        if (u.Effect is { } e && u.EffectOnDamage) why.Add($"{u.Name} : {e} non simulé");
        return damage;
    }

    private static void Deaths(Sim s, List<string> why)
    {
        if (s.Mine.Concat(s.Theirs).Any(u => u.Hp <= 0))
            foreach (var a in s.Mine.Concat(s.Theirs).Where(u => u.Aura && u.Hp > 0))
                why.Add($"{a.Name} : aura non simulée après une mort");
        foreach (var u in s.Mine.Concat(s.Theirs).Where(u => u.Hp <= 0 && u.Effect is not null && !u.EffectOnDamage))
            why.Add($"{u.Name} meurt : {u.Effect} non simulé");
        s.Mine.RemoveAll(u => u.Hp <= 0);
        s.Theirs.RemoveAll(u => u.Hp <= 0);
    }
}
