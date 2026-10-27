# Banc d'essai du conseiller : synthèse

Généré le 2026-10-26 11:15 UTC. 25 cas. Coût total mesuré : 0.686 $.
Cas : empreinte `3153321989f9`. Instructions : `f313f7c17a59`. Stratégie : `589c3f3f50f9`. Historique transmis : 25 actions.

Lecture : l'accord avec ton coup mesure la ressemblance avec ton jeu, pas la qualité. Les signalements sont des heuristiques ;
ils sont aussi appliqués à tes propres coups pour donner un point de comparaison. Aucun choix n'est prouvé optimal.

## Résultats par variante

| Variante | Réponses | Légales | Accord exact | Même carte | Même type | Accord exact (classées) | Douteux | À vérifier | Létal pris |
|---|---|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium+local | 14/25 | 100% (14/14) | 85% (11/13) | 85% (11/13) | 92% (12/13) | 85% (11/13) | 7% (1/14) | 7% (1/14) | 75% (3/4) |
| gpt-6.1-sol:medium+local+danger | 16/25 | 100% (16/16) | 73% (11/15) | 80% (12/15) | 87% (13/15) | 73% (11/15) | 6% (1/16) | 13% (2/16) | 67% (2/3) |
| **ton jeu (référence)** | 24 | n/a | n/a | n/a | n/a | n/a | 0% (0/24) | n/a | 80% (4/5) |

| Variante | Latence P50 | P95 | max | ≤ 20 s | Coût moyen/décision |
|---|---|---|---|---|---|
| gpt-6.1-sol:medium+local | 14.4 s | 30.2 s | 30.2 s | 86% (12/14) | 0.012 $ |
| gpt-6.1-sol:medium+local+danger | 14.8 s | 45.4 s | 45.4 s | 69% (11/16) | 0.015 $ |

| Variante | Jetons entrée (moy.) | dont cache | Jetons sortie (moy.) | dont raisonnement | Coût total | Confiance déclarée | Annotations |
|---|---|---|---|---|---|---|---|
| gpt-6.1-sol:medium+local | 10820 | 2572 | 552 | 141 | 0.312 $ | élevée 9, faible 1, moyenne 4 | aucune |
| gpt-6.1-sol:medium+local+danger | 10994 | 2528 | 620 | 193 | 0.374 $ | élevée 11, moyenne 5 | aucune |

## Accord entre variantes

| A | B | Cas communs | Même action | Même carte |
|---|---|---|---|---|
| gpt-6.1-sol:medium+local | gpt-6.1-sol:medium+local+danger | 11 | 91% (10/11) | 91% (10/11) |

## Signalements (coups légaux mais discutables)

### gpt-6.1-sol:medium+local
LETAL_IGNORE : 1, ECHANGE_PERDANT : 1

- `sessions/20261025_213529_hdt.log#1@13819` (tour 8, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (20 dégâts contre 17 PV + 0 armure) mais le coup choisi est « Activer Profondeurs Sanguines → Grommash Hurlenfer (allié) ». Secrets adverses éventuels non pris en compte. Conseil donné : « Active Profondeurs Sanguines sur Grommash Hurlenfer, puis attaque Rexxar avec Grommash et le Wyrm. »
- `sessions/20261026_003106_hdt.log#1@32017` (tour 16, douteux) **ECHANGE_PERDANT** : Gardien des couvées (2/3) meurt contre Ursol (9/7) sans le tuer. Conseil donné : « Attaque Ursol avec le Gardien des couvées pour préparer son élimination par le Glissedrake visionnaire. »

### gpt-6.1-sol:medium+local+danger
LETAL_IGNORE : 1, HEROS_ATTAQUE_SANS_TUER : 1, ECHANGE_PERDANT : 1

- `sessions/20261025_213529_hdt.log#1@13819` (tour 8, à vérifier) **LETAL_IGNORE** : Létal possible par les attaques autorisées sur le héros (20 dégâts contre 17 PV + 0 armure) mais le coup choisi est « Activer Profondeurs Sanguines → Grommash Hurlenfer (allié) ». Secrets adverses éventuels non pris en compte. Conseil donné : « Active Profondeurs Sanguines sur Grommash Hurlenfer, puis dirige les deux attaques disponibles vers Rexxar. »
- `sessions/20261025_224728_hdt.log#1@15002` (tour 7, à vérifier) **HEROS_ATTAQUE_SANS_TUER** : Le héros (4 ATQ) attaque Devin (6 PV) sans le tuer et subit 6 dégâts. Conseil donné : « Attaque le Devin avec Garrosh Hurlenfer et le Canon à eau avant de remplacer l’arme. »
- `sessions/20261026_003106_hdt.log#1@32017` (tour 16, douteux) **ECHANGE_PERDANT** : Gardien des couvées (2/3) meurt contre Ursol (9/7) sans le tuer. Conseil donné : « Attaque Ursol avec Gardien des couvées pour préparer son élimination et tester le Secret avec le petit serviteur. »

### Ton jeu (même détecteur)
Aucun signalement.

## Erreurs et réponses rejetées

- gpt-6.1-sol:medium+local : sessions/20261025_230748_hdt.log#1@12570 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local : sessions/20261025_230748_hdt.log#1@15984 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local : sessions/20261026_000900_hdt.log#1@11609 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local : sessions/20261026_000900_hdt.log#1@15932 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local : sessions/20261026_003106_hdt.log#1@18871 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local+danger : sessions/20261025_230748_hdt.log#1@12570 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local+danger : sessions/20261025_230748_hdt.log#1@8606 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local+danger : sessions/20261026_000900_hdt.log#1@23506 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local+danger : sessions/20261026_003106_hdt.log#1@18871 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.
- gpt-6.1-sol:medium+local+danger : sessions/20261026_010743_hdt.log#1@11363 : HTTP 429 : You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.

## Répartition des situations

début-de-tour : 21, rembobinage-jouable : 14, wyrm-jouable : 7, profondeurs-prêtes : 7, menace-létale-adverse : 6, milieu-de-tour : 4, létal-attaques : 4, chronoseigneur-jouable : 4, grommash-jouable : 2, létal-avec-main : 1
