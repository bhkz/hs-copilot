# Danger au prochain tour adverse, avant et après chaque coup

Journaux : 61 ; parties : 49 ; décisions d'action : 1197 ; traces : `C:\Users\thoma\AppData\Local\HsCopilot\advisor`.

## GT_RANKED

- Décisions : 1139 ; avec un conseil affiché relié à une action légale : 1056 (libellé non relié : 0).
- Origine du conseil : modèle 664, plan 323, solveur 69.

### Verdicts sur les conseils affichés

| Verdict | Conseils | complète | partielle | non simulable | modèle | plan | solveur |
|---|---|---|---|---|---|---|---|
| danger visible faible | 899 | 674 | 225 | 0 | 590 | 309 | 0 |
| non évalué | 70 | 0 | 0 | 70 | 60 | 10 | 0 |
| létal ce tour | 69 | 41 | 22 | 6 | 0 | 0 | 69 |
| défend | 7 | 2 | 5 | 0 | 5 | 2 | 0 |
| neutre | 7 | 2 | 5 | 0 | 7 | 0 | 0 |
| aucune défense simulée | 4 | 3 | 1 | 0 | 2 | 2 | 0 |

### Verdicts sur les coups joués

| Verdict | Coups | complète | partielle | non simulable | coup = conseil |
|---|---|---|---|---|---|
| danger visible faible | 946 | 718 | 228 | 0 | 840 |
| non évalué | 76 | 0 | 0 | 76 | 70 |
| létal ce tour | 62 | 38 | 18 | 6 | 57 |
| défend | 9 | 2 | 7 | 0 | 6 |
| aucune défense simulée | 7 | 4 | 3 | 0 | 3 |
| neutre | 6 | 1 | 5 | 0 | 5 |
| aggrave nettement | 1 | 0 | 1 | 0 | 0 |

### Validation de la simulation contre l'état réel suivant (coup joué, même tour)

| Statut du coup | Transitions | marge identique | meilleure marge identique |
|---|---|---|---|
| complète | 485 | 484 sur 485 (100 %) | 475 sur 485 (98 %) |
| partielle | 231 | 210 sur 231 (91 %) | 211 sur 231 (91 %) |

Écarts sur des transitions « complètes » (à expliquer) :

- 002106-1777587045 tour 5 : Jouer Frappe anticipée → Rampeur du goudron ; simulé 23, réel 20

## GT_VS_AI

- Décisions : 58 ; avec un conseil affiché relié à une action légale : 22 (libellé non relié : 0).
- Origine du conseil : modèle 12, plan 6, solveur 4.

### Verdicts sur les conseils affichés

| Verdict | Conseils | complète | partielle | non simulable | modèle | plan | solveur |
|---|---|---|---|---|---|---|---|
| danger visible faible | 18 | 7 | 11 | 0 | 12 | 6 | 0 |
| létal ce tour | 4 | 3 | 1 | 0 | 0 | 0 | 4 |

### Verdicts sur les coups joués

| Verdict | Coups | complète | partielle | non simulable | coup = conseil |
|---|---|---|---|---|---|
| danger visible faible | 47 | 33 | 14 | 0 | 16 |
| létal ce tour | 7 | 5 | 2 | 0 | 4 |
| non évalué | 4 | 0 | 0 | 4 | 0 |

### Validation de la simulation contre l'état réel suivant (coup joué, même tour)

| Statut du coup | Transitions | marge identique | meilleure marge identique |
|---|---|---|---|
| complète | 26 | 26 sur 26 (100 %) | 26 sur 26 (100 %) |
| partielle | 9 | 9 sur 9 (100 %) | 9 sur 9 (100 %) |

## Conseils qui perdent la dernière défense simulée : 0

## Coups joués qui perdent la dernière défense simulée : 0

## Conseils qui aggravent nettement le danger (critère large, non retenu) : 0

## Tours finis avec un létal adverse visible

| Partie | Type | Tour | Meilleure défense au début du tour | Marge réelle en fin de tour | Coup qui la perd | Conseillé | Issue |
|---|---|---|---|---|---|---|---|
| 003106-1777587045 | RANKED | 16 | 9 | 0 | non attribué (coup non simulable ou effet adverse) |  | mort au tour adverse suivant ; partie perdue |
| 021216-1089642197 | RANKED | 13 | -1 | -1 | — |  | abandon au tour adverse suivant ; partie abandonnée |

Tours finis avec un létal visible : 2, dont 1 avec une défense simulée disponible au début du tour ; survécu au tour adverse malgré tout : 0.

## Tours avec danger : meilleure défense au début, marge réelle à la fin

| Partie | Type | Tour | Marge au début | Meilleure défense au début | Marge réelle en fin de tour | Perte | Coups conseillés suivis | Issue |
|---|---|---|---|---|---|---|---|---|
| 213529-1502170609 | RANKED | 6 | 5 | 13 | 13 | 0 | 1 sur 3 | survit au tour adverse ; partie gagnée |
| 214523-391479413 | RANKED | 5 | 5 | 10 | 8 | 2 | 2 sur 3 | survit au tour adverse ; partie gagnée |
| 214523-391479413 | RANKED | 6 | 0 | 8 | 8 | 0 | 5 sur 5 | victoire au tour suivant ; partie gagnée |
| 230748-1495328103 | RANKED | 6 | 2 | 14 | 14 | 0 | 3 sur 3 | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 8 | 4 | 13 | 13 | 0 | 3 sur 7 | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 9 | -6 | 0 | fin de partie | ? | 2 sur 4 | ? ; partie abandonnée |
| 000900-472012207 | RANKED | 9 | 2 | 9 | 2 | 7 | 3 sur 5 | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 10 | -25 | -8 | fin de partie | ? | 3 sur 6 | ? ; partie abandonnée |
| 003106-1777587045 | RANKED | 16 | -2 | 9 | 0 | 9 | 0 sur 5 | mort au tour adverse suivant ; partie perdue |
| 010743-1093615795 | RANKED | 6 | 5 | 25 | 16 | 9 | 7 sur 8 | victoire au tour suivant ; partie gagnée |
| 021216-1089642197 | RANKED | 13 | -4 | -1 | -1 | 0 | 2 sur 3 | abandon au tour adverse suivant ; partie abandonnée |

