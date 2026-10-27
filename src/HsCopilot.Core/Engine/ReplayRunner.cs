using HsCopilot.Core.Logs;

namespace HsCopilot.Core.Engine;

/// <summary>Points d'arrêt utiles pour rejouer un enregistrement.</summary>
public static class ReplayRunner
{
    /// <summary>
    /// Rejoue jusqu'au « début de décision » du tour <paramref name="playerTurn"/> du joueur local :
    /// fin du premier bloc d'options légales reçu pendant ce tour. Renvoie l'index (base 0) de la prochaine ligne non traitée,
    /// ou -1 si le point n'a pas été atteint (toutes les lignes ont alors été traitées).
    /// </summary>
    public static int RunUntilLocalTurn(GameEngine engine, IReadOnlyList<string> lines, int playerTurn, int startIndex = 0)
    {
        var inTargetOptions = false;
        for (int i = startIndex; i < lines.Count; i++)
        {
            var parsed = PowerLogLineParser.TryParse(lines[i]);
            if (parsed is null) continue;
            if (inTargetOptions && parsed.Method != "DebugPrintOptions") return i;
            engine.Process(parsed);
            if (parsed.Method == "DebugPrintOptions" && parsed.Body.StartsWith("id=", StringComparison.Ordinal)
                && (engine.GameTurn + 1) / 2 == playerTurn && engine.LocalPlayerId is not null && engine.CurrentPlayerId == engine.LocalPlayerId)
                inTargetOptions = true;
        }
        return inTargetOptions ? lines.Count : -1;
    }

    /// <summary>Traite les lignes [startIndex, endExclusive).</summary>
    public static void RunRange(GameEngine engine, IReadOnlyList<string> lines, int startIndex, int endExclusive)
    {
        for (int i = startIndex; i < Math.Min(endExclusive, lines.Count); i++) engine.Process(lines[i]);
    }

    /// <summary>Index des lignes où commence un tour (changement du tag TURN), avec le numéro de tour de jeu.</summary>
    public static List<(int LineIndex, int GameTurn)> FindTurnStarts(IReadOnlyList<string> lines)
    {
        var result = new List<(int, int)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var p = l.IndexOf("Entity=GameEntity tag=TURN value=", StringComparison.Ordinal);
            if (p < 0 || !l.Contains("GameState.", StringComparison.Ordinal)) continue;
            if (int.TryParse(PowerStatements.FirstToken(l[(p + 33)..]), out var t)) result.Add((i, t));
        }
        return result;
    }
}
