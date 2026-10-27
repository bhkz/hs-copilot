using System.Text.Json;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Evaluation;

/// <summary>Coup réellement joué après le point de décision (étiquette d'évaluation, jamais transmise au modèle).</summary>
public sealed class ActualMove
{
    public int OptionIndex { get; init; }
    public int SubOption { get; init; }
    public int TargetEntityId { get; init; }
    public int Position { get; init; }
    /// <summary>Identifiant de l'action légale correspondante, ou null si la correspondance est impossible.</summary>
    public string? ActionId { get; init; }
    public string? Label { get; init; }
    public string? Note { get; init; }
}

/// <summary>Un point de décision extrait d'une partie réelle.</summary>
public sealed class DecisionCase
{
    public required string CaseId { get; init; }
    public required string Source { get; init; }
    public int GameIndex { get; init; }
    /// <summary>Index (base 0) de la dernière ligne traitée par le moteur pour produire l'état : rien d'ultérieur n'y figure.</summary>
    public int LineIndex { get; init; }
    public required string GameId { get; init; }
    public int Revision { get; init; }
    public int GameTurn { get; init; }
    public int TurnNumber { get; init; }
    /// <summary>Rang de la décision dans le tour (1 = première décision du tour).</summary>
    public int DecisionInTurn { get; init; }
    public string? GameType { get; init; }
    public string? OpponentClass { get; init; }
    public List<string> Tags { get; init; } = new();
    /// <summary>État observable au moment de la décision (GameState v1).</summary>
    public required JsonElement Snapshot { get; init; }
    /// <summary>Identifiants cachés connus du moteur : servent uniquement au contrôle anti-fuite du message.</summary>
    public List<string> ForbiddenCardIds { get; init; } = new();
    /// <summary>Étiquette d'évaluation : coup joué ensuite. Jamais incluse dans le message envoyé au modèle.</summary>
    public ActualMove? Actual { get; init; }

    public GameStateSnapshot LoadSnapshot() =>
        Snapshot.Deserialize<GameStateSnapshot>(Json.Read) ?? throw new InvalidDataException($"état illisible : {CaseId}");
}

public sealed class DatasetOptions
{
    /// <summary>Nombre maximal de décisions retenues par tour (1 = début de tour seulement).</summary>
    public int DecisionsPerTurn { get; init; } = 2;
    /// <summary>Classe du joueur local exigée (null = toutes).</summary>
    public string? RequiredHeroClass { get; init; } = "WARRIOR";
    public int MaxCasesPerGame { get; init; } = 40;
    public Decklist? Decklist { get; init; }
}

/// <summary>Extraction déterministe des points de décision d'un ensemble de logs.</summary>
public static class DatasetBuilder
{
    public static List<DecisionCase> Build(IEnumerable<string> files, ICardDatabase cards, DatasetOptions options)
    {
        var cases = new List<DecisionCase>();
        foreach (var file in files)
        {
            var games = ReplayFile.SplitGames(ReplayFile.ReadAllLines(file));
            foreach (var g in games) cases.AddRange(BuildGame(SourceName(file), g, cards, options));
        }
        return cases;
    }

