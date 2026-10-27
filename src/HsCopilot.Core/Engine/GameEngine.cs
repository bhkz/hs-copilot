using HsCopilot.Core.Cards;
using HsCopilot.Core.Logs;
using static HsCopilot.Core.Logs.PowerStatements;

namespace HsCopilot.Core.Engine;

public sealed class EngineOptions
{
    /// <summary>Base de cartes, utilisée seulement pour savoir si une entité en zone SECRET est un vrai Secret.</summary>
    public ICardDatabase? Cards { get; init; }
    /// <summary>BattleTag (ou nom court) du joueur local, s'il est configuré.</summary>
    public string? LocalPlayerName { get; init; }
    /// <summary>Révision de départ : permet de garder des révisions strictement croissantes quand l'hôte recrée le moteur (changement de source).</summary>
    public int InitialRevision { get; init; }
}

/// <summary>
/// Moteur d'état : applique les lignes GameState du Power.log, dans l'ordre, et maintient l'état complet
/// de la partie tel que le client l'a reçu.
/// </summary>
/// <remarks>
/// Source de vérité unique : la séquence de lignes GameState, quelle que soit la façon dont elle arrive
/// (plugin HDT, lecture directe du fichier, rejeu d'un enregistrement). Le moteur est déterministe :
/// la même séquence produit toujours le même état et les mêmes numéros de révision.
/// Il ne lève jamais d'exception pour une ligne inattendue : il l'ignore et l'enregistre dans <see cref="Warnings"/>.
/// </remarks>
public sealed class GameEngine
{
    private enum ContextKind { None, GameEntity, Player, Create, FullUpdate, Show, Change }

    private sealed class Block
    {
        public required string Type { get; init; }
        public int? SourceId { get; init; }
        public int? TargetId { get; init; }
        public HistoryEntry? History { get; set; }
        public bool IsReset { get; init; }
    }

    private const string PlaceholderName = "UNKNOWN HUMAN PLAYER";
    private const string CoinCardId = "GAME_005";

    private readonly EngineOptions _opt;
    private readonly Dictionary<int, EntityState> _entities = new();
    private readonly Dictionary<int, PlayerSlot> _players = new();
    private readonly List<Block> _blocks = new();
    private readonly List<HistoryEntry> _history = new();
    private readonly Dictionary<string, EngineWarning> _warnings = new(StringComparer.Ordinal);
    private readonly Dictionary<int, ChoiceState> _choices = new();
    private readonly List<SentOption> _sentOptions = new();
    private readonly Dictionary<int, int> _turnOwner = new();
    private readonly List<string> _unparsedSamples = new();
    private readonly List<(string Name, string Tag, string Value)> _pendingNameChanges = new();
    private readonly HashSet<int> _conceded = new();

    private int? _tagContext;
    private ContextKind _contextKind;
    private bool _changedSinceBoundary;
    private OptionsSet? _options;
    private OptionEntry? _lastOption;
    private OptionEntry? _lastSubOption;
    private ChoiceState? _printingChoice;
    private ChoiceState? _printingChosen;
    private int? _sendChoicesId;
    private bool _inReset;
    private HashSet<int>? _resetSeen;
    private bool _mulliganStarted;
    private int _historySeq;
    private string? _gameId;

    public GameEngine(EngineOptions? options = null)
    {
        _opt = options ?? new EngineOptions();
        Revision = _opt.InitialRevision;
    }

    // ---------------------------------------------------------------- API de lecture

    /// <summary>Identifiant stable de la partie : heure de création + graine (tag GAME_SEED).</summary>
    public string? GameId => _gameId;
    public bool HasGame { get; private set; }
    /// <summary>Nombre de parties commencées depuis la création du moteur.</summary>
    public int GameNumber { get; private set; }
    /// <summary>Révision de l'état. Strictement croissante pendant toute la vie du moteur (jamais remise à zéro).</summary>
    public int Revision { get; private set; }
    public long LinesInGame { get; private set; }
    public long TotalLines { get; private set; }
    /// <summary>Vrai si la première ligne reçue n'était pas CREATE_GAME : le début de partie manque.</summary>
    public bool JoinedMidGame { get; private set; }
    public GameMeta Meta { get; private set; } = new();
    public IReadOnlyDictionary<int, EntityState> Entities => _entities;
    public IReadOnlyDictionary<int, PlayerSlot> Players => _players;
    public int GameEntityId { get; private set; } = 1;
    public int? LocalPlayerId { get; private set; }
    public LocalPlayerEvidence LocalEvidence { get; private set; }
    public OptionsSet? CurrentOptions => _options;
    /// <summary>Les options légales correspondent à la révision courante (aucun changement d'état depuis leur réception).</summary>
    public bool OptionsFresh => _options is not null && _options.Revision == Revision;
    public IReadOnlyList<HistoryEntry> History => _history;
    public IReadOnlyCollection<EngineWarning> Warnings => _warnings.Values;
    public IEnumerable<ChoiceState> Choices => _choices.Values.OrderBy(c => c.Id);
    public IReadOnlyList<SentOption> SentOptions => _sentOptions;
    public IReadOnlyDictionary<int, int> TurnOwners => _turnOwner;
    public int UnparsedCount { get; private set; }
    public IReadOnlyList<string> UnparsedSamples => _unparsedSamples;
    public int ResetCount { get; private set; }
    /// <summary>Changements de tag en attente parce que le nom de joueur qu'ils citent n'a pas pu être associé.</summary>
    public int PendingPlayerNameChanges => _pendingNameChanges.Count;
    /// <summary>Joueurs (PlayerId) ayant abandonné (PLAYSTATE=CONCEDED observé).</summary>
    public IReadOnlyCollection<int> ConcededPlayerIds => _conceded;
    public TimeSpan LastLineTime { get; private set; }
    public int BlockDepth => _blocks.Count;

