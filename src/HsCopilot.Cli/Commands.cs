using System.Text.Json;
using System.Text.RegularExpressions;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;
using HsCopilot.Core.Verification;

namespace HsCopilot.Cli;

public sealed class Args
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public Args(string[] args)
    {
        var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--", StringComparison.Ordinal))
            {
                var key = a[2..];
                string? value = null;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal)) value = args[++i];
                _options[key] = value;
            }
            else positional.Add(a);
        }
        Command = positional.FirstOrDefault();
        Positional = positional.Skip(1).ToList();
    }

    public string? Command { get; }
    public List<string> Positional { get; }
    public bool Has(string key) => _options.ContainsKey(key);
    public string? Get(string key) => _options.TryGetValue(key, out var v) ? v : null;
    public int? GetInt(string key) => int.TryParse(Get(key), out var v) ? v : null;
}

public static class Commands
{
    public static (CopilotConfig Config, ICardDatabase Cards) LoadEnvironment(Args a, bool quiet = false)
    {
        var (cfg, path) = CopilotConfig.Load(a.Get("config"));
        var (db, message) = CardDbLocator.LoadBest(a.Get("cards") ?? cfg.CardDbPath, cfg.HdtCardDefsDir);
        if (!quiet)
        {
            Console.Error.WriteLine($"Configuration : {path ?? "(défaut)"}");
            Console.Error.WriteLine($"Cartes : {message}");
        }
        return (cfg, db);
    }

    private static string RequireFile(Args a, int index = 0)
    {
        if (a.Positional.Count <= index) throw new ArgumentException("Fichier manquant.");
        var p = a.Positional[index];
        if (!File.Exists(p)) throw new FileNotFoundException("Fichier introuvable", p);
        return p;
    }

    internal static Decklist? DeckFor(Args a, CopilotConfig cfg)
    {
        if (a.Get("deck") is string name)
        {
            var d = cfg.Decks.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Deck « {name} » absent de la configuration.");
            return new Decklist { Source = "configuration", Name = d.Name, Cards = d.Cards };
        }
        return a.Has("no-deck") ? null : cfg.GetActiveDecklist();
    }

    public static int Games(Args a)
    {
        var file = RequireFile(a);
        var (cfg, cards) = LoadEnvironment(a);
        var lines = ReplayFile.ReadAllLines(file);
        var games = ReplayFile.SplitGames(lines);
        Console.WriteLine($"{lines.Count} lignes, {games.Count} partie(s)");
        foreach (var g in games)
        {
            var engine = new GameEngine(new EngineOptions { Cards = cards, LocalPlayerName = cfg.LocalPlayerName });
            foreach (var l in g.Lines) engine.Process(l);
            var snap = new GameStateProjector(cards).Project(engine, new ProjectionContext { Transport = "replay-file" });
            var local = engine.LocalPlayer;
            var opp = engine.OpponentPlayer;
            Console.WriteLine($"#{g.Index} lignes {g.StartLine + 1}-{g.EndLine} id={snap.GameId} {snap.Game.GameType}/{snap.Game.FormatType} " +
                              $"vous=P{local?.PlayerId}({snap.Local?.HeroClass}) vs P{opp?.PlayerId}({snap.Opponent?.HeroClass}) " +
                              $"tours={snap.Game.TurnNumber} premier={StateText.Side(snap.Game.FirstPlayer)} résultat={snap.Game.Result ?? "?"} " +
                              $"resets={engine.ResetCount} non-compris={engine.UnparsedCount} preuve-joueur={engine.LocalEvidence}");
        }
        return 0;
    }

    public static int State(Args a)
    {
        var file = RequireFile(a);
        var (cfg, cards) = LoadEnvironment(a);
        var lines = ReplayFile.ReadAllLines(file);
        var games = ReplayFile.SplitGames(lines);
        if (games.Count == 0) throw new InvalidDataException("Aucune ligne GameState dans le fichier.");
        var gameIndex = a.GetInt("game") ?? games[^1].Index;
        var game = games.FirstOrDefault(x => x.Index == gameIndex) ?? throw new ArgumentException($"Partie {gameIndex} introuvable.");

        var engine = new GameEngine(new EngineOptions { Cards = cards, LocalPlayerName = cfg.LocalPlayerName });
        var stopTurn = a.GetInt("turn");
        var stopLine = a.GetInt("line");
        if (stopTurn is int t)
        {
            if (ReplayRunner.RunUntilLocalTurn(engine, game.Lines, t) < 0)
                Console.Error.WriteLine($"Tour {t} du joueur local non atteint : état de fin de partie affiché.");
        }
        else
        {
            ReplayRunner.RunRange(engine, game.Lines, 0, stopLine ?? game.Lines.Count);
        }

        var snap = new GameStateProjector(cards).Project(engine, new ProjectionContext
        {
            Transport = "replay-file",
            TransportDetail = Path.GetFileName(file),
            Decklist = DeckFor(a, cfg),
        });
        Console.WriteLine(a.Has("json") ? JsonSerializer.Serialize(snap, Json.Pretty) : StateText.Render(snap, a.GetInt("history") ?? 20));
        return 0;
    }

