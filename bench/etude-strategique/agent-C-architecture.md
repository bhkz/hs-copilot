# Agent C : ce que HS Copilot sait, calcule et envoie au modèle

Étude d'architecture du 26/10/2026, matin. **Aucun appel à l'API OpenAI.** Aucun fichier du dépôt n'a été modifié en dehors de ce rapport, et rien n'a été écrit dans `%LOCALAPPDATA%\HsCopilot`.

Chaque constat porte une étiquette :

| Étiquette | Sens |
|---|---|
| **[code]** | démontré par la lecture du code, avec fichier:ligne |
| **[données]** | mesuré sur les traces, les rapports ou les positions réelles |
| **[hypothèse]** | supposition raisonnée, pas mesurée |

## Résumé

1. **Le système marche, et sa partie déterministe est fiable.** Les 25 tours où le solveur a annoncé un létal « certain » ont tous fini la partie [données]. La garde de continuité laisse passer 63 % des coups joués sans nouvel appel. Elle s'est trompée une seule fois sur 45 attaques acceptées [données].
2. **55 % des conseils affichés sont des étapes de plan** que le modèle a écrites sur un état antérieur et n'a jamais réévaluées : 317 sur 581, contre 195 venant du modèle et 69 du solveur [données]. Ces étapes ne passent que par le validateur : légalité, ordre et plafond du létal.
3. **Le danger adverse est calculé, mais mal, et seulement affiché.** Dans la partie Rexxar perdue de 0 h 21, l'alerte « Menace 25 contre 25 » s'est affichée aux décisions 1 et 2. Elle a disparu à la décision 3, juste avant l'attaque fatale sur Rexxar, parce que le calcul ignore le pouvoir héroïque adverse (2 dégâts). Ce calcul n'est envoyé ni au modèle ni au validateur [code + données].
4. **Le solveur ne simule pas les échanges.** Dans 6 des 25 tours de létal, il disait « plausible » à la première décision alors que le létal passait par le retrait d'une Provocation : 5 échanges et une attaque de zone. Aujourd'hui, ce calcul est laissé au modèle [données].
5. **Le banc d'essai n'envoie pas ce qu'envoie le direct** : ni `localAnalysis`, ni `previousAdvice`, ni Découvertes, ni mulligans, ni décisions au-delà de la 2e du tour. La liste du deck dépend du fichier de cas utilisé [code]. Les résultats du MVP 3A portent donc sur un autre message que celui du direct.
6. **Le message fait environ 20 000 caractères, soit environ 8 300 jetons par appel avec les instructions** [données].
   - En retirant ce qui est vide, redondant ou constant, il perd 37 à 40 % de sa taille [données, simulation] sans perdre d'information utilisée.
   - Son ordre empêche le cache au-delà des instructions : `revision` arrive en deuxième position [code].
7. **Coût réel des appels** : sur 49 parties classées, 818 appels envoyés.
   - Environ 430 ont été annulés après envoi. Leur coût estimé (6,43 $) dépasse le coût réel des appels terminés (6,35 $) [données].
   - Le joueur agit en médiane toutes les 7 s, soit à peu près la latence d'un début de réponse [données].
8. **Douze pistes sans réécriture** : tableau final au §9. Les trois premières :
   - danger adverse corrigé et branché partout ;
   - banc fidèle au direct ;
   - échanges contre les Provocations dans le solveur.

## 1. Méthode et sources

**Version étudiée.** Les fichiers de `src/HsCopilot.Core/Strategy`, `State` et `Evaluation` ont été lus vers 11 h 50.
- **Deux modifications sont apparues pendant l'étude**, faites par un autre agent :
  - `Strategy/ThreatAssessment.cs`, créé à 11 h 58 ;
  - `Evaluation/BenchRunner.cs` et `Cli/BenchCommand.cs`, modifiés à 11 h 59 (variantes de banc « local » et « danger »).
- **Les numéros de ligne de ce rapport désignent la version lue avant ces modifications.** Les fichiers cités, sauf BenchRunner, n'ont pas changé depuis.
- La Release lancée par l'application, compilée à 10 h 31, n'a aucune de ces deux modifications.

**Données, en lecture seule** :
- 49 rapports de parties classées terminées depuis le 25/10 à 19 h (42 victoires, 7 défaites), dans `%LOCALAPPDATA%\HsCopilot\reports` ;
- les traces du conseiller depuis le 25/10 à 21 h 08 ;
- 830 positions de décision classées tirées des journaux de 46 parties : `bench/etude-strategique/positions-toutes.jsonl`, produit par un autre agent et copié avant lecture. Ce fichier contient au plus 3 décisions par tour et pas de liste de deck ;
- les 582 cas de `bench/cases.jsonl` et `bench/cases-deck.jsonl`.