Tours avec danger : 11 ; perte d'au moins 3 de marge pour finir à 2 ou moins : 2.

## Danger élevé mais non létal (marge 1 à 5) et conseil offensif

Conseils d'attaque au visage avec une marge de 1 à 5 : 0 ; survécu au tour adverse : 0 ; verdicts : .

## Revue : décisions conseillées avec un danger simulé

| Partie | Type | Tour | Conseil | Origine | Joué | Verdict | Statut | Marge maintenant → après | Meilleure défense avant → après | Défense simulée avant | Non simulé | Issue |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 192643-990343039 | RANKED | 7.1 | Jouer Cogneur d’œufs → Colporteur de pétales (allié) | modèle | oui | non évalué | non simulable | 21 → ? | 27 → ? | Attaquer avec Colporteur de pétales → Surveillant satyre 4/2, puis Pouvoir héroïque : +2 armure | Cogneur d’œufs : effet non simulé | ? ; partie gagnée |
| 193512-1981755535 | RANKED | 5.2 | Jouer Guerrière du Vol infini | modèle | oui | non évalué | non simulable | 26 → ? | 28 → ? | Pouvoir héroïque : +2 armure | Guerrière du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 193512-1981755535 | RANKED | 9.3 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 26 → ? | 26 → ? |  | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 194727-1945398485 | RANKED | 5.1 | Jouer Berserker hacheuse | modèle | oui | non évalué | non simulable | 25 → ? | 30 → ? | Attaquer avec Forgeron dimensionnel → Fourrageur d’étang 1/2, puis Attaquer avec Gorgonzormu → Amalgame des profondeurs 2/3, puis Pouvoir héroïque : +2 armure | Berserker hacheuse : effet non simulé | survit au tour adverse ; partie gagnée |
| 194727-1945398485 | RANKED | 6.1 | Jouer Fromage savoureux | modèle | oui | non évalué | non simulable | 26 → ? | 31 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Voyante distordue par le temps → Chef de la côte sud 3/2 | Fromage savoureux : effet non simulé | victoire au tour suivant ; partie gagnée |
| 194727-1945398485 | RANKED | 7.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 23 → ? | 31 → ? | Attaquer avec Luisaile → Chasse-marée murloc 2/1, puis Attaquer avec Voyante distordue par le temps → Éclaireur murloc 1/1, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Chronoamirale Harpagone → Gardecerf gelé 3/4 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire ce tour ; partie gagnée |
| 211732-1383214767 | RANKED | 8.1 | Jouer Le Grand Dracorex | modèle | oui | non évalué | non simulable | 32 → ? | 37 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Serpent-nuage → Écorcheur d’esprit Kaahrj 3/3 | Le Grand Dracorex : effet non simulé | survit au tour adverse ; partie gagnée |
| 211732-1383214767 | RANKED | 10.2 | Jouer Huissier antrelige | modèle | oui | non évalué | non simulable | 29 → ? | 36 → ? | Attaquer avec Avant-garde du portail → Serviteur de Zuramat 1/2, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Glissedrake visionnaire → Théotar, le duc fou 4/4 | Huissier antrelige : effet non simulé | survit au tour adverse ; partie gagnée |
| 211732-1383214767 | RANKED | 10.3 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 34 → ? | 36 → ? | Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 211732-1383214767 | RANKED | 12.4 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 36 → ? | 36 → ? |  | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 211732-1383214767 | RANKED | 13.4 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 36 → ? | 36 → ? |  | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire au tour suivant ; partie gagnée |
| 213529-1502170609 | RANKED | 6.1 | Jouer Wyrm du pic venteux → Traqueur phasique | modèle | Jouer Wyrm du pic venteux → Traqueur phasique | défend | partielle | 5 → 12 | 13 → 13 | Attaquer avec Avant-garde du portail → Traqueur phasique 2/3, puis Jouer Wyrm du pic venteux → Guide 1/1 | Secret adverse inconnu : jouer une carte peut le déclencher ; Traqueur phasique : déclencheur non simulé ; Secret adverse inconnu : une attaque peut le déclencher | survit au tour adverse ; partie gagnée |
| 213529-1502170609 | RANKED | 8.1 | Jouer Grommash Hurlenfer | modèle | oui | non évalué | non simulable | 16 → ? | 18 → ? | Pouvoir héroïque : +2 armure | Grommash Hurlenfer : effet non simulé | victoire ce tour ; partie gagnée |
| 214523-391479413 | RANKED | 5.1 | Attaquer avec Forgeron dimensionnel → Seigneur de la toile nérub’ar | modèle | Attaquer avec Forgeron dimensionnel → Seigneur de la toile nérub’ar | neutre | partielle | 5 → 5 | 10 → 9 | Attaquer avec Forgeron dimensionnel → Bébé marsuul 2/2, puis Attaquer avec Forgeron dimensionnel → Bébé marsuul 1/1, puis Pouvoir héroïque : +2 armure | Seigneur de la toile nérub’ar : aura non simulée après une mort | survit au tour adverse ; partie gagnée |
| 214523-391479413 | RANKED | 6.1 | Jouer Wyrm du pic venteux → Orweyna | modèle | oui | défend | complète | 0 → 5 | 8 → 8 | Attaquer avec Forgeron dimensionnel → Bébé marsuul 1/1, puis Jouer Wyrm du pic venteux → Matelot des mers du Sud 2/1 |  | victoire au tour suivant ; partie gagnée |
| 214523-391479413 | RANKED | 6.2 | Activer Profondeurs Sanguines → Matelot des mers du Sud | plan | oui | défend | complète | 5 → 7 | 8 → 8 | Attaquer avec Forgeron dimensionnel → Bébé marsuul 1/1, puis Attaquer avec Glissedrake visionnaire → Matelot des mers du Sud 2/1 |  | victoire au tour suivant ; partie gagnée |
| 215103-62838473 | RANKED | 8.1 | Jouer Le Grand Dracorex | modèle | oui | non évalué | non simulable | 12 → ? | 17 → ? | Attaquer avec Luisaile → Choriste de karaoké 2/4, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Choriste de karaoké 2/1, puis Jouer Profondeurs Sanguines, puis Activer Profondeurs Sanguines → Feu follet 1/1 | Le Grand Dracorex : effet non simulé | survit au tour adverse ; partie gagnée |
| 222102-1857162627 | RANKED | 6.6 | Jouer Dragonnet du Vol infini | plan | oui | non évalué | non simulable | 19 → ? | 21 → ? | Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 222823-527181711 | RANKED | 6.2 | Activer Profondeurs Sanguines → Glissedrake visionnaire (allié) | modèle | oui | non évalué | non simulable | 23 → ? | 23 → ? |  | Activer Profondeurs Sanguines → Glissedrake visionnaire (allié) : coup non simulé | victoire au tour suivant ; partie gagnée |
| 223523-401721727 | RANKED | 6.2 | Jouer Dragonnet du Vol infini | plan | oui | non évalué | non simulable | 26 → ? | 31 → ? | Attaquer avec Avant-garde du portail → Scarabée nuisible 1/5, puis Attaquer avec Plaiedécaille cobalt → Scarabée nuisible 1/5, puis Attaquer avec Voyante distordue par le temps → Scarabée nuisible 1/5, puis Attaquer avec Sombre chevaucheur → Scarabée nuisible 1/3, puis Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 224728-608097497 | RANKED | 6.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 28 → ? | 30 → ? | Pouvoir héroïque : +2 armure | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | survit au tour adverse ; partie perdue |
| 224728-608097497 | RANKED | 7.1 | Jouer Nozdormu l’Intemporel | modèle | oui | non évalué | non simulable | 9 → ? | 11 → ? | Pouvoir héroïque : +2 armure | Nozdormu l’Intemporel : l'adversaire en profite aussi (texte), danger non chiffré | mort au tour adverse suivant ; partie perdue |
| 225447-852149691 | RANKED | 9.3 | Jouer Canonnier défias | plan | oui | non évalué | non simulable | 35 → ? | 35 → ? |  | Canonnier défias : effet non simulé | victoire au tour suivant ; partie gagnée |
| 225447-852149691 | RANKED | 10.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 32 → ? | 34 → ? | Pouvoir héroïque : +2 armure | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire ce tour ; partie gagnée |
| 230748-1495328103 | RANKED | 5.2 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 13 → ? | 18 → ? | Attaquer avec Forgeron dimensionnel → Régis, le régisseur 3/1, puis Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 6.1 | Jouer Chronoseigneur Époque | modèle | oui | défend | partielle | 2 → 11 | 14 → 14 | Attaquer avec Dragonnet du Vol infini → Recrue de la Main d’argent 3/2, puis Jouer Chronoseigneur Époque (détruit les serviteurs joués au tour précédent) | Recrue de la Main d’argent meurt : Râle d'agonie non simulé | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 8.1 | Jouer Suffusion d’ombreflamme → Recrue de la Main d’argent | modèle | oui | neutre | partielle | 4 → 4 | 13 → 13 | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 2/1, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Gardien des couvées → Recrue de la Main d’argent 5/4 | Recrue de la Main d’argent meurt : Râle d'agonie non simulé | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 8.2 | Jouer Saccageur de chapiteaux | modèle | oui | non évalué | non simulable | 4 → ? | 13 → ? | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 5/2, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Gardien des couvées → Recrue de la Main d’argent 2/1 | Saccageur de chapiteaux : effet non simulé | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 8.3 | Attaquer avec Saccageur de chapiteaux → Recrue de la Main d’argent | plan | oui | défend | partielle | 4 → 6 | 13 → 13 | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 5/2, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Gardien des couvées → Recrue de la Main d’argent 2/1 | Recrue de la Main d’argent meurt : Râle d'agonie non simulé | survit au tour adverse ; partie abandonnée |
| 230748-1495328103 | RANKED | 9.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | -6 → ? | 0 → ? | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 4/3, puis Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Recrue de la Main d’argent 4/1 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | ? ; partie abandonnée |
| 230748-1495328103 | RANKED | 9.3 | Jouer Profondeurs Sanguines | modèle | oui | neutre | complète | -4 → -4 | 2 → 2 | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 4/3, puis Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Recrue de la Main d’argent 4/1 |  | ? ; partie abandonnée |
| 230748-1495328103 | RANKED | 9.4 | Activer Profondeurs Sanguines → Avant-garde du portail (allié) | modèle | ? | neutre | complète | -4 → -4 | 2 → 2 | Attaquer avec Avant-garde du portail → Recrue de la Main d’argent 4/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Recrue de la Main d’argent 4/1 |  | ? ; partie abandonnée |
| 231754-1040532073 | RANKED | 5.2 | Jouer Émeutier | modèle | oui | non évalué | non simulable | 17 → ? | 24 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Glissedrake visionnaire → Brute de C’Thrax 5/5 | Émeutier : effet non simulé | survit au tour adverse ; partie gagnée |
| 231754-1040532073 | RANKED | 9.1 | Jouer Le Grand Dracorex | modèle | oui | non évalué | non simulable | 27 → ? | 31 → ? | Attaquer avec Wyrm du pic venteux → Marcheur du Vide 1/3, puis Pouvoir héroïque : +2 armure, puis Jouer Frappe anticipée → Marcheur du Vide 1/3 | Le Grand Dracorex : effet non simulé | victoire ce tour ; partie gagnée |
| 232750-128742317 | RANKED | 4.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 22 → ? | 22 → ? |  | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | survit au tour adverse ; partie gagnée |
| 232750-128742317 | RANKED | 6.3 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 14 → ? | 18 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Forgeron dimensionnel → Sbire du gang des diablotins 2/2 | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 232750-128742317 | RANKED | 7.1 | Jouer Fracasseur de confluence | modèle | oui | non évalué | non simulable | 12 → ? | 18 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Dragonnet du Vol infini → Shivarra infiltrée 4/3 | Fracasseur de confluence : effet non simulé | victoire ce tour ; partie gagnée |
| 233545-1018234489 | RANKED | 4.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 22 → ? | 28 → ? | Attaquer avec Forgeron dimensionnel → Mobilisateur de renforts 2/2, puis Attaquer avec Gardien des couvées → Générale des forestiers Sylvanas 2/4, puis Pouvoir héroïque : +2 armure, puis Jouer Frappe anticipée → Générale des forestiers Sylvanas 2/2 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | survit au tour adverse ; partie gagnée |
| 233545-1018234489 | RANKED | 5.2 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 16 → ? | 22 → ? | Attaquer avec Forgeron dimensionnel → Braise crépitante 2/1, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Animatrice de stade → Recrue des forestiers Vereesa 2/4 | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 234130-1031141439 | RANKED | 4.1 | Jouer Dragonnet du Vol de bronze | modèle | oui | non évalué | non simulable | 26 → ? | 28 → ? | Pouvoir héroïque : +2 armure | Dragonnet du Vol de bronze : effet non simulé | survit au tour adverse ; partie gagnée |
| 234930-1865353269 | RANKED | 7.1 | Jouer Présage de la fin | modèle | oui | non évalué | non simulable | 22 → ? | 26 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Glissedrake visionnaire → Amasseur de butin 2/1 | Présage de la fin : effet non simulé | victoire au tour suivant ; partie gagnée |
| 235725-415381193 | RANKED | 9.1 | Jouer Grommash Hurlenfer | modèle | oui | non évalué | non simulable | 18 → ? | 23 → ? | Attaquer avec Forgeron dimensionnel → Baron Chute-des-Froids 2/2, puis Attaquer avec Glissedrake visionnaire → Ramasse-cadavres 1/3, puis Pouvoir héroïque : +2 armure | Grommash Hurlenfer : effet non simulé | survit au tour adverse ; partie gagnée |
| 235725-415381193 | RANKED | 10.2 | Jouer Le Grand Dracorex | modèle | oui | non évalué | non simulable | 18 → ? | 22 → ? | Attaquer avec Grommash Hurlenfer → Garde-essaim nérubien 1/3, puis Attaquer avec Glissedrake visionnaire → Garde-essaim nérubien 1/3, puis Pouvoir héroïque : +2 armure | Le Grand Dracorex : effet non simulé | victoire au tour suivant ; partie gagnée |
| 000900-472012207 | RANKED | 3.1 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 24 → ? | 28 → ? | Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Champion cryogénisé 2/1 | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 6.2 | Jouer Guerrière du Vol infini | modèle | oui | non évalué | non simulable | 16 → ? | 21 → ? | Attaquer avec Forgeron dimensionnel → Thassarian 3/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Thassarian 3/1 | Guerrière du Vol infini : effet non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 7.1 | Jouer Ysondre | modèle | oui | non évalué | non simulable | 13 → ? | 25 → ? | Jouer La pièce, puis Jouer Wyrm du pic venteux → Thassarian 3/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Fossoyeuse nécrotique 2/1 | Ysondre : effet non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 9.1 | Jouer Avant-garde du portail | modèle | oui | neutre | partielle | 2 → 2 | 9 → 9 | Attaquer avec Wyrm du pic venteux → Fantassin ressuscité 1/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Tour de goules : déclencheur non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 9.2 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 2 → ? | 9 → ? | Attaquer avec Wyrm du pic venteux → Fantassin ressuscité 1/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 9.3 | Jouer Avant-garde du portail | modèle | oui | neutre | partielle | 2 → 2 | 9 → 7 | Attaquer avec Wyrm du pic venteux → Fantassin ressuscité 1/3, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Tour de goules : déclencheur non simulé | survit au tour adverse ; partie abandonnée |
| 000900-472012207 | RANKED | 10.1 | Jouer Mage de sang Thalnos | modèle | oui | non évalué | non simulable | -25 → ? | -8 → ? | Attaquer avec Avant-garde du portail → Fantassin ressuscité 1/3, puis Attaquer avec Dragonnet du Vol infini → Commandante de la garde d’os 10/10, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Mage de sang Thalnos : effet non simulé | ? ; partie abandonnée |
| 000900-472012207 | RANKED | 10.2 | Activer Profondeurs Sanguines → Mage de sang Thalnos (allié) | plan | oui | aucune défense simulée | partielle | -25 → -25 | -8 → -8 | Attaquer avec Avant-garde du portail → Fantassin ressuscité 1/3, puis Attaquer avec Dragonnet du Vol infini → Commandante de la garde d’os 10/10, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Mage de sang Thalnos meurt : Râle d'agonie non simulé ; Dragonnet du Vol infini meurt : Réincarnation non simulé ; Tour de goules : déclencheur non simulé ; Grommash Hurlenfer : effet non simulé qui pourrait défendre | ? ; partie abandonnée |
| 000900-472012207 | RANKED | 10.3 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | -25 → ? | -8 → ? | Attaquer avec Avant-garde du portail → Fantassin ressuscité 1/3, puis Attaquer avec Dragonnet du Vol infini → Commandante de la garde d’os 10/10, puis Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tour de goules 4/1 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | ? ; partie abandonnée |
| 003106-1777587045 | RANKED | 9.1 | Jouer Le Grand Dracorex | modèle | oui | non évalué | non simulable | 9 → ? | 17 → ? | Attaquer avec Glissedrake visionnaire → Elekk migrateur 6/7, puis Pouvoir héroïque : +2 armure | Le Grand Dracorex : effet non simulé | survit au tour adverse ; partie perdue |
| 003106-1777587045 | RANKED | 10.1 | Jouer Champion invaincu | modèle | oui | non évalué | non simulable | 14 → ? | 35 → ? | Attaquer avec Glissedrake visionnaire → Ours des montagnes 9/10, puis Attaquer avec Le Grand Dracorex → Ours des montagnes 9/3, puis Pouvoir héroïque : +2 armure, puis Jouer Wyrm du pic venteux → Nourrice du nid de raptors 5/5 | Champion invaincu : effet non simulé | survit au tour adverse ; partie perdue |
| 003106-1777587045 | RANKED | 13.4 | Jouer Dragon féerique | plan | oui | non évalué | non simulable | 27 → ? | 27 → ? |  | Dragon féerique : effet non simulé | survit au tour adverse ; partie perdue |
| 003106-1777587045 | RANKED | 14.1 | Jouer Siamat | modèle | oui | non évalué | non simulable | 17 → ? | 25 → ? | Attaquer avec Luisaile → Elekk migrateur 2/3, puis Attaquer avec Dragon féerique → Niri du cratère 2/5, puis Attaquer avec Glissedrake visionnaire → Nourrice du nid de raptors 2/2, puis Pouvoir héroïque : +2 armure | Siamat : effet non simulé | survit au tour adverse ; partie perdue |
| 004607-1049641679 | RANKED | 6.1 | Jouer Lancier écailleux | modèle | oui | non évalué | non simulable | 27 → ? | 27 → ? |  | Lancier écailleux : effet non simulé | survit au tour adverse ; partie gagnée |
| 004607-1049641679 | RANKED | 7.5 | Jouer Dragonnet du Vol infini | plan | oui | non évalué | non simulable | 27 → ? | 27 → ? |  | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 005441-2135902901 | RANKED | 5.3 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 29 → ? | 29 → ? |  | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 010156-107298239 | RANKED | 4.1 | Jouer Salamandre dimensionnelle | modèle | oui | non évalué | non simulable | 27 → ? | 31 → ? | Attaquer avec Forgeron dimensionnel → Sabrerune mystique 2/3, puis Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Sabrerune mystique 2/1 | Salamandre dimensionnelle : effet non simulé | survit au tour adverse ; partie gagnée |
| 010156-107298239 | RANKED | 5.2 | Jouer Verdaile illusoire | plan | oui | non évalué | non simulable | 29 → ? | 29 → ? |  | Verdaile illusoire : effet non simulé | survit au tour adverse ; partie gagnée |
| 010156-107298239 | RANKED | 6.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 28 → ? | 28 → ? |  | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | ? ; partie gagnée |
| 010743-1093615795 | RANKED | 5.1 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 21 → ? | 32 → ? | Attaquer avec Colporteur de pétales → Rescapée de la fin des temps 9/11, puis Attaquer avec Avant-garde du portail → Rescapée de la fin des temps 9/8, puis Pouvoir héroïque : +2 armure, puis Attaquer avec Forgeron dimensionnel → Rescapée de la fin des temps 9/4 | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 010743-1093615795 | RANKED | 6.1 | Attaquer avec Dragonnet du Vol infini → Rescapée de la fin des temps | modèle | oui | défend | partielle | 5 → 14 | 25 → 25 | Attaquer avec Dragonnet du Vol infini → Rescapée de la fin des temps 9/11, puis Attaquer avec Colporteur de pétales → Canonnière 1/1, puis Attaquer avec Colporteur de pétales → Canonnière 1/1, puis Jouer Wyrm du pic venteux → Capitaine Crowley 4/5 | Capitaine Crowley : aura non simulée après une mort ; Dragonnet du Vol infini meurt : Réincarnation non simulé ; Capitaine Crowley meurt : Réincarnation non simulé | victoire au tour suivant ; partie gagnée |
| 011533-1390276953 | RANKED | 4.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 23 → ? | 29 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Forgeron dimensionnel → Messager raptor 4/2 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | survit au tour adverse ; partie gagnée |
| 011533-1390276953 | RANKED | 7.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 18 → ? | 23 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Chronoseigneur Époque → Grizzly du bois Maudit 3/7 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire ce tour ; partie gagnée |
| 012511-2016028875 | RANKED | 3.1 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 30 → ? | 30 → ? |  | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 012511-2016028875 | RANKED | 5.1 | Jouer Héritière de l’au-delà | modèle | oui | non évalué | non simulable | 30 → ? | 30 → ? |  | Héritière de l’au-delà : effet non simulé | victoire au tour suivant ; partie gagnée |
| 014601-2065976969 | RANKED | 8.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 29 → ? | 29 → ? |  | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire au tour suivant ; partie gagnée |
| 014601-2065976969 | RANKED | 8.4 | Jouer Dragonnet du Vol infini | plan | oui | non évalué | non simulable | 27 → ? | 27 → ? |  | Dragonnet du Vol infini : effet non simulé | victoire au tour suivant ; partie gagnée |
| 020236-1947967227 | RANKED | 8.1 | Jouer Frappe anticipée → Aile du Vide | modèle | oui | neutre | partielle | 0 → 0 | 24 → 24 | Attaquer avec Glissedrake visionnaire → Aile du Vide 9/11, puis Jouer Frappe anticipée → Aile du Vide 9/2, puis Attaquer avec Chronoseigneur Époque → Aile du Vide 5/5, puis Attaquer avec Chronoseigneur Époque → Aile du Vide 5/5, puis Pouvoir héroïque : +2 armure | Aile du Vide meurt : Réincarnation non simulé | victoire ce tour ; partie gagnée |
| 020236-1947967227 | RANKED | 8.2 | Attaquer avec Glissedrake visionnaire → Aile du Vide | modèle | oui | défend | partielle | 0 → 9 | 32 → 32 | Attaquer avec Glissedrake visionnaire → Aile du Vide 9/8, puis Attaquer avec Chronoseigneur Époque → Aile du Vide 5/5, puis Attaquer avec Chronoseigneur Époque → Aile du Vide 5/5, puis Jouer Wyrm du pic venteux → Aile du Vide 5/5 | Aile du Vide meurt : Réincarnation non simulé | victoire ce tour ; partie gagnée |
| 021216-1089642197 | RANKED | 9.1 | Jouer Morchie | modèle | oui | non évalué | non simulable | 25 → ? | 31 → ? | Attaquer avec Luisaile → Tisse-rêves resplendissant 4/4, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Tisse-rêves resplendissant 4/1 | Morchie : effet non simulé | survit au tour adverse ; partie abandonnée |
| 021216-1089642197 | RANKED | 11.4 | Jouer Dragonnet du Vol infini | plan | oui | non évalué | non simulable | 28 → ? | 32 → ? | Attaquer avec Voyante distordue par le temps → Feu follet 1/1, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Feu follet 1/1 | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie abandonnée |
| 021216-1089642197 | RANKED | 13.1 | Activer Profondeurs Sanguines → Feu follet | modèle | Activer Profondeurs Sanguines → Feu follet | aucune défense simulée | complète | -4 → -3 | -1 → -1 | Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Feu follet 1/1 |  | abandon au tour adverse suivant ; partie abandonnée |
| 021216-1089642197 | RANKED | 13.2 | Pouvoir héroïque Gain d’armure ! | modèle | oui | aucune défense simulée | complète | -3 → -1 | -1 → -1 | Pouvoir héroïque : +2 armure |  | abandon au tour adverse suivant ; partie abandonnée |
| 021216-1089642197 | RANKED | 13.3 | Jouer Sombre chevaucheur | plan | oui | aucune défense simulée | complète | -1 → -1 | -1 → -1 |  |  | abandon au tour adverse suivant ; partie abandonnée |
| 023027-1380538213 | RANKED | 5.2 | Jouer Poing-crochet 3000 | modèle | oui | non évalué | non simulable | 25 → ? | 25 → ? |  | Poing-crochet 3000 : effet non simulé | survit au tour adverse ; partie gagnée |
| 103415-397244595 | RANKED | 4.2 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 25 → ? | 27 → ? | Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 103415-397244595 | RANKED | 5.2 | Jouer Verdaile illusoire | plan | oui | non évalué | non simulable | 15 → ? | 25 → ? | Attaquer avec Dragonnet du Vol infini → Gardienne bruissaile 6/4, puis Attaquer avec Sombre chevaucheur → Préposé au sablier 2/4, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Forgeron dimensionnel, puis Attaquer avec Forgeron dimensionnel → Préposé au sablier 2/4 | Verdaile illusoire : effet non simulé | survit au tour adverse ; partie gagnée |
| 103415-397244595 | RANKED | 6.2 | Activer Profondeurs Sanguines → Verdaile illusoire (allié) | plan | oui | non évalué | non simulable | 28 → ? | 28 → ? |  | Activer Profondeurs Sanguines → Verdaile illusoire (allié) : coup non simulé | victoire au tour suivant ; partie gagnée |
| 110603-1407577647 | RANKED | 3.1 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 27 → ? | 31 → ? | Jouer Profondeurs Sanguines, puis Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Gardien des couvées 2/1 | Dragonnet du Vol infini : effet non simulé | ? ; partie gagnée |
| 111058-1732160237 | RANKED | 9.1 | Jouer Animatrice de stade | modèle | oui | non évalué | non simulable | 21 → ? | 25 → ? | Pouvoir héroïque : +2 armure, puis Activer Profondeurs Sanguines → Mimiron 2/1 | Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré | victoire ce tour ; partie gagnée |
| 112706-1622115605 | RANKED | 6.3 | Jouer Cogneur d’œufs → Glissedrake visionnaire (allié) | modèle | oui | non évalué | non simulable | 21 → ? | 27 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Glissedrake visionnaire → Veilleur des couvées 4/5 | Cogneur d’œufs : effet non simulé | ? ; partie gagnée |
| 113340-2317295 | RANKED | 3.1 | Jouer Mère des couvées sombrécaille | modèle | oui | non évalué | non simulable | 30 → ? | 30 → ? |  | Mère des couvées sombrécaille : effet non simulé | survit au tour adverse ; partie gagnée |
| 113340-2317295 | RANKED | 3.2 | Jouer Informateur anonyme | modèle | oui | non évalué | non simulable | 30 → ? | 30 → ? |  | Informateur anonyme : effet non simulé | survit au tour adverse ; partie gagnée |
| 113340-2317295 | RANKED | 4.1 | Jouer Dragonnet du Vol infini | modèle | oui | non évalué | non simulable | 24 → ? | 29 → ? | Attaquer avec Informateur anonyme → Brute de C’Thrax 3/5, puis Pouvoir héroïque : +2 armure | Dragonnet du Vol infini : effet non simulé | survit au tour adverse ; partie gagnée |
| 113340-2317295 | RANKED | 5.1 | Jouer Libération des Bêtes | modèle | oui | non évalué | non simulable | 23 → ? | 28 → ? | Pouvoir héroïque : +2 armure, puis Attaquer avec Dragonnet du Vol infini → Brute de C’Thrax 3/5 | Libération des Bêtes : effet non simulé | survit au tour adverse ; partie gagnée |

