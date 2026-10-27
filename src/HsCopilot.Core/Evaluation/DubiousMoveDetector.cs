using System.Text.RegularExpressions;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;

namespace HsCopilot.Core.Evaluation;

/// <summary>Signalement d'un coup légal mais stratégiquement discutable (heuristique, jamais une preuve d'erreur).</summary>
public sealed class DubiousFlag
{
    public required string Code { get; init; }
    /// <summary>douteux, à vérifier ou info.</summary>
    public required string Severity { get; init; }
    public required string Message { get; init; }
}

/// <summary>Calculs de plateau simples et déterministes, fondés sur l'état observable et les options du serveur.</summary>
public static class BoardMath
{
    public static MinionView? Minion(GameStateSnapshot s, int? entityId) =>
        entityId is int id ? (s.Local?.Board ?? new()).Concat(s.Opponent?.Board ?? new()).FirstOrDefault(m => m.EntityId == id) : null;

    public static bool IsOpponentHero(GameStateSnapshot s, int? entityId) => entityId is int id && s.Opponent?.Hero?.EntityId == id;
    public static bool IsLocalHero(GameStateSnapshot s, int? entityId) => entityId is int id && s.Local?.Hero?.EntityId == id;

    /// <summary>Attaque d'un attaquant (serviteur ou héros) ; Furie des vents comptée si aucune attaque n'a encore eu lieu.</summary>
    public static int AttackValue(GameStateSnapshot s, int? entityId, bool countWindfury)
    {
        if (Minion(s, entityId) is { } m)
        {
            var mult = countWindfury && m.State.AttacksThisTurn == 0 ? (m.Keywords.Contains("MEGA_WINDFURY") ? 4 : m.Keywords.Contains("WINDFURY") ? 2 : 1) : 1;
            return m.Attack * mult;
        }
        if (IsLocalHero(s, entityId)) return s.Local!.Hero!.Attack;
        return 0;
    }

    /// <summary>
    /// Dégâts au visage disponibles par les seules attaques que le serveur autorise sur le héros adverse (chaque attaquant une fois).
    /// Borne basse : ignore sorts, pouvoir héroïque, Charge à jouer, Secrets adverses et effets à la mort.
    /// </summary>
    public static int FaceDamageAvailable(DecisionContext ctx)
    {
        var s = ctx.State;
        if (s.Opponent?.Hero is not { } hero || hero.Immune) return 0;
        return ctx.LegalActions
            .Where(a => a.Kind == "attack" && a.TargetEntityId == hero.EntityId)
            .Select(a => a.SourceEntityId)
            .Distinct()
            .Sum(id => AttackValue(s, id, countWindfury: true));
    }

    private static readonly Regex DirectDamage = new(@"^(?:Cri de guerre\s*:\s*)?Inflige (\d+) points? de dégâts", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Bonus de dégâts des sorts visible sur notre plateau (tags SPELLPOWER).</summary>
    public static int SpellPower(GameStateSnapshot s) =>
        s.Local?.Board.Sum(m => m.Keywords.Where(k => k.StartsWith("SPELLPOWER+", StringComparison.Ordinal))
            .Sum(k => int.TryParse(k["SPELLPOWER+".Length..], out var v) ? v : 0)) ?? 0;

    /// <summary>
    /// Meilleure contribution au visage d'UNE carte de la main jouable maintenant : sort ou cri de guerre « Inflige N points
    /// de dégâts » ciblant le héros adverse (bonus des sorts compris pour un sort), ou serviteur avec Charge si le héros est
    /// attaquable. Borne basse, sans combinaison de plusieurs cartes ni gestion du mana restant après les attaques.
    /// </summary>
    public static (int Damage, LegalAction? Action) BestHandFaceDamage(DecisionContext ctx)
    {
        var s = ctx.State;
        if (s.Opponent?.Hero is not { } hero || hero.Immune) return (0, null);
        var mana = s.Local?.Mana.Available ?? 0;
        var heroAttackable = ctx.LegalActions.Any(a => a.Kind == "attack" && a.TargetEntityId == hero.EntityId)
                             || !(s.Opponent.Board.Any(m => m.Keywords.Contains("TAUNT")));
        var best = (Damage: 0, Action: (LegalAction?)null);
        foreach (var a in ctx.LegalActions.Where(a => a.Kind == "play" && (a.ManaCost ?? int.MaxValue) <= mana))
        {
            var card = s.Local?.Hand.Cards.FirstOrDefault(c => c.EntityId == a.SourceEntityId);
            if (card?.Text is not string text) continue;
            var dmg = 0;
            if (a.TargetEntityId == hero.EntityId && DirectDamage.Match(text) is { Success: true } m)
                dmg = int.Parse(m.Groups[1].Value) + (card.Type == "SPELL" ? SpellPower(s) : 0);
            else if (card.Type == "MINION" && a.TargetEntityId is null && heroAttackable && text.StartsWith("Charge", StringComparison.Ordinal))
                dmg = card.Attack ?? 0;
            if (dmg > best.Damage) best = (dmg, a);
        }
        return best;
    }

    /// <summary>Dégâts que le plateau adverse pourrait infliger au prochain tour (serviteurs non gelés + arme), sans tenir compte de nos Provocations.</summary>
    public static int OpponentBoardDamage(GameStateSnapshot s)
    {
        var minions = s.Opponent?.Board.Where(m => m.Type == "MINION" && !m.State.Frozen && !m.State.Dormant && !m.State.CantAttack)
            .Sum(m => m.Attack * (m.Keywords.Contains("WINDFURY") ? 2 : 1)) ?? 0;
        var weapon = s.Opponent?.Weapon?.Attack ?? 0;
        return minions + weapon;
    }
}

/// <summary>
/// Détecteur de coups légaux mais stratégiquement douteux. Règles explicites et vérifiables ; chaque signalement
/// explique son calcul. Ce sont des heuristiques : un signalement « à vérifier » peut être un bon coup.
/// </summary>
public static class DubiousMoveDetector
{
    private const string Depths = "CORE_REV_990";
    private const string Seer = "END_022";
    private const string Grommash = "CORE_EX1_414";
    private static readonly HashSet<string> DamageCards = new() { "TLC_600", "FIR_939", "TIME_750" };

