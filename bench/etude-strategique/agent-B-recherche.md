# Agent B — Méthodes d'IA et de planification : ce qui peut améliorer les décisions de HS Copilot

Rapport du 26/10/2026. Aucun appel à l'API OpenAI, aucune commande `hscopilot` lancée. Aucun fichier du dépôt modifié en dehors de ce rapport.

**Légende de fiabilité**, utilisée dans tout le document :
- **[lu]** : j'ai lu le texte intégral, la page ou le code.
- **[partiel]** : résumé, page d'accueil ou source secondaire seulement.
- **[code]** : vérifié dans le code de HS Copilot ou d'un projet tiers.
- **[interne]** : mesure déjà faite dans le dépôt (`docs/`, `bench/runs/`).
- **[hypothèse]** : mon raisonnement, non vérifié.

Les références `[S1]`… renvoient à la liste des sources en fin de document.

---

## 0. Conclusions en bref

1. **Les deux défaites évitables portaient sur le tour adverse**, que rien dans la chaîne ne modélise aujourd'hui. Rexxar : un létal visible sur le plateau. Tyrande : un effet public, Nozdormu qui donne 10 cristaux à l'adversaire. Le solveur ne regarde que tes dégâts au visage ; l'alerte « Menace » actuelle n'additionne que l'attaque du plateau et de l'arme adverses, sur l'état courant [code].
2. **La méthode la plus rentable n'est pas une grande recherche.** C'est une recherche locale à deux demi-tours sur l'information publique : ton tour, puis la riposte visible de l'adversaire. Elle donne la marge de survie et la ligne défensive minimale. Elle a des précédents directs en Hearthstone [S1, S15] et dans Forge pour Magic [S42], tourne en millisecondes et ne dépend ni du budget ni de la latence d'OpenAI.
3. **MCTS, ISMCTS, la déterminisation multi-tours, les réseaux de valeur, l'évolution de poids et l'apprentissage par renforcement exigent tous un simulateur complet et à jour.** Aucun n'existe pour Hearthstone 2026. SabberStone s'arrête aux cartes de 2019, Fireplace à celles de 2020, MetaStone n'a plus bougé depuis 2018 [S39–S41]. Tous les résultats publiés viennent de cartes de 2015 à 2019, contre des bots, avec des decks fixes [S1, S3, S15, S19, S20] : ils ne se transposent pas.
4. **Le patron hybride réaliste est « le moteur vérifie, le LLM juge ».** Le moteur calcule des faits et des lignes candidates exactes sur ce qu'il simule. Le LLM choisit et formule. Des garde-fous locaux signalent un plan qui laisse un létal visible ou qui offre une ressource à l'adversaire. C'est le cadre « LLM-Modulo » [S22] et ce que fait Forge en octobre 2026 [S42].
5. **Monter l'effort de raisonnement change peu de décisions et coûte cher en temps.** Sur le banc interne de 36 cas, low, medium et high conseillent la même action dans 81 à 89 % des cas. La réponse complète passe d'environ 12 s (médiane, medium) à 28,6 s (high) [interne]. Le calcul adaptatif n'a de sens que piloté par des drapeaux locaux, et seulement si l'interface demande au joueur d'attendre sur les décisions critiques.
6. **Le taux de victoire ne peut pas servir de mesure.** 39 victoires sur 44 donnent un intervalle de confiance d'environ 76 à 95 %. Il faudrait de l'ordre de 1 200 parties par variante pour voir +3 points. L'évaluation doit se faire décision par décision, sur des états enregistrés.

---

## 1. Point de départ : ce que fait HS Copilot et où il se trompe

### 1.1 Ce que la chaîne fait déjà (vérifié dans le code)

- **`TurnSolver`** (`src/HsCopilot.Core/Strategy/TurnSolver.cs`, 763 lignes) [code].
  - Recherche **exhaustive avec mémoïsation** (`Search` récursif + dictionnaire) sur les cartes jouables, les activations des Profondeurs Sanguines, puis les attaques.
  - Objectif unique : **les dégâts au visage ce tour**.
  - Mécaniques non simulées signalées (`Unmodeled`), arme aléatoire traitée en probabilité estimée, Provocations, Secrets.
  - `SolveNextTurn` estime le potentiel du tour suivant.
- **`QuickAnalysis`** produit l'alerte « Menace » [code].
  - Elle utilise `BoardMath.OpponentBoardDamage`, qui additionne l'attaque des serviteurs adverses non gelés, non endormis et capables d'attaquer (×2 pour Furie des vents), plus l'arme.
  - Elle compare ce total à tes PV + armure, **sur l'état actuel seulement**.
  - Elle ignore le pouvoir héroïque, tes Provocations (le texte le dit : « avant tes Provocations ») et l'état après ta séquence.
- **Fournisseur OpenAI** [code].
  - API Responses, `reasoning.effort` = `medium` par défaut, sortie JSON stricte dont le champ `action` vient en premier pour le flux.
  - `store: false`, `prompt_cache_key`, aucun `service_tier`, donc niveau Standard.
- **`DeckOdds`** calcule déjà les probabilités de pioche sur le contenu connu du deck, avec la mention « exact » ou « estimation ». D'après son commentaire, le modèle n'a pas utilisé les probabilités générales sur de vraies décisions [code].

### 1.2 Ce que disent les données

- **44 parties, 39 victoires** (88,6 %) [hypothèse de calcul, sur les chiffres de la mission].
  - L'intervalle de confiance à 95 % (Wilson) va d'environ **76 % à 95 %**.
  - Pour détecter un passage de 88,6 % à 92 % avec une puissance de 80 %, il faudrait environ **1 200 parties par variante**.
  - **Conséquence : aucune méthode ne pourra être validée par le taux de victoire.** Il faut des mesures au niveau des décisions (voir §6).
- **Défaites** [interne, `docs/DIAGNOSTIC-DEFAITES.md`].
  - Rexxar : 25 dégâts visibles contre 25 PV + armure. Aucun conseil n'est arrivé à temps : cinq appels ont chacun été dépassés par ton coup suivant, et le budget avait bloqué le modèle aux tours précédents. Une ligne défensive gardait 9 PV de marge.
  - Tyrande : Nozdormu a fait passer l'adversaire de 7 à 10 cristaux alors qu'il avait 8 cartes en main. Le combo caché coûtait 10 cristaux.
  - **Les deux erreurs identifiables portent sur le tour adverse.**
- **Mesure hors ligne** [interne] : sur 291 fins de tour de 35 parties, une marge visible ≤ 0 a été suivie d'une défaite 4 fois sur 4, sans aucune fausse alarme dans les parties gagnées.
- **Banc des efforts du 25/10/2026** (`bench/runs/efforts-36/report.md`, 36 cas) [interne]. Il s'agit de latences de réponse complète, mesurées en banc ; elles diffèrent du premier fragment en direct.

| Variante | Même action que medium | Médiane | P95 | Coût moyen |
|---|---|---|---|---|
| gpt-6.1-sol low | 86 % | 9,8 s | 14,5 s | 0,017 $ |
| gpt-6.1-sol medium (2e passage) | 97 % | 11,6 s | 20,1 s | 0,0062 $ (cache) |
| gpt-6.1-sol high | 89 % | 28,6 s | 65,6 s | 0,026 $ |
| gpt-6-luna medium | 51 % | 12,2 s | 22,6 s | 0,0013 $ |

  - Sur 8 cas divergents, `xhigh` a une médiane de 58,4 s et un P95 de 127 s (`bench/runs/xhigh-divergents`).
  - L'accord avec ton propre coup reste à 42–44 % quel que soit l'effort. Cet accord mesure la ressemblance avec ton jeu, pas la qualité : aucune vérité terrain n'existe.

### 1.3 Typologie des erreurs et méthodes correspondantes

| Type d'erreur | Exemple | Méthode adaptée | État actuel |
|---|---|---|---|
| Tactique de ton tour (ordre, létal) | Profondeurs avant l'attaque | recherche exhaustive locale | fait (`TurnSolver`) |
| Défense contre la riposte visible | Rexxar | recherche à 2 demi-tours sur l'info publique | absent |
| Effets publics, ressources adverses | Nozdormu | règles sur le texte + bornes publiques | absent |
| Valeur et tempo à long terme | choix d'échange, garder une carte | jugement (LLM), évaluation heuristique | LLM |
| Conseil absent ou trop tard | Rexxar | calcul local instantané, routage, latence | partiel (létal local) |

