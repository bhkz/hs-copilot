using HsCopilot.Core.Cards;
using HsCopilot.Core.Engine;

namespace HsCopilot.Core.State;

/// <summary>Liste de cartes de départ du joueur local (deck sélectionné dans HDT ou configuré).</summary>
public sealed class Decklist
{
    public required string Source { get; init; }
    public string? Name { get; init; }
    public required IReadOnlyDictionary<string, int> Cards { get; init; }
    public int Total => Cards.Values.Sum();
}

/// <summary>Contexte fourni par l'hôte (session) en plus du moteur.</summary>
public sealed class ProjectionContext
{
    public string Transport { get; init; } = "unknown";
    public string? TransportDetail { get; init; }
    public Decklist? Decklist { get; init; }
    /// <summary>Indicateurs ajoutés par la session (perte de lignes, écart avec HDT…).</summary>
    public IReadOnlyList<UncertaintyFlag> ExtraFlags { get; init; } = Array.Empty<UncertaintyFlag>();
    /// <summary>Nombre maximal d'entrées d'historique incluses (les plus récentes).</summary>
    public int MaxHistory { get; init; } = 200;
}

/// <summary>Construit un <see cref="GameStateSnapshot"/> à partir du moteur, en appliquant <see cref="Visibility"/>.</summary>
public sealed class GameStateProjector
{
    private static readonly string[] KeywordTags =
    {
        "TAUNT", "DIVINE_SHIELD", "CHARGE", "RUSH", "WINDFURY", "MEGA_WINDFURY", "STEALTH", "POISONOUS", "VENOMOUS", "LIFESTEAL",
        "REBORN", "FROZEN", "IMMUNE", "DORMANT", "SILENCED", "CANT_ATTACK", "DEATHRATTLE", "CANT_BE_TARGETED_BY_SPELLS",
        "CANT_BE_TARGETED_BY_HERO_POWERS", "ELUSIVE", "TRIGGER_VISUAL", "AURA", "ENRAGED", "UNTOUCHABLE",
    };

    private readonly ICardDatabase _cards;

    public GameStateProjector(ICardDatabase cards) => _cards = cards;

    public GameStateSnapshot Project(GameEngine engine, ProjectionContext? context = null)
    {
        context ??= new ProjectionContext();
        var flags = new List<UncertaintyFlag>();
        var unknownCards = new HashSet<string>(StringComparer.Ordinal);
        var local = engine.LocalPlayerId;
        var opponentId = engine.Players.Keys.FirstOrDefault(p => p != local);

        string NameOf(string? cardId)
        {
            if (string.IsNullOrEmpty(cardId)) return "Carte inconnue";
            var c = _cards.Get(cardId);
            if (c is null) { unknownCards.Add(cardId); return cardId; }
            return c.DisplayName;
        }

        var snapshotLocal = local is int lp ? BuildPlayer(engine, lp, true, context, NameOf, flags) : null;
        var snapshotOpp = engine.Players.Count == 2 && opponentId != 0 ? BuildPlayer(engine, opponentId, false, context, NameOf, flags) : null;

        string SideOf(int? playerId) =>
            playerId is null ? "unknown" : playerId == local ? "local" : local is null ? "unknown" : "opponent";

        var current = engine.CurrentPlayerId;
        var first = engine.Players.Keys.FirstOrDefault(p => engine.PlayerEntity(p)?.Flag("FIRST_PLAYER") == true);
        string? result = null;
        if (local is int l2 && engine.PlayerEntity(l2)?.GetTag("PLAYSTATE") is string ps && ps is "WON" or "LOST" or "TIED" or "CONCEDED")
            result = ps == "CONCEDED" ? "LOST" : ps; // CONCEDED précède LOST : l'abandon est indiqué par GameInfo.Conceded
        var gameOver = engine.Step == "FINAL_GAMEOVER" || engine.GameEntity?.GetTag("STATE") == "COMPLETE" || result is not null;

        var game = new GameInfo
        {
            GameType = engine.Meta.GameType,
            FormatType = engine.Meta.FormatType,
            ScenarioId = engine.Meta.ScenarioId,
            GameTurn = engine.GameTurn,
            TurnNumber = (engine.GameTurn + 1) / 2,
            Step = engine.Step,
            ActivePlayer = SideOf(current),
            FirstPlayer = first == 0 ? "unknown" : SideOf(first),
            MulliganDone = engine.Players.Keys.All(p => engine.PlayerEntity(p)?.GetTag("MULLIGAN_STATE") == "DONE") && engine.Players.Count == 2,
            GameOver = gameOver,
            Result = result,
            Conceded = engine.ConcededPlayerIds.Count == 0 ? null : SideOf(engine.ConcededPlayerIds.First()),
            ResetCount = engine.ResetCount,
        };

        // --- Uncertainty
        if (!engine.HasGame) flags.Add(Flag("NO_GAME", "critical", "Aucune partie détectée dans le flux reçu."));
        if (engine.JoinedMidGame) flags.Add(Flag("JOINED_MID_GAME", "critical", "Début de partie manquant : l'état est partiel."));
        switch (engine.LocalEvidence)
        {
            case LocalPlayerEvidence.None when engine.HasGame:
                flags.Add(Flag("LOCAL_PLAYER_UNKNOWN", "critical", "Joueur local non identifié : toutes les cartes sont traitées comme adverses."));
                break;
            case LocalPlayerEvidence.HandHeuristic:
                flags.Add(Flag("LOCAL_PLAYER_HEURISTIC", "info", "Joueur local déduit des cartes visibles en main (pas encore confirmé par le serveur)."));
                break;
        }
        if (_cards.Count == 0) flags.Add(Flag("CARD_DB_EMPTY", "critical", "Base de cartes absente : noms, coûts de base et effets inconnus."));

        foreach (var w in engine.Warnings)
        {
            var f = MapWarning(w);
            if (f is not null) flags.Add(f);
        }
        if (engine.PendingPlayerNameChanges > 0)
            flags.Add(Flag("PENDING_PLAYER_NAME", "warning",
                $"{engine.PendingPlayerNameChanges} changement(s) d'état d'un joueur en attente : nom de joueur non associé (tours et mana possiblement faux).", engine.PendingPlayerNameChanges));

        var options = BuildOptions(engine, NameOf, SideOf);
        if (options is not null && !options.Fresh && current == local && local is not null && !gameOver)
            flags.Add(Flag("OPTIONS_STALE", "info", "Les actions légales du serveur datent d'une révision précédente (état modifié depuis)."));

        var history = BuildHistory(engine, NameOf, SideOf, context.MaxHistory);
        var choice = BuildPendingChoice(engine, NameOf);

        flags.AddRange(context.ExtraFlags);
        if (unknownCards.Count > 0)
            flags.Add(Flag("UNKNOWN_CARD_IDS", "warning",
                $"{unknownCards.Count} identifiant(s) absent(s) de la base de cartes (build {_cards.Build}) : {string.Join(", ", unknownCards.Take(5))}", unknownCards.Count));

        return new GameStateSnapshot
        {
            ForbiddenCardIds = ComputeForbiddenCardIds(engine),
            LastLogTimeOfDay = engine.LastLineTime,
            GameId = engine.GameId ?? "aucune",
            Revision = engine.Revision,
            LinesProcessed = engine.LinesInGame,
            LastLogTime = engine.LastLineTime.ToString(@"hh\:mm\:ss\.fff"),
            OpenBlocks = engine.BlockDepth,
            Source = new SourceInfo
            {
                Transport = context.Transport,
                Detail = context.TransportDetail,
                ClientBuild = engine.Meta.BuildNumber,
                CardDbBuild = _cards.Build,
                CardDbCount = _cards.Count,
            },
            Game = game,
            Local = snapshotLocal,
            Opponent = snapshotOpp,
            LegalOptions = options,
            PendingChoice = choice,
            History = history,
            Uncertainty = new UncertaintyView
            {
                LocalPlayerEvidence = engine.LocalEvidence.ToString(),
                Flags = flags,
                Incomplete = flags.Any(f => f.Severity == "critical"),
            },
        };
    }

