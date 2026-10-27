# Agent D — Mesurer objectivement si une variante du conseiller décide mieux

Étude du 26/10/2026, sans aucun appel payant. Données lues en lecture seule dans `%LOCALAPPDATA%\HsCopilot` (rapports, traces, journaux de session) et dans `bench/`. Les comptages viennent de scripts Python (`python -I -X utf8`) et de `hscopilot bench build` / `bench run --dry-run`, écrits et lancés dans le dossier temporaire (voir l'annexe). Les seuils et puissances sont calculés exactement (loi binomiale), pas lus dans une table.

## En bref

1. **Rien de ce qui est mesuré aujourd'hui ne dit si le conseiller décide bien.** La légalité, l'accord avec ton coup (42–44 %), le suivi des conseils (96 %) et les victoires (39 sur 44) décrivent le système ; ce ne sont pas des mesures de qualité.
2. **Le vrai obstacle est statistique.** Deux variantes raisonnables ne choisissent un coup différent que dans 10 à 15 % des positions : 5 sur 36 entre low et medium, 4 sur 36 entre medium et high, 3 sur 30 avec ou sans liste de deck. Et une partie de ces écarts oppose des lignes équivalentes. Sur des positions tirées au hasard, 1 $ (environ 25 positions × 2 variantes) donne **moins de 1 % de puissance**. Avec 50 positions, on monte à peine à 3–13 %.
3. **Seule méthode viable avec ce budget :**
   - enrichir l'échantillon en positions où la variante doit changer la décision ;
   - apparier les comparaisons sur des positions figées ;
   - fixer à l'avance un critère binaire **calculable** ;
   - appliquer un test de signe exact unilatéral, pré-enregistré ;
   - ajouter une garde de non-régression sur des positions calmes ;
   - mesurer le bruit du modèle, c'est-à-dire la même variante relancée sur les mêmes positions.
4. **Le danger adverse ne concerne que 4,5 % des décisions où le modèle est appelé en direct** : 26 sur 572, IC 95 % de 3,1 à 6,6 %. En classé, les positions encore sauvables sont rarissimes : 5 positions à marge ≤ 0 avec une défense simple, réparties sur 3 parties. **Toutes ont servi à concevoir le module danger**, ce sont des données de développement.
5. **Le seul réservoir indépendant pour le danger, ce sont 18 positions** : 12 parties contre l'IA du 22 au 25/10, figées dans `bench/cases.jsonl`, au plus 1 position par tour et 2 par partie.
   - Les journaux d'origine ont disparu : Hearthstone n'a gardé que les dossiers postérieurs au 25/10 à 21 h 07.
   - Ce fichier est donc **la seule copie**, et `bench build` l'écrase par défaut si on oublie `--out`.
6. **Les 8 parties du 26/10 matin n'ont jamais servi à rien.** Elles donnent 75 positions utiles, mais aucune en danger, aucune défaite, et seulement du Standard. Elles servent de réserve pour la non-régression, pas pour valider le danger.
7. **Le banc d'essai n'évalue pas exactement le conseiller en direct.**
   - MVP 3A a été mesuré avant l'ajout de `localAnalysis` (le solveur), qui figure dans chaque appel en direct.
   - La variante `+local` n'a été ajoutée à `bench run` que ce matin.
   - Il manque encore `previousAdvice` et la liste du deck : 3 décisions sur 30 changent avec la liste.
   - La référence d'une comparaison doit donc être `gpt-6.1-sol:medium+local`, pas `gpt-6.1-sol:medium`.
8. **Le bruit du modèle est mal connu.** Mesuré une seule fois, il vaut 1 sur 36, avec un IC 95 % de 0,5 à 14 %. La phrase de MVP3A « plus d'un cas sur 36 n'est pas du bruit » est trop forte : avec un bruit réel de 8 %, on observerait 5 écarts ou plus sur 36 dans 16 % des cas.
9. **Ma recommandation pour 1 $ : une seule comparaison**, A = `medium+local` contre B = A + `danger`, **en deux étapes avec arrêt pour futilité** :
   - **étape 1, environ 0,38 $** : A et A relancé sur 14 positions de danger indépendantes. Si A commet au plus 4 erreurs de survie démontrables, on s'arrête : B ne peut alors pas être démontré meilleur, et on économise environ 0,58 $ ;
   - **étape 2, environ 0,58 $** : B sur ces 14 positions, puis A et B sur 8 positions calmes du 26/10 matin.
10. **Critères de succès, fixés avant toute dépense :**
    - test de signe unilatéral p ≤ 0,05 sur « erreur de survie », soit au moins 5–0, 7–1 ou 9–2 en faveur de B ;
    - bruit de A au plus 2 positions sur 14 sur ce même critère ;
    - 0 action illégale de B ;
    - au plus 2 coups changés sur 8 positions calmes, et aucun avec une erreur démontrable ;
    - coût par appel au plus +15 % ;
    - latence médiane appariée au plus +2 s.
11. **Ce que ce test ne pourra pas conclure :**
    - un effet sur le taux de victoire : il faudrait 126 à 280 parties par bras pour détecter un écart de 9 à 14 points ;
    - une supériorité « en général » ;
    - un petit effet ;
    - une équivalence entre variantes ;
    - quoi que ce soit sur les Découvertes et les mulligans, absents du constructeur de cas.

## 0. Ce que disent les données

### Inventaire

| Élément | Valeur | Source |
|---|---|---|
| Parties classées comptées | 44 (39 V, 5 D) ; 43 avec au moins une décision (38 V, 5 D) | 45 graines classées dans `reports` : celle de 19 h 22, conseils désactivés, n'est pas comptée ; celle du 26/10 à 10 h 33, abandon adverse au tour 0, n'a aucune décision |
| Format | 14 parties en Libre (388 décisions), 29 en Standard (719) | `formatType` des états figés |
| Points de décision figés (tous rangs) | 1 320 extraits, 1 168 après dédoublonnage (graine, tour, bloc d'options), dont 1 107 en classé ; coup réel associé : 1 286 sur 1 320 ; **0 fuite** | `bench build` sur les 60 `*_hdt.log`, `--per-turn 99`, avec un plafond de 40 décisions par fichier : un léger sous-comptage dans 4 parties |
| Classé, par phase | début (tours 1–4) 415, milieu (5–8) 548, fin (9 et plus) 144 ; première décision du tour : 330 | |
| Classé, par résultat | 925 décisions dans des parties gagnées, 182 dans des parties perdues | résultat lu dans les rapports, **pour stratifier seulement** |
| Coups évidents | forcés (une seule action hors fin de tour) : 151 ; létal par les seules attaques : 55 ; décidés par le solveur : 56 dans la trace | |
| Population utile | **572 positions sur 43 parties** : non évidentes, et où le modèle a réellement été appelé en direct (réponse complète ou début de réponse affiché) | appariement trace ↔ état par `stateLogTime` |
| Danger adverse dans la population utile | marge ≤ 10 : 26 positions (4,5 %) ; dans l'ensemble non évident, 44 positions sur 9 parties, dont 34 dans 3 parties seulement | marge = PV + armure − attaque adverse visible (approximation Python) |
| Danger avec une défense simple, classé | marge ≤ 0 : 5 positions (3 parties) ; marge 1 à 5 : 14 (5 parties) ; marge ≤ 0 sans défense simple : 8 (2 parties) | **toutes en développement** (voir 1.4) |
| Danger avec une défense simple, contre l'IA 22–25/10 | 39 positions, 25 tours, 12 parties, hors les 36 cas déjà vus ; **18 avec le plafond** de 1 par tour et 2 par partie | `bench/cases.jsonl`, 2 décisions par tour |
| Réserve du 26/10 matin | 8 parties, 166 décisions, dont 75 utiles ; 0 en danger, 0 Secret, 0 défaite, uniquement du Standard | |
| Découvertes et mulligans | 108 états de Découverte et 46 mulligans analysés en direct ; **0 dans le constructeur de cas**, qui ne lit que les blocs d'options | traces ; vérifié sur les 1 320 cas : aucun choix en attente, aucun mulligan |
| Origine des conseils complets (trace) | suite du plan 329, modèle 234, solveur 86 | 662 entrées « prêt » |
| Début de réponse puis réponse complète | 190 paires, **0 changement d'action** | le début de réponse annonce déjà l'action finale |
| Direct, en classé (compteurs des rapports) | 866 points de décision, dont 270 sans conseil (31 %) ; 837 appels envoyés, 467 annulés, 212 conservés ; 323 suites de plan affichées sans appel ; 69 létaux démontrés affichés sans appel ; suite reçue après le coup joué dans 131 cas, contre 35 réponses sans suite (79 %) | 52 rapports classés |
| Délais en direct (médiane des parties) | appel : P50 8,8 s, P95 16,7 s ; premier fragment : P50 3,6 s ; décision → action affichée : P50 6,1 s, P95 12,7 s | rapports |

### Quatre constats qui changent la façon d'évaluer

1. **Le banc d'essai doit reproduire le conseiller en direct, et il ne le fait qu'en partie.**
   - En direct, `StrategyCoordinator` joint `localAnalysis` (solveur, ordre de jeu, tour suivant) et `previousAdvice` à chaque décision d'action.
   - MVP 3A a été mesuré avant l'existence de `localAnalysis`. Jusqu'à la modification de `BenchRunner.cs` ce matin à 11 h 59 (variantes `+local` et `+danger`), `bench run` ne l'ajoutait pas.
   - `previousAdvice` manque toujours au banc d'essai.
   - La liste du deck manque aussi : les cas construits depuis les sessions portent `DECKLIST_UNKNOWN`, car en direct la liste vient du plugin HDT et `copilot.json` n'en contient aucune.
   - **Conséquence** : les chiffres de MVP3A ne décrivent pas la configuration actuelle. La référence doit être `+local`, avec la liste du deck. Les décisions de début de tour, sans `previousAdvice`, sont les plus fidèles.
2. **Le danger est rare et concentré.**
   - Tiré au hasard dans la population utile, un échantillon de 50 positions ne contiendrait qu'environ 2 positions de danger.
   - Les positions où défendre change l'issue sont encore plus rares : sur les 291 fins de tour du diagnostic, une seule était sauvable (Rexxar).
   - **L'argument actuel en faveur du module danger repose donc sur une position sauvable et un cadeau (Nozdormu), toutes deux de développement.**
3. **Il n'y a pas de réserve « propre » pour le danger en classé.**
   - Le module danger a été calibré sur les 35 parties classées : 291 fins de tour, et les fixtures Rexxar et Tyrande.
   - Les 8 parties du matin n'ont aucune position de danger.
   - Le seul réservoir indépendant est le fichier figé des parties contre l'IA.
4. **Le « changement d'avis » n'existe pas au sein d'un même appel.**
   - Le début de réponse et la réponse complète donnent la même action dans 190 cas sur 190.
   - Le changement d'avis se mesure donc :
     - entre deux appels sur le même état, c'est le bruit (voir 3.5) ;
     - lorsqu'un plan est interrompu par un changement imprévu : 44 annulations de ce type et 35 réponses sans suite en classé.
   - Ces positions sont **à décrire après coup, jamais à utiliser pour sélectionner l'échantillon de test** (voir 1.3).

## 1. Protocole de constitution du jeu de positions

### 1.1 Population cible et unité d'analyse

- **Population cible.** Les décisions d'action pendant ton tour **où le conseiller en direct appelle le modèle**.
  - Le solveur répond seul aux létaux certains : 17 sur 17 justes, IC 95 % de 82 à 100 %. Les évaluer au modèle mesurerait une situation qui n'existe pas en direct.
  - Les suites de plan sont décidées par l'appel précédent : elles sont exclues par défaut.
- **Unité.** Une position figée : état observable, actions légales, contexte local. **Chaque variante est évaluée sur les mêmes positions** : c'est une comparaison appariée et une vraie expérience, puisque seule la variante change.
- **Indépendance.** Au plus **1 position par tour** et **2 par partie**. Deux décisions d'un même tour partagent presque tout leur état, et compter les deux gonflerait artificiellement l'échantillon. Une analyse de sensibilité refait le test avec 1 seule position par partie.
- **Coups évidents exclus**, selon des critères connus avant tout appel :
  - une seule action légale hors fin de tour ;
  - un létal certain du solveur ;
  - un létal par les seules attaques ;
  - un état incomplet.

  Un critère post-hoc comme « les deux variantes sont d'accord » ne peut pas servir d'exclusion : il dépend du résultat.

### 1.2 Règles de gel de l'information

| Règle | Contenu | Existe déjà ? |
|---|---|---|
| G1. État antérieur | l'état est calculé sur les seules lignes antérieures à la décision ; un test rejoue ces lignes et compare | oui (`DatasetBuilder`, test octet par octet) |
| G2. Aucune carte cachée | `PayloadLeakGuard` sur chaque message envoyé : 0 fuite sur les 1 320 cas de session et les 582 cas contre l'IA (essai à blanc du 26/10). **Le même contrôle doit s'appliquer aux fiches des évaluateurs.** | oui pour le modèle, à étendre aux fiches |
| G3. Ni coup réel, ni résultat, ni suite | le coup joué, le résultat de la partie et les tours suivants ne vont jamais au modèle, **ni à l'évaluateur** ; la fiche retire aussi le nom de l'adversaire, l'heure et l'identifiant de partie | modèle : oui ; fenêtre « Banc d'essai » : **non**, elle montre ton coup en vert et le nom des variantes |
| G4. Hypothèses cachées tirées de réservoirs publics | les hypothèses sur la main et les Secrets adverses viennent de la liste des cartes de la classe et du format, **fixée avant l'étude** ; jamais des cartes révélées plus tard dans la même partie | à faire |
| G5. Mémoire de l'évaluateur | tu as joué ces parties : pour chaque fiche, une case « je me souviens de la suite » ; une position cochée est exclue de ton jugement | à faire |
| G6. Gel des versions | empreinte SHA-256 du fichier de cas, des instructions et de la stratégie (déjà dans `run.json`), **et des binaires** `hscopilot.exe` et `HsCopilot.Core.dll`, puisque le dossier n'est pas sous git ; le tout dans un fichier de pré-enregistrement daté, écrit **avant le premier appel payant** | en partie |
| G7. Pas de nouvelle sélection, pas de relance de chance | aucune position ajoutée ou retirée après avoir vu des réponses ; seules les erreurs techniques sont relancées (`--retry-errors`) ; une réponse ne se « retente » jamais | à appliquer |
| G8. Protéger les archives | copier `bench/cases.jsonl` et `bench/cases-deck.jsonl` sous un autre nom, avec leur empreinte, **avant tout `bench build`** : sans `--out`, la commande écrase `bench/cases.jsonl`, seule trace restante des parties du 22 au 25/10 | à faire |
| G9. Base de cartes en lecture seule | passer `--cards <cache existant>` ; sinon un changement de build HDT réécrit le cache dans `%LOCALAPPDATA%\HsCopilot\carddb` | à appliquer |

### 1.3 Strates et effectifs disponibles

Une position peut appartenir à plusieurs strates. Les quotas sont des **minimums**, pas des partitions. Les effectifs portent sur les positions non évidentes.

| Strate | Définition calculable avant tout appel | Développement (classé, 25/10 19 h 26 → 26/10 2 h 36) | Réserve A (26/10 matin) | Réserve B (contre l'IA, 22–25/10) | Rôle |
|---|---|---|---|---|---|
| Victoires / défaites | résultat du rapport, pour **stratifier seulement** | 725 / 165 positions (38 / 5 parties) | 75 / 0 | inconnu, sans intérêt | équilibrer ; jamais une étiquette |
| Standard / Libre | `formatType` | 560 / 330 | 75 / 0 | contre l'IA | rapporter à part |
| Début / milieu / fin | tour 1–4 / 5–8 / 9 et plus | 320 / 441 / 129 | 44 / 30 / 1 | disponible | quotas : environ 40 / 45 / 15 %, comme en direct (41 / 46 / 13 %) |
| Létal conditionnel ou plausible | niveau du solveur ≠ certain et ≠ aucun ; le niveau certain est exclu (solveur) | 7 « létal avec main » | rare | à compter par essai à blanc | vérifier que la variante ne rate pas un létal |
| Pioche ou Découverte jouable | une carte jouable dont le texte contient « Pioche » ou « Découvre » | 327 (40 parties) | 42 | disponible | ordre de jeu « piocher avant de s'engager » |
| Plusieurs coups raisonnables | au moins 6 actions légales et au moins 3 sources distinctes | 514 | 43 | disponible | positions où une variante peut changer quelque chose |
| Danger adverse | `ThreatAssessment` : niveau « létal visible » ou « danger élevé » (marge ≤ 5), au moins une défense, et pas de létal certain pour toi | 19 (5 parties) | 0 | **39, soit 18 avec le plafond (12 parties)** | strate principale de la comparaison danger |
| Secret adverse | `opponent.secrets` non vide | 51 (4 parties) | 0 | à compter | robustesse aux hypothèses cachées |
| Course au létal | létal par attaques **et** marge ≤ 10 | 4 | 0 | 18 | la variante danger ne doit pas faire oublier le létal ; si le solveur le dit certain, il passe sans appel en direct |
| Découvertes, mulligans | — | 108 / 46 en direct | — | — | **hors banc** : il faut étendre le constructeur aux `EntityChoices` |
| Changement d'avis | A ≠ A relancé (bruit) ; plan interrompu par un changement imprévu | décrit après coup | — | — | **à décrire, jamais à sélectionner** |

**Pourquoi ne pas sélectionner sur le changement d'avis ou le désaccord.** Si l'on choisit les positions où la variante A s'est montrée instable, ou en désaccord avec toi, A y est mécaniquement désavantagée : on a retenu ses mauvais tirages, et une relance tendrait vers la moyenne. B, elle, n'a pas subi cette sélection. La comparaison serait biaisée en faveur de n'importe quelle variante différente de A.

**Ne pas surreprésenter les coups évidents, ni fausser le global.**
- Le banc enrichit volontairement la strate danger, qui représente 4,5 % des décisions en direct.
- Toute conclusion « globale » doit donc **repondérer chaque strate par sa fréquence en direct** (post-stratification), ou être formulée par strate.
- Exemple : « B corrige 6 positions de danger sur 14 » n'est pas « B est meilleur dans 43 % des cas ».

### 1.4 Séparation développement / validation

| Données | Ce qui en a été tiré | Statut |
|---|---|---|
| 36 cas de MVP3A, plus les 8 cas divergents et les 30 cas avec la liste du deck | choix de l'effort, relus à la main | **développement** ; exclus des réserves |
| Parties contre l'IA du 25/10 (15 h 44, 16 h 37, 18 h 34) | TurnSolver, ordre des Profondeurs, Drake terrestre, repères « vérifié » du fichier de stratégie | développement |
| 35 parties classées du 25/10 à 19 h 26 au 26/10 à 2 h 36 | borne des Provocations (parties de 0 h 09 et de 23 h 26), module danger (291 fins de tour, Rexxar et Tyrande devenus des fixtures), audits de plan | **développement**, y compris les 5 défaites |
| 8 parties classées du 26/10, de 10 h 34 à 11 h 41 | rien | **réserve A** : non-régression en positions calmes |
| `bench/cases.jsonl` hors les 36 cas vus : 546 positions, 30 parties, dont 28 contre l'IA | rien pour le module danger | **réserve B** : danger. Réserves : adversaires IA ; deck différent le 22/10 ; états figés avec le code du 25/10 à 14 h 10. L'essai à blanc charge les 582 cas sans fuite. |
| Parties futures | — | **validation prospective** : variante et protocole gelés **avant** de jouer |

**Règles.**
- Une variante modifiée après avoir vu ses résultats sur une réserve doit être réévaluée sur des positions neuves : la réserve consommée devient du développement.
- Les cas de développement, comme Rexxar ou Tyrande, servent de **tests unitaires** (« le correctif fait ce qu'il doit sur le cas qui l'a motivé »). Ils ne sont jamais une preuve d'amélioration.

### 1.5 Sélection déterministe

1. Copier les archives et noter leurs empreintes (G8).
2. Construire les cas avec la liste du deck, sans écraser l'archive. Par exemple, une configuration temporaire qui contient la liste « Guerrier dragons » :
   `bench build <logs> --deck "Guerrier dragons" --config %TEMP%\cfg.json --cards <cache> --out bench\eval\pool.jsonl`.
3. Faire un essai à blanc avec la variante B et `--dump` : on obtient `localAnalysis.opponentThreat`, avec la marge exacte et les défenses, pour chaque position, sans aucun appel. Mes comptages Python ne sont qu'une approximation de `ThreatAssessment`.
4. Appliquer les critères de strate du 1.3, puis ordonner par empreinte de l'identifiant de cas, comme le fait déjà `DatasetBuilder.Select`. Appliquer les plafonds de 1 par tour et 2 par partie, et retenir les N premiers.
5. Écrire le pré-enregistrement : identifiants, variantes, critères, seuils, budget par étape, règle de décision, empreintes. Le sceller par son SHA-256 avant le premier appel.

## 2. Métriques

### 2.1 Erreurs tactiques démontrables, vérifiables par calcul local

Une erreur est **démontrable** si un calcul sur l'information publique la constate sans jugement : même entrée, même verdict. Chaque détecteur a ses faux positifs. Par exemple, le détecteur actuel a compté une Frappe héroïque comme un létal ignoré. Chaque signalement est donc **revu à l'aveugle**, sans savoir quelle variante l'a produit, et on publie le taux de faux positifs du détecteur.

| Code | Définition | Vérification | Existe ? |
|---|---|---|---|
| E1 Action illégale | `actionId` hors des actions légales | validateur | oui (0 sur 180 pour gpt-6.1-sol en MVP3A) |
| E2 Létal certain ignoré | le solveur dit « certain » et l'action n'est pas une première étape d'une séquence létale | `TurnSolver` | oui ; en direct, le solveur répond seul |
| E3 **Erreur de survie** | marge ≤ 0 (létal adverse visible), au moins une ligne défensive ramène la marge au-dessus de 0, pas de létal certain pour toi, **et** la ligne proposée laisse une marge ≤ 0 | appliquer la première action et les étapes structurées de la suite (identifiants d'entité), avec une simulation déterministe des attaques, de l'armure et des Provocations, puis recalculer `ThreatAssessment`. Si la suite n'est pas simulable, on juge la première action seule : erreur si elle supprime toute défense qui existait avant | **à écrire** (environ 150 lignes, tests sur Rexxar), ou calcul à la main par deux évaluateurs |
| E4 Cadeau chiffré | carte qui profite aussi à l'adversaire (Nozdormu : cristaux à 10), jouée alors que l'adversaire a beaucoup de cartes en main et que le gain ne change rien à ton tour | `ThreatAssessment.Gifts` | en partie ; « à vérifier », pas automatique |
| E5 Échange perdant | ton serviteur meurt sans tuer sa cible, sans effet compensateur connu | `DubiousMoveDetector` | oui |
| E6 Profondeurs sur un serviteur adverse qui survit | +2 ATQ offerts à l'adversaire | idem | oui |
| E7 Bonus d'ordre perdu | attaquer avec un serviteur que les Profondeurs auraient renforcé d'abord | `localAnalysis.ordering` | oui (solveur) |
| E8 Fin de tour prématurée | fin du tour avec au moins 2 cristaux et une carte jouable sans effet négatif | détecteur | oui |
| E9 Fait faux dans le texte | chiffre, mot-clé ou mécanique contredit par l'état : Bouclier divin ignoré, Ruée vers le héros, statistiques | expressions régulières sur les chiffres et mots-clés, plus relecture | en partie (8 chiffres vérifiés en PvP 2) |
| E10 Risque de Secret évitable | héros à au plus 2 PV, Secret de Chasseur inconnu, attaque sur le héros alors qu'une ligne sans attaque sur le héros atteint le même objectif | règle et liste des Secrets du format | à écrire |

**Limite : ces erreurs sont rares.** gpt-6.1-sol n'a commis aucune erreur « douteuse » sur 180 appels en MVP3A. Pour un taux de 0 à 3 %, une variante en commettra 0 à 1 sur 25 positions. Ces erreurs servent de **gardes**, avec 0 erreur tolérée pour E1 et E2, et non de critère discriminant. **Sauf dans une strate ciblée** : c'est pourquoi le critère principal de la recommandation est E3, mesuré uniquement sur des positions de danger.

### 2.2 Comparaisons à l'aveugle

**Quand.** Uniquement sur les positions **discordantes**, celles où A et B ne choisissent pas la même première action, et où aucune erreur démontrable ne tranche. Sur les autres, le jugement est inutile.

**Fiche d'évaluation.**
- État observable rendu comme dans `bench show`, sans le nom des variantes, sans la marque « JOUÉ », sans l'heure ni l'adversaire nommé.
- Les deux lignes (première action et étapes du plan), libellées de façon uniforme, dans un ordre gauche/droite tiré au hasard avec une graine notée.
- **Le raisonnement du modèle est retiré**, ou ramené à la même longueur des deux côtés.
- Les faits calculés pour chaque ligne sont fournis (voir 2.3).
- Réponses possibles : A meilleure, B meilleure, équivalentes, ne sais pas ; plus une case « erreur démontrable ? ».

**Évaluateurs distincts.**
1. **Toi.** Tu es l'expert du deck, mais tu n'es pas neutre : tu as joué ces parties, tu as vu les conseils en direct et tu as validé les règles. D'où G5, et la priorité donnée aux positions de la réserve B, plus anciennes, pour ton jugement.
2. **Un second joueur, si possible.** C'est la seule vraie mesure d'accord entre juges (kappa de Cohen).
3. **Un évaluateur LLM d'une autre famille que le conseiller**, par exemple un agent Claude neuf, **sans accès au dépôt ni aux documents**. Il ne coûte rien sur le budget OpenAI de 1 $. Un juge OpenAI noterait sa propre famille.

**Biais d'un évaluateur LLM, et parades.**

| Biais | Parade |
|---|---|
| Préférence pour sa propre famille ou son propre style | juge d'une autre famille ; jamais le modèle et le prompt du conseiller |
| Position : il préfère la ligne présentée en premier | chaque paire jugée **dans les deux ordres** ; seuls les verdicts cohérents comptent, le reste vaut « équivalentes » ; le taux d'incohérence est publié |
| Verbosité et éloquence | pas de raisonnement du modèle ; lignes et faits calculés seulement |
| Ancrage | pas de coup réel, pas de résultat, pas de « confiance haute » |
| Barème qui reprend les priorités de B | le barème ne doit pas recopier les consignes ajoutées à la variante, sinon le juge récompense la conformité au texte plutôt que la qualité du coup ; il reste générique : survie, létal, valeur |
| Mauvaise lecture des cartes | textes des cartes joints ; faits de combat calculés et fournis |
| Instabilité | 20 % des paires rejugées plus tard ; accord test-retest publié |
| Juge non calibré | **jeu d'étalonnage** de 8 à 10 positions où la réponse est démontrable (létal certain, létal adverse avec une défense unique, échange perdant, Ruée vers le héros) ; on exige au moins 90 % de réussite avant d'utiliser le juge ; on publie son accord avec toi sur les mêmes paires |

**L'accord entre deux modèles n'est pas une vérité.** Ni l'accord entre luna et sol, ni celui entre deux efforts, ni celui entre le juge et le conseiller. Un juge LLM est un **instrument d'opinion**. Sa validité se mesure par l'étalonnage et par l'accord avec des juges humains, et il ne remplace jamais une erreur démontrable.

### 2.3 Analyse des lignes alternatives

Pour chaque position discordante, on calcule localement, sans appel, un **vecteur de conséquences** pour chaque ligne, sur les seules mécaniques simulées :
- dégâts au visage ;
- niveau de létal après la ligne ;
- marge face au danger adverse après la ligne ;
- serviteurs et statistiques des deux camps après les échanges ;
- cristaux et cartes dépensés ;
- ressources clés gardées : lieu prêt, Grommash, Parenté ;
- risques signalés : Secret, cadeau.

Si une ligne est au moins aussi bonne sur toutes les dimensions et strictement meilleure sur une, elle **domine** : la différence est démontrable. Sinon, les lignes sont « non comparables » et passent au jugement à l'aveugle, avec ce vecteur sur la fiche. Le calcul ne tient jamais compte de ce qui s'est passé ensuite dans la vraie partie : on n'observe que la conséquence du coup réellement joué, jamais celle de l'autre ligne.

### 2.4 Robustesse à plusieurs hypothèses cachées

Une ligne est jugée sous une liste fixe d'hypothèses, **écrite avant l'étude** à partir d'informations publiques. Pour la main adverse, on n'utilise que le nombre de cartes, les cristaux au tour suivant et la classe ; jamais les cartes réellement jouées ensuite.

| Hypothèse | Contenu |
|---|---|
| H0 | rien de caché ne compte (plateau seul) |
| H1 | dégâts directs depuis la main, au niveau typique de la classe et du format (liste fixée par classe) |
| H2 | le Secret inconnu est l'un des Secrets de la classe dans le format (réservoir de la base de cartes) ; chacun est testé |
| H3 | « combo » : au moins 8 cartes en main et au moins 10 cristaux, donc une marge de sécurité exigée |
| H4 | ta pioche : chaque carte restante de **ta** liste de deck, pondérée par `DeckOdds` (information légitime pour toi) |

Score : la ligne « survit » ou « garde son létal » sous k hypothèses sur K. On rapporte la part de positions où B est plus robuste que A, et l'inverse. C'est un critère **secondaire**, sans probabilité affirmée.

### 2.5 Suites de plan utilisables

- **En direct (traces).** Suite reçue après le coup joué dans 131 cas contre 35 réponses sans suite, soit 79 % ; attente « suite du plan attendue » terminée par la suite dans 82 cas sur 99 ; 323 étapes affichées sans appel.
- **Sur le banc d'essai.**
  - Part des réponses dont les étapes structurées désignent des entités présentes et des actions possibles. Ce calcul est gratuit.
  - Exécutabilité, mesurée seulement là où **A et B proposent la même première action, identique au coup réellement joué** : l'état suivant du journal existe alors, et l'on vérifie si l'étape 2 y est légale.
  - Cette restriction est obligatoire. Sinon, seules les variantes d'accord avec ton coup seraient évaluables, ce qui est une sélection.
  - Utiliser l'état suivant ne sert ici qu'à vérifier l'exécutabilité, jamais à choisir un coup.

### 2.6 Délais

- **Ce qui compte en direct, c'est le délai jusqu'à l'action**, c'est-à-dire le début de réponse : tu joues souvent dessus, et il annonce toujours l'action finale (190 sur 190).
- Le banc d'essai appelle sans flux (`ProposeWithPayloadAsync`) et ne mesure que la réponse complète. Pour une comparaison, ce n'est qu'une approximation acceptable.
- Différence de latence **appariée**, par la médiane des différences, sur les appels non servis par le cache ; même concurrence, variantes entrelacées.
- Avec environ 20 paires et un écart-type des différences d'environ 4 s, on détecte une variation d'environ 2,5 s. Il faut aussi rapporter la part de réponses en 20 s ou moins.

### 2.7 Coût

- Le surcoût d'entrée d'une variante se **calcule avant toute dépense**, à partir de la taille des messages de l'essai à blanc (environ 3,2 caractères par jeton d'après MVP3A, au tarif de 2 $ le million de jetons d'entrée).
- Ensuite, on mesure le coût réel et les jetons de sortie et de raisonnement.
- On distingue les appels pleins des appels servis par le cache : un message identique coûte 0,006 $ au lieu de 0,017 $.

### 2.8 Ce qui n'est pas une mesure de qualité

| Indicateur | Pourquoi ce n'en est pas une |
|---|---|
| Accord avec ton coup | ton coup n'est pas une vérité de référence ; tu improvises en fin de tour ; contre l'IA, tu laissais l'adversaire en vie volontairement |
| Victoires (39 sur 44, IC 95 % de 76 à 95 %) | pas de groupe témoin ; adversaires, rang et tirages non contrôlés ; tu joues aussi ; 5 défaites seulement |
| Conseils suivis (96 %) | suivre n'est pas valider |
| Coup suivi d'une victoire | causalité inversée et biais du survivant : on ne connaît pas l'issue de l'autre ligne |
| Accord entre variantes ou entre modèles | il mesure la stabilité, pas la qualité |
| Alarme danger corrélée aux défaites (4 sur 4) | 3 des 4 positions étaient déjà perdues ; la corrélation ne montre pas que défendre aurait sauvé la partie |

## 3. Puissance statistique

### 3.1 Le bon test

- Les positions sont appariées. Pour chaque position, on a un résultat binaire par variante, par exemple « erreur de survie oui ou non ».
- Seules comptent les positions **discordantes** : b, où A est mauvaise et B bonne, et c, l'inverse.
- Le **test de McNemar exact**, c'est-à-dire le test de signe sur b contre c, est le bon test.
- Pour la comparaison « B meilleure ou A meilleure » des juges, on garde le même test et l'on retire les « équivalentes ».
- On rapporte b, c, le p exact et un IC de Wilson sur la part θ = b / (b + c).

Deux paramètres gouvernent la puissance :
- **pd**, la part de positions discordantes sur le critère ;
- **θ**, la part des discordances en faveur de B.

L'écart de taux d'erreur entre les deux variantes vaut pd × (2θ − 1).

### 3.2 Seuils exacts du test de signe (bilatéral 5 %)

| Discordances | 5 | 6 | 7 | 8 | 9 | 10 | 12 | 15 | 20 | 25 |
|---|---|---|---|---|---|---|---|---|---|---|
| Minimum en faveur de B | impossible | 6–0 | 7–0 | 8–0 | 8–1 | 9–1 | 10–2 | 12–3 | 15–5 | 18–7 |
| En unilatéral 5 %, pré-enregistré | 5–0 | 6–0 | 7–0 | 7–1 | 8–1 | 9–1 | 10–2 | 12–3 | 15–5 | 18–7 |

Avec 4 discordances ou moins, **aucun résultat n'est significatif**, même 4–0 (p = 0,125 en bilatéral, 0,0625 en unilatéral).

### 3.3 Puissance selon le nombre de positions (bilatéral 5 %, 2 variantes)

| N positions | pd | θ = 0,8 | θ = 0,9 | θ = 1,0 | Écart de taux d'erreur (θ = 0,9) |
|---|---|---|---|---|---|
| 15 | 0,2 | 0,02 | 0,03 | 0,06 | 0,16 |
| 15 | 0,3 | 0,07 | 0,14 | 0,28 | 0,24 |
| 15 | 0,5 | 0,24 | 0,49 | 0,85 | 0,40 |
| 25 | 0,2 | 0,10 | 0,20 | 0,38 | 0,16 |
| 25 | 0,3 | 0,24 | 0,48 | 0,81 | 0,24 |
| 25 | 0,5 | 0,48 | 0,82 | 1,00 | 0,40 |
| 50 | 0,2 | 0,36 | 0,68 | 0,95 | 0,16 |
| 50 | 0,3 | 0,58 | 0,89 | 1,00 | 0,24 |
| 50 | 0,5 | 0,84 | 0,99 | 1,00 | 0,40 |

**Écart minimal détectable à 80 % de puissance :**
- 15 positions : 48 points d'écart de taux d'erreur, à condition que toutes les discordances aillent dans le même sens ;
- 25 positions : 30 points ;
- 50 positions : 16 points ;
- 100 positions : 8 points.

**Sur des positions tirées au hasard**, pd « de qualité » vaut environ 0,06 à 0,08. En MVP3A, sur les 8 cas où les efforts divergeaient, 5 ou 6 ont été jugés équivalents. Avec pd = 0,07, la puissance est de 0,000 pour 15 positions, 0,002 à 0,007 pour 25, et 0,03 à 0,13 pour 50.

**Positions ciblées, test unilatéral pré-enregistré :**

| N | pd = 0,3 | pd = 0,4 | pd = 0,5 | pd = 0,6 |
|---|---|---|---|---|
| 12 (θ = 1) | 0,28 | 0,56 | 0,81 | 0,94 |
| 14 (θ = 1) | 0,42 | 0,72 | 0,91 | 0,98 |
| 14 (θ = 0,9) | 0,24 | 0,43 | 0,58 | 0,70 |
| 16 (θ = 1) | 0,55 | 0,83 | 0,96 | 1,00 |

### 3.4 Avec 3 variantes

- Il faut corriger pour les comparaisons multiples (Bonferroni) : 2,5 % pour deux comparaisons à la référence, 1,67 % pour les trois paires.
- Avec 15 positions, et pd = 0,3 et θ = 0,9, la puissance tombe à **0,06**. Avec 25 positions, à 0,30–0,34.
- Avec 1 $, trois variantes, c'est 15 positions par variante. **Je le déconseille** : on paierait pour ne rien pouvoir conclure.

### 3.5 Intervalles de Wilson (95 %), pour lire les chiffres existants

| Observé | IC 95 % | Ce que cela veut dire |
|---|---|---|
| 0 sur 12 | 0 – 24 % | « aucune erreur sur 12 » est compatible avec un taux de 24 % |
| 0 sur 25 | 0 – 13 % | |
| 0 sur 50 | 0 – 7 % | même 50 positions propres ne garantissent pas moins de 7 % |
| 1 sur 36 (bruit, medium relancé) | 0,5 – 14 % | bruit très mal connu |
| 5 sur 36 (low contre medium) | 6 – 29 % | |
| 9 sur 9 létaux engagés | 70 – 100 % | |
| 17 sur 17 létaux certains justes | 82 – 100 % | |
| 39 sur 44 victoires | 76 – 95 % | |
| 26 positions de danger sur 572 | 3,1 – 6,6 % | |

### 3.6 Le bruit propre du modèle

**Mesures disponibles (MVP3A).**
- Même variante medium relancée sur les mêmes 36 messages : **1 action différente sur 36**, IC 0,5 à 14 %.
- C'est environ 1,4 % par appel si les deux tirages sont indépendants.
- La relance a profité d'un cache presque complet (8 136 jetons sur 8 253), pour 0,006 $ par appel. Le cache ne change pas le tirage ; c'est une hypothèse raisonnable, à confirmer.

**Pourquoi il faut le connaître.**
1. **Fixer la base.** Sans aucun effet de la variante, deux appels diffèrent déjà dans environ 2ε des positions, où ε est le taux d'instabilité par appel.
   - Si ε = 1,4 %, observer au moins 3 changements sur 8 positions calmes arrive 0,1 % du temps : c'est un vrai changement de comportement.
   - Si ε = 7 %, cela arrive 9 % du temps : c'est peut-être du bruit.
2. **Le bruit dilue la puissance.** Les discordances dues au hasard vont dans les deux sens, θ ≈ 0,5 : plus de discordances, mais moins nettes.
3. **Il dépend de la position.** Il se concentre dans les positions à plusieurs coups raisonnables, exactement celles qu'on veut tester. Il faut donc le mesurer **dans la strate testée**, pas en moyenne.
4. **C'est un défaut en soi.** En direct, un appel relancé après une annulation peut afficher un autre conseil pour le même état.
5. **Il s'applique aussi aux juges**, d'où la règle de rejuger 20 % des paires (2.2).

**Correction d'une affirmation de MVP3A.** « Une différence entre niveaux d'effort supérieure à 1 cas sur 36 n'est pas du bruit » vaut seulement si le bruit réel est d'environ 3 % : P(au moins 5 sur 36) = 0,003. Si le bruit réel est de 8 %, ce qui reste dans l'IC, cette probabilité monte à 0,16 ; à 14 %, à 0,58.

**En pratique :**
- relancer A une fois sur **toutes** les positions de la strate principale ; c'est presque gratuit, environ 0,007 $ par appel grâce au cache, à condition de relancer dans les minutes qui suivent ;
- compter le bruit **sur le critère principal**, et pas seulement sur l'action ;
- l'analyse de sensibilité ne compte une erreur de A que si elle se reproduit dans les deux tirages.

### 3.7 Ce que 1 $ permet de conclure, et ce qu'il ne permet pas

**Ce qu'on peut conclure, si les critères pré-enregistrés sont atteints :**
- un **effet causal de la variante sur la décision**, dans une strate ciblée, si l'effet est grand. Par exemple : « dans les positions de létal adverse visible avec une défense, B évite l'erreur de survie là où A la commet, au moins 5–0 » ;
- l'**absence d'une régression grossière** hors danger : par exemple, au plus 2 changements sur 8, sans erreur démontrable. On n'exclut pas une régression modérée ;
- l'écart de **coût**, qui est déterministe, et un écart de **latence** d'environ 2 à 3 s ;
- le **bruit** de la référence dans la strate testée.

**Ce qu'on ne peut pas conclure :**
- **Un effet sur le taux de victoire.** Il faudrait environ 280 parties par bras pour distinguer 88,6 % de 80 %, et 126 pour 88,6 % contre 75 %, en tirage au sort des parties. C'est hors de portée et sans rapport avec le budget API.
- Que B soit meilleure **en général**, ni « optimale ».
- Un effet de moins d'environ 30 points dans la strate ciblée, ou de moins d'environ 15 points en moyenne.
- **L'équivalence** des variantes : ne pas détecter de différence ne prouve pas qu'il n'y en a pas.
- Une généralisation à d'autres decks, à d'autres versions du modèle, aux Découvertes et aux mulligans.
- Les effets propres au direct : les annulations, le moment de l'affichage, ta réaction au conseil.
- Quoi que ce soit à partir des cas de développement : Rexxar et Tyrande valident le correctif sur sa propre cause.
- Une preuve à partir du « bilan 39–5 » : le conseiller n'en est pas la cause démontrée.

## 4. Recommandation concrète pour ce projet

### 4.0 Ce qui se fait gratuitement, avant de dépenser

1. **Le module danger local**, c'est-à-dire l'alerte affichée sans modèle, s'évalue **sans appel**.
   - Sur la réserve B et sur la réserve A, calculer la précision et le rappel de l'alerte, et les faux positifs sur les positions calmes.
   - Contrôler le calcul : comparer les dégâts visibles à la fin de ton tour avec les attaques réellement disponibles au tour adverse. C'est une vérification du calcul, pas du coup.
   - En développement, le résultat est de 4 alarmes sur 4 suivies de défaites et de 0 fausse alarme sur 291 fins de tour. Mais une seule position était sauvable : l'utilité est démontrée sur **un** cas.
2. **L'écart de coût** de `+danger` se lit dans les messages de l'essai à blanc.
3. **La sélection** des positions et leur marge exacte viennent de l'essai à blanc avec `--dump`.
4. **Le détecteur d'erreur de survie (E3)** est à écrire et à tester sur les cas de développement, Rexxar en tête, avant toute dépense.
5. **La sauvegarde** des archives (G8) et le **pré-enregistrement** scellé (G6).

### 4.1 La question et les variantes

**Question.** Le champ `opponentThreat`, avec sa consigne, améliore-t-il les décisions du modèle quand un létal adverse est visible, sans dégrader les autres décisions ?

**Variantes.**
- **A** = `gpt-6.1-sol:medium+local`, la référence fidèle au direct.
- **B** = `gpt-6.1-sol:medium+local+danger`, tel que codé ce matin dans `BenchRunner`.
- **A#2** = A relancé, pour le bruit.

**Point de vigilance.** Dans la version lue à 11 h 58, `ThreatAssessment.Payload` joint le champ à **toutes** les décisions d'action, même au niveau « faible ». B diffère donc de A partout, et la non-régression hors danger doit être **mesurée**, pas supposée.

### 4.2 Plan de dépense en deux étapes, avec arrêt pour futilité

Coûts estimés à environ 0,020 $ par appel plein sur la réserve B (messages d'environ 21 000 caractères, plus `localAnalysis`), environ 0,017 $ sur la réserve A, environ 0,007 $ par appel servi par le cache. **Il faut recalculer depuis l'essai à blanc** avant de lancer.

| Étape | Positions | Appels | Coût estimé | Ce qu'elle mesure |
|---|---|---|---|---|
| 1a | 14 positions de danger de la réserve B : marge ≤ 5 selon `ThreatAssessment`, au moins une défense, pas de létal certain, plafonds de 1 par tour et 2 par partie, au moins 6 positions à marge ≤ 0 | A × 14 | 0,28 $ | taux d'erreurs de survie de A |
| 1b | les mêmes 14, **lancées juste après 1a**, pour profiter du cache | A#2 × 14 | 0,10 $ | bruit sur le critère principal |
| **Porte de futilité** | si A commet **au plus 4** erreurs de survie sur 14, **arrêt** : même un 4–0 ne serait pas significatif (p = 0,0625 en unilatéral). Conclusion : le besoin côté modèle n'est pas démontré ; on garde l'alerte locale, gratuite. **Environ 0,58 $ économisés.** | | | |
| 2a | contrôle positif de développement : la position Rexxar, dernier tour de la partie de 0 h 21 | B × 1 | 0,02 $ | si B ne défend pas sur son propre cas d'origine, arrêt |
| 2b | les 14 positions de l'étape 1 | B × 14 | 0,29 $ | critère principal |
| 2c | 8 positions calmes de la réserve A : 3 en début, 4 en milieu et 1 en fin de partie, au moins 6 actions légales, au plus 2 par partie, ordre par empreinte | A × 8 + B × 8 | 0,27 $ | non-régression |
| **Total** | | 59 appels, dont 14 servis par le cache | **environ 0,96 $** | |

**Contraintes d'exécution.**
- Plafond `--budget` par étape, et un dossier par étape.
- Lancer 2c en dernier : si le coût réel dépasse l'estimation, c'est elle qui est tronquée.

**Piège du plafond actuel.**
- Tant qu'aucun coût n'est mesuré pour une variante, chaque appel réserve 0,10 $. Avec `--concurrency 4`, quatre appels réservent donc 0,40 $.
- Si le plafond de l'étape est plus bas, la boucle s'arrête **définitivement** (`break`) au lieu d'attendre.
- Il faut donc un plafond d'au moins 0,45 $ par étape, ou bien `--concurrency 1`.

### 4.3 Critères de succès, fixés à l'avance

| Critère | Seuil | Si échec |
|---|---|---|
| **P (principal)** : erreur de survie démontrable (E3), A contre B sur les 14 positions | test de signe exact **unilatéral** p ≤ 0,05 : au moins 5–0, 7–1 ou 9–2 en faveur de B | voir 4.4 |
| P' (sensibilité) | le même test, en ne comptant une erreur de A que si elle se reproduit dans A#2 | rapporté ; s'il contredit P, la conclusion est « fragile » |
| Bruit | A et A#2 diffèrent sur E3 dans au plus 2 positions sur 14 | au-delà, résultat **non interprétable** ; il faut plus de relances, pas une conclusion |
| G1 | 0 action illégale de B sur tous ses appels | rejet de B |
| G2 | sur les 8 positions calmes, B change la première action de A au plus 2 fois. Si B la change dans 3 positions ou plus, les positions changées passent au jugement à l'aveugle ; B est rejetée dès qu'une seule présente une erreur démontrable de B, ou est jugée « B moins bonne » par toi **et** par le second évaluateur | rejet de B, ou révision **testée sur des positions neuves** |
| G3 | surcoût d'entrée de B au plus 15 % par appel | à arbitrer |
| G4 | médiane des différences de latence appariées au plus +2 s | à arbitrer |
| Hors critère | E2, E5 à E10, robustesse et exécutabilité du plan : rapportés à titre **descriptif**, sans test | — |

**Règle de décision.**
- **Adopter le champ danger dans le message en direct** seulement si P, le bruit, G1, G2, G3 et G4 sont tous satisfaits.
- Un seul critère principal, donc pas de correction pour comparaisons multiples. Tout le reste est descriptif.

### 4.4 Interprétations écrites d'avance

| Issue | Conclusion autorisée |
|---|---|
| Futilité : A commet au plus 4 erreurs sur 14 | « Sur 14 positions indépendantes de danger, la référence évite déjà l'erreur de survie dans au moins 10 cas. Le besoin du champ côté modèle n'est pas démontré. » On garde l'alerte locale, dont l'intérêt ne dépend pas du modèle, puisque Rexxar est mort faute de conseil à temps. On revalide sur les parties futures. |
| P atteint et gardes satisfaites | « Dans les positions de létal adverse visible avec une défense, B évite l'erreur de survie là où A la commet (b–c, p = …). » **Pas** : « B fait gagner plus de parties ». |
| P non atteint, b > c | « Signal favorable, non démontré. » Adopter B pour d'autres raisons reste possible, en le documentant comme non validé. **Il est interdit d'ajouter des positions ou de relancer pour atteindre le seuil.** |
| c ≥ b | B n'apporte rien sur ces positions ; on cherche pourquoi sur les cas de développement, puis on refait le test sur des positions neuves. |
| Échec de G2 | B rend le modèle plus défensif hors danger. Piste : n'envoyer le champ qu'au-dessus d'un seuil de danger, puis retester sur des positions neuves, la réserve A étant consommée. |

### 4.5 Ensuite, hors de ce budget

1. **Validation prospective.** Geler la variante adoptée et ce protocole, puis jouer. Toutes les positions de danger des parties futures forment l'échantillon de confirmation. Après les plafonds, on compte 8 positions de danger avec défense dans les 43 parties classées, soit environ 0,2 par partie : il faut **de l'ordre de 70 parties** pour en réunir 14.
2. **Un banc de référence de 50 positions**, pour les prochaines variantes. Il se tire des réserves et des parties futures, avec les quotas du 1.3 et la repondération par strate. On le garde comme **suite de non-régression** : un banc réutilisé finit par servir à régler les variantes, et il faut renouveler l'échantillon de confirmation à chaque nouvelle variante.
3. **Étendre le constructeur aux Découvertes et aux mulligans** (`EntityChoices`) : 108 et 46 décisions réelles aujourd'hui hors banc.
4. **Ajouter un mode aveugle** à la fenêtre « Banc d'essai » (variantes masquées, coup réel masqué, ordre tiré au hasard), ou un export de fiches. Ajouter `previousAdvice` et la liste du deck au banc pour qu'il ressemble au direct.

## Annexe : reproduction

Toutes les commandes ont tourné en lecture seule sur `%LOCALAPPDATA%\HsCopilot`, avec la clé OpenAI retirée de l'environnement (`env -u OPENAI_API_KEY`) pour l'essai à blanc. Aucun fichier du dépôt n'a été modifié, à part ce rapport.

- Construction des cas : `hscopilot bench build <60 journaux *_hdt.log> --cards %LOCALAPPDATA%\HsCopilot\carddb\cards.255130.frFR.json --per-turn 99 --out %TEMP%\…\agentD\cases-sessions-all.jsonl`. Résultat : 1 320 cas, 0 fuite. La même commande avec 2 décisions par tour donne 750 cas.
- Essai à blanc : `bench run --dry-run` sur les 582 cas de `bench/cases.jsonl` et sur les 1 320 cas de session : 0 fuite, aucun appel.
- Scripts, dans `C:\Users\thoma\AppData\Local\Temp\claude\c--Users-thoma-Desktop-HSguide\f24c0e17-f6bd-4886-8fec-932936373eb9\scratchpad\agentD\` :
  - `inv_reports.py`, `inv_advisor.py` : inventaire des rapports et des traces ;
  - `strata.py`, `pool.py` : strates, population utile, appariement trace ↔ état ;
  - `danger_pool.py` : ordre de grandeur des positions de danger avec défense ;
  - `power.py`, `power2.py`, `power3.py` : seuils, puissances et Wilson exacts.
- Limites de ces comptages :
  - la marge de danger y est une approximation Python, sans pouvoir héroïque ni effets de fin de tour ; la sélection réelle doit utiliser `ThreatAssessment` ;
  - le plafond de 40 décisions par fichier sous-compte légèrement 4 parties ;
  - l'appariement par l'heure de l'état laisse 72 positions classées sans trace.
