using System.Text;

namespace HsCopilot.Core.State;

/// <summary>Rendu texte français d'un <see cref="GameStateSnapshot"/> (CLI, onglet texte du diagnostic).</summary>
public static class StateText
{
    public static string Render(GameStateSnapshot s, int historyLines = 15)
    {
        var sb = new StringBuilder();
        var g = s.Game;
        sb.AppendLine($"Partie {s.GameId} — révision {s.Revision} — {s.LinesProcessed} lignes — dernière ligne {s.LastLogTime}{(s.OpenBlocks > 0 ? $" — {s.OpenBlocks} bloc(s) en cours" : "")}");
        sb.AppendLine($"Mode {g.GameType ?? "?"} / {g.FormatType ?? "?"} — Tour {g.TurnNumber} (TURN={g.GameTurn}, {Side(g.ActivePlayer)}) — étape {g.Step ?? "?"} — premier joueur : {Side(g.FirstPlayer)}{(g.GameOver ? $" — PARTIE TERMINÉE ({g.Result ?? "?"}{(g.Conceded is null ? "" : ", abandon " + Side(g.Conceded))})" : "")}");
        sb.AppendLine($"Source {s.Source.Transport}{(s.Source.Detail is null ? "" : " (" + s.Source.Detail + ")")} — client build {s.Source.ClientBuild?.ToString() ?? "?"} — cartes build {s.Source.CardDbBuild} ({s.Source.CardDbCount})");
        if (s.Uncertainty.Flags.Count > 0)
        {
            sb.AppendLine(s.Uncertainty.Incomplete ? "⚠ ÉTAT INCOMPLET" : "Indicateurs :");
            foreach (var f in s.Uncertainty.Flags) sb.AppendLine($"  [{f.Severity}] {f.Message}{(f.Count > 1 ? $" (×{f.Count})" : "")}");
        }
        sb.AppendLine();
        if (s.Opponent is not null) RenderPlayer(sb, s.Opponent, "ADVERSAIRE");
        sb.AppendLine();
        if (s.Local is not null) RenderPlayer(sb, s.Local, "VOUS");

        if (s.LegalOptions is { } o)
        {
            sb.AppendLine();
            sb.AppendLine($"Actions légales (serveur, options #{o.OptionsId}, révision {o.Revision}{(o.Fresh ? ", à jour" : ", PÉRIMÉES")}) : {o.Playable.Count} jouable(s){(o.CanEndTurn ? " + fin de tour" : "")}");
            foreach (var op in o.Playable)
            {
                var targets = op.Targets.Count == 0 ? "" : " → " + string.Join(", ", op.Targets.Select(t => $"{t.Name ?? "#" + t.EntityId}{(t.Side == "local" ? " (allié)" : "")}"));
                var subs = op.SubOptions.Count == 0 ? "" : " [choix : " + string.Join(" / ", op.SubOptions.Select(x => x.Name ?? "?")) + "]";
                sb.AppendLine($"  - {KindFr(op.Kind)} {op.Name ?? "#" + op.EntityId}{targets}{subs}");
            }
        }
        if (s.PendingChoice is { } c)
        {
            sb.AppendLine();
            sb.AppendLine($"Choix en attente ({c.ChoiceType}, source {c.SourceName ?? "?"}) : {string.Join(" | ", c.Offered.Select(x => x.Name ?? "?"))}");
        }
        if (s.History.Count > 0 && historyLines > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Historique récent :");
            foreach (var h in s.History.TakeLast(historyLines)) sb.AppendLine("  " + HistoryLine(h));
        }
        return sb.ToString();
    }

    private static void RenderPlayer(StringBuilder sb, PlayerView p, string title)
    {
        var hero = p.Hero;
        var heroText = hero is null ? "héros ?" : $"{hero.Name ?? "Héros"} {hero.Health}/{hero.MaxHealth} PV{(hero.Armor > 0 ? $" +{hero.Armor} armure" : "")}{(hero.Attack > 0 ? $", {hero.Attack} ATQ" : "")}";
        sb.AppendLine($"== {title} ({p.HeroClass ?? "?"}) — {heroText} — mana {p.Mana.Available}/{p.Mana.Max}{(p.Mana.OverloadLocked > 0 ? $" (surcharge {p.Mana.OverloadLocked})" : "")}");
        if (p.HeroPower is { } hp) sb.AppendLine($"   Pouvoir : {hp.Name ?? "?"} ({hp.Cost}){(hp.Exhausted ? " — utilisé" : "")}");
        if (p.Weapon is { } w) sb.AppendLine($"   Arme : {w.Name ?? "?"} {w.Attack}/{w.Durability}");
        sb.AppendLine($"   Plateau ({p.Board.Count}) :");
        foreach (var m in p.Board)
        {
            var kw = m.Keywords.Count > 0 ? " [" + string.Join(", ", m.Keywords) + "]" : "";
            var ench = m.Enchantments.Count > 0 ? " {" + string.Join(", ", m.Enchantments.Select(e => e.Name ?? "?")) + "}" : "";
            var state = m.Type == "LOCATION"
                ? (m.State.ServerAllowsAction is bool can ? (can ? " (activable)" : " (non activable)") : m.State.Exhausted || m.State.LocationCooldown > 0 ? " (en recharge)" : " (prêt)")
                : m.State.ServerAllowsAction is bool ok ? (ok ? " (peut attaquer)" : " (ne peut pas attaquer)") : m.State.Exhausted ? " (épuisé)" : "";
            var stats = m.Type == "LOCATION" ? $"{m.Health} charge(s)" : $"{m.Attack}/{m.Health}{(m.Health < m.MaxHealth ? $" (max {m.MaxHealth})" : "")}";
            sb.AppendLine($"     {m.Position}. {m.Name ?? "?"} {stats}{kw}{ench}{state}");
        }
        sb.AppendLine($"   Main ({p.Hand.Count}) :");
        foreach (var c in p.Hand.Cards)
        {
            if (c.Knowledge == Visibility.Own)
            {
                var cost = c.Cost is int cc ? (c.CostModified ? $"({cc}, base {c.BaseCost})" : $"({cc})") : "(?)";
                var stats = c.Attack is int a && c.Health is int h ? $" {a}/{h}" : "";
                var buff = c.BaseAttack is int ba && c.Attack is int aa && aa != ba ? $" [base {ba}/{c.BaseHealth}]" : "";
                sb.AppendLine($"     {c.Position}. {cost} {c.Name ?? "?"}{stats}{buff}{(c.PoweredUp == true ? " ✦condition remplie" : "")}{(c.Created == true ? " (créée)" : "")}");
            }
            else
            {
                var seen = c.CardId is null ? "carte cachée" + (c.Hypothesis is null ? "" : $" — hypothèse : {c.Hypothesis}") : $"{c.Name} ({(c.Knowledge == Visibility.PublicSeenStale ? "vue, peut avoir changé" : "vue publiquement")})";
                sb.AppendLine($"     {c.Position}. {seen} — en main depuis TURN={c.EnteredTurn}");
            }
        }
        var d = p.Deck;
        sb.Append($"   Deck : {d.Count} carte(s)");
        if (d.DecklistKnown)
            sb.Append($" — restantes connues : {string.Join(", ", d.KnownRemaining.Select(k => $"{k.Name}{(k.Count > 1 ? " ×" + k.Count : "")}"))}");
        sb.AppendLine();
        if (d.Warning is not null) sb.AppendLine($"   ⚠ {d.Warning}");
        if (p.Secrets.Count > 0)
            sb.AppendLine($"   Secrets : {string.Join(", ", p.Secrets.Select(x => x.Known ? x.Name : $"Secret inconnu ({x.CardClass ?? "?"})"))}");
        if (p.PublicSecretZone.Count > 0)
            sb.AppendLine($"   Quêtes/éléments publics : {string.Join(", ", p.PublicSecretZone.Select(x => x.Name))}");
        if (p.LastTurn is { } lt)
            sb.AppendLine($"   Dernier tour (TURN={lt.GameTurn}) : {(lt.CardsPlayed.Count == 0 ? "aucune carte jouée" : string.Join(", ", lt.CardsPlayed.Select(x => x.Name + (x.Note is null ? "" : $" ({x.Note})"))))}");
        if (p.ThisTurn is { } tt && tt.CardsPlayed.Count > 0)
            sb.AppendLine($"   Ce tour : {string.Join(", ", tt.CardsPlayed.Select(x => x.Name))}");
    }

    public static string HistoryLine(HistoryItem h)
    {
        var who = Side(h.Player);
        var rew = h.Rewound ? " [annulé par Rembobinage]" : "";
        var related = h.Related.Count == 0 ? "" : " : " + string.Join(", ", h.Related.Select(r => (r.Name ?? "?") + (r.Note is null ? "" : $" ({r.Note})")));
        return h.Kind switch
        {
            "turn-start" => $"— Tour {h.TurnNumber} (TURN={h.GameTurn}, {who}) —",
            "play" => $"T{h.TurnNumber} {who} joue {h.SourceName}{(h.TargetName is null ? "" : " → " + h.TargetName)}{related}{rew}",
            "attack" => $"T{h.TurnNumber} {who} attaque : {h.SourceName} → {h.TargetName}{related}{rew}",
            "hero-power" => $"T{h.TurnNumber} {who} pouvoir héroïque {h.SourceName}{(h.TargetName is null ? "" : " → " + h.TargetName)}{rew}",
            "location" => $"T{h.TurnNumber} {who} active le lieu {h.SourceName}{(h.TargetName is null ? "" : " → " + h.TargetName)}{rew}",
            "deaths" => $"T{h.TurnNumber} morts{related}{rew}",
            "trigger" => $"T{h.TurnNumber} déclenchement {h.SourceName} ({who}){related}{rew}",
            "mulligan" => $"Mulligan {who} : {h.Detail}{related}",
            "choice" => $"T{h.TurnNumber} choix {who} ({h.Detail}){related}{rew}",
            "rewind" => $"T{h.TurnNumber} REMBOBINAGE par {h.SourceName} : {h.Detail}",
            "game-end" => $"Fin de partie : {who} {h.Detail}",
            _ => $"T{h.TurnNumber} {h.Kind} {h.SourceName}{rew}",
        };
    }

    public static string Side(string side) => side switch
    {
        "local" => "vous",
        "opponent" => "adversaire",
        "system" => "jeu",
        _ => "inconnu",
    };

    private static string KindFr(string kind) => kind switch
    {
        "play" => "Jouer",
        "attack" => "Attaquer avec",
        "hero-power" => "Pouvoir héroïque",
        "location" => "Activer le lieu",
        _ => kind,
    };
}
