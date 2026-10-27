# Banc d'essai du conseiller : synthèse

Généré le 2026-10-25 13:37 UTC. 8 cas. Coût total mesuré : 0.345 $.
Cas : empreinte `6c2476d5a285`. Instructions : `89db2eda2c5a`. Stratégie : `2076d066f36e`. Historique transmis : 25 actions.

Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;
ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.

## Résultats par variante

| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |
|---|---|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:xhigh | 8/8 | 100% (8/8) | 0% (0/8) | 0% (0/8) | 38% (3/8) | 0% (0/2) | 0% (0/8) | 0% (0/8) | 100% (2/2) |
| **ton jeu (référence)** | 8 | n/a | n/a | n/a | n/a | n/a | 13% (1/8) | n/a | 0% (0/2) |

| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |
|---|---|---|---|---|---|
| gpt-6.1-sol:xhigh | 58.4 s | 127.4 s | 127.4 s | 0% (0/8) | 0.043 $ |

| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |
|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:xhigh | 8240 | 1434 | 2936 | 2544 | 0.345 $ | élevée 2, faible 1, moyenne 5 | aucune |

## Signalements (coups légaux mais discutables)

### gpt-6.1-sol:xhigh
Aucun signalement.

### Ton jeu (même détecteur)
LETAL_IGNORE : 2

- `2026_10_22_14_55_57/Power.log#8@9340` (tour 7, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (6 dégâts contre 6 PV + 0 armure) mais le coup choisi est « Jouer Frappe héroïque ». Secrets adverses éventuels non pris en compte.
- `2026_10_25_01_34_44/Power_old.log#3@21689` (tour 11, douteux) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (28 dégâts contre 1 PV + 0 armure) mais le coup choisi est « Attaquer avec Avant-garde du portail → Hyène charognarde ». Secrets adverses éventuels non pris en compte.

## Répartition des situations

début-de-tour : 7, profondeurs-prêtes : 4, rembobinage-jouable : 4, létal-attaques : 2, menace-létale-adverse : 2, grommash-jouable : 1, wyrm-jouable : 1, milieu-de-tour : 1
