# Étude technique préalable (25 octobre 2026)

Ce document consigne ce qui a été **constaté** sur la machine et dans les logs réels avant d'écrire le MVP 1.
Chaque affirmation indique sa source. Ce qui n'a pas pu être vérifié est signalé comme tel.

## 1. Environnement de développement

| Élément | Constat | Source |
|---|---|---|
| OS | Windows 11 Famille 10.0.26200 | `dotnet --info` |
| SDK .NET | 10.0.301 (runtimes 10.0.9 et 6.0.11, Windows Desktop inclus) | `dotnet --list-sdks` |
| .NET Framework | 4.8.9363 (runtime de HDT) | test d'exécution sous Windows PowerShell 5.1 |
| Pack de ciblage .NET Framework 4.7.2 | **absent**, pas de Visual Studio | `Reference Assemblies` inexistant |
| Solution retenue | paquet NuGet `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3 : le plugin net472 compile avec le SDK .NET 10 seul | compilation réussie |
| Hearthstone | `C:\Program Files (x86)\Hearthstone`, logs dans `Logs\Hearthstone_AAAA_MM_JJ_HH_MM_SS\` | système de fichiers |
| `log.config` | section `[Power]` présente avec `Verbose=True` (nécessaire) | `%LocalAppData%\Blizzard\Hearthstone\log.config` |
| Clés d'API | `OPENAI_API_KEY` définie, `ANTHROPIC_API_KEY` absente (utile au MVP 3) | variables d'environnement |

## 2. Hearthstone Deck Tracker installé

| Élément | Constat |
|---|---|
| Version | 1.59.6.9018 au début de l'étude, mise à jour automatiquement en **1.59.7.9019** au premier lancement (Velopack) |
| Cible | .NET Framework 4.7.2 (`HearthstoneDeckTracker.exe.config` : `supportedRuntime ... v4.7.2`) |
| Dossier d'installation | `%LocalAppData%\HearthstoneDeckTracker\current` |
| Données | `%AppData%\HearthstoneDeckTracker` (config, decks, replays, CardDefs, Plugins) |
| Plugin déjà présent | `HdtCollectionExporter-main` : **code source seul, sans DLL**, donc jamais chargé ; `plugins.xml` était vide |
| Dépôt GitHub public | dernière release publiée v1.55.6 (août 2026) : le source public est en retard sur le binaire installé |

### API de plugins (vérifiée par réflexion sur le binaire installé)

- `IPlugin` : `Name`, `Description`, `ButtonText`, `Author`, `Version`, `MenuItem`, `OnLoad()`, `OnUnload()`, `OnButtonPress()`, `OnUpdate()` (environ toutes les 100 ms d'après le wiki).
- `API.GameEvents` : 45 événements (`OnPlayerDraw`, `OnPlayerPlay`, `OnOpponentPlay`, `OnTurnStart(ActivePlayer)`, `OnGameStart`, `OnGameEnd`, `OnModeChanged`…). Ils transportent des objets `Card` déjà interprétés par HDT.
- `API.LogEvents` : `OnPowerGamestateLogLine`, `OnPowerLogLine`, `OnArenaLogLine`… Le source (`LogWatcherManager.cs`) montre que `OnPowerGamestateLogLine` reçoit **chaque ligne brute `GameState.*`**, et `OnPowerLogLine` les lignes `PowerTaskList.*`.
- `API.Core.Game` (`GameV2`) : `Player`, `Opponent` (`Hand`, `Board`, `Deck`, `Secrets`, `Hero`…), `Entities`, `PowerLog` (lignes GameState de la partie), `IsMulliganDone`, `CurrentGameType`…
- `Entity` : `Tags`, `CardId`, `Info` (contient des **suppositions** de HDT : `GuessedCardState`, `CardIdBeforeReveal`, `StoredCardIds`…), `Health` = HEALTH − DAMAGE.
- `DeckList.Instance.ActiveDeckVersion` : deck sélectionné (champ `Cards`, propriétés `Name`, `DeckId`).
- Chargement des plugins (source `PluginManager.cs`) : HDT copie `%AppData%\HearthstoneDeckTracker\Plugins` vers `<installation>\Plugins`, charge chaque DLL par `Assembly.LoadFrom`, **refuse les plugins de reconnexion** (« Blizzard has kindly asked us to stop supporting reconnector plugins ») et toute DLL important `iphlpapi` ou `lovepapi`.

### Choix qui en découle

Le plugin relaie les **lignes brutes GameState** (`LogEvents.OnPowerGamestateLogLine`) au lieu de copier l'état interne de HDT :

1. **Une seule source de vérité** : le flux GameState est exactement ce que lit HDT. Le repli par lecture directe du fichier donne le même flux, et le rejeu hors ligne aussi.
2. Les entités HDT mêlent observations et suppositions (`GuessedCardState`…). Le risque d'exposer une carte cachée serait réel.
3. Le flux contient ce que l'API de HDT n'expose pas proprement : **les actions légales envoyées par le serveur** (`DebugPrintOptions`), les choix (Découverte, Rembobinage), la structure des blocs (ordre de résolution).
4. Les tests et la simulation se font sans HDT. `GameV2` n'est pas une API documentée (le wiki API Reference indique « TBD »), alors que `LogEvents` tient en 7 lignes.

La vue de HDT (`Core.Game`) sert uniquement au **contrôle croisé** (onglet « Contrôle HDT »).

## 3. Format du Power.log (build client 254477)

Observé sur 2 replays HDT (`.hdtreplay` = zip contenant `output_log.txt`, lignes GameState seulement) et sur 4 Power.log bruts (62 parties, dont 59 contre l'IA).

- Forme : `D 11:10:18.2348071 GameState.DebugPrintPower() -     TAG_CHANGE Entity=38 tag=COST value=4`.
- Instructions rencontrées : `CREATE_GAME`, `GameEntity`, `Player`, `FULL_ENTITY - Creating`, `FULL_ENTITY - Updating`, `SHOW_ENTITY`, `HIDE_ENTITY`, `CHANGE_ENTITY`, `TAG_CHANGE`, `BLOCK_START/END`, `META_DATA`, `SUB_SPELL_START/END`, `SHUFFLE_DECK`, `CACHED_TAG_FOR_DORMANT_CHANGE`, `RESET_GAME`, et une ligne d'erreur interne du client `ERROR: unhandled PowType TAG_LIST_CHANGE`.
- Références d'entités : `GameEntity`, numéro, nom de joueur (`Joueur1#1001`, nom court `Joueur1`, `UNKNOWN HUMAN PLAYER`, `Aubergiste` pour l'IA), forme crochetée `[entityName=… id=46 zone=HAND zonePos=2 cardId=TIME_045 player=2]`. Les noms d'entités contiennent des espaces et des crochets (`UNKNOWN ENTITY [cardType=INVALID]`).
- **La zone/position/carte imprimée dans une référence est la vue animée du client**, en retard sur les données serveur. Exemple : une carte déjà passée en PLAY est encore imprimée `zone=HAND`. Ces champs ne servent jamais à modifier l'état.
- Le nom de l'adversaire apparaît d'abord comme `UNKNOWN HUMAN PLAYER`, puis comme son BattleTag plus tard dans la partie.
- **Ordre de fin de tour** : `CURRENT_PLAYER` passe à 1 pour le nouveau joueur **avant** l'incrément de `TURN` sur `GameEntity`.
- **Contre l'IA, l'adversaire change de nom** : « Aubergiste » dans `DebugPrintGame`, puis le nom du boss (« Seigneur Gargamoelle », « Le roi-liche », « Xavius »…) dans les `TAG_CHANGE`. Constaté dans 24 parties.
- **Surcharge** : en début de tour, les cristaux verrouillés sont déjà inclus dans `RESOURCES_USED` (constaté : RESOURCES=6, RESOURCES_USED=3, OVERLOAD_LOCKED=3, cartes à 3 jouables).
- Le client écrit parfois `GameState.ReportStuck()` quand il reste bloqué plusieurs secondes : sans effet sur l'état.
- Certaines parties contre l'IA se terminent par un script : le boss est déclaré perdant sans être tué.
- Mulligan : `DebugPrintEntityChoices` liste les cartes proposées ; `DebugPrintEntitiesChosen`/`SendChoices` listent les cartes **gardées**.
- Actions légales : `DebugPrintOptions` liste, pendant le tour du joueur local, chaque option avec `error=NONE` (jouable) ou un code (`REQ_ENOUGH_MANA`, `REQ_NOT_EXHAUSTED_LOCATION`, `REQ_YOUR_TURN`…), ainsi que les cibles valides.
- Abandon : `PLAYSTATE=CONCEDED` puis `LOST`.
- Armes : la durabilité est portée par `HEALTH` (et non `DURABILITY`), l'usure par `DAMAGE`.
- Lieux : une activation fait `DAMAGE+1` (une charge en moins), `EXHAUSTED=1` et `LOCATION_ACTION_COOLDOWN=1`. Au tour suivant du joueur, le cooldown repasse à 0 mais `EXHAUSTED` reste à 1 et le serveur refuse l'activation tout le tour (`REQ_NOT_EXHAUSTED_LOCATION`). Profondeurs Sanguines activées au tour 4 n'étaient pas réutilisables au tour 5 (un seul exemple observé).
- Parenté : le coût en main est réellement modifié par le serveur (Wyrm du pic venteux à 6 au lieu de 8 après un Dragon joué au tour précédent), avec `POWERED_UP=1`.

