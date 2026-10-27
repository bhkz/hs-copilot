# Synchronisation des conseils avec l'écran

Partie du 25/10/2026, de 18 h 34 à 18 h 40, contre l'IA. Guerrier Dragons contre Guerrier, gagnée au tour 7. Version testée : celle de l'itération Sombres dons, effort medium.

Preuves :
- Power.log brut, avec ses deux flux, anonymisé dans [ai-visual-lag-two-streams.log](../tests/HsCopilot.Core.Tests/Fixtures/ai-visual-lag-two-streams.log) ;
- trace du conseiller `20261025_183451_183451-1215177117.jsonl` ;
- capture Hearthstone `Hearthstone Screenshot 10-25-26 18.37.06.png` et capture Windows de 18:37:08 ;
- audit : [bench/partie3-synchro/audit-partie-18h34.md](../bench/partie3-synchro/audit-partie-18h34.md).

## Réponse courte

**Tu avais raison sur l'écran, pas sur l'état analysé.**

- **Ce que le copilote a analysé.** Il a lancé l'analyse quand le serveur avait déjà fini le tour adverse, commencé ton tour 4 et distribué ta pioche (Profondeurs Sanguines). Le message envoyé contenait cette pioche et les actions possibles du tour 4. Le conseil portait donc sur ton vrai tour.
- **Ce que tu voyais.** Contre l'IA, le serveur joue le tour adverse en une à deux secondes, puis Hearthstone en rejoue les animations. Ton tour 4 n'est apparu à l'écran que 10,1 s après son début côté serveur. Le début de réponse « Jouer Glissedrake » s'est affiché 3,6 s avant ton tour, et l'overlay disait « action acceptée par le serveur à cet instant ». C'était vrai pour le serveur, mais trompeur pour toi, qui voyais encore « Tour adverse » et 0/3 mana.
- **Ce qui change.**
  - Le copilote lit maintenant les lignes `PowerTaskList` du Power.log. Hearthstone les écrit quand il **exécute** les événements, au rythme des animations.
  - Tant que l'écran n'a pas atteint ton tour, le conseil s'affiche sous le titre **TON TOUR ARRIVE**, avec la mention « calculé sur ton tour, pioche comprise · Hearthstone affiche encore la fin du tour adverse ».
  - Dès que le client exécute ton tour, le même conseil devient **ACTION IMMÉDIATE**, sans nouvel appel.
  - L'analyse démarre toujours dès que le serveur a commencé ton tour : c'est ce qui permet d'avoir le conseil peu après l'apparition du tour.

## 1. Chronologie du tour 4

| # | Événement | Heure | Source |
|---|---|---|---|
| 1 | Dernière action adverse : la Fabricante d'armures attaque ton héros | 18:36:55.92 | GameState |
| 2 | Fin du tour adverse côté serveur (MAIN_END) | 18:36:56.62 | GameState |
| 3 | Changement de joueur actif : toi (CURRENT_PLAYER) | 18:36:56.62 | GameState |
| 4 | Début de ton tour 4 (TURN=8, MAIN_READY) | 18:36:56.62 | GameState |
| 5 | Mana de 3 à 4 (RESOURCES=4) | 18:36:56.62 | GameState |
| 6 | Pioche : entité 30, CORE_REV_990 Profondeurs Sanguines | 18:36:56.62 | GameState |
| 7 | Options du serveur pour ton tour, bloc id=23 | 18:36:56.69 | GameState |
| 8 | État analysé créé : révision 143, log de 18:36:56.686, pioche comprise, 5 cartes en main | 18:36:56.80 | trace (`analyzedRevision`, `drawnThisTurn`) |
| 9 | Appel envoyé à OpenAI | 18:36:57.10 | trace (`sentUtc`) |
| 10 | Début de réponse « Jouer Glissedrake visionnaire » affiché | 18:37:03.08 | trace |
| — | Hearthstone rejoue la Fabricante qui attaque ton héros | 18:37:04.80 | PowerTaskList |
| — | Capture Hearthstone : « Tour adverse », 0/3 mana, ton héros à −1 | 18:37:06 (à la seconde) | fichier de capture |
| 12 | Le client exécute le début de ton tour (TURN=8, MAIN_READY, pioche, 4 mana) | 18:37:06.07 | PowerTaskList |
| 12 | Le client exécute l'étape d'action de ton tour (MAIN_ACTION) | 18:37:06.71 | PowerTaskList |
| 11 | Conseil complet « Jouer Glissedrake visionnaire » | 18:37:08.24 | trace |

