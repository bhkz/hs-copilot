Configuration : (défaut)
Cartes : 36390 cartes (cache build 255130) depuis C:\Users\thoma\AppData\Local\HsCopilot\carddb\cards.255130.frFR.json
Simulation : partie 1 (9031 lignes GameState), via pipe, vitesse ×1, attente max 60 s, horodatages actualisés.
Conseiller : openai:gpt-6.1-sol:low · 3 analyses/tour · budget partie 0,60 $
Envoi terminé en 501,7 s.
# Rapport de validation : partie 152848-1999744645

- Verdict : **À COMPLÉTER**
- Source : hdt-plugin · type GT_VS_AI · résultat WON · partie terminée
- Début 2026-10-25 15:28:48 · fin 15:37:10 · 9031 lignes · révisions 2–147

| Contrôle | Statut | Détail |
|---|---|---|
| Début de partie détecté (CREATE_GAME) | OK | partie 152848-1999744645 |
| Joueur local confirmé par le serveur ou le client | OK | preuve : SentByClient |
| Mulligan terminé | OK |  |
| Tours observés (vous / adversaire) | OK | 10 / 9 |
| Pioche en début de tour local | OK | 10 pioche(s) détectée(s) sur 10 tour(s) |
| Actions jouées (cartes, pouvoirs, lieux, attaques) | OK | vous : 22 jouées, 16 attaques · adversaire : 20 jouées, 11 attaques · 47 options envoyées |
| Fin de tour (changement de joueur) | OK | 19 début(s) de tour |
| Fin de partie et résultat | OK | WON |
| Rembobinage appliqué | non observé | 0 RESET_GAME reçu(s), 0 appliqué(s) |
| Vues du client = état reconstruit | OK | 1590 vues, 1372 identiques, 218 en retard (max 6 rév.), 0 écart(s) |
| Tour attribué au joueur local quand le serveur envoie ses options | OK | 47 blocs d'options, 0 écart(s) |
| Références de joueur toutes associées | OK | 0 changement(s) en attente |
| Coût et mana = légalité serveur | OK | 130 cartes jugées, 0 désaccord(s) |
| Aucune carte adverse cachée exposée | OK | 140 états, 256 emplacements, 0 violation(s) |
| Concordance avec HDT au repos | non observé | aucun relevé (plugin HDT absent ou partie trop rapide) |
| États incomplets après la mise en place | OK | 0 état(s) incomplet(s) sur 141, 0,0 s |
| Latence log → réception (P95 < 1000 ms, seuil indicatif) | OK | log → réception : P50 0 ms, P95 1 ms, max 7 ms (192 mesures) |
| Latence état → affichage (P95 < 250 ms, seuil indicatif) | OK | état publié → affichage : P50 0 ms, P95 2 ms, max 81 ms (142 mesures) |

## Latences

| Mesure | Nombre | P50 (ms) | P95 (ms) | Max (ms) |
|---|---|---|---|---|
| action → nouvelles options | 37 | 327 | 530 | 10224 |
| appel stratégie (LLM) | 14 | 8945 | 12846 | 12846 |
| appel stratégie → premier fragment | 14 | 2805 | 6839 | 6839 |
| début de tour → premier conseil | 10 | 10614 | 29754 | 29754 |
| début de tour → première action affichée | 10 | 4771 | 25438 | 25438 |
| décision → action affichée | 20 | 4674 | 8405 | 9262 |
| décision → conseil | 14 | 9262 | 14402 | 14402 |
| log → affichage | 141 | 2 | 79 | 191 |
| log → réception | 192 | 0 | 1 | 7 |
| plugin → réception | 191 | 0 | 1 | 7 |
| réception → état publié | 141 | 1 | 78 | 144 |
| état publié → affichage | 142 | 0 | 2 | 81 |

## Compteurs

- stratégie : actions proposées légales : 14
- stratégie : appels annulés (décision passée) : 18
- stratégie : appels envoyés : 29
- stratégie : points de décision : 32
- stratégie : points sans conseil : 17
- stratégie : premier conseil dans le budget du tour : 10
- stratégie : coût API (USD) : 0,2176
- stratégie : coût estimé des appels annulés (USD) : 0,2244
- stratégie : jetons envoyés : 109517
- stratégie : jetons reçus : 5055