### Rembobinage (mécanique de l'extension « Across the Timeways »)

- Après le cri de guerre d'une carte avec Rembobinage, un choix `GENERAL` propose « Ligne temporelle actuelle » (`TIME_000ta`, garder) et « Ligne temporelle passée » (`TIME_000tb`, tag `REWIND=1`).
- Si le joueur rembobine : `BLOCK_START BlockType=GAME_RESET`, puis `RESET_GAME`, puis un `FULL_ENTITY - Updating` pour **chaque entité qui existe encore**. Le serveur renvoie l'état complet.
- Sur les 10 resets analysés, les entités absentes du renvoi sont uniquement celles créées pendant la période annulée (zones REMOVEDFROMGAME, GRAVEYARD, SETASIDE, enchantements). Aucune carte en main ou au deck n'est concernée.
- Le serveur **réutilise ensuite leurs identifiants** (constaté : entités 119 et 120 recréées après le second reset).
- HDT, lui, gère le Rembobinage en ignorant une plage horaire puis en relisant la partie (`HandleRewind` → `Core.Reset`). Le plugin peut donc recevoir la partie une deuxième fois ; le moteur la reconstruit à l'identique.

### Informations cachées présentes dans le log

Le log contient des identifiants de cartes que l'interface ne montre pas forcément. Le bloc de reset, par exemple, renvoie des card ID d'entités adverses. Les Quêtes adverses (`TLC_817t`) apparaissent en zone SECRET avec leur identifiant : elles sont publiques, contrairement aux vrais Secrets. La Pièce donnée à l'adversaire n'est **pas** identifiée dans le log. Conséquence : le moteur n'expose jamais une identité adverse sur la seule présence d'un identifiant ; il suit la **provenance** de l'information (voir ARCHITECTURE.md).