---

## 2. Ce que la littérature mesure vraiment : environnement et date de chaque résultat

**Aucun de ces résultats n'a été obtenu sur les cartes, le méta ou les mécaniques de Hearthstone 2026.**

| Travail (date) | Environnement réel | Résultat principal | Transposable à HS 2026 ? |
|---|---|---|---|
| Świechowski et al., CIG 2018 [S1] [lu] | Simulateur C++ maison, 483 cartes, 10 000 parties/s ; deux decks fixes (ZooWarlock en premier joueur, CubeWarlock en second) ; bots MCTS | À 0,5 s par coup, en second joueur : 26,5 % → 56,1 % avec réseau de valeur + solveur d'attaques. En premier joueur : 73,5 % → jusqu'à 82,0 %. Quelques dizaines de parties contre 4 humains | Non pour les chiffres. Oui pour l'idée du « board solver » (létal, puis menace adverse au tour suivant) |
| Zhang, mémoire de master (11/2017), et Zhang & Buro, CIG 2017 [S15] [lu], [S16] [partiel] | Simulateur dérivé de Silverfish, 732 classes de cartes, extension Mont Rochenoire (2015) ; matchs miroirs Mech Mage, Handlock, Face Hunter ; 5 s par coup | MCTS déterminisé (DUCT) contre Silverfish : 60,1 %, puis 72,3 % avec regroupement des événements aléatoires, puis 75,3 % avec politiques de déroulement apprises | Non pour les chiffres. Description utile de Silverfish (voir §3.1) |
| García-Sánchez et al., KBS 2020, arXiv 10/2024 [S3] [lu] | SabberStone, compétition CIG 2018, piste « decks fournis » (Pirate Warrior, Jade Shaman, Reno Kazakus Mage de 2017) | Agent glouton à 21 poids évolués : 2e sur 33 (74,2 %), devant un MCTS (72,5 %) et un alpha-bêta (68,3 %) | Non. Leçon : une heuristique réglée pour des decks donnés peut battre une recherche générique |
| Cowling, Powley, Whitehouse, 05/2012 [S12] [lu] | Lord of the Rings: The Confrontation, Phantom (4,4,4), Dou Di Zhu | ISMCTS fait 2 à 4 fois moins d'itérations par seconde que l'UCT déterminisé. Il est un peu moins bon à 1 s, nettement meilleur à partir de 3 s sur LOTR:C. 30 victoires sur 64 parties contre des humains | Principe seulement |
| Cowling, Ward, Powley, 2012 [S13] [lu] | Variante simplifiée de Magic, decks fixes | Ensembles de déterminisations compétitifs avec un joueur expert à règles en moins d'une seconde de calcul | Principe seulement |
| Long et al., AAAI 2010 [S14] [lu] | Arbres synthétiques, Skat, Hearts | La déterminisation (PIMC) marche quand les feuilles voisines ont des valeurs corrélées, et échoue quand les décisions critiques arrivent tard | Grille d'analyse |
| Goodman, 02/2019 [S2] [partiel] | Hanabi, jeu coopératif ; compétition CIG 2018 ; 40 ms par coup | Re-déterminiser l'ISMCTS évite que l'information cachée fuie dans les modèles adverses ; évaluation apprise aux feuilles ; modèle adverse bayésien simple | Principe seulement |
| Xiao et al. (ByteDance), 03/2023 [S19] [lu] | Moteur modifié de Hearthbreaker, règles d'environ avril 2015, 350+ cartes, 3 classes ; 24 GPU V100, 5 856 cœurs CPU, 23 jours | 12 victoires sur 15 parties contre un streamer (Bo5, interface maison, joueur payé) | Non |
| Xia et al., AAMAS 2025 [S20] [lu] | SabberStone (vérifié dans le dépôt : `SabberStoneCore.dll`, `classical_cards.csv`) | GPT-4o jouant seul gagne 60 %, 24 % puis 12 % contre une IA à recherche de niveau 0, 1 et 2 ; l'agent RL + T5 fait 96, 76 et 68 % | Indication : un LLM seul face à une recherche, dans un cadre ancien |
| Karten et al., PokéChamp, ICML 2025 [S21] [lu] | Pokémon Showdown, Gen 9 OU, simulateur local exact, GPT-4o | Minimax guidé par LLM : 76 % contre le meilleur bot LLM, 84 % contre le meilleur bot à règles, contre 44 % pour la recherche seule. Environ un tiers des défaites en classé viennent du dépassement du temps | Patron oui ; il suppose un simulateur exact |
| Epoch AI, SpireClimb, 14/10/2026 [S33] [lu] | Slay the Spire 1 et 2, interface graphique, notes et wiki autorisés | gpt-6.1-sol : 97 % en Ascension 0, 20 % au Cœur en Ascension 20 ; les meilleurs humains sont cités à 80–95 % | Indication sur le modèle même de HS Copilot : fort, pas expert |
| Forge, code d'octobre 2026 [S42] [code] | Moteur complet de Magic (le jeu lui-même) | Pas de mesure publiée. Monte-Carlo plat qui ne départage que les coups approuvés par l'heuristique, et vérificateur de sûreté par simulation | Patron transposable sans simulateur complet, si l'on reste sur ce que le moteur local modélise |

---

## 3. Fiches par approche

Chaque fiche suit le même plan : fonctionnement, hypothèses, applicabilité à Hearthstone 2026, simulateur requis, apport à HS Copilot, complexité, coût d'exécution, limites.

**Contrainte de temps commune.**
- Un tour dure 75 s.
- Le premier fragment du LLM arrive en 2–4 s, la réponse complète en 8–10 s.
- Le joueur joue souvent dès le premier fragment.
- Donc tout ce qui ajoute un aller-retour au modèle avant le premier fragment dégrade l'usage réel. Tout calcul local de quelques millisecondes est gratuit en temps.

### 3.1 Recherche tactique à profondeur limitée : ton tour, puis la riposte visible

- **Fonctionnement.** Minimax tronqué, avec le tour comme unité. Pour chaque état de fin de ton tour, on calcule les **dégâts maximaux que l'adversaire peut t'infliger avec ce qui est visible**, en tenant compte de tes Provocations :
  - serviteurs capables d'attaquer, arme, pouvoir héroïque ;
  - effets de début et de fin de tour lisibles.

  On en tire ta marge de survie, puis la meilleure ligne qui la rend positive.
- **Précédents.**
  - *Świechowski et al. 2018* [S1] [lu]. Le « board solver » vérifie d'abord s'il y a létal ; sinon, si l'adversaire va probablement gagner au tour suivant, il concentre les attaques sur ses serviteurs les plus menaçants ; sinon, il choisit les attaques une à une selon le gain moins la perte de potentiel du plateau. L'ajouter au MCTS améliore nettement le bot.
  - *Silverfish* [S15] [lu].
    - Bot communautaire, version Mont Rochenoire de 2015.
    - Il cherche sur 3 tours : ses séquences de tour, puis un module de modélisation adverse qui énumère des coups typiques comme les sorts de zone et les gros serviteurs.
    - Il choisit ensuite par minimax au niveau des tours.
  - *Forge* [S42] [code]. `OnePlaySafetyChecker` refuse un coup dont la simulation fait baisser le score, sauf si « le prochain combat allait déjà probablement tuer l'IA ». Le dernier commit du paquet (25/10/2026) partage une même prédiction de blocage entre « les deux vérifications de danger ».
- **Hypothèses.**
  - Les dégâts visibles se calculent depuis l'état public : exact pour attaques, armes et pouvoir ; partiel pour les effets textuels.
  - On suppose que l'adversaire ne joue aucune carte nouvelle. C'est une sous-estimation assumée et affichée.
- **Applicable à HS 2026 ?** Oui. Le combat (attaque, PV, Provocation, Bouclier divin, Furie des vents, Gel, Camouflage, Ruée, Charge, Toxicité) est stable depuis le lancement. Les effets hors modèle sont signalés comme inconnues, comme `TurnSolver` le fait déjà.
- **Simulateur complet ?** Non : un modèle partiel du combat et des dégâts directs suffit.
- **Apport à HS Copilot.**
  - Il corrige directement le cas Rexxar : « 25 visibles contre 25 PV + armure ; Glissedrake → Serpent éternel : −7 ».
  - Il est instantané : il s'affiche même quand aucun appel n'aboutit, budget atteint ou appel dépassé par ton coup.
  - Il donne au LLM un fait fiable dans `localAnalysis`.
  - Il permet au validateur d'avertir quand le conseil attaque le visage alors qu'une défense existe.