## Défaites : verdicts des deux derniers tours

- **224728-608097497** (mort au tour de jeu 14) ; marge réelle en fin de tes deux derniers tours : 24, 11
  - tour 6.1 : joué « Jouer Animatrice de stade » (conseil suivi, modèle) → non évalué, non simulable, marge 28 → ?, meilleure défense 30 → ? ; non simulé : Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré
  - tour 6.2 : joué « Attaquer avec Garrosh Hurlenfer → Tyrande Murmevent » (conseil suivi, modèle) → danger visible faible, partielle, marge 24 → 24, meilleure défense 26 → 26 ; non simulé : Canon à eau : effet de l'arme non simulé
  - tour 6.3 : joué « Jouer Profondeurs Sanguines » (conseil suivi, modèle) → danger visible faible, complète, marge 24 → 24, meilleure défense 26 → 24
  - tour 6.4 : joué « Finir le tour » (conseil suivi, modèle) → danger visible faible, complète, marge 24 → 24, meilleure défense 24 → 24
  - tour 7.1 : joué « Jouer Nozdormu l’Intemporel » (conseil suivi, modèle) → non évalué, non simulable, marge 9 → ?, meilleure défense 11 → ? ; non simulé : Nozdormu l’Intemporel : l'adversaire en profite aussi (texte), danger non chiffré
  - tour 7.2 : joué « Pouvoir héroïque Gain d’armure ! » (conseil suivi, plan) → danger visible faible, complète, marge 9 → 11, meilleure défense 11 → 11
  - tour 7.3 : joué « Finir le tour » (conseil suivi, modèle) → danger visible faible, complète, marge 11 → 11, meilleure défense 11 → 11
