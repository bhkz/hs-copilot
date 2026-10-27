# Agent E : contre-expertise indépendante de l'étude stratégique

Rédigée le 26/10/2026 par un sous-agent indépendant. Elle remplace la version simulée par le coordinateur ([agent-E-contre-expertise.md](agent-E-contre-expertise.md)).

**Cadre** :
- **aucun appel OpenAI, aucune dépense** ; la clé n'a été ni lue ni affichée ;
- aucun fichier modifié dans `src/`, `tests/`, `docs/` ni dans les fichiers existants de `bench/` ;
- tout ce que j'ai produit est dans [agent-E/](agent-E/) : scripts Python, programme C# `harness/` lié à une **copie** de `HsCopilot.Core.dll` (build Release des tests, 13 h 12, qui contient les dernières modifications de l'étude), sorties intermédiaires ;
- les issues des parties n'ont servi qu'à repérer le dernier tour d'une partie (§1, Q3). Aucune décision n'est jugée avec une carte cachée révélée plus tard.

**Qualification des affirmations** :

| Étiquette | Sens |
|---|---|
| **[mesuré]** | calculé sur les données brutes, avec le script indiqué (dossier `agent-E/`) |
| **[exécuté]** | obtenu en faisant tourner le vrai code du projet (copie de la DLL) |
| **[code]** | lu dans le code, avec fichier:ligne |
| **[déduit]** | raisonnement à partir de mesures, pas mesuré directement |
| **[non vérifié]** | repris d'un autre document sans contrôle |

## Résumé

1. **« Plafond probable du modèle » n'est pas justifié.**
   - Ce qui est démontré : aucune erreur nette n'a été trouvée sur 20 conseils jugés par un LLM. Or ce juge **voyait le conseil**.
   - Le modèle raisonne très peu : environ 150 jetons de raisonnement en medium, et l'action est le premier champ qu'il écrit.
   - Il a commis au moins une erreur nette et coûteuse en partie réelle, Nozdormu. Il la reproduit à l'identique sur le banc d'essai.
   - Aucune autre façon de décider n'a été testée sur des positions difficiles.
2. **Le correctif « alerte de danger » ne fait pas ce que le rapport annonce.**
   - L'alerte est bien calculée instantanément.
   - Mais l'overlay **n'affiche les faits rapides que pendant l'analyse**. L'alerte disparaît dès que le début de réponse, une étape de plan ou le létal du solveur s'affiche [code, exécuté].
   - Contre Tyrande, l'alerte Nozdormu aurait été visible 15,5 s, puis **effacée par le conseil « Jouer Nozdormu »**.
3. **La latence n'est pas le principal obstacle à la qualité.**
   - Dans 93 % des décisions où le modèle est appelé (99 % en début de tour), l'action s'affiche **avant** ton coup [mesuré].
   - Tu joues 3 à 4 s après son affichage : tu attends le conseil.
   - La latence te coûte surtout du **temps** : 12 s d'attente par tour en médiane, 34 s dans les tours de plus de 60 s.
   - Les « 26 % d'appels avec conseil complet affiché » et les « 55 % d'étapes de plan » mesurent mal ce que tu vois. Les étapes de plan représentent **31 %** des premières actions affichées.