    public EntityState? GameEntity => _entities.GetValueOrDefault(GameEntityId);
    public int GameTurn => GameEntity?.GetInt("TURN") ?? 0;
    public string? Step => GameEntity?.GetTag("STEP");
    public bool MulliganStarted => _mulliganStarted;

    public PlayerSlot? LocalPlayer => LocalPlayerId is int id ? _players.GetValueOrDefault(id) : null;
    public PlayerSlot? OpponentPlayer => LocalPlayerId is int id ? _players.Values.FirstOrDefault(p => p.PlayerId != id) : null;

    public EntityState? PlayerEntity(int playerId) =>
        _players.TryGetValue(playerId, out var p) ? _entities.GetValueOrDefault(p.EntityId) : null;

    public int? CurrentPlayerId =>
        _players.Values.FirstOrDefault(p => PlayerEntity(p.PlayerId)?.Flag("CURRENT_PLAYER") == true)?.PlayerId;

    /// <summary>Déclenché après le traitement d'une ligne CREATE_GAME (l'état précédent vient d'être effacé).</summary>
    public event Action<GameEngine>? GameStarted;

    // ---------------------------------------------------------------- Entrée

    /// <summary>Traite une ligne brute. Renvoie faux si ce n'est pas une ligne GameState (ligne ignorée).</summary>
    public bool Process(string? raw)
    {
        var line = PowerLogLineParser.TryParse(raw);
        if (line is null) return false;
        Process(line);
        return true;
    }

    public void Process(PowerLogLine line)
    {
        TotalLines++;
        LinesInGame++;
        LastLineTime = line.Time;
        try
        {
            Dispatch(line);
        }
        catch (Exception ex)
        {
            Warn("PARSER_EXCEPTION", $"{ex.GetType().Name}: {ex.Message} — {Truncate(line.Raw)}");
        }
    }

    private void Dispatch(PowerLogLine line)
    {
        switch (line.Method)
        {
            case "DebugPrintPower":
                OnPower(line);
                break;
            case "DebugPrintPowerList":
                EndTagContext();
                Boundary();
                break;
            case "DebugPrintOptions":
                EndTagContext();
                OnOptions(line);
                break;
            case "DebugPrintEntityChoices":
                EndTagContext();
                OnEntityChoices(line);
                break;
            case "DebugPrintEntitiesChosen":
                EndTagContext();
                OnEntitiesChosen(line);
                break;
            case "SendChoices":
                EndTagContext();
                OnSendChoices(line);
                break;
            case "SendOption":
                EndTagContext();
                OnSendOption(line);
                break;
            case "DebugPrintGame":
                EndTagContext();
                OnGameInfo(line);
                break;
            case "OnEntityChoices":
            case "OnEntitiesChosen":
            case "ReportStuck":
                break; // notifications côté client (file d'attente, client bloqué) sans effet sur l'état
            default:
                Unparsed(line, "méthode inconnue");
                break;
        }
    }

    // ---------------------------------------------------------------- DebugPrintPower

    private void OnPower(PowerLogLine line)
    {
        var b = line.Body;
        if (b.Length == 0) return;

        if (TryTagLine(b, out var tl))
        {
            if (_tagContext is int cid)
            {
                ApplyTag(cid, tl.Tag, tl.Value);
                // Finalisation immédiate : l'état doit être juste même si aucune ligne ne suit (fin de paquet).
                if (_entities.TryGetValue(cid, out var ctxEntity)) FinalizeContext(ctxEntity, _contextKind);
            }
            return; // sinon : tag d'un bloc META_DATA ou d'un contexte ignoré
        }

        EndTagContext();

        if (b == "CREATE_GAME")
        {
            StartNewGame(line);
            return;
        }

        EnsureGame(line);

        if (b.StartsWith("GameEntity EntityID=", StringComparison.Ordinal))
        {
            if (int.TryParse(FirstToken(b["GameEntity EntityID=".Length..]), out var gid))
            {
                GameEntityId = gid;
                CreateEntity(gid, "");
                BeginTagContext(gid, ContextKind.GameEntity);
            }
            return;
        }

        if (b.StartsWith("Player EntityID=", StringComparison.Ordinal))
        {
            var eid = ReadField(b, "EntityID");
            var pid = ReadField(b, "PlayerID");
            if (int.TryParse(eid, out var entityId) && int.TryParse(pid, out var playerId))
            {
                var slot = GetOrAddPlayer(playerId);
                slot.EntityId = entityId;
                CreateEntity(entityId, "");
                BeginTagContext(entityId, ContextKind.Player);
                FlushPendingNames();
            }
            return;
        }

        if (TryFullEntityCreating(b, out var newId, out var newCard))
        {
            CreateEntity(newId, newCard);
            BeginTagContext(newId, ContextKind.Create);
            return;
        }

        if (TryUpdatingHeader(b, "FULL_ENTITY - Updating ", out var fu))
        {
            var id = Resolve(fu.Entity, b);
            if (id is null) return;
            var e = GetOrCreate(id.Value);
            _resetSeen?.Add(id.Value);
            // Un FULL_ENTITY "Updating" transmet l'ensemble des tags : remplacement complet.
            e.Tags.Clear();
            e.Orphaned = false;
            e.Placeholder = false;
            SetCardId(e, fu.CardId);
            BeginTagContext(id.Value, ContextKind.FullUpdate);
            return;
        }

        if (TryUpdatingHeader(b, "SHOW_ENTITY - Updating Entity=", out var se))
        {
            var id = Resolve(se.Entity, b);
            if (id is null) return;
            var e = GetOrCreate(id.Value);
            _resetSeen?.Add(id.Value);
            SetCardId(e, se.CardId);
            BeginTagContext(id.Value, ContextKind.Show);
            return;
        }

        if (TryUpdatingHeader(b, "CHANGE_ENTITY - Updating Entity=", out var ce))
        {
            var id = Resolve(ce.Entity, b);
            if (id is null) return;
            var e = GetOrCreate(id.Value);
            _resetSeen?.Add(id.Value);
            e.WasTransformed = true;
            if (!IsLocalEntity(e) && IsHiddenZone(e.Zone) && e.PublicCardId is not null) e.PublicMayBeStale = true;
            SetCardId(e, ce.CardId);
            BeginTagContext(id.Value, ContextKind.Change);
            return;
        }

        if (TryHideEntity(b, out var he))
        {
            var id = Resolve(he.Entity, b);
            if (id is null) return;
            var e = GetOrCreate(id.Value);
            ApplyTag(id.Value, he.Tag, he.Value);
            if (e.CardId.Length > 0)
            {
                e.CardId = "";
                Touch(e);
            }
            return;
        }

        if (TryTagChange(b, out var tc))
        {
            if (tc.Entity.PlayerName is string pname && ResolvePlayerName(pname) is null)
            {
                _pendingNameChanges.Add((pname, tc.Tag, tc.Value));
                Warn("DEFERRED_PLAYER_NAME", pname);
                return;
            }
            var id = Resolve(tc.Entity, b);
            if (id is null) return;
            ApplyTag(id.Value, tc.Tag, tc.Value);
            UpdateProvenance(_entities[id.Value]);
            return;
        }

        if (TryBlockStart(b, out var bs))
        {
            OnBlockStart(bs, line);
            return;
        }

        if (b == "BLOCK_END")
        {
            OnBlockEnd();
            return;
        }

        if (b == "RESET_GAME")
        {
            OnResetGame();
            return;
        }

        if (b.StartsWith("META_DATA", StringComparison.Ordinal) ||
            b.StartsWith("Info[", StringComparison.Ordinal) ||
            b.StartsWith("SUB_SPELL_START", StringComparison.Ordinal) ||
            b == "SUB_SPELL_END" ||
            b.StartsWith("Source =", StringComparison.Ordinal) ||
            b.StartsWith("Targets[", StringComparison.Ordinal) ||
            b.StartsWith("SHUFFLE_DECK", StringComparison.Ordinal) ||
            b.StartsWith("CACHED_TAG_FOR_DORMANT_CHANGE", StringComparison.Ordinal) ||
            b.StartsWith("VO_SPELL", StringComparison.Ordinal) ||
            b.StartsWith("ERROR:", StringComparison.Ordinal))
        {
            return; // sans effet sur l'état observable
        }

        Unparsed(line, "instruction inconnue");
    }