**Outils, tous hors du dépôt**, dans le dossier temporaire de session `C:\Users\thoma\AppData\Local\Temp\claude\c--Users-thoma-Desktop-HSguide\f24c0e17-f6bd-4886-8fec-932936373eb9\scratchpad` :
- **essai à blanc** : `hscopilot bench run --cases bench\cases.jsonl --dry-run --dump --limit 20`, puis la même commande sur `cases-deck.jsonl`. La clé avait été retirée de l'environnement, et l'exécutable Debug avait été copié dans le dossier temporaire pour ne pas gêner l'autre agent, qui compilait au même moment. Le dossier de sortie est `essai\` au lieu de `%TEMP%\essai`, pour ne pas reprendre les cas d'un autre agent ;
- **petit programme C#** (`harness\`) qui référence une copie de `HsCopilot.Core.dll` : il rejoue `TurnSolver`, `QuickAnalysis`, `StrategyCoordinator.UnexpectedChange` et `PromptBuilder` sur les positions ;
- **scripts Python** (`scripts\`) : tailles du message, simulation d'allègement, rythme du joueur, attentes de suite du plan.

## 2. La chaîne de décision telle qu'elle tourne

```
état publié ─► StrategyCoordinator.Evaluate (StrategyCoordinator.cs:323)
  ├─ clé de décision, état incomplet, classé, pause, rattrapage           (360-397)
  ├─ contexte : DecisionContextBuilder.Build / BuildChoice                 (399-401)
  ├─ solveur + faits rapides (QuickAnalysis), gratuits                     (402-404)
  ├─ seule la fin de tour possible → rien                                  (405-409)
  ├─ létal « certain » → conseil local, sans appel                         (411-421)
  ├─ conseil suivi, rien de nouveau → étape suivante du plan, sans appel  (423-433)
  ├─ appel conservé : début de réponse joué → on attend sa suite          (435-448)
  ├─ localAnalysis + previousAdvice ajoutés au contexte (actions seules)  (449-453)
  ├─ garde-fou anti-fuite, empreinte, réutilisation, quotas, budgets      (454-502)
  └─ appel en flux (attente 0,3 s, ou 2,5 s en milieu de tour) → validation → publication (504-527, 917-1078)
```

| Mesure (49 parties classées, rapports) | Valeur |
|---|---|
| Appels envoyés | 818 |
| Appels annulés après envoi | ≈ 430 (6,43 $ estimés à 0,015 $ l'appel) |
| Appels terminés | ≈ 385, pour 6,35 $ réels |
| Jetons par appel terminé | ≈ 8 300 en entrée, ≈ 470 en sortie (3 196 730 / 181 243 au total) |
| Appels conservés après un début de réponse joué | 208, dont 129 avec une suite reçue |
| Conseils affichés (traces dédoublonnées, depuis le 25/10 à 21 h 08) | modèle 195, **plan 317**, solveur 69 |
| Conseils du modèle sans suite (`followUp` vide) | 89 sur 195 |

## 3. Informations disponibles mais non utilisées pour décider

| Information | Où elle est | Ce qui en est fait | Preuve | Conséquence |
|---|---|---|---|---|
| **Pouvoir héroïque adverse** (texte, coût) | `state.opponent.heroPower` | envoyée brute au modèle ; ignorée par le calcul de menace | `BoardMath.OpponentBoardDamage` ne compte que serviteurs et arme (DubiousMoveDetector.cs:87-93) [code] | Partie Rexxar (trace du 26/10, 23:44 UTC) : « Menace : 25 dégâts contre tes 25 PV » à 23:44:00 et 23:44:13. Plus d'alerte à 23:44:23 : le plateau tombe à 23, et les 2 dégâts du pouvoir héroïque ne sont pas comptés. C'est la décision où le Glissedrake attaque Rexxar et déclenche le Piège explosif. Position `sessions/20261026_003106_hdt.log#1@32460` [données] |
| **Faits rapides** (Menace, Prochain tour, Ordre) | `QuickAnalysis.Analyze` (QuickAnalysis.cs:188-232) | affichés dans l'overlay | `LocalAnalysisPayload` n'envoie pas la menace (QuickAnalysis.cs:110-130) ; le validateur ne la regarde pas (RecommendationValidator.cs:122-141) [code] | le modèle doit recalculer la menace lui-même (config/strategy/guerrier-dragons.md:25) |
| **Tes Provocations ; Méga-Furie des vents adverse** | plateau | ignorées par la menace | DubiousMoveDetector.cs:89-90 [code] | la menace est surestimée (Provocations) ou sous-estimée (Méga-Furie) |
| **Ressources adverses** : cristaux du prochain tour, cartes en main, cartes dans le deck | `opponent.mana`, `hand.count`, `deck.count` | envoyées brutes, jamais résumées | aucune référence dans Strategy/ [code] | Nozdormu : 7 → 10 cristaux pour un adversaire à 8 cartes, non signalé. Rexxar : 1 carte dans son deck, fatigue proche, non signalée (DIAGNOSTIC-DEFAITES §1-2) |
| **Rythme du joueur** | `history[].time` | retiré du message (PromptBuilder.cs:107-108) ; inutilisé par le coordinateur | [code] ; médiane de 7 s entre deux actions d'un tour (P25 4 s, P75 11 s), et 52 % sous 8 s, sur 644 intervalles [données] | appels de milieu de tour envoyés alors que la réponse arrivera après le coup |
| **Règles du détecteur de coups douteux** : échange perdant, Profondeurs sur un adversaire qui survit, dégâts sur son camp, Chronoseigneur sans cible, fin de tour prématurée | `DubiousMoveDetector` (DubiousMoveDetector.cs:126-230) | banc d'essai et fenêtre d'examen seulement | appelé uniquement dans BenchRunner, BenchSummary et BenchViewModels [code] | aucune mise en garde de ce type en direct, ni sur les 317 étapes de plan |
| **Jetons servis depuis le cache** | `StrategyResult.CachedInputTokens` | non cumulés dans les rapports | seuls « jetons envoyés » et « reçus » sont comptés (StrategyCoordinator.cs:988-989) [code] | l'effet d'un réordonnancement du message ne peut pas être mesuré en direct |
| **Effet écrit et certain de la carte jouée** (épée 2/2 de Gardien des couvées) | texte de la carte, `poweredUp` | traité comme une information nouvelle | `NothingNew` refuse toute nouvelle arme (StrategyCoordinator.cs:725) [code] | 24 nouveaux appels sur 486 coups mesurés, pour une épée prévue par le texte [données] |
| **Liste du deck** | `local.deck.knownRemaining` | envoyée brute ; probabilités seulement si une pioche filtrée est en main (DeckOdds.cs:264) | [code] ; le modèle ne cite jamais une carte restante, sur 221 réponses (SOMBRES-DONS §4) | aucune estimation des cartes qui donneraient le létal au tour suivant |
| **Cartes adverses vues** (`revealedCards`, cimetière) | état | envoyées brutes : environ 2 600 caractères, souvent en double | [données] | rien de synthétisé (cartes déjà jouées, archétype) |
| **Classe des Secrets adverses** | `SecretView.CardClass` | « Secret inconnu de classe X » dans `hypotheses` | DecisionContextBuilder.cs:105-106 [code] | la liste publique des Secrets possibles de cette classe n'est pas fournie ; son utilité reste une **[hypothèse]** |

## 4. Calculs laissés au modèle qui devraient être déterministes

| Calcul | Aujourd'hui | Pourquoi il devrait être déterministe |
|---|---|---|
| **Danger au prochain tour adverse** : létal visible, marge, défense minimale | demandé au modèle : « Survie : menace de létal adverse visible… » (guerrier-dragons.md:25) | c'est une arithmétique sur l'information publique. Mesuré sur 291 fins de tour : 4 létaux adverses visibles, 4 défaites, aucune fausse alarme (DIAGNOSTIC-DEFAITES §4) |
| **Retrait des Provocations pour un létal** | le solveur répond « plausible » et laisse le modèle chercher l'échange (TurnSolver.cs:338-360) | une attaque contre un serviteur a une issue certaine, hors déclencheurs : ATQ contre PV, Bouclier divin, Toxicité. Cas réels au §5.2 |
| **Issue des échanges** (« A tue B, A survit ») | le modèle compare les statistiques (consigne 11, PromptBuilder.cs:53-54) | gpt-6-luna s'est trompé sur un Bouclier divin (MVP3A, exemple 3). 463 positions sur 830 proposent au moins une attaque sur un serviteur, 3 en médiane et jusqu'à 20 [données] |
| **Fin de tour avec du mana et des cartes jouables** | le validateur ne regarde que les attaques et le pouvoir héroïque (RecommendationValidator.cs:133-141) | la règle existe déjà dans le détecteur (`FIN_DE_TOUR_PREMATUREE`, DubiousMoveDetector.cs:146-162) |
| **Chance de létal au tour suivant selon la pioche** | non calculée ; le modèle écrit « selon la pioche » | `SolveNextTurn` (TurnSolver.cs:417-493) peut être rejoué avec chaque carte restante ajoutée à la main : une vingtaine de cartes distinctes à moins de 0,1 ms chacune |
| **Cadeaux à l'adversaire** : Nozdormu, l'arme aléatoire d'Animatrice | ignorés : la défaite contre Tyrande en vient | Animatrice est jouable dans 236 positions sur 830, Nozdormu dans 5 [données] ; le texte « chaque joueur » / « les deux adversaires » se reconnaît |

**Pourquoi c'est important : le modèle choisit avant d'argumenter.**
- **Ordre du schéma de sortie** : `actionId`, `summary`, `why`, `lethal`, `planNextTurn`, `followUp`, puis seulement `confidence`, `uncertaintyReasons` et `rationale` (OpenAiProvider.cs:123-155) [code].
- **Peu de raisonnement caché** : 141 jetons en moyenne en effort medium, 32 en low (MVP3A-RESULTATS).
- **Conséquence** : le « raisonnement » visible justifie un choix déjà fait. Ce qui n'est pas précalculé dans le message est décidé presque sans réflexion **[hypothèse étayée]**.

## 5. Limites du TurnSolver

### 5.1 Ce qui est mesuré (830 positions classées)

| Mesure | Valeur [données] |
|---|---|
| Niveaux | aucun 750, certain 44, conditionnel 8, plausible 28 |
| Temps de calcul | P50 0,07 ms, P95 0,30 ms, maximum 50 ms |
| Tours avec un létal « certain » | 25, **tous derniers tours de leur partie** (aucun faux positif) |
| Visage bloqué par une Provocation | 180 positions |
| Cartes « non simulées » les plus fréquentes | Le Grand Dracorex 17, Tour des Hurleurs noirs 7, Archaedas 6, Guerrière du Vol infini 5 |

### 5.2 « Plausible » ou « aucun » alors qu'un létal existait

**Sur 25 tours de létal, 10 n'étaient pas « certains » à la première décision** [données].

| Partie (journal) | Tour | 1re décision | Ce qui a débloqué le létal | Classement |
|---|---|---|---|---|
| 20261026_113340 | 8 | plausible : 30 si la Provocation tombe, contre 14 PV | Wyrm (6) tue la Provocation à 5 PV, puis Grommash, Forgeron et Chronoseigneur : 24 | **échange** |
| 20261026_014601 | 9 | plausible : 12 contre 5, Provocation à 6 PV | Dragonnet (Toxicité) tue Techysaurus, puis 11 dégâts | **échange** |
| 20261025_210804 | 8 | plausible : 34 contre 18, Provocation à 9 PV | Suffusion puis Glissedrake sur Tirion, puis le Wyrm : 23 | **sort + échange** |
| 20261026_020236 | 8 | plausible : 3 contre 11 | Frappe puis Glissedrake sur Aile du Vide, puis le Wyrm | **sort + échange** |
| 20261025_225447 | 10 | plausible : 0 contre 6 | Garrosh attaque Tortolla (Provocation) | **échange** |
| 20261025_231754 | 9 | plausible : 3 contre 5 | attaque de zone du Grand Dracorex | **zone, non simulée** |
| 20261025_232750 | 7 | aucun : 6 contre 11 | Fracasseur de confluence (dégâts) | carte non modélisée |
| 20261025_223523 | 9 | aucun : 23 contre 28 | la mort de Kil'jaeden rend Grommash jouable | interaction non modélisée |
| 20261025_213529 | 8 | conditionnel | Provocation à Râle d'agonie | prudence voulue |
| 20261026_111058 | 9 | conditionnel | l'arme aléatoire tombe bien | prudence voulue |

- **Six létaux passaient par le retrait de Provocations** : cinq échanges et une attaque de zone.
- Après l'échange, le solveur trouvait chaque fois un létal « certain » sans condition.
- Un solveur qui simule l'attaque sur la Provocation l'aurait donc très probablement démontré dès la première décision, sans appel au modèle. C'est une **[hypothèse forte]**, à confirmer par des tests sur ces positions.

### 5.3 Trous du modèle de jeu

| Trou | Preuve | Effet | Fréquence |
|---|---|---|---|
| **Aucun échange simulé** : seuls les Profondeurs sur une Provocation à 1 PV et Chronoseigneur retirent des Provocations | TurnSolver.cs:599-653 ; remarque 61-62 | « plausible » à tort (§5.2) | 6 tours sur 25 |
| **La pièce absente** : seules les cartes jouables maintenant sont tentées (280, 530), sans le cristal de La pièce | aucune référence à la pièce dans TurnSolver.cs [code] | une carte au coût mana + 1 n'entre jamais dans un létal | La pièce en main dans 135 positions, dont 30 avec une carte de dégâts ou à Charge au coût mana + 1 [données] ; aucun létal manqué constaté |
| **Borne des Provocations trop stricte** : un serviteur à Ruée ne peut pas viser le héros mais peut tuer une Provocation sans retirer de dégâts au visage | règle « aucun » (TurnSolver.cs:344-352) ; `OpenFace` exclut `RushOnly` (660) [code] | faux « aucun » possible sans carte à effet de zone | non observé |
| **Provocations camouflées** retenues dans `Solve` (205), pas dans `SolveNextTurn` (423) | [code] | une Charge jouée peut être comptée bloquée à tort | rare |
| **Réveil brutal** (Cri de guerre doublé) : un Wyrm vaut 10 au lieu de 5, mais il est compté une fois **et** déclaré modélisé (495-496) | [code] | sous-estimation silencieuse | rare |
| **Dégâts de zone au héros** (« Inflige N … à tous les ennemis ») : reconnus par `DirectDamage` (76), sans cible héros (403-405) | [code] | carte déclarée modélisée mais pas comptée, sans signalement | rare dans ce deck |
| **Cartes d'arme en main, arme à Furie des vents, bonus des sorts hors serviteurs** | 222-223, 702-717 [code] | sous-estimation | 0 arme en main sur 830 positions |
| **Identifiants de cartes codés en dur** (Profondeurs, Voyante, Grommash, Forgeron…) | TurnSolver.cs:66-75, QuickAnalysis.cs:132, RecommendationValidator.cs:188 [code] | maintenance à chaque changement de deck ou de version de carte | — |

### 5.4 Le tour adverse

**Le solveur ne regarde jamais le tour adverse.**
- `SolveNextTurn` porte sur **ton** prochain tour. Il suppose que ton plateau survit et que l'adversaire ne joue rien (TurnSolver.cs:412-416).
- La seule approche du tour adverse est la ligne « Menace » de QuickAnalysis.cs:218-224 :
  - elle est inexacte (§3) ;
  - elle est calculée sur l'état courant, pas après la séquence conseillée ;
  - elle n'est utilisée par personne : ni le modèle, ni le validateur, ni la reprise du plan.

## 6. Continuité du plan : « rien de nouveau », « effet non prévu », appel conservé

### 6.1 Mesures

**Un coup joué entre deux décisions du même tour** : 486 paires, rejugées par `StrategyCoordinator.UnexpectedChange` [données].

| Issue de la garde | Paires |
|---|---|
| changement attendu : la suite du plan peut être reprise | **307 (63 %)** |
| information nouvelle | 148 : carte en main 91, ton arme 40 (dont 24 épées de Gardien), serviteur allié venu d'ailleurs 9, arme adverse 7, serviteur adverse 5, main adverse 3, Secret 3 |
| effet non prévu : serviteur parti | 15, par exemple Chronoseigneur ou l'attaque de zone du Dracorex, alors que l'effet est écrit sur la carte |
| Rembobinage | 10 |
| effet non prévu : héros adverse | 3, par exemple 19 → 22 PV en attaquant Chevalier greffé |
| effet non prévu : autres | 2 : Grommash blessé par un tiers, Forgeron modifié par une aura qui disparaît |

**Attentes « suite du plan attendue »** : 140 dans les traces [données].
- **Issue** : 116 suites affichées, 20 nouvelles analyses, 4 retours à l'attente.
- **Durée** : 1,8 s en médiane, P90 3,5 s, au plus 6,1 s.

### 6.2 Défauts et risques

1. **Sous-options ignorées à la reprise du plan.** `ContinuePlanLocked` ne compare que la source et la cible (StrategyCoordinator.cs:691-692), jamais `SubOptionIndex`.
   - Pour une carte à « Choix des armes », la première sous-option légale est prise, quelle que soit l'intention du modèle [code].
   - `RebindTo`, lui, compare bien la sous-option (StrategyContracts.cs:266-267).
2. **Textes périmés.** Une étape de plan reprend le `Why` et le `Rationale` du conseil précédent (StrategyCoordinator.cs:698, 702). L'overlay affiche donc la raison d'un autre coup [code].
3. **Le résultat du coup n'est pas vérifié.** La garde excuse tout changement de la source et de la cible (StrategyCoordinator.cs:592-595, 609).
   - Si la cible survit alors qu'elle devait mourir, le plan continue.
   - Mesuré : 1 issue contraire au calcul simple sur 45 attaques acceptées. Glissedrake 9 ATQ contre Prédateur en chasse 4/8 : l'attaquant meurt alors que le calcul le donnait survivant [données].
   - Si l'étape suivante utilise ce serviteur, le contrôle des options du serveur la bloque. Sinon, le plan continue sur une hypothèse fausse.
4. **L'exception des auras est large.** Dès qu'une entité concernée entre sur le plateau ou le quitte, toute modification d'attaque ou de PV maximum ailleurs est acceptée (StrategyCoordinator.cs:597-599, 614) [code].
   - Un seul cas réel, légitime : la mort de l'Oracle sinistrécaille [données].
   - Une Hyène charognarde adverse qui grandit passerait aussi.
5. **Une étape de plan n'est jamais confrontée au danger adverse.** Elle passe le validateur (légalité, ordre, plafond du létal : StrategyCoordinator.cs:706), pas une menace qui n'existe pas encore dans ce validateur [code].
   - 55 % des conseils affichés sont de ce type [données].
6. **La suite n'est lue qu'à la fin de la réponse.**
   - Le plan conservé n'est construit qu'à l'arrivée de la réponse complète (StrategyCoordinator.cs:998-1014).
   - `ExtractPartial` ne lit que des chaînes (OpenAiProvider.cs:329-350), alors que `followUp` est le 6e champ du schéma.
   - Dans 221 réponses stockées, 64 % des caractères visibles sont dans des champs qui viennent après `followUp` [données, ancien ordre des champs].
   - La suite pourrait donc être utilisable 1 à 3 s plus tôt **[hypothèse à mesurer]**.
7. **`previousAdvice` peut renvoyer à un conseil ancien.** C'est le dernier conseil **complet**. Les débuts de réponse annulés, très fréquents en classé, n'y figurent pas (StrategyCoordinator.cs:730-743) [code].
8. **Validation à l'arrivée d'un appel conservé.** Elle se fait sans le calcul de létal courant (StrategyCoordinator.cs:1002). C'est sans conséquence : la reprise revalide avec le létal courant (706) [code].

### 6.3 Ce qui marche

- La garde est prudente, et ses refus réels sont justifiés : Grommash blessé par un tiers, héros adverse soigné, aura retirée.
- Les attentes sont courtes.
- Elle ne produit presque pas de fausses acceptations mesurables (1 sur 45).
- **Le coût principal est inverse : des appels de trop.** 24 épées de Gardien, et les morts écrites sur la carte jouée, comme Chronoseigneur ou le Dracorex.

## 7. Contexte envoyé au modèle

### 7.1 Taille réelle

| Échantillon | Message moyen (caractères) | P95 | Max |
|---|---|---|---|
| Essai à blanc, 20 cas de `cases.jsonl`, sans liste de deck | 19 994 | — | 39 257 |
| Essai à blanc, 20 cas de `cases-deck.jsonl` | 20 984 | — | 39 454 |
| 830 positions classées, message du banc | 18 239 | 29 776 | 38 830 |
| Mêmes positions, **message du direct** (avec `localAnalysis`) | 18 948 | 30 569 | 40 578 |
| `localAnalysis` seul (830 / 582 cas) | 692 / 939 | 1 308 / 2 095 | 2 183 / 2 785 |
| Instructions, fixes et en cache | 8 553 | — | — |

Avec la liste HDT, qui ajoute environ 1 070 caractères (écart mesuré entre `cases` et `cases-deck`), un message réel fait **environ 20 000 caractères**. Avec les instructions et le schéma, cela donne **environ 8 300 jetons** d'entrée, cohérent avec les rapports.

**Répartition**, sur les 20 cas avec deck [données] :

| Partie du message | Part | Détail |
|---|---|---|
| `state` | 81 % | `local` 33 % (main 10 %, plateau 7 %, deck 6 %, cimetière 5 %) ; `opponent` 24 % (`revealedCards` 8 %, cimetière 6 %, plateau 4 %) ; `history` 22 % |
| `legalActions` | 16 % | jusqu'à 14 Ko quand le Wyrm, Suffusion et Frappe ont chacun une dizaine de cibles |
| `rules` et `sequenceVerification` | 1,5 % | constantes répétées à chaque appel (StrategyContracts.cs:111, 115-120) |
| valeurs vides, `false` ou `null` | 8 à 10 % | — |

**Historique** (25 entrées) :
- 21 % de marqueurs « turn-start » et 13 % de déclencheurs ;
- 5 Ko en médiane, dont 290 caractères seulement pour le tour en cours [données].

### 7.2 Le banc d'essai n'envoie pas ce qu'envoie le direct

| Élément | Direct | Banc (version de référence) | Preuve |
|---|---|---|---|
| `localAnalysis` : létal, ordre, tour suivant, probabilités | oui | **non** : `BuildPayload` ne fait que `PromptBuilder.BuildUserPayload(ctx)` | StrategyCoordinator.cs:449-451 ; BenchRunner.cs:134-139 [code] |
| `previousAdvice` | oui | **non** | StrategyCoordinator.cs:452 [code] |
| Liste du deck | oui (HDT) | selon le fichier : absente de `cases.jsonl` et de `positions-toutes.jsonl` (DECKLIST_UNKNOWN), présente dans `cases-deck.jsonl` | [données] |
| Types de décision | actions, Découvertes, Rembobinages, mulligans | **actions seulement** (`BuildContext` → `Build`), et seulement les 2 premières décisions du tour par défaut | BenchRunner.cs:130-131 ; DecisionCases.cs:89-100 [code] |
| Mode d'appel | flux | appel simple : latence et premier fragment non comparables | BenchRunner.cs:264-266 [code] |
| Instructions | `BuildInstructions(notes)` | identiques ; le fichier de stratégie de la Release est identique à celui du dépôt | [code + données] |

**Conséquences** :
- Les consignes 13 et 14 (PromptBuilder.cs:57-61) renvoient au banc à un champ absent.
- Toute mesure du banc (MVP3A, `advisor-probe` à part) porte sur un conseiller sans calcul local.
- Les Découvertes à Sombre don, où une vraie lacune a été trouvée (SOMBRES-DONS §3), ne sont pas mesurables par le banc.

**Modification en cours.** Celle de l'autre agent (11 h 59) ajoute `localAnalysis` et la menace seulement pour les variantes suffixées « local » ou « danger ». Par défaut, le banc reste sans calcul local, sans `previousAdvice` et sans choix.

**Détail.** Le garde-fou anti-fuite contrôle `ctx.ToPayloadJson()` (StrategyCoordinator.cs:454), alors que le fournisseur envoie `PromptBuilder.BuildUserPayload` (OpenAiProvider.cs:187-188). Le message contrôlé contient tout le message envoyé, donc le contrôle reste sûr. Mais l'interface `IPayloadStrategyProvider`, prévue pour éviter cet écart, n'est utilisée que par le banc.

### 7.3 Ce qui manque

1. **Danger adverse** : dégâts visibles avec le pouvoir héroïque, marge avec tes Provocations, meilleure ligne défensive, cadeaux à l'adversaire (§3, §4).
2. **Ressources adverses résumées** : cristaux au prochain tour, cartes en main, tours avant la fatigue, des deux côtés.
3. **Table des échanges** pour les attaques légales sur des serviteurs : tue / meurt / survit.
4. **Létal du prochain tour selon la pioche**, et les cartes clés restantes (Grommash, Wyrm, Profondeurs), aujourd'hui seulement dans la liste brute.
5. **Pour les Découvertes**, aucun calcul local : par exemple, quelle proposition donne un létal au tour suivant, comme un serviteur avec Charge par Somnambule. Le coordinateur n'ajoute `localAnalysis` qu'aux actions (StrategyCoordinator.cs:449).
6. **Confiance plafonnée.** `opponentInformationHidden` est vrai dans 830 positions sur 830, parce que le deck adverse n'est jamais vide (DecisionContextBuilder.cs:86-87) [données]. Le validateur ramène donc toute confiance « élevée » à « moyenne » et ajoute la même raison partout (RecommendationValidator.cs:101-110). Le champ ne porte plus d'information.

### 7.4 Ce qui est volumineux sans servir : simulation d'allègement

Chaque étape s'ajoute aux précédentes ; aucun appel n'a été fait [données].

| Étape | 20 cas avec deck | 830 positions |
|---|---|---|
| Retirer les valeurs par défaut (`false`, `[]`, `null`) | −9,4 % | −10,5 % |
| `legalActions` réduites à id, type, libellé, entités et coût | −8,6 % | −6,8 % |
| Retirer les `cardId` et les champs `base*`, `costModified`, `knowledge`, `damage`, `turnNumber` | −14,7 % | −13,8 % |
| Retirer des `revealedCards` adverses les cartes déjà au cimetière ou en jeu | −5,4 % | −5,4 % |
| Retirer les constantes (`rules`, `sequenceVerification`, identifiants) | −2,1 % | −2,3 % |
| **Total** | **−40,3 %** | **−36,5 %** |

**Effet estimé** : environ 2 100 jetons de moins par appel, soit environ 0,004 $ au tarif d'entrée hors cache (2 $ le million).
- Cela fait environ −25 % sur le coût d'un appel terminé et −30 % sur un appel annulé, qui ne paie que l'entrée.
- Sur les 818 appels des 49 parties : environ 3,5 $ **[estimation]**.
- L'effet sur la latence n'est pas mesuré.

**Attention, le garde-fou anti-fuite ne cherche que des identifiants de carte** (AdviceBoard.cs:77-97). Si on retire les `cardId` du message, il faut lui faire vérifier aussi les noms des cartes cachées. Sinon, il ne protège plus rien.

**L'historique** pourrait devenir une ligne par tour, par exemple « T5 adverse : Exécutrice déguisée, Toile de tromperie sur elle ; Patin-o-tron tue Avant-garde ». Le gain n'est pas simulé ici.

### 7.5 Cache

- **Le message est le même à chaque appel jusqu'au premier élément qui change** : c'est cette partie que le cache peut réutiliser. Elle s'arrête après les instructions.
- **La cause** : le message commence par `gameId`, puis `revision`, qui change à chaque état (StrategyContracts.cs:99-103). Le reste se compte en milliers de jetons stables pendant un tour : liste du deck, cimetières, cartes adverses vues, historique des tours passés.
- **Mesure actuelle** : environ 2 560 jetons servis depuis le cache sur 8 400 (DIAGNOSTIC-DEFAITES §5) [données].
- **Ce qu'il faudrait** : mettre d'abord ce qui est stable, puis ce qui change.
  - Le gain sur le coût, et peut-être sur le délai avant le premier jeton, est une **[hypothèse]**.
  - Il est mesurable, à condition de compter enfin les jetons servis depuis le cache (§3).

## 8. Pistes d'amélioration sans réécriture majeure

### A. Danger adverse corrigé et branché partout

**Quoi.**
- Remplacer `BoardMath.OpponentBoardDamage` par un calcul complet : pouvoir héroïque, Méga-Furie des vents, effets de fin de tour, tes Provocations.
- Il est utilisé dans QuickAnalysis.cs:220 et DecisionCases.cs:235.
- Le module `ThreatAssessment` qu'un autre agent est en train d'écrire couvre déjà le calcul. Il reste à le brancher sur :
  1. le fait affiché immédiatement, en tête de l'overlay et non noyé parmi les faits ;
  2. `localAnalysis.opponentThreat` ;
  3. une mise en garde du validateur quand le conseil va au visage alors qu'un létal adverse est visible et qu'une défense existe ;
  4. la reprise du plan (`ContinuePlanLocked`).

| | |
|---|---|
| Preuve | partie Rexxar : l'alerte disparaît à la décision fatale (§3) ; 4 létaux adverses visibles sur 291 fins de tour, sans fausse alarme (DIAGNOSTIC-DEFAITES §4) |
| Bénéfice | élevé. Une défaite évitable sur 35 parties dans le diagnostic. Un signal gratuit et instantané, même quand le modèle n'a pas répondu |
| Coût de développement | faible à moyen : le module est en cours ; il reste environ 60 lignes d'intégration et des tests sur les états Rexxar et Tyrande |
| Coût de maintenance | faible à moyen : textes de pouvoirs héroïques et effets de fin de tour à tenir à jour |
| Risque | faible. La main adverse reste inconnue : présenter un **plancher**, jamais une sécurité |

### B. Banc d'essai fidèle au direct

**Quoi.**
- Une seule fonction construit le message, pour le coordinateur comme pour le banc : action plus `localAnalysis` plus `previousAdvice` éventuel.
- Le banc l'utilise **par défaut**.
- Le banc se construit avec la liste du deck (`--deck`) et inclut les Découvertes, les mulligans et les décisions au-delà de la 2e du tour.
- Pour `previousAdvice` : le rejouer à partir de la réponse du banc sur la décision précédente du même tour, ou l'omettre explicitement.

| | |
|---|---|
| Preuve | §7.2 |
| Bénéfice | élevé et indirect : chaque changement de message ou de solveur, et chaque piste ci-dessous, devient mesurable sur ce qui part vraiment |
| Coût de développement | faible à moyen, environ 100 à 150 lignes |
| Coût de maintenance | négatif : il supprime une divergence à surveiller |
| Risque | faible |

### C. Solveur : échanges contre les Provocations

**Quoi.**
- Avant de compter le visage, tenter d'affecter des attaquants aux Provocations, de façon exhaustive avec au plus 7 × 7 combinaisons :
  - attaque ≥ PV, ou Toxicité ;
  - deux coups contre un Bouclier divin ;
  - sort de dégâts ciblant la Provocation, Profondeurs puis attaque.
- Les attaquants à Ruée et ceux qui ne peuvent pas viser le héros servent d'abord.
- Une Provocation à Râle d'agonie, à Réincarnation ou à déclencheur « après avoir subi des dégâts » rend la ligne conditionnelle, comme aujourd'hui.

| | |
|---|---|
| Preuve | 6 tours de létal sur 25 vus seulement après l'échange (§5.2) |
| Bénéfice | moyen à élevé : un létal démontré dès la 1re décision, sans appel et sans attendre 5 à 10 s, et la suite calculée pour tout le tour |
| Coût de développement | moyen, environ 150 à 250 lignes, plus des tests sur les 5 positions réelles du §5.2 |
| Coût de maintenance | moyen : effets de mort et déclencheurs |
| Risque | moyen. Il faut préserver le 25 sur 25 des « certain » : rejouer les 830 positions avant et après, et ne rien déclarer « certain » en présence d'un texte de déclencheur adverse non reconnu |

### D. Suite du plan lue dans le flux

**Quoi.**
- Extraire `followUp` du flux dès que le tableau est fermé, comme `ExtractPartial` le fait déjà pour les chaînes.
- Le valider aussitôt, avec les mêmes règles, quand l'appel porte le plan.
- Pour la même raison, placer `followUp` avant `planNextTurn` dans le schéma.

| | |
|---|---|
| Preuve | §6.2, point 6 |
| Bénéfice | moyen : 140 attentes mesurées, 1,8 s en médiane et 3,5 s au P90, qui pourraient raccourcir de 1 à 3 s **[hypothèse]** ; et moins d'appels conservés perdus quand tu enchaînes |
| Coût de développement | faible à moyen, environ 60 à 100 lignes |
| Coût de maintenance | faible |
| Risque | faible à moyen : lecture d'un JSON partiel, à tester sur des flux enregistrés |

### E. Message allégé

**Quoi.** Les étapes du §7.4, plus un historique résumé par tour.

| | |
|---|---|
| Bénéfice | moyen : −36 à −40 % de caractères, environ −0,004 $ par appel ; latence à mesurer |
| Coût de développement | moyen, environ 150 lignes dans PromptBuilder, plus le garde-fou étendu aux noms |
| Coût de maintenance | faible |
| Risque | moyen : la qualité des conseils peut changer. **À ne faire qu'après B**, avec une comparaison avant / après sur les mêmes cas |

### F. Ordre « stable d'abord » et compteur des jetons en cache

**Quoi.**
- Sérialiser d'abord ce qui ne change pas pendant la partie ou le tour : liste du deck, cimetières, cartes adverses vues, historique des tours passés.
- Mettre l'état volatil et les actions légales en dernier.
- Cumuler `CachedInputTokens` dans les rapports.

| | |
|---|---|
| Bénéfice | moyen **[hypothèse]** : coût, et peut-être délai du premier jeton |
| Coût de développement | faible, environ 40 lignes |
| Coût de maintenance | faible |
| Risque | faible |

### G. Règles du détecteur en direct

**Quoi.**
- Brancher dans `RecommendationValidator` des mises en garde « à vérifier » :
  - `ECHANGE_PERDANT` ;
  - `PROFONDEURS_SUR_ADVERSAIRE` ;
  - `DEGATS_SUR_SON_HEROS` et `DEGATS_SUR_SON_SERVITEUR` ;
  - `CHRONOSEIGNEUR_SANS_CIBLE` ;
  - fin de tour avec du mana et une carte jouable.
- Les appliquer aussi aux étapes de plan.

| | |
|---|---|
| Preuve | §3 et §4 ; 317 étapes de plan que rien ne rejuge |
| Bénéfice | moyen |
| Coût de développement | faible, environ 50 lignes : les règles existent déjà |
| Coût de maintenance | faible |
| Risque | faible à moyen : ce sont des heuristiques, à afficher comme « à vérifier » et jamais comme des blocages |

### H. Garde de continuité affinée

**Quoi.**
1. Comparer `SubOptionIndex` à la reprise du plan.
2. Ne pas reprendre le `Why` du coup précédent, ou le marquer comme tel.
3. Accepter un effet écrit et certain de la carte jouée : épée 2/2 de Gardien avec `poweredUp`, mort des cibles de Chronoseigneur.
4. Comparer l'issue d'une attaque au calcul simple (tue / survit) et traiter un écart comme imprévu.

| | |
|---|---|
| Bénéfice | faible à moyen : environ 24 appels de moins sur 46 parties pour le seul Gardien ; deux défauts latents corrigés |
| Coût de développement | faible, environ 60 lignes et 6 tests |
| Coût de maintenance | faible |
| Risque | faible |

### I. Appels de milieu de tour selon ton rythme

**Quoi.**
- Prévoir, à partir de tes délais entre actions dans la partie et de la latence mesurée du premier fragment, si une réponse de milieu de tour arrivera avant ton coup.
- Si non, et si rien n'est risqué (pas de létal plausible, pas de danger), ne pas envoyer l'appel, ou allonger l'attente.
- Le calcul local et la suite du plan restent affichés.

| | |
|---|---|
| Preuve | environ 430 appels annulés après envoi, pour 6,43 $ estimés, contre 6,35 $ réels ; médiane de 7 s entre deux actions |
| Bénéfice | moyen à élevé sur le coût, de l'ordre de −20 à −35 % **[hypothèse]** |
| Coût de développement | moyen, environ 100 lignes |
| Coût de maintenance | faible |
| Risque | moyen. Le rythme mesuré dépend du conseiller lui-même, puisque tu attends parfois le conseil ; moins de conseils de milieu de tour. À régler par un plafond, pas par une interdiction |

### J. Petits trous du solveur

**Quoi** :
- La pièce : un cristal de plus et les cartes au coût mana + 1 ;
- la Ruée dans la borne des Provocations ;
- les Provocations camouflées retirées dans `Solve` ;
- Réveil brutal et les dégâts de zone signalés comme non simulés, ou simulés.

| | |
|---|---|
| Bénéfice | faible : cas rares ; La pièce est en main dans 135 positions |
| Coût de développement | faible, environ 40 lignes et des tests |
| Coût de maintenance | faible |
| Risque | faible |

### K. Compléments de `localAnalysis`

**Quoi** :
- table des échanges ;
- ressources adverses résumées et tours avant la fatigue ;
- chance de létal au tour suivant selon la pioche (§4) ;
- calcul local pour les Découvertes : létal au tour suivant avec chaque proposition et son don.

| | |
|---|---|
| Bénéfice | incertain : le modèle utilise-t-il ces champs ? Les probabilités de pioche ne l'ont pas été (SOMBRES-DONS §6) |
| Coût de développement | faible à moyen |
| Coût de maintenance | faible |
| Risque | faible ; environ 200 à 1 200 caractères de plus. À mesurer sur le banc B |

### L. Confiance informative

**Quoi.** Ne plus compter « deck adverse non vide » comme information cachée qui interdit la confiance élevée, puisque c'est toujours vrai. Garder la main cachée et les Secrets inconnus.

| | |
|---|---|
| Bénéfice | faible : lisibilité, et un signal de confiance qui varie de nouveau |
| Coût de développement | très faible |
| Coût de maintenance | nul |
| Risque | très faible |

## 9. Tableau final : pistes classées

Le classement suit le rapport bénéfice / (coût + risque), en tenant compte des dépendances : E et K se mesurent avec B.

| Rang | Piste | Bénéfice attendu | Coût de développement | Coût de maintenance | Risque | Preuve principale |
|---|---|---|---|---|---|---|
| 1 | **A. Danger adverse corrigé et branché** (fait, message, validateur, plan) | élevé | faible à moyen (module en cours) | faible à moyen | faible | Rexxar : alerte perdue à la décision fatale ; 4 létaux adverses sur 291 fins de tour |
| 2 | **B. Banc fidèle au direct** | élevé (indirect) | faible à moyen | négatif | faible | `localAnalysis`, `previousAdvice`, choix et deck absents du banc |
| 3 | **C. Échanges contre les Provocations** | moyen à élevé | moyen | moyen | moyen | 6 tours de létal sur 25 vus seulement après l'échange |
| 4 | **G. Règles du détecteur en direct** (et sur le plan) | moyen | faible | faible | faible à moyen | 317 étapes de plan sans réexamen |
| 5 | **H. Garde de continuité affinée** | faible à moyen | faible | faible | faible | sous-option ignorée ; 24 appels pour l'épée de Gardien ; 1 issue sur 45 |
| 6 | **D. Suite du plan lue dans le flux** | moyen | faible à moyen | faible | faible à moyen | 64 % des caractères après `followUp` ; attente de 1,8 s en médiane |
| 7 | **F. Ordre « stable d'abord » et jetons en cache comptés** | moyen (hypothèse) | faible | faible | faible | cache limité aux instructions, environ 2 560 jetons sur 8 400 |
| 8 | **I. Appels selon ton rythme** | moyen à élevé (coût) | moyen | faible | moyen | 430 appels annulés, coût estimé supérieur au coût réel |
| 9 | **E. Message allégé** (après B) | moyen | moyen | faible | moyen | −36 à −40 % simulés |
| 10 | **J. Petits trous du solveur** | faible | faible | faible | faible | La pièce, Ruée, Camouflage, Réveil brutal |
| 11 | **L. Confiance informative** | faible | très faible | nul | très faible | 830 sur 830 marquées « informations cachées » |
| 12 | **K. Compléments de `localAnalysis`** (échanges, ressources, pioche, Découvertes) | incertain | faible à moyen | faible | faible | à mesurer sur le banc B |

**Ordre conseillé.**
1. Commencer par A et B ensemble : A règle le défaut démontré, et B permet de mesurer tout le reste.
2. Faire ensuite C, avec rejeu des 830 positions pour garantir que le « certain » reste à 25 sur 25.
3. G, H, D et F sont de petites pièces indépendantes.
4. Garder I et E pour après la mesure, car ils peuvent changer ce que tu vois ou la qualité des conseils.

## 10. Limites de cette étude

- **Pas de vérité de référence.** Rien ici ne mesure la qualité stratégique des conseils, seulement ce qui est calculé, envoyé et réutilisé.
- **Échantillon.** Les 830 positions ont au plus 3 décisions par tour.
  - La continuité n'est donc mesurée que sur les deux premiers enchaînements de chaque tour.
  - Les paires où deux actions séparent deux décisions sont écartées.
- **Message du direct reconstitué.** Il est rebâti hors ligne avec le code actuel : `localAnalysis` est calculé, mais sans la liste du deck pour ces positions, et sans `previousAdvice`. Les messages réellement envoyés ne sont pas enregistrés, et la trace ne les contient pas (AdvisorTrace.cs:106-107).
- **Comptes d'appels estimés.** Les appels annulés après envoi sont déduits du coût estimé (0,015 $ l'appel). Le compteur « appels annulés » inclut aussi les annulations pendant l'attente avant envoi.
- **Hypothèses non mesurées.** Les gains de latence (D, E, F) et l'effet du message allégé sur la qualité (E) sont des hypothèses. Ils demandent un banc fidèle (B) et quelques appels payants, hors de cette étude.
- **Modifications en cours d'un autre agent** (ThreatAssessment, variantes de banc) : elles ne sont pas évaluées ici, seulement prises en compte dans les pistes A et B.
