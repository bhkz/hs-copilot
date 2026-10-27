namespace HsCopilot.Core.State;

// Format GameState v1 : ce que l'application expose (fenêtre de diagnostic, puis prompts LLM).
// Règle absolue : ce format ne contient que des informations observables par le joueur local.
// Les noms de propriétés sont en anglais (stables pour les machines), les messages en français.

public sealed class GameStateSnapshot
{
    public const string SchemaId = "hscopilot.gamestate/1";

    public string Schema { get; init; } = SchemaId;
    /// <summary>Identifiant de partie (heure de création + graine). Toute recommandation doit citer GameId + Revision.</summary>
    public required string GameId { get; init; }
    /// <summary>Révision de l'état, strictement croissante.</summary>
    public required int Revision { get; init; }
    /// <summary>Nombre de lignes GameState traitées pour cette partie (position exacte dans le flux).</summary>
    public required long LinesProcessed { get; init; }
    /// <summary>Horodatage (heure du log) de la dernière ligne traitée.</summary>
    public required string LastLogTime { get; init; }
    /// <summary>Profondeur de blocs ouverts : 0 = l'état est entre deux actions.</summary>
    public required int OpenBlocks { get; init; }
    public required SourceInfo Source { get; init; }
    public required GameInfo Game { get; init; }
    public PlayerView? Local { get; init; }
    public PlayerView? Opponent { get; init; }
    public LegalOptionsView? LegalOptions { get; init; }
    public ChoiceView? PendingChoice { get; init; }
    public List<HistoryItem> History { get; init; } = new();
    public required UncertaintyView Uncertainty { get; init; }

    /// <summary>
    /// Identifiants que le moteur connaît pour des cartes adverses cachées et qui n'apparaissent nulle part de façon visible.
    /// Jamais sérialisé : sert de garde-fou pour vérifier qu'aucun message sortant ne les contient.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlySet<string> ForbiddenCardIds { get; init; } = new HashSet<string>();

    /// <summary>Horodatage interne de publication (Stopwatch), pour mesurer la latence jusqu'à l'affichage. Jamais sérialisé.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public long PublishedTimestamp { get; set; }

    /// <summary>Heure (sans date) de la dernière ligne du log prise en compte. Jamais sérialisé.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan LastLogTimeOfDay { get; init; }

    /// <summary>Vrai si la dernière ligne vient du flux direct (pas d'un tampon ni d'un rejeu) : les latences « log → … » ont un sens.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool LiveTimestamps { get; set; }
}

public sealed class SourceInfo
{
    /// <summary>hdt-plugin, log-tail, replay-file, test…</summary>
    public required string Transport { get; init; }
    public string? Detail { get; init; }
    public int? ClientBuild { get; init; }
    public required string CardDbBuild { get; init; }
    public required int CardDbCount { get; init; }
}

public sealed class GameInfo
{
    public string? GameType { get; init; }
    public string? FormatType { get; init; }
    public int? ScenarioId { get; init; }
    /// <summary>Valeur brute du tag TURN (incrémentée à chaque tour de chaque joueur).</summary>
    public int GameTurn { get; init; }
    /// <summary>Numéro de tour « joueur » affiché par HDT : (GameTurn + 1) / 2.</summary>
    public int TurnNumber { get; init; }
    public string? Step { get; init; }
    /// <summary>local, opponent ou unknown.</summary>
    public required string ActivePlayer { get; init; }
    /// <summary>Joueur qui a commencé : local, opponent ou unknown.</summary>
    public required string FirstPlayer { get; init; }
    public bool MulliganDone { get; init; }
    public bool GameOver { get; init; }
    /// <summary>Résultat pour le joueur local : WON, LOST, TIED, CONCEDED.</summary>
    public string? Result { get; init; }
    /// <summary>Joueur ayant abandonné : local ou opponent (null si aucun abandon).</summary>
    public string? Conceded { get; init; }
    /// <summary>Nombre de Rembobinages (reset serveur) appliqués.</summary>
    public int ResetCount { get; init; }
}

