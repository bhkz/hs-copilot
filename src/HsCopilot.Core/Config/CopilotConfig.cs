using System.Text.Json;
using HsCopilot.Core.State;
using HsCopilot.Core.Util;

namespace HsCopilot.Core.Config;

public sealed class DeckConfig
{
    public string Name { get; set; } = "";
    public string? Class { get; set; }
    /// <summary>Identifiants de cartes (CardID) et nombre d'exemplaires.</summary>
    public Dictionary<string, int> Cards { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Configuration de l'application (fichier JSON, voir config/copilot.example.json).</summary>
public sealed class CopilotConfig
{
    /// <summary>BattleTag du joueur local (facultatif : il est normalement déduit du log).</summary>
    public string? LocalPlayerName { get; set; }
    /// <summary>Dossier "Logs" de Hearthstone (contient un sous-dossier par session de jeu).</summary>
    public string HearthstoneLogsDir { get; set; } = @"C:\Program Files (x86)\Hearthstone\Logs";
    /// <summary>Base de cartes explicite (HearthstoneJSON cards.json ou cache HsCopilot). Vide : CardDefs de HDT.</summary>
    public string? CardDbPath { get; set; }
    public string? HdtCardDefsDir { get; set; }
    /// <summary>
    /// Horloge de l'affichage : lire aussi les lignes PowerTaskList du Power.log pour savoir quand Hearthstone affiche ton
    /// tour (le serveur le commence parfois 10 s plus tôt contre l'IA). Ne sert qu'à l'affichage des conseils.
    /// </summary>
    public bool VisualClockFromLog { get; set; } = true;
    /// <summary>auto (plugin HDT, sinon lecture du log), hdt, log ou none.</summary>
    public string Source { get; set; } = "auto";
    /// <summary>Délai d'attente du plugin HDT avant de basculer sur la lecture directe du log, en mode auto.</summary>
    public int AutoFallbackSeconds { get; set; } = 5;
    /// <summary>Dossier des journaux bruts (une copie des lignes reçues par partie). Vide : %LocalAppData%\HsCopilot\sessions.</summary>
    public string? JournalDir { get; set; }
    public bool JournalEnabled { get; set; } = true;
    /// <summary>Rapports de validation automatiques (un par partie). Vide : %LocalAppData%\HsCopilot\reports.</summary>
    public string? ReportsDir { get; set; }
    public bool ReportsEnabled { get; set; } = true;
    /// <summary>Nom du named pipe local (le plugin utilise toujours la valeur par défaut ; modifiable pour les tests).</summary>
    public string PipeName { get; set; } = HsCopilot.Shared.PipeProtocol.PipeName;
    public List<DeckConfig> Decks { get; set; } = new();
    /// <summary>Deck utilisé quand HDT ne fournit pas le deck actif (lecture directe du log, rejeu).</summary>
    public string? ActiveDeck { get; set; }
    /// <summary>Conseiller stratégique en direct (modèle, effort, plafonds, overlay).</summary>
    public AdvisorSettings Advisor { get; set; } = new();

    public static string DefaultDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HsCopilot");

    public string EffectiveReportsDir => string.IsNullOrWhiteSpace(ReportsDir) ? Path.Combine(DefaultDataDir, "reports") : ReportsDir!;

    public string EffectiveJournalDir => string.IsNullOrWhiteSpace(JournalDir) ? Path.Combine(DefaultDataDir, "sessions") : JournalDir!;

    public Decklist? GetActiveDecklist()
    {
        var deck = Decks.FirstOrDefault(d => string.Equals(d.Name, ActiveDeck, StringComparison.OrdinalIgnoreCase)) ?? (Decks.Count == 1 ? Decks[0] : null);
        if (deck is null || deck.Cards.Count == 0) return null;
        return new Decklist { Source = "configuration", Name = deck.Name, Cards = deck.Cards };
    }

    /// <summary>Recherche : chemin explicite, variable HSCOPILOT_CONFIG, copilot.json du dossier courant, puis %LocalAppData%\HsCopilot\copilot.json.</summary>
    public static (CopilotConfig Config, string? Path) Load(string? explicitPath = null)
    {
        var candidates = new[]
        {
            explicitPath,
            Environment.GetEnvironmentVariable("HSCOPILOT_CONFIG"),
            Path.Combine(Environment.CurrentDirectory, "copilot.json"),
            Path.Combine(DefaultDataDir, "copilot.json"),
        };
        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c) || !File.Exists(c)) continue;
            var cfg = JsonSerializer.Deserialize<CopilotConfig>(File.ReadAllText(c), Json.Read) ?? new CopilotConfig();
            return (cfg, c);
        }
        if (!string.IsNullOrWhiteSpace(explicitPath)) throw new FileNotFoundException("Configuration introuvable", explicitPath);
        return (new CopilotConfig(), null);
    }

    /// <summary>Enregistre la configuration (par défaut dans %LocalAppData%\HsCopilot\copilot.json) et renvoie le chemin écrit.</summary>
    public string Save(string? path = null)
    {
        path ??= Path.Combine(DefaultDataDir, "copilot.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json.Pretty), new System.Text.UTF8Encoding(false));
        File.Move(tmp, path, overwrite: true);
        return path;
    }
}
