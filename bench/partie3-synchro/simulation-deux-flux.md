# Rapport de validation : partie 191451-1215177117

- Verdict : **À COMPLÉTER**
- Source : hdt-plugin · type GT_VS_AI · résultat WON · partie terminée
- Début 2026-10-25 19:14:51 · fin 19:17:28 · 5252 lignes · révisions 2–93

| Contrôle | Statut | Détail |
|---|---|---|
| Début de partie détecté (CREATE_GAME) | OK | partie 191451-1215177117 |
| Joueur local confirmé par le serveur ou le client | OK | preuve : SentByClient |
| Mulligan terminé | OK |  |
| Tours observés (vous / adversaire) | OK | 7 / 7 |
| Pioche en début de tour local | OK | 7 pioche(s) détectée(s) sur 7 tour(s) |
| Actions jouées (cartes, pouvoirs, lieux, attaques) | OK | vous : 12 jouées, 10 attaques · adversaire : 10 jouées, 7 attaques · 28 options envoyées |
| Fin de tour (changement de joueur) | OK | 14 début(s) de tour |
| Fin de partie et résultat | OK | WON |
| Rembobinage appliqué | non observé | 0 RESET_GAME reçu(s), 0 appliqué(s) |
| Vues du client = état reconstruit | OK | 491 vues, 436 identiques, 55 en retard (max 6 rév.), 0 écart(s) |
| Tour attribué au joueur local quand le serveur envoie ses options | OK | 28 blocs d'options, 0 écart(s) |
| Références de joueur toutes associées | OK | 0 changement(s) en attente |
| Coût et mana = légalité serveur | OK | 110 cartes jugées, 0 désaccord(s) |
| Aucune carte adverse cachée exposée | OK | 85 états, 311 emplacements, 0 violation(s) |
| Concordance avec HDT au repos | non observé | aucun relevé (plugin HDT absent ou partie trop rapide) |
| États incomplets après la mise en place | OK | 0 état(s) incomplet(s) sur 87, 0,0 s |
| Latence log → réception (P95 < 1000 ms, seuil indicatif) | OK | log → réception : P50 0 ms, P95 1 ms, max 24 ms (119 mesures) |
| Latence état → affichage (P95 < 250 ms, seuil indicatif) | OK | état publié → affichage : P50 0 ms, P95 18 ms, max 77 ms (88 mesures) |

## Latences

| Mesure | Nombre | P50 (ms) | P95 (ms) | Max (ms) |
|---|---|---|---|---|
| action → nouvelles options | 21 | 187 | 218 | 5377 |
| appel stratégie (LLM) | 12 | 2792 | 3285 | 3285 |
| début de tour serveur → écran | 7 | 4766 | 7325 | 7325 |
| début de tour → premier conseil | 6 | 3178 | 3693 | 3693 |
| début de tour → première action affichée | 6 | 1715 | 1913 | 1913 |
| décision → action affichée | 14 | 1784 | 3876 | 3876 |
| décision → conseil | 12 | 3516 | 5538 | 5538 |
| log → affichage | 87 | 3 | 121 | 250 |
| log → réception | 119 | 0 | 1 | 24 |
| plugin → réception | 237 | 0 | 1 | 5 |
| réception → état publié | 87 | 2 | 111 | 195 |
| état publié → affichage | 88 | 0 | 18 | 77 |

## Compteurs

- affichage : conseils retenus jusqu'à l'affichage du tour : 7
- stratégie : actions proposées légales : 12
- stratégie : appels annulés (décision passée) : 8
- stratégie : appels envoyés : 16
- stratégie : létaux démontrés affichés sans appel : 4
- stratégie : points de décision : 20
- stratégie : points sans conseil : 6
- stratégie : premier conseil dans le budget du tour : 6
- stratégie : coût API (USD) : 0
- stratégie : coût estimé des appels annulés (USD) : 0,0598
- stratégie : jetons envoyés : 0
- stratégie : jetons reçus : 0

## Indicateurs d'incertitude rencontrés

- DECKLIST_UNKNOWN : présent dans 87 état(s) publié(s)
- LOCAL_PLAYER_HEURISTIC : présent dans 2 état(s) publié(s)
- OPTIONS_STALE : présent dans 29 état(s) publié(s)

## Conseiller : simulé (3 s ± 0,5 s, aucune stratégie)

- appels envoyés : 16 ; coût mesuré : 0,0598 $
- conseils publiés : 16
- conseils retirés (choix effectué) : 2
- conseils retirés (conseil suivi) : 9
- conseils retirés (autre action jouée) : 4
- conseils retirés (fin de partie) : 1
- événements « published » : 16
- événements « invalidated » : 16
- événements « skipped » : 6

### Chronologie de l'overlay