- **Complexité.** Faible à modérée. Le diagnostic estime environ 200 lignes, plus des tests sur les états de Rexxar et de Tyrande [interne]. On peut réutiliser la logique Provocations / visage de `TurnSolver` en miroir pour l'adversaire. Pièges :
  - Bouclier divin ;
  - serviteurs qui punissent les dégâts, comme le Prédateur en chasse ;
  - Secrets ;
  - « ne peut pas attaquer de héros » ;
  - Furie des vents méga.
- **Coût d'exécution.** Quelques millisecondes.
- **Limites.**
  - Il ne voit ni les cartes en main adverses (15 dégâts cachés contre Tyrande) ni les effets aléatoires. **Une marge positive n'est pas une sécurité** : l'afficher comme « dégâts visibles », avec ses inconnues (cartes en main, cristaux au prochain tour, Secrets).
  - Une alarme sur une position déjà perdue (3 des 4 cas mesurés) n'apporte rien. Le gain attendu se concentre sur les cas où une défense existe encore.

### 3.2 Beam search et exploration des séquences du tour

- **Fonctionnement.** On explore les séquences de ton tour (cartes, cibles, attaques, ordre). Deux options :
  - exhaustif avec mémoïsation tant que l'espace le permet (ce que fait `TurnSolver` pour le visage) ;
  - beam search : à chaque profondeur, on garde les k meilleurs états partiels selon une fonction d'évaluation.

  García-Sánchez et al. rappellent que les agents qui évaluent la séquence complète du tour battent ceux qui évaluent action par action [S3] [lu]. Świechowski et al. citent jusqu'à ~10¹⁰ ordres d'attaque dans le pire cas, d'où leur solveur glouton [S1] [lu].
- **Hypothèses.** Un modèle de transition fiable pour les actions explorées (sinon on optimise un monde faux) et une fonction d'évaluation de la fin de tour.
- **Applicable à HS 2026 ?** Oui pour les sous-espaces bien modélisés : combat, dégâts directs, buffs simples. Non pour les cartes à effets riches (Découverte, Rembobinage, Sombres dons, Lieux variés) : elles doivent rester « non simulées » et passer au LLM.
- **Simulateur complet ?** Non si l'objectif est étroit : dégâts au visage (déjà fait) ou dégâts adverses visibles à minimiser (§3.1). Oui pour optimiser la valeur générale du tour.
- **Apport.**
  - Étendre `TurnSolver` aux **échanges** (attaquer des serviteurs) pour trouver la ligne défensive minimale et son coût offensif.
  - Produire 2 ou 3 **lignes candidates distinctes** (offensive, défensive, mixte) avec leurs chiffres, transmises au LLM et affichées.
- **Complexité.** Modérée. Exhaustif mémoïsé possible avec 7 attaquants au plus et 8 cibles au plus ; beam ou glouton (à la Świechowski) si l'espace grandit.
- **Coût.** De quelques millisecondes à quelques dizaines en C# [hypothèse].
- **Limites.** Le point dur est la fonction d'évaluation générale : valeur des cartes, tempo, menace future. Silverfish la règle à la main avec des experts [S15] ; EVA fait évoluer 21 poids sur des milliers de parties simulées [S3]. Sans simulateur ni données, il faut **garder des objectifs étroits et vérifiables** plutôt qu'un score global inventé.

### 3.3 MCTS (Monte-Carlo Tree Search)

- **Fonctionnement.** UCT : sélection, expansion, simulation (déroulement), rétropropagation. L'algorithme est utilisable à tout moment et s'améliore avec le temps.
- **Résultats publiés en Hearthstone** (tous anciens, voir §2).
  - Świechowski 2018 : voir §2 ; les heuristiques comptent le plus quand le temps est court (0,5 s) [S1].
  - Zhang 2017 : 72,3 % puis 75,3 % contre Silverfish [S15].
  - CIG 2018 : un MCTS finit 3e derrière un glouton optimisé [S3].
- **Hypothèses.** Un **modèle avant** (forward model) complet et rapide, pour toi et pour l'adversaire. Świechowski et al. notent que SabberStone ne simulait qu'environ 200 parties par seconde contre 10 000 pour leur simulateur. Pour la compétition, cela les contraignait à arrêter la recherche à la fin du tour courant [S1] [lu].
- **Applicable à HS 2026 ?** Pas sans simulateur 2026. **Sur ton seul tour, avec des mécaniques déterministes, la recherche exhaustive de `TurnSolver` est exacte et plus rapide : MCTS n'apporte rien.** MCTS ne sert que si l'on modélise de l'aléatoire ou plusieurs tours.
- **Simulateur complet ?** Oui.
- **Apport.** Nul à court terme.
- **Complexité.** L'algorithme est simple. Le coût réel est le simulateur : des centaines à des milliers de scripts de cartes.
- **Coût d'exécution.** 0,5 à 10 s de CPU par coup dans les études : compatible avec 75 s, pas avec un affichage avant le geste du joueur.
- **Limites.** Cartes de 2015–2018, bots adverses, decks fixes : aucune transposition.

### 3.4 ISMCTS, déterminisation (PIMC) et variantes pour l'information cachée

- **Fonctionnement.**
  - *PIMC, ou déterminisation* : on tire des « mondes » compatibles avec ce qu'on sait (main adverse, ordre des decks), on cherche dans chacun comme si tout était visible, puis on agrège. Trois défauts sont connus [S12] [lu] :
    - la fusion de stratégies : on suppose qu'on pourra distinguer des mondes indiscernables ;
    - la non-localité : on oublie que l'adversaire oriente la partie selon sa main ;
    - un budget partagé entre des arbres indépendants.
  - *ISMCTS* : un seul arbre dont les nœuds sont des ensembles d'information, avec une déterminisation par itération (SO-ISMCTS, MO-ISMCTS) [S12].
  - *Variantes* :
    - ensembles de déterminisations, élagage et arbre binaire pour Magic [S13] ;
    - re-déterminisation contre les fuites d'information (Hanabi) [S2] ;
    - hybride PIMC/ISMCTS avec table de transposition indexée par ensembles d'information (Hearthstone 2018), mais déterminisation « tricheuse » pour générer les données d'apprentissage [S1] ;
    - « sparse sampling » (Choe & Kim, CoG 2019) [S38] [partiel].
  - *Forge 2026* [S42] [code] :
    - `HiddenInfoShuffler` redistribue, dans une copie de la partie, les cartes de main et de bibliothèque que l'IA ne peut pas voir.
    - `FlatMonteCarloPicker` ne compare que les **premiers coups déjà approuvés par l'heuristique**, plus l'option d'attendre, jusqu'à la fin du propre tour.
    - Il utilise des nombres aléatoires communs et donne un bonus au choix heuristique.
    - En cas de problème (temps, erreur, ligne non simulable), il revient au choix heuristique.
- **Ce que disent les résultats.**
  - ISMCTS l'emporte quand chercher plus profond compte plus que l'information cachée, et à partir de quelques secondes de calcul [S12].
  - PIMC marche bien quand les feuilles voisines ont des valeurs corrélées [S14].
  - [hypothèse] Hearthstone paraît mixte : beaucoup de positions où le plateau décide (corrélation élevée), mais des cartes cachées qui renversent tout au dernier moment (corrélation faible), comme contre Tyrande.
- **Hypothèses requises.**
  1. Un simulateur complet.
  2. **Une distribution des cartes cachées.** En Hearthstone, la liste du deck adverse est **inconnue** : 30 cartes parmi des milliers. Il faudrait un a priori par archétype, issu de données externes. Forge, lui, connaît les listes des deux joueurs, puisqu'il est le jeu ; il ne mélange que leur répartition entre main et bibliothèque [code]. HS Copilot n'a pas cette information.