    public static int Verify(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Fichier ou dossier manquant.");
        var target = a.Positional[0];
        var files = Directory.Exists(target)
            ? Directory.EnumerateFiles(target, "Power*.log", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(target, "*.hdtreplay", SearchOption.AllDirectories))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string> { RequireFile(a) };
        var (cfg, cards) = LoadEnvironment(a);
        var failures = 0;
        var total = 0;
        foreach (var file in files)
        {
            if (files.Count > 1) Console.WriteLine($"=== {file}");
            var lines = ReplayFile.ReadAllLines(file);
            var games = ReplayFile.SplitGames(lines);
            var (f, n) = VerifyGames(a, cfg, cards, games);
            failures += f;
            total += n;
        }
        if (files.Count > 1 || total > 1) Console.WriteLine($"Bilan : {total} partie(s), {failures} avec écart(s).");
        return failures == 0 ? 0 : 3;
    }

    private static (int Failures, int Total) VerifyGames(Args a, CopilotConfig cfg, ICardDatabase cards, List<LoggedGame> games)
    {
        var only = a.GetInt("game");
        var failures = 0;
        var total = 0;
        foreach (var g in games.Where(x => only is null || x.Index == only))
        {
            total++;
            var opts = new EngineOptions { Cards = cards, LocalPlayerName = cfg.LocalPlayerName };
            var engine = new GameEngine(opts);
            var oracle = ConsistencyOracle.Run(engine, g.Lines);
            var vis = VisibilityAudit.Run(g.Lines, cards, opts);
            var snap = new GameStateProjector(cards).Project(engine, new ProjectionContext { Transport = "replay-file", Decklist = DeckFor(a, cfg) });
            var endCheck = EndOfGameCheck(engine);
            var ok = oracle.Mismatches == 0 && oracle.ManaMismatches == 0 && oracle.TurnOwnerMismatches == 0 && engine.PendingPlayerNameChanges == 0
                     && vis.Violations == 0 && engine.UnparsedCount == 0
                     && !engine.Warnings.Any(w => w.Code == "PARSER_EXCEPTION") && endCheck.Ok;
            if (!ok) failures++;
            Console.WriteLine($"Partie #{g.Index} ({snap.GameId}, {snap.Game.GameType}) : {(ok ? "OK" : "ÉCARTS")}");
            Console.WriteLine($"  Oracle options/choix : {oracle.Checks} vues client sur {oracle.OptionBlocks} blocs d'options — identiques à l'état courant {oracle.ExactMatches}, " +
                              $"à un état récent {oracle.LaggedMatches} (retard max {oracle.MaxLagRevisions} rév.), écarts {oracle.Mismatches} (dont entités absentes {oracle.MissingEntities}) — accord {oracle.AgreementRate:P2}");
            Console.WriteLine($"  Répartition des retards (révisions:vues) : {string.Join(" ", oracle.LagHistogram.Select(kv => $"{kv.Key}:{kv.Value}"))}");
            foreach (var ex in oracle.Examples.Take(a.GetInt("examples") ?? 8)) Console.WriteLine("    " + ex);
            Console.WriteLine($"  Tour attribué au joueur local à chaque bloc d'options : {oracle.TurnOwnerChecks} contrôles, {oracle.TurnOwnerMismatches} écart(s) ; changements de joueur non appliqués (nom inconnu) : {engine.PendingPlayerNameChanges}");
            Console.WriteLine($"  Mana/coûts : {oracle.ManaChecks} cartes de la main jugées jouables ou non par le serveur, {oracle.ManaMismatches} désaccord(s) avec le coût et le mana reconstruits");
            foreach (var ex in oracle.ManaExamples.Take(8)) Console.WriteLine("    " + ex);
            Console.WriteLine($"  Fin de partie : {endCheck.Message}");
            Console.WriteLine($"  Visibilité : {vis.SnapshotsChecked} états projetés, {vis.OpponentHiddenSlotsChecked} emplacements adverses cachés vérifiés, {vis.Violations} violation(s)");
            foreach (var ex in vis.Examples.Take(8)) Console.WriteLine("    " + ex);
            Console.WriteLine($"  Fin : tour {snap.Game.TurnNumber}, premier joueur {StateText.Side(snap.Game.FirstPlayer)}, résultat {snap.Game.Result ?? "?"}, " +
                              $"lignes non comprises {engine.UnparsedCount}, resets {engine.ResetCount}, joueur local P{engine.LocalPlayerId} ({engine.LocalEvidence})");
            foreach (var w in engine.Warnings) Console.WriteLine($"    avertissement {w.Code} ×{w.Count} {w.Detail}");
            foreach (var u in engine.UnparsedSamples.Take(5)) Console.WriteLine("    " + u);
            if (snap.Local?.Deck.Warning is string dw) Console.WriteLine("    deck : " + dw);
        }
        return (failures, total);
    }