- **Faits établis par les logs** :
  - la carte piochée (Profondeurs Sanguines) était dans l'état et dans le message envoyé ;
  - c'était une analyse du nouveau tour, avec les options fraîches du tour 4, et non une ancienne réponse réutilisée ni une analyse anticipée.
- **Non prouvable** : l'instant exact où l'image change. Les logs donnent l'exécution par le client (18:37:06.07 à 06.71), la capture Hearthstone donne la seconde (18:37:06), et l'animation de pioche dure encore un peu ensuite.

## 2. Le décalage mesuré sur toutes tes parties

Mesure : début de ton tour côté serveur (GameState MAIN_ACTION), puis même étape exécutée par le client (PowerTaskList).

| Type de partie | Débuts de tour | Médiane | 90 % des cas sous | Maximum | Plus de 3 s |
|---|---|---|---|---|---|
| Contre l'IA | 625 | **9,9 s** | 17,3 s | 28,5 s | 620 |
| Classée | 35 | **1,6 s** | 2,1 s | 6,0 s | 1 |

Contre l'IA, le serveur résout les actions adverses presque instantanément, puis le client les anime une par une. En classé, l'adversaire joue en temps réel et le retard se limite aux dernières animations.

Partie de 18 h 34, tour par tour, avant correction :

| Tour | Début serveur | Analyse lancée | Premier affichage | Conseil complet | Tour affiché | Affiché avant l'écran | Conseil disponible après l'écran |
|---|---|---|---|---|---|---|---|
| 1 | 18:35:17.3 | 18:35:17.6 | 18:35:21.9 | 18:35:25.8 | 18:35:26.7 | 4,8 s | 0,0 s |
| 2 | 18:35:48.6 | 18:35:48.8 | 18:35:51.9 | 18:35:57.2 | 18:35:55.5 | 3,5 s | 1,7 s |
| 3 | 18:36:11.5 | 18:36:11.8 | 18:36:23.1 | 18:36:28.4 | 18:36:18.3 | — | 10,1 s |
| 4 | 18:36:56.6 | 18:36:56.8 | 18:37:03.0 | 18:37:08.2 | 18:37:06.7 | **3,6 s** | 1,5 s |
| 5 | 18:37:28.2 | 18:37:28.5 | 18:37:36.9 | 18:37:44.1 | 18:37:35.5 | — | 8,7 s |
| 6 | 18:38:48.5 | 18:38:48.7 | 18:38:54.0 | 18:38:59.6 | 18:38:59.3 | 5,3 s | 0,2 s |
| 7 | 18:39:23.8 | 18:39:24.0 | 18:39:24.0 (létal local) | 18:39:24.0 | 18:39:38.2 | **14,2 s** | 0,0 s |

Au tour 7, le « Létal démontré » calculé localement s'est affiché 14,2 s avant que ton tour apparaisse à l'écran.

## 3. PowerTaskList : vérifié sur le client actuel

- **Deux flux** : le Power.log d'octobre 2026 contient toujours `GameState.*`, les événements reçus du serveur, et `PowerTaskList.*`, les mêmes événements écrits quand le client les exécute. Dans cette partie, il y a 5 252 lignes GameState et 5 496 lignes PowerTaskList ; c'est la distinction décrite par HearthSim.
- **Côté HDT** : HDT expose les deux aux plugins, `OnPowerGamestateLogLine` et `OnPowerLogLine`. Notre plugin ne s'abonne qu'au premier : le journal de session ne contenait aucune ligne PowerTaskList.
- **Le choix retenu** : l'application lit elle-même les lignes PowerTaskList du Power.log actif, en partant de la fin du fichier. Aucune modification du plugin, aucun redémarrage de HDT.
- **Aucun double traitement** :
  - l'état de la partie vient uniquement de GameState ;
  - les lignes PowerTaskList ne vont qu'à l'horloge de l'affichage, qui ne suit que le tour et l'étape de l'entité de jeu ;
  - test : la partie rejouée avec ou sans les lignes PowerTaskList donne exactement le même état et le même historique.
- **Signal retenu** : l'exécution de l'étape MAIN_ACTION de ton tour par le client. C'est le signal le plus proche de « ton tour est à l'écran » ; l'instant exact de l'image n'est pas dans les logs.
- **Repli** :
  - si le Power.log n'est pas lisible, aucun conseil n'est retenu ;
  - si l'horloge ne progresse plus pendant 40 s, le conseil s'affiche normalement, avec la mention « affichage de Hearthstone non confirmé ».
  - Ce n'est pas une temporisation présentée comme une synchronisation.

