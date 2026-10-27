namespace HsCopilot.Core.Engine;

public enum LocalPlayerEvidence
{
    /// <summary>Joueur local inconnu.</summary>
    None = 0,
    /// <summary>Déduit : seul ce joueur a des cartes identifiées en main.</summary>
    HandHeuristic = 1,
    /// <summary>Le serveur a envoyé des options jouables pour les entités de ce joueur (signal fort).</summary>
    ServerOptions = 2,
    /// <summary>Le client a envoyé un choix ou une option pour ce joueur (signal fort).</summary>
    SentByClient = 3,
    /// <summary>Nom de joueur configuré par l'utilisateur.</summary>
    Configured = 4,
}

public sealed class PlayerSlot
{
    internal readonly HashSet<string> Aliases = new(StringComparer.Ordinal);

    public required int PlayerId { get; init; }
    public int EntityId { get; internal set; }
    /// <summary>Nom le plus précis connu (BattleTag complet si disponible).</summary>
    public string? Name { get; internal set; }
    public IReadOnlyCollection<string> KnownNames => Aliases;

    public override string ToString() => $"P{PlayerId} (entité {EntityId}) {Name}";
}

public sealed class GameMeta
{
    public int? BuildNumber { get; internal set; }
    public string? GameType { get; internal set; }
    public string? FormatType { get; internal set; }
    public int? ScenarioId { get; internal set; }
    public long? GameSeed { get; internal set; }
    public TimeSpan? CreatedAt { get; internal set; }
}

public sealed class OptionTarget
{
    public required int EntityId { get; init; }
    public required string Error { get; init; }
    public bool Playable => Error == "NONE";
}

public sealed class OptionEntry
{
    public required int Index { get; init; }
    public required string Type { get; init; }
    public int? EntityId { get; init; }
    public required string Error { get; init; }
    public string? ErrorParam { get; init; }
    public List<OptionTarget> Targets { get; } = new();
    public List<OptionEntry> SubOptions { get; } = new();
    public bool Playable => Error == "NONE";
}

/// <summary>Bloc "DebugPrintOptions" : actions légales envoyées par le serveur au joueur local.</summary>
public sealed class OptionsSet
{
    public required int Id { get; init; }
    public required TimeSpan Time { get; init; }
    public int Revision { get; internal set; }
    public List<OptionEntry> Options { get; } = new();
}

/// <summary>Choix proposé (mulligan, Découverte, Rembobinage…).</summary>
public sealed class ChoiceState
{
    public required int Id { get; init; }
    public required string ChoiceType { get; init; }
    public required string PlayerName { get; init; }
    public int? PlayerId { get; internal set; }
    public int CountMin { get; init; }
    public int CountMax { get; init; }
    public int? SourceId { get; internal set; }
    public List<int> Offered { get; } = new();
    public List<int>? Chosen { get; internal set; }
    public bool Resolved => Chosen is not null;
    public int Revision { get; internal set; }
    public TimeSpan Time { get; init; }
}

public enum HistoryKind
{
    TurnStart,
    Mulligan,
    Play,
    HeroPower,
    LocationActivation,
    Attack,
    Trigger,
    Power,
    Deaths,
    Fatigue,
    Choice,
    Rewind,
    GameEnd,
    Other,
}

/// <summary>Action observée, en ordre chronologique.</summary>
public sealed class HistoryEntry
{
    public required int Seq { get; init; }
    public required HistoryKind Kind { get; init; }
    public required int GameTurn { get; init; }
    public required TimeSpan Time { get; init; }
    public required int Revision { get; init; }
    /// <summary>Joueur à l'origine (PlayerId), si connu.</summary>
    public int? PlayerId { get; internal set; }
    public int? SourceId { get; init; }
    public int? TargetId { get; init; }
    public string? BlockType { get; init; }
    public List<int> Entities { get; } = new();
    public List<int> SecondaryEntities { get; } = new();
    public string? Detail { get; internal set; }
    /// <summary>Action annulée par un Rembobinage (le serveur a rétabli l'état antérieur).</summary>
    public bool Rewound { get; internal set; }
    /// <summary>Zone de la source au début du bloc (HAND pour une carte jouée depuis la main).</summary>
    public string? SourceZone { get; init; }
    /// <summary>Position (ZONE_POSITION) de la source au début du bloc.</summary>
    public int? SourcePositionAtStart { get; init; }
}

/// <summary>Option envoyée par le client (action réellement effectuée par le joueur local).</summary>
public sealed class SentOption
{
    public required TimeSpan Time { get; init; }
    public required int GameTurn { get; init; }
    public required int OptionIndex { get; init; }
    public int SubOption { get; init; }
    public int TargetId { get; init; }
    public int Position { get; init; }
    public int? OptionsId { get; init; }
    public int? EntityId { get; init; }
    public string? OptionType { get; init; }
}

public sealed class EngineWarning
{
    public required string Code { get; init; }
    public string? Detail { get; internal set; }
    public int Count { get; internal set; }
    public long FirstLine { get; init; }
}