4. **« La recherche exhaustive couvre déjà ton tour » est faux.**
   - Le TurnSolver ne maximise que les **dégâts au visage**. Il ne simule aucune attaque contre un serviteur et ne joue aucun serviteur à Ruée [code].
   - Avec des échanges déterministes, 7 des 10 derniers tours « plausibles » deviennent démontrables. Le gain porte sur la vitesse, pas sur les décisions : aucun létal manqué en classé [exécuté].
   - La vraie limite n'est pas la recherche : 55 % des phases d'attaque comportent un effet que rien ne simule (Râle d'agonie, déclencheur, aura, Secret) [mesuré].
5. **Conclusion : B, des optimisations ciblées, pour le système.** Pour le modèle, **preuves insuffisantes**, et non « plateau ».
   - La première marge est de faire réellement arriver le signal local à l'écran et de vérifier le danger **après** la ligne conseillée. C'est gratuit.
   - La deuxième est une mesure capable de trancher, avec un juge aveugle au conseil.
   - La troisième est un test ciblé sur les décisions difficiles : effort plus élevé, ou lignes candidates calculées localement. Environ 1,7 $.

---

## 1. Les six questions

### Q1. « Plafond probable du modèle sur les décisions courantes » est-il justifié ?

**Verdict : non justifié.** Ce que les données permettent de dire est plus faible : « sur 20 positions, un juge LLM qui voyait le conseil n'a pas trouvé d'erreur nette ».

**Preuves.**

1. **Le juge n'était pas aveugle au conseil** [mesuré].
   - Les 20 fiches de l'audit (`audit-conseils-texte/*.txt`) se terminent toutes par « Conseil donné en partie par HS Copilot », avec l'action, le résumé, la raison et la suite.
   - Le rapport écrit pourtant « agent A, à l'aveugle sur 20 conseils réels » (ETUDE §1, ligne 4).
   - Juger un conseil qu'on a sous les yeux favorise l'accord : c'est l'ancrage.
   - La méthode de sélection des 20 positions n'est documentée nulle part ; je n'ai trouvé aucun script [non vérifié].
2. **Le modèle raisonne très peu** [mesuré, `banc_payant.py`].
   - Jetons de raisonnement sur le banc payant : médiane de 129 pour V0 et 162 pour V1, sur toutes les réponses. Sur les 11 paires : moyenne de 151 pour V0 et 192 pour V1.
   - L'action est le **premier** champ du schéma de sortie (`OpenAiProvider.cs:123`), et l'effort est medium par défaut (`OpenAiProvider.cs:73`) [code].
   - Un « plafond » mesuré à environ 150 jetons de raisonnement, avec l'action choisie avant toute argumentation, est le plafond d'une **configuration**, pas celui du modèle.
3. **Une erreur nette existe en partie réelle, et elle est stable.**
   - Contre Tyrande, le conseil « Jouer Nozdormu » donne 10 cristaux à un adversaire qui a 8 cartes en main. DIAGNOSTIC-DEFAITES §1 le qualifie d'« erreur stratégique identifiable du conseiller ».
   - Sur le banc, V0 rejoue Nozdormu tout en écrivant lui-même le risque (RESULTATS-BENCHMARK).
   - Le rapport classe ce cas dans le « danger », mais c'est un défaut de **jugement** sur un texte de carte public que le modèle avait sous les yeux.
4. **La stabilité n'est pas la qualité.**
   - Sur les positions comparables, V0 reproduit **13 fois sur 13** l'action affichée en partie : 9 conseils du modèle et 4 létaux du solveur [mesuré, `banc_payant.py`].
   - Le bruit est donc faible, mais cela veut aussi dire que les erreurs sont **systématiques**. Elles se répètent au lieu de se diluer.
5. **L'argument « l'effort change peu » vient d'un autre banc** (MVP3A) :
   - 36 cas, majoritairement simples, **sans** `localAnalysis` ;
   - les écarts ont été jugés par le coordinateur ;
   - dans le seul cas difficile (2 PV et un Secret inconnu), xhigh est « le plus soigné » (MVP3A-RESULTATS, tableau des cas divergents).

   Les rendements décroissants ne sont donc pas établis sur les décisions difficiles.
6. **Les faiblesses relevées par l'agent A** (charges de Profondeurs, échanges qui protègent un serviteur clé, marge de létal, Bouclier divin retiré par le mauvais attaquant, Glissedrake rendu au mulligan dans 5 cas contre 4 gardés) sont des faiblesses de **jugement de valeur**. Elles contredisent l'idée qu'il n'y a « rien à corriger dans son raisonnement courant ».

**Ce qui changerait le verdict.**
- Un jugement **aveugle au conseil** (le juge écrit sa ligne d'abord), par toi et par un juge LLM étalonné, sur au moins 30 positions difficiles qui n'ont pas servi au développement.
- Puis un A/B pré-enregistré : medium contre high, ou contre medium + lignes candidates locales. S'il ne montre aucun écart dans le sens attendu, « plateau » deviendrait défendable pour ce modèle.

### Q2. Onze comparaisons payantes et vingt jugements LLM permettent-ils de conclure sur la qualité stratégique ?

**Verdict : non pour toute comparaison ou tout plafond ; oui pour quelques bornes grossières.**

**Ce qu'on peut conclure** :

| Énoncé | Fondement |
|---|---|
| Le taux d'erreurs nettes sur ce type de positions n'est probablement pas énorme | 0 sur 20 donne une borne haute de Wilson à 95 % de **16 %**, si le juge est fiable, ce qui n'est pas établi [déduit] |
| Les « discutables » existent en proportion non négligeable | 3 sur 20 : IC 95 % de 5 à 36 % ; 4 sur 20 avec la « suite perfectible » : de 8 à 42 % [déduit] |
| Le modèle est stable à entrée identique | 13 positions sur 13 reproduites [mesuré] |
| La légalité est sûre | 0 action illégale acceptée sur 837 appels [mesuré, `compteurs.py`] |

**Ce qu'on ne peut pas conclure** :
- **V1 contre V0** : 1 désaccord sur 11 paires donne un test de signe à p = 1 en bilatéral et 0,5 en unilatéral.
- **Les positions de danger** : sur les 11 paires, 4 seulement sont des positions de danger. La première action y est **identique dans les 4**. Le seul changement porte sur Nozdormu, cas d'origine du module.
- **Le danger lui-même** : les 12 positions de danger et les 2 positions Nozdormu viennent toutes des parties de la nuit, celles qui ont servi à concevoir le module (les 8 parties du matin n'ont aucune position de danger, selon l'agent D). Au total, 24 positions du banc sur 25 viennent de ces parties [mesuré]. La circularité couvre donc toute la strate de danger, pas seulement 2 positions.
- **La latence de V1** : le rapport dit « pas plus lent » (médianes non appariées de 15,2 et 14,8 s). **En apparié**, V1 est plus lent dans 8 paires sur 11, de 2,0 s en médiane et 3,8 s en moyenne (p unilatéral = 0,11) [mesuré]. Ce n'est ni démontré plus lent, ni démontré aussi rapide.
- **Le juge** : il n'a pas été étalonné (aucune position à réponse démontrable) et n'a pas été comparé à un humain. Partie 1 : 3 positions sur 11 « vues » avec les conseils, **dont la seule paire décisive** (P3c57d927). Partie 2 : conseil visible sur les 20.
- **Un plafond** : il faudrait comparer à quelque chose de plus fort. Rien n'a été comparé.

**Ce qui changerait le verdict** : le protocole de l'agent D, que je juge correct, avec deux ajouts :
- un juge aveugle au conseil ;
- des positions difficiles hors développement : la réserve contre l'IA contient **57 positions de danger** sur 582 [mesuré, `threat-audit` relancé].

### Q3. Le TurnSolver explore-t-il assez de possibilités tactiques ?

**Verdict : la phrase « la recherche exhaustive couvre déjà ton tour » (ETUDE §7) n'est pas justifiée.** Le solveur est exhaustif, mais sur un sous-espace étroit : les dégâts au visage.

**Ce que le code cherche réellement** [code, `TurnSolver.cs`] :

| Question | Réponse | Où |
|---|---|---|
| Objectif | **maximum de dégâts au visage ce tour**, puis moins de risques, puis moins d'étapes ; ni valeur, ni plateau, ni survie | `Search`, 504-523 |
| Cartes tentées | seulement celles qui apportent des dégâts au visage ou les préparent : Profondeurs ; sorts et Cris de guerre « Inflige N » vers le héros ; Charge ; dégâts de fin de tour ; Voyante seulement si un sort de dégâts est en main ; Forgeron seulement si une Charge est en main ; Gardien (épée) ; Chronoseigneur seulement contre des Provocations jouées au tour précédent | `Moves`, 525-611 |
| Serviteurs à Ruée | **jamais joués** : pas de branche dans `Moves`, et `IsModeled` ne les reconnaît pas | 495-499 |
| Attaques | ajoutées à la fin, **toutes au visage**, et seulement s'il ne reste aucune Provocation | `Finish`, 665-700 |
| Attaques contre un serviteur, échanges | **aucune** ; une Provocation ne tombe que par Profondeurs (à 1 PV) ou Chronoseigneur | 61-62, 599-653 |
| Profondeur | tout le tour, sur ce sous-espace, avec mémoïsation | 504-523 |
| Tour suivant | `SolveNextTurn` : même recherche, en supposant que ton plateau survit et que l'adversaire ne joue rien | 412-493 |
| Tour adverse | absent du solveur. `ThreatAssessment` regarde l'état **actuel**, pas l'état après ta ligne, et ne propose que des défenses **en un coup** : tuer un attaquant, ou l'armure | `ThreatAssessment.cs:65-124` |

Le message envoyé au modèle le dit lui-même : « dégâts au héros adverse uniquement ; échanges, survie et valeur ne sont pas évalués par ce calcul » (`QuickAnalysis.cs:126`).

**Mesures** :

1. **Niveaux du solveur** sur les 830 positions : aucun 750, certain 44, plausible 28, conditionnel 8. C'est identique aux agents C et D [exécuté]. Sur 90 % des positions, le solveur n'apporte que le maximum au visage et l'ordre des Profondeurs.
2. **Recherche d'échanges.**
   - **Méthode** : `harness trades`. On énumère des échanges déterministes contre les Provocations : attaques, sorts et Cris de guerre « Inflige N », serviteurs à Ruée joués depuis la main. Puis on rappelle le **vrai** TurnSolver [exécuté].
   - **Résultat sur 180 positions** bloquées par une Provocation sans létal certain : 18 létaux trouvés, dont 5 sans aucune Provocation à effet (Râle d'agonie, déclencheur).
   - **Par tour, rétrospectivement**, sur les 38 derniers tours de parties gagnées [`letal_tours.py`] :

   | Niveau à la 1re décision | Tours | Avec les échanges |
   |---|---|---|
   | certain | 15 | — |
   | plausible | 10 | 3 démontrables sans risque, 4 démontrables si l'on classe mieux les risques (Râle d'agonie de Tirion qui donne une arme, Sbire qui invoque un 1/1 sans Provocation…), 3 non trouvés |
   | conditionnel | 5 | — |
   | aucun | 8 | — (abandons ou cartes non modélisées) |

   - L'ordre de grandeur de l'agent C (6 sur 25) est donc **confirmé** (7 sur 38).
   - **Le classement des risques est le nœud.** Un faux positif réel a été écarté par ce drapeau : au tour 15 contre Rexxar, tuer l'Ours des montagnes invoque deux Provocations 2/4, donc pas de létal.
3. **Aucun létal manqué en classé** dans cette recherche [exécuté].
   - Les 44 positions « certain » couvrent 25 tours, **tous** derniers tours de parties gagnées.
   - En direct : 69 létaux du solveur affichés sur 26 tours, **tous** derniers tours gagnés [mesuré].
   - Le gain d'un solveur à échanges est donc **la vitesse**. Ces tours-là, la première action du modèle arrivait 6 à 21 s après le bloc d'options, contre environ 0,1 s pour le solveur. Aucune décision ne change.
4. **Phases d'attaque comparées à toutes les phases possibles** [mesuré, `phase_attaque.py`].
   - On part de 1 109 décisions reconstruites par `bench build --per-turn 99`.
   - **Couverture** : sur 195 phases d'attaque, seules 87 sont simulables. 107 sont écartées : 60 par un serviteur adverse à effet, 37 par un attaquant à effet, 10 par un Secret.
   - **Taille de la recherche** : médiane de 3 fins de phase distinctes, 49 au plus. La recherche est donc triviale.
   - **Résultat** : 2 phases jouées sur 87 sont strictement dominées, avec des gains insignifiants (1 PV d'un de tes serviteurs, 1 ATQ).
   - **Conséquence** : dans les positions simples, ton arithmétique de combat (qui suit le conseil dans 96 % des cas) n'a pas de défaut. La limite d'un calcul tactique local, c'est **la couverture des effets de cartes**, pas la recherche.

**Ce qui changerait le verdict** : un solveur qui énumère les échanges et évalue la fin de tour (dégâts au visage, menace adverse restante, plateau) sur les positions simulables, et qui classe comme « incertaines » les autres. On saurait alors combien de décisions courantes il départage. Aujourd'hui, on sait seulement qu'il ne trouve presque rien de **strictement** meilleur sur les positions simples.

### Q4. Les alertes de danger arrivent-elles assez tôt pour influencer tes décisions ?

**Verdict : partiellement justifié.** Elles arrivent **tôt**. Mais elles **disparaissent dès qu'un conseil s'affiche**, et leur présentation est peu visible. « Influencer utilement » n'est donc pas démontré, et le code actuel va plutôt contre.

**Chronologie mesurée** [mesuré, `chronologie.py`, sur 1 365 blocs d'options de 48 journaux classés, appariés aux traces par l'heure du bloc] :

| Étape | Délai depuis le bloc d'options (GameState) |
|---|---|
| Analyse rapide visible (premier événement de la trace) | **0,17 s** en médiane, P90 0,27 s |
| Ton tour exécuté par le client (PowerTaskList MAIN_ACTION), 1re décision | 0,85 s en médiane, P90 4,4 s ; plus de 3 s dans 56 cas sur 318 |
| Ton action (SendOption), 1re décision du tour | **11,1 s** en médiane (9,4 s après l'écran) |
| Ton action, milieu de tour | **5,3 s** en médiane |

- **La retenue « TON TOUR ARRIVE » ne retarde pas l'alerte.**
  - Pendant l'analyse, `AdviceDisplay.Build` garde les faits et ne change que le titre (`AdviceDisplay.cs:38-59`, 108-119) [code].
  - En classé, l'écran rattrape le serveur en moins d'une seconde en médiane.
- **Mais les faits ne sont affichés que dans les phases « analyse » et « sans conseil ».**
  - Les vues « prêt » (modèle, suite du plan, solveur) et « partiel » ne contiennent **aucun fait** (`AdviceDisplay.cs:68-107` contre 108-139) [code].
  - **Vérifié par exécution** du vrai `AdviceDisplay` sur la décision fatale contre Rexxar ([affichage-rexxar.txt](agent-E/affichage-rexxar.txt)) et sur Nozdormu ([affichage-nozdormu.txt](agent-E/affichage-nozdormu.txt)). L'alerte est visible en « analyse », mais pas en « partiel », en « prêt (modèle) » ni en « prêt (suite du plan) » [exécuté].
  - Les tests de l'étude ne vérifient que le texte produit par `QuickAnalysis.Analyze`, pas l'affichage (`ThreatAssessmentTests.cs:82-112`) [code].
- **Fenêtre de visibilité sur les 28 positions de danger** (marge ≤ 5) [mesuré, [danger_fenetre.md](agent-E/danger_fenetre.md)] :
  - **8,7 s** en médiane avant ton coup. L'alerte reste longtemps visible justement parce que le modèle est lent sur ces positions : première action à 10,8 s en médiane ;
  - **0 s dans 4 positions sur 28** : 2 étapes de plan et 2 létaux du solveur. Moins de 2 s dans 5 ;
  - **contre Rexxar, au tour fatal** : 12,2 s, 8,7 s puis 3,9 s, puisqu'aucun conseil n'est arrivé. Le nouveau calcul aurait donc été visible à la décision fatale ;
  - **contre Tyrande, au tour 7** : l'alerte Nozdormu aurait été visible 15,5 s. Puis le début de réponse « Jouer Nozdormu » l'aurait **remplacée**, et tu as joué 3,0 s plus tard. Le dispositif fait disparaître l'alerte précisément quand le modèle la contredit.
- **Peu de relief visuel** [code] :
  - les faits sont en gris, en 11,5 points, sous le plan et la raison (`OverlayWindow.xaml:77`) ;
  - ils sont masqués en mode compact (`ShowDetails`, `OverlayWindow.xaml:65`) ;
  - la gravité « alerte » est perdue à l'affichage (`AdviceDisplay.cs:63`).
- **Un précédent** : à la décision fatale contre Rexxar, la nouvelle alerte aurait été visible. Mais l'ancienne (« Menace : 25 contre 25 ») était déjà affichée aux décisions 1 et 2 du même tour, pendant 12 et 9 s environ, et n'a pas empêché l'attaque au visage de la décision 3 (DIAGNOSTIC-DEFAITES §2) [mesuré, puis déduit]. Elle avait toutefois disparu à cette décision 3 : on ne peut pas départager « signal absent » et « signal ignoré ».
- **Fréquence au moment de décider.**
  - 13 tours avec une alerte, dans 8 parties [mesuré, `threat-audit` relancé].
  - 6 de ces tours sont dans des parties gagnées. Dans 2 parties perdues, l'alerte arrivait **plus tôt** que le tour fatal : Uther aux tours 6 et 8 avec des marges de 2 et 4 (fatal au tour 9), le roi-liche au tour 9 avec une marge de 2 (fatal au tour 10). C'est là qu'un signal pourrait changer le cours de la partie ; l'effet reste contrefactuel.

**Ce qui changerait le verdict** :
- garder les faits de gravité « alerte » dans les vues « partiel » et « prêt », en tête et en couleur, et le tester sur l'affichage ;
- puis compter, sur tes prochaines parties, les décisions avec alerte affichée en même temps qu'un conseil, et ce que tu as joué.

### Q5. La latence et les réponses partielles sont-elles désormais les principaux obstacles ?

**Verdict : non justifié comme obstacle principal à la qualité. Justifié comme coût en temps, surtout dans les tours difficiles.** Il reste des faiblesses stratégiques identifiables.

**Preuves** [mesuré, `chrono_detail.py` et `latence_difficulte.py`] :

1. **Le conseil arrive presque toujours avant ton coup.**
   - Sur 824 décisions d'action avec appel au modèle, la première action s'affiche avant ton coup dans **765 cas, soit 93 %** : 313 sur 315 en début de tour, 452 sur 509 en milieu de tour.
   - Quand ce n'est pas le cas, tu as joué en **2,1 s** en médiane : des coups rapides, donc probablement évidents.
2. **Tu attends le conseil.**
   - Tu joues 4,2 s (début de tour) et 3,1 s (milieu de tour) en médiane **après** l'affichage de l'action.
   - Dans 633 cas sur 765, l'écart est de 2 à 8 s ; dans 6 cas seulement, il est inférieur à 1 s.
   - Sur une étape de plan, affichée en 0,2 s, tu joues 4,5 s après le bloc.
   - « Tu joues toutes les 7 s » décrit donc ton rythme **avec** le conseiller, pas ton rythme propre [déduit].
3. **Le coût en temps est réel.**
   - Durée d'un tour : 31 s en médiane ; 44 tours sur 325 dépassent 60 s, et 12 dépassent 75 s.
   - Attente cumulée avant les premières actions affichées : 12 s par tour en médiane, **34 s dans les tours de plus de 60 s** [mesuré].
   - Le délai s'allonge avec la difficulté :

     | Situation | 1re action affichée | Ton coup |
     |---|---|---|
     | 4 actions légales ou moins | 4,4 s | 7,2 s |
     | 14 actions légales ou plus | 9,2 s | 15,3 s |
     | Danger | 10,8 s | 17,0 s |

4. **Les appels annulés ont le plus souvent déjà servi** [déduit].
   - Au moins 90 % des appels ont affiché une action avant ton coup.
   - Les 467 annulations portent donc surtout sur des appels dont **le début de réponse a déjà été utilisé**.
   - Les 6,55 $ estimés pour les annulations ne mesurent donc pas, pour l'essentiel, du gaspillage.
5. **Les « réponses partielles » sont la forme principale du conseil.**
   - Le début de réponse annonce la même action que la réponse complète dans 190 cas sur 190 (agent D) [non vérifié].
   - Première action affichée par décision : 663 débuts de réponse (63 %), 323 étapes de plan (31 %), 69 létaux du solveur (7 %).
6. **Il reste des faiblesses stratégiques identifiables.**
   - Nozdormu, déjà vu à la Q1.
   - Les pertes de valeur relevées par l'agent A.
   - Le danger évalué avant ta ligne et non après : aucune vérification de la fin de tour conseillée.
   - Rexxar : le budget de session avait bloqué le modèle aux tours 14 et 15 (« 5,001 $ sur 5,00 $ », trace du 26/10 à 0 h 41) [mesuré]. Au tour 16, aucun début de réponse n'est arrivé en 12,3 s sur une position complexe.

   La défaite contre Rexxar combine donc **budget, latence sur une position difficile et signal peu visible**, pas la latence seule.

**Ce qui changerait le verdict** : une mesure montrant que tu joues sans attendre le conseil, ou des pertes répétées de conseil sur des décisions qui comptent. Rien de tel dans les 48 parties.

### Q6. Quelles hypothèses du rapport sont insuffisamment démontrées ou fausses ?

Voir la section 2, classée par gravité.

---

## 2. Hypothèses du rapport réfutées ou insuffisamment démontrées

| Gravité | Affirmation du rapport | Statut | Preuve |
|---|---|---|---|
| **1. Grave** | L'alerte « affichage seul » est faite, instantanée, « maintenue pendant le tour » (§6, rang 1 ; §8) | **réfutée pour l'affichage** | les faits ne sont pas affichés dès qu'un conseil l'est (`AdviceDisplay.cs:68-107`) ; vérifié par exécution ; 0 s de visibilité sur les étapes de plan et les létaux du solveur |
| **2. Grave** | « Plafond probable pour le modèle sur les décisions courantes » | **non démontrée** | juge non aveugle au conseil ; environ 150 jetons de raisonnement ; aucune variante testée sur positions difficiles ; une erreur nette en partie (Nozdormu) |
| **3. Grave** | « Pour ton tour, la recherche exhaustive existe déjà » (§7) ; « MCTS est inutile pour ton seul tour » (réponse courte) | **fausse dans sa première moitié** | le solveur ne cherche que les dégâts au visage, sans échange ni Ruée (`TurnSolver.cs:504-700`). La seconde moitié tient : il ne faut pas de MCTS, puisqu'une énumération des échanges coûte quelques ms |
| **4. Grave** | « La latence face à ton rythme » est « le vrai goulot » ; « seulement 26 % des appels finissent par un conseil complet affiché » | **mal mesurée** | l'action s'affiche avant ton coup dans 93 % des décisions avec appel ; tu attends le conseil ; le coût est du temps (12 à 34 s par tour), pas des conseils perdus |
| **5. Moyenne** | « Le défaut était la latence et l'absence de signal immédiat, pas le raisonnement » (Rexxar) ; « ni le raisonnement ni l'information disponible ne sont le facteur limitant démontré » | **partielle** | Rexxar : budget bloqué aux tours 14-15, puis latence sur une position difficile, et un ancien signal visible 12 s sans effet. Nozdormu est une erreur de raisonnement sur une information disponible |
| **6. Moyenne** | « 55 % des conseils affichés (317 sur 581) sont des étapes de plan » | **dénominateur incomplet** | la proportion ne compte que les événements « prêt » ; en comptant les débuts de réponse, c'est 323 sur 1 055 premières actions affichées, soit **31 %** |
| **7. Moyenne** | V1 : « pas de régression », « n'est pas plus lent » | **non démontrée** | apparié : plus lent dans 8 paires sur 11, +2,0 s en médiane ; raisonnement +27 % en moyenne ; 4 paires de danger, toutes identiques |
| **8. Moyenne** | « Gain d'un calcul plus déterministe : réel mais borné […] 3 à 9 % des positions » | **borne sous-estimée dans son principe** | 463 positions sur 830 proposent une attaque contre un serviteur (agent C) ; mais la couverture des effets limite tout calcul local (107 phases d'attaque sur 195 non simulables) |
| **9. Moyenne** | « Effort high : […] peu de décisions changées » | **non transposable** | MVP3A : 36 cas surtout simples, sans `localAnalysis` ; dans le seul cas difficile, xhigh fait mieux |
| **10. Faible** | « 4 alarmes sur 4, 0 fausse alarme » | **en partie tautologique** (déjà dit), et non recalculée ici | au moment de décider : 13 tours avec alerte, dont 6 dans des parties gagnées, et 1 seul tour sauvable (Rexxar) |
| **11. Faible** | « Concordance avec le conseil donné en partie : 6 positions sur 6 » | **sous-compté** | 13 sur 13 pour V0, 14 sur 15 pour V1 ; cela renforce la stabilité, donc la systématicité des erreurs |
| **12. Faible** | « Le modèle se répète déjà », donc le vote est inutile | **juste, mais la conclusion est incomplète** | une erreur stable ne se corrige ni par le vote ni par la relance : il faut une **autre** information ou un **autre** calcul |

---

## 3. Mes désaccords explicites

### Avec l'agent A

| Citation | Ma position | Ma preuve |
|---|---|---|
| « 20 positions figées : 16 conseils bons […] aucune erreur nette » (Résumé) | Ce n'est pas un audit à l'aveugle : tu juges un conseil que tu as sous les yeux | chaque fiche `audit-conseils-texte/*.txt` contient « Conseil donné en partie par HS Copilot » ; aucune ligne écrite avant de lire le conseil n'est mentionnée en partie 2 |
| « Ses faiblesses sont des pertes de valeur, pas des contresens » | D'accord sur la nature. Mais Nozdormu, que tu qualifies toi-même de « recommandation légale mais mauvaise » (partie 1), est un contresens sur un texte public, et il a coûté une partie | DIAGNOSTIC-DEFAITES §1 ; RESULTATS-BENCHMARK (V0 rejoue Nozdormu) |
| « Évaluateur d'échanges […] il couvrirait Pf7cf118d et P312a5de4 » (§3.3) | Partiellement. Sur les phases d'attaque simulables, l'arithmétique est déjà presque toujours non dominée. Tes deux exemples relèvent de la **riposte adverse** (qui va tuer Luisaile) et de l'ordre (Réincarnation). Un tableau « tue / survit » ne suffit pas : il faut la menace sur **tes serviteurs** après ta ligne | `phase_attaque.py` : 2 lignes dominées sur 87, gains insignifiants |

### Avec l'agent B

| Citation | Ma position | Ma preuve |
|---|---|---|
| « La méthode la plus rentable […] recherche locale à deux demi-tours […] ton tour, puis la riposte visible » (§0.2) | **D'accord, et c'est justement ce qui n'a pas été fait.** `ThreatAssessment` évalue l'état actuel avec des défenses en un coup, pas la fin de ta ligne | `ThreatAssessment.cs:65-124` ; `QuickAnalysis.cs:221` |
| « Monter l'effort de raisonnement change peu de décisions » (§0.5) | Établi seulement sur des positions surtout simples, sans calcul local | MVP3A-RESULTATS, tableau des 8 divergences |
| « Le joueur joue souvent dès le premier fragment » (§3) | D'accord, et c'est mesurable : 63 % des premières actions affichées sont des débuts de réponse ; tu joues 3 à 4 s après | `chrono_detail.py` |

### Avec l'agent C

| Citation | Ma position | Ma preuve |
|---|---|---|
| « 55 % des conseils affichés sont des étapes de plan : 317 sur 581 » (Résumé 2) | 31 % en comptant les débuts de réponse, qui sont des conseils affichés | `chronologie.py` : 323 sur 1 055 |
| « Le joueur agit en médiane toutes les 7 s, soit à peu près la latence d'un début de réponse » ; piste I, ne pas envoyer l'appel « si la réponse arrivera après ton coup » | La piste I supprimerait surtout des conseils qui **arrivent** : 89 % des appels de milieu de tour affichent l'action avant ton coup. Ton rythme dépend du conseiller | `chrono_detail.py` |
| « Environ 430 annulés après envoi. Leur coût estimé (6,43 $) dépasse le coût réel » (Résumé 7) | Exact, mais ce coût n'est pas un gaspillage : l'annulation suit le plus souvent l'affichage du début de réponse | déduit de 765 décisions sur 824 avec action affichée avant ton coup |
| Piste A.1 : le danger « affiché immédiatement, en tête de l'overlay et non noyé parmi les faits » | **D'accord. Ta recommandation n'a pas été appliquée** : l'alerte reste noyée parmi les faits, et masquée dès qu'un conseil s'affiche | `AdviceDisplay.cs:63-139` ; `OverlayWindow.xaml:65`, 77 |
| « Six létaux passaient par le retrait de Provocations […] très probablement démontré dès la première décision » (§5.2) | Confirmé : 7 sur 38. Le gain est la vitesse, et tout dépend du classement des risques | `harness trades` ; `letal_tours.py` |

### Avec l'agent D

| Citation | Ma position | Ma preuve |
|---|---|---|
| « 866 points de décision, dont 270 sans conseil (31 %) » (§0) | **Dénominateur faux.** « Points sans conseil » compte les décisions ignorées, surtout « seule action possible : finir le tour », et non les appels restés sans réponse. Ce n'est pas un sous-ensemble des 866 | « points de décision » est incrémenté au lancement d'un appel (`StrategyCoordinator.cs:515`), « points sans conseil » dans `SkipLocked` (`:637`) ; 226 « fin de tour seule » dans les traces |
| Critère G4 : « latence médiane appariée au plus +2 s » | Bon critère : sur les 11 paires, V1 est déjà **à la limite** (+2,0 s) | `banc_payant.py` |
| Protocole, puissance, réserve des parties contre l'IA | D'accord sur le fond. J'ajoute que le juge doit écrire sa ligne **avant** de voir le conseil, ce que la partie 2 de l'agent A n'a pas fait | — |

### Avec la contre-expertise simulée par le coordinateur

| Citation | Ma position | Ma preuve |
|---|---|---|
| P1 : « C'est du texte affiché, que le modèle ne reçoit pas : aucun risque de dégrader ses choix. Elle est instantanée » | Calcul instantané, oui ; affichage, non : il est effacé par tout conseil. Le risque n'est pas de dégrader le modèle, mais de ne rien changer du tout | exécution de `AdviceDisplay` ; `danger_fenetre.md` |
| « Le défaut principal est le délai par rapport à ton rythme, pas le raisonnement » (§3) | Faux pour Nozdormu (raisonnement), incomplet pour Rexxar (budget, latence, signal peu visible) | DIAGNOSTIC-DEFAITES ; trace du 26/10 à 0 h 41 |
| « 55 % des conseils sont des étapes non réévaluées. C'est vrai. » | 31 % | `chronologie.py` |
| P12 : « Pour ton seul tour, la recherche exhaustive existe déjà. » | Faux : seulement les dégâts au visage | `TurnSolver.cs:504-700` |
| P9 : « low, medium et high choisissent la même action dans 81 à 89 % des cas » | Mesuré sur des cas surtout simples, sans calcul local ; dans le cas difficile, xhigh est meilleur | MVP3A |
| P2 : « Pas de régression observée, coût +5 %, latence identique » | Latence non identique en apparié | +2,0 s en médiane, V1 plus lent 8 fois sur 11 |
| « Circularité du banc d'essai (grave) » | D'accord, et c'est plus large : toutes les positions de danger viennent des parties de développement, et les 4 paires de danger complètes n'ont rien changé | `danger-check` ; `banc_payant.py` |
| « La statistique "4 alarmes sur 4" est en partie tautologique » | D'accord | — |

---

## 4. Les chiffres du rapport, recalculés

| Chiffre annoncé | Valeur trouvée | Méthode |
|---|---|---|
| 44 parties classées, 39 victoires et 5 défaites | **confirmé** : 45 parties distinctes (39 V, 6 D), dont celle de 19 h 22, conseils désactivés et 0 appel | `compteurs.py` ; doublons = heure du fichier plus de 60 s après le début de la partie |
| 837 appels | **837** | somme des compteurs des 48 rapports classés non doublons |
| 217 conseils complets affichés | **217** (« actions proposées légales ») ; mais **765 décisions sur 824** ont affiché une action avant ton coup | `compteurs.py`, `chrono_detail.py` |
| Premier fragment 3,6 s, réponse complète 8,8 s (médianes des parties) | **3,61 s et 8,84 s**, mais sur les seuls appels terminés (391 sur 837), mesurés depuis l'envoi. Du bloc d'options à la 1re action affichée : **5,6 s** en médiane (P90 11,4 s) ; 9,2 s à 14 actions légales ou plus ; 10,8 s en danger | rapports et traces |
| « Tu joues toutes les 7 s » | 6,7 s du bloc à l'action, toutes décisions ; 5,3 s en milieu de tour (6,5 s hors fin de tour) ; 11,1 s en début de tour. Rythme **avec** le conseiller : 3 à 4 s après l'affichage | `chronologie.py` (SendOption contre DebugPrintOptions) |
| 25 létaux certains sur 25 | **confirmé** : 44 positions sur 25 tours, tous derniers tours gagnés ; en direct, 69 affichages sur 26 tours, tous derniers tours gagnés | harness, `chronologie.py` |
| 55 % des conseils affichés sont des étapes de plan (317 sur 581) | **31 %** (323 sur 1 055 premières actions affichées) | `chronologie.py` |
| 28 alertes sur 830 ; 57 sur 582 contre l'IA | **confirmé** (15 + 13 ; 31 + 26) ; 13 tours distincts en classé | `threat-audit` relancé |
| « 4 alarmes sur 4, 0 fausse alarme » | non recalculé (approximation Python sur 291 fins de tour, DIAGNOSTIC §4) ; au moment de décider : 13 tours dans 8 parties, dont 6 dans des parties gagnées | `threat-classe.jsonl` |
| 6,51 $ réels et 6,55 $ estimés | **6,512 $ et 6,552 $** | `compteurs.py` |
| 0 action illégale acceptée, 1 erreur, 0 dépassement de délai | **confirmé** par les compteurs | `compteurs.py` |
| Banc : 11 paires, 10 sur 11 identiques | **confirmé** | `banc_payant.py` |
| Banc : 15,2 s contre 14,8 s | médianes non appariées confirmées ; **en apparié : V1 +2,0 s en médiane, +3,8 s en moyenne, plus lent 8 fois sur 11** | `banc_payant.py` |
| Banc : 0,0216 $ contre 0,0227 $ par appel | **confirmé** | idem |
| Concordance avec la partie : 6 sur 6 | **13 sur 13** pour V0 ; 14 sur 15 pour V1, Nozdormu différant | `banc_payant.py` + `chronologie.py` |
| Niveaux du solveur : 750 / 44 / 28 / 8 | **confirmé** | harness |
| 6 létaux vus seulement après l'échange | **7 tours sur 38** démontrables avec des échanges (3 sans risque, 4 selon le classement des risques) | `harness trades`, `letal_tours.py` |
| Écran contre serveur en classé : 1,6 s (SYNCHRO, 35 tours) | **0,85 s** en médiane sur 318 premières décisions (P90 4,4 s) | `chronologie.py` |

---

## 5. Où se trouve la prochaine marge de progression

Les pistes sont classées par preuve × impact ÷ coût. Le coût d'un appel est d'environ 0,022 $, environ 0,007 $ quand le message vient du cache.

### 1. Faire arriver le signal local à l'écran et vérifier le danger après la ligne conseillée

**Pourquoi.** C'est la seule piste qui touche les deux mécanismes de défaite identifiés, Rexxar et Tyrande, sans changer le modèle.
- Le correctif actuel est **incomplet** : l'alerte disparaît dès qu'un conseil s'affiche.
- Il évalue l'état **avant** ta ligne, pas après.

**Sans appel payant (0 $).**
1. **Affichage** : un test d'affichage qui exige les faits « alerte » dans les vues « partiel » et « prêt », avec rejeu des fixtures Rexxar et Tyrande. `harness display` montre déjà l'échec actuel.
2. **Danger après la ligne** :
   - simuler l'action conseillée et les étapes structurées de la suite (identifiants d'entités), puis recalculer `ThreatAssessment` ;
   - sur les 830 positions classées et les 582 contre l'IA, avec les conseils réels (`conseils-direct.jsonl`) ou ceux du banc, compter les lignes qui finissent à marge ≤ 0 alors qu'une défense existait ;
   - relire chaque signalement à la main.
3. **En direct** : ensuite, compter sur tes parties les décisions avec une alerte visible en même temps qu'un conseil, et ce que tu as joué.

**Avec appel payant** : inutile tant que cela reste un affichage.

### 2. Une mesure capable de trancher la qualité stratégique

**Pourquoi.** Sans elle, ni « plateau », ni « nouveau palier » n'est décidable. Aujourd'hui, le seul juge est un LLM qui voyait le conseil.

**Sans appel payant (0 $, environ 1 à 2 h de ton temps).**
- **Positions** : 30 positions difficiles hors développement. Critères : 14 actions légales ou plus, danger, ou Provocations avec un Secret. Sources : la réserve contre l'IA et tes prochaines parties.
- **Ton jugement** : tu écris ta ligne d'abord, puis tu juges deux lignes anonymes, en cochant « je me souviens de la suite » si c'est le cas.
- **Un juge LLM** étalonné sur 10 positions à réponse démontrable.
- **Un banc fidèle au direct** : `previousAdvice`, liste du deck, Découvertes.

**Avec appel payant** : aucun pour la mesure elle-même.

### 3. Les décisions difficiles : une autre manière de produire le conseil, seulement là où c'est difficile

**Pourquoi.**
- Sur les positions difficiles, tu attends déjà 15 à 17 s, et la première action arrive vers 9 à 11 s.
- Le modèle n'y consacre qu'environ 150 jetons de raisonnement.
- C'est le seul endroit où un « nouveau palier » reste plausible.

**Variantes** :
- medium (référence) ;
- high ;
- medium + lignes candidates locales : tableau des échanges et marge de danger après chaque ligne, sur le modèle « le moteur vérifie, le LLM choisit » de l'agent B.

**Sans appel payant** : construire les lignes candidates et mesurer leur couverture, c'est-à-dire la part des positions difficiles simulables. Aujourd'hui, 45 % des phases d'attaque le sont.

**Avec appel payant (environ 1,7 $).**
- 20 positions difficiles pré-enregistrées × (référence, référence relancée sur cache pour le bruit, high, candidates) : environ 20 × (0,022 + 0,007 + 0,033 + 0,024) ≈ 1,7 $.
- Jugement aveugle, comme en piste 2.
- **Critères** : erreurs démontrables, préférence aveugle au test de signe, latence de la première action au plus 15 s.

**Comment améliorer le difficile sans dégrader le simple** :
- le routage existe déjà : solveur, plan, modèle ;
- la difficulté se calcule localement avant l'appel : nombre d'actions légales, danger, Provocations, Secrets ;
- les positions simples ne changent pas si la variante n'est appliquée qu'au-dessus du seuil ;
- **la vérification de séquences n'ajoute que des mises en garde** : son risque se borne hors ligne en comptant ses déclenchements sur les positions simples, à 0 $.

### 4. Solveur : les échanges contre les Provocations pour le létal

**Pourquoi.** Le gain est un létal démontré instantanément au lieu de 6 à 21 s d'attente, sur environ 7 tours de victoire sur 38. Aucune décision ne change, et le risque est de produire un faux « certain ».

**Sans appel payant (0 $).**
- Porter la recherche du harness dans le solveur, avec un classement des risques plus fin (quel Râle d'agonie peut invoquer une Provocation).
- Rejouer les 830 + 582 positions : aucun « certain » hors des derniers tours gagnés, et les 7 tours certifiés.

**Avec appel payant** : aucun.

### 5. Vérifier une étape de plan avant de l'afficher

**Pourquoi.** 31 % des premières actions affichées sont des étapes de plan que rien ne rejuge contre le danger.

**Sans appel payant (0 $)** :
- rejouer les traces : chaque étape de plan affichée, avec un contrôle de légalité, d'issue (tue ou survit) et de marge de danger après l'étape ;
- compter les signalements et les relire.

### 6. Latence et coût

**Pourquoi.** C'est un gain de confort et de chronomètre, environ 12 à 34 s d'attente par tour. Pas un gain de qualité démontré.
- Lire la suite du plan dans le flux.
- Mettre ce qui est stable en tête du message pour le cache.
- Alléger le message.

**Sans appel payant** : simuler la lecture de `followUp` dans les flux enregistrés.

**Avec appel payant (environ 1 $)** : un A/B sur le message allégé, 25 positions × 2 variantes, plus une relance sur cache.

### 7. Mulligans et Découvertes

**Pourquoi.** Les défauts récurrents (Glissedrake) ont été jugés sans contexte.

**Sans appel payant** : enregistrer le contexte (main, plateau, mana) dans la trace, puis juger à l'aveugle.

### Faut-il comparer des manières de produire et d'utiliser le conseil, et pas seulement des modèles ?

**Oui. Les données disent même que c'est là que se joue l'essentiel.** Tu consommes déjà quatre « produits » différents, et on peut les comparer sans appel sur la vitesse, la couverture et le niveau de vérification :

| Produit | Part des premières actions affichées | Délai | Ce qui est vérifié | Problème |
|---|---|---|---|---|
| Début de réponse du modèle | 63 % | 5,6 s en médiane | la légalité de la 1re action | effondre l'alerte, qui disparaît de l'écran |
| Étape de plan | 31 % | environ 0,2 s | la légalité au moment de l'exécution | jamais confrontée au danger ; alerte invisible |
| Létal du solveur | 7 % | environ 0,1 s | démontré : 26 tours sur 26 | limité au visage sans échanges |
| Alerte de danger | 3,4 % des positions | 0,17 s | calcul exact sur l'état actuel | masquée dès qu'un des trois autres s'affiche ; évaluée avant ta ligne |

Améliorer la **composition** de ces produits (pistes 1, 4 et 5) est gratuit et mesurable hors ligne. Changer de modèle ou d'effort (piste 3) est la seule partie qui demande de l'argent.

---

## 6. Conclusion

**B, des optimisations ciblées, pour le système. Preuves insuffisantes pour le modèle**, pas un plateau.

| Composant | Verdict | Pourquoi |
|---|---|---|
| **Modèle** | **preuves insuffisantes** | aucun test d'une variante plus forte sur des positions difficiles ; juge non aveugle ; très peu de raisonnement ; une erreur nette et stable (Nozdormu). « Plateau » est prématuré, « nouveau palier » non démontré : seule la piste 3 peut trancher, pour environ 1,7 $ |
| **Solveur** | **B** | son périmètre réel est plus étroit qu'annoncé (visage seulement) ; les échanges pour le létal n'apportent que de la vitesse ; un calcul tactique plus large bute sur la couverture des effets (55 % des phases d'attaque), pas sur la recherche |
| **Danger et affichage** | **B, défaut à corriger d'abord** | le correctif n'atteint pas l'écran dès qu'un conseil existe ; le danger n'est pas évalué après ta ligne |
| **Latence** | **B** | ce n'est pas la cause des conseils manqués (93 % arrivent avant ton coup), mais un coût en temps, maximal sur les décisions difficiles, là où l'on voudrait justement ajouter du calcul |
| **Mesure** | **le vrai goulot pour savoir où est la marge** | sans juge aveugle ni positions difficiles indépendantes, aucune conclusion sur la qualité du modèle n'est possible |

En une phrase : **ne cherche pas encore un meilleur modèle, et ne déclare pas non plus le plateau.**
1. Fais d'abord arriver à l'écran ce que le système calcule déjà, et vérifie le danger après la ligne conseillée.
2. Mets en place une mesure aveugle.
3. Dépense environ 1,7 $ sur 20 positions difficiles pour savoir si une autre façon de produire le conseil y change quelque chose.

---

## Annexe : reproduction

Tous les fichiers sont dans `bench/etude-strategique/agent-E/`. Aucun appel réseau.

| Fichier | Rôle |
|---|---|
| `compteurs.py` → `compteurs.txt` | compteurs, coûts et latences des 48 rapports classés non doublons |
| `chronologie.py` → `chronologie.jsonl`, `chronologie-resume.txt` | par bloc d'options : heure GameState, écran (PowerTaskList MAIN_ACTION), ton action (SendOption), ce que la trace affichait et quand |
| `chrono_detail.py` → `chrono_detail.txt` | conseil avant ou après ton coup, attente, plans, solveur |
| `latence_difficulte.py` → `latence_difficulte.md` | délai selon le nombre d'actions, le danger et le tour |
| `danger_fenetre.py` → `danger_fenetre.md` | fenêtre de visibilité de l'alerte sur les positions de danger et Nozdormu |
| `banc_payant.py` → `banc_payant.txt` | relecture de `run-danger` : paires appariées, raisonnement, concordance avec la partie |
| `letal_tours.py` → `letal_tours.txt` | derniers tours gagnés : niveau du solveur et apport des échanges (rétrospectif) |
| `phase_attaque.py` → `phase_attaque.txt`, `.jsonl` | phases d'attaque jouées contre toutes les phases possibles (dominance) |
| `voir_position.py` | affichage d'une position (variable `CAS` pour changer de fichier) |
| `harness/` | C#, lié à `lib/HsCopilot.Core.dll` (copie). Modes : `trades` (échanges puis vrai TurnSolver → `echanges.jsonl`), `dubious` (détecteur existant sur les conseils réels → `douteux-direct.jsonl` : 3 signalements « douteux » sur 802, ambigus), `display` (vrai `AdviceDisplay` → `affichage-*.txt`) |
| `threat-classe.*`, `threat-ia.*` | `hscopilot threat-audit` relancé sur les 830 et 582 positions |
| `cas-tous.jsonl` (26 Mo, supprimable) | `hscopilot bench build <46 journaux classés> --per-turn 99 --cards …\cards.255130.frFR.json --out …` (1 109 décisions, 0 fuite) |