- **Applicable à HS 2026 ?** Pas sous sa forme multi-tours.
  - [hypothèse] Une **variante légère** est concevable sur un seul tour adverse. On tire des mains plausibles dans une petite liste de menaces de l'archétype (dégâts directs, nettoyages de plateau). On mesure, pour chaque ligne candidate, la part des mains qui te tuent.
  - Rien ne démontre son intérêt, et la liste devrait être maintenue à chaque extension.
- **Simulateur complet ?** Oui pour la forme classique ; non pour la variante légère limitée aux menaces listées.
- **Apport.** Faible et incertain à effort raisonnable.
- **Complexité.** Élevée pour la forme classique ; modérée pour la variante légère, avec une maintenance continue.
- **Coût d'exécution.** Quelques dizaines de millisecondes pour la variante légère ; des secondes avec un simulateur.
- **Limites.** La qualité dépend entièrement de l'a priori ; la non-localité reste entière ; risque de fausse précision.

### 3.5 Évaluation heuristique d'états

- **Fonctionnement.** Une fonction note un état de fin de tour. Quatre formes connues :
  - combinaison linéaire réglée à la main (Silverfish : variables globales, plateau, main, qualité des actions jouées) [S15] ;
  - poids évolués (EVA : 21 poids, stratégie d'évolution (μ+λ) en coévolution) [S3] ;
  - réseau de valeur appris (750 entrées, 3 couches denses, environ 0,76 à 0,79 de précision pour prédire le vainqueur, sur 3,5 millions d'états de parties entre bots) [S1] ;
  - LLM évaluateur (PokéChamp [S21], LATS [S24]).
- **Hypothèses.** Pour apprendre ou faire évoluer les poids : des millions d'états étiquetés ou des milliers de parties simulées par génération. Pour la forme manuelle : un expert et un banc de test.
- **Applicable à HS 2026 ?** La forme oui, les poids non. Les cartes et le méta changent sans cesse : le rapport vS #359 du 24/09/2026 annonce la prochaine extension et une nouvelle classe au printemps [S4] [lu].
- **Simulateur complet ?** Oui pour apprendre ou faire évoluer les poids. Non pour des critères étroits : marge de survie, dégâts visibles, cartes dépensées, létal au tour suivant.
- **Apport.** Un **petit jeu de critères explicites**, calculés exactement et affichés à côté des lignes candidates. Le jugement de valeur global reste au LLM.
- **Complexité.** Faible pour les critères étroits ; élevée pour une évaluation générale.
- **Coût.** Microsecondes à millisecondes.
- **Limites.** Une évaluation générale mal réglée produit des conseils confiants et faux. 44 parties ne permettent aucun réglage statistique.

### 3.6 Probabilités : tirages et mains adverses

- **Fonctionnement.**
  - *Tirages* : loi hypergéométrique exacte sur le contenu connu de ton deck. `DeckOdds` le fait déjà [code].
  - *Mains adverses* : a priori par archétype, mis à jour par les cartes jouées.
    - Bursztein fait des cooccurrences de paires de cartes sur 50 000 parties de 2014 ; au tour 5, la carte prédite la plus probable a été jouée dans 96,2 % des cas [S36] [lu].
    - Dockhorn et al., 2018 : non lu [S37].
    - Le Data Reaper Report donne fréquences d'archétypes et matchups par rang (1 588 000 parties pour le rapport #359), mais rien sur le contenu des mains [S4] [lu].
- **Hypothèses.** Le deck adverse appartient à un archétype connu ; les données sont récentes.
- **Applicable à HS 2026 ?** Tirages : oui, déjà fait. Mains adverses : partiellement, avec des données externes à jour, ce qui pose accès, conformité et maintenance.
- **Simulateur complet ?** Non.
- **Apport.**
  - Tirages : annoncer les **« outs » décisifs** seulement (« 3 cartes sur 12 te donnent le létal au prochain tour »), puisque le modèle ignore les probabilités générales [code].
  - Adversaire : **le cas Tyrande montre qu'on n'avait pas besoin de deviner sa main.** Des faits publics certains suffisaient : cristaux au prochain tour, 8 cartes en main, effet qui donne 3 cristaux. Mieux vaut des **bornes publiques** que des distributions.
- **Complexité.** Faible pour les outs et les bornes publiques ; élevée pour des a priori de mains à maintenir.
- **Coût.** Négligeable.
- **Limites.** Une probabilité sur la main adverse reste une estimation fragile. La règle stricte impose de ne jamais présenter une estimation comme une carte connue. Un a priori de méta n'est pas une information cachée, mais il doit être étiqueté comme estimation.

### 3.7 Modélisation des réponses adverses

- **Fonctionnement, par niveaux.**
  - **N0, pire cas visible** : voir §3.1.
  - **N1, ressources publiques** : cristaux au prochain tour, taille de la main, Secrets, cartes révélées.
  - **N2, menaces typiques** : comme le module adverse de Silverfish, qui génère sorts de zone et gros serviteurs [S15].
  - **N3, profils de continuation** : chez Pluribus, à la limite de profondeur, chaque joueur peut passer à l'une de k = 4 stratégies (stratégie de base, ou versions biaisées vers se coucher, suivre ou relancer), ce qui rend le plan robuste au lieu d'être optimisé contre un seul adversaire supposé [S29] [lu].
  - **N4, modèle appris ou LLM** :
    - PokéChamp fait prédire au LLM les actions adverses et les statistiques cachées, à partir des statistiques d'usage de Showdown [S21] [lu] ;
    - CICERO prédit les coups des autres joueurs puis planifie (piKL) [S31] [partiel].
- **Hypothèses.** N0 et N1 : aucune. N2 et N3 : une liste de menaces par classe et par coût. N4 : une connaissance à jour du méta.
- **Applicable à HS 2026 ?** N0 et N1 : oui, ce sont des règles locales sûres. N2 : oui, mais il faut maintenir la table à chaque extension. N3 : oui, sous forme d'heuristique. N4 : discutable, car PokéChamp attribue en partie la faiblesse de sa variante rapide à l'écart entre le méta et la date de coupure du LLM [S21].
- **Simulateur complet ?** Non pour N0 à N3. Oui pour un vrai minimax sur le tour adverse.
- **Apport.**
  - N0 + N1 corrigent les deux défaites évitables.
  - N3, transposé [hypothèse] : évaluer chaque ligne candidate contre trois profils (visible seul ; visible + X dégâts depuis la main ; visible + nettoyage du plateau). Préférer la ligne au meilleur pire cas quand le coût offensif est faible. Le DIAGNOSTIC le chiffre déjà : « Rexxar reste à 14 au lieu de 8 ».
- **Complexité.** Faible pour N0 et N1 ; modérée pour N2 et N3 ; élevée pour N4.
- **Coût d'exécution.** Millisecondes pour N0 à N3. N4 demande un appel au LLM.
- **Limites.** Risque de jouer trop prudent (perte de tempo) : toujours chiffrer le coût de la défense. La non-localité demeure.

### 3.8 Combinaison moteur déterministe + LLM (planification hybride)

- **Patrons possibles.**
  - **(a) Faits précalculés dans le prompt** : ce que fait HS Copilot aujourd'hui.
  - **(b) Générer puis vérifier (« LLM-Modulo »)** : le LLM propose, des vérificateurs fondés sur un modèle critiquent ou bloquent. Les auteurs soutiennent qu'un LLM ne planifie ni ne se vérifie seul, mais aide dans une boucle avec vérificateurs externes [S22] [partiel].
  - **(c) LLM générateur de candidats et évaluateur dans une recherche** : PokéChamp [S21] [lu].
  - **(d) La recherche locale génère les candidats, le LLM choisit** : patron proche de Forge, où le simulateur ne départage que des coups déjà proposés [S42].
  - **(e) Le LLM appelle le solveur comme outil.**
  - **(f) Le LLM écrit le modèle du monde en code** (Code World Models, DeepMind, 10/2025 : 10 jeux dont 5 à information imparfaite, égal ou meilleur que Gemini 2.5 Pro sur 9) [S23] [partiel].
  - **(g) Recherche arborescente sur les pensées du LLM** (Tree of Thoughts : 4 % → 74 % au Jeu de 24, 2023 [S25] ; LATS, 2023 [S24]) [partiel].
- **Résultats dans les jeux.**
  - PokéChamp (2025) : la recherche guidée par le LLM bat la recherche seule (84 % contre 44 % face au même bot à règles). Mais elle repose sur un simulateur Showdown exact, et le temps de jeu cause environ un tiers des défaites [S21].
  - Hearthstone sur SabberStone : un LLM qui joue seul s'effondre face à une recherche plus profonde (12 %) [S20].
  - CICERO (2022) : en 40 parties sur webDiplomacy, plus du double du score humain moyen, dans les 10 % supérieurs [S31] [partiel].
- **Hypothèses.** (a), (b), (d) : un moteur exact sur ce qu'il prétend calculer et honnête sur le reste, ce qui est déjà le principe de `TurnSolver`. (c) et (g) : un simulateur fiable sur tout l'horizon de recherche. (e) : du temps pour des allers-retours.
- **Applicable à HS 2026 ?**
  - **(b) et (d)** s'insèrent directement dans l'architecture.
  - **(e)** ajoute des allers-retours, car chaque appel d'outil relance le modèle. C'est incompatible avec le premier fragment ; il vaut mieux tout précalculer localement et l'injecter.
  - **(c)** exige un simulateur sur l'horizon de recherche, qui n'existe que pour le combat.
  - **(f)** est une piste de long terme : scripts de cartes écrits par LLM, vérifiés contre les vrais Power.log.
- **Simulateur complet ?** Non pour (a), (b), (d) et (e). Oui pour (c) et (g) sur plusieurs tours ; (f) vise justement à en construire un.
- **Apport concret.**
  - Avertissements locaux sur le plan : létal visible laissé, effet symétrique joué, ressource donnée à l'adversaire.
  - Lignes candidates chiffrées dans `localAnalysis`.
  - Affichage des avertissements **au joueur**, pas seulement dans le prompt : contre Tyrande, le LLM n'a pas tenu compte du texte de la carte [interne].
- **Complexité.** Faible à modérée pour (b) et (d) ; élevée pour (c) et (f).
- **Coût.** (b) et (d) : millisecondes en local, aucun appel supplémentaire. (c) : N appels par décision, soit des secondes à des dizaines de secondes, et un coût multiplié par N.
- **Limites.** Le vérificateur ne vérifie que ce qu'il modélise. Il faut éviter que des avertissements trop fréquents soient ignorés : mesurer les fausses alarmes (§6).

### 3.9 Évaluation par plusieurs modèles

- **Fonctionnement.**
  - Auto-cohérence : vote sur plusieurs raisonnements échantillonnés (+17,9 points sur GSM8K, problèmes de mathématiques, 2022) [S26] [partiel].
  - Panel de juges de familles différentes (PoLL) : moins de biais intra-modèle, plus de 7 fois moins cher qu'un grand juge unique (2024) [S28] [partiel].
  - Divergence entre modèles utilisée comme signal d'incertitude.
- **Hypothèses.** Des modèles assez divers pour que leurs erreurs ne soient pas corrélées, et soit du temps pour attendre plusieurs réponses (en direct), soit une étape hors ligne.
- **Applicable à HS 2026 ?** Oui hors ligne. Mal en direct (voir ci-dessous).
- **Simulateur complet ?** Non.
- **Données internes** [interne].
  - Même modèle, même effort : 97 % de mêmes actions.
  - Low contre high : 81 %.
  - Luna contre Sol : 46 à 54 %.
  - **Voter avec le même modèle n'apporte presque aucune diversité ; un modèle plus faible ajoute surtout du bruit** (luna prend 78 % des létaux, contre 100 % pour Sol).
- **En direct.** N appels multiplient le coût par N, et le résultat n'est connu qu'à l'arrivée du plus lent. C'est incompatible avec un joueur qui agit au premier fragment.
- **Apport.** Hors ligne : étiqueter le banc de décisions avec un panel de modèles, puis revue humaine des désaccords.
- **Complexité.** Faible.
- **Coût d'exécution.** Coût multiplié par le nombre de modèles ; latence du plus lent.
- **Limites.** L'accord n'est pas la justesse, et les juges LLM ont leurs biais. Rien n'est démontré en Hearthstone.

### 3.10 Optimisation adaptative du temps de calcul

- **Fonctionnement.**
  - Allouer plus de calcul aux décisions difficiles. Snell et al. (2024) montrent qu'une allocation adaptative à la difficulté est plus de 4 fois plus efficace qu'un « best-of-N », sur des tâches de raisonnement avec vérificateur [S27] [partiel].
  - Guide OpenAI [S6] [lu] :
    - `low` pour l'exécution, `medium` par défaut, `high`/`xhigh` pour le raisonnement difficile, après évaluation ;
    - un préambule court accélère le premier jeton visible ;
    - un élément `configuration_update` change l'effort entre deux réponses (famille GPT-6) ;
    - GPT-6.1 Sol n'accepte pas `none` ni `minimal`.
- **Hypothèses.** Un signal de difficulté fiable avant l'appel, et un joueur prêt à attendre quand la décision le mérite.
- **Applicable à HS 2026 ?** Oui, le signal de difficulté peut venir du moteur local :
  - létal certain : aucun appel (déjà fait) ;
  - **décision critique** : marge de danger faible ou négative, carte à effet symétrique en main, cartes non simulées décisives, Secret adverse, Découverte ou Rembobinage ;
  - sinon, **routine**.
- **Simulateur complet ?** Non.
- **Apport** [hypothèse].
  - Routine : `low`. Sur le banc, il donne la même décision que `medium` dans 86 % des cas, avec une médiane de 9,8 s au lieu de 11,9 s.
  - Critique : afficher d'abord le fait local (« Danger : 25 visibles contre 25 PV »), puis lancer `medium` ou `high` avec la mention « décision critique : attends l'analyse complète ».
- **Complexité.** Faible côté code.
- **Coût.** `high` coûte environ 0,026 $ contre 0,017 $ [interne]. Attention : l'effort fait partie du préfixe mis en cache [S7] ; alterner les efforts peut réduire la part servie depuis le cache. À mesurer.
- **Limites.** **Aucune preuve que `high` décide mieux en Hearthstone**, faute de vérité terrain. Un joueur qui agit au premier fragment n'attendra pas 30 à 60 s : le gain dépend surtout de l'interface.

### 3.11 Latence OpenAI

- **Fonctionnement : leviers documentés** [S5] [lu].
  - Générer moins de jetons : « réduire de 50 % les jetons de sortie peut réduire la latence d'environ 50 % ».
  - Envoyer moins de jetons : gain faible, de l'ordre de 1 à 5 %.
  - Faire moins de requêtes.
  - Paralléliser, ou exécuter de façon spéculative.
  - Diffuser en flux : déjà fait.
  - **Ne pas utiliser un LLM quand une règle suffit** : déjà fait pour le létal local.
- **Niveaux de service.**

| Niveau (gpt-6.1-sol, par million de jetons) | Entrée | Entrée en cache | Sortie | Coût estimé par appel [hypothèse] | Gain annoncé |
|---|---|---|---|---|---|
| Standard (actuel) | 2,00 $ | 0,10 $ | 10,00 $ | ≈ 0,017 $ | — |
| Fast (ex-Priority, renommé le 30/07/2026) | 4,00 $ | 0,20 $ | 20,00 $ | ≈ 0,034 $ | « jusqu'à 2,5× plus rapide », chiffre donné pour gpt-5.6-sol |
| Ultrafast | 12,00 $ | 0,60 $ | 60,00 $ | ≈ 0,10 $ | « le plus rapide », sans chiffre ; WebSocket recommandé ; limites séparées |
| Batch/Flex | 1,00 $ | 0,05 $ | 5,00 $ | ≈ 0,0085 $ | asynchrone, inutile en direct |

  - Sources : [S8], [S9], [S10] [lu] et [S11] [lu, secondaire]. GPT-6.1 Sol est listé pour Fast et Ultrafast.
  - **Aucun chiffre de temps au premier jeton n'est publié pour GPT-6.1 Sol.**
- **Cache.**
  - La lecture coûte 0,05 fois le prix d'entrée pour GPT-6.1 Sol, l'écriture coûte plus. Le préfixe doit être identique : modèle, outils, format, effort [S7] [lu].
  - Mesure interne : au 2e passage du banc, 8 136 jetons sur 8 253 venaient du cache, pour 0,0062 $ au lieu de 0,017 $ [interne].
  - Aucun gain de latence n'est chiffré par OpenAI.
- **Hypothèses.** Les gains annoncés par le fournisseur valent pour GPT-6.1 Sol et pour la charge de HS Copilot ; rien ne le démontre encore.
- **Applicable à HS 2026 ?** Oui : ce sont des réglages d'API, indépendants du jeu.
- **Simulateur complet ?** Non.
- **Apport** [hypothèse]. L'appel le plus rapide peut éviter les appels dépassés par le coup suivant, ce qui est arrivé contre Rexxar. Recommandations :
  - mesurer premier fragment et réponse complète en Fast sur un petit banc, par exemple 36 cas × 0,034 $ ≈ 1,2 $ ;
  - raccourcir les champs de sortie qui suivent `action` ;
  - garder un préfixe stable (instructions et stratégie en tête, état à la fin).
- **Complexité.** Très faible : un paramètre `service_tier`, plus le raccourcissement des champs de sortie.
- **Coût d'exécution.** Fast double le prix par appel ; Ultrafast le multiplie par six.
- **Limites.** Ce sont des promesses du fournisseur, à mesurer. **Les défaites observées ne viennent pas d'abord de la latence du LLM, mais de l'absence de calcul local du danger.**

### 3.12 Apprentissage par renforcement, auto-jeu, ajustement fin

- **Fonctionnement.**
  - Entraîner une politique par auto-jeu (RL). ByteDance (2023) utilise un moteur Hearthbreaker modifié, des règles d'avril 2015, 24 GPU V100 et 5 856 cœurs pendant 23 jours [S19] [lu].
  - Encoder le texte des cartes pour généraliser à de nouvelles cartes : T5 + RL sur SabberStone (AAMAS 2025) [S20] [lu].
  - Ajuster finement un LLM sur des parties de haute qualité : il « approche » les IA fortes sur 8 jeux de cartes (NeurIPS 2025) [S35] [partiel].
- **Hypothèses.** Simulateur, calcul massif, données de qualité.
- **Applicable à HS 2026 ?** Non pour HS Copilot. Les cartes changent plus vite qu'un cycle d'entraînement, et 44 parties ne constituent pas des données.
- **Simulateur complet ?** Oui.
- **Apport.** Nul à court terme.
- **Complexité.** Très élevée : simulateur, infrastructure d'entraînement, collecte de données.
- **Coût d'exécution.** Faible une fois entraîné ; l'entraînement est le coût prohibitif.
- **Limites.** Le moindre changement de cartes invalide une partie de l'apprentissage.

---

## 4. Tableau comparatif

| Approche | Simulateur complet ? | Effort pour HS Copilot | Coût dans un tour de 75 s | Apport attendu | Niveau de preuve |
|---|---|---|---|---|---|
| Danger visible au tour suivant + ligne défensive (§3.1) | non | faible à modéré | ms, sans appel | **élevé** (Rexxar) | interne (4/4, 0 fausse alarme) + précédents [S1, S15, S42] |
| Garde-fous d'effets publics et de ressources adverses (§3.7 N1, §3.8 b) | non | faible | ms | **élevé** (Tyrande) | cas unique, mécanisme public et certain |
| Lignes candidates locales, le LLM choisit (§3.2, §3.8 d) | non (combat seul) | modéré | ms + même appel | moyen | patron Forge [S42], non mesuré en HS |
| Routage de l'effort par drapeaux locaux (§3.10) | non | faible | baisse en routine | moyen (latence) | banc interne : peu de changements de décision |
| Fast mode, sorties plus courtes (§3.11) | non | très faible | ×2 par appel en Fast | moyen (moins d'appels dépassés) | promesses du fournisseur, à mesurer |
| Outs de pioche décisifs (§3.6) | non | faible | négligeable | faible à moyen | fondement exact ; usage par le modèle non démontré |
| Profils adverses à la Pluribus (§3.7 N3) | non | modéré | ms | incertain | poker 2019 [S29] ; non démontré en HS |
| Déterminisation légère du tour adverse (§3.4) | non (liste de menaces) | modéré + maintenance | ms à dizaines de ms | incertain | non démontré |
| Vote entre plusieurs modèles en direct (§3.9) | non | faible | coût ×N, latence du plus lent | faible | banc interne : 97 % d'accord même modèle |
| Panel de juges hors ligne (§3.9) | non | faible | hors ligne | moyen (mesure) | [S28] hors jeux |
| MCTS / ISMCTS multi-tours (§3.3, §3.4) | **oui** | très élevé | 0,5–10 s CPU | potentiellement élevé | anciens environnements seulement |
| Réseau de valeur / poids évolués (§3.5) | **oui** (pour apprendre) | très élevé | µs | inconnu | anciens environnements |
| RL / auto-jeu (§3.12) | **oui** | hors de portée | — | — | 2015–2019 seulement |

---

## 5. Classement argumenté

### A. Applicable à HS Copilot avec un effort raisonnable (par ordre de priorité)

1. **Module local « danger au prochain tour » et ligne défensive minimale.**
   - Pourquoi en premier : il vise exactement la défaite évitable la plus nette (Rexxar).
   - Il tourne en millisecondes, sans appel ni budget, et se teste hors ligne sur toutes les fins de tour enregistrées. La mesure interne montre déjà une alarme fiable (4 sur 4, aucune fausse alarme sur 291 fins de tour).
   - Il a des précédents directs en Hearthstone : le solveur de Świechowski (2018), qui vérifie la menace adverse au tour suivant [S1], et le module adverse de Silverfish [S15].
   - Calcul conseillé : sur l'état actuel **et** sur l'état après la séquence conseillée, avec tes Provocations et le pouvoir héroïque adverse.
2. **Garde-fous sur l'information publique : effets symétriques et ressources adverses.**
   - Repérer dans le texte des cartes de ta main ce qui profite aussi à l'adversaire (« chaque joueur », « les deux joueurs », cristaux, arme donnée) et le chiffrer : « Nozdormu : l'adversaire passera de 7 à 10 cristaux, avec 8 cartes en main ».
   - Afficher les bornes publiques : cristaux au prochain tour, cartes en main, Secrets.
   - Le validateur avertit quand le conseil déclenche un tel effet sans contrepartie claire.
   - C'est le patron « générer puis vérifier » [S22], analogue au `OnePlaySafetyChecker` de Forge [S42].
3. **Vérification du plan dès le premier fragment.**
   - Quand l'action conseillée arrive, le moteur local vérifie en quelques millisecondes qu'il reste une suite gardant une marge visible positive.
   - Sinon, il avertit avant que le joueur n'agisse. Quand la suite du plan (`followUp`) arrive, il vérifie l'état final.
   - Aucun appel supplémentaire.
4. **Lignes candidates calculées localement.**
   - Étendre `TurnSolver` aux échanges pour fournir 2 ou 3 lignes chiffrées (offensive, défensive, mixte), avec leur coût. Le LLM choisit parmi elles ou justifie un autre choix.
   - Le moteur reste borné à ce qu'il simule exactement ; le reste est marqué « non simulé ».
5. **Routage du calcul et latence.**
   - Aucun appel quand le moteur suffit (déjà fait pour le létal).
   - `low` pour la routine ; `medium` ou `high` seulement pour les décisions marquées critiques, avec le fait local affiché d'abord.
   - Mesurer Fast mode avant de l'adopter ; raccourcir les champs de sortie ; garder le préfixe stable.
6. **Banc de décisions et panel de juges hors ligne.** C'est la seule façon de mesurer les points 1 à 5 sans un millier de parties (§6).

### B. Exige un simulateur complet : probablement hors de portée

- **Concerné** : MCTS et ISMCTS sur plusieurs tours, déterminisation multi-tours, expectimax sur le vrai tour adverse, réseaux de valeur, poids évolués, RL et auto-jeu.
- **Pourquoi c'est hors de portée :**
  1. **Aucun simulateur à jour n'existe** [lu, API GitHub consultée le 26/10/2026] :
     - SabberStone : README du 23/07/2019 (98 % du Standard de l'Année du Dragon), dernier envoi le 08/12/2022 [S39] ;
     - Fireplace : README au patch 17.6 (2020), commits de décembre 2025 consacrés à Ashes of Outland (2020) [S40] ;
     - MetaStone : dernier envoi le 17/08/2018 [S41].
  2. **Le coût de scripter les cartes est énorme et permanent.** Les chercheurs ont implémenté 483 cartes [S1] et 732 classes de cartes [S15] pour des ensembles de 2015 à 2018. Hearthstone 2026 ajoute plusieurs extensions par an, des mini-ensembles et des équilibrages, avec des mécaniques comme Rembobinage, Sombres dons et Parenté.
  3. **Le Power.log est un observateur, pas un modèle avant** : il décrit ce qui s'est passé, il ne calcule pas ce qui se passerait.
  4. **Le deck adverse est inconnu** : même Forge, qui est le jeu, connaît les listes [S42]. Il faudrait des a priori d'archétypes maintenus.
  5. **Il n'existe aucune donnée pour régler ou entraîner** : 44 parties.
- Les gains publiés (+9 points en premier joueur, de 26,5 à 56,1 % en second, 72–75 % contre Silverfish) ont été mesurés entre bots, sur des cartes de 2015 à 2018. Ils ne disent rien de Hearthstone 2026.

### C. Prometteur, mais non démontré

- **Recherche guidée par LLM sur le modèle partiel** (façon PokéChamp) : le patron marche en Pokémon parce que Showdown fournit un simulateur exact [S21]. Ici, il ne vaudrait que sur l'horizon modélisé (combat).
- **Profils de continuation adverses à la Pluribus** pour choisir des lignes robustes [S29].
- **Déterminisation légère du seul tour adverse**, à partir d'une liste de menaces par archétype : à maintenir à chaque extension.
- **LLM comme modèle de l'adversaire** (« menaces plausibles à N cristaux ») : risque de connaissances périmées [S21].
- **Scripts de cartes écrits par le LLM et vérifiés contre les logs réels** (Code World Models) [S23], pour agrandir peu à peu le modèle local.
- **Ultrafast mode** et **réutilisation du raisonnement entre les appels d'un même tour** (`reasoning.context`, `previous_response_id`) [S6, S9] : aucun chiffre pour ce cas d'usage.
- **Effort élevé sur les décisions critiques, avec une interface qui demande d'attendre.**

---

## 6. Comment mesurer sans 1 200 parties par variante

- **L'unité est la décision, pas la partie.**
  - On rejoue les états enregistrés hors ligne, sans appel, pour les modules locaux.
  - Le dépôt contient déjà 582 cas (`bench/cases.jsonl`) et les fins de tour de toutes les parties.
- **Étiquettes automatiques et vérifiables, par décision :**
  - marge visible après le coup conseillé et après le coup joué ;
  - létal manqué ;
  - effet symétrique déclenché ;
  - coût offensif de la meilleure défense.
- **Métriques :**
  1. nombre de fins de tour avec un létal adverse visible **évitable** ; objectif : 0 ;
  2. part des décisions où un conseil, local ou du modèle, est affiché **avant** l'action du joueur ;
  3. fausses alarmes : avertissements dans les parties gagnées, et coups gagnants signalés ;
  4. sur les seuls cas critiques, avis d'un panel de modèles différents, puis revue humaine des désaccords [S28]. C'est une étiquette faible, jamais une preuve.
- **Appels payants** : seulement sur de petits bancs ciblés (≈ 1 à 3 $) pour comparer un réglage, par exemple Fast contre Standard, ou `low` contre `medium` sur les cas de routine.

---

## 7. Limites de cette étude

- **Lu en entier :** [S1], [S3], [S12] (extraits longs), [S13] (résumé et conclusion), [S14], [S15], [S19] (version HTML), [S20], [S21] (version HTML), [S29], [S33], [S36], les pages OpenAI et le code de Forge.
- **Résumés seulement :** [S2], [S16], [S17], [S18], [S22]–[S28], [S30]–[S32], [S35], [S38]. Les articles IEEE de Zhang & Buro 2017 et de Choe & Kim 2019 n'ont pas été lus ; le premier est couvert par le mémoire [S15].
- **Non lu :** Dockhorn et al. 2018 [S37], seulement la référence bibliographique.
- Les graphiques du Data Reaper Report n'étaient pas lisibles : je n'ai ni la fréquence ni le taux de victoire du Guerrier Dragons [S4].
- **Forge :** j'ai lu les en-têtes des classes de simulation, pas de mesure de leur efficacité.
- **Estimations personnelles, marquées [hypothèse] :**
  - le calcul de puissance statistique ;
  - le coût par appel en Fast et en Ultrafast (8 250 jetons d'entrée, environ 500 en sortie, part de cache comme en production) ;
  - les temps de calcul des modules locaux ;
  - l'intérêt des variantes « légères ».
- Les mesures internes (banc des efforts, diagnostic des défaites) viennent du dépôt. Je ne les ai pas refaites.

---

## 8. Sources

Consultées entre le 26/10/2026 et la rédaction, sauf mention contraire.

**Références de départ**
- [S1] M. Świechowski, T. Tajmajer, A. Janusz, « Improving Hearthstone AI by Combining MCTS and Supervised Learning Algorithms », arXiv:1808.04794 v1, 14/08/2018 ; IEEE CIG 2018, p. 445-452. https://arxiv.org/abs/1808.04794 — **lu** (PDF intégral).
- [S2] J. Goodman, « Re-determinizing Information Set Monte Carlo Tree Search in Hanabi », arXiv:1902.06075, v1 16/02/2019, v2 14/05/2019. https://arxiv.org/abs/1902.06075 — **partiel** (page de résumé). L'article porte sur Hanabi, pas sur la recherche en information imparfaite en général.
- [S3] P. García-Sánchez, A. Tonda, A. J. Fernández-Leiva, C. Cotta, « Optimizing Hearthstone Agents using an Evolutionary Algorithm », Knowledge-Based Systems 188 (2020) 105032 ; arXiv:2410.19681, 25/10/2024. https://arxiv.org/abs/2410.19681 — **lu** (PDF intégral).
- [S4] vicious Syndicate, page « Data Reaper Report » (consultée le 26/10/2026), https://www.vicioussyndicate.com/drr/ — **lu** ; et « vS Data Reaper Report #359 », 24/09/2026, https://www.vicioussyndicate.com/vs-data-reaper-report-359/ — **lu** (texte ; graphiques non lisibles).
- [S5] OpenAI, « Latency optimization », https://developers.openai.com/api/docs/guides/latency-optimization — **lu** ; page non datée.
- [S6] OpenAI, « Reasoning models », https://developers.openai.com/api/docs/guides/reasoning — **lu** ; page non datée.

**OpenAI : autres pages**
- [S7] OpenAI, « Prompt caching », https://developers.openai.com/api/docs/guides/prompt-caching — **lu** ; non datée.
- [S8] OpenAI, « Fast mode », https://developers.openai.com/api/docs/guides/fast-mode — **lu** ; mentionne le renommage du 30/07/2026.
- [S9] OpenAI, « Ultrafast mode », https://developers.openai.com/api/docs/guides/ultrafast-mode — **lu** ; non datée.
- [S10] OpenAI, « Pricing », https://developers.openai.com/api/docs/pricing — **lu** ; consulté le 26/10/2026.
- [S11] LiteLLM, « OpenAI Fast & Ultrafast mode », https://docs.litellm.ai/docs/providers/openai/ultrafast — **lu** (source secondaire ; vérification annoncée le 06/10/2026).

**Information imparfaite, MCTS**
- [S12] P. I. Cowling, E. J. Powley, D. Whitehouse, « Information Set Monte Carlo Tree Search », IEEE TCIAIG 4(2):120-143, publié le 22/05/2012. https://eprints.whiterose.ac.uk/id/eprint/75048/ — **lu** (PDF : résumé, expériences de temps de calcul, conclusion).
- [S13] P. I. Cowling, C. D. Ward, E. J. Powley, « Ensemble Determinization in Monte Carlo Tree Search for the Imperfect Information Card Game Magic: The Gathering », IEEE TCIAIG 4(4):241-257, 2012. https://eprints.whiterose.ac.uk/75050/ — **lu** (résumé et conclusion).
- [S14] J. Long, N. Sturtevant, M. Buro, T. Furtak, « Understanding the Success of Perfect Information Monte Carlo Sampling in Game Tree Search », AAAI 2010, p. 134-140. https://ojs.aaai.org/index.php/AAAI/article/view/7562 — **lu** (PDF).

**Hearthstone**
- [S15] S. Zhang, « Improving Collectible Card Game AI with Heuristic Search and Machine Learning Techniques », mémoire de master, University of Alberta, 11/2017. https://ualberta.scholaris.ca/handle/123456789/89399 — **lu** (PDF).
- [S16] S. Zhang, M. Buro, « Improving Hearthstone AI by Learning High-Level Rollout Policies and Bucketing Chance Node Events », IEEE CIG 2017, p. 309-316. https://doi.org/10.1109/CIG.2017.8080452 — **partiel** (contenu couvert par [S15]).
- [S17] A. Dockhorn, S. Mostaghim, « Introducing the Hearthstone-AI Competition », arXiv:1906.04238, 06/05/2019. https://arxiv.org/abs/1906.04238 — **partiel**.
- [S18] A. K. Hoover, J. Togelius, S. Lee, F. de Mesentier Silva, « The Many AI Challenges of Hearthstone », arXiv:1907.06562, 15/07/2019. https://arxiv.org/abs/1907.06562 — **partiel**.
- [S19] C. Xiao et al. (ByteDance), « Mastering Strategy Card Game (Hearthstone) with Improved Techniques », arXiv:2303.05197, 09/03/2023 (v2 du 28/05/2023). https://arxiv.org/abs/2303.05197 — **lu** (version HTML ar5iv).
- [S20] W. Xia, M. Fang, Z. Guo, Y. Du, B. Xu, « Integrating Large Language Models with Reinforcement Learning for Generalization in Strategic Card Games », AAMAS 2025 (résumé étendu), https://aamas.csc.liv.ac.uk/Proceedings/aamas2025/pdfs/p2795.pdf — **lu** ; dépôt https://github.com/WannianXia/GeneralizableHS (code consulté le 26/10/2026 : `SabberStoneCore.dll`, `classical_cards.csv`).

**LLM, planification, jeux**
- [S21] S. Karten, A. L. Nguyen, C. Jin, « PokéChamp: an Expert-level Minimax Language Agent », arXiv:2503.04094, 06/03/2025 ; ICML 2025. https://arxiv.org/html/2503.04094v1 — **lu** (HTML v1).
- [S22] S. Kambhampati et al., « Position: LLMs Can't Plan, But Can Help Planning in LLM-Modulo Frameworks », ICML 2024 ; arXiv:2402.01817, 02/2024. https://arxiv.org/abs/2402.01817 — **partiel**.
- [S23] W. Lehrach et al., « Code World Models for General Game Playing », arXiv:2510.04542, 06/10/2025. https://arxiv.org/abs/2510.04542 — **partiel**.
- [S24] A. Zhou et al., « Language Agent Tree Search Unifies Reasoning Acting and Planning in Language Models », arXiv:2310.04406, 06/10/2023. https://arxiv.org/abs/2310.04406 — **partiel**.
- [S25] S. Yao et al., « Tree of Thoughts: Deliberate Problem Solving with Large Language Models », arXiv:2305.10601, 17/05/2023. https://arxiv.org/abs/2305.10601 — **partiel**.
- [S26] X. Wang et al., « Self-Consistency Improves Chain of Thought Reasoning in Language Models », arXiv:2203.11171, 21/03/2022 (ICLR 2023). https://arxiv.org/abs/2203.11171 — **partiel**.
- [S27] C. Snell, J. Lee, K. Xu, A. Kumar, « Scaling LLM Test-Time Compute Optimally can be More Effective than Scaling Model Parameters », arXiv:2408.03314, 06/08/2024. https://arxiv.org/abs/2408.03314 — **partiel**.
- [S28] P. Verga et al., « Replacing Judges with Juries: Evaluating LLM Generations with a Panel of Diverse Models », arXiv:2404.18796, 29/04/2024. https://arxiv.org/abs/2404.18796 — **partiel**.

**Poker, Diplomacy, jeux de cartes**
- [S29] Meta AI, « Facebook, Carnegie Mellon build first AI that beats pros in 6-player poker » (Pluribus), 11/07/2019. https://ai.meta.com/blog/pluribus-first-ai-to-beat-pros-in-6-player-poker/ — **lu**.
- [S30] M. Schmid et al., « Student of Games: A unified learning algorithm for both perfect and imperfect information games », Science Advances 9(46) eadg3256, 15/11/2023 ; https://arxiv.org/abs/2112.03178 — **partiel** (cité pour le contexte, non utilisé dans les conclusions).
- [S31] Meta AI, CICERO, https://ai.meta.com/research/cicero/ ; billet du 22/11/2022, https://ai.meta.com/blog/cicero-ai-negotiates-persuades-and-cooperates-with-people/ — **partiel**.
- [S32] R. Zhuang et al., « PokerBench: Training Large Language Models to become Professional Poker Players », AAAI 2025 ; arXiv:2501.08328. https://arxiv.org/abs/2501.08328 — **partiel** (via moteur de recherche ; conclusion : les LLM sont nettement en deçà du jeu optimal ; cité pour le contexte).
- [S33] B. Ou, S. Chen (Epoch AI), « AI systems are improving rapidly at Slay the Spire and its sequel », 14/10/2026. https://epoch.ai/publications/ai-systems-are-improving-rapidly-at-slay-the-spire-and-its-sequel — **lu**.
- [S34] MageBench Season 1, instantané du 28/05/2026, https://benchmarklist.com/benchmarks/mage_bench_season1/ — **lu** (source secondaire ; indicateurs incohérents relevés par la page elle-même ; cité pour le contexte).
- [S35] W. Wang et al., « Can Large Language Models Master Complex Card Games? », NeurIPS 2025. https://neurips.cc/virtual/2025/poster/117059 — **partiel**.
- [S36] E. Bursztein, « Predicting a Hearthstone opponent's deck using machine learning », 08/2014, révisé 10/2016. https://elie.net/blog/hearthstone/predicting-hearthstone-opponent-deck-using-machine-learning/ — **lu**. La page contenait une instruction adressée aux agents, ignorée.
- [S37] A. Dockhorn, M. Frick, Ü. Akkaya, R. Kruse, « Predicting Opponent Moves for Improving Hearthstone AI », IPMU 2018, DOI 10.1007/978-3-319-91476-3_51 — **non lu**.
- [S38] J. S. B. Choe, J.-K. Kim, « Enhancing Monte Carlo Tree Search for Playing Hearthstone », IEEE CoG 2019. https://pure.korea.ac.kr/en/publications/enhancing-monte-carlo-tree-search-for-playing-hearthstone/ — **partiel**.

**Simulateurs et moteurs**
- [S39] SabberStone, https://github.com/HearthSim/SabberStone — **lu** (README ; API GitHub du 26/10/2026 : `pushed_at` 2022-12-08, dernier commit de la branche principale 2021-01-15, une mise à jour du README).
- [S40] Fireplace, https://github.com/jleclanche/fireplace — **lu** (README « Patch 17.6.0.53261 » ; API GitHub : `pushed_at` 2025-12-19, commits récents « outland »).
- [S41] MetaStone, https://github.com/demilich1/metastone — **lu** (README ; API GitHub : `pushed_at` 2018-08-17).
- [S42] Forge (Magic: The Gathering), https://github.com/Card-Forge/forge — **code lu** le 26/10/2026 : `forge-ai/src/main/java/forge/ai/simulation/` (`FlatMonteCarloPicker.java`, `HiddenInfoShuffler.java`, `OnePlaySafetyChecker.java`), dernier commit du paquet le 25/10/2026 ; wiki https://github.com/Card-Forge/forge/wiki/ai (révision du 23/10/2026).

**Sources internes au dépôt** [interne]
- `docs/DIAGNOSTIC-DEFAITES.md` : défaites des 25-26/10/2026, mesure sur 291 fins de tour.
- `bench/runs/efforts-36/report.md` : 36 cas, 25/10/2026.
- `bench/runs/xhigh-divergents/report.md` : 8 cas, 25/10/2026.
- `src/HsCopilot.Core/Strategy/TurnSolver.cs`, `QuickAnalysis.cs`, `DeckOdds.cs`, `Providers/OpenAiProvider.cs` et `src/HsCopilot.Core/Evaluation/DubiousMoveDetector.cs` : code lu le 26/10/2026.