    public static List<DubiousFlag> Analyze(DecisionContext ctx, LegalAction chosen, StrategyOutput? output = null)
    {
        var s = ctx.State;
        var flags = new List<DubiousFlag>();
        var target = BoardMath.Minion(s, chosen.TargetEntityId);
        var source = BoardMath.Minion(s, chosen.SourceEntityId);

        // 1. Létal par les attaques disponible mais non pris.
        var oppHero = s.Opponent?.Hero;
        var face = BoardMath.FaceDamageAvailable(ctx);
        if (oppHero is not null && face > 0 && face >= oppHero.Health + oppHero.Armor)
        {
            // Toute action qui vise le héros adverse (attaque, sort ou cri de guerre à dégâts) avance vers le létal.
            var goesFace = BoardMath.IsOpponentHero(s, chosen.TargetEntityId);
            if (!goesFace)
            {
                var severe = chosen.Kind is "end-turn" or "attack";
                flags.Add(new DubiousFlag
                {
                    Code = "LETAL_IGNORE",
                    Severity = severe ? "douteux" : "à vérifier",
                    Message = $"Létal possible par les attaques autorisées sur le héros ({face} dégâts contre {oppHero.Health} PV + {oppHero.Armor} armure) mais le coup choisi est « {chosen.Label} ». Secrets adverses éventuels non pris en compte.",
                });
            }
        }

        // 1 bis. Létal possible avec une carte de la main en plus des attaques (heuristique, à vérifier).
        else if (oppHero is not null && BoardMath.BestHandFaceDamage(ctx) is { Damage: > 0 } hand && face + hand.Damage >= oppHero.Health + oppHero.Armor)
        {
            var progresses = BoardMath.IsOpponentHero(s, chosen.TargetEntityId) || chosen.Id == hand.Action!.Id;
            if (!progresses)
                flags.Add(new DubiousFlag
                {
                    Code = "LETAL_AVEC_MAIN_IGNORE",
                    Severity = "à vérifier",
                    Message = $"Létal apparent avec « {hand.Action!.Label} » ({hand.Damage}) et les attaques autorisées ({face}) contre {oppHero.Health} PV + {oppHero.Armor} armure, mais le coup choisi est « {chosen.Label} ».",
                });
        }

        // 2. Fin de tour alors que des actions restent possibles.
        if (chosen.Kind == "end-turn")
        {
            var mana = s.Local?.Mana.Available ?? 0;
            var plays = ctx.LegalActions.Where(a => a.Kind is "play" or "hero-power").ToList();
            var attacks = ctx.LegalActions.Where(a => a.Kind == "attack").Select(a => a.SourceEntityId).Distinct().Count();
            if (plays.Count > 0 || attacks > 0)
            {
                var cheapest = plays.Where(a => a.ManaCost is not null).Select(a => a.ManaCost!.Value).DefaultIfEmpty(int.MaxValue).Min();
                flags.Add(new DubiousFlag
                {
                    Code = "FIN_DE_TOUR_PREMATUREE",
                    Severity = mana >= 2 && plays.Count > 0 ? "douteux" : "à vérifier",
                    Message = $"Fin de tour avec {mana} mana disponible, {plays.Select(p => p.SourceCardId).Distinct().Count()} carte(s) ou pouvoir jouable(s)" +
                              (cheapest != int.MaxValue ? $" (la moins chère coûte {cheapest})" : "") + $" et {attacks} attaquant(s) disponible(s).",
                });
            }
        }

        // 3. Échange perdant : l'attaquant meurt, la cible survit.
        if (chosen.Kind == "attack" && target is not null && target.EntityId != 0 && BoardMath.Minion(s, chosen.SourceEntityId) is { } attacker)
        {
            var attackerDies = target.Attack >= attacker.Health && !attacker.Keywords.Contains("DIVINE_SHIELD") && !attacker.Keywords.Contains("IMMUNE");
            var kills = (attacker.Attack >= target.Health || attacker.Keywords.Contains("POISONOUS") || attacker.Keywords.Contains("VENOMOUS")) && !target.Keywords.Contains("DIVINE_SHIELD");
            if (attackerDies && !kills)
                flags.Add(new DubiousFlag
                {
                    Code = "ECHANGE_PERDANT",
                    Severity = "douteux",
                    Message = $"{attacker.Name} ({attacker.Attack}/{attacker.Health}) meurt contre {target.Name} ({target.Attack}/{target.Health}) sans le tuer.",
                });
        }
        if (chosen.Kind == "attack" && target is not null && BoardMath.IsLocalHero(s, chosen.SourceEntityId))
        {
            var killsWithHero = s.Local!.Hero!.Attack >= target.Health && !target.Keywords.Contains("DIVINE_SHIELD");
            if (!killsWithHero)
                flags.Add(new DubiousFlag
                {
                    Code = "HEROS_ATTAQUE_SANS_TUER",
                    Severity = "à vérifier",
                    Message = $"Le héros ({s.Local.Hero.Attack} ATQ) attaque {target.Name} ({target.Health} PV) sans le tuer et subit {target.Attack} dégâts.",
                });
        }

        // 4. Profondeurs Sanguines sur un serviteur adverse qui survit : +2 ATQ offert à l'adversaire.
        if (chosen.Kind == "location" && chosen.SourceCardId == Depths && target is not null && chosen.TargetSide == "opponent")
        {
            var survives = target.Health > 1 || target.Keywords.Contains("DIVINE_SHIELD");
            if (survives)
                flags.Add(new DubiousFlag
                {
                    Code = "PROFONDEURS_SUR_ADVERSAIRE",
                    Severity = "douteux",
                    Message = $"Profondeurs Sanguines sur {target.Name} ({target.Attack}/{target.Health}) : il survit à 1 dégât et gagne +2 ATQ.",
                });
        }

        // 5. Dégâts sur son propre camp.
        if (chosen.Kind == "play" && chosen.SourceCardId is string card && DamageCards.Contains(card) && chosen.TargetSide == "local")
        {
            if (BoardMath.IsLocalHero(s, chosen.TargetEntityId))
                flags.Add(new DubiousFlag { Code = "DEGATS_SUR_SON_HEROS", Severity = "douteux", Message = $"{chosen.SourceName} inflige des dégâts à ton propre héros." });
            else if (target is not null && target.CardId is not (Seer or Grommash))
                flags.Add(new DubiousFlag { Code = "DEGATS_SUR_SON_SERVITEUR", Severity = "à vérifier", Message = $"{chosen.SourceName} cible ton propre serviteur {target.Name} (ni Voyante ni Grommash)." });
        }

        // 6. Chronoseigneur Époque sans cible.
        if (chosen.Kind == "play" && chosen.SourceCardId == "TIME_714")
        {
            var targets = s.Opponent?.LastTurn?.MinionsPlayed.Count(m => m.Note == "encore en jeu") ?? 0;
            if (targets == 0)
                flags.Add(new DubiousFlag { Code = "CHRONOSEIGNEUR_SANS_CIBLE", Severity = "à vérifier", Message = "Chronoseigneur Époque joué alors qu'aucun serviteur joué par l'adversaire au dernier tour n'est en jeu : cri de guerre sans effet." });
        }

        // 7. Frappe anticipée sans condition de pioche.
        if (chosen.Kind == "play" && chosen.SourceCardId == "TIME_750")
        {
            var big = s.Local?.Hand.Cards.Any(c => c.EntityId != chosen.SourceEntityId && c.Type == "MINION" && c.Cost >= 5) ?? false;
            if (!big)
                flags.Add(new DubiousFlag { Code = "FRAPPE_SANS_PIOCHE", Severity = "info", Message = "Frappe anticipée jouée sans serviteur coûtant 5 ou plus en main : pas de pioche." });
        }

        // 8. Incohérence de sortie.
        if (output?.RejectedAlternativeId is string alt && alt == chosen.Id)
            flags.Add(new DubiousFlag { Code = "ALTERNATIVE_IDENTIQUE", Severity = "à vérifier", Message = "L'alternative écartée est identique au coup choisi." });

        return flags;
    }
}