- **230748-1495328103** (abandon au tour de jeu 17) ; marge réelle en fin de tes deux derniers tours : 13
  - tour 8.1 : joué « Jouer Suffusion d’ombreflamme → Recrue de la Main d’argent » (conseil suivi, modèle) → neutre, partielle, marge 4 → 4, meilleure défense 13 → 13 ; non simulé : Recrue de la Main d’argent meurt : Râle d'agonie non simulé
  - tour 8.2 : joué « Jouer Saccageur de chapiteaux » (conseil suivi, modèle) → non évalué, non simulable, marge 4 → ?, meilleure défense 13 → ? ; non simulé : Saccageur de chapiteaux : effet non simulé
  - tour 8.3 : joué « Attaquer avec Saccageur de chapiteaux → Recrue de la Main d’argent » (conseil suivi, plan) → défend, partielle, marge 4 → 6, meilleure défense 13 → 13 ; non simulé : Recrue de la Main d’argent meurt : Râle d'agonie non simulé
  - tour 8.4 : joué « Attaquer avec Gardien des couvées → Recrue de la Main d’argent » (conseil : Attaquer avec Avant-garde du portail → Recrue de la Main d’argent) → danger visible faible, partielle, marge 6 → 11, meilleure défense 13 → 11 ; non simulé : Recrue de la Main d’argent meurt : Râle d'agonie non simulé
  - tour 8.5 : joué « Attaquer avec Avant-garde du portail → Uther le Porteur de Lumière » (sans conseil) → danger visible faible, complète, marge 11 → 11, meilleure défense 11 → 11
  - tour 8.6 : joué « Attaquer avec Garrosh Hurlenfer → Uther le Porteur de Lumière » (sans conseil) → danger visible faible, complète, marge 11 → 11, meilleure défense 11 → 11
  - tour 8.7 : joué « Pouvoir héroïque Gain d’armure ! » (sans conseil) → danger visible faible, complète, marge 11 → 13, meilleure défense 11 → 13
  - tour 9.1 : joué « Jouer Animatrice de stade » (conseil suivi, modèle) → non évalué, non simulable, marge -6 → ?, meilleure défense 0 → ? ; non simulé : Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré
  - tour 9.2 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
  - tour 9.3 : joué « Jouer Profondeurs Sanguines » (conseil suivi, modèle) → neutre, complète, marge -4 → -4, meilleure défense 2 → 2
  - tour 9.4 : joué « ? » (conseil : Activer Profondeurs Sanguines → Avant-garde du portail (allié)) → ?, , marge  → ?, meilleure défense  → ?