public sealed class PlayerView
{
    /// <summary>local ou opponent.</summary>
    public required string Side { get; init; }
    public required int PlayerId { get; init; }
    public string? HeroClass { get; init; }
    public HeroView? Hero { get; init; }
    public HeroPowerView? HeroPower { get; init; }
    public WeaponView? Weapon { get; init; }
    public required ManaView Mana { get; init; }
    /// <summary>Serviteurs et lieux en jeu, dans l'ordre du plateau (gauche à droite).</summary>
    public List<MinionView> Board { get; init; } = new();
    public required HandView Hand { get; init; }
    public required DeckView Deck { get; init; }
    /// <summary>Secrets en jeu. Pour l'adversaire, l'identité est null tant qu'ils ne sont pas révélés.</summary>
    public List<SecretView> Secrets { get; init; } = new();
    /// <summary>Quêtes, Sigils, Objectifs et autres éléments publics de la zone SECRET.</summary>
    public List<CardRef> PublicSecretZone { get; init; } = new();
    public List<CardRef> Graveyard { get; init; } = new();
    /// <summary>Adversaire : cartes vues publiquement au cours de la partie (jouées, révélées, détruites).</summary>
    public List<CardRef> RevealedCards { get; init; } = new();
    public int Fatigue { get; init; }
    public int Corpses { get; init; }
    public TurnActivity? ThisTurn { get; init; }
    /// <summary>Dernier tour terminé de ce joueur (Parenté, Chronoseigneur Époque…).</summary>
    public TurnActivity? LastTurn { get; init; }
}

public sealed class HeroView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Damage { get; init; }
    public int Armor { get; init; }
    public int Attack { get; init; }
    public bool Exhausted { get; init; }
    public int AttacksThisTurn { get; init; }
    public bool Frozen { get; init; }
    public bool Immune { get; init; }
    public int UpdatedAtRevision { get; init; }
}

public sealed class HeroPowerView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public int Cost { get; init; }
    /// <summary>Déjà utilisé ce tour (tag EXHAUSTED).</summary>
    public bool Exhausted { get; init; }
    public string? Text { get; init; }
}

public sealed class WeaponView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public int Attack { get; init; }
    public int Durability { get; init; }
    public int MaxDurability { get; init; }
    public string? Text { get; init; }
}

public sealed class ManaView
{
    /// <summary>Cristaux utilisables maintenant : RESOURCES + TEMP_RESOURCES − RESOURCES_USED (les cristaux verrouillés par la Surcharge sont déjà comptés dans RESOURCES_USED).</summary>
    public int Available { get; init; }
    /// <summary>Cristaux de ce tour (tag RESOURCES).</summary>
    public int Max { get; init; }
    public int Used { get; init; }
    public int Temporary { get; init; }
    public int OverloadLocked { get; init; }
    public int OverloadOwed { get; init; }
}

public sealed class MinionView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    /// <summary>MINION ou LOCATION.</summary>
    public required string Type { get; init; }
    public int Position { get; init; }
    public int Attack { get; init; }
    /// <summary>PV actuels (HEALTH − DAMAGE). Pour un lieu : charges restantes.</summary>
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Damage { get; init; }
    public int? BaseAttack { get; init; }
    public int? BaseHealth { get; init; }
    public int? BaseCost { get; init; }
    public List<string> Races { get; init; } = new();
    /// <summary>Codes de tags actifs (TAUNT, DIVINE_SHIELD, RUSH, FROZEN, SPELLPOWER+2…).</summary>
    public List<string> Keywords { get; init; } = new();
    public List<EnchantmentView> Enchantments { get; init; } = new();
    public required AttackState State { get; init; }
    public string? Text { get; init; }
    /// <summary>Tour de jeu (GameTurn) d'arrivée sur le plateau.</summary>
    public int EnteredTurn { get; init; }
    public int UpdatedAtRevision { get; init; }
}

