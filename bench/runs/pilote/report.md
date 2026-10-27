# Banc d'essai du conseiller : synthèse

Généré le 2026-10-25 13:37 UTC. 3 cas. Coût total mesuré : 0.074 $.
Cas : empreinte `2eddddb22e4e`. Instructions : `89db2eda2c5a`. Stratégie : `2076d066f36e`. Historique transmis : 25 actions.

Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;
ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.

## Résultats par variante

| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |
|---|---|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 3/3 | 100% (3/3) | 0% (0/3) | 0% (0/3) | 33% (1/3) | 0% (0/1) | 0% (0/3) | 0% (0/3) | 100% (2/2) |
| **ton jeu (référence)** | 3 | n/a | n/a | n/a | n/a | n/a | 0% (0/3) | n/a | 50% (1/2) |

| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |
|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 9.7 s | 19.5 s | 19.5 s | 100% (3/3) | 0.025 $ |

| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |
|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium | 11123 | 1093 | 466 | 126 | 0.074 $ | élevée 3 | aucune |

## Signalements (coups légaux mais discutables)

### gpt-6.1-sol:medium
Aucun signalement.

### Ton jeu (même détecteur)
LETAL_IGNORE : 1

- `2026_10_25_01_34_44/Power_old.log#3@29975` (tour 15, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (36 dégâts contre 1 PV + 0 armure) mais le coup choisi est « Jouer Suffusion d’ombreflamme → Hyène ». Secrets adverses éventuels non pris en compte.

## Répartition des situations

milieu-de-tour : 3, létal-attaques : 2, profondeurs-prêtes : 1, wyrm-jouable : 1, rembobinage-jouable : 1