## Indicateurs d'incertitude rencontrés

- DECKLIST_UNKNOWN : présent dans 141 état(s) publié(s)
- LOCAL_PLAYER_HEURISTIC : présent dans 1 état(s) publié(s)
- CHOSEN_WITHOUT_CHOICE : présent dans 141 état(s) publié(s)
- OPTIONS_STALE : présent dans 49 état(s) publié(s)

## Conseiller : openai:gpt-6.1-sol:low

- appels envoyés : 29 ; coût mesuré : 0,4420 $
- conseils publiés : 14
- conseils retirés (choix effectué) : 1
- conseils retirés (conseil suivi) : 2
- conseils retirés (autre action jouée) : 11
- événements « published » : 14
- événements « invalidated » : 14
- événements « skipped » : 17

### Chronologie de l'overlay

```text
00:00.4 r1 inactif   En attente d'une décision.
00:00.6 r7 analyse   Analyse en cours…
00:09.8 r7 prêt      Remplacer Profondeurs Sanguines | Remplace Profondeurs Sanguines au mulligan ; garde Gardien des couvées et Glissedrake visionnaire pour garantir l’arme.
00:21.2 r7 périmé    Conseil retiré : choix effectué.
00:21.2 r8 inactif   Conseil retiré : choix effectué. En attente des nouvelles actions possibles…
00:21.7 r11 analyse   Analyse en cours…
00:25.9 r11 partiel   (partiel) Jouer Profondeurs Sanguines
00:26.3 r11 partiel   (partiel) Jouer Profondeurs Sanguines
00:26.6 r11 partiel   (partiel) Jouer Profondeurs Sanguines
00:26.6 r11 partiel   (partiel) Jouer Profondeurs Sanguines
00:27.2 r11 partiel   (partiel) Jouer Profondeurs Sanguines
00:30.2 r11 prêt      Jouer Profondeurs Sanguines | Joue Profondeurs Sanguines sur ton plateau pour préparer le renforcement du Gardien des couvées.
00:32.7 r11 périmé    Conseil retiré : conseil suivi.
00:32.7 r12 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
00:32.8 r13 ignoré    seule action possible : finir le tour
00:34.5 r14 inactif   Tour adverse : en attente de ton tour.
00:35.9 r17 inactif   En attente des actions possibles…
00:36.0 r18 analyse   Analyse en cours…
00:40.6 r18 partiel   (partiel) Jouer Gardien des couvées
00:41.3 r18 partiel   (partiel) Jouer Gardien des couvées
00:41.9 r18 partiel   (partiel) Jouer Gardien des couvées
00:42.0 r18 partiel   (partiel) Jouer Gardien des couvées
00:42.4 r18 partiel   (partiel) Jouer Gardien des couvées
00:47.7 r18 prêt      Jouer Gardien des couvées | Joue Gardien des couvées sur ton plateau pour développer une 2/3 et potentiellement équiper l’épée.
00:50.9 r18 périmé    Conseil retiré : autre action jouée.
00:50.9 r19 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
00:51.0 r20 analyse   Analyse en cours…
00:54.9 r21 inactif   En attente des actions possibles…
00:55.1 r22 analyse   Analyse en cours…
01:03.5 r22 partiel   (partiel) Finir le tour
01:03.9 r22 partiel   (partiel) Finir le tour
01:03.9 r23 inactif   Tour adverse : en attente de ton tour.
01:05.2 r26 inactif   En attente des actions possibles…
01:05.3 r27 analyse   Analyse en cours…
01:08.0 r27 partiel   (partiel) Jouer Forgeron dimensionnel
01:08.1 r27 partiel   (partiel) Jouer Forgeron dimensionnel
01:08.5 r27 partiel   (partiel) Jouer Forgeron dimensionnel
01:08.5 r27 partiel   (partiel) Jouer Forgeron dimensionnel
01:09.1 r27 partiel   (partiel) Jouer Forgeron dimensionnel
01:13.3 r27 prêt      Jouer Forgeron dimensionnel | Joue Forgeron dimensionnel sur ton plateau pour renforcer Gardien des couvées et Glissedrake visionnaire en main.
01:43.7 r27 périmé    Conseil retiré : autre action jouée.
01:43.7 r28 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
01:43.9 r29 analyse   Analyse en cours…
01:49.1 r29 partiel   (partiel) Attaquer avec Voyante distordue par le temps → Ennuy-o-tron
01:49.5 r29 partiel   (partiel) Attaquer avec Voyante distordue par le temps → Ennuy-o-tron
01:50.2 r29 partiel   (partiel) Attaquer avec Voyante distordue par le temps → Ennuy-o-tron
01:50.3 r29 partiel   (partiel) Attaquer avec Voyante distordue par le temps → Ennuy-o-tron
01:50.9 r30 inactif   En attente des actions possibles…
01:51.0 r31 analyse   Analyse en cours…
01:55.0 r32 inactif   En attente des actions possibles…
01:55.1 r33 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
01:59.5 r34 inactif   Tour adverse : en attente de ton tour.
02:01.1 r38 inactif   En attente des actions possibles…
02:01.2 r39 analyse   Analyse en cours…
02:05.9 r39 partiel   (partiel) Jouer Glissedrake visionnaire
02:06.3 r39 partiel   (partiel) Jouer Glissedrake visionnaire
02:06.9 r39 partiel   (partiel) Jouer Glissedrake visionnaire
02:07.0 r39 partiel   (partiel) Jouer Glissedrake visionnaire
02:07.5 r39 partiel   (partiel) Jouer Glissedrake visionnaire
02:12.4 r39 prêt      Jouer Glissedrake visionnaire | Joue Glissedrake visionnaire sur ton plateau pour développer une menace 5/8 insaisissable.
02:28.5 r39 périmé    Conseil retiré : autre action jouée.
02:28.5 r40 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
02:28.7 r41 analyse   Analyse en cours…
02:31.8 r42 inactif   En attente des actions possibles…
02:31.9 r43 analyse   Analyse en cours…
02:36.7 r44 inactif   En attente des actions possibles…
02:36.8 r45 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
02:46.5 r46 inactif   En attente des actions possibles…
02:46.8 r48 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
02:50.6 r49 inactif   Tour adverse : en attente de ton tour.
02:52.1 r53 inactif   En attente des actions possibles…
02:52.2 r54 analyse   Analyse en cours…
02:58.7 r54 partiel   (partiel) Jouer Forgeron dimensionnel
02:59.1 r54 partiel   (partiel) Jouer Forgeron dimensionnel
02:59.8 r54 partiel   (partiel) Jouer Forgeron dimensionnel
02:59.8 r54 partiel   (partiel) Jouer Forgeron dimensionnel
03:00.4 r54 partiel   (partiel) Jouer Forgeron dimensionnel
03:05.3 r54 prêt      Jouer Forgeron dimensionnel | Joue Forgeron dimensionnel sur ton plateau pour renforcer tes trois serviteurs en main, notamment Grommash Hurlenfer.
03:10.7 r54 périmé    Conseil retiré : autre action jouée.
03:10.7 r55 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
03:10.8 r56 analyse   Analyse en cours…
03:14.8 r56 partiel   (partiel) Activer Profondeurs Sanguines → Écuyère d’Argent
03:15.1 r56 partiel   (partiel) Activer Profondeurs Sanguines → Écuyère d’Argent
03:15.6 r57 inactif   En attente des actions possibles…
03:15.8 r58 analyse   Analyse en cours…
03:20.1 r59 inactif   En attente des actions possibles…
03:20.3 r60 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
03:25.2 r61 inactif   Tour adverse : en attente de ton tour.
03:27.2 r66 inactif   En attente des actions possibles…
03:27.3 r67 analyse   Analyse en cours…
03:29.6 r67 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:30.2 r67 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:30.8 r67 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:30.8 r67 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:31.6 r67 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:36.1 r67 prêt      Jouer Frappe anticipée → Chef de raid | Joue Frappe anticipée sur le Chef de raid pour supprimer son aura et piocher.
03:47.7 r67 périmé    Conseil retiré : autre action jouée.
03:47.7 r68 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
03:47.8 r69 analyse   Analyse en cours…
03:53.6 r69 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:53.9 r69 partiel   (partiel) Jouer Frappe anticipée → Chef de raid
03:54.0 r70 inactif   En attente des actions possibles…
03:54.1 r71 analyse   Analyse en cours…
04:00.6 r71 partiel   (partiel) Jouer Forgeron dimensionnel
04:01.2 r71 partiel   (partiel) Jouer Forgeron dimensionnel
04:01.8 r71 partiel   (partiel) Jouer Forgeron dimensionnel
04:01.8 r71 partiel   (partiel) Jouer Forgeron dimensionnel
04:02.4 r71 partiel   (partiel) Jouer Forgeron dimensionnel
04:04.6 r72 inactif   En attente des actions possibles…
04:04.7 r73 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
04:13.4 r74 inactif   Tour adverse : en attente de ton tour.
04:16.0 r81 inactif   En attente des actions possibles…
04:16.2 r82 analyse   Analyse en cours…
04:23.3 r82 partiel   (partiel) Attaquer avec Dragonnet du Vol infini → Chef de raid
04:23.6 r82 partiel   (partiel) Attaquer avec Dragonnet du Vol infini → Chef de raid
04:24.1 r82 partiel   (partiel) Attaquer avec Dragonnet du Vol infini → Chef de raid
04:24.1 r82 partiel   (partiel) Attaquer avec Dragonnet du Vol infini → Chef de raid
04:24.6 r82 partiel   (partiel) Attaquer avec Dragonnet du Vol infini → Chef de raid
04:28.9 r82 prêt      Attaquer avec Dragonnet du Vol infini → Chef de raid | Attaquez le Chef de raid avec le Dragonnet du Vol infini pour supprimer son aura grâce à Toxicité.
04:41.9 r82 périmé    Conseil retiré : autre action jouée.
04:41.9 r83 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
04:42.0 r84 analyse   Analyse en cours…
04:43.5 r85 inactif   En attente des actions possibles…
04:43.7 r86 analyse   Analyse en cours…
04:49.3 r87 inactif   En attente des actions possibles…
04:49.5 r88 analyse   Analyse en cours…
04:53.6 r89 inactif   En attente des actions possibles…
04:53.7 r90 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
04:57.0 r91 inactif   En attente des actions possibles…
04:57.2 r92 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
05:00.7 r93 inactif   En attente des actions possibles…
05:00.9 r94 ignoré    seule action possible : finir le tour
05:03.9 r95 inactif   Tour adverse : en attente de ton tour.
05:05.9 r100 inactif   En attente des actions possibles…
05:06.0 r101 analyse   Analyse en cours…
05:09.2 r101 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:09.5 r101 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:10.2 r101 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:10.2 r101 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:10.9 r101 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:16.5 r101 prêt      Attaquer avec Glissedrake visionnaire → Gul’dan | Attaque Gul’dan avec Glissedrake visionnaire pour infliger 11 dégâts et préparer la pression du prochain tour.
05:23.9 r101 périmé    Conseil retiré : autre action jouée.
05:23.9 r102 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
05:24.1 r103 analyse   Analyse en cours…
05:31.4 r103 partiel   (partiel) Jouer Gardien des couvées
05:31.8 r103 partiel   (partiel) Jouer Gardien des couvées
05:32.4 r103 partiel   (partiel) Jouer Gardien des couvées
05:32.4 r103 partiel   (partiel) Jouer Gardien des couvées
05:32.9 r103 partiel   (partiel) Jouer Gardien des couvées
05:38.5 r103 prêt      Jouer Gardien des couvées | Joue Gardien des couvées pour équiper l’épée et préparer la pression sur Gul’dan.
05:40.2 r103 périmé    Conseil retiré : conseil suivi.
05:40.2 r104 inactif   Conseil retiré : conseil suivi. En attente des nouvelles actions possibles…
05:40.3 r105 analyse   Analyse en cours…
05:47.1 r105 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:47.5 r105 partiel   (partiel) Attaquer avec Glissedrake visionnaire → Gul’dan
05:47.6 r106 inactif   En attente des actions possibles…
05:47.7 r107 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
05:54.1 r108 inactif   En attente des actions possibles…
05:54.2 r109 ignoré    seule action possible : finir le tour
05:58.7 r110 inactif   Tour adverse : en attente de ton tour.
06:00.3 r114 inactif   En attente des actions possibles…
06:00.4 r115 analyse   Analyse en cours…
06:05.4 r115 partiel   (partiel) Jouer Grommash Hurlenfer
06:05.9 r115 partiel   (partiel) Jouer Grommash Hurlenfer
06:06.3 r115 partiel   (partiel) Jouer Grommash Hurlenfer
06:06.3 r115 partiel   (partiel) Jouer Grommash Hurlenfer
06:06.7 r115 partiel   (partiel) Jouer Grommash Hurlenfer
06:10.6 r115 prêt      Jouer Grommash Hurlenfer | Joue Grommash Hurlenfer pour préparer une attaque létale sur Gul’dan avec les Profondeurs Sanguines. [létal plausible]
06:23.9 r115 périmé    Conseil retiré : autre action jouée.
06:23.9 r116 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
06:24.1 r117 analyse   Analyse en cours…
06:32.0 r118 inactif   En attente des actions possibles…
06:32.2 r119 analyse   Analyse en cours…
06:38.7 r120 inactif   En attente des actions possibles…
06:38.9 r121 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
06:44.3 r122 inactif   En attente des actions possibles…
06:44.5 r123 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
06:51.7 r124 inactif   En attente des actions possibles…
06:51.9 r125 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
06:57.1 r126 inactif   En attente des actions possibles…
06:57.3 r127 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
07:03.6 r128 inactif   Tour adverse : en attente de ton tour.
07:05.6 r133 inactif   En attente des actions possibles…
07:05.7 r134 analyse   Analyse en cours…
07:08.1 r134 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:08.6 r134 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:09.0 r134 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:09.1 r134 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:10.8 r134 prêt      Attaquer avec Grommash Hurlenfer → Gul’dan | Attaquez Gul’dan avec Grommash Hurlenfer pour infliger les dégâts létaux. [létal visible]
07:28.9 r134 périmé    Conseil retiré : autre action jouée.
07:28.9 r135 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
07:29.1 r136 analyse   Analyse en cours…
07:31.0 r137 inactif   En attente des actions possibles…
07:31.2 r138 analyse   Analyse en cours…
07:33.4 r139 inactif   En attente des actions possibles…
07:33.6 r140 analyse   Analyse en cours…
07:37.8 r140 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:38.3 r140 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:38.7 r140 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:38.7 r140 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:40.4 r140 prêt      Attaquer avec Grommash Hurlenfer → Gul’dan | Attaque Gul’dan avec Grommash Hurlenfer pour infliger les dégâts létaux. [létal visible]
07:45.8 r140 périmé    Conseil retiré : autre action jouée.
07:45.8 r141 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
07:46.0 r142 analyse   Analyse en cours…
07:50.5 r142 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:51.0 r142 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:51.4 r142 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:51.4 r142 partiel   (partiel) Attaquer avec Grommash Hurlenfer → Gul’dan
07:53.5 r142 prêt      Attaquer avec Grommash Hurlenfer → Gul’dan | Attaque Gul’dan avec Grommash Hurlenfer pour infliger les dégâts létaux. [létal visible]
08:13.6 r142 périmé    Conseil retiré : autre action jouée.
08:13.6 r143 inactif   Conseil retiré : autre action jouée. En attente des nouvelles actions possibles…
08:13.7 r144 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
08:15.6 r145 inactif   En attente des actions possibles…
08:15.8 r146 ignoré    3 analyses déjà faites ce tour : Ctrl+Alt+R pour réanalyser
08:22.1 r147 inactif   Partie terminée.
```
Rapport enregistré : bench/live/reports\20261025_152848_152848-1999744645.md
