# Architecture

## Vue d'ensemble

```
 Hearthstone ──écrit──> Power.log
                           │
          ┌────────────────┴──────────────────┐
          │ (lu par HDT)                       │ (repli : lecture directe, sans déplacer le fichier)
          ▼                                    ▼
 ┌──────────────────────┐           ┌──────────────────────┐
 │ HDT + plugin          │           │ PowerLogTailer        │
 │ HsCopilot.Collector   │           │ (HsCopilot.Core)      │
 │ net472, dans HDT      │           └──────────┬───────────┘
 └──────────┬───────────┘                      │
            │ named pipe local, NDJSON          │
            │ (lignes GameState brutes          │
            │  + annotations HDT)               │
            ▼                                   ▼
 ┌────────────────────────────────────────────────────────────┐
 │ CopilotSession (HsCopilot.Core, .NET 10)                    │
 │  - une seule source active à la fois (arbitrage)            │
 │  - un seul fil de traitement                                │
 │  - journal brut par partie                                  │
 │                                                              │
 │  GameEngine ──> GameStateProjector ──> GameStateSnapshot v1  │
 │  (état complet     (règles de           (observable, versionné)
 │   reçu)             visibilité)                              │
 └───────────────┬──────────────────────────────────┬─────────┘
                 ▼                                  ▼
       HsCopilot.App (WPF)                   hscopilot (CLI)
       fenêtre de diagnostic                 live / state / verify
```

## Composants (état au 25/10/2026)

| Composant demandé | Réalisation | État |
|---|---|---|
| Game Collector | `src/HsCopilot.Collector` : plugin HDT net472 | livré, chargé par HDT 1.59.7 |
| State Engine | `GameEngine` + `GameStateProjector` (`src/HsCopilot.Core`) | livré, vérifié sur 62 parties réelles |
| Rules Engine | non commencé ; le MVP 1 expose les **actions légales du serveur** | MVP 5 |
| Strategy Engine | contrats, validateur, invalidation, garde-fou anti-fuite et coordinateur prêts (`src/HsCopilot.Core/Strategy`) ; aucun LLM branché | MVP 3 |
| Overlay | non commencé (la fenêtre de diagnostic n'est pas l'overlay) | MVP 4 |
| Replay Analyzer | base posée : journal brut par partie, rejeu de fichiers, simulateur à rythme réel (`hscopilot simulate`) | MVP 2 |
| Validation en direct | `LiveValidator` : oracles, transitions, latences, rapport automatique par partie | livré |

## Source de vérité unique

La seule entrée du moteur est la **séquence des lignes `GameState.*` du Power.log**. Elle peut arriver par trois transports :

1. **Plugin HDT** (principal) : `LogEvents.OnPowerGamestateLogLine`. HDT gère la découverte du dossier de logs, la rotation des fichiers et la reprise en début de partie.
2. **Lecture directe** (repli, `PowerLogTailer`) : sous-dossier `Hearthstone_*` le plus récent, reprise à la dernière `CREATE_GAME`. Ouverture partagée ; le fichier n'est jamais déplacé ni supprimé (HDT le renomme en `Power_old.log` quand Hearthstone ne le verrouille pas).
3. **Fichier** (simulation hors ligne) : `.hdtreplay`, Power.log ou journal HsCopilot.

Règles d'arbitrage (`CopilotSession`) :

- `source = auto` : le pipe écoute en permanence. Sans plugin après `autoFallbackSeconds`, la lecture directe démarre. Dès que le plugin se connecte, la lecture directe s'arrête.
- Chaque changement de source crée un **moteur neuf** reconstruit depuis le début de la partie. Le plugin renvoie son tampon de la partie en cours ; le lecteur reprend à la dernière `CREATE_GAME`.
- Les lignes d'une source inactive sont comptées puis ignorées. Les deux flux ne sont jamais mélangés.
- Les événements HDT (`GameStart`, `TurnStart`…) et la vue HDT (`hdtCheck`) sont des **annotations** : ils ne modifient jamais l'état.

## Protocole local (plugin → application)