    /// <summary>Le perdant doit avoir 0 PV ou moins, sauf abandon.</summary>
    public static (bool Ok, string Message) EndOfGameCheck(GameEngine engine)
    {
        var states = engine.Players.Values.Select(p => (p, State: engine.PlayerEntity(p.PlayerId)?.GetTag("PLAYSTATE"))).ToList();
        if (states.All(s => s.State is not ("WON" or "LOST" or "TIED" or "CONCEDED"))) return (true, "partie non terminée dans ce log");
        var parts = new List<string>();
        var ok = true;
        foreach (var (p, state) in states)
        {
            var pe = engine.PlayerEntity(p.PlayerId);
            var heroId = pe?.GetInt("HERO_ENTITY") ?? 0;
            var hero = engine.Entities.GetValueOrDefault(heroId);
            var hp = hero is null ? (int?)null : hero.GetInt("HEALTH") - hero.GetInt("DAMAGE");
            var conceded = engine.ConcededPlayerIds.Contains(p.PlayerId) || state == "CONCEDED";
            // Contre l'IA, certains scénarios se terminent par un script (constaté : boss Xavius déclaré perdant avec 23 PV).
            var scripted = state == "LOST" && hp > 0 && !conceded && engine.Meta.GameType == "GT_VS_AI";
            parts.Add($"P{p.PlayerId} {state} PV={hp?.ToString() ?? "?"}{(conceded ? " (abandon)" : "")}{(scripted ? " (fin scénarisée contre l'IA)" : "")}");
            if (state == "LOST" && hp > 0 && !conceded && !scripted) ok = false;
            if (state == "WON" && hp <= 0) ok = false;
        }
        return (ok, (ok ? "cohérente : " : "INCOHÉRENTE : ") + string.Join(", ", parts));
    }

    public static int Anonymize(Args a)
    {
        var input = RequireFile(a);
        if (a.Positional.Count < 2) throw new ArgumentException("Fichier de sortie manquant.");
        var output = a.Positional[1];
        var lines = ReplayFile.ReadAllLines(input);
        var anon = Anonymizer.Anonymize(lines);
        if (a.GetInt("game") is int gi)
        {
            var game = ReplayFile.SplitGames(anon).FirstOrDefault(g => g.Index == gi) ?? throw new ArgumentException($"Partie {gi} introuvable.");
            anon = game.Lines.ToList();
        }
        if (a.Has("gamestate-only")) anon = anon.Where(l => l.Contains(" GameState.", StringComparison.Ordinal)).ToList();
        File.WriteAllLines(output, anon);
        Console.WriteLine($"{anon.Count} lignes écrites dans {output}");
        return 0;
    }

    public static int CardDb(Args a)
    {
        var (cfg, cards) = LoadEnvironment(a);
        Console.WriteLine($"{cards.Count} cartes, build {cards.Build}, langue {cards.Locale}, source {cards.Source}");
        if (a.Get("subset-of") is string file && a.Get("out") is string outPath && cards is CardDatabase db)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var rx = new Regex(@"(?:cardId|CardID)=([A-Za-z0-9_]+)");
            foreach (var path in file.Split(';'))
                foreach (var l in ReplayFile.ReadAllLines(path))
                    foreach (Match m in rx.Matches(l)) ids.Add(m.Groups[1].Value);
            foreach (var d in cfg.Decks) foreach (var id in d.Cards.Keys) ids.Add(id);
            var subset = new CardDatabase(db.All.Where(c => ids.Contains(c.Id)), db.Build, db.Locale, "sous-ensemble de " + db.Source);
            HearthstoneJsonLoader.SaveCache(subset, outPath);
            Console.WriteLine($"{subset.Count} cartes exportées dans {outPath} ({ids.Count} identifiants demandés)");
        }
        foreach (var id in a.Positional)
        {
            var c = cards.Get(id);
            Console.WriteLine(c is null ? $"{id} : inconnue" : $"{id} : {c.DisplayName} ({c.Type}, coût {c.Cost}, {c.Attack}/{c.Health}) {c.PlainText}");
        }
        return 0;
    }
}
