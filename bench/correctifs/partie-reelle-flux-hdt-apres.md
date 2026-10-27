Configuration : C:\Users\thoma\AppData\Local\HsCopilot\copilot.json
Cartes : 36390 cartes (cache build 255130) depuis C:\Users\thoma\AppData\Local\HsCopilot\carddb\cards.255130.frFR.json
Simulation : partie 2 (8613 lignes GameState), via pipe, vitesse ×1, attente max 30 s, horodatages d'origine.
Conseiller : simulé (9 s ± 3 s, aucune stratégie) · 3 analyses/tour · budget partie 0,75 $
Envoi terminé en 672,9 s.
# Rapport de validation : partie 154451-1682628969

- Verdict : **À COMPLÉTER**
- Source : hdt-plugin · type GT_VS_AI · résultat WON · partie terminée
- Début 2026-10-25 16:10:14 · fin 16:21:27 · 5275 lignes · révisions 2–82

| Contrôle | Statut | Détail |
|---|---|---|
| Début de partie détecté (CREATE_GAME) | OK | partie 154451-1682628969 |
| Joueur local confirmé par le serveur ou le client | OK | preuve : SentByClient |
| Mulligan terminé | OK |  |
| Tours observés (vous / adversaire) | OK | 7 / 6 |
| Pioche en début de tour local | OK | 7 pioche(s) détectée(s) sur 7 tour(s) |
| Actions jouées (cartes, pouvoirs, lieux, attaques) | OK | vous : 10 jouées, 9 attaques · adversaire : 9 jouées, 4 attaques · 24 options envoyées |
| Fin de tour (changement de joueur) | OK | 13 début(s) de tour |
| Fin de partie et résultat | OK | WON |
| Rembobinage appliqué | OK | 1 RESET_GAME reçu(s), 1 appliqué(s) |
| Vues du client = état reconstruit | OK | 366 vues, 311 identiques, 55 en retard (max 4 rév.), 0 écart(s) |
| Tour attribué au joueur local quand le serveur envoie ses options | OK | 25 blocs d'options, 0 écart(s) |
| Références de joueur toutes associées | OK | 0 changement(s) en attente |
| Coût et mana = légalité serveur | OK | 89 cartes jugées, 0 désaccord(s) |
| Aucune carte adverse cachée exposée | OK | 74 états, 460 emplacements, 0 violation(s) |
| Concordance avec HDT au repos | non observé | aucun relevé (plugin HDT absent ou partie trop rapide) |
| États incomplets après la mise en place | OK | 0 état(s) incomplet(s) sur 76, 0,0 s |
| Latence log → réception (P95 < 1000 ms, seuil indicatif) | à examiner | log → réception : P50 1523303 ms, P95 1775823 ms, max 1775864 ms (103 mesures) |
| Latence état → affichage (P95 < 250 ms, seuil indicatif) | OK | état publié → affichage : P50 0 ms, P95 18 ms, max 80 ms (77 mesures) |

## Latences

| Mesure | Nombre | P50 (ms) | P95 (ms) | Max (ms) |
|---|---|---|---|---|
| Rembobinage → état stable | 1 | 315907 | 315907 | 315907 |
| action → nouvelles options | 17 | 355 | 329820 | 329820 |
| appel stratégie (LLM) | 12 | 7578 | 10897 | 10897 |
| début de tour → premier conseil | 6 | 8042 | 11324 | 11324 |
| début de tour → première action affichée | 6 | 4253 | 5875 | 5875 |
| décision → action affichée | 19 | 6223 | 10364 | 10364 |
| décision → conseil | 12 | 9284 | 11619 | 11619 |
| log → affichage | 76 | 1523315 | 1775831 | 1775946 |
| log → réception | 103 | 1523303 | 1775823 | 1775864 |
| plugin → réception | 187 | 0 | 1 | 2 |
| réception → état publié | 76 | 1 | 78 | 113 |
| état publié → affichage | 77 | 0 | 18 | 80 |

## Compteurs

- flux : lignes renvoyées ignorées : 3338
- stratégie : actions proposées légales : 12
- stratégie : appels annulés (décision passée) : 7
- stratégie : appels envoyés : 19
- stratégie : points de décision : 19
- stratégie : points sans conseil : 8
- stratégie : premier conseil dans le budget du tour : 6
- stratégie : coût API (USD) : 0
- stratégie : coût estimé des appels annulés (USD) : 0,1047
- stratégie : jetons envoyés : 0
- stratégie : jetons reçus : 0