    private void StartNewGame(PowerLogLine line)
    {
        _entities.Clear();
        _players.Clear();
        _blocks.Clear();
        _history.Clear();
        _warnings.Clear();
        _choices.Clear();
        _sentOptions.Clear();
        _turnOwner.Clear();
        _unparsedSamples.Clear();
        _pendingNameChanges.Clear();
        _conceded.Clear();
        _tagContext = null;
        _contextKind = ContextKind.None;
        _options = null;
        _lastOption = null;
        _lastSubOption = null;
        _printingChoice = null;
        _printingChosen = null;
        _sendChoicesId = null;
        _inReset = false;
        _resetSeen = null;
        _mulliganStarted = false;
        _historySeq = 0;
        UnparsedCount = 0;
        ResetCount = 0;
        LocalPlayerId = null;
        LocalEvidence = LocalPlayerEvidence.None;
        GameEntityId = 1;
        Meta = new GameMeta { CreatedAt = line.Time };
        _gameId = FormatTime(line.Time);
        HasGame = true;
        JoinedMidGame = false;
        LinesInGame = 1;
        GameNumber++;
        MarkChanged();
        GameStarted?.Invoke(this);
    }

    private void EnsureGame(PowerLogLine line)
    {
        if (HasGame) return;
        HasGame = true;
        JoinedMidGame = true;
        GameNumber++;
        Meta = new GameMeta { CreatedAt = line.Time };
        _gameId = "partiel-" + FormatTime(line.Time);
        Warn("JOINED_MID_GAME", "La première ligne reçue n'est pas CREATE_GAME : le début de partie manque.");
        // Convention constatée dans tous les logs étudiés : PlayerID 1 = entité 2, PlayerID 2 = entité 3.
        foreach (var (pid, eid) in new[] { (1, 2), (2, 3) })
        {
            var slot = GetOrAddPlayer(pid);
            slot.EntityId = eid;
            if (!_entities.ContainsKey(eid)) _entities[eid] = new EntityState(eid) { Placeholder = true };
        }
        Warn("PLAYER_ENTITIES_ASSUMED", "Entités joueurs supposées (2 et 3) faute de CREATE_GAME.");
        MarkChanged();
    }

    private void CreateEntity(int id, string cardId)
    {
        if (_entities.TryGetValue(id, out var existing))
        {
            if (existing.Placeholder || existing.Id == GameEntityId || IsPlayerEntity(id))
            {
                // Entité référencée avant sa création (partie rejointe en cours) : on complète l'existant.
                existing.Placeholder = false;
                existing.Orphaned = false;
                SetCardId(existing, cardId);
                _resetSeen?.Add(id);
                return;
            }
            // Après un Rembobinage, le serveur réutilise les identifiants des entités annulées (constaté : #119, #120).
            // Une création remplace donc entièrement l'ancienne entité.
            if (!existing.Orphaned) Warn("DUPLICATE_ENTITY_CREATE", $"#{id}");
            _entities.Remove(id);
        }
        var e = new EntityState(id)
        {
            CreatedRevision = Revision,
            CreatedTurn = GameTurn,
        };
        _entities[id] = e;
        _resetSeen?.Add(id);
        SetCardId(e, cardId);
        Touch(e);
    }

    private EntityState GetOrCreate(int id)
    {
        if (_entities.TryGetValue(id, out var e)) return e;
        e = new EntityState(id) { Placeholder = true, CreatedRevision = Revision, CreatedTurn = GameTurn };
        _entities[id] = e;
        Warn("UNKNOWN_ENTITY_REFERENCED", $"#{id}");
        Touch(e);
        return e;
    }

