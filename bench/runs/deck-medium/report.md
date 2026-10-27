# Banc d'essai du conseiller : synthèse

Généré le 2026-10-25 13:37 UTC. 30 cas. Coût total mesuré : 0.582 $.
Cas : empreinte `472128292b87`. Instructions : `89db2eda2c5a`. Stratégie : `2076d066f36e`. Historique transmis : 25 actions.

Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;
ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.

## Résultats par variante

| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |
|---|---|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 30/30 | 100% (30/30) | 40% (12/30) | 43% (13/30) | 70% (21/30) | 60% (6/10) | 0% (0/30) | 0% (0/30) | 100% (8/8) |
| **ton jeu (référence)** | 30 | n/a | n/a | n/a | n/a | n/a | 10% (3/30) | n/a | 13% (1/8) |

| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |
|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 11.4 s | 21.6 s | 22.7 s | 93% (28/30) | 0.019 $ |

| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |
|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 8725 | 1639 | 508 | 152 | 0.582 $ | élevée 23, moyenne 7 | aucune |

## Signalements (coups légaux mais discutables)

### gpt-6.1-sol:medium
FRAPPE_SANS_PIOCHE : 1

- `2026_10_24_20_59_43/Power_old.log#9@3154` (tour 3, info) **FRAPPE_SANS_PIOCHE** : Frappe anticipée jouée sans serviteur coûtant 5 ou plus en main : pas de pioche. Conseil donné : « Joue Frappe anticipée sur l’Éleveur sélectif pour conserver Dragonnet du Vol infini et préparer Glissedrake visionnaire. »

### Ton jeu (même détecteur)
LETAL_IGNORE : 5, LETAL_AVEC_MAIN_IGNORE : 2, ECHANGE_PERDANT : 2

- `2026_10_24_20_59_43/Power_old.log#10@17630` (tour 9, à vérifier) **LETAL_AVEC_MAIN_IGNORE** : Létal apparent avec « Jouer Grommash Hurlenfer » (4) et les attaques autorisées (15) contre 17 PV + 0 armure, mais le coup choisi est « Pouvoir héroïque Gain d’armure ! ».
- `2026_10_24_20_59_43/Power_old.log#18@17538` (tour 9, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (15 dégâts contre 1 PV + 0 armure) mais le coup choisi est « Jouer Chronoseigneur Époque ». Secrets adverses éventuels non pris en compte.
- `2026_10_24_20_59_43/Power_old.log#22@36852` (tour 17, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (32 dégâts contre 30 PV + 2 armure) mais le coup choisi est « Jouer Pointe d’os ». Secrets adverses éventuels non pris en compte.
- `2026_10_24_20_59_43/Power_old.log#8@13795` (tour 9, à vérifier) **LETAL_AVEC_MAIN_IGNORE** : Létal apparent avec « Jouer Frappe anticipée → Malfurion Hurlorage » (3) et les attaques autorisées (0) contre 3 PV + 0 armure, mais le coup choisi est « Jouer Chronoseigneur Époque ».
- `2026_10_24_20_59_43/Power_old.log#8@32608` (tour 18, douteux) **ECHANGE_PERDANT** : Luisaile (3/2) meurt contre Druide de la Griffe (4/4) sans le tuer.
- `2026_10_25_01_34_44/Power_old.log#3@21689` (tour 11, douteux) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (28 dégâts contre 1 PV + 0 armure) mais le coup choisi est « Attaquer avec Avant-garde du portail → Hyène charognarde ». Secrets adverses éventuels non pris en compte.
- `2026_10_25_01_34_44/Power_old.log#3@35672` (tour 17, douteux) **ECHANGE_PERDANT** : Gardien des couvées (2/3) meurt contre Faucon-dragon difforme (5/5) sans le tuer.

## Répartition des situations

début-de-tour : 19, rembobinage-jouable : 11, milieu-de-tour : 11, profondeurs-prêtes : 8, létal-attaques : 6, wyrm-jouable : 5, grommash-jouable : 3, chronoseigneur-jouable : 3, létal-avec-main : 2, menace-létale-adverse : 2