- **000900-472012207** (abandon au tour de jeu 20) ; marge réelle en fin de tes deux derniers tours : 2
  - tour 9.1 : joué « Jouer Avant-garde du portail » (conseil suivi, modèle) → neutre, partielle, marge 2 → 2, meilleure défense 9 → 9 ; non simulé : Tour de goules : déclencheur non simulé
  - tour 9.2 : joué « Jouer Dragonnet du Vol infini » (conseil suivi, modèle) → non évalué, non simulable, marge 2 → ?, meilleure défense 9 → ? ; non simulé : Dragonnet du Vol infini : effet non simulé
  - tour 9.3 : joué « Jouer Avant-garde du portail » (conseil suivi, modèle) → neutre, partielle, marge 2 → 2, meilleure défense 9 → 7 ; non simulé : Tour de goules : déclencheur non simulé
  - tour 9.4 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
  - tour 9.5 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
  - tour 10.1 : joué « Jouer Mage de sang Thalnos » (conseil suivi, modèle) → non évalué, non simulable, marge -25 → ?, meilleure défense -8 → ? ; non simulé : Mage de sang Thalnos : effet non simulé
  - tour 10.2 : joué « Activer Profondeurs Sanguines → Mage de sang Thalnos (allié) » (conseil suivi, plan) → aucune défense simulée, partielle, marge -25 → -25, meilleure défense -8 → -8 ; non simulé : Mage de sang Thalnos meurt : Râle d'agonie non simulé ; Dragonnet du Vol infini meurt : Réincarnation non simulé ; Tour de goules : déclencheur non simulé
  - tour 10.3 : joué « Jouer Animatrice de stade » (conseil suivi, modèle) → non évalué, non simulable, marge -25 → ?, meilleure défense -8 → ? ; non simulé : Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré
  - tour 10.4 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
  - tour 10.5 : joué « Attaquer avec Dragonnet du Vol infini → Commandante de la garde d’os » (sans conseil) → aucune défense simulée, partielle, marge -27 → -17, meilleure défense -10 → -10 ; non simulé : Dragonnet du Vol infini meurt : Réincarnation non simulé ; Tour de goules : déclencheur non simulé
  - tour 10.6 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