- Named pipe `\\.\pipe\HsCopilot.Collector.v1`, flux d'octets, une ligne JSON par message (NDJSON, UTF-8). L'application est le serveur, le plugin le client ; il se reconnecte toutes les secondes.
- Le pipe est créé par `CreateNamedPipe` avec **`PIPE_REJECT_REMOTE_CLIENTS`** et une ACL limitée à l'utilisateur courant, avec refus explicite du groupe NETWORK. Test effectué : une connexion via `\\127.0.0.1\pipe\…` ou `\\NOM-PC\pipe\…` est refusée, une connexion locale `\\.\pipe\…` est acceptée. Sans ce drapeau, la connexion par le chemin réseau aboutissait.
- Messages : `hello` (version de protocole, de HDT, du plugin), `lines` (lignes brutes, champ `backlog` pour le tampon renvoyé à la connexion), `deck` (deck actif HDT), `hdtCheck` (vue résumée HDT), `hdtEvent`, `gap` (lignes perdues si la file de 200 000 lignes déborde), `heartbeat`.
- Côté plugin, les gestionnaires d'événements HDT ne font qu'ajouter à une file. Un fil dédié écrit dans le pipe : HDT n'est jamais bloqué.
- Chaque ligne reçoit un numéro de séquence ; à la connexion, le tampon jusqu'au numéro S est envoyé, puis seules les lignes > S. Il n'y a ni doublon ni trou.

## GameEngine

- Applique les lignes dans l'ordre et maintient toutes les entités avec leurs tags (valeurs telles qu'écrites dans le log).
- **Révision** : entier strictement croissant pendant toute la vie de la session, y compris lors d'un changement de source ou d'une nouvelle partie. Il s'incrémente à la première modification qui suit une frontière de paquet serveur (`DebugPrintPowerList`, options, choix, envoi d'une option). Le moteur est déterministe : même flux, mêmes révisions.
- Le joueur local est identifié par ordre de preuve croissant :
  1. cartes identifiées en main au départ (heuristique) ;
  2. options jouables envoyées par le serveur ;
  3. choix ou option envoyés par le client ;
  4. BattleTag configuré.
  Une contradiction entre preuves fortes est signalée.
- Rembobinage : application native du `RESET_GAME` (remplacement complet des tags pour chaque entité renvoyée). Les entités non renvoyées sont marquées « orphelines » et exclues. Les actions annulées sont marquées dans l'historique. Les identifiants réutilisés donnent des entités neuves.
- Robustesse : aucune ligne ne lève d'exception. Une instruction inconnue, une fin de bloc orpheline, une entité référencée avant sa création, un nom de joueur non résolu ou une partie rejointe en cours produisent un avertissement codé, repris dans les indicateurs d'incertitude.

## Règles de visibilité (`Visibility`, seul point de sortie)

| Entité | Identité exposée |
|---|---|
| Joueur local, toute zone | ce que le client connaît (le deck n'est jamais exposé dans l'ordre) |
| Adverse en PLAY, GRAVEYARD, REMOVEDFROMGAME | identité affichée |
| Adverse en SECRET | cachée, sauf Quête/Sigil/Objectif reconnus comme non-Secrets |
| Adverse en HAND, DECK, SETASIDE | uniquement une identité **vue publiquement auparavant** (carte renvoyée en main, révélation de début de partie, tag `REVEALED=1`), marquée `public-seen` ou `public-seen-stale` si transformée depuis |
| Joueur local inconnu | tout est traité comme adverse |

Les tags des cartes cachées adverses (coût, attaque…) ne sont jamais exposés. Les **hypothèses** sont étiquetées comme telles, par exemple « probablement La pièce » pour la carte reçue en main au départ par le second joueur. Elles ne remplissent jamais `cardId`.

## Format GameState v1

Voir [GAMESTATE.md](GAMESTATE.md). Points clés : `gameId` + `revision` identifient l'état ; chaque entité porte `updatedAtRevision` ; `legalOptions.fresh` indique si les actions légales du serveur correspondent encore à l'état ; `uncertainty.flags` liste les problèmes (critique, avertissement, info) ; `uncertainty.incomplete` est vrai dès qu'un indicateur critique est présent.

## Strategy Engine : contrats (préparation du MVP 3)

```
GameStateSnapshot (options fraîches, tour local)
   │  DecisionContextBuilder
   ▼
DecisionContext ── couche 1 : état observable + hypothèses étiquetées
                ── couche 2 : LegalActions (id stable « o9.5.t21 », libellé, coût, cible)
                ── couche 3 : VerifiedSequences (vide : moteur de règles absent)
   │  PayloadLeakGuard (aucun identifiant interdit dans le message, sinon envoi bloqué)
   ▼
IStrategyProvider (LLM au MVP 3)  ──►  StrategyOutput { actionId, confidence, summary… }
   │  StrategyCoordinator : délai, annulation si l'état change, réponse tardive écartée
   │  RecommendationValidator : action légale identifiée, incertitude plancher, 20 mots, étapes « non vérifiées »
   ▼
Recommendation (gameId + revision + optionsId + action) ──► AdviceBoard (invalidation à chaque état)
```