## 4. Comportement dans les trois situations

| Situation | Avant | Maintenant |
|---|---|---|
| A, tour adverse, côté serveur et côté écran | aucun appel, « TOUR ADVERSE » | inchangé : aucun appel |
| B, serveur dans ton tour, écran encore dans le tour adverse | conseil affiché comme ACTION IMMÉDIATE, « acceptée par le serveur à cet instant » | analyse lancée sur ton vrai tour, pioche comprise ; titre **TON TOUR ARRIVE**, conseil marqué « calculé sur ton tour… Hearthstone affiche encore la fin du tour adverse » ; idem pour le létal local |
| C, ton tour à l'écran | ACTION IMMÉDIATE | ACTION IMMÉDIATE : le même conseil est republié sans nouvel appel au moment où le client exécute ton tour |

Dans l'autre sens, un conseil disparaît dès que le serveur termine ton tour, c'est-à-dire avant même que l'écran montre le tour adverse.

## 5. L'arbitrage mesuré

Deux possibilités s'offraient :

1. **Lancer l'analyse quand l'écran montre ton tour.** Le conseil arriverait alors toute la durée de l'analyse après l'apparition du tour.
2. **Analyser dès que le serveur commence ton tour, mais n'afficher l'action comme jouable qu'à l'écran.** C'est la solution retenue.

| Tour | Durée de l'analyse | Conseil après l'apparition du tour : solution 1 | Conseil après l'apparition du tour : solution retenue |
|---|---|---|---|
| 1 | 8,2 s | 8,2 s | 0,0 s |
| 2 | 8,4 s | 8,4 s | 1,7 s |
| 3 | 16,6 s | 16,6 s | 10,1 s |
| 4 | 11,4 s | 11,4 s | 1,5 s |
| 5 | 15,6 s | 15,6 s | 8,7 s |
| 6 | 10,9 s | 10,9 s | 0,2 s |
| 7 | létal local | ≈ 0 s | 0,0 s |

La solution retenue n'ajoute aucun délai : le conseil complet n'est jamais affiché plus tard qu'avant. Seul un affichage trop précoce devient « TON TOUR ARRIVE ». La solution 1 aurait coûté environ 8 s de plus par tour contre l'IA, sur des tours de 75 s.

Simulation de bout en bout (session et pipe, avec les deux flux, fournisseur simulé, vitesse ×2), dans [bench/partie3-synchro/simulation-deux-flux.md](../bench/partie3-synchro/simulation-deux-flux.md) :
- 13 affichages retenus (7 conseils complets, 6 débuts de réponse) ;
- 7 conseils devenus « ACTION IMMÉDIATE » quand le client a exécuté ton tour ;
- même nombre d'appels qu'avant.

## 6. Autres vérifications demandées

- **Sombres dons en direct** : la partie ne contenait aucune Découverte à Sombre don. Le seul choix était le Rembobinage. Ce n'est donc pas encore validé en direct. La trace enregistre maintenant les propositions telles qu'envoyées au modèle (champ `offered`), ce qui permettra de le vérifier à la prochaine Découverte.
- **Mulligan** :
  - Tu as suivi le conseil : Dragonnet du Vol infini et Suffusion remplacés ; Gardien des couvées, Colporteur de pétales et La pièce gardés.
  - **Défaut trouvé et corrigé** : jouant en second, La pièce figurait parmi les 5 cartes proposées. La trace la comptait « remplacée », concluait « différent du conseil » et n'écrivait le résultat qu'après que tu as joué La pièce, en y mêlant ta pioche du tour 1.
  - Maintenant :
    - La pièce n'est jamais proposée au remplacement ni comptée comme remplacée ;
    - le résultat est écrit juste après l'échange : 2 cartes reçues, Frappe anticipée et Glissedrake.
  - Le même défaut faussait le compte du mulligan adverse dans un ancien test : corrigé.
- **Létal local** : le « certain » reste réservé aux mécaniques simulées (itération précédente). Pendant le tour adverse affiché, il porte désormais le titre « TON TOUR ARRIVE ».
- **Conseils périmés** : un conseil disparaît à chaque changement d'état, y compris quand le serveur passe au tour adverse (test sur la partie réelle).
- **Coût** : aucun appel API dans cette itération. L'horloge ne change ni le moment ni le nombre des appels (test : même nombre d'appels avec ou sans elle).

## 7. Modifications du code

