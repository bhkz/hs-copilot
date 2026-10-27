using System.Text;
using System.Text.RegularExpressions;
using HsCopilot.Core.Evaluation;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>Une étape d'une séquence calculée par le solveur.</summary>
public sealed record SolverStep
{
    /// <summary>play, location, attack.</summary>
    public required string Kind { get; init; }
    public required string Label { get; init; }
    public int? SourceEntityId { get; init; }
    public int? TargetEntityId { get; init; }
    /// <summary>Dégâts au héros adverse apportés par l'étape elle-même.</summary>
    public int FaceDamage { get; init; }
    /// <summary>Action légale correspondante dans l'état analysé (null pour une étape qui n'existe qu'après une autre, ex. activer un lieu joué ce tour).</summary>
    public LegalAction? Action { get; init; }
}

/// <summary>Résultat du solveur pour l'état courant.</summary>
public sealed class SolverResult
{
    public int Needed { get; init; }
    /// <summary>Dégâts au visage atteignables de façon déterministe ce tour (mécaniques simulées uniquement).</summary>
    public int MaxFaceDamage { get; init; }
    /// <summary>Dégâts des seules attaques disponibles maintenant, sans préparation.</summary>
    public int AttacksOnlyDamage { get; init; }
    public List<SolverStep> Sequence { get; init; } = new();
    /// <summary>Séquence du létal conditionnel par l'arme aléatoire (vide sinon).</summary>
    public List<SolverStep> ConditionalSequence { get; init; } = new();
    /// <summary>Dégâts si les Provocations adverses étaient retirées (létal plausible), 0 sinon.</summary>
    public int DamageIfFaceOpen { get; init; }
    /// <summary>Temps de calcul.</summary>
    public double ElapsedMs { get; init; }
    /// <summary>certain, conditionnel, plausible ou aucun (voir <see cref="LethalLevels"/>).</summary>
    public string Level { get; init; } = LethalLevels.None;
    public List<string> Conditions { get; init; } = new();
    /// <summary>Cartes en main dont l'effet n'est pas simulé et qui pourraient changer le calcul.</summary>
    public List<string> Unmodeled { get; init; } = new();
    /// <summary>Améliorations d'ordre détectées (ex. Profondeurs Sanguines sur un serviteur avant son attaque).</summary>
    public List<string> OrderingHints { get; init; } = new();
    /// <summary>Lignes létales trop improbables pour être annoncées (ex. arme aléatoire d'au moins 6 ATQ).</summary>
    public List<string> LongShots { get; init; } = new();
    public bool FaceBlockedByTaunt { get; init; }

    public string SequenceText => string.Join(", puis ", Sequence.Select(s => s.Label));
}