- Le fournisseur ne choisit qu'un identifiant d'action. Partie, révision et options sont imposées par le coordinateur.
- Les conseils en temps réel sont désactivés par défaut en partie classée (`StrategyOptions.AllowRanked = false`).
- `DiagnosticFirstActionProvider` choisit la première action légale, sans stratégie. Il sert uniquement aux tests et aux mesures de la chaîne ; il n'est jamais affiché comme conseil.

## Copilote en direct (MVP 3B)

```
CopilotSession ──état publié──► AdvisorHost ──► StrategyCoordinator ──► OpenAiResponsesProvider (flux SSE)
                                   ▲                 │  AdvisorUpdate (phase, analyse rapide, conseil, numéro d'ordre)
              réglages (copilot.json)                ▼
                                          MainWindow (onglet « Conseiller ») et OverlayWindow (AdviceDisplay)
```

- **Décisions** : mulligan et choix (`DecisionContextBuilder.BuildChoice`, actions `c{choix}.{entité}`), options fraîches pendant le tour local (`Build`).
- **Déclenchement** : attente courte (300 ms) au début du tour, attente de 2,5 s pour une décision suivante du même tour, réanalyse manuelle immédiate. Pas d'appel en partie classée, en pause, quand seule la fin de tour est possible, pour une décision équivalente déjà conseillée, ou au-delà des plafonds (analyses par tour et par partie, coût par partie et par session, avec réserve avant envoi et coût estimé des appels annulés).
- **Péremption** : l'appel en cours est annulé dès que la décision est passée. Une réponse n'est publiée que si l'état n'a pas changé ; sinon elle est rattachée à un état strictement équivalent, ou écartée. Les mises à jour portent un numéro d'ordre : l'interface ignore toute mise à jour plus ancienne que la dernière affichée.
- **Affichage progressif** : dès que `actionId` est complet dans le flux et figure parmi les actions légales, l'overlay l'affiche comme « acceptée par le serveur, conseil en cours de rédaction ». Une action inventée n'est jamais affichée.
- **Létal** : `QuickAnalysis` calcule un niveau visible, conditionnel ou plausible sur l'état observable. Le validateur plafonne le niveau annoncé par le modèle à ce calcul.
- **Overlay** : fenêtre WPF au premier plan, style `WS_EX_NOACTIVATE` et `WS_EX_TOOLWINDOW`, raccourcis `RegisterHotKey` avec repli. Aucune entrée n'est envoyée au jeu.

## Validation en direct et mesures

- `LiveValidator` applique en continu les contrôles hors ligne : oracle des vues client, des tours et du mana, audit de visibilité.
- Il suit les transitions de jeu (début, mulligan, pioche, actions, fin de tour, fin de partie, Rembobinage), les états incomplets et le contrôle croisé HDT.
- Il écrit un rapport Markdown et JSON par partie dans `%LocalAppData%\HsCopilot\reports`, et l'affiche dans l'onglet « Validation ».
- `MetricsRegistry` calcule les P50/P95 (rang le plus proche) de : log → réception, plugin → réception, réception → état publié, état publié → affichage, log → affichage, action → nouvelles options, Rembobinage → état stable, et pour la stratégie appel LLM, décision → conseil, début de tour → premier conseil, avec les compteurs (légales, illégales, invalidées par raison, délais, coût).
- `hscopilot simulate` rejoue une partie à son rythme d'origine, en réécrivant les horodatages à l'heure courante. Il passe soit par le pipe, comme le plugin, soit par un Power.log écrit progressivement, comme Hearthstone.

## Fils d'exécution

- Plugin : fil HDT (lecture des logs) → file → fil d'écriture du pipe ; `OnUpdate` (fil d'interface HDT) construit la vue de contrôle au plus une fois par seconde, sous `try/catch`.
- Application : fil du serveur pipe et fil du lecteur de log → `Channel` → **un seul fil** pour le moteur → instantanés publiés au plus toutes les 100 ms → fenêtre WPF (dispatcher, fusion des mises à jour).