| Fichier | Rôle |
|---|---|
| [VisualClock.cs](../src/HsCopilot.Core/Session/VisualClock.cs) | tour et étape exécutés par le client, d'après PowerTaskList |
| [PowerLogTailer.cs](../src/HsCopilot.Core/Transport/PowerLogTailer.cs) | filtre de lignes paramétrable, départ en fin de fichier |
| [CopilotSession.cs](../src/HsCopilot.Core/Session/CopilotSession.cs) | lecture des lignes PowerTaskList vers l'horloge et le journal, jamais vers le moteur ; option `visualClockFromLog` (activée par défaut) |
| [StrategyCoordinator.cs](../src/HsCopilot.Core/Strategy/StrategyCoordinator.cs) | `AwaitingDisplay` sur chaque mise à jour, republication quand ton tour est affiché, mesure « début de tour serveur → écran » |
| [AdviceDisplay.cs](../src/HsCopilot.Core/Strategy/AdviceDisplay.cs) | titre TON TOUR ARRIVE, mention vérifiée, repli à 40 s |
| [AdvisorTrace.cs](../src/HsCopilot.Core/Strategy/AdvisorTrace.cs) | champs `awaitingDisplay`, `displayTracked`, `offered` ; La pièce dans le résultat du mulligan |
| [CardRules.cs](../src/HsCopilot.Core/Cards/CardRules.cs), [DecisionContextBuilder.cs](../src/HsCopilot.Core/Strategy/DecisionContextBuilder.cs), [GameStateProjector.cs](../src/HsCopilot.Core/State/GameStateProjector.cs) | La pièce jamais remplacée |
| [AuditCommand.cs](../src/HsCopilot.Cli/AuditCommand.cs) | section « Synchronisation avec l'écran », option `--visual <Power.log>` |
| [SimulateCommand.cs](../src/HsCopilot.Cli/SimulateCommand.cs) | rejoue aussi les lignes PowerTaskList (`--no-visual` pour l'ancien comportement) |

## 8. Tests

Il y a 222 tests, tous réussis, dont 10 nouveaux sur la partie réelle aux deux flux.

[VisualSyncTests](../tests/HsCopilot.Core.Tests/VisualSyncTests.cs) :
- les lignes PowerTaskList n'atteignent jamais le moteur ;
- le tour 4 est affiché 10,1 s après son début côté serveur ;
- le conseil du tour 4 est calculé sur le vrai tour : pioche et options du tour 4 dans le contexte réellement envoyé ;
- il reste « TON TOUR ARRIVE » jusqu'à 18:37:06.71, puis devient « ACTION IMMÉDIATE » sans nouvel appel ;
- le létal local est retenu de la même façon ;
- le mulligan n'est jamais retenu ;
- sans horloge, rien n'est retenu et les appels sont identiques ;
- le conseil disparaît quand le serveur termine le tour ;
- si l'horloge se fige, le conseil est relâché avec une mise en garde ;
- La pièce au mulligan.

[DarkGiftAndDeckTests](../tests/HsCopilot.Core.Tests/DarkGiftAndDeckTests.cs) : la trace enregistre les propositions de Découverte telles qu'envoyées.

Déjà couverts par les tests existants : réponse arrivée après un changement d'état, Rembobinage, Découverte à Sombre don.

Non couvert, faute d'exemple dans tes logs : une carte générée en début de tour.

## 9. Limites

- L'instant exact où l'image change n'est pas dans les logs. Le signal utilisé est l'exécution de ton tour par le client, juste avant l'animation de pioche.
- L'horloge lit le Power.log du dossier de session le plus récent. Si Hearthstone écrit ailleurs, ou si le fichier est illisible, aucun conseil n'est retenu ; la trace l'indique (`displayTracked: false`).
- Les choix (Découverte, Rembobinage) ne sont retenus que si ton tour n'est pas encore affiché. Le court délai d'animation à l'intérieur de ton propre tour n'est pas suivi.

## 10. Prochain test en direct

1. Lance `tools\lancer-copilote.ps1`. Rien à réinstaller : le plugin HDT est inchangé.
2. Contre l'IA, regarde le panneau à la fin de chaque tour adverse :
   - pendant les animations adverses, il affiche **TON TOUR ARRIVE** avec le conseil ;
   - quand ton tour apparaît, il passe à **ACTION IMMÉDIATE**.
3. Si possible, joue Sombre chevaucheur ou Suffusion : la trace contiendra les propositions avec leurs dons.
4. Envoie-moi la trace `.jsonl`. Je lancerai :
   ```powershell
   hscopilot advisor-audit <trace> <journaux> --visual "<dossier de session Hearthstone>\Power.log"
   ```