public sealed class AttackState
{
    public bool Exhausted { get; init; }
    public int AttacksThisTurn { get; init; }
    public bool JustPlayed { get; init; }
    public bool Frozen { get; init; }
    public bool Dormant { get; init; }
    public bool CantAttack { get; init; }
    /// <summary>Lieu : tag LOCATION_ACTION_COOLDOWN (tours de recharge restants). Null pour un serviteur.</summary>
    public int? LocationCooldown { get; init; }
    /// <summary>
    /// Selon les options légales envoyées par le serveur : vrai si une attaque/activation est proposée pour cette entité,
    /// faux si elle ne l'est pas, null si les options ne sont pas à jour (pas notre tour, ou état modifié depuis).
    /// </summary>
    public bool? ServerAllowsAction { get; init; }
}

public sealed class EnchantmentView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public string? Text { get; init; }
}

public sealed class HandView
{
    public int Count { get; init; }
    public List<HandCardView> Cards { get; init; } = new();
}

public sealed class HandCardView
{
    public required int EntityId { get; init; }
    public int Position { get; init; }
    /// <summary>null si la carte est cachée pour le joueur local.</summary>
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    /// <summary>Coût actuellement affiché (cartes du joueur local uniquement).</summary>
    public int? Cost { get; init; }
    public int? BaseCost { get; init; }
    public bool CostModified { get; init; }
    public int? Attack { get; init; }
    public int? Health { get; init; }
    public int? BaseAttack { get; init; }
    public int? BaseHealth { get; init; }
    /// <summary>Condition de la carte remplie selon le client (surbrillance, tag POWERED_UP).</summary>
    public bool? PoweredUp { get; init; }
    /// <summary>Tour de jeu (GameTurn) d'arrivée en main.</summary>
    public int EnteredTurn { get; init; }
    /// <summary>Carte créée par un effet (pas issue du deck d'origine). Joueur local uniquement.</summary>
    public bool? Created { get; init; }
    public string? CreatedByCardId { get; init; }
    public List<EnchantmentView> Enchantments { get; init; } = new();
    /// <summary>own (notre carte), public-seen (vue publiquement avant), public-seen-stale (vue puis transformée en main), hidden.</summary>
    public required string Knowledge { get; init; }
    /// <summary>Hypothèse explicitement étiquetée sur une carte cachée (jamais une certitude).</summary>
    public string? Hypothesis { get; init; }
    /// <summary>Races de la carte (DRAGON…), si elle en a : conditions « si vous avez un Dragon en main ».</summary>
    public List<string>? Races { get; init; }
    public string? Text { get; init; }
    public int UpdatedAtRevision { get; init; }
}

public sealed class DeckView
{
    public int Count { get; init; }
    public bool DecklistKnown { get; init; }
    public string? DecklistSource { get; init; }
    /// <summary>Cartes de la liste de départ pas encore vues hors du deck (calcul : liste − cartes d'origine sorties).</summary>
    public List<DeckCardCount> KnownRemaining { get; init; } = new();
    /// <summary>Cartes ajoutées au deck en cours de partie et connues du joueur.</summary>
    public List<CardRef> KnownAdded { get; init; } = new();
    /// <summary>Cartes du deck dont l'identité n'est pas déductible.</summary>
    public int UnknownCount { get; init; }
    /// <summary>
    /// Cartes dont la position est connue publiquement : placées sur le dessus du deck par un effet (Sombre don Beaux rêves).
    /// La première est la prochaine pioche normale, sauf si le deck a été mélangé depuis.
    /// </summary>
    public List<CardRef> KnownTop { get; init; } = new();
    public string? Warning { get; init; }
}

public sealed class DeckCardCount
{
    public required string CardId { get; init; }
    public string? Name { get; init; }
    public int Count { get; init; }
    public int? Cost { get; init; }
    /// <summary>MINION, SPELL, LOCATION, WEAPON (base de cartes) : effets « piochez un serviteur », probabilités.</summary>
    public string? Type { get; init; }
    public List<string>? Races { get; init; }
}