/// <summary>
/// Solveur déterministe des dégâts au visage pour le tour en cours, d'abord pour le Guerrier Dragons. Il explore les
/// séquences de cartes jouables avec le mana disponible et retient la meilleure, puis ajoute les attaques.
/// </summary>
/// <remarks>
/// Mécaniques simulées : attaques autorisées par le serveur (Furie des vents), attaque du héros avec son arme, sorts et
/// cris de guerre « Inflige N points de dégâts » (bonus des sorts compris, Voyante blessée), serviteurs avec Charge
/// (Grommash +6 ATQ s'il est blessé), Profondeurs Sanguines (+2 ATQ et 1 dégât, une fois par tour, jouable puis activable),
/// Forgeron dimensionnel (+2 ATQ aux serviteurs en main), Gardien des couvées (épée 2/2 avec un Dragon en main),
/// Chronoseigneur Époque (détruit les serviteurs joués au tour précédent, Provocations comprises), Provocations à 1 PV
/// détruites par Profondeurs, dégâts de fin de tour au héros (Drake terrestre, même derrière une Provocation). Non simulé : échanges contre des Provocations, effets aléatoires (arme d'Animatrice de stade
/// traitée comme condition), Secrets (rendent un létal conditionnel), cartes hors de ce modèle (signalées).
/// </remarks>
public static class TurnSolver
{
    private const string Depths = "CORE_REV_990";
    private const string DepthsOriginal = "REV_990";
    private const string Seer = "END_022";
    private const string Grommash = "CORE_EX1_414";
    private const string Smith = "END_021";
    private const string BroodKeeper = "EDR_457";
    private const string StageHand = "TIME_034";
    private const string Chronolord = "TIME_714";
    /// <summary>Dragons du deck (repli quand la race n'est pas connue).</summary>
    private static readonly HashSet<string> KnownDragons = new() { "END_022", "EDR_889", "CORE_EX1_189", "TIME_003", "TIME_045", "END_021", "TIME_034", "TIME_714", "END_033", "TLC_600" };
    private static readonly Regex DirectDamage = new(@"^(?:Cri de guerre\s*:\s*)?Inflige (\d+) points? de dégâts", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>« À la fin de votre tour, inflige N points de dégâts au héros adverse » (Drake terrestre).</summary>
    private static readonly Regex EndOfTurnFace = new(@"fin de votre tour\W{0,4}inflige (\d+) points? de dégâts au héros", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>Serviteur avec Charge qui ne peut pas viser les héros.</summary>
    /// <summary>
    /// Ce qui peut dégager les Provocations autrement que point par point : Silence, destruction, transformation, renvoi,
    /// Toxicité, dégâts sur plusieurs cibles (Le Grand Dracorex : « inflige des dégâts à TOUS les autres serviteurs adverses »)
    /// ou aléatoires, arme aléatoire (Animatrice de stade). Une pioche ou une Découverte n'en fait pas partie : comme partout
    /// dans ce calcul, les cartes à venir ne sont pas comptées.
    /// </summary>
    private static readonly Regex TauntRemoval = new(@"silence|détrui|transform|renvoie|perd (?:sa |la )?Provocation|Toxicité|Venimeu|tous les|à tous|adjacent|voisin|chaque serviteur|répartis|aléatoirement|cibles? aléatoires?|serviteurs? adverses? aléatoires?|arme aléatoire", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NoHeroAttack = new(@"ne peut pas attaquer (les|de) héros", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>
    /// Effets adverses qui peuvent empêcher ou réduire des dégâts (Insensible, « subirait des dégâts », « à la place »…) :
    /// non simulés, ils interdisent un létal « certain ».
    /// </summary>
    private static readonly Regex DamagePrevention = new(@"Insensible|ne peut pas (subir|être blessé)|subirai(t|ent) des dégâts|devrai(t|ent) subir des dégâts|subit des dégâts.{0,40}à la place|à la place de (votre|son) héros|ne subit que|réduit les dégâts", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>Provocation dont la mort peut invoquer quelque chose ou la ramener (Râle d'agonie, Réincarnation).</summary>
    private static readonly Regex AfterDeath = new(@"Râle d’agonie|Râle d'agonie|Réincarnation", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    /// <summary>Enchantement du Sombre don « Somnambule » : la carte en main a Charge (vu dans les logs : EDR_100t6e).</summary>
    private const string ChargeGiftEnchantment = "EDR_100t6e";
    private static readonly Regex DamageWords = new(@"dégâts|Charge|arme|ATQ", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const int MaxRandomWeaponAttack = 8;
    /// <summary>
    /// ATQ de base des 234 armes à collectionner de la base de cartes 255130 (toutes extensions) : 55 à 1, 70 à 2, 61 à 3,
    /// 31 à 4, 13 à 5, 1 à 6, 3 à 7. Sert seulement à estimer une chance : la réserve exacte du tirage n'est pas connue.
    /// </summary>
    private static readonly int[] WeaponAttackCounts = { 0, 55, 70, 61, 31, 13, 1, 3 };
    /// <summary>En dessous de cette chance estimée (Rembobinage compris), le létal par l'arme aléatoire n'est pas annoncé.</summary>
    private const double MinRandomLethalChance = 0.5;

    private sealed class Minion
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public string? CardId { get; init; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public bool Damaged { get; set; }
        public int AttacksLeft { get; set; }
        public bool CanFace { get; set; }
        public bool DivineShield { get; init; }
        /// <summary>Ruée posée ce tour : ne peut viser que des serviteurs, même sans Provocation.</summary>
        public bool RushOnly { get; init; }
        /// <summary>Bonus des sorts d'une Voyante blessée pendant la séquence (ou blessée sans tag du serveur).</summary>
        public int SeerBonus { get; set; }
        /// <summary>Dégâts au héros adverse à la fin du tour (Drake terrestre).</summary>
        public int EndTurnFace { get; init; }
        public Minion Copy() => (Minion)MemberwiseClone();
        public int EffectiveAttack => Attack + (CardId == Grommash && Damaged ? 6 : 0);
    }

    private sealed class Card
    {
        public required int Index { get; init; }
        public required int EntityId { get; init; }
        public required string Name { get; init; }
        public string? CardId { get; init; }
        public string? Type { get; init; }
        public int Cost { get; init; }
        public int Attack { get; init; }
        public int Health { get; init; }
        public string Text { get; init; } = "";
        public bool IsDragon { get; init; }
        /// <summary>Charge donnée par un Sombre don (enchantement attaché par le serveur).</summary>
        public bool ChargeGift { get; init; }
        /// <summary>Condition remplie selon le client (POWERED_UP), null si inconnu.</summary>
        public bool? PoweredUp { get; init; }
        public int EndTurnFace => EndTurn(Text);
    }

    private static int EndTurn(string? text) => text is not null && EndOfTurnFace.Match(text) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;

    private sealed class Taunt
    {
        public required int Id { get; init; }
        public required string Name { get; init; }
        public int Health { get; init; }
        public bool DivineShield { get; init; }
        public bool PlayedLastTurn { get; init; }
        /// <summary>Raison pour laquelle sa destruction n'ouvre pas sûrement la voie (Râle d'agonie, Réincarnation), null sinon.</summary>
        public string? Risk { get; init; }
    }

    private sealed class State
    {
        public int Mana;
        public int Slots;             // places libres sur le plateau (serviteurs et lieux)
        public int Played;            // masque des cartes jouées
        public int SmithBonus;        // +ATQ des serviteurs encore en main
        public bool DepthsReady;      // lieu en jeu et activable
        public bool DepthsUsed;
        public int HeroAttack;
        public bool HeroCanAttack;
        public bool RandomWeapon;
        public int Damage;
        public List<Minion> Minions = new();
        public List<Taunt> Taunts = new();
        public List<SolverStep> Steps = new();
        /// <summary>Hypothèses non simulées sur lesquelles repose la séquence (vide : séquence démontrée).</summary>
        public List<string> Risks = new();

        public State Copy() => new()
        {
            Mana = Mana, Slots = Slots, Played = Played, SmithBonus = SmithBonus, DepthsReady = DepthsReady, DepthsUsed = DepthsUsed,
            HeroAttack = HeroAttack, HeroCanAttack = HeroCanAttack, RandomWeapon = RandomWeapon, Damage = Damage,
            Minions = Minions.Select(m => m.Copy()).ToList(), Taunts = Taunts.ToList(), Steps = Steps.ToList(), Risks = Risks.ToList(),
        };

        public int SpellPower(int basePower) => basePower + Minions.Sum(m => m.SeerBonus);

        public string Key() =>
            $"{Mana}|{Slots}|{Played}|{SmithBonus}|{DepthsReady}|{DepthsUsed}|{HeroAttack}|{HeroCanAttack}|{RandomWeapon}|{Damage}|" +
            string.Join(",", Minions.Select(m => $"{m.Id}:{m.Attack}:{m.Health}:{m.Damaged}:{m.AttacksLeft}:{m.CanFace}")) + "|" +
            string.Join(",", Taunts.Select(t => t.Id)) + "|" + Risks.Count;
    }

    public static SolverResult Solve(DecisionContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = ctx.State;
        if (ctx.Kind != DecisionContext.KindAction || s.Opponent?.Hero is not { } oppHero || s.Local is not { } me)
            return new SolverResult();
        var needed = oppHero.Health + oppHero.Armor;
        var actions = ctx.LegalActions;
        var oppHeroId = oppHero.EntityId;

        // Provocations adverses.
        var lastTurnIds = s.Opponent.LastTurn?.MinionsPlayed.Select(m => m.EntityId).ToHashSet() ?? new HashSet<int>();
        var taunts = s.Opponent.Board.Where(m => m.Type == "MINION" && m.Keywords.Contains("TAUNT"))
            .Select(m => new Taunt
            {
                Id = m.EntityId,
                Name = m.Name ?? "?",
                Health = m.Health,
                DivineShield = m.Keywords.Contains("DIVINE_SHIELD"),
                PlayedLastTurn = lastTurnIds.Contains(m.EntityId),
                Risk = m.Keywords.Contains("DEATHRATTLE") || m.Keywords.Contains("REBORN") || AfterDeath.IsMatch(m.Text ?? "")
                    ? $"{m.Name ?? "la Provocation"} a un Râle d'agonie ou une Réincarnation : sa mort peut laisser une Provocation, non simulé"
                    : null,
            })
            .ToList();
        // Le serveur autorise une attaque contre le héros : aucune Provocation ne bloque (Camouflage, etc.).
        if (actions.Any(a => a.Kind == "attack" && a.TargetEntityId == oppHeroId)) taunts.Clear();

        // Bonus des sorts déjà en jeu (tags du serveur ; une Voyante blessée l'a déjà).
        var basePower = me.Board.Sum(m => m.Keywords.Where(k => k.StartsWith("SPELLPOWER+", StringComparison.Ordinal))
            .Sum(k => int.TryParse(k["SPELLPOWER+".Length..], out var v) ? v : 0));

        var start = new State
        {
            Mana = me.Mana.Available,
            Slots = Math.Max(0, 7 - me.Board.Count),
            HeroAttack = me.Hero?.Attack ?? 0,
            HeroCanAttack = taunts.Count == 0
                ? actions.Any(a => a.Kind == "attack" && a.SourceEntityId == me.Hero?.EntityId && a.TargetEntityId == oppHeroId)
                : actions.Any(a => a.Kind == "attack" && a.SourceEntityId == me.Hero?.EntityId),
            Taunts = taunts,
        };
        var heroFresh = me.Hero is { } h && h.AttacksThisTurn == 0 && !h.Frozen;
        foreach (var m in me.Board.Where(m => m.Type == "MINION"))
        {
            var attacks = actions.Where(a => a.Kind == "attack" && a.SourceEntityId == m.EntityId).ToList();
            var windfury = m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1;
            start.Minions.Add(new Minion
            {
                Id = m.EntityId,
                Name = m.Name ?? "?",
                CardId = m.CardId,
                Attack = m.Attack - (m.CardId == Grommash && m.Health < m.MaxHealth ? 6 : 0),
                Health = m.Health,
                Damaged = m.Health < m.MaxHealth,
                AttacksLeft = attacks.Count == 0 ? 0 : Math.Max(1, windfury - m.State.AttacksThisTurn),
                CanFace = attacks.Any(a => a.TargetEntityId == oppHeroId),
                DivineShield = m.Keywords.Contains("DIVINE_SHIELD"),
                RushOnly = m.State.JustPlayed && m.Keywords.Contains("RUSH") && !m.Keywords.Contains("CHARGE"),
                EndTurnFace = m.State.Dormant || m.Keywords.Contains("SILENCED") ? 0 : EndTurn(m.Text),
                SeerBonus = m.CardId == Seer && !m.Keywords.Contains("SILENCED") && m.Health < m.MaxHealth && !m.Keywords.Any(k => k.StartsWith("SPELLPOWER", StringComparison.Ordinal)) ? 2 : 0,
            });
        }
        var depthsOnBoard = me.Board.FirstOrDefault(m => m.CardId is Depths or DepthsOriginal);
        start.DepthsReady = depthsOnBoard is not null && actions.Any(a => a.Kind == "location" && a.SourceEntityId == depthsOnBoard.EntityId);

        var cards = new List<Card>();
        foreach (var c in me.Hand.Cards)
        {
            if (c.CardId is null || cards.Count >= 30) continue;
            cards.Add(new Card
            {
                Index = cards.Count,
                EntityId = c.EntityId,
                Name = c.Name ?? c.CardId,
                CardId = c.CardId,
                Type = c.Type,
                Cost = c.Cost ?? 99,
                Attack = c.Attack ?? 0,
                Health = c.Health ?? 0,
                Text = c.Text ?? "",
                IsDragon = (c.Races?.Contains("DRAGON") ?? false) || KnownDragons.Contains(c.CardId),
                ChargeGift = c.Enchantments.Any(e => e.CardId == ChargeGiftEnchantment),
                PoweredUp = c.PoweredUp,
            });
        }
        // Les cartes déjà jouables doivent l'être selon le serveur (coût, conditions) ; les autres ne sont pas tentées.
        var playableNow = actions.Where(a => a.Kind is "play" or "location").Select(a => a.SourceEntityId).ToHashSet();
        var unmodeled = new List<string>();
        foreach (var c in cards)
        {
            if (!playableNow.Contains(c.EntityId)) continue;
            if (!IsModeled(c) && DamageWords.IsMatch(c.Text)) unmodeled.Add(c.Name);
        }

        var ctxInfo = new Info(cards, playableNow, oppHeroId, basePower, heroFresh, me.Hero?.EntityId ?? 0, actions);
        var final = Finish(Search(start, ctxInfo, new Dictionary<string, State>()), ctxInfo);
        // Séquences qui reposent sur la mort d'une Provocation à Râle d'agonie ou Réincarnation : jamais « certaines ».
        State? risky = null;
        if (final.Damage < needed && taunts.Any(t => t.Risk is not null))
        {
            var permissive = ctxInfo with { AllowRisky = true };
            var r = Finish(Search(start, permissive, new Dictionary<string, State>()), permissive);
            if (r.Damage >= needed && r.Risks.Count > 0) risky = r;
        }

        // Effets adverses non simulés qui peuvent empêcher des dégâts.
        var blockers = s.Opponent.Board.Where(m => !m.Keywords.Contains("SILENCED") && DamagePrevention.IsMatch(m.Text ?? ""))
            .Select(m => m.Name ?? "?").ToList();
        if (s.Opponent.Weapon is { } ow && DamagePrevention.IsMatch(ow.Text ?? "")) blockers.Add(ow.Name ?? "arme adverse");

        var attacksOnly = start.Taunts.Count == 0
            ? start.Minions.Where(m => m.CanFace).Sum(m => m.EffectiveAttack * m.AttacksLeft) + (start.HeroCanAttack ? start.HeroAttack : 0)
            : 0;

        var conditions = new List<string>();
        // Tous les Secrets comptent : un Secret connu (Bloc de glace…) peut aussi empêcher le létal.
        var secrets = s.Opponent.Secrets.Count;
        var unknownSecrets = s.Opponent.Secrets.Count(x => !x.Known);
        var knownSecretNames = s.Opponent.Secrets.Where(x => x.Known).Select(x => x.Name ?? "Secret").ToList();
        var conditionalSequence = new List<SolverStep>();
        var ifFaceOpen = 0;
        string? tauntShortfall = null;
        string level;
        if (oppHero.Immune) level = LethalLevels.None;
        else if (final.Damage >= needed || risky is not null)
        {
            if (final.Damage < needed) final = risky!;
            level = secrets > 0 || blockers.Count > 0 || final.Risks.Count > 0 ? LethalLevels.Conditional : LethalLevels.Certain;
            if (unknownSecrets > 0) conditions.Add(unknownSecrets == 1 ? "un Secret adverse inconnu peut l'empêcher" : $"{unknownSecrets} Secrets adverses inconnus peuvent l'empêcher");
            if (knownSecretNames.Count > 0) conditions.Add($"Secret adverse en jeu ({string.Join(", ", knownSecretNames)}) : son effet n'est pas simulé");
            if (secrets > 0 && me.Hero is { } meHero && meHero.Health + meHero.Armor <= 3)
                conditions.Add($"ton héros n'a que {meHero.Health + meHero.Armor} PV : un Secret de dégâts, comme Piège explosif, peut te tuer avant");
            foreach (var b in blockers) conditions.Add($"{b} peut empêcher ou réduire des dégâts : effet adverse non simulé");
            conditions.AddRange(final.Risks.Distinct());
        }
        else if (RandomWeaponLine(start, ctxInfo, needed) is { } random && random.Chance >= MinRandomLethalChance)
        {
            level = LethalLevels.Conditional;
            conditions.Add(random.Text);
            conditionalSequence = random.Steps;
            conditions.Add("l'adversaire reçoit aussi une arme aléatoire, dont l'effet n'est pas simulé");
            if (secrets > 0) conditions.Add("un Secret adverse peut l'empêcher");
            foreach (var b in blockers) conditions.Add($"{b} peut empêcher ou réduire des dégâts : effet adverse non simulé");
        }
        else if (start.Taunts.Count > 0 && (ifFaceOpen = TotalIfFaceOpen(start, ctxInfo)) >= needed)
        {
            // Dégager le visage par des dégâts coûte au moins les PV des Provocations (un coup de plus par Bouclier divin) :
            // partie classée du 26/10/2026 à 0 h 16, « 20 dégâts si la Provocation tombe, contre 15 PV » derrière cinq
            // Provocations totalisant 22 PV. La borne ne vaut que si aucune carte connue ne peut les retirer autrement : à 23 h 26,
            // Le Grand Dracorex en a retiré deux d'une seule attaque et le létal a eu lieu.
            var tauntCost = start.Taunts.Sum(t => t.Health + (t.DivineShield ? 1 : 0));
            var otherRemoval = ctxInfo.Cards.Any(c => TauntRemoval.IsMatch(c.Text) || c.CardId == Chronolord)
                || me.Board.Any(m => m.Keywords.Contains("POISONOUS") || m.Keywords.Contains("VENOMOUS") || TauntRemoval.IsMatch(m.Text ?? ""))
                || TauntRemoval.IsMatch(me.Weapon?.Text ?? "");
            if (!otherRemoval && ifFaceOpen < needed + tauntCost)
            {
                level = LethalLevels.None;
                tauntShortfall = $"létal derrière {(start.Taunts.Count == 1 ? "la Provocation" : $"les {start.Taunts.Count} Provocations")} : {ifFaceOpen} dégâts au plus, il en faudrait {needed + tauntCost} ({needed} pour le héros, {tauntCost} pour retirer {(start.Taunts.Count == 1 ? "la Provocation" : "les Provocations")})";
            }
            else
            {
                level = LethalLevels.Plausible;
                conditions.Add(start.Taunts.Count == 1
                    ? $"il faut d'abord retirer la Provocation ({tauntCost} PV) par des échanges ou des effets, non simulés"
                    : $"il faut d'abord retirer les {start.Taunts.Count} Provocations ({tauntCost} PV au total) par des échanges ou des effets, non simulés");
            }
        }
        else level = LethalLevels.None;
        var longShots = new List<string>();
        if (tauntShortfall is not null) longShots.Add(tauntShortfall);
        if (level == LethalLevels.None && RandomWeaponLine(start, ctxInfo, needed) is { } unlikely) longShots.Add(unlikely.Text);

        var hints = new List<string>();
        foreach (var step in final.Steps.Where(x => x.Kind == "location"))
        {
            var target = start.Minions.FirstOrDefault(m => m.Id == step.TargetEntityId);
            if (target is not null && target.AttacksLeft > 0)
                hints.Add($"Profondeurs Sanguines sur {target.Name} avant son attaque : +2 dégâts");
        }

        return new SolverResult
        {
            Needed = needed,
            MaxFaceDamage = final.Damage,
            AttacksOnlyDamage = attacksOnly,
            Sequence = final.Steps,
            Level = level,
            Conditions = conditions,
            Unmodeled = unmodeled,
            OrderingHints = hints,
            LongShots = longShots,
            ConditionalSequence = conditionalSequence,
            DamageIfFaceOpen = level == LethalLevels.Plausible ? ifFaceOpen : 0,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            FaceBlockedByTaunt = start.Taunts.Count > 0,
        };
    }

    private sealed record Info(List<Card> Cards, HashSet<int?> PlayableNow, int OppHeroId, int BasePower, bool HeroFresh, int HeroId, List<LegalAction> Actions)
    {
        /// <summary>Autorise la destruction de Provocations à Râle d'agonie ou Réincarnation (séquence alors conditionnelle).</summary>
        public bool AllowRisky { get; init; }
        /// <summary>Tour suivant supposé : aucune option du serveur, cibles déduites du texte des cartes.</summary>
        public bool Hypothetical { get; init; }
    }

    /// <summary>« Inflige N points de dégâts » sans restriction aux serviteurs ni effet aléatoire (tour supposé seulement).</summary>
    private static readonly Regex MinionOnlyOrRandom = new(@"à un serviteur|aux serviteurs|à tous les|aléatoire|répartis|à chaque", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool CanHitFace(Info info, Card c) => info.Hypothetical
        ? DirectDamage.IsMatch(c.Text) && !MinionOnlyOrRandom.IsMatch(c.Text)
        : LegalFor(info, c.EntityId, info.OppHeroId) is not null;

    private static readonly Regex KindredDiscount = new(@"Parenté\W*coûte \((\d+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Réduction de Parenté d'une carte (Wyrm : 2), 0 sans Parenté.</summary>
    public static int KindredReduction(string? text) => text is not null && KindredDiscount.Match(text) is { Success: true } m ? int.Parse(m.Groups[1].Value) : 0;

    /// <summary>
    /// Potentiel du tour suivant si ton plateau survit et que tu ne joues plus rien ce tour-ci : mana du tour suivant, tous
    /// tes serviteurs prêts, ta main actuelle (sans la pioche), PV et Provocations adverses actuels. C'est une hypothèse,
    /// jamais un létal : l'adversaire joue entre-temps.
    /// </summary>
    public static SolverResult SolveNextTurn(DecisionContext ctx)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var s = ctx.State;
        if (ctx.Kind != DecisionContext.KindAction || s.Opponent?.Hero is not { } oppHero || s.Local is not { } me) return new SolverResult();
        var needed = oppHero.Health + oppHero.Armor;
        var taunts = s.Opponent.Board.Where(m => m.Type == "MINION" && m.Keywords.Contains("TAUNT") && !m.Keywords.Contains("STEALTH"))
            .Select(m => new Taunt { Id = m.EntityId, Name = m.Name ?? "?", Health = m.Health, DivineShield = m.Keywords.Contains("DIVINE_SHIELD") })
            .ToList();
        var weapon = me.Weapon is { Durability: > 0 } w ? w.Attack : 0;
        var start = new State
        {
            Mana = Math.Max(0, Math.Min(10, me.Mana.Max + 1) - me.Mana.OverloadOwed),
            Slots = Math.Max(0, 7 - me.Board.Count),
            HeroAttack = weapon,
            HeroCanAttack = weapon > 0 && taunts.Count == 0,
            Taunts = taunts,
        };
        foreach (var m in me.Board.Where(m => m.Type == "MINION" && !m.State.Dormant && !m.Keywords.Contains("CANT_ATTACK")))
        {
            var windfury = m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1;
            start.Minions.Add(new Minion
            {
                Id = m.EntityId,
                Name = m.Name ?? "?",
                CardId = m.CardId,
                Attack = m.Attack - (m.CardId == Grommash && m.Health < m.MaxHealth ? 6 : 0),
                Health = m.Health,
                Damaged = m.Health < m.MaxHealth,
                AttacksLeft = windfury,
                CanFace = taunts.Count == 0,
                DivineShield = m.Keywords.Contains("DIVINE_SHIELD"),
                EndTurnFace = m.Keywords.Contains("SILENCED") ? 0 : EndTurn(m.Text),
                SeerBonus = m.CardId == Seer && !m.Keywords.Contains("SILENCED") && m.Health < m.MaxHealth && !m.Keywords.Any(k => k.StartsWith("SPELLPOWER", StringComparison.Ordinal)) ? 2 : 0,
            });
        }
        // Profondeurs : prête au tour suivant sauf si activée ce tour-ci (recharge vérifiée sur les parties réelles).
        var depths = me.Board.FirstOrDefault(m => m.CardId is Depths or DepthsOriginal && m.Health > 0);
        start.DepthsReady = depths is not null && !s.History.Any(h => h.Kind == "location" && h.Player == "local" && h.GameTurn == s.Game.GameTurn && h.SourceEntityId == depths.EntityId);
        var basePower = me.Board.Sum(m => m.Keywords.Where(k => k.StartsWith("SPELLPOWER+", StringComparison.Ordinal))
            .Sum(k => int.TryParse(k["SPELLPOWER+".Length..], out var v) ? v : 0));
        var typesThisTurn = s.Local.ThisTurn?.TypesPlayed ?? new List<string>();
        var cards = new List<Card>();
        foreach (var c in me.Hand.Cards)
        {
            if (c.CardId is null || cards.Count >= 30) continue;
            var cost = c.Cost ?? 99;
            // Parenté : active au tour suivant seulement si un serviteur du même type a été joué ce tour-ci.
            if (KindredReduction(c.Text) is var red and > 0 && c.BaseCost is int baseCost)
                cost = c.Races?.Any(r => typesThisTurn.Contains(r)) == true ? Math.Max(0, baseCost - red) : baseCost;
            cards.Add(new Card
            {
                Index = cards.Count,
                EntityId = c.EntityId,
                Name = c.Name ?? c.CardId,
                CardId = c.CardId,
                Type = c.Type,
                Cost = cost,
                Attack = c.Attack ?? 0,
                Health = c.Health ?? 0,
                Text = c.Text ?? "",
                IsDragon = (c.Races?.Contains("DRAGON") ?? false) || KnownDragons.Contains(c.CardId),
                ChargeGift = c.Enchantments.Any(e => e.CardId == ChargeGiftEnchantment),
            });
        }
        var info = new Info(cards, cards.Select(c => (int?)c.EntityId).ToHashSet(), oppHero.EntityId, basePower, true, me.Hero?.EntityId ?? 0, new List<LegalAction>()) { Hypothetical = true };
        var final = Finish(Search(start, info, new Dictionary<string, State>()), info);
        return new SolverResult
        {
            Needed = needed,
            MaxFaceDamage = final.Damage,
            Sequence = final.Steps,
            Level = LethalLevels.None,
            ElapsedMs = sw.Elapsed.TotalMilliseconds,
            FaceBlockedByTaunt = taunts.Count > 0,
        };
    }

    private static bool IsModeled(Card c) =>
        c.CardId is Depths or DepthsOriginal or Seer or Smith or BroodKeeper or StageHand or Chronolord || DirectDamage.IsMatch(c.Text) || IsCharge(c) || c.EndTurnFace > 0;

    private static bool IsCharge(Card c) =>
        c.Type == "MINION" && (c.Text.TrimStart().StartsWith("Charge", StringComparison.Ordinal) || c.ChargeGift) && !NoHeroAttack.IsMatch(c.Text);

    private static bool Played(State st, Card c) => (st.Played & (1 << c.Index)) != 0;

    /// <summary>Exploration en profondeur des préparations (cartes, Profondeurs) ; les attaques sont ajoutées à la fin.</summary>
    private static State Search(State st, Info info, Dictionary<string, State> memo)
    {
        var key = st.Key();
        if (memo.TryGetValue(key, out var cached)) return cached;
        var best = st;
        var bestValue = Finish(st, info).Damage;
        foreach (var next in Moves(st, info))
        {
            var result = Search(next, info, memo);
            var value = Finish(result, info).Damage;
            if (value > bestValue || (value == bestValue && (result.Risks.Count < best.Risks.Count
                || (result.Risks.Count == best.Risks.Count && result.Steps.Count < best.Steps.Count))))
            {
                best = result;
                bestValue = value;
            }
        }
        memo[key] = best;
        return best;
    }

    private static IEnumerable<State> Moves(State st, Info info)
    {
        var faceOpen = st.Taunts.Count == 0;
        foreach (var c in info.Cards)
        {
            if (Played(st, c) || c.Cost > st.Mana || !info.PlayableNow.Contains(c.EntityId)) continue;
            if (c.Type is "MINION" or "LOCATION" && st.Slots <= 0) continue;
            var n = st.Copy();
            if (c.Type is "MINION" or "LOCATION") n.Slots -= 1;
            n.Mana -= c.Cost;
            n.Played |= 1 << c.Index;
            var action = LegalFor(info, c.EntityId, null);
            var dmgMatch = DirectDamage.Match(c.Text);
            if (dmgMatch.Success && !CanHitFace(info, c)) dmgMatch = Match.Empty;
            if (c.CardId is Depths or DepthsOriginal)
            {
                if (st.DepthsReady || st.DepthsUsed) continue;
                n.DepthsReady = true;
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name}", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.Type == "SPELL" && dmgMatch.Success)
            {
                var dmg = int.Parse(dmgMatch.Groups[1].Value) + n.SpellPower(info.BasePower);
                n.Damage += dmg;
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} → héros adverse ({dmg})", SourceEntityId = c.EntityId, TargetEntityId = info.OppHeroId, FaceDamage = dmg, Action = LegalFor(info, c.EntityId, info.OppHeroId) });
                yield return n;
            }
            else if (c.Type == "MINION" && dmgMatch.Success)
            {
                var dmg = int.Parse(dmgMatch.Groups[1].Value);
                n.Damage += dmg;
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0 });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} → héros adverse ({dmg})", SourceEntityId = c.EntityId, TargetEntityId = info.OppHeroId, FaceDamage = dmg, Action = LegalFor(info, c.EntityId, info.OppHeroId) });
                yield return n;
            }
            else if (IsCharge(c))
            {
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack + st.SmithBonus, Health = c.Health, AttacksLeft = 1, CanFace = true });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} (Charge)", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.Type == "MINION" && c.EndTurnFace > 0)
            {
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0, EndTurnFace = c.EndTurnFace });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} ({c.EndTurnFace} au héros adverse en fin de tour)", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.CardId == Seer)
            {
                if (!info.Cards.Any(x => x.Type == "SPELL" && DirectDamage.IsMatch(x.Text) && !Played(st, x) && CanHitFace(info, x))) continue;
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0 });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name}", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.CardId == Smith)
            {
                if (!info.Cards.Any(x => IsCharge(x) && !Played(st, x))) continue;
                n.SmithBonus += 2;
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0 });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} (+2 ATQ en main)", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.CardId == BroodKeeper)
            {
                var dragonLeft = info.Cards.Any(x => x.Index != c.Index && x.IsDragon && !Played(st, x));
                if (!dragonLeft || !info.HeroFresh || !faceOpen || c.PoweredUp == false) continue;
                n.HeroAttack = 2;
                n.HeroCanAttack = true;
                n.RandomWeapon = false;
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0 });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} (épée 2/2)", SourceEntityId = c.EntityId, Action = action });
                yield return n;
            }
            else if (c.CardId == Chronolord)
            {
                if (st.Taunts.All(t => !t.PlayedLastTurn)) continue;
                var destroyed = st.Taunts.Where(t => t.PlayedLastTurn).ToList();
                if (destroyed.Any(t => t.Risk is not null) && !info.AllowRisky) continue;
                n.Risks.AddRange(destroyed.Where(t => t.Risk is not null).Select(t => t.Risk!));
                n.Taunts = st.Taunts.Where(t => !t.PlayedLastTurn).ToList();
                n.Minions.Add(new Minion { Id = c.EntityId, Name = c.Name, CardId = c.CardId, Attack = c.Attack, Health = c.Health, AttacksLeft = 0 });
                n.Steps.Add(new SolverStep { Kind = "play", Label = $"Jouer {c.Name} (détruit les Provocations jouées au tour précédent)", SourceEntityId = c.EntityId, Action = action });
                if (n.Taunts.Count == 0) OpenFace(n, info);
                yield return n;
            }
        }

        // Profondeurs Sanguines : une activation par tour, sur un serviteur qui attaquera encore ou pour une Provocation à 1 PV.
        if (st.DepthsReady && !st.DepthsUsed)
        {
            var depthsId = info.Actions.FirstOrDefault(a => a.Kind == "location" && a.SourceCardId is Depths or DepthsOriginal)?.SourceEntityId
                           ?? info.Cards.FirstOrDefault(c => c.CardId is Depths or DepthsOriginal)?.EntityId;
            foreach (var m in st.Minions)
            {
                var useful = (m.AttacksLeft > 0 && m.CanFace && faceOpen) || (m.CardId == Seer && !m.Damaged) || (m.CardId == Grommash && !m.Damaged && m.AttacksLeft > 0);
                if (!useful || (m.Health <= 1 && !m.DivineShield)) continue;
                var n = st.Copy();
                n.DepthsUsed = true;
                n.DepthsReady = false;
                var t = n.Minions.First(x => x.Id == m.Id);
                t.Attack += 2;
                if (!t.DivineShield)
                {
                    if (t.CardId == Seer && !t.Damaged) t.SeerBonus = 2;
                    t.Health -= 1;
                    t.Damaged = true;
                }
                n.Steps.Add(new SolverStep
                {
                    Kind = "location",
                    Label = $"Activer Profondeurs Sanguines → {m.Name} (+2 ATQ)",
                    SourceEntityId = depthsId,
                    TargetEntityId = m.Id,
                    Action = LegalFor(info, depthsId, m.Id),
                });
                yield return n;
            }
            foreach (var taunt in st.Taunts.Where(t => t.Health == 1 && !t.DivineShield && (t.Risk is null || info.AllowRisky)))
            {
                var n = st.Copy();
                if (taunt.Risk is not null) n.Risks.Add(taunt.Risk);
                n.DepthsUsed = true;
                n.DepthsReady = false;
                n.Taunts = st.Taunts.Where(t => t.Id != taunt.Id).ToList();
                n.Steps.Add(new SolverStep { Kind = "location", Label = $"Activer Profondeurs Sanguines → {taunt.Name} (détruit la Provocation)", SourceEntityId = depthsId, TargetEntityId = taunt.Id, Action = LegalFor(info, depthsId, taunt.Id) });
                if (n.Taunts.Count == 0) OpenFace(n, info);
                yield return n;
            }
        }
    }

    /// <summary>Plus aucune Provocation : les serviteurs qui pouvaient attaquer peuvent viser le héros.</summary>
    private static void OpenFace(State st, Info info)
    {
        foreach (var m in st.Minions.Where(m => m.AttacksLeft > 0 && !m.RushOnly)) m.CanFace = true;
        if (info.HeroFresh && st.HeroAttack > 0) st.HeroCanAttack = true;
    }

    /// <summary>Ajoute les attaques au visage restantes (ordre indifférent contre le héros).</summary>
    private static State Finish(State st, Info info)
    {
        var n = st.Copy();
        var faceOpen = n.Taunts.Count == 0;
        foreach (var m in n.Minions.Where(m => faceOpen && m.AttacksLeft > 0 && m.CanFace))
        {
            var dmg = m.EffectiveAttack * m.AttacksLeft;
            if (dmg <= 0) continue;
            n.Damage += dmg;
            n.Steps.Add(new SolverStep
            {
                Kind = "attack",
                Label = $"Attaquer avec {m.Name} → héros adverse ({dmg})",
                SourceEntityId = m.Id,
                TargetEntityId = info.OppHeroId,
                FaceDamage = dmg,
                Action = LegalFor(info, m.Id, info.OppHeroId),
            });
        }
        if (faceOpen) AddHeroAttack(n, info);
        // Effets de fin de tour au héros adverse (Drake terrestre) : ils se déclenchent après toutes les actions du tour.
        var endTurn = n.Minions.Where(m => m.EndTurnFace > 0).ToList();
        if (endTurn.Count > 0)
        {
            var dmg = endTurn.Sum(m => m.EndTurnFace);
            n.Damage += dmg;
            n.Steps.Add(new SolverStep
            {
                Kind = "end-turn",
                Label = $"Finir le tour : {string.Join(" et ", endTurn.Select(m => m.Name))} inflige{(endTurn.Count > 1 ? "nt" : "")} {dmg} au héros adverse",
                FaceDamage = dmg,
                Action = info.Actions.FirstOrDefault(a => a.Kind == "end-turn"),
            });
        }
        return n;
    }

    private static void AddHeroAttack(State n, Info info)
    {
        if (n.HeroCanAttack && n.HeroAttack > 0 && !n.RandomWeapon)
        {
            n.Damage += n.HeroAttack;
            n.Steps.Add(new SolverStep
            {
                Kind = "attack",
                Label = $"Attaquer avec ton héros → héros adverse ({n.HeroAttack})",
                SourceEntityId = info.HeroId,
                TargetEntityId = info.OppHeroId,
                FaceDamage = n.HeroAttack,
                Action = LegalFor(info, info.HeroId, info.OppHeroId),
            });
        }
    }

    /// <summary>
    /// Létal conditionnel par l'arme aléatoire d'Animatrice de stade : meilleure séquence sans l'arme, puis attaque du héros
    /// avec une arme dont il faudrait au moins N ATQ.
    /// </summary>
    private static (string Text, double Chance, List<SolverStep> Steps)? RandomWeaponLine(State start, Info info, int needed)
    {
        var stage = info.Cards.FirstOrDefault(c => c.CardId == StageHand && info.PlayableNow.Contains(c.EntityId));
        if (stage is null || !info.HeroFresh || start.Taunts.Count > 0 || stage.Cost > start.Mana) return null;
        var n = start.Copy();
        n.Mana -= stage.Cost;
        n.Played |= 1 << stage.Index;
        n.HeroCanAttack = false; // l'arme actuelle est remplacée ; l'attaque avec l'arme aléatoire est comptée à part
        foreach (var keeper in info.Cards.Where(c => c.CardId == BroodKeeper)) n.Played |= 1 << keeper.Index; // une autre arme remplacerait l'arme aléatoire
        var best = Finish(Search(n, info, new Dictionary<string, State>()), info);
        var missing = needed - best.Damage;
        if (missing <= 0 || missing > MaxRandomWeaponAttack) return null;
        // Chance estimée qu'une arme tirée parmi les armes à collectionner atteigne l'ATQ manquante avec le bonus +1.
        var total = WeaponAttackCounts.Sum();
        var p = (double)WeaponAttackCounts.Where((_, atk) => atk + 1 >= missing).Sum() / total;
        var rewind = stage.Text.Contains("Rembobinage", StringComparison.OrdinalIgnoreCase);
        var chance = rewind ? 1 - (1 - p) * (1 - p) : p;
        var estimate = rewind
            ? $"environ {p:P0} par tirage, {chance:P0} avec le second tirage du Rembobinage"
            : $"environ {p:P0}";
        var steps = new List<SolverStep>
        {
            new() { Kind = "play", Label = $"Jouer {stage.Name} (arme aléatoire +1/+1)", SourceEntityId = stage.EntityId, Action = LegalFor(info, stage.EntityId, null) },
        };
        steps.AddRange(best.Steps);
        steps.Add(new SolverStep { Kind = "attack", Label = $"Attaquer avec ton héros et l'arme aléatoire → héros adverse (il faut {missing} ATQ)", SourceEntityId = info.HeroId, TargetEntityId = info.OppHeroId });
        return ($"létal si l'arme aléatoire d'{stage.Name} a au moins {missing} ATQ, bonus +1 compris ({estimate}, estimation sur les armes à collectionner)", chance, steps);
    }

    private static int TotalIfFaceOpen(State start, Info info)
    {
        var n = start.Copy();
        n.Taunts = new List<Taunt>();
        OpenFace(n, info);
        return Finish(Search(n, info, new Dictionary<string, State>()), info).Damage;
    }

    private static LegalAction? LegalFor(Info info, int? source, int? target) =>
        info.Actions.FirstOrDefault(a => a.SourceEntityId == source && (target is null ? a.TargetEntityId is null : a.TargetEntityId == target));

}