    public static IEnumerable<DecisionCase> BuildGame(string source, LoggedGame game, ICardDatabase cards, DatasetOptions options)
    {
        var engine = new GameEngine(new EngineOptions { Cards = cards });
        var projector = new GameStateProjector(cards);
        var perTurn = new Dictionary<int, int>();
        var produced = 0;
        var lines = game.Lines;
        for (int i = 0; i < lines.Count; i++)
        {
            var parsed = PowerLogLineParser.TryParse(lines[i]);
            if (parsed is null) continue;
            engine.Process(parsed);
            if (parsed.Method != "DebugPrintOptions") continue;
            var nextIsOption = i + 1 < lines.Count && lines[i + 1].Contains("GameState.DebugPrintOptions()", StringComparison.Ordinal);
            if (nextIsOption) continue;
            if (engine.LocalPlayerId is null || engine.CurrentPlayerId != engine.LocalPlayerId || !engine.OptionsFresh) continue;

            var snapshot = projector.Project(engine, new ProjectionContext { Transport = "replay-file", TransportDetail = source, Decklist = options.Decklist });
            if (options.RequiredHeroClass is string cls && snapshot.Local?.HeroClass != cls) yield break;
            var ctx = DecisionContextBuilder.Build(snapshot);
            if (ctx is null || ctx.LegalActions.Count(a => a.Kind != "end-turn") == 0) continue;
            var rank = perTurn.GetValueOrDefault(engine.GameTurn) + 1;
            perTurn[engine.GameTurn] = rank;
            if (rank > options.DecisionsPerTurn) continue;

            var actual = FindActual(lines, i + 1, engine.CurrentOptions!, ctx);
            produced++;
            yield return new DecisionCase
            {
                CaseId = $"{source}#{game.Index}@{i}",
                Source = source,
                GameIndex = game.Index,
                LineIndex = i,
                GameId = snapshot.GameId,
                Revision = snapshot.Revision,
                GameTurn = snapshot.Game.GameTurn,
                TurnNumber = snapshot.Game.TurnNumber,
                DecisionInTurn = rank,
                GameType = snapshot.Game.GameType,
                OpponentClass = snapshot.Opponent?.HeroClass,
                Tags = CaseTagger.Tags(ctx, rank),
                Snapshot = JsonSerializer.SerializeToElement(snapshot, Json.Compact),
                ForbiddenCardIds = snapshot.ForbiddenCardIds.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                Actual = actual,
            };
            if (produced >= options.MaxCasesPerGame) yield break;
        }
    }

    /// <summary>Nom court et unique d'un fichier : dossier parent + nom (les sessions Hearthstone ont toutes un Power_old.log).</summary>
    public static string SourceName(string file)
    {
        var dir = Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(file)) ?? "");
        if (dir.StartsWith("Hearthstone_", StringComparison.OrdinalIgnoreCase)) dir = dir["Hearthstone_".Length..];
        return string.IsNullOrEmpty(dir) ? Path.GetFileName(file) : $"{dir}/{Path.GetFileName(file)}";
    }

    /// <summary>
    /// Coup réellement joué : première ligne SendOption après le point de décision et avant tout nouveau bloc d'options.
    /// Seules les lignes postérieures sont lues ici, uniquement pour étiqueter le cas.
    /// </summary>
    public static ActualMove? FindActual(IReadOnlyList<string> lines, int start, OptionsSet options, DecisionContext ctx)
    {
        for (int j = start; j < lines.Count; j++)
        {
            var p = PowerLogLineParser.TryParse(lines[j]);
            if (p is null) continue;
            if (p.Method == "DebugPrintOptions" && p.Body.StartsWith("id=", StringComparison.Ordinal)) return null;
            if (p.Method == "DebugPrintPower" && p.Body == "CREATE_GAME") return null;
            if (p.Method != "SendOption" || !p.Body.StartsWith("selectedOption=", StringComparison.Ordinal)) continue;
            var opt = int.TryParse(PowerStatements.ReadField(p.Body, "selectedOption"), out var o) ? o : -1;
            var sub = int.TryParse(PowerStatements.ReadField(p.Body, "selectedSubOption"), out var so) ? so : -1;
            var target = int.TryParse(PowerStatements.ReadField(p.Body, "selectedTarget"), out var t) ? t : 0;
            var pos = int.TryParse(PowerStatements.ReadField(p.Body, "selectedPosition"), out var ps) ? ps : 0;
            var entry = options.Options.FirstOrDefault(x => x.Index == opt);
            string? id;
            if (entry?.Type == "END_TURN") id = $"o{ctx.OptionsId}.fin";
            else id = $"o{ctx.OptionsId}.{opt}" + (sub >= 0 ? $".s{sub}" : "") + (target > 0 ? $".t{target}" : "");
            var action = ctx.LegalActions.FirstOrDefault(a => a.Id == id);
            return new ActualMove
            {
                OptionIndex = opt,
                SubOption = sub,
                TargetEntityId = target,
                Position = pos,
                ActionId = action?.Id,
                Label = action?.Label,
                Note = action is null ? $"option {opt} (sous-option {sub}, cible {target}) absente des actions légales projetées" : null,
            };
        }
        return null;
    }

    public static void Save(IEnumerable<DecisionCase> cases, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        foreach (var c in cases) w.WriteLine(JsonSerializer.Serialize(c, Json.Compact));
    }

    public static List<DecisionCase> Load(string path) =>
        File.ReadLines(path).Where(l => l.Length > 0).Select(l => JsonSerializer.Deserialize<DecisionCase>(l, Json.Read)!).ToList();

    /// <summary>
    /// Sélection déterministe et stratifiée : parties classées (PvP réel), létal disponible, menace adverse, cartes clés
    /// du deck, puis complément pseudo-aléatoire stable (empreinte de l'identifiant). Plafond par partie pour couvrir
    /// un maximum de parties ; les cas dont le coup réel est inconnu passent en dernier.
    /// </summary>
    public static List<DecisionCase> Select(IReadOnlyList<DecisionCase> cases, int limit)
    {
        if (limit <= 0 || limit >= cases.Count) return cases.OrderBy(c => c.CaseId, StringComparer.Ordinal).ToList();
        var ordered = cases.OrderBy(c => c.Actual?.ActionId is null ? 1 : 0).ThenBy(c => PromptBuilder.Fingerprint(c.CaseId), StringComparer.Ordinal).ToList();
        var chosen = new List<DecisionCase>();
        var perGame = new Dictionary<(string, int), int>();
        var cap = Math.Max(2, (int)Math.Ceiling(limit / 8.0));

        bool TryAdd(DecisionCase c, bool enforceCap)
        {
            if (chosen.Count >= limit || chosen.Contains(c)) return false;
            var key = (c.Source, c.GameIndex);
            if (enforceCap && perGame.GetValueOrDefault(key) >= cap) return false;
            chosen.Add(c);
            perGame[key] = perGame.GetValueOrDefault(key) + 1;
            return true;
        }
        void Fill(Func<DecisionCase, bool> predicate, int quota)
        {
            var added = 0;
            foreach (var c in ordered.Where(predicate))
            {
                if (added >= quota) break;
                if (TryAdd(c, enforceCap: true)) added++;
            }
        }

        Fill(c => c.GameType == "GT_RANKED", (int)Math.Ceiling(limit * 0.3));
        Fill(c => c.Tags.Contains("létal-attaques"), Math.Max(2, limit / 10));
        Fill(c => c.Tags.Contains("menace-létale-adverse"), Math.Max(1, limit / 15));
        foreach (var tag in new[] { "grommash-jouable", "profondeurs-prêtes", "chronoseigneur-jouable", "wyrm-jouable", "rembobinage-jouable" })
            Fill(c => c.Tags.Contains(tag), Math.Max(1, limit / 15));
        Fill(_ => true, limit);
        foreach (var c in ordered) TryAdd(c, enforceCap: false);
        return chosen.OrderBy(c => c.CaseId, StringComparer.Ordinal).ToList();
    }
}

