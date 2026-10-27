# Banc d'essai du conseiller : synthèse

Généré le 2026-10-25 13:42 UTC. 36 cas. Coût total mesuré : 2.436 $.
Cas : empreinte `cc2c334b2950`. Instructions : `89db2eda2c5a`. Stratégie : `2076d066f36e`. Historique transmis : 25 actions.

Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;
ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.

## Résultats par variante

| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |
|---|---|---|---|---|---|---|---|---|---|
| gpt-6-luna:medium | 36/36 | 97% (35/36) | 43% (15/35) | 46% (16/35) | 77% (27/35) | 50% (5/10) | 0% (0/36) | 3% (1/36) | 78% (7/9) |
| gpt-6.1-sol:low | 36/36 | 100% (36/36) | 44% (16/36) | 50% (18/36) | 72% (26/36) | 60% (6/10) | 0% (0/36) | 0% (0/36) | 100% (9/9) |
| gpt-6.1-sol:medium | 36/36 | 100% (36/36) | 42% (15/36) | 44% (16/36) | 72% (26/36) | 60% (6/10) | 0% (0/36) | 0% (0/36) | 100% (9/9) |
| gpt-6.1-sol:medium#2 | 36/36 | 100% (36/36) | 42% (15/36) | 44% (16/36) | 72% (26/36) | 60% (6/10) | 0% (0/36) | 3% (1/36) | 100% (9/9) |
| gpt-6.1-sol:high | 36/36 | 100% (36/36) | 44% (16/36) | 47% (17/36) | 72% (26/36) | 70% (7/10) | 0% (0/36) | 0% (0/36) | 100% (9/9) |
| **ton jeu (référence)** | 36 | n/a | n/a | n/a | n/a | n/a | 8% (3/36) | n/a | 11% (1/9) |

| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |
|---|---|---|---|---|---|
| gpt-6-luna:medium | 12.2 s | 22.6 s | 23.1 s | 92% (33/36) | 0.0013 $ |
| gpt-6.1-sol:low | 9.8 s | 14.5 s | 15.8 s | 100% (36/36) | 0.017 $ |
| gpt-6.1-sol:medium | 11.9 s | 22.0 s | 23.0 s | 92% (33/36) | 0.017 $ |
| gpt-6.1-sol:medium#2 | 11.6 s | 20.1 s | 20.6 s | 94% (34/36) | 0.0062 $ |
| gpt-6.1-sol:high | 28.6 s | 65.6 s | 77.0 s | 36% (13/36) | 0.026 $ |

| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |
|---|---|---|---|---|---|---|---|
| gpt-6-luna:medium | 8253 | 1593 | 1175 | 864 | 0.046 $ | élevée 15, moyenne 21 | aucune |
| gpt-6.1-sol:low | 8253 | 1593 | 377 | 32 | 0.621 $ | élevée 31, moyenne 5 | aucune |
| gpt-6.1-sol:medium | 8253 | 2543 | 511 | 141 | 0.604 $ | élevée 31, moyenne 5 | aucune |
| gpt-6.1-sol:medium#2 | 8253 | 8136 | 514 | 145 | 0.223 $ | élevée 31, moyenne 5 | aucune |
| gpt-6.1-sol:high | 8253 | 1593 | 1270 | 904 | 0.943 $ | élevée 26, moyenne 10 | aucune |

## Accord entre variantes

| A | B | Cas communs | Même action | Même carte |
|---|---|---|---|---|
| gpt-6-luna:medium | gpt-6.1-sol:low | 35 | 46% (16/35) | 49% (17/35) |
| gpt-6-luna:medium | gpt-6.1-sol:medium | 35 | 51% (18/35) | 54% (19/35) |
| gpt-6-luna:medium | gpt-6.1-sol:medium#2 | 35 | 51% (18/35) | 54% (19/35) |
| gpt-6-luna:medium | gpt-6.1-sol:high | 35 | 54% (19/35) | 57% (20/35) |
| gpt-6.1-sol:low | gpt-6.1-sol:medium | 36 | 86% (31/36) | 86% (31/36) |
| gpt-6.1-sol:low | gpt-6.1-sol:medium#2 | 36 | 83% (30/36) | 86% (31/36) |
| gpt-6.1-sol:low | gpt-6.1-sol:high | 36 | 81% (29/36) | 81% (29/36) |
| gpt-6.1-sol:medium | gpt-6.1-sol:medium#2 | 36 | 97% (35/36) | 100% (36/36) |
| gpt-6.1-sol:medium | gpt-6.1-sol:high | 36 | 89% (32/36) | 89% (32/36) |
| gpt-6.1-sol:medium#2 | gpt-6.1-sol:high | 36 | 89% (32/36) | 89% (32/36) |