- **003106-1777587045** (mort au tour de jeu 32) ; marge réelle en fin de tes deux derniers tours : 16, 0
  - tour 15.1 : joué « Attaquer avec Siamat → Ours des montagnes » (sans conseil) → danger visible faible, partielle, marge 11 → 16, meilleure défense 25 → 25 ; non simulé : Ours des montagnes meurt : Râle d'agonie non simulé ; Prédateur en chasse : déclencheur non simulé
  - tour 15.2 : joué « Attaquer avec Dragon féerique → Techysaurus » (sans conseil) → danger visible faible, complète, marge 12 → 15, meilleure défense 21 → 21
  - tour 15.3 : joué « Attaquer avec Glissedrake visionnaire → Ourson des montagnes » (sans conseil) → danger visible faible, complète, marge 15 → 17, meilleure défense 21 → 21
  - tour 15.4 : joué « Attaquer avec Luisaile → Ourson des montagnes » (sans conseil) → danger visible faible, complète, marge 17 → 17, meilleure défense 21 → 21
  - tour 15.5 : joué « Attaquer avec Garrosh Hurlenfer → Ourson des montagnes » (sans conseil) → danger visible faible, complète, marge 17 → 17, meilleure défense 21 → 19
  - tour 15.6 : joué « Attaquer avec Gardien des couvées → Rexxar » (sans conseil) → danger visible faible, complète, marge 17 → 17, meilleure défense 19 → 19
  - tour 15.7 : joué « Attaquer avec Colporteur de pétales → Rexxar » (sans conseil) → danger visible faible, complète, marge 17 → 17, meilleure défense 19 → 19
  - tour 15.8 : joué « Jouer Dragonnet du Vol infini » (sans conseil) → non évalué, non simulable, marge 17 → ?, meilleure défense 19 → ? ; non simulé : Dragonnet du Vol infini : effet non simulé
  - tour 15.9 : joué « Jouer Animatrice de stade » (sans conseil) → non évalué, non simulable, marge 17 → ?, meilleure défense 19 → ? ; non simulé : Animatrice de stade : l'adversaire en profite aussi (texte), danger non chiffré
  - tour 15.10 : joué « ? » (sans conseil) → ?, , marge  → ?, meilleure défense  → ?
  - tour 15.11 : joué « Pouvoir héroïque Gain d’armure ! » (sans conseil) → danger visible faible, complète, marge 14 → 16, meilleure défense 16 → 16
  - tour 16.1 : joué « Jouer Forgeron dimensionnel » (sans conseil) → neutre, partielle, marge -2 → -2, meilleure défense 9 → 9 ; non simulé : Secret adverse inconnu : jouer une carte peut le déclencher ; Secret adverse inconnu : une attaque peut le déclencher
  - tour 16.2 : joué « Attaquer avec Gardien des couvées → Parle-esprit » (sans conseil) → défend, partielle, marge -2 → 0, meilleure défense 9 → 9 ; non simulé : Secret adverse inconnu : une attaque peut le déclencher
  - tour 16.3 : joué « Attaquer avec Glissedrake visionnaire → Rexxar » (sans conseil) → aggrave nettement, partielle, marge 0 → 0, meilleure défense 9 → 2 ; non simulé : Secret adverse inconnu : une attaque peut le déclencher
  - tour 16.4 : joué « Attaquer avec Garrosh Hurlenfer → Rexxar » (sans conseil) → aucune défense simulée, partielle, marge -2 → -2, meilleure défense 0 → 0 ; non simulé : Lame runique horrible : effet de l'arme non simulé
  - tour 16.5 : joué « Pouvoir héroïque Gain d’armure ! » (sans conseil) → aucune défense simulée, complète, marge -2 → 0, meilleure défense 0 → 0