    private void SetCardId(EntityState e, string cardId)
    {
        if (e.CardId == cardId) return;
        e.CardId = cardId;
        if (cardId.Length > 0) e.LastKnownCardId = cardId;
        Touch(e);
    }

    private void BeginTagContext(int id, ContextKind kind)
    {
        _tagContext = id;
        _contextKind = kind;
    }

    private void EndTagContext()
    {
        if (_tagContext is not int id) return;
        var kind = _contextKind;
        _tagContext = null;
        _contextKind = ContextKind.None;
        if (!_entities.TryGetValue(id, out var e)) return;
        FinalizeContext(e, kind);
        if (kind == ContextKind.Player) FlushPendingNames();
    }

    /// <summary>Applique les conséquences d'un en-tête d'entité (création, révélation…). Idempotent.</summary>
    private void FinalizeContext(EntityState e, ContextKind kind)
    {
        switch (kind)
        {
            case ContextKind.Create:
                e.InitialZone = e.Zone;
                e.InitialController = e.Controller;
                e.CreatedDuringSetup = !_mulliganStarted;
                e.CreatedTurn = GameTurn;
                e.ZoneEnteredTurn = GameTurn;
                break;
            case ContextKind.GameEntity:
            {
                var seed = e.GetTag("GAME_SEED");
                if (long.TryParse(seed, out var s)) Meta.GameSeed = s;
                if (!JoinedMidGame && Meta.CreatedAt is TimeSpan t)
                    _gameId = Meta.GameSeed is long gs ? $"{FormatTime(t)}-{gs}" : FormatTime(t);
                break;
            }
        }
        UpdateProvenance(e);
    }

    private void ApplyTag(int id, string tag, string value)
    {
        var e = GetOrCreate(id);
        var old = e.GetTag(tag);
        if (old == value) return;
        e.Tags[tag] = value;
        Touch(e);

        switch (tag)
        {
            case "ZONE":
                OnZoneChanged(e, old, value);
                break;
            case "TURN" when id == GameEntityId:
                // Ordre observé (build 254477) : CURRENT_PLAYER passe à 1 pour le nouveau joueur AVANT l'incrément de TURN.
                if (int.TryParse(value, out var turn) && turn > 0)
                {
                    var h = AddHistory(HistoryKind.TurnStart, null, null, null, null);
                    if (CurrentPlayerId is int owner)
                    {
                        _turnOwner[turn] = owner;
                        h.PlayerId = owner;
                    }
                }
                break;
            case "CURRENT_PLAYER" when value == "1":
            {
                // Cas où CURRENT_PLAYER arrive après TURN (mise en place, autres modes) : compléter sans écraser.
                var pid = PlayerIdOfEntity(id);
                if (pid is int p && GameTurn > 0 && !_turnOwner.ContainsKey(GameTurn))
                {
                    _turnOwner[GameTurn] = p;
                    var last = _history.LastOrDefault(h => h.Kind == HistoryKind.TurnStart);
                    if (last is not null && last.GameTurn == GameTurn && last.PlayerId is null) last.PlayerId = p;
                }
                break;
            }
            case "STEP" when id == GameEntityId:
                if (value == "BEGIN_MULLIGAN") _mulliganStarted = true;
                if (value is "MAIN_READY" or "MAIN_START" or "MAIN_ACTION") _mulliganStarted = true;
                break;
            case "PLAYSTATE" when value is "WON" or "LOST" or "TIED" or "CONCEDED":
            {
                var pid = PlayerIdOfEntity(id);
                if (value == "CONCEDED" && pid is int cp) _conceded.Add(cp);
                var h = AddHistory(HistoryKind.GameEnd, null, null, null, null);
                h.PlayerId = pid;
                h.Detail = value;
                break;
            }
            case "CONTROLLER":
                UpdateProvenance(e);
                break;
        }
    }

    private void OnZoneChanged(EntityState e, string? oldZone, string newZone)
    {
        e.ZoneEnteredTurn = GameTurn;
        if (newZone == "GRAVEYARD" && oldZone == "PLAY" && e.CardType is "MINION" or "WEAPON" or "LOCATION")
        {
            var top = _blocks.FirstOrDefault()?.History;
            if (top is not null)
            {
                if (top.Kind == HistoryKind.Deaths) top.Entities.Add(e.Id);
                else top.SecondaryEntities.Add(e.Id);
            }
        }
    }

    // ---------------------------------------------------------------- Visibilité / provenance

    private static bool IsHiddenZone(string zone) => zone is "HAND" or "DECK" or "SETASIDE" or "SECRET" or "INVALID";

    private bool IsLocalEntity(EntityState e) => LocalPlayerId is int lp && e.Controller == lp;

    /// <summary>Retient l'identité publique d'une entité quand elle est observée dans un contexte public.</summary>
    private void UpdateProvenance(EntityState e)
    {
        if (e.CardId.Length == 0) return;
        string? reason = null;
        var zone = e.Zone;
        if (zone is "PLAY" or "GRAVEYARD" or "REMOVEDFROMGAME") reason = zone;
        else if (e.GetInt("REVEALED") == 1) reason = "REVEALED";
        else if (zone == "SECRET" && IsKnownNonSecretSecretZoneCard(e)) reason = "SECRET_ZONE_PUBLIC";
        if (reason is null) return;
        if (e.PublicCardId == e.CardId && !e.PublicMayBeStale) return;
        e.PublicCardId = e.CardId;
        e.FirstPublicCardId ??= e.CardId;
        e.PublicReason = reason;
        e.PublicAtRevision = Revision;
        e.PublicMayBeStale = false;
    }