## Signalements (coups légaux mais discutables)

### gpt-6-luna:medium
LETAL_AVEC_MAIN_IGNORE : 1

- `2026_10_24_20_59_43/Power_old.log#8@13795` (tour 9, à vérifier) **LETAL_AVEC_MAIN_IGNORE** : Létal apparent avec « Jouer Frappe anticipée → Malfurion Hurlorage » (3) et les attaques autorisées (0) contre 3 PV + 0 armure, mais le coup choisi est « Jouer Frappe anticipée → Dragon assoupi ». Conseil donné : « Joue Frappe anticipée sur le Dragon assoupi pour ouvrir une séquence de létal. »

### gpt-6.1-sol:low
Aucun signalement.

### gpt-6.1-sol:medium
Aucun signalement.

### gpt-6.1-sol:medium#2
HEROS_ATTAQUE_SANS_TUER : 1

- `2026_10_25_10_27_21/Power_old.log#1@7801` (tour 6, à vérifier) **HEROS_ATTAQUE_SANS_TUER** : Le héros (2 ATQ) attaque Gardien des couvées (3 PV) sans le tuer et subit 2 dégâts. Conseil donné : « Attaque le Gardien des couvées adverse avec la Trancheuse cauchemardesque pour préparer son élimination. »

### gpt-6.1-sol:high
Aucun signalement.

### Ton jeu (même détecteur)
LETAL_IGNORE : 6, LETAL_AVEC_MAIN_IGNORE : 2, ECHANGE_PERDANT : 2

- `2026_10_22_14_55_57/Power.log#8@9340` (tour 7, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (6 dégâts contre 6 PV + 0 armure) mais le coup choisi est « Jouer Frappe héroïque ». Secrets adverses éventuels non pris en compte.
- `2026_10_24_20_59_43/Power_old.log#10@17630` (tour 9, à vérifier) **LETAL_AVEC_MAIN_IGNORE** : Létal apparent avec « Jouer Grommash Hurlenfer » (4) et les attaques autorisées (15) contre 17 PV + 0 armure, mais le coup choisi est « Pouvoir héroïque Gain d’armure ! ».
- `2026_10_24_20_59_43/Power_old.log#18@17538` (tour 9, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (15 dégâts contre 1 PV + 0 armure) mais le coup choisi est « Jouer Chronoseigneur Époque ». Secrets adverses éventuels non pris en compte.
- `2026_10_24_20_59_43/Power_old.log#22@36852` (tour 17, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (32 dégâts contre 30 PV + 2 armure) mais le coup choisi est « Jouer Pointe d’os ». Secrets adverses éventuels non pris en compte.
- `2026_10_24_20_59_43/Power_old.log#8@13795` (tour 9, à vérifier) **LETAL_AVEC_MAIN_IGNORE** : Létal apparent avec « Jouer Frappe anticipée → Malfurion Hurlorage » (3) et les attaques autorisées (0) contre 3 PV + 0 armure, mais le coup choisi est « Jouer Chronoseigneur Époque ».
- `2026_10_24_20_59_43/Power_old.log#8@32608` (tour 18, douteux) **ECHANGE_PERDANT** : Luisaile (3/2) meurt contre Druide de la Griffe (4/4) sans le tuer.
- `2026_10_25_01_34_44/Power_old.log#3@35672` (tour 17, douteux) **ECHANGE_PERDANT** : Gardien des couvées (2/3) meurt contre Faucon-dragon difforme (5/5) sans le tuer.

## Erreurs et réponses rejetées

- gpt-6-luna:medium (rejetée) : 2026_10_24_20_59_43/Power_old.log#10@17630 : o75.5.t66 (action « o75.5.t66 » absente des actions légales de la révision 136)

## Répartition des situations

début-de-tour : 24, milieu-de-tour : 12, rembobinage-jouable : 12, profondeurs-prêtes : 10, létal-attaques : 7, wyrm-jouable : 5, menace-létale-adverse : 3, grommash-jouable : 3, chronoseigneur-jouable : 3, létal-avec-main : 2