- **021216-1089642197** (abandon au tour de jeu 26) ; marge réelle en fin de tes deux derniers tours : 27, -1
  - tour 12.1 : joué « Attaquer avec Glissedrake visionnaire → Jaina Portvaillant » (conseil suivi, modèle) → danger visible faible, complète, marge 25 → 25, meilleure défense 30 → 29
  - tour 12.2 : joué « Attaquer avec Dragonnet du Vol infini → Jaina Portvaillant » (conseil suivi, plan) → danger visible faible, complète, marge 25 → 25, meilleure défense 29 → 28
  - tour 12.3 : joué « Jouer Voyante distordue par le temps » (conseil suivi, plan) → danger visible faible, complète, marge 25 → 25, meilleure défense 28 → 28
  - tour 12.4 : joué « Pouvoir héroïque Gain d’armure ! » (conseil suivi, plan) → danger visible faible, complète, marge 25 → 27, meilleure défense 28 → 28
  - tour 12.5 : joué « Finir le tour » (conseil suivi, modèle) → danger visible faible, complète, marge 27 → 27, meilleure défense 28 → 27
  - tour 13.1 : joué « Activer Profondeurs Sanguines → Feu follet » (conseil : Activer Profondeurs Sanguines → Feu follet) → aucune défense simulée, complète, marge -4 → -3, meilleure défense -1 → -1
  - tour 13.2 : joué « Pouvoir héroïque Gain d’armure ! » (conseil suivi, modèle) → aucune défense simulée, complète, marge -3 → -1, meilleure défense -1 → -1
  - tour 13.3 : joué « Jouer Sombre chevaucheur » (conseil suivi, plan) → aucune défense simulée, complète, marge -1 → -1, meilleure défense -1 → -1

