# Banc d'essai « danger adverse » du 26/10/2026

## Protocole (fixé avant les appels)

- **Positions** : 25, tirées par `selection.py` (graine 20261026) parmi 830 positions figées de 44 parties classées :
  - 12 positions de danger : 6 en danger élevé, 6 en létal adverse visible ;
  - 5 positions de létal, dont 2 sous danger ;
  - 2 positions avec Nozdormu l'Intemporel en main ;
  - 6 témoins.
- **Gel de l'information** : aucune carte cachée (contrôle anti-fuite : 0) ; les résultats des parties sont rangés à part dans `positions-retro.json`, jamais transmis aux variantes ni à l'évaluateur.
- **Variantes** : `gpt-6.1-sol` en effort medium, sans flux, avec l'analyse locale du conseiller en direct.
  - V0, `+local` : le conseiller actuel.
  - V1, `+local+danger` : V0, plus le bilan de danger (`ThreatAssessment`) et une ligne de description dans les instructions.
- **Critères déterministes**, dans `danger-check` :
  - une position de létal est réussie si le conseil est une étape de la séquence de létal ;
  - une position de danger (marge ≤ 5) est réussie si le conseil est une défense légale en un coup (tuer un attaquant, gagner de l'armure) ;
  - une position témoin est réussie si le conseil n'est pas défensif.
- **Évaluation à l'aveugle** : l'agent A compare X et Y sans savoir quelle variante est laquelle (`paires-aveugles/`, clé gardée hors du dépôt).
- **Plafond** : 0,95 $, un appel à la fois.

## Ce qui s'est passé

- 50 appels lancés, 0,686 $ dépensés. **20 appels ont échoué : le crédit du compte OpenAI était épuisé** (HTTP 429, « no credits remaining »), à partir de 12:08. Aucun nouvel appel n'a été tenté.
- 30 réponses, dont **11 paires complètes** (V0 et V1 sur la même position).

## Résultats

| Mesure | V0 (actuel) | V1 (+ danger) |
|---|---|---|
| Positions de létal : étape du létal | 4 sur 4 | 3 sur 3 |
| Témoins : conseil non défensif | 5 sur 5 | 6 sur 6 |
| Danger : défense en un coup | 2 sur 6 | 2 sur 8 |
| Paires complètes : même première action | 10 sur 11 | 10 sur 11 |
| Coût moyen par appel (paires) | 0,0216 $ | 0,0227 $ (+5 %) |
| Réponse complète, médiane (paires, sans flux) | 15,2 s | 14,8 s |
| Concordance avec le conseil donné en partie (6 positions, 9 comparaisons) | 6 sur 6 positions identiques | identiques |

**Ce que montrent les deux positions clés :**
- **Rexxar (0 h 44, 27 dégâts visibles contre 25 PV).**
  - **Les deux variantes proposent la même défense en plusieurs coups** : Gardien puis Glissedrake sur Ursol, ton héros sur le Serpent, puis Forgeron et Gain d'armure, « 20 PV et armure contre 11 dégâts visibles ». Même sans le bilan de danger, le modèle voit la menace quand il a le temps de répondre.
  - En partie, aucune réponse n'était arrivée. La défaite vient de la **latence**, pas du raisonnement.
  - Le critère « défense en un coup » classe ce plan « non défensif » : il est trop étroit.
- **Nozdormu (22 h 53).**
  - **V0 rejoue Nozdormu**, comme en partie, tout en écrivant lui-même : « Nozdormu donne accès à 10 cristaux à l'adversaire ».
  - **V1, avec le signal chiffré** (« 10 cristaux au lieu de 7, avec 8 cartes en main »), choisit autre chose : attaquer Devin, Animatrice de stade, Gain d'armure.
  - Seule différence de première action sur les 11 paires.

## Ce qu'on peut conclure, et ce qu'on ne peut pas

- **Pas de régression mesurée** : V1 ne perd aucun létal, ne devient pas défensif sur les témoins, ne coûte presque rien de plus et n'est pas plus lent. Mais 11 paires ne permettent pas d'exclure un effet rare.
- **Gain de décision** : une seule décision change sur 11, la plus importante. **Ce n'est pas statistiquement démontrable** : un test de signe sur un seul désaccord ne prouve rien.
- **Variabilité du modèle** : elle est faible sur ces positions, puisque 6 positions sur 6 reproduisent le conseil donné en partie. Les écarts entre variantes ne sont donc probablement pas du bruit, mais l'échantillon est minuscule.
- **Positions de danger sans paire** : les 12 positions de danger n'ont que 4 paires complètes, à cause du crédit épuisé.