/// <summary>Étiquettes descriptives d'un cas (calculées sur l'état observable uniquement).</summary>
public static class CaseTagger
{
    public static List<string> Tags(DecisionContext ctx, int rank)
    {
        var tags = new List<string> { rank == 1 ? "début-de-tour" : "milieu-de-tour" };
        var s = ctx.State;
        var face = BoardMath.FaceDamageAvailable(ctx);
        var oppHero = s.Opponent?.Hero;
        if (oppHero is not null && face > 0 && face >= oppHero.Health + oppHero.Armor) tags.Add("létal-attaques");
        else if (oppHero is not null && BoardMath.BestHandFaceDamage(ctx) is { Damage: > 0 } hand && face + hand.Damage >= oppHero.Health + oppHero.Armor)
            tags.Add("létal-avec-main");
        if (BoardMath.OpponentBoardDamage(s) is var threat && s.Local?.Hero is { } me && threat >= me.Health + me.Armor && threat > 0) tags.Add("menace-létale-adverse");
        bool Playable(string cardId, string kind) => ctx.LegalActions.Any(a => a.SourceCardId == cardId && a.Kind == kind);
        if (Playable("CORE_EX1_414", "play")) tags.Add("grommash-jouable");
        if (Playable("CORE_REV_990", "location")) tags.Add("profondeurs-prêtes");
        if (Playable("TIME_714", "play")) tags.Add("chronoseigneur-jouable");
        if (Playable("TLC_600", "play")) tags.Add("wyrm-jouable");
        if (Playable("TIME_003", "play") || Playable("TIME_034", "play")) tags.Add("rembobinage-jouable");
        return tags;
    }
}