## 4. Base de cartes

- HDT télécharge les CardDefs de HearthSim (même source que HearthstoneJSON) : `%AppData%\HearthstoneDeckTracker\CardDefs\CardDefs.base.xml` (35 Mo) et `CardDefs.frFR.xml` (13 Mo), **build de données 255130**, 36 390 cartes.
- Le numéro de build client du log (254477) est différent du build de données : les deux sont conservés dans le GameState.
- Tables d'énumérations (types, classes, races, raretés, écoles de sorts) relevées par réflexion sur `HearthDb.dll`.
- Cache JSON par build dans `%LocalAppData%\HsCopilot\carddb\cards.255130.frFR.json` (premier chargement environ 1,8 s, ensuite environ 0,3 s). Le chargeur accepte aussi un `cards.json` HearthstoneJSON.

## 5. Ce qui n'a pas pu être vérifié pendant cette session

- **Une partie en direct de bout en bout** : Hearthstone n'était pas lancé. Le plugin a été chargé par le vrai HDT 1.59.7, s'est connecté et a transmis le deck actif, mais aucune ligne de partie n'a circulé en direct. La réception de `OnPowerGamestateLogLine` repose sur la lecture du source HDT et sur le format des replays HDT, qui sont exactement ces lignes.
- Le mode Champs de bataille, l'Arène, le Mercenaires et le mode spectateur n'ont pas été étudiés.