public sealed class SecretView
{
    public required int EntityId { get; init; }
    public string? CardClass { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public bool Known { get; init; }
    public int EnteredTurn { get; init; }
    /// <summary>Hypothèses explicitement étiquetées (vide en MVP 1).</summary>
    public List<string> Hypotheses { get; init; } = new();
}

public sealed class CardRef
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public int? Turn { get; init; }
    public string? Zone { get; init; }
    public string? Note { get; init; }
    /// <summary>Renseignés seulement pour les cartes proposées dans un choix (mulligan, Découverte…) : coût actuel, statistiques, texte.</summary>
    public int? Cost { get; init; }
    public int? Attack { get; init; }
    public int? Health { get; init; }
    public string? Text { get; init; }
    /// <summary>Découverte avec Sombre don : le don relié à cette proposition par le serveur, et la carte qui en résulte.</summary>
    public DarkGiftView? DarkGift { get; init; }
}

public sealed class TurnActivity
{
    public int GameTurn { get; init; }
    public List<CardRef> CardsPlayed { get; init; } = new();
    public List<CardRef> MinionsPlayed { get; init; } = new();
    /// <summary>Types de serviteur (races) et écoles de sorts des cartes jouées, pour la Parenté.</summary>
    public List<string> TypesPlayed { get; init; } = new();
}

public sealed class LegalOptionsView
{
    public int OptionsId { get; init; }
    public int Revision { get; init; }
    /// <summary>Vrai si aucune modification d'état n'a eu lieu depuis la réception de ces options.</summary>
    public bool Fresh { get; init; }
    public bool CanEndTurn { get; init; }
    public int TotalOptions { get; init; }
    public List<LegalOption> Playable { get; init; } = new();
}

public sealed class LegalOption
{
    public int Index { get; init; }
    /// <summary>play, attack, hero-power, location, end-turn, other.</summary>
    public required string Kind { get; init; }
    public int? EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    public List<OptionTargetView> Targets { get; init; } = new();
    public List<LegalOption> SubOptions { get; init; } = new();
}

public sealed class OptionTargetView
{
    public required int EntityId { get; init; }
    public string? CardId { get; init; }
    public string? Name { get; init; }
    /// <summary>local, opponent ou unknown.</summary>
    public required string Side { get; init; }
}

public sealed class ChoiceView
{
    public int Id { get; init; }
    public required string ChoiceType { get; init; }
    public int CountMin { get; init; }
    public int CountMax { get; init; }
    public string? SourceCardId { get; init; }
    public string? SourceName { get; init; }
    public List<CardRef> Offered { get; init; } = new();
}

public sealed class HistoryItem
{
    public int Seq { get; init; }
    public int GameTurn { get; init; }
    public int TurnNumber { get; init; }
    /// <summary>local, opponent ou system.</summary>
    public required string Player { get; init; }
    /// <summary>turn-start, mulligan, play, hero-power, location, attack, trigger, power, deaths, fatigue, choice, rewind, game-end, other.</summary>
    public required string Kind { get; init; }
    public int? SourceEntityId { get; init; }
    public string? SourceCardId { get; init; }
    public string? SourceName { get; init; }
    public int? TargetEntityId { get; init; }
    public string? TargetCardId { get; init; }
    public string? TargetName { get; init; }
    public List<CardRef> Related { get; init; } = new();
    public string? Detail { get; init; }
    /// <summary>Annulé par un Rembobinage.</summary>
    public bool Rewound { get; init; }
    public required string Time { get; init; }
}

public sealed class UncertaintyView
{
    /// <summary>Vrai si l'état ne doit pas être considéré comme complet (au moins un indicateur critique).</summary>
    public bool Incomplete { get; init; }
    /// <summary>Comment le joueur local a été identifié (None, HandHeuristic, ServerOptions, SentByClient, Configured).</summary>
    public required string LocalPlayerEvidence { get; init; }
    public List<UncertaintyFlag> Flags { get; init; } = new();
}

public sealed class UncertaintyFlag
{
    public required string Code { get; init; }
    /// <summary>info, warning, critical.</summary>
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public int Count { get; init; } = 1;
}