    /// <summary>Identifiants connus du moteur pour des entités cachées au joueur local, et visibles nulle part ailleurs.</summary>
    private static HashSet<string> ComputeForbiddenCardIds(GameEngine engine)
    {
        var local = engine.LocalPlayerId;
        var visible = new HashSet<string>(StringComparer.Ordinal);
        var hidden = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in engine.Entities.Values)
        {
            if (e.Orphaned) continue;
            var (cid, _) = Visibility.VisibleCardId(e, local);
            if (cid is not null) visible.Add(cid);
            var hist = Visibility.HistoryCardId(e, local);
            if (hist is not null) visible.Add(hist);
            if (cid is null && e.CardId.Length > 0 && !Visibility.IsLocal(e, local)) hidden.Add(e.CardId);
        }
        hidden.ExceptWith(visible);
        return hidden;
    }

    // ------------------------------------------------------------------ Joueurs

    private PlayerView BuildPlayer(GameEngine engine, int playerId, bool isLocal, ProjectionContext ctx, Func<string?, string> nameOf, List<UncertaintyFlag> flags)
    {
        var local = engine.LocalPlayerId;
        var pe = engine.PlayerEntity(playerId);
        var mine = engine.Entities.Values.Where(e => !e.Orphaned && e.Controller == playerId).ToList();
        var enchantments = engine.Entities.Values
            .Where(e => !e.Orphaned && e.CardType == "ENCHANTMENT" && e.Zone == "PLAY" && e.GetInt("ATTACHED") > 0)
            .GroupBy(e => e.GetInt("ATTACHED"))
            .ToDictionary(g => g.Key, g => g.ToList());

        List<EnchantmentView> EnchantmentsOf(EntityState target)
        {
            if (!enchantments.TryGetValue(target.Id, out var list)) return new List<EnchantmentView>();
            var visibleTarget = Visibility.VisibleCardId(target, local).CardId is not null;
            if (!visibleTarget) return new List<EnchantmentView>();
            return list.Select(en =>
            {
                var (cid, _) = Visibility.VisibleCardId(en, local);
                var card = _cards.Get(cid);
                return new EnchantmentView { EntityId = en.Id, CardId = cid, Name = cid is null ? null : nameOf(cid), Text = NullIfEmpty(card?.PlainText) };
            }).ToList();
        }

        // Héros
        HeroView? hero = null;
        string? heroClass = null;
        var heroId = pe?.GetInt("HERO_ENTITY") ?? 0;
        if (heroId > 0 && engine.Entities.TryGetValue(heroId, out var h))
        {
            var (cid, _) = Visibility.VisibleCardId(h, local);
            var card = _cards.Get(cid);
            heroClass = card?.CardClass ?? h.GetTag("CLASS");
            var maxHp = h.GetInt("HEALTH");
            hero = new HeroView
            {
                EntityId = h.Id,
                CardId = cid,
                Name = cid is null ? null : nameOf(cid),
                Health = maxHp - h.GetInt("DAMAGE"),
                MaxHealth = maxHp,
                Damage = h.GetInt("DAMAGE"),
                Armor = h.GetInt("ARMOR"),
                Attack = h.GetInt("ATK"),
                Exhausted = h.Flag("EXHAUSTED"),
                AttacksThisTurn = h.GetInt("NUM_ATTACKS_THIS_TURN"),
                Frozen = h.Flag("FROZEN"),
                Immune = h.Flag("IMMUNE"),
                UpdatedAtRevision = h.LastChangedRevision,
            };
        }

        var heroPowerEntity = mine.FirstOrDefault(e => e.Zone == "PLAY" && e.CardType == "HERO_POWER");
        HeroPowerView? heroPower = null;
        if (heroPowerEntity is not null)
        {
            var (cid, _) = Visibility.VisibleCardId(heroPowerEntity, local);
            heroPower = new HeroPowerView
            {
                EntityId = heroPowerEntity.Id,
                CardId = cid,
                Name = cid is null ? null : nameOf(cid),
                Cost = heroPowerEntity.GetInt("COST"),
                Exhausted = heroPowerEntity.Flag("EXHAUSTED"),
                Text = NullIfEmpty(_cards.Get(cid)?.PlainText),
            };
        }

        var weaponEntity = mine.FirstOrDefault(e => e.Zone == "PLAY" && e.CardType == "WEAPON");
        WeaponView? weapon = null;
        if (weaponEntity is not null)
        {
            var (cid, _) = Visibility.VisibleCardId(weaponEntity, local);
            // Build 254477 : la durabilité des armes est portée par HEALTH (DURABILITY absent), l'usure par DAMAGE.
            var maxDur = WeaponMaxDurability(weaponEntity);
            weapon = new WeaponView
            {
                EntityId = weaponEntity.Id,
                CardId = cid,
                Name = cid is null ? null : nameOf(cid),
                Attack = weaponEntity.GetInt("ATK"),
                Durability = maxDur - weaponEntity.GetInt("DAMAGE"),
                MaxDurability = maxDur,
                Text = NullIfEmpty(_cards.Get(cid)?.PlainText),
            };
        }

        // Mana
        var resources = pe?.GetInt("RESOURCES") ?? 0;
        var used = pe?.GetInt("RESOURCES_USED") ?? 0;
        var temp = pe?.GetInt("TEMP_RESOURCES") ?? 0;
        var locked = pe?.GetInt("OVERLOAD_LOCKED") ?? 0;
        var mana = new ManaView
        {
            Max = resources,
            Used = used,
            Temporary = temp,
            OverloadLocked = locked,
            OverloadOwed = pe?.GetInt("OVERLOAD_OWED") ?? 0,
            // Constaté (partie avec Surcharge) : les cristaux verrouillés sont déjà inclus dans RESOURCES_USED en début de tour.
            Available = Math.Max(0, resources + temp - used),
        };

        // Plateau
        var options = engine.OptionsFresh ? engine.CurrentOptions : null;
        bool? ServerAllows(int entityId)
        {
            if (options is null || !isLocal || engine.CurrentPlayerId != playerId) return null;
            return options.Options.Any(o => o.EntityId == entityId && o.Playable);
        }

        var board = mine
            .Where(e => e.Zone == "PLAY" && e.CardType is "MINION" or "LOCATION")
            .OrderBy(e => e.ZonePosition)
            .Select(e =>
            {
                var (cid, _) = Visibility.VisibleCardId(e, local);
                var card = _cards.Get(cid);
                var maxHp = e.GetInt("HEALTH");
                var kws = new List<string>();
                foreach (var t in KeywordTags) if (e.Flag(t)) kws.Add(t);
                var sp = e.GetInt("SPELLPOWER");
                if (sp != 0) kws.Add($"SPELLPOWER+{sp}");
                return new MinionView
                {
                    EntityId = e.Id,
                    CardId = cid,
                    Name = cid is null ? null : nameOf(cid),
                    Type = e.CardType,
                    Position = e.ZonePosition,
                    Attack = e.GetInt("ATK"),
                    Health = maxHp - e.GetInt("DAMAGE"),
                    MaxHealth = maxHp,
                    Damage = e.GetInt("DAMAGE"),
                    BaseAttack = card?.Attack,
                    BaseHealth = card?.Health,
                    BaseCost = card?.Cost,
                    Races = card?.Races?.ToList() ?? new List<string>(),
                    Keywords = kws,
                    Enchantments = EnchantmentsOf(e),
                    State = new AttackState
                    {
                        Exhausted = e.Flag("EXHAUSTED"),
                        AttacksThisTurn = e.GetInt("NUM_ATTACKS_THIS_TURN"),
                        JustPlayed = e.Flag("JUST_PLAYED"),
                        Frozen = e.Flag("FROZEN"),
                        Dormant = e.Flag("DORMANT"),
                        CantAttack = e.Flag("CANT_ATTACK"),
                        LocationCooldown = e.CardType == "LOCATION" ? e.GetInt("LOCATION_ACTION_COOLDOWN") : null,
                        ServerAllowsAction = ServerAllows(e.Id),
                    },
                    Text = NullIfEmpty(card?.PlainText),
                    EnteredTurn = e.ZoneEnteredTurn,
                    UpdatedAtRevision = e.LastChangedRevision,
                };
            })
            .ToList();

        // Main
        var isFirstPlayer = pe?.Flag("FIRST_PLAYER") == true;
        var handEntities = mine.Where(e => e.Zone == "HAND").OrderBy(e => e.ZonePosition).ToList();
        var hand = new HandView
        {
            Count = handEntities.Count,
            Cards = handEntities.Select(e =>
            {
                var (cid, knowledge) = Visibility.VisibleCardId(e, local);
                var card = _cards.Get(cid);
                var own = knowledge == Visibility.Own;
                int? creator = e.GetInt("CREATOR") is var c1 && c1 > 0 ? c1 : e.GetInt("DISPLAYED_CREATOR") is var c2 && c2 > 0 ? c2 : null;
                string? creatorCard = null;
                if (own && creator is int cr && engine.Entities.TryGetValue(cr, out var creatorEntity))
                    creatorCard = Visibility.HistoryCardId(creatorEntity, local);
                var isMinion = card?.Type == "MINION" || e.CardType == "MINION";
                var isWeapon = card?.Type == "WEAPON" || e.CardType == "WEAPON";
                return new HandCardView
                {
                    EntityId = e.Id,
                    Position = e.ZonePosition,
                    CardId = cid,
                    Name = cid is null ? null : nameOf(cid),
                    Type = own ? (card?.Type ?? NullIfInvalid(e.CardType)) : card?.Type,
                    Cost = own && e.GetTag("COST") is not null ? e.GetInt("COST") : null,
                    BaseCost = card?.Cost,
                    CostModified = own && card?.Cost is int bc && e.GetTag("COST") is not null && e.GetInt("COST") != bc,
                    Attack = own && (isMinion || isWeapon) ? e.GetInt("ATK") : null,
                    Health = own && isMinion ? e.GetInt("HEALTH") : own && isWeapon ? WeaponMaxDurability(e) : null,
                    BaseAttack = card?.Attack,
                    BaseHealth = isWeapon ? card?.Durability : card?.Health,
                    PoweredUp = own ? e.Flag("POWERED_UP") : null,
                    EnteredTurn = e.ZoneEnteredTurn,
                    Created = own ? !(e.CreatedDuringSetup && e.InitialZone == "DECK" && e.InitialController == playerId) : null,
                    CreatedByCardId = creatorCard,
                    Enchantments = own ? EnchantmentsOf(e) : new List<EnchantmentView>(),
                    Knowledge = knowledge,
                    Hypothesis = !own && cid is null && e.CreatedDuringSetup && e.InitialZone == "HAND" && !isFirstPlayer
                        ? "probablement La pièce (règle : le second joueur la reçoit en début de partie)"
                        : null,
                    Text = NullIfEmpty(card?.PlainText),
                    Races = card?.Races is { Count: > 0 } r ? r.ToList() : null,
                    UpdatedAtRevision = own ? e.LastChangedRevision : 0,
                };
            }).ToList(),
        };

        // Deck
        var deck = BuildDeck(engine, playerId, mine, isLocal, isLocal ? ctx.Decklist : null, nameOf, enchantments);
        if (deck.Warning is not null && isLocal)
            flags.Add(Flag("DECKLIST_MISMATCH", "warning", deck.Warning));
        if (isLocal && !deck.DecklistKnown)
            flags.Add(Flag("DECKLIST_UNKNOWN", "info", "Liste du deck inconnue : cartes restantes non calculées."));

        // Secrets et zone secrète publique
        var secrets = new List<SecretView>();
        var publicSecretZone = new List<CardRef>();
        foreach (var e in mine.Where(e => e.Zone == "SECRET").OrderBy(e => e.ZonePosition))
        {
            var (cid, knowledge) = Visibility.VisibleCardId(e, local);
            var card = _cards.Get(cid);
            var isRealSecret = e.Flag("SECRET") || card?.IsSecret == true || cid is null;
            if (isRealSecret)
            {
                secrets.Add(new SecretView
                {
                    EntityId = e.Id,
                    CardClass = e.GetTag("CLASS") ?? card?.CardClass,
                    CardId = cid,
                    Name = cid is null ? null : nameOf(cid),
                    Known = cid is not null,
                    EnteredTurn = e.ZoneEnteredTurn,
                });
            }
            else
            {
                publicSecretZone.Add(Ref(e, cid, nameOf, e.ZoneEnteredTurn, null));
            }
        }

        var graveyard = mine
            .Where(e => e.Zone == "GRAVEYARD" && e.CardType is not ("ENCHANTMENT" or "HERO_POWER" or "PLAYER" or "GAME"))
            .OrderBy(e => e.LastChangedRevision)
            .Select(e => Ref(e, Visibility.VisibleCardId(e, local).CardId, nameOf, e.ZoneEnteredTurn, null))
            .Where(r => r.CardId is not null)
            .ToList();

        var revealed = new List<CardRef>();
        if (!isLocal)
        {
            revealed = mine
                .Where(e => e.FirstPublicCardId is not null && e.CardType is not ("ENCHANTMENT" or "HERO_POWER" or "PLAYER" or "GAME") && e.Id != heroId)
                .OrderBy(e => e.PublicAtRevision ?? 0)
                .Select(e => Ref(e, e.FirstPublicCardId, nameOf, e.ZoneEnteredTurn,
                    e.GetInt("CREATOR") > 0 || e.GetInt("DISPLAYED_CREATOR") > 0 ? "créée par un effet" : null))
                .ToList();
        }

        var (thisTurn, lastTurn) = BuildTurnActivity(engine, playerId, local, nameOf);

        return new PlayerView
        {
            Side = isLocal ? "local" : "opponent",
            PlayerId = playerId,
            HeroClass = heroClass,
            Hero = hero,
            HeroPower = heroPower,
            Weapon = weapon,
            Mana = mana,
            Board = board,
            Hand = hand,
            Deck = deck,
            Secrets = secrets,
            PublicSecretZone = publicSecretZone,
            Graveyard = graveyard,
            RevealedCards = revealed,
            Fatigue = pe?.GetInt("FATIGUE") ?? 0,
            Corpses = pe?.GetInt("CORPSES") ?? 0,
            ThisTurn = thisTurn,
            LastTurn = lastTurn,
        };
    }

    /// <summary>Enchantement du Sombre don Beaux rêves sur la carte placée sur le dessus du deck (vu dans les logs).</summary>
    private const string TopOfDeckEnchantment = "EDR_100t8e1";

    private DeckView BuildDeck(GameEngine engine, int playerId, List<EntityState> mine, bool isLocal, Decklist? decklist, Func<string?, string> nameOf,
        Dictionary<int, List<EntityState>> enchantments)
    {
        var inDeck = mine.Where(e => e.Zone == "DECK").ToList();
        // Dessus du deck connu : la plus récente carte placée par Beaux rêves en premier.
        var knownTop = !isLocal ? new List<CardRef>() : inDeck
            .Where(e => e.CardId.Length > 0 && enchantments.TryGetValue(e.Id, out var list) && list.Any(en => en.CardId == TopOfDeckEnchantment))
            .Select(e => (e, en: enchantments[e.Id].Where(x => x.CardId == TopOfDeckEnchantment).Max(x => x.Id)))
            .OrderByDescending(x => x.en)
            .Select(x => Ref(x.e, x.e.CardId, nameOf, x.e.CreatedTurn, "placée sur le dessus du deck (Beaux rêves)"))
            .ToList();
        if (!isLocal || decklist is null)
        {
            var known = isLocal
                ? inDeck.Where(e => e.CardId.Length > 0).Select(e => Ref(e, e.CardId, nameOf, null, null)).ToList()
                : inDeck.Where(e => e.PublicCardId is not null).Select(e => Ref(e, e.PublicCardId, nameOf, null, "vue publiquement")).ToList();
            return new DeckView { Count = inDeck.Count, DecklistKnown = false, KnownAdded = known, UnknownCount = inDeck.Count - known.Count, KnownTop = knownTop };
        }

        var remaining = decklist.Cards.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        int leftUnknown = 0, notInList = 0;
        var offList = new List<string>();
        // Cartes du deck d'origine (créées au départ dans le deck de ce joueur) qui l'ont quitté, même si elles ont changé de camp.
        bool IsOriginal(EntityState e) => e.CreatedDuringSetup && e.InitialZone == "DECK" && e.InitialController == playerId;
        foreach (var e in engine.Entities.Values.Where(e => !e.Orphaned && IsOriginal(e) && !(e.Zone == "DECK" && e.Controller == playerId)))
        {
            var id = e.CardId.Length > 0 ? e.CardId : e.LastKnownCardId;
            if (id is null) { leftUnknown++; continue; }
            if (remaining.TryGetValue(id, out var n) && n > 0) remaining[id] = n - 1;
            else { notInList++; offList.Add(id); }
        }
        var added = inDeck.Where(e => !IsOriginal(e)).ToList();
        var addedKnown = added.Where(e => e.CardId.Length > 0).Select(e => Ref(e, e.CardId, nameOf, e.CreatedTurn, "ajoutée en cours de partie")).ToList();
        var knownRemaining = remaining.Where(kv => kv.Value > 0)
            .Select(kv => new DeckCardCount
            {
                CardId = kv.Key,
                Name = nameOf(kv.Key),
                Count = kv.Value,
                Cost = _cards.Get(kv.Key)?.Cost,
                Type = _cards.Get(kv.Key)?.Type,
                Races = _cards.Get(kv.Key)?.Races is { Count: > 0 } r ? r.ToList() : null,
            })
            .OrderBy(c => c.Cost ?? 99).ThenBy(c => c.Name, StringComparer.CurrentCulture)
            .ToList();

        var originalInDeck = inDeck.Count - added.Count;
        string? warning = null;
        var expected = knownRemaining.Sum(c => c.Count) - leftUnknown;
        if (notInList > 0)
            warning = $"{notInList} carte(s) sortie(s) du deck d'origine absente(s) de la liste « {decklist.Name ?? decklist.Source} » ({string.Join(", ", offList.Select(x => $"{nameOf(x)} [{x}]"))}) : liste probablement différente du deck joué.";
        else if (expected != originalInDeck)
            warning = $"Incohérence deck : {originalInDeck} carte(s) d'origine encore dans le deck, {expected} attendue(s) d'après la liste.";

        return new DeckView
        {
            Count = inDeck.Count,
            DecklistKnown = true,
            DecklistSource = decklist.Source,
            KnownRemaining = knownRemaining,
            KnownAdded = addedKnown,
            UnknownCount = added.Count - addedKnown.Count + leftUnknown,
            Warning = warning,
            KnownTop = knownTop,
        };
    }

    private (TurnActivity? ThisTurn, TurnActivity? LastTurn) BuildTurnActivity(GameEngine engine, int playerId, int? local, Func<string?, string> nameOf)
    {
        var owned = engine.TurnOwners.Where(kv => kv.Value == playerId).Select(kv => kv.Key).OrderBy(t => t).ToList();
        var current = engine.GameTurn;
        int? thisTurn = owned.Contains(current) && engine.TurnOwners.GetValueOrDefault(current) == playerId ? current : null;
        int? lastTurn = owned.Where(t => t != current).Select(t => (int?)t).LastOrDefault();

        TurnActivity? Build(int? turn)
        {
            if (turn is not int t) return null;
            var plays = engine.History.Where(h => h.Kind == HistoryKind.Play && !h.Rewound && h.GameTurn == t && h.PlayerId == playerId && h.SourceZone == "HAND").ToList();
            var cards = new List<CardRef>();
            var minions = new List<CardRef>();
            var types = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var h in plays)
            {
                if (h.SourceId is not int sid || !engine.Entities.TryGetValue(sid, out var e)) continue;
                var cid = Visibility.HistoryCardId(e, local);
                var card = _cards.Get(cid);
                var onBoard = e.Zone == "PLAY" && !e.Orphaned;
                var r = Ref(e, cid, nameOf, t, card?.Type == "MINION" ? (onBoard ? "encore en jeu" : "n'est plus en jeu") : null);
                cards.Add(r);
                if (card?.Type == "MINION") minions.Add(r);
                if (card?.Races is { } rs) foreach (var race in rs) types.Add(race);
                if (card?.SpellSchool is string school) types.Add("SPELL_SCHOOL:" + school);
            }
            return new TurnActivity { GameTurn = t, CardsPlayed = cards, MinionsPlayed = minions, TypesPlayed = types.ToList() };
        }

        return (Build(thisTurn), Build(lastTurn));
    }

    // ------------------------------------------------------------------ Options, choix, historique

    private LegalOptionsView? BuildOptions(GameEngine engine, Func<string?, string> nameOf, Func<int?, string> sideOf)
    {
        var set = engine.CurrentOptions;
        if (set is null) return null;
        var local = engine.LocalPlayerId;

        LegalOption Map(OptionEntry o)
        {
            EntityState? e = o.EntityId is int id ? engine.Entities.GetValueOrDefault(id) : null;
            var cid = e is null ? null : Visibility.VisibleCardId(e, local).CardId;
            var kind = o.Type switch
            {
                "END_TURN" => "end-turn",
                "POWER" when e?.CardType == "HERO_POWER" => "hero-power",
                "POWER" when e?.Zone == "HAND" => "play",
                "POWER" when e?.Zone == "PLAY" && e.CardType == "LOCATION" => "location",
                "POWER" when e?.Zone == "PLAY" && e.CardType is "MINION" or "HERO" => "attack",
                "SUBOPTION" => "sub-option",
                _ => "other",
            };
            return new LegalOption
            {
                Index = o.Index,
                Kind = kind,
                EntityId = o.EntityId,
                CardId = cid,
                Name = cid is null ? null : nameOf(cid),
                Targets = o.Targets.Where(t => t.Playable).Select(t =>
                {
                    var te = engine.Entities.GetValueOrDefault(t.EntityId);
                    var tcid = te is null ? null : Visibility.VisibleCardId(te, local).CardId;
                    return new OptionTargetView { EntityId = t.EntityId, CardId = tcid, Name = tcid is null ? null : nameOf(tcid), Side = sideOf(te?.Controller) };
                }).ToList(),
                SubOptions = o.SubOptions.Where(s => s.Playable).Select(Map).ToList(),
            };
        }

        var playable = set.Options.Where(o => o.Playable && o.Type != "END_TURN").Select(Map).ToList();
        return new LegalOptionsView
        {
            OptionsId = set.Id,
            Revision = set.Revision,
            Fresh = engine.OptionsFresh,
            CanEndTurn = set.Options.Any(o => o.Type == "END_TURN" && o.Error is "NONE" or "INVALID"),
            TotalOptions = set.Options.Count,
            Playable = playable,
        };
    }

    private ChoiceView? BuildPendingChoice(GameEngine engine, Func<string?, string> nameOf)
    {
        var local = engine.LocalPlayerId;
        if (local is null) return null;
        var pending = engine.Choices.LastOrDefault(c => !c.Resolved && c.PlayerId == local);
        if (pending is null) return null;
        string? sourceCard = null;
        if (pending.SourceId is int s && engine.Entities.TryGetValue(s, out var se)) sourceCard = Visibility.VisibleCardId(se, local).CardId;
        return new ChoiceView
        {
            Id = pending.Id,
            ChoiceType = pending.ChoiceType,
            CountMin = pending.CountMin,
            CountMax = pending.CountMax,
            SourceCardId = sourceCard,
            SourceName = sourceCard is null ? null : nameOf(sourceCard),
            Offered = pending.Offered.Select(id =>
            {
                var e = engine.Entities.GetValueOrDefault(id);
                var cid = e is null ? null : Visibility.VisibleCardId(e, local).CardId;
                var card = _cards.Get(cid);
                // Coût et statistiques actuels si le serveur les a envoyés (carte en main au mulligan), sinon valeurs de base.
                int? Tag(string t) => e is not null && e.GetTag(t) is not null ? e.GetInt(t) : null;
                var cost = cid is null ? null : Tag("COST") ?? card?.Cost;
                // Un serviteur à 0 ATQ n'a ni tag ATK ni attaque dans la base (Avocat commis d'office 0/7, 25/10/2026).
                var minion = (e?.GetTag("CARDTYPE") ?? card?.Type) == "MINION";
                var attack = cid is null ? null : Tag("ATK") ?? card?.Attack ?? (minion ? 0 : null);
                var health = cid is null ? null : Tag("HEALTH") ?? card?.Health;
                return new CardRef
                {
                    EntityId = id,
                    CardId = cid,
                    Name = cid is null ? null : nameOf(cid),
                    Type = card?.Type,
                    Cost = cost,
                    Attack = attack,
                    Health = health,
                    Text = cid is null ? null : NullIfEmpty(card?.PlainText),
                    DarkGift = e is null || cid is null ? null : DarkGifts.Describe(engine, e, local, _cards, cost, attack, health),
                };
            }).ToList(),
        };
    }

    private List<HistoryItem> BuildHistory(GameEngine engine, Func<string?, string> nameOf, Func<int?, string> sideOf, int max)
    {
        var local = engine.LocalPlayerId;
        var items = new List<HistoryItem>();

        string? CardOf(int? id) => id is int i && engine.Entities.TryGetValue(i, out var e) ? Visibility.HistoryCardId(e, local) : null;

        CardRef RefOf(int id)
        {
            var e = engine.Entities.GetValueOrDefault(id);
            var cid = e is null ? null : Visibility.HistoryCardId(e, local);
            return new CardRef { EntityId = id, CardId = cid, Name = cid is null ? "carte cachée" : nameOf(cid) };
        }

        foreach (var h in engine.History.Skip(Math.Max(0, engine.History.Count - max)))
        {
            var kind = h.Kind switch
            {
                HistoryKind.TurnStart => "turn-start",
                HistoryKind.Mulligan => "mulligan",
                HistoryKind.Play => "play",
                HistoryKind.HeroPower => "hero-power",
                HistoryKind.LocationActivation => "location",
                HistoryKind.Attack => "attack",
                HistoryKind.Trigger => "trigger",
                HistoryKind.Power => "power",
                HistoryKind.Deaths => "deaths",
                HistoryKind.Fatigue => "fatigue",
                HistoryKind.Choice => "choice",
                HistoryKind.Rewind => "rewind",
                HistoryKind.GameEnd => "game-end",
                _ => "other",
            };
            if (h.Kind == HistoryKind.Other) continue;

            var src = CardOf(h.SourceId);
            var tgt = CardOf(h.TargetId);
            var related = new List<CardRef>();
            string? detail = h.Detail;
            var player = h.Kind is HistoryKind.TurnStart ? sideOf(h.PlayerId) : h.PlayerId is null ? "system" : sideOf(h.PlayerId);

            if (h.Kind is HistoryKind.Mulligan or HistoryKind.Choice)
            {
                var choice = engine.ChoiceFor(h);
                detail = null;
                if (choice is not null)
                {
                    var isLocal = choice.PlayerId == local && local is not null;
                    var chosen = choice.Chosen ?? new List<int>();
                    if (h.Kind == HistoryKind.Mulligan)
                    {
                        // La pièce n'est jamais remplacée, même quand elle n'est pas « choisie ».
                        bool Coin(int id) => engine.Entities.TryGetValue(id, out var ce) && Cards.CardRules.IsCoin(Visibility.HistoryCardId(ce, local));
                        var replaced = choice.Offered.Where(o => !chosen.Contains(o) && !Coin(o)).ToList();
                        chosen = chosen.Concat(choice.Offered.Where(o => !chosen.Contains(o) && Coin(o))).ToList();
                        detail = $"{chosen.Count} gardée(s), {replaced.Count} remplacée(s)";
                        if (isLocal)
                        {
                            related.AddRange(chosen.Select(id => WithNote(RefOf(id), "gardée")));
                            related.AddRange(replaced.Select(id => WithNote(RefOf(id), "remplacée")));
                        }
                    }
                    else if (isLocal)
                    {
                        related.AddRange(choice.Offered.Select(id => WithNote(RefOf(id), chosen.Contains(id) ? "choisie" : "proposée")));
                        detail = choice.ChoiceType;
                    }
                    else
                    {
                        detail = $"choix adverse ({choice.ChoiceType})";
                    }
                    player = choice.PlayerId is null ? "unknown" : sideOf(choice.PlayerId);
                }
            }
            else
            {
                related.AddRange(h.Entities.Select(RefOf));
                related.AddRange(h.SecondaryEntities.Select(id => WithNote(RefOf(id), "détruit pendant l'action")));
            }

            items.Add(new HistoryItem
            {
                Seq = h.Seq,
                GameTurn = h.GameTurn,
                TurnNumber = (h.GameTurn + 1) / 2,
                Player = player,
                Kind = kind,
                SourceEntityId = h.SourceId,
                SourceCardId = src,
                SourceName = h.SourceId is null ? null : src is null ? "carte cachée" : nameOf(src),
                TargetEntityId = h.TargetId,
                TargetCardId = tgt,
                TargetName = h.TargetId is null ? null : tgt is null ? "cible cachée" : nameOf(tgt),
                Related = related,
                Detail = detail,
                Rewound = h.Rewound,
                Time = h.Time.ToString(@"hh\:mm\:ss"),
            });
        }
        return items;
    }

    // ------------------------------------------------------------------ Utilitaires

    private CardRef Ref(EntityState e, string? cardId, Func<string?, string> nameOf, int? turn, string? note) => new()
    {
        EntityId = e.Id,
        CardId = cardId,
        Name = cardId is null ? null : nameOf(cardId),
        Type = _cards.Get(cardId)?.Type,
        Turn = turn,
        Zone = e.Zone,
        Note = note,
    };

    private static CardRef WithNote(CardRef r, string note) => new()
    {
        EntityId = r.EntityId, CardId = r.CardId, Name = r.Name, Type = r.Type, Turn = r.Turn, Zone = r.Zone, Note = note,
    };

    private static int WeaponMaxDurability(EntityState e) =>
        e.GetTag("DURABILITY") is not null ? e.GetInt("DURABILITY") : e.GetInt("HEALTH");

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
    private static string? NullIfInvalid(string? s) => s is null or "INVALID" ? null : s;

    private static UncertaintyFlag Flag(string code, string severity, string message, int count = 1) =>
        new() { Code = code, Severity = severity, Message = message, Count = count };

    private static UncertaintyFlag? MapWarning(EngineWarning w) => w.Code switch
    {
        "JOINED_MID_GAME" => null, // déjà signalé par le drapeau dédié
        "GAME_RESET_APPLIED" => Flag(w.Code, "info", "Rembobinage : l'état a été reconstruit à partir de l'instantané envoyé par le serveur.", w.Count),
        "UNPARSED_LINES" => Flag(w.Code, "warning", $"Lignes non comprises par le parseur ({w.Detail}).", w.Count),
        "UNKNOWN_ENTITY_REFERENCED" => Flag(w.Code, "warning", "Des entités ont été référencées avant leur création (lignes manquantes ou partie rejointe en cours).", w.Count),
        "PARSER_EXCEPTION" => Flag(w.Code, "critical", $"Erreur interne du parseur : {w.Detail}", w.Count),
        "LOCAL_PLAYER_CONFLICT" => Flag(w.Code, "critical", $"Identification du joueur local contradictoire : {w.Detail}", w.Count),
        "BLOCK_END_WITHOUT_START" => Flag(w.Code, "warning", "Fin de bloc sans début : flux incomplet ou désordonné.", w.Count),
        "CHOSEN_WITHOUT_CHOICE" => Flag(w.Code, "warning", "Choix reçu sans la proposition correspondante.", w.Count),
        "UNRESOLVED_PLAYER_NAME" => Flag(w.Code, "warning", $"Nom de joueur non associé : {w.Detail}", w.Count),
        "DUPLICATE_ENTITY_CREATE" => Flag(w.Code, "warning", "Entité créée deux fois (flux rejoué ou désordonné).", w.Count),
        "RESET_OUTSIDE_RESET_BLOCK" => Flag(w.Code, "warning", "RESET_GAME reçu hors d'un bloc GAME_RESET.", w.Count),
        "PLAYER_ALIAS_INFERRED" => Flag(w.Code, "info", $"Nom de joueur associé par élimination : {w.Detail}", w.Count),
        _ => null, // DEFERRED_PLAYER_NAME : normal en début de partie
    };
}