    /// <summary>Quêtes, Sigils, Objectifs… sont affichés publiquement ; les vrais Secrets ne le sont pas.</summary>
    private bool IsKnownNonSecretSecretZoneCard(EntityState e)
    {
        if (e.Flag("SECRET")) return false;
        if (e.Flag("QUEST") || e.Flag("SIDE_QUEST") || e.Flag("QUESTLINE") || e.Flag("SIGIL") || e.Flag("OBJECTIVE")) return true;
        var card = _opt.Cards?.Get(e.CardId);
        if (card is null) return false; // inconnu : prudence
        return !card.IsSecret;
    }

    // ---------------------------------------------------------------- Blocs

    private void OnBlockStart(BlockStart bs, PowerLogLine line)
    {
        var src = bs.Entity.IsEmpty ? null : Resolve(bs.Entity, line.Body);
        var tgt = bs.Target.IsEmpty ? null : Resolve(bs.Target, line.Body);
        var block = new Block { Type = bs.BlockType, SourceId = src, TargetId = tgt, IsReset = bs.BlockType == "GAME_RESET" };
        if (_blocks.Count == 0) block.History = CreateHistoryForBlock(block);
        _blocks.Add(block);
    }

    private HistoryEntry? CreateHistoryForBlock(Block block)
    {
        var srcE = block.SourceId is int s ? _entities.GetValueOrDefault(s) : null;
        var srcIsSystem = block.SourceId is null || block.SourceId == GameEntityId || (block.SourceId is int sid && IsPlayerEntity(sid));
        HistoryKind? kind = block.Type switch
        {
            "PLAY" when srcE?.CardType == "HERO_POWER" => HistoryKind.HeroPower,
            "PLAY" when srcE?.CardType == "LOCATION" && srcE.Zone == "PLAY" => HistoryKind.LocationActivation,
            "PLAY" => HistoryKind.Play,
            "ATTACK" => HistoryKind.Attack,
            "TRIGGER" when srcIsSystem => null,
            "TRIGGER" => HistoryKind.Trigger,
            "POWER" => HistoryKind.Power,
            "DEATHS" => HistoryKind.Deaths,
            "FATIGUE" => HistoryKind.Fatigue,
            "GAME_RESET" => HistoryKind.Rewind,
            _ => HistoryKind.Other,
        };
        if (kind is null) return null;
        var h = AddHistory(kind.Value, block.SourceId, block.TargetId, block.Type, srcE?.Zone);
        h.PlayerId = srcE is null ? null : srcE.Controller != 0 ? srcE.Controller : PlayerIdOfEntity(srcE.Id);
        return h;
    }

    private void OnBlockEnd()
    {
        if (_blocks.Count == 0)
        {
            Warn("BLOCK_END_WITHOUT_START", null);
            return;
        }
        var block = _blocks[^1];
        _blocks.RemoveAt(_blocks.Count - 1);
        if (block.IsReset) EndReset();
        if (block.History is { Kind: HistoryKind.Deaths } h && h.Entities.Count == 0) _history.Remove(h);
    }

    private void OnResetGame()
    {
        var resetBlock = _blocks.LastOrDefault(b => b.IsReset);
        if (resetBlock is null) Warn("RESET_OUTSIDE_RESET_BLOCK", null);
        _inReset = true;
        _resetSeen = new HashSet<int>();
        ResetCount++;
        _options = null;
        _printingChoice = null;
        _printingChosen = null;

        var src = resetBlock?.SourceId;
        var idx = _history.FindLastIndex(h => h.Kind == HistoryKind.Play && h.SourceId == src);
        if (idx >= 0)
        {
            for (int i = idx + 1; i < _history.Count; i++)
            {
                if (!ReferenceEquals(_history[i], resetBlock?.History) && _history[i].Kind != HistoryKind.Rewind)
                    _history[i].Rewound = true;
            }
        }
        if (resetBlock?.History is { } rh) rh.Detail = "État de partie réinitialisé par le serveur (Rembobinage)";
        Warn("GAME_RESET_APPLIED", src is int s ? $"source #{s}" : null);
        MarkChanged();
    }

    private void EndReset()
    {
        if (!_inReset || _resetSeen is null) return;
        foreach (var e in _entities.Values)
        {
            if (_resetSeen.Contains(e.Id) || e.Id == GameEntityId || IsPlayerEntity(e.Id) || e.Orphaned) continue;
            e.Orphaned = true;
            Touch(e);
        }
        _inReset = false;
        _resetSeen = null;
    }

    // ---------------------------------------------------------------- Options

    private void OnOptions(PowerLogLine line)
    {
        var b = line.Body;
        if (b.StartsWith("id=", StringComparison.Ordinal))
        {
            Boundary();
            var id = int.TryParse(FirstToken(b[3..]), out var oid) ? oid : -1;
            _options = new OptionsSet { Id = id, Time = line.Time };
            _lastOption = null;
            _lastSubOption = null;
            MarkChanged();
            _options.Revision = Revision;
            return;
        }
        if (_options is null) return;

        if (b.StartsWith("option ", StringComparison.Ordinal))
        {
            var index = int.TryParse(FirstToken(b[7..]), out var i) ? i : -1;
            var type = ReadField(b, "type") ?? "";
            var main = ReadField(b, "mainEntity", "error");
            var error = ReadField(b, "error") ?? "";
            var errorParam = ReadField(b, "errorParam");
            int? eid = string.IsNullOrEmpty(main) ? null : Resolve(EntityRef.Parse(main), b);
            _lastOption = new OptionEntry { Index = index, Type = type, EntityId = eid, Error = error, ErrorParam = errorParam };
            _lastSubOption = null;
            _options.Options.Add(_lastOption);
            if (error == "NONE" && type == "POWER" && eid is int e && _entities.TryGetValue(e, out var ent))
            {
                // Propriétaire connu par l'état ; à défaut (partie rejointe en cours), celui imprimé par le client.
                var owner = ent.Controller > 0 ? ent.Controller : EntityRef.Parse(main).PrintedPlayer ?? 0;
                if (owner > 0) SetLocalPlayer(owner, LocalPlayerEvidence.ServerOptions);
            }
            return;
        }
        if (b.StartsWith("subOption ", StringComparison.Ordinal) && _lastOption is not null)
        {
            var index = int.TryParse(FirstToken(b[10..]), out var i) ? i : -1;
            var ent = ReadField(b, "entity", "error");
            var error = ReadField(b, "error") ?? "";
            _lastSubOption = new OptionEntry
            {
                Index = index,
                Type = "SUBOPTION",
                EntityId = string.IsNullOrEmpty(ent) ? null : Resolve(EntityRef.Parse(ent), b),
                Error = error,
            };
            _lastOption.SubOptions.Add(_lastSubOption);
            return;
        }
        if (b.StartsWith("target ", StringComparison.Ordinal) && _lastOption is not null)
        {
            var ent = ReadField(b, "entity", "error");
            var error = ReadField(b, "error") ?? "";
            var tid = string.IsNullOrEmpty(ent) ? null : Resolve(EntityRef.Parse(ent), b);
            if (tid is null) return;
            var target = new OptionTarget { EntityId = tid.Value, Error = error };
            // Une cible plus indentée qu'une sous-option lui appartient.
            if (_lastSubOption is not null && line.Indent >= 6) _lastSubOption.Targets.Add(target);
            else _lastOption.Targets.Add(target);
        }
    }