## Indicateurs d'incertitude rencontrés

- DECKLIST_UNKNOWN : présent dans 76 état(s) publié(s)
- LOCAL_PLAYER_HEURISTIC : présent dans 2 état(s) publié(s)
- CHOSEN_WITHOUT_CHOICE : présent dans 76 état(s) publié(s)
- OPTIONS_STALE : présent dans 25 état(s) publié(s)
- GAME_RESET_APPLIED : présent dans 13 état(s) publié(s)

## Conseiller : simulé (9 s ± 3 s, aucune stratégie)

- appels envoyés : 19 ; coût mesuré : 0,1047 $
- conseils publiés : 12
- conseils retirés (choix effectué) : 2
- conseils retirés (conseil suivi) : 3
- conseils retirés (autre action jouée) : 6
- conseils retirés (fin de partie) : 1
- événements « published » : 12
- événements « invalidated » : 12
- événements « skipped » : 8

### Chronologie de l'overlay

```text
00:00.4 r1 inactif   En attente d'une décision.
00:00.5 r5 inactif   En attente des actions possibles…
00:00.7 r7 analyse   Analyse en cours…
00:11.0 r7 prêt      Remplacer Gardien des couvées | Simulation : Remplacer Gardien des couvées
00:20.1 r7 périmé    Conseil retiré : choix effectué.
00:20.1 r8 inactif   Conseil retiré : choix effectué. En attente des nouvelles actions possibles…
00:20.6 r11 ignoré    seule action possible : finir le tour
00:34.7 r12 inactif   Tour adverse : en attente de ton tour.
00:36.0 r15 inactif   En attente des actions possibles…
00:36.2 r16 analyse   Analyse en cours…
00:39.9 r16 partiel   (partiel) Jouer Gardien des couvées
00:43.4 r16 prêt      Jouer Gardien des couvées | Simulation : Jouer Gardien des couvées
00:51.2 r16 périmé    Conseil retiré : conseil suivi.
00:51.2 r17 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
00:51.3 r18 analyse   Analyse en cours…
00:57.2 r18 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:00.6 r18 prêt      Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer | Simulation : Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:05.2 r18 périmé    Conseil retiré : autre action jouée.
01:05.2 r19 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
01:05.3 r20 ignoré    seule action possible : finir le tour
01:10.9 r21 inactif   Tour adverse : en attente de ton tour.
01:12.3 r25 inactif   En attente des actions possibles…
01:12.4 r26 analyse   Analyse en cours…
01:17.3 r26 partiel   (partiel) Jouer Dragonnet du Vol infini
01:21.9 r26 prêt      Jouer Dragonnet du Vol infini | Simulation : Jouer Dragonnet du Vol infini
01:37.1 r26 périmé    Conseil retiré : autre action jouée.
01:37.1 r27 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
01:37.3 r28 analyse   Analyse en cours…
01:43.3 r28 partiel   (partiel) Attaquer avec Garrosh Hurlenfer → Garrosh Hurlenfer
01:46.8 r29 inactif   En attente des actions possibles…
01:46.9 r30 ignoré    seule action possible : finir le tour
01:55.0 r31 inactif   Tour adverse : en attente de ton tour.
01:56.0 r33 inactif   En attente des actions possibles…
01:56.2 r34 analyse   Analyse en cours…
02:00.3 r34 partiel   (partiel) Jouer Dragonnet du Vol infini
02:04.1 r34 prêt      Jouer Dragonnet du Vol infini | Simulation : Jouer Dragonnet du Vol infini
02:06.6 r34 périmé    Conseil retiré : autre action jouée.
02:06.6 r35 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
02:06.7 r36 analyse   Analyse en cours…
02:14.4 r36 partiel   (partiel) Jouer Voyante distordue par le temps
02:15.3 r37 inactif   En attente des actions possibles…
02:15.4 r38 analyse   Analyse en cours…
02:22.5 r38 partiel   (partiel) Attaquer avec Forgeron dimensionnel → Garrosh Hurlenfer
02:24.2 r39 inactif   En attente des actions possibles…
02:24.3 r40 ignoré    seule action possible : finir le tour
02:28.5 r41 inactif   Tour adverse : en attente de ton tour.
02:29.8 r44 inactif   En attente des actions possibles…
02:29.9 r45 analyse   Analyse en cours…
02:33.8 r45 partiel   (partiel) Jouer Dragonnet du Vol infini
02:37.3 r45 prêt      Jouer Dragonnet du Vol infini | Simulation : Jouer Dragonnet du Vol infini
02:42.7 r45 périmé    Conseil retiré : autre action jouée.
02:42.7 r46 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
02:42.8 r47 analyse   Analyse en cours…
02:50.6 r47 partiel   (partiel) Jouer Dragonnet du Vol infini
02:51.4 r48 inactif   En attente des actions possibles…
02:51.6 r49 analyse   Analyse en cours…
02:57.8 r49 partiel   (partiel) Pouvoir héroïque Gain d’armure !
03:00.0 r50 inactif   En attente des actions possibles…
03:00.2 r51 analyse   Analyse en cours…
03:06.5 r51 partiel   (partiel) Pouvoir héroïque Gain d’armure !
03:10.3 r51 prêt      Pouvoir héroïque Gain d’armure ! | Simulation : Pouvoir héroïque Gain d’armure !
03:10.8 r51 périmé    Conseil retiré : autre action jouée.
03:10.8 r52 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
03:10.9 r53 analyse   Analyse en cours…
03:18.0 r53 partiel   (partiel) Pouvoir héroïque Gain d’armure !
03:22.5 r53 prêt      Pouvoir héroïque Gain d’armure ! | Simulation : Pouvoir héroïque Gain d’armure !
03:41.2 r53 périmé    Conseil retiré : conseil suivi.
03:41.2 r54 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
03:41.3 r55 ignoré    seule action possible : finir le tour
03:44.4 r56 inactif   Tour adverse : en attente de ton tour.
03:45.6 r58 inactif   En attente des actions possibles…
03:45.8 r59 analyse   Analyse en cours…
03:50.0 r59 partiel   (partiel) Jouer Chronoseigneur Époque
03:54.0 r59 prêt      Jouer Chronoseigneur Époque | Simulation : Jouer Chronoseigneur Époque
04:16.0 r59 périmé    Conseil retiré : autre action jouée.
04:16.0 r60 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
04:16.1 r61 analyse   Analyse en cours…
04:22.7 r61 partiel   (partiel) Jouer Animatrice de stade
04:26.4 r62 inactif   En attente des actions possibles…
04:26.6 r63 analyse   Analyse en cours…
04:32.9 r63 partiel   (partiel) Jouer Animatrice de stade
04:33.5 r64 inactif   En attente des actions possibles…
04:33.6 r65 analyse   Analyse en cours…
04:40.7 r65 partiel   (partiel) Jouer Animatrice de stade
04:45.3 r65 prêt      Jouer Animatrice de stade | Simulation : Jouer Animatrice de stade
05:01.6 r65 périmé    Conseil retiré : conseil suivi.
05:01.6 r66 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
05:01.7 r67 analyse   Analyse en cours…
05:05.1 r67 partiel   (partiel) Choisir Ligne temporelle actuelle
05:08.2 r67 prêt      Choisir Ligne temporelle actuelle | Simulation : Choisir Ligne temporelle actuelle
05:15.1 r67 périmé    Conseil retiré : choix effectué.
05:15.1 r68 inactif   Conseil retiré : choix effectué. En attente des nouvelles actions possibles…
10:31.1 r70 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
10:31.2 r72 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
10:43.7 r73 inactif   En attente des actions possibles…
10:43.8 r74 ignoré    seule action possible : finir le tour
10:49.5 r75 inactif   Tour adverse : en attente de ton tour.
10:51.4 r80 inactif   En attente des actions possibles…
10:51.5 r81 analyse   Analyse en cours…
10:57.3 r81 partiel   (partiel) Jouer Chronoseigneur Époque
11:02.8 r81 prêt      Jouer Chronoseigneur Époque | Simulation : Jouer Chronoseigneur Époque
11:13.2 r81 périmé    Conseil retiré : fin de partie.
11:13.2 r82 inactif   Partie terminée.
```
Rapport enregistré : C:\Users\thoma\AppData\Local\Temp/claude/c--Users-thoma-Desktop-HSguide/f24c0e17-f6bd-4886-8fec-932936373eb9/scratchpad/reports-corr\20261025_161014_154451-1682628969.md
