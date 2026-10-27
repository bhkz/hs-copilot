using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Media;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Util;

namespace HsCopilot.App;

public sealed record LineVm(string Text, Brush Color);

public sealed record MinionVm(string Name, string Stats, string Keywords, string Enchantments, string State, Brush StateColor, string Tooltip);

public sealed record HandVm(string Cost, Brush CostColor, string Name, string Details, string Tooltip);

public sealed class PlayerVm
{
    public string Title { get; init; } = "";
    public string HeroLine { get; init; } = "";
    public string ManaLine { get; init; } = "";
    public string EquipmentLine { get; init; } = "";
    public List<MinionVm> Board { get; init; } = new();
    public string BoardEmpty => Board.Count == 0 ? "Plateau vide" : "";
    public string HandTitle { get; init; } = "";
    public List<HandVm> Hand { get; init; } = new();
    public string DeckLine { get; init; } = "";
    public string DeckDetails { get; init; } = "";
    public string ExtraLine { get; init; } = "";
    public string LastTurnLine { get; init; } = "";
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly Brush Good = Freeze(new SolidColorBrush(Color.FromRgb(0x6C, 0xC4, 0x87)));
    private static readonly Brush Warn = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xB0, 0x4B)));
    private static readonly Brush Bad = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0x6D, 0x6D)));
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2)));
    private static readonly Brush Text = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0xE9, 0xEF)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private string _sourceText = "Démarrage…";
    private Brush _sourceColor = Muted;
    private string _gameText = "";
    private string _turnText = "";
    private string _revisionText = "";
    private string _incompleteText = "";
    private List<LineVm> _flags = new();
    private PlayerVm _opponent = new() { Title = "ADVERSAIRE" };
    private PlayerVm _local = new() { Title = "VOUS" };
    private List<LineVm> _options = new();
    private List<LineVm> _history = new();
    private List<LineVm> _check = new();
    private List<LineVm> _validation = new() { new LineVm("Le rapport de validation apparaît dès qu'une partie est reçue en direct.", Freeze(new SolidColorBrush(Color.FromRgb(0x9A, 0xA3, 0xB2)))) };
    private List<string> _log = new();
    private string _json = "";
    private string _stateText = "";
    private string _footer = "";
    private string _modeText = "Direct";

    public string SourceText { get => _sourceText; set => Set(ref _sourceText, value); }
    public Brush SourceColor { get => _sourceColor; set => Set(ref _sourceColor, value); }
    public string GameText { get => _gameText; set => Set(ref _gameText, value); }
    public string TurnText { get => _turnText; set => Set(ref _turnText, value); }
    public string RevisionText { get => _revisionText; set => Set(ref _revisionText, value); }
    public string IncompleteText { get => _incompleteText; set => Set(ref _incompleteText, value); }
    public List<LineVm> Flags { get => _flags; set => Set(ref _flags, value); }
    public PlayerVm Opponent { get => _opponent; set => Set(ref _opponent, value); }
    public PlayerVm Local { get => _local; set => Set(ref _local, value); }
    public List<LineVm> Options { get => _options; set => Set(ref _options, value); }
    public List<LineVm> History { get => _history; set => Set(ref _history, value); }
    public List<LineVm> Check { get => _check; set => Set(ref _check, value); }
    public List<LineVm> Validation { get => _validation; set => Set(ref _validation, value); }
    public List<string> Log { get => _log; set => Set(ref _log, value); }
    public string Json { get => _json; set => Set(ref _json, value); }
    public string StateText { get => _stateText; set => Set(ref _stateText, value); }
    public string Footer { get => _footer; set => Set(ref _footer, value); }
    public string ModeText { get => _modeText; set => Set(ref _modeText, value); }

    public GameStateSnapshot? Current { get; private set; }

    // ------------------------------------------------------------------ Application d'un état

    public void Apply(GameStateSnapshot s)
    {
        Current = s;
        var g = s.Game;
        GameText = $"Partie {s.GameId} · {Mode(g.GameType)} {Format(g.FormatType)}";
        TurnText = g.GameOver
            ? $"Terminée : {Result(g.Result)}{(g.Conceded is null ? "" : $" (abandon {Core.State.StateText.Side(g.Conceded)})")} · tour {g.TurnNumber}"
            : $"Tour {g.TurnNumber} · {(g.ActivePlayer == "local" ? "à vous" : g.ActivePlayer == "opponent" ? "adversaire" : "?")} · {Step(g.Step)}";
        RevisionText = $"Révision {s.Revision} · {s.LinesProcessed} lignes · log {s.LastLogTime}{(s.OpenBlocks > 0 ? " · action en cours" : "")}";

        IncompleteText = s.Uncertainty.Incomplete
            ? "État incomplet : " + string.Join(" ", s.Uncertainty.Flags.Where(f => f.Severity == "critical").Select(f => f.Message))
            : "";
        Flags = s.Uncertainty.Flags.Where(f => f.Severity != "critical")
            .Select(f => new LineVm((f.Severity == "warning" ? "⚠ " : "ℹ ") + f.Message + (f.Count > 1 ? $" (×{f.Count})" : ""), f.Severity == "warning" ? Warn : Muted))
            .ToList();

        Opponent = s.Opponent is null ? new PlayerVm { Title = "ADVERSAIRE", HeroLine = "inconnu" } : BuildPlayer(s.Opponent, "ADVERSAIRE");
        Local = s.Local is null ? new PlayerVm { Title = "VOUS", HeroLine = "joueur local non identifié" } : BuildPlayer(s.Local, "VOUS");

        var opts = new List<LineVm>();
        if (s.LegalOptions is { } o)
        {
            opts.Add(new LineVm($"Options serveur #{o.OptionsId} (révision {o.Revision}) — {(o.Fresh ? "à jour" : "PÉRIMÉES : l'état a changé depuis")}", o.Fresh ? Good : Warn));
            foreach (var op in o.Playable)
            {
                var targets = op.Targets.Count == 0 ? "" : " → " + string.Join(", ", op.Targets.Select(t => (t.Name ?? "#" + t.EntityId) + (t.Side == "local" ? " (allié)" : "")));
                var subs = op.SubOptions.Count == 0 ? "" : $" [{string.Join(" / ", op.SubOptions.Select(x => x.Name ?? "?"))}]";
                opts.Add(new LineVm($"• {KindFr(op.Kind)} {op.Name ?? "#" + op.EntityId}{targets}{subs}", Text));
            }
            if (o.CanEndTurn) opts.Add(new LineVm("• Fin de tour", Muted));
            opts.Add(new LineVm("Ces actions sont celles que le serveur accepte maintenant ; aucune séquence n'est vérifiée au-delà de la première action.", Muted));
        }
        else
        {
            opts.Add(new LineVm("Aucune option reçue (le serveur n'envoie les actions légales que pendant votre tour).", Muted));
        }
        if (s.PendingChoice is { } c)
            opts.Add(new LineVm($"Choix en attente ({c.ChoiceType}, {c.SourceName ?? "?"}) : {string.Join(" | ", c.Offered.Select(x => x.Name ?? "?"))}", Warn));
        Options = opts;

        History = s.History.AsEnumerable().Reverse().Take(150)
            .Select(h => new LineVm(Core.State.StateText.HistoryLine(h), h.Kind == "turn-start" ? Warn : h.Rewound ? Muted : h.Player == "opponent" ? Bad : Text))
            .ToList();
        StateText = Core.State.StateText.Render(s, historyLines: 40);
        Json = JsonSerializer.Serialize(s, HsCopilot.Core.Util.Json.Pretty);
    }

    public void ApplyStatus(SessionStatus st, bool fileMode)
    {
        if (!fileMode)
        {
            (SourceText, SourceColor) = st.ActiveSource switch
            {
                SourceKind.HdtPlugin => ($"● Plugin HDT connecté{(st.HdtVersion is null ? "" : $" (HDT {st.HdtVersion})")}", Good),
                SourceKind.LogTail => ("● Lecture directe du Power.log (repli)", Warn),
                _ => (st.ConfiguredSource == "none" ? "Aucune source" : "○ En attente du plugin HDT…", Muted),
            };
        }
        Log = st.Log.Reverse().ToList();
        var check = new List<LineVm>();
        if (st.LastCheck is { } chk)
        {
            check.Add(new LineVm($"Dernier contrôle au repos : {chk.At:HH:mm:ss} — {(chk.AllOk ? "identique à HDT" : "ÉCARTS")}", chk.AllOk ? Good : Bad));
            foreach (var (item, ok, detail) in chk.Items) check.Add(new LineVm($"{(ok ? "✓" : "✗")} {item} — {detail}", ok ? Good : Bad));
        }
        else
        {
            check.Add(new LineVm(st.PluginConnected
                ? "En attente d'un relevé HDT pris au repos (aucune ligne depuis 3 s)."
                : "Contrôle croisé indisponible : nécessite le plugin HDT.", Muted));
        }
        check.Add(new LineVm("HDT suit le rythme des animations ; l'application suit les données serveur. Un écart pendant une action est normal, seul un écart au repos est signalé.", Muted));
        Check = check;
        if (!fileMode && st.LiveReport is { } r)
        {
            var lines = new List<LineVm>
            {
                new($"Validation de la partie {r.GameId} : {r.Verdict}", r.Verdict == "VALIDÉ" ? Good : r.Verdict == "ÉCHEC" ? Bad : Warn),
                new($"Source {r.Source} · {r.Lines} lignes · révisions {r.FirstRevision}–{r.LastRevision}", Muted),
            };
            foreach (var c in r.Checks)
            {
                var color = c.Status switch
                {
                    Core.Validation.CheckStatus.Ok => Good,
                    Core.Validation.CheckStatus.Failed => Bad,
                    Core.Validation.CheckStatus.Review => Warn,
                    _ => Muted,
                };
                lines.Add(new LineVm($"{Core.Validation.LiveValidationReport.StatusText(c.Status)} · {c.Name} — {c.Detail}", color));
            }
            lines.Add(new LineVm("Latences (P50 / P95 / max) :", Text));
            foreach (var l in r.Latencies) lines.Add(new LineVm($"  {l.Name} : {l.P50:0} / {l.P95:0} / {l.Max:0} ms ({l.Count})", Muted));
            if (st.LastReportPath is not null) lines.Add(new LineVm($"Dernier rapport enregistré : {st.LastReportPath}", Muted));
            Validation = lines;
        }
        Footer = $"{st.CardDbMessage} · deck : {st.DeckName ?? "inconnu"} · lignes reçues {st.LinesReceived}{(st.LinesIgnored > 0 ? $", ignorées {st.LinesIgnored}" : "")}{(st.JournalPath is null ? "" : " · journal " + st.JournalPath)}";
    }

    // ------------------------------------------------------------------ Construction des panneaux

    private static PlayerVm BuildPlayer(PlayerView p, string title)
    {
        var h = p.Hero;
        var heroLine = h is null ? "Héros inconnu" : $"{h.Name ?? "Héros"} · {h.Health}/{h.MaxHealth} PV{(h.Armor > 0 ? $" · {h.Armor} armure" : "")}{(h.Attack > 0 ? $" · {h.Attack} ATQ" : "")}{(h.Frozen ? " · gelé" : "")}{(h.Immune ? " · insensible" : "")}";
        var mana = $"Mana {p.Mana.Available}/{p.Mana.Max}{(p.Mana.OverloadLocked > 0 ? $" · surcharge {p.Mana.OverloadLocked}" : "")}{(p.Mana.OverloadOwed > 0 ? $" · surcharge à venir {p.Mana.OverloadOwed}" : "")}{(p.Fatigue > 0 ? $" · fatigue {p.Fatigue}" : "")}";
        var equip = string.Join(" · ", new[]
        {
            p.HeroPower is { } hp ? $"Pouvoir : {hp.Name ?? "?"} ({hp.Cost}){(hp.Exhausted ? " — utilisé" : "")}" : null,
            p.Weapon is { } w ? $"Arme : {w.Name ?? "?"} {w.Attack}/{w.Durability}" : null,
        }.Where(x => x is not null));

        var board = p.Board.Select(m =>
        {
            var location = m.Type == "LOCATION";
            var stats = location ? $"{m.Health} charge(s)" : $"{m.Attack} / {m.Health}{(m.Health < m.MaxHealth ? $" (max {m.MaxHealth})" : "")}";
            var buff = !location && m.BaseAttack is int ba && m.BaseHealth is int bh && (ba != m.Attack || bh != m.MaxHealth) ? $" · base {ba}/{bh}" : "";
            var (state, color) = location
                ? m.State.ServerAllowsAction switch
                {
                    true => ("activable", Good),
                    false => ("non activable", Muted),
                    null => (m.State.Exhausted || m.State.LocationCooldown > 0 ? "en recharge" : "prêt", Muted),
                }
                : m.State.ServerAllowsAction switch
                {
                    true => ("peut attaquer", Good),
                    false => ("ne peut pas attaquer", Muted),
                    null => (m.State.Frozen ? "gelé" : m.State.Exhausted ? "épuisé" : "", Muted),
                };
            return new MinionVm(
                m.Name ?? "?",
                stats + buff,
                string.Join(" ", m.Keywords.Select(KeywordFr)),
                m.Enchantments.Count == 0 ? "" : "✚ " + string.Join(", ", m.Enchantments.Select(e => e.Name ?? "?")),
                state,
                color,
                $"{m.CardId} · entité #{m.EntityId} · mise à jour rév. {m.UpdatedAtRevision}\n{m.Text}");
        }).ToList();

        var hand = p.Hand.Cards.Select(c =>
        {
            if (c.Knowledge == Visibility.Own)
            {
                var cost = c.Cost?.ToString() ?? "?";
                var color = c.CostModified ? (c.Cost < c.BaseCost ? Good : Bad) : Text;
                var stats = c.Attack is int a && c.Health is int hh ? $"{a}/{hh}" + (c.BaseAttack is int ba2 && ba2 != a ? $" (base {ba2}/{c.BaseHealth})" : "") : "";
                var notes = string.Join(" · ", new[]
                {
                    stats.Length > 0 ? stats : null,
                    c.CostModified ? $"coût de base {c.BaseCost}" : null,
                    c.PoweredUp == true ? "✦ condition remplie" : null,
                    c.Created == true ? "créée" : null,
                    c.Enchantments.Count > 0 ? "✚ " + string.Join(", ", c.Enchantments.Select(e => e.Name)) : null,
                }.Where(x => x is not null));
                return new HandVm(cost, color, c.Name ?? "?", notes, $"{c.CardId} · entité #{c.EntityId}\n{c.Text}");
            }
            var name = c.CardId is null ? "Carte cachée" : c.Name ?? "?";
            var details = c.CardId is null
                ? (c.Hypothesis is null ? $"en main depuis TURN={c.EnteredTurn}" : $"hypothèse : {c.Hypothesis}")
                : c.Knowledge == Visibility.PublicSeenStale ? "vue publiquement, peut avoir changé" : "vue publiquement";
            return new HandVm("·", Muted, name, details, $"entité #{c.EntityId}");
        }).ToList();

        var d = p.Deck;
        var deckLine = $"Deck : {d.Count} carte(s)" + (d.DecklistKnown ? $" · liste « {d.DecklistSource} »" : p.Side == "local" ? " · liste inconnue" : "") + (d.Warning is null ? "" : " · ⚠ " + d.Warning);
        var deckDetails = d.DecklistKnown
            ? "Restantes : " + string.Join(", ", d.KnownRemaining.Select(k => $"{k.Name}{(k.Count > 1 ? " ×" + k.Count : "")}"))
            : d.KnownAdded.Count > 0 ? "Connues : " + string.Join(", ", d.KnownAdded.Select(k => k.Name)) : "";
        var extra = string.Join(" · ", new[]
        {
            p.Secrets.Count > 0 ? "Secrets : " + string.Join(", ", p.Secrets.Select(x => x.Known ? x.Name : $"inconnu ({ClassFr(x.CardClass)})")) : null,
            p.PublicSecretZone.Count > 0 ? "Quêtes/effets : " + string.Join(", ", p.PublicSecretZone.Select(x => x.Name)) : null,
            p.Graveyard.Count > 0 ? $"Cimetière : {p.Graveyard.Count}" : null,
            p.Corpses > 0 ? $"Cadavres : {p.Corpses}" : null,
        }.Where(x => x is not null));
        var lastTurn = p.LastTurn is { } lt
            ? $"Dernier tour (TURN={lt.GameTurn}) : " + (lt.CardsPlayed.Count == 0 ? "aucune carte jouée" : string.Join(", ", lt.CardsPlayed.Select(x => x.Name + (x.Note is null ? "" : $" ({x.Note})"))))
            : "";

        return new PlayerVm
        {
            Title = $"{title} · {ClassFr(p.HeroClass)}",
            HeroLine = heroLine,
            ManaLine = mana,
            EquipmentLine = equip,
            Board = board,
            HandTitle = $"Main ({p.Hand.Count})",
            Hand = hand,
            DeckLine = deckLine,
            DeckDetails = deckDetails,
            ExtraLine = extra,
            LastTurnLine = lastTurn,
        };
    }

    // ------------------------------------------------------------------ Libellés français

    private static string Mode(string? t) => t switch
    {
        "GT_RANKED" => "Classé",
        "GT_CASUAL" => "Partie libre",
        "GT_VS_AI" => "Contre l'IA",
        "GT_VS_FRIEND" => "Amical",
        "GT_ARENA" => "Arène",
        null => "?",
        _ => t,
    };

    private static string Format(string? f) => f switch
    {
        "FT_WILD" => "Libre",
        "FT_STANDARD" => "Standard",
        "FT_CLASSIC" => "Classique",
        "FT_TWIST" => "Twist",
        null => "",
        _ => f,
    };

    private static string Result(string? r) => r switch
    {
        "WON" => "victoire",
        "LOST" => "défaite",
        "TIED" => "égalité",
        "CONCEDED" => "abandon",
        _ => "?",
    };

    private static string Step(string? s) => s switch
    {
        "BEGIN_MULLIGAN" => "mulligan",
        "MAIN_ACTION" => "phase d'action",
        "MAIN_COMBAT" => "combat",
        "MAIN_START" or "MAIN_START_TRIGGERS" or "MAIN_READY" => "début de tour",
        "MAIN_END" or "MAIN_CLEANUP" or "MAIN_NEXT" => "fin de tour",
        "FINAL_GAMEOVER" => "fin de partie",
        null => "?",
        _ => s.ToLowerInvariant(),
    };

    private static string KindFr(string kind) => kind switch
    {
        "play" => "Jouer",
        "attack" => "Attaquer avec",
        "hero-power" => "Pouvoir héroïque",
        "location" => "Activer",
        _ => kind,
    };

    public static string ClassFr(string? c) => c switch
    {
        "WARRIOR" => "Guerrier",
        "PRIEST" => "Prêtre",
        "ROGUE" => "Voleur",
        "MAGE" => "Mage",
        "HUNTER" => "Chasseur",
        "PALADIN" => "Paladin",
        "SHAMAN" => "Chaman",
        "WARLOCK" => "Démoniste",
        "DRUID" => "Druide",
        "DEMONHUNTER" => "Chasseur de démons",
        "DEATHKNIGHT" => "Chevalier de la mort",
        "NEUTRAL" => "Neutre",
        null => "?",
        _ => c,
    };

    private static string KeywordFr(string k) => k switch
    {
        "TAUNT" => "[Provocation]",
        "DIVINE_SHIELD" => "[Bouclier divin]",
        "CHARGE" => "[Charge]",
        "RUSH" => "[Ruée]",
        "WINDFURY" => "[Furie des vents]",
        "STEALTH" => "[Camouflage]",
        "POISONOUS" => "[Toxicité]",
        "VENOMOUS" => "[Venimeux]",
        "LIFESTEAL" => "[Vol de vie]",
        "REBORN" => "[Réincarnation]",
        "FROZEN" => "[Gelé]",
        "IMMUNE" => "[Insensible]",
        "DORMANT" => "[En sommeil]",
        "SILENCED" => "[Réduit au silence]",
        "ELUSIVE" => "[Insaisissable]",
        "DEATHRATTLE" => "[Râle d'agonie]",
        "CANT_ATTACK" => "[Ne peut pas attaquer]",
        "CANT_BE_TARGETED_BY_SPELLS" or "CANT_BE_TARGETED_BY_HERO_POWERS" or "TRIGGER_VISUAL" or "AURA" => "",
        _ when k.StartsWith("SPELLPOWER+", StringComparison.Ordinal) => $"[Dégâts des sorts +{k[11..]}]",
        _ => $"[{k}]",
    };
}
