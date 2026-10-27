using System.Text.Json;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.State;

namespace HsCopilot.Cli;

/// <summary>
/// Statistiques reconstruites par le moteur à chaque point de décision (début d'un bloc d'options), une ligne JSON par point :
/// numéro de ligne dans les journaux concaténés, révision, serviteurs et héros tels que projetés.
/// Sert à les confronter à une relecture indépendante des lignes brutes du Power.log.
/// </summary>
public static class StatsDumpCommand
{
    public static int Run(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Usage : stats-dump <journal1> [journal2…] --out fichier.jsonl");
        var (_, cards) = Commands.LoadEnvironment(a, quiet: true);
        var lines = a.Positional.SelectMany(ReadShared).ToList();
        var engine = new GameEngine(new EngineOptions { Cards = cards });
        var projector = new GameStateProjector(cards);
        var output = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            var p = PowerLogLineParser.TryParse(lines[i]);
            if (p is null) continue;
            // Point de décision : l'état juste avant le bloc d'options du serveur.
            if (p.Method == "DebugPrintOptions" && p.Body.StartsWith("id=", StringComparison.Ordinal) && engine.HasGame && engine.LocalPlayerId is not null)
            {
                var s = projector.Project(engine, new ProjectionContext { Transport = "audit" });
                object Side(PlayerView? v) => new
                {
                    hero = v?.Hero is { } h ? new { id = h.EntityId, health = h.Health, armor = h.Armor, attack = h.Attack, frozen = h.Frozen } : null,
                    board = (v?.Board ?? new()).Where(m => m.Type == "MINION").Select(m => new { id = m.EntityId, name = m.Name, attack = m.Attack, health = m.Health, maxHealth = m.MaxHealth, damage = m.Damage, keywords = m.Keywords, text = m.Text }).ToList(),
                    weapon = v?.Weapon is { } w ? new { name = w.Name, attack = w.Attack, durability = w.Durability } : null,
                    heroPower = v?.HeroPower is { } hp ? new { name = hp.Name, cost = hp.Cost, text = hp.Text } : null,
                    hand = v?.Hand.Count,
                    secrets = v?.Secrets.Count,
                    mana = v?.Mana.Max,
                };
                output.Add(JsonSerializer.Serialize(new { line = i, time = p.Time.ToString(@"hh\:mm\:ss\.fffffff"), game = s.GameId, revision = s.Revision, turn = s.Game.GameTurn, local = Side(s.Local), opponent = Side(s.Opponent) }));
            }
            engine.Process(p);
        }
        File.WriteAllLines(a.Get("out") ?? "stats.jsonl", output);
        Console.WriteLine($"{output.Count} points de décision écrits.");
        return 0;
    }

    private static List<string> ReadShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var r = new StreamReader(fs, System.Text.Encoding.UTF8);
        var result = new List<string>();
        string? line;
        while ((line = r.ReadLine()) is not null) result.Add(line);
        return result;
    }
}