## Attaques au visage jouées avec une marge de 5 ou moins (hors létal certain) : 2

- 003106-1777587045 tour 16.3 (RANKED) : « Attaquer avec Glissedrake visionnaire → Rexxar » (sans conseil) ; marge 0 → 0, meilleure défense 9 → 2 ; aggrave nettement, partielle ; mort au tour adverse suivant ; partie perdue
- 003106-1777587045 tour 16.4 (RANKED) : « Attaquer avec Garrosh Hurlenfer → Rexxar » (sans conseil) ; marge -2 → -2, meilleure défense 0 → 0 ; aucune défense simulée, partielle ; mort au tour adverse suivant ; partie perdue

## Coût du calcul

Par conseil : médiane 0,03 ms, P95 3,77 ms, maximum 410,2 ms ; recherche tronquée : 0 ; total de l'analyse : 3665 ms.

## Positions figées : cases-mvp3a-sauvegarde.jsonl (571 coups joués, 1853 ms)

- GT_RANKED : 24 coups ; danger visible faible 19 (complète 18) ; non évalué 5 (complète 0)
- GT_VS_AI : 547 coups ; danger visible faible 319 (complète 195) ; létal ce tour 105 (complète 41) ; non évalué 93 (complète 0) ; défend 12 (complète 3) ; neutre 12 (complète 5) ; aucune défense simulée 6 (complète 5)

