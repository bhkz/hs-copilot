using System.Text.RegularExpressions;
using HsCopilot.Core.State;

namespace HsCopilot.Core.Strategy;

/// <summary>Une action légale qui retire des dégâts adverses visibles au prochain tour.</summary>
public sealed record DefensiveOption
{
    public required LegalAction Action { get; init; }
    /// <summary>Dégâts visibles retirés (attaque de la menace tuée, ou armure gagnée).</summary>
    public int Removed { get; init; }
    /// <summary>Ce que l'action coûte en pression : dégâts au visage qu'elle ne fait pas (0 si le visage est fermé).</summary>
    public int FaceForgone { get; init; }
    public required string Text { get; init; }
}

/// <summary>Effet d'une carte de ta main qui profite aussi à l'adversaire (Nozdormu l'Intemporel, Animatrice de stade…).</summary>
public sealed record OpponentGift
{
    public required string Card { get; init; }
    public required string Effect { get; init; }
}

/// <summary>
/// Danger au prochain tour adverse, à partir de l'information publique seulement : ce que l'adversaire peut infliger avec ce
/// qui est déjà en jeu (serviteurs pouvant attaquer, arme, pouvoir héroïque, fin de tour), contre tes PV, ton armure et tes
/// Provocations. Ni sa main, ni ses tirages, ni ses Secrets ne sont chiffrés : ils sont seulement signalés. Pas de simulateur.
/// </summary>
public sealed record ThreatReport
{
    public int VisibleDamage { get; init; }
    public List<string> Sources { get; init; } = new();
    public int EffectiveHealth { get; init; }
    /// <summary>PV des Provocations : absorbés au mieux par les attaques (approximation basse de leur effet).</summary>
    public int TauntShield { get; init; }
    /// <summary>PV + armure + Provocations − dégâts visibles. ≤ 0 : létal adverse visible.</summary>
    public int Margin { get; init; }
    /// <summary>« létal visible », « danger élevé » (marge ≤ 5) ou « faible ».</summary>
    public required string Level { get; init; }
    public List<DefensiveOption> Defenses { get; init; } = new();
    public List<OpponentGift> Gifts { get; init; } = new();
    public int OpponentHand { get; init; }
    public int OpponentManaNext { get; init; }
    public int UnknownSecrets { get; init; }

    public const string VisibleLethal = "létal visible";
    public const string High = "danger élevé";
    public const string Low = "faible";
}