```text
00:00.4 r1 inactif   En attente d'une décision.
00:00.5 r4 inactif   Tour adverse : en attente de ton tour.
00:00.7 r7 analyse   Analyse en cours…
00:04.2 r7 prêt      Remplacer Dragonnet du Vol infini | Simulation : Remplacer Dragonnet du Vol infini
00:12.5 r7 périmé    Conseil retiré : choix effectué.
00:12.5 r8 inactif   Tour adverse : en attente de ton tour.
00:13.1 r11 inactif   Conseil retiré : choix effectué. En attente des nouvelles actions possibles…
00:13.4 r12 analyse   Analyse en cours…
00:14.9 r12 partiel   (partiel) Jouer La pièce [TON TOUR ARRIVE]
00:16.3 r12 prêt      Jouer La pièce | Simulation : Jouer La pièce [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
00:17.9 r12 prêt      Jouer La pièce | Simulation : Jouer La pièce [tour affiché : ACTION IMMÉDIATE]
00:20.9 r12 périmé    Conseil retiré : conseil suivi.
00:20.9 r13 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
00:21.1 r14 analyse   Analyse en cours…
00:22.5 r15 inactif   En attente des actions possibles…
00:22.7 r16 analyse   Analyse en cours…
00:26.4 r16 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
00:26.9 r17 inactif   En attente des actions possibles…
00:27.0 r18 ignoré    seule action possible : finir le tour
00:28.3 r19 inactif   Tour adverse : en attente de ton tour.
00:29.0 r22 inactif   En attente des actions possibles…
00:29.1 r23 analyse   Analyse en cours…
00:30.8 r23 partiel   (partiel) Jouer Colporteur de pétales [TON TOUR ARRIVE]
00:32.5 r23 prêt      Jouer Colporteur de pétales | Simulation : Jouer Colporteur de pétales [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
00:32.5 r23 prêt      Jouer Colporteur de pétales | Simulation : Jouer Colporteur de pétales [tour affiché : ACTION IMMÉDIATE]
00:35.5 r23 périmé    Conseil retiré : conseil suivi.
00:35.5 r24 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
00:35.7 r25 analyse   Analyse en cours…
00:37.3 r26 inactif   En attente des actions possibles…
00:37.4 r27 ignoré    seule action possible : finir le tour
00:39.9 r28 inactif   Tour adverse : en attente de ton tour.
00:40.6 r31 inactif   En attente des actions possibles…
00:40.8 r32 analyse   Analyse en cours…
00:42.3 r32 partiel   (partiel) Jouer Frappe anticipée → Garrosh Hurlenfer (allié) [TON TOUR ARRIVE]
00:43.8 r32 prêt      Jouer Frappe anticipée → Garrosh Hurlenfer (allié) | Simulation : Jouer Frappe anticipée → Garrosh Hurlenfer (allié) [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
00:44.1 r32 prêt      Jouer Frappe anticipée → Garrosh Hurlenfer (allié) | Simulation : Jouer Frappe anticipée → Garrosh Hurlenfer (allié) [tour affiché : ACTION IMMÉDIATE]
00:51.0 r32 périmé    Conseil retiré : autre action jouée.
00:51.0 r33 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
00:51.1 r34 analyse   Analyse en cours…
00:54.9 r34 partiel   (partiel) Jouer Voyante distordue par le temps
00:56.4 r34 prêt      Jouer Voyante distordue par le temps | Simulation : Jouer Voyante distordue par le temps
00:57.7 r34 périmé    Conseil retiré : conseil suivi.
00:57.7 r35 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
00:57.8 r36 analyse   Analyse en cours…
01:00.0 r37 inactif   En attente des actions possibles…
01:00.1 r38 ignoré    seule action possible : finir le tour
01:02.5 r39 inactif   Tour adverse : en attente de ton tour.
01:03.4 r43 inactif   En attente des actions possibles…
01:03.5 r44 analyse   Analyse en cours…
01:05.3 r44 partiel   (partiel) Jouer Glissedrake visionnaire [TON TOUR ARRIVE]
01:07.0 r44 prêt      Jouer Glissedrake visionnaire | Simulation : Jouer Glissedrake visionnaire [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
01:08.5 r44 prêt      Jouer Glissedrake visionnaire | Simulation : Jouer Glissedrake visionnaire [tour affiché : ACTION IMMÉDIATE]
01:12.7 r44 périmé    Conseil retiré : conseil suivi.
01:12.7 r45 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
01:12.8 r46 analyse   Analyse en cours…
01:16.2 r47 inactif   En attente des actions possibles…
01:16.4 r48 ignoré    seule action possible : finir le tour
01:18.6 r49 inactif   Tour adverse : en attente de ton tour.
01:19.4 r53 inactif   En attente des actions possibles…
01:19.6 r54 analyse   Analyse en cours…
01:21.1 r54 partiel   (partiel) Jouer Animatrice de stade [TON TOUR ARRIVE]
01:22.5 r54 prêt      Jouer Animatrice de stade | Simulation : Jouer Animatrice de stade [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
01:23.1 r54 prêt      Jouer Animatrice de stade | Simulation : Jouer Animatrice de stade [tour affiché : ACTION IMMÉDIATE]
01:28.0 r54 périmé    Conseil retiré : conseil suivi.
01:28.0 r55 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
01:28.1 r56 analyse   Analyse en cours…
01:29.9 r56 partiel   (partiel) Choisir Ligne temporelle actuelle
01:31.7 r56 prêt      Choisir Ligne temporelle actuelle | Simulation : Choisir Ligne temporelle actuelle
01:33.2 r56 périmé    Conseil retiré : choix effectué.
01:33.2 r57 inactif   Conseil retiré : choix effectué. En attente des nouvelles actions possibles…
01:33.3 r59 analyse   Analyse en cours…
01:37.0 r59 partiel   (partiel) Jouer Profondeurs Sanguines
01:38.5 r59 prêt      Jouer Profondeurs Sanguines | Simulation : Jouer Profondeurs Sanguines
01:39.3 r59 périmé    Conseil retiré : conseil suivi.
01:39.3 r60 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
01:39.4 r61 analyse   Analyse en cours…
01:43.1 r61 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:44.7 r61 prêt      Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer | Simulation : Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:46.3 r61 périmé    Conseil retiré : autre action jouée.
01:46.3 r62 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
01:46.4 r63 analyse   Analyse en cours…
01:50.3 r63 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:51.9 r63 prêt      Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer | Simulation : Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:52.1 r63 périmé    Conseil retiré : autre action jouée.
01:52.1 r64 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
01:52.2 r65 analyse   Analyse en cours…
01:56.0 r65 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:56.9 r66 inactif   En attente des actions possibles…
01:57.0 r67 ignoré    seule action possible : finir le tour
01:59.2 r68 inactif   Tour adverse : en attente de ton tour.
02:00.0 r72 inactif   En attente des actions possibles…
02:00.1 r73 analyse   Analyse en cours…
02:01.8 r73 partiel   (partiel) Jouer Wyrm du pic venteux → Garrosh Hurlenfer (allié) [TON TOUR ARRIVE]
02:03.4 r73 prêt      Jouer Wyrm du pic venteux → Garrosh Hurlenfer (allié) | Simulation : Jouer Wyrm du pic venteux → Garrosh Hurlenfer (allié) [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
02:05.5 r73 prêt      Jouer Wyrm du pic venteux → Garrosh Hurlenfer (allié) | Simulation : Jouer Wyrm du pic venteux → Garrosh Hurlenfer (allié) [tour affiché : ACTION IMMÉDIATE]
02:09.3 r73 périmé    Conseil retiré : autre action jouée.
02:09.3 r74 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
02:09.5 r75 analyse   Analyse en cours…
02:12.9 r76 inactif   En attente des actions possibles…
02:13.0 r77 analyse   Analyse en cours…
02:15.0 r78 inactif   En attente des actions possibles…
02:15.2 r79 ignoré    seule action possible : finir le tour
02:16.9 r80 inactif   Tour adverse : en attente de ton tour.
02:17.9 r85 inactif   En attente des actions possibles…
02:18.0 r86 prêt      Jouer Wyrm du pic venteux → Garrosh Hurlenfer | Létal démontré : Jouer Wyrm du pic venteux → héros adverse (5), puis la suite. [létal certain] [TON TOUR ARRIVE : retenu jusqu'à l'affichage]
02:25.2 r86 prêt      Jouer Wyrm du pic venteux → Garrosh Hurlenfer | Létal démontré : Jouer Wyrm du pic venteux → héros adverse (5), puis la suite. [létal certain] [tour affiché : ACTION IMMÉDIATE]
02:31.0 r86 périmé    Conseil retiré : conseil suivi.
02:31.0 r87 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
02:31.1 r88 prêt      Activer Profondeurs Sanguines → Wyrm du pic venteux (allié) | Létal démontré : Activer Profondeurs Sanguines → Wyrm du pic venteux (+2 ATQ), puis la suite. [létal certain]
02:33.9 r88 périmé    Conseil retiré : conseil suivi.
02:33.9 r89 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
02:34.0 r90 prêt      Attaquer avec Wyrm du pic venteux → Garrosh Hurlenfer | Létal démontré : Attaquer avec Wyrm du pic venteux → héros adverse (8), puis la suite. [létal certain]
02:35.8 r90 périmé    Conseil retiré : conseil suivi.
02:35.8 r91 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
02:36.0 r92 prêt      Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer | Létal démontré : Attaquer avec ton héros → héros adverse (4), puis la suite. [létal certain]
02:37.6 r92 périmé    Conseil retiré : fin de partie.
02:37.6 r93 inactif   Partie terminée.
```
Rapport enregistré : C:\Users\thoma\AppData\Local\HsCopilot\reports\20261025_191451_191451-1215177117.md