    // ---------------------------------------------------------------- Choix

    private void OnEntityChoices(PowerLogLine line)
    {
        var b = line.Body;
        if (b.StartsWith("id=", StringComparison.Ordinal))
        {
            Boundary();
            var id = int.TryParse(FirstToken(b[3..]), out var cid) ? cid : -1;
            var player = ReadField(b, "Player", "TaskList", "ChoiceType", "CountMin", "CountMax") ?? "";
            var choice = new ChoiceState
            {
                Id = id,
                PlayerName = player,
                ChoiceType = ReadField(b, "ChoiceType") ?? "",
                CountMin = int.TryParse(ReadField(b, "CountMin"), out var mn) ? mn : 0,
                CountMax = int.TryParse(ReadField(b, "CountMax"), out var mx) ? mx : 0,
                Time = line.Time,
            };
            var pe = ResolvePlayerName(player);
            choice.PlayerId = pe is int pid ? PlayerIdOfEntity(pid) : null;
            _choices[id] = choice;
            _printingChoice = choice;
            MarkChanged();
            choice.Revision = Revision;
            return;
        }
        if (_printingChoice is null) return;
        if (b.StartsWith("Source=", StringComparison.Ordinal))
        {
            _printingChoice.SourceId = Resolve(EntityRef.Parse(b[7..]), b);
            return;
        }
        if (b.StartsWith("Entities[", StringComparison.Ordinal))
        {
            var eq = b.IndexOf("]=", StringComparison.Ordinal);
            if (eq < 0) return;
            if (Resolve(EntityRef.Parse(b[(eq + 2)..]), b) is int eid) _printingChoice.Offered.Add(eid);
        }
    }

    private void OnEntitiesChosen(PowerLogLine line)
    {
        var b = line.Body;
        if (b.StartsWith("id=", StringComparison.Ordinal))
        {
            Boundary();
            var id = int.TryParse(FirstToken(b[3..]), out var cid) ? cid : -1;
            if (!_choices.TryGetValue(id, out var choice))
            {
                var player = ReadField(b, "Player", "EntitiesCount") ?? "";
                choice = new ChoiceState { Id = id, PlayerName = player, ChoiceType = "UNKNOWN", Time = line.Time };
                var pe = ResolvePlayerName(player);
                choice.PlayerId = pe is int pid ? PlayerIdOfEntity(pid) : null;
                _choices[id] = choice;
                Warn("CHOSEN_WITHOUT_CHOICE", $"choix {id}");
            }
            choice.Chosen = new List<int>();
            _printingChosen = choice;
            MarkChanged();
            RecordChoiceHistory(choice);
            return;
        }
        if (_printingChosen is null) return;
        if (b.StartsWith("Entities[", StringComparison.Ordinal))
        {
            var eq = b.IndexOf("]=", StringComparison.Ordinal);
            if (eq < 0) return;
            if (Resolve(EntityRef.Parse(b[(eq + 2)..]), b) is int eid) _printingChosen.Chosen!.Add(eid);
        }
    }

    private void RecordChoiceHistory(ChoiceState choice)
    {
        var kind = choice.ChoiceType == "MULLIGAN" ? HistoryKind.Mulligan : HistoryKind.Choice;
        // Les entités choisies sont ajoutées au fil des lignes "Entities[i]" ; l'entrée référence le même objet ChoiceState.
        var h = AddHistory(kind, choice.SourceId, null, choice.ChoiceType, null);
        h.PlayerId = choice.PlayerId;
        h.Detail = $"choice:{choice.Id}";
    }

    private void OnSendChoices(PowerLogLine line)
    {
        var b = line.Body;
        if (b.StartsWith("id=", StringComparison.Ordinal))
        {
            _sendChoicesId = int.TryParse(FirstToken(b[3..]), out var id) ? id : null;
            if (_sendChoicesId is int sid && _choices.TryGetValue(sid, out var choice))
            {
                if (choice.PlayerId is int pid) SetLocalPlayer(pid, LocalPlayerEvidence.SentByClient);
                else if (LocalPlayer is { } lp && !string.IsNullOrEmpty(choice.PlayerName))
                {
                    // Seul le joueur local envoie des choix : son nom (tel qu'écrit par le client) lui appartient.
                    lp.Aliases.Add(choice.PlayerName);
                    if (lp.Name is null || IsPlaceholder(lp.Name)) lp.Name = choice.PlayerName;
                    choice.PlayerId = lp.PlayerId;
                    FlushPendingNames();
                }
            }
            return;
        }
        if (b.StartsWith("m_chosenEntities[", StringComparison.Ordinal) && _sendChoicesId is not null)
        {
            var eq = b.IndexOf("]=", StringComparison.Ordinal);
            if (eq < 0) return;
            var r = EntityRef.Parse(b[(eq + 2)..]);
            if (r.PrintedPlayer is int pp && LocalEvidence < LocalPlayerEvidence.SentByClient && _choices.TryGetValue(_sendChoicesId.Value, out var c) && c.PlayerId is null)
                SetLocalPlayer(pp, LocalPlayerEvidence.SentByClient);
        }
    }