public static class ThreatAssessment
{
    internal static readonly Regex HeroPowerDamage = new(@"Inflige\s+\$?(\d+)\s+points?\s+de\s+dégâts", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex EndOfTurnDamage = new(@"fin de votre tour\W{0,4}inflige (\d+) points? de dégâts (?:au héros|à (?:chaque|tous les) (?:adversaires?|ennemis))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DirectDamage = new(@"^(?:Cri de guerre\s*:\s*)?Inflige (\d+) points? de dégâts", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex Gift = new(@"chaque joueur|les deux joueurs|les deux adversaires|votre adversaire (?:pioche|obtient|gagne|s’équipe|s'équipe|invoque|reçoit)|à votre adversaire", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ManaToTen = new(@"cristaux de mana de chaque joueur à\W*10", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static readonly Regex ArmorGain = new(@"Gagne\s+\$?d?(\d+)\s+points?\s+d.armure", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static ThreatReport? Assess(DecisionContext ctx)
    {
        var s = ctx.State;
        if (s.Local is not { Hero: { } myHero } me || s.Opponent is not { } opp) return null;

        // Dégâts visibles au prochain tour adverse.
        var sources = new List<string>();
        var minionDamage = 0;
        foreach (var m in opp.Board.Where(m => m.Type == "MINION"))
        {
            if (m.Attack <= 0 || m.State.Frozen || m.State.Dormant || m.State.CantAttack || m.Keywords.Contains("FROZEN")) continue;
            var n = m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1;
            minionDamage += m.Attack * n;
            sources.Add($"{m.Name ?? "serviteur"} {m.Attack}{(n > 1 ? $" ×{n}" : "")}");
        }
        var weapon = opp.Weapon is { Attack: > 0, Durability: > 0 } w && opp.Hero is { Frozen: false } ? w.Attack : 0;
        if (weapon > 0) sources.Add($"arme {weapon}");
        var power = opp.HeroPower is { } hp && HeroPowerDamage.Match(hp.Text ?? "") is { Success: true } pm ? int.Parse(pm.Groups[1].Value) : 0;
        if (power > 0) sources.Add($"pouvoir héroïque {power}");
        var endTurn = opp.Board.Sum(m => EndOfTurnDamage.Match(m.Text ?? "") is { Success: true } em ? int.Parse(em.Groups[1].Value) : 0);
        if (endTurn > 0) sources.Add($"fin de tour {endTurn}");

        var taunt = me.Board.Where(m => m.Type == "MINION" && m.Keywords.Contains("TAUNT")).Sum(m => m.Health);
        var attackDamage = minionDamage + weapon;
        var absorbed = Math.Min(attackDamage, taunt);
        var visible = attackDamage + power + endTurn;
        var hpEff = myHero.Health + myHero.Armor;
        var margin = hpEff + absorbed - visible;

        // Actions légales qui retirent une menace : une attaque ou un sort de dégâts direct qui tue un attaquant adverse, l'armure.
        var faceOpen = !opp.Board.Any(m => m.Type == "MINION" && m.Keywords.Contains("TAUNT"));
        var threats = opp.Board.Where(m => m.Type == "MINION" && m.Attack > 0 && !m.State.Frozen && !m.State.Dormant && !m.State.CantAttack)
            .ToDictionary(m => m.EntityId);
        var mine = me.Board.ToDictionary(m => m.EntityId);
        var hand = me.Hand.Cards.ToDictionary(c => c.EntityId);
        var defenses = new List<DefensiveOption>();
        foreach (var a in ctx.LegalActions)
        {
            if (a.TargetEntityId is int t && threats.TryGetValue(t, out var target) && !target.Keywords.Contains("DIVINE_SHIELD"))
            {
                int? dealt = null;
                var forgone = 0;
                if (a.Kind == "attack")
                {
                    if (a.SourceEntityId == myHero.EntityId) { dealt = myHero.Attack; forgone = faceOpen ? myHero.Attack : 0; }
                    else if (a.SourceEntityId is int src && mine.TryGetValue(src, out var att)) { dealt = att.Attack; forgone = faceOpen ? att.Attack : 0; }
                }
                else if (a.Kind == "play" && a.SourceEntityId is int card && hand.TryGetValue(card, out var c) && DirectDamage.Match(c.Text ?? "") is { Success: true } dm)
                    dealt = int.Parse(dm.Groups[1].Value);
                if (dealt is int d && d >= target.Health)
                {
                    var n = target.Keywords.Contains("MEGA_WINDFURY") ? 4 : target.Keywords.Contains("WINDFURY") ? 2 : 1;
                    defenses.Add(new DefensiveOption
                    {
                        Action = a, Removed = target.Attack * n, FaceForgone = forgone,
                        Text = $"{AdviceDisplay.Label(a, s)} : −{target.Attack * n} dégâts visibles{(forgone > 0 ? $", {forgone} de moins au visage" : "")}",
                    });
                }
            }
            else if (a.Kind == "hero-power" && me.HeroPower is { } myPower && ArmorGain.Match(myPower.Text ?? "") is { Success: true } am)
            {
                var gain = int.Parse(am.Groups[1].Value);
                defenses.Add(new DefensiveOption { Action = a, Removed = gain, Text = $"{a.Label} : +{gain} armure" });
            }
        }
        defenses = defenses.OrderByDescending(d => d.Removed).ThenBy(d => d.FaceForgone).ToList();

        // Cartes de ta main qui profitent aussi à l'adversaire.
        var oppManaNext = Math.Min(10, opp.Mana.Max + 1);
        var gifts = new List<OpponentGift>();
        // Signal utile seulement s'il pèse : la mise à 10 cristaux toujours (Nozdormu l'Intemporel, partie du 25/10 à 22 h 47) ;
        // les autres (l'arme aléatoire d'Animatrice de stade, carte de base du deck) seulement quand le danger est réel.
        foreach (var c in me.Hand.Cards.Where(c => c.Text is not null && Gift.IsMatch(c.Text) && (ManaToTen.IsMatch(c.Text) || margin <= 10)))
        {
            var effect = ManaToTen.IsMatch(c.Text!)
                ? $"l'adversaire aurait 10 cristaux au prochain tour au lieu de {oppManaNext}, avec {opp.Hand.Count} cartes en main"
                : c.Text!.Length > 140 ? c.Text[..140] + "…" : c.Text;
            gifts.Add(new OpponentGift { Card = c.Name ?? "?", Effect = effect });
        }

        return new ThreatReport
        {
            VisibleDamage = visible, Sources = sources, EffectiveHealth = hpEff, TauntShield = absorbed, Margin = margin,
            Level = margin <= 0 ? ThreatReport.VisibleLethal : margin <= 5 ? ThreatReport.High : ThreatReport.Low,
            Defenses = defenses, Gifts = gifts, OpponentHand = opp.Hand.Count, OpponentManaNext = oppManaNext,
            UnknownSecrets = opp.Secrets.Count(x => !x.Known),
        };
    }

    /// <summary>Champ compact pour le message du modèle (analyse locale), null si rien à signaler.</summary>
    public static object? Payload(ThreatReport? r)
    {
        if (r is null) return null;
        return new
        {
            level = r.Level,
            visibleDamageNextTurn = r.VisibleDamage,
            yourEffectiveHealth = r.EffectiveHealth,
            margin = r.Margin,
            sources = r.Sources,
            defenses = r.Margin <= 5 ? r.Defenses.Take(4).Select(d => d.Text).ToList() : null,
            giftsToOpponent = r.Gifts.Count > 0 ? r.Gifts.Select(g => $"{g.Card} : {g.Effect}").ToList() : null,
            unknown = $"{r.OpponentHand} cartes en main adverses, {r.OpponentManaNext} cristaux au prochain tour, {r.UnknownSecrets} Secret(s) inconnu(s) : non chiffrés",
        };
    }
}