    private void OnSendOption(PowerLogLine line)
    {
        var b = line.Body;
        if (!b.StartsWith("selectedOption=", StringComparison.Ordinal)) return;
        var opt = int.TryParse(ReadField(b, "selectedOption"), out var o) ? o : -1;
        var sub = int.TryParse(ReadField(b, "selectedSubOption"), out var so) ? so : -1;
        var tgt = int.TryParse(ReadField(b, "selectedTarget"), out var t) ? t : 0;
        var pos = int.TryParse(ReadField(b, "selectedPosition"), out var p) ? p : 0;
        var entry = _options?.Options.FirstOrDefault(x => x.Index == opt);
        _sentOptions.Add(new SentOption
        {
            Time = line.Time,
            GameTurn = GameTurn,
            OptionIndex = opt,
            SubOption = sub,
            TargetId = tgt,
            Position = pos,
            OptionsId = _options?.Id,
            EntityId = entry?.EntityId,
            OptionType = entry?.Type,
        });
        if (entry?.EntityId is int eid && _entities.TryGetValue(eid, out var ent) && ent.Controller > 0)
            SetLocalPlayer(ent.Controller, LocalPlayerEvidence.SentByClient);
        else if (entry?.Type == "END_TURN" && CurrentPlayerId is int cp)
            SetLocalPlayer(cp, LocalPlayerEvidence.SentByClient);
        Boundary();
    }

    // ---------------------------------------------------------------- Informations de partie

    private void OnGameInfo(PowerLogLine line)
    {
        var b = line.Body;
        if (b.StartsWith("BuildNumber=", StringComparison.Ordinal) && int.TryParse(b[12..], out var build)) Meta.BuildNumber = build;
        else if (b.StartsWith("GameType=", StringComparison.Ordinal)) Meta.GameType = b[9..];
        else if (b.StartsWith("FormatType=", StringComparison.Ordinal)) Meta.FormatType = b[11..];
        else if (b.StartsWith("ScenarioID=", StringComparison.Ordinal) && int.TryParse(b[11..], out var sc)) Meta.ScenarioId = sc;
        else if (b.StartsWith("PlayerID=", StringComparison.Ordinal))
        {
            var comma = b.IndexOf(", PlayerName=", StringComparison.Ordinal);
            if (comma > 0 && int.TryParse(b[9..comma], out var pid))
            {
                var name = b[(comma + 13)..];
                var slot = GetOrAddPlayer(pid);
                slot.Aliases.Add(name);
                if (slot.Name is null || IsPlaceholder(slot.Name)) slot.Name = name;
                if (_opt.LocalPlayerName is string cfg && !IsPlaceholder(name) && NamesMatch(cfg, name))
                    SetLocalPlayer(pid, LocalPlayerEvidence.Configured);
                FlushPendingNames();
            }
        }
        MarkChanged();
        UpdateLocalPlayerHeuristic();
    }

    // ---------------------------------------------------------------- Joueurs

    private PlayerSlot GetOrAddPlayer(int playerId)
    {
        if (!_players.TryGetValue(playerId, out var slot))
        {
            slot = new PlayerSlot { PlayerId = playerId };
            _players[playerId] = slot;
        }
        return slot;
    }

    private bool IsPlayerEntity(int entityId) => _players.Values.Any(p => p.EntityId == entityId);

    private int? PlayerIdOfEntity(int entityId) => _players.Values.FirstOrDefault(p => p.EntityId == entityId)?.PlayerId;

    private static bool IsPlaceholder(string name) => name.StartsWith("UNKNOWN", StringComparison.Ordinal);

    private static string ShortName(string name)
    {
        var i = name.IndexOf('#');
        return i < 0 ? name : name[..i];
    }

    private static bool NamesMatch(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase) ||
        (a.Contains('#') != b.Contains('#') && string.Equals(ShortName(a), ShortName(b), StringComparison.OrdinalIgnoreCase));

    /// <summary>Associe un nom de joueur à l'entité joueur correspondante. Renvoie l'id d'entité ou null si ambigu.</summary>
    private int? ResolvePlayerName(string name)
    {
        if (_players.Count == 0) return null;
        foreach (var slot in _players.Values)
            if (slot.Aliases.Contains(name)) return slot.EntityId;

        if (!IsPlaceholder(name))
        {
            var shortName = ShortName(name);
            foreach (var slot in _players.Values)
            {
                if (slot.Aliases.Any(a => !IsPlaceholder(a) && ShortName(a) == shortName))
                {
                    slot.Aliases.Add(name);
                    return slot.EntityId;
                }
            }
            // Nom inconnu : il appartient au seul joueur qui n'a encore aucun nom réel.
            var unnamed = _players.Values.Where(s => !s.Aliases.Any(a => !IsPlaceholder(a))).ToList();
            if (unnamed.Count == 1 && _players.Count == 2)
            {
                var slot = unnamed[0];
                slot.Aliases.Add(name);
                slot.Name = name;
                return slot.EntityId;
            }
            // Les deux joueurs ont déjà un nom : contre l'IA, l'adversaire change de nom en cours de partie
            // (constaté : « Aubergiste » puis le nom du boss). Le nom du joueur local, lui, ne change pas :
            // un nom inconnu, différent de tous ses noms, désigne l'adversaire.
            if (_players.Count == 2 && LocalPlayer is { } local && !local.Aliases.Any(a => NamesMatch(a, name)))
            {
                var opponent = _players.Values.First(p => p.PlayerId != local.PlayerId);
                opponent.Aliases.Add(name);
                Warn("PLAYER_ALIAS_INFERRED", $"« {name} » attribué à l'adversaire (P{opponent.PlayerId})");
                return opponent.EntityId;
            }
            return null;
        }

        // Nom générique ("UNKNOWN HUMAN PLAYER") jamais vu : l'adversaire si le joueur local est connu.
        if (LocalPlayerId is int lp)
        {
            var opp = _players.Values.FirstOrDefault(p => p.PlayerId != lp);
            if (opp is not null)
            {
                opp.Aliases.Add(name);
                return opp.EntityId;
            }
        }
        return null;
    }

    private void FlushPendingNames()
    {
        if (_pendingNameChanges.Count == 0) return;
        var pending = _pendingNameChanges.ToList();
        _pendingNameChanges.Clear();
        foreach (var (name, tag, value) in pending)
        {
            if (ResolvePlayerName(name) is int id) ApplyTag(id, tag, value);
            else _pendingNameChanges.Add((name, tag, value));
        }
    }

    private void SetLocalPlayer(int playerId, LocalPlayerEvidence evidence)
    {
        if (!_players.ContainsKey(playerId)) return;
        if (LocalPlayerId == playerId)
        {
            if (evidence > LocalEvidence) LocalEvidence = evidence;
            return;
        }
        if (LocalPlayerId is not null && evidence <= LocalEvidence) return;
        if (LocalPlayerId is not null && LocalEvidence >= LocalPlayerEvidence.ServerOptions)
            Warn("LOCAL_PLAYER_CONFLICT", $"P{LocalPlayerId} ({LocalEvidence}) -> P{playerId} ({evidence})");
        LocalPlayerId = playerId;
        LocalEvidence = evidence;
        FlushPendingNames();
        // Les identités observées avant de connaître le joueur local doivent être réévaluées.
        foreach (var e in _entities.Values) UpdateProvenance(e);
        MarkChanged();
    }

    /// <summary>Seul le joueur local voit l'identité de ses cartes en main dès la mise en place.</summary>
    private void UpdateLocalPlayerHeuristic()
    {
        if (LocalEvidence > LocalPlayerEvidence.HandHeuristic || _players.Count != 2) return;
        var known = _players.Keys.ToDictionary(p => p, _ => 0);
        foreach (var e in _entities.Values)
        {
            if (e.Zone == "HAND" && e.CardId.Length > 0 && e.CardId != CoinCardId && known.ContainsKey(e.Controller))
                known[e.Controller]++;
        }
        var withCards = known.Where(kv => kv.Value > 0).ToList();
        if (withCards.Count == 1) SetLocalPlayer(withCards[0].Key, LocalPlayerEvidence.HandHeuristic);
    }

    // ---------------------------------------------------------------- Révisions

    /// <summary>Frontière entre deux paquets serveur : la prochaine modification ouvrira une nouvelle révision.</summary>
    private void Boundary()
    {
        _changedSinceBoundary = false;
        UpdateLocalPlayerHeuristic();
    }

    private void MarkChanged()
    {
        if (_changedSinceBoundary) return;
        _changedSinceBoundary = true;
        Revision++;
    }

    private void Touch(EntityState e)
    {
        MarkChanged();
        e.LastChangedRevision = Revision;
        EntityTouched?.Invoke(e);
    }

    /// <summary>Notification interne (oracles de cohérence) : une entité vient d'être modifiée.</summary>
    internal event Action<EntityState>? EntityTouched;

    // ---------------------------------------------------------------- Divers

    private int? Resolve(EntityRef r, string context)
    {
        if (r.IsGameEntity) return GameEntityId;
        if (r.Id is int id) return id == 0 ? null : id;
        if (r.PlayerName is string name)
        {
            var resolved = ResolvePlayerName(name);
            if (resolved is null) Warn("UNRESOLVED_PLAYER_NAME", name);
            return resolved;
        }
        return null;
    }

    private HistoryEntry AddHistory(HistoryKind kind, int? source, int? target, string? blockType, string? sourceZone)
    {
        var h = new HistoryEntry
        {
            Seq = ++_historySeq,
            Kind = kind,
            GameTurn = GameTurn,
            Time = LastLineTime,
            Revision = Revision,
            SourceId = source,
            TargetId = target,
            BlockType = blockType,
            SourceZone = sourceZone,
            SourcePositionAtStart = source is int s && _entities.TryGetValue(s, out var se) ? se.ZonePosition : null,
        };
        _history.Add(h);
        return h;
    }

    private void Warn(string code, string? detail)
    {
        if (_warnings.TryGetValue(code, out var w))
        {
            w.Count++;
            if (detail is not null && w.Count <= 3 && w.Detail is not null && !w.Detail.Contains(detail, StringComparison.Ordinal))
                w.Detail += " ; " + detail;
            return;
        }
        _warnings[code] = new EngineWarning { Code = code, Detail = detail, Count = 1, FirstLine = TotalLines };
    }

    private void Unparsed(PowerLogLine line, string why)
    {
        UnparsedCount++;
        if (_unparsedSamples.Count < 20) _unparsedSamples.Add($"[{why}] {Truncate(line.Raw)}");
        Warn("UNPARSED_LINES", why);
    }

    private static string Truncate(string s) => s.Length <= 220 ? s : s[..220] + "…";

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalHours:00}{t.Minutes:00}{t.Seconds:00}";

    /// <summary>Choix enregistré associé à une entrée d'historique de type Mulligan/Choice.</summary>
    public ChoiceState? ChoiceFor(HistoryEntry h)
    {
        if (h.Detail is null || !h.Detail.StartsWith("choice:", StringComparison.Ordinal)) return null;
        return int.TryParse(h.Detail[7..], out var id) ? _choices.GetValueOrDefault(id) : null;
    }
}
