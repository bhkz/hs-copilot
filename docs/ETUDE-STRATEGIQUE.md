# Étude stratégique : jusqu'où HS Copilot peut-il progresser ?

Étude du 26/10/2026, sur les 44 parties classées jouées avec les conseils (39 victoires, 5 défaites) :
- 6 en Libre, 38 en Standard, dont 9 parties du matin du 26/10, toutes gagnées ;
- les doublons ont été écartés : renvois de partie au démarrage d'une session, coupures de la partie de 0 h 21.

Les dossiers et données de l'étude sont dans [bench/etude-strategique](../bench/etude-strategique).

> **Corrigée le 26/10 après-midi par la contre-expertise indépendante** ([agent E](../bench/etude-strategique/agent-E-contre-expertise-independante.md)).
> - Plusieurs affirmations de la première version étaient fausses ou trop fortes : le plafond du modèle, le juge « à l'aveugle », l'alerte « maintenue », la « recherche exhaustive », la latence comme « vrai goulot », les 55 % et la latence de V1. Elles sont corrigées ci-dessous et marquées « corrigé ».
> - La confrontation et les désaccords non résolus sont dans [SYNTHESE-CONTRE-EXPERTISE.md](SYNTHESE-CONTRE-EXPERTISE.md).

## Réponse courte

**Conclusion : B, des optimisations ciblées**, avec une réserve forte.
- **Ce qui est démontré** :
  - l'état du jeu, la légalité et le létal sont fiables ;
  - les défaites évitables récentes viennent du **tour adverse**, que rien ne modélisait correctement, et du **délai** par rapport à ton rythme de jeu ;
  - la correction locale de l'alerte de danger est justifiée, et elle est faite.
- **Ce qui n'est pas démontré** : un gain de qualité stratégique du modèle lui-même, par plus de contexte, de raisonnement ou de calcul. Sur les positions courantes, on ne lui trouve pas d'erreur nette. Le seul test payant s'est arrêté à 11 paires, quand le crédit OpenAI s'est épuisé.
- ~~Plafond probable pour le modèle sur les décisions courantes.~~ **Corrigé : aucun plafond n'est démontré.** Le seul juge voyait le conseil, le modèle raisonne environ 150 jetons, une erreur nette et stable existe (Nozdormu), et aucune variante plus forte n'a été testée sur des positions difficiles. Pour le modèle : **preuves insuffisantes**.
- **Aucune preuve qu'une nouvelle architecture apporterait un palier** : pas de simulateur à jour, et pas besoin de MCTS pour ton seul tour, puisqu'une énumération des échanges coûte quelques ms. Le solveur actuel, lui, ne cherche que les dégâts au visage (corrigé).

**À savoir tout de suite** :
- **ton compte OpenAI n'a plus de crédit**, épuisé pendant le banc d'essai à 12 h 08, après 0,686 $ dépensés sur le 1 $ autorisé ; le copilote ne pourra plus interroger le modèle tant que tu n'auras pas rechargé ;
- l'application relancée à 13 h 12 calculait bien l'alerte, mais **l'effaçait dès qu'un conseil s'affichait** (corrigé dans l'après-midi) ; relance-la avec `tools\lancer-copilote.ps1` pour avoir la correction.

## 1. L'intelligence actuelle, dimension par dimension

| Dimension | État | Preuve | Ce qui n'est pas évalué |
|---|---|---|---|
| 1. Fidélité de l'état | **résolue** | 6 732 valeurs relues indépendamment depuis les lignes brutes, 0 écart, y compris avec 12 Rembobinages et 3 reconnexions ; 0 incohérence moteur ; 0 fuite de carte cachée | l'affichage de la fenêtre principale, déduit du code |
| 2. Légalité | **résolue** | 837 appels, 0 action illégale acceptée, 1 erreur réseau, 0 dépassement de délai | — |
| 3. Détection du létal | **fiable pour le « certain »** | 25 tours de létal « certain » sur 25 ont fini la partie (agent C) | il n'y a pas de modèle des échanges : 6 létaux n'ont été vus qu'après l'échange qui retirait une Provocation ; le « plausible » était trop optimiste derrière des Provocations (corrigé le 26/10 au matin) |
| 4. Qualité stratégique des actions | **sans erreur nette relevée, mais non mesurée par une référence** | agent A, **en voyant le conseil** (corrigé : ce n'était pas à l'aveugle), sur 20 conseils réels : 16 bons, 1 bon avec une suite perfectible, 3 discutables, aucune erreur nette | aucune vérité de référence ; le juge est lui-même un LLM ; 39 victoires sur 44 (intervalle de 76 à 95 %) ne mesurent rien |
| 5. Plans sur plusieurs tours | **présents, peu évalués** | Profondeurs gardées pour Grommash, Parenté (agent A) ; les étapes de plan non réévaluées par le modèle font 31 % des premières actions affichées (323 sur 1 055, débuts de réponse compris), et 55 % des seuls conseils complets (317 sur 581) (corrigé) ; la garde de continuité ne s'est trompée qu'une fois sur 45 | qualité des plans non mesurée |
| 6. Incertitude et danger adverse | **lacune démontrée, en partie corrigée** | Rexxar : l'alerte existante, sans pouvoir héroïque ni Provocations, s'est effacée à la décision fatale ; Tyrande : Nozdormu a donné 10 cristaux à un adversaire qui avait 8 cartes en main | le danger « probable » (deck présumé) n'est pas modélisé |
| 7. Rapidité et coût | **coût en temps, pas des conseils perdus** (corrigé) | premier fragment 3,6 s, réponse complète 8,8 s (médianes des appels terminés) ; la première action s'affiche avant ton coup dans 93 % des décisions avec appel, et tu attends le conseil (agent E) ; 12 s d'attente par tour en médiane, 34 s dans les tours de plus de 60 s ; 6,51 $ réels et 6,55 $ estimés pour les annulations | effet de Fast mode non mesuré |

## 2. Les agents et leurs désaccords

Cinq perspectives ont été demandées. Quatre ont été produites par de vrais sous-agents Claude, indépendants et lancés en parallèle :
- A, la stratégie : [rapport](../bench/etude-strategique/agent-A-strategie.md) et [comparaison à l'aveugle](../bench/etude-strategique/agent-A-aveugle.md) ;
- B, la recherche : [rapport](../bench/etude-strategique/agent-B-recherche.md) ;
- C, l'architecture : [rapport](../bench/etude-strategique/agent-C-architecture.md) ;
- D, l'évaluation : [rapport](../bench/etude-strategique/agent-D-evaluation.md).

**L'agent E (contre-expertise) s'est arrêté sur la limite de session Claude.** Sa revue a été écrite par le coordinateur et **présentée comme une perspective simulée, non indépendante** : [rapport](../bench/etude-strategique/agent-E-contre-expertise.md). Elle peut être refaite par un agent indépendant après 15 h.

**Convergences.**
- **A, B, C et E** placent le danger adverse en tête des défauts démontrés.
- **B, C et D** jugent le banc d'essai non fidèle au direct, donc les mesures antérieures (MVP3A) faites sur un autre conseiller.
- **B et E** jugent MCTS, ISMCTS et un simulateur complet hors de portée et inutiles ici.

**Désaccords et arbitrages.**

| Sujet | Position défendue | Critique reçue | Arbitrage |
|---|---|---|---|
| Danger : l'afficher ou l'envoyer au modèle ? | **B et C** : le brancher partout (affichage, message, validateur, plan) | **E** : l'affichage est justifié, car il corrige une alerte fausse sans toucher au modèle ; l'envoi au modèle est non démontré (1 cas, circulaire) ; la mise en garde du validateur est risquée, car elle pousse à défendre trop tôt | **affichage fait** ; envoi au modèle et validateur **en attente** d'une validation sur positions indépendantes |
| Le modèle raisonne-t-il mal le danger ? | **B** : la défaite contre Rexxar montre un manque de modélisation | **banc d'essai et E** : avec du temps, le modèle trouve la défense contre Rexxar même sans bilan de danger | le défaut était **la latence et l'absence de signal immédiat**, pas le raisonnement |
| Échanges contre les Provocations dans le solveur | **C** : rang 3, 6 létaux sur 25 vus seulement après l'échange | **E** : le gain porte sur la vitesse, et cela risque un faux « certain », l'atout de confiance du solveur | **proposition**, avec un niveau « conditionnel » d'abord |
| Message allégé (−37 à −40 %) | **C** : environ 0,004 $ et de la latence gagnés par appel | **E et D** : cela change l'entrée du modèle, donc il faut une comparaison A/B | **proposition** à mesurer |
| Consignes Découvertes et mulligans (Glissedrake…) | **A** : défauts récurrents | **E** : jugés sans contexte de partie, risque de surapprentissage | **enregistrer le contexte d'abord** ; pas de règle au prompt |
| Effort de raisonnement et Fast mode | **B** : router l'effort selon la difficulté, mesurer Fast mode | **E** : low, medium et high donnent la même action dans 81 à 89 % des cas | **faible priorité** |

## 3. Recherche externe (agent B, sources datées)

| Approche | Simulateur complet ? | Applicable ici | Apport | Coût dans un tour |
|---|---|---|---|---|
| Danger visible au tour suivant + ligne défensive | non | **oui** | élevé sur les défaites concernées | quelques ms |
| Garde-fous sur les effets publics : ressources adverses, cartes symétriques | non | **oui** | élevé sur le cas Tyrande | ms |
| Lignes candidates calculées localement, le LLM choisit (« LLM-Modulo », Forge) | non pour le combat | oui, effort moyen | moyen, non mesuré en Hearthstone | ms + le même appel |
| Routage de l'effort, Fast mode | non | oui | moyen sur la latence | Fast mode : coût ×2 |
| Profils adverses à la Pluribus, déterminisation légère du tour adverse | non (listes à maintenir) | incertain | non démontré | ms |
| MCTS / ISMCTS sur plusieurs tours, réseaux de valeur, RL | **oui** | **non** | résultats publiés seulement entre bots, cartes 2015-2019 | — |
| Vote de plusieurs modèles en direct | non | non | faible | coût ×N, latence du plus lent |

**Faits clés** :
- **aucun simulateur Hearthstone à jour** : SabberStone s'arrête à l'Année du Dragon (2019), Fireplace au patch 17.6 (2020), MetaStone en 2018 ;
- **les gains publiés** (MCTS + apprentissage supervisé, optimisation évolutionnaire) ont été mesurés entre bots, sur des ensembles de cartes anciens : ils **ne se transposent pas** à Hearthstone 2026.

**Sources principales** :
- Świechowski et al., arXiv:1808.04794 (2018) ;
- Goodman, arXiv:1902.06075 (2019) ;
- García-Sánchez et al., arXiv:2410.19681 ;
- Zhang, mémoire Silverfish, University of Alberta (2017) ;
- Kambhampati et al., « LLM-Modulo », arXiv:2402.01817 ;
- Karten et al., PokéChamp, arXiv:2503.04094 ;
- Lehrach et al., Code World Models, arXiv:2510.04542 ;
- Pluribus, Meta AI (2019) ;
- dépôts SabberStone, Fireplace, MetaStone et Forge ;
- OpenAI, guides « Reasoning », « Latency optimization » et « Ultrafast mode ».

La liste complète, avec ce qui a été lu, partiellement lu ou non lu, est dans le [rapport B, §8](../bench/etude-strategique/agent-B-recherche.md).

## 4. Décisions réelles difficiles

| Situation | Ce qui s'est joué | Ce qu'on en retient |
|---|---|---|
| Rexxar, 0 h 44 : 27 puis 25 dégâts visibles contre 25 PV | aucun conseil à temps ; tu as attaqué au visage, ce qui a déclenché un Secret, et tu es mort au tour suivant | le modèle, interrogé à froid, propose la bonne défense en quatre coups ; il fallait un signal **instantané**, et Rexxar n'avait plus qu'une carte dans son deck (fatigue) |
| Tyrande, 22 h 53 : Nozdormu en main, adversaire à 8 cartes | le conseil « Nozdormu » a donné 10 cristaux à l'adversaire, ce qui a permis son combo exact | V0 rejoue Nozdormu tout en écrivant le risque ; avec le signal chiffré, V1 choisit une autre ligne, que le juge à l'aveugle préfère |
| Le roi-liche, 0 h 16 : 15 PV derrière cinq Provocations (22 PV) | le calcul local affichait « létal plausible : 20 dégâts » | corrigé : le texte donne maintenant le coût des Provocations ; une borne prudente évite les faux « aucun » |
| Gul'dan, 23 h 26 : 2 Provocations, 5 PV | Le Grand Dracorex a retiré les deux Provocations d'une attaque : létal | contre-exemple qui a empêché une borne trop stricte ; ce genre de mécanique doit rester en « plausible » |
| Orweyna, 21 h 49 : 3 dégâts visibles contre 3 PV en cours de tour | tu as attaqué au visage et gagné | **une alerte en cours de tour n'est pas un ordre de défendre** : l'affichage reste informatif, aucune mise en garde automatique |
| Profondeurs Sanguines dépensées pour 2 dégâts (2 positions, agent A) | conseil discutable | perte de valeur d'une charge de lieu, non modélisée |
| Visage plutôt qu'un échange qui protégeait Luisaile (agent A) | discutable | il n'y a pas d'évaluateur d'échanges |
| Découverte : Star du rock + Posture de défense plutôt que Calamar + Réveil brutal (agent A) | probablement sous-optimal | jugé sans contexte de plateau : à confirmer |

## 5. Comparaisons réellement mesurées

Banc d'essai payant ([RESULTATS-BENCHMARK.md](../bench/etude-strategique/RESULTATS-BENCHMARK.md)) :
- 25 positions tirées avec une graine fixe ;
- V0 : conseiller actuel, avec l'analyse locale du direct ;
- V1 : V0 + bilan de danger ;
- effort medium, plafond de 0,95 $.

| Mesure | V0 | V1 |
|---|---|---|
| Paires complètes (crédit épuisé au 19e appel) | 11 | 11 |
| Même première action | 10 sur 11 | 10 sur 11 |
| Létal certain conservé | 4 sur 4 | 3 sur 3 |
| Témoins restés offensifs | 5 sur 5 | 6 sur 6 |
| Juge à l'aveugle (agent A) | 1 préférence légère | 1 préférence nette (Nozdormu) et 1 légère |
| Coût par appel | 0,0216 $ | 0,0227 $ (+5 %) |
| Réponse complète (sans flux, médiane) | 15,2 s | 14,8 s ; **en apparié, V1 est plus lent dans 8 paires sur 11 (+2,0 s en médiane)** (corrigé) |
| Concordance avec le conseil donné en partie | 13 positions sur 13 identiques (agent E) | 14 sur 15 (Nozdormu diffère) |

**Autres mesures, sans appel** :
- **alerte de danger** : 28 positions sur 830 en classé (3,4 %), 57 sur 582 contre l'IA (parties où tu laissais l'IA en vie pour tester des cartes) ;
- **simulation hors ligne**, à la fin de tes tours : 4 alarmes, toutes suivies d'une défaite, et 0 fausse alarme sur 291. Cette mesure est en partie tautologique, car elle porte sur l'état final (voir l'agent E).

**Ce qu'on ne peut pas conclure** :
- l'effet de V1 sur la qualité en général ;
- un effet sur les victoires : il faudrait plusieurs centaines de parties par variante (agents B et D) ;
- l'équivalence stricte des variantes ;
- tout ce qui touche aux Découvertes et aux mulligans : le banc d'essai ne les contient pas.

## 6. Améliorations classées

| Rang | Amélioration | Impact attendu | Preuve | Coût | Risque | Statut |
|---|---|---|---|---|---|---|
| 1 | Alerte locale de danger corrigée (pouvoir héroïque, Provocations, marge, défenses) | défaites du type Rexxar ; signal instantané | alerte existante démontrée fausse | faible | très faible : affichage seul | **fait**, mais effacée par tout conseil jusqu'à la correction de l'après-midi : elle est maintenant sur la ligne de mise en garde (corrigé) |
| 2 | Signal chiffré des cartes qui aident l'adversaire (mise à 10 cristaux) | défaites du type Tyrande | 1 cas + mécanisme public certain ; préféré à l'aveugle | très faible | faible | **fait** (affichage) |
| 3 | Fatigue adverse signalée | marginal | 1 cas (Rexxar) | très faible | nul | **fait** |
| 4 | Banc d'essai fidèle au direct : avis précédent, Découvertes, mulligans, deck | condition de toute mesure future | constat de B, C et D | faible à moyen | nul | `+local` fait ; reste **à faire** |
| 5 | Bilan de danger envoyé au modèle | à mesurer | 1 cas circulaire, pas de régression sur 11 | faible | faible à moyen | **outil prêt** (`+danger`) ; validation indépendante requise |
| 6 | Échanges contre les Provocations dans le solveur | létaux plus tôt et sans appel (6 sur 25) | agent C | moyen | moyen pour la confiance | proposition, « conditionnel » d'abord |
| 7 | Message allégé et réordonné pour le cache | environ −20 % de coût, latence | simulation de C | moyen | moyen pour la qualité | proposition, A/B requis |
| 8 | Suite du plan lue plus tôt dans le flux | environ −1,8 s | C | faible à moyen | faible à moyen | proposition |
| 9 | Contexte des Découvertes et mulligans enregistré, puis jugé | inconnu | A, sans contexte | faible | faible | proposition |

## 7. Ce qui ne vaut probablement pas le coût

- **Simulateur complet, MCTS, ISMCTS, RL** : aucun simulateur à jour, des milliers de cartes à scripter et à maintenir, aucune donnée d'entraînement. ~~Pour ton tour, la recherche exhaustive existe déjà.~~ Corrigé : le solveur n'explore que les dégâts au visage ; l'élargir aux échanges est une énumération locale, pas du MCTS.
- **Vote de plusieurs modèles en direct** : coût multiplié, latence du plus lent, et le modèle se répète déjà.
- **Effort high ou xhigh en permanence** : 28 à 58 s par décision dans le banc MVP3A, pour peu de décisions changées. Ce banc portait surtout sur des positions simples : sur les positions difficiles, la question reste ouverte (agent E).
- **Mise en garde automatique du validateur sur le danger, en l'état** : elle risque de pousser à défendre au mauvais moment.
- **Nouvelles consignes de prompt tirées de quelques observations** : surapprentissage probable.

## 8. Modifications faites et tests

| Modification | Fichiers | Tests |
|---|---|---|
| Module `ThreatAssessment` : dégâts visibles du prochain tour, marge, défenses légales, cartes qui aident l'adversaire, inconnues signalées | `Strategy/ThreatAssessment.cs` | 3 tests sur les états réels de Rexxar et Tyrande |
| L'alerte « Menace » remplacée par « Danger au prochain tour » (dès une marge ≤ 5, avec défenses), le signal Nozdormu et la fatigue ; affichage seul, rien n'est envoyé au modèle | `Strategy/QuickAnalysis.cs` | 4 tests : début du tour fatal, maintien après la mort du Parle-esprit, Nozdormu sans fausse alerte, fatigue |
| « Létal plausible » derrière des Provocations : coût affiché, borne prudente | `Strategy/TurnSolver.cs` | 4 tests sur la fixture du roi-liche (fait le matin) |
| Alerte de danger sur la ligne de mise en garde de l'overlay, dans toutes les vues et en mode compact ; omise quand le létal est démontré (après la contre-expertise) | `Strategy/AdviceDisplay.cs`, `Strategy/QuickAnalysis.cs` | 3 tests sur le vrai affichage : Rexxar en analyse, début de réponse, conseil et suite du plan ; Nozdormu conseillé ; létal démontré |
| Variantes `+local` et `+danger` du banc d'essai | `Evaluation/BenchRunner.cs`, `Cli/BenchCommand.cs` | 2 tests |
| Outils de diagnostic : `threat-audit`, `danger-check`, `case-text`, `stats-dump` étendu | `Cli/` | — |
| Fixtures anonymisées : Rexxar, Tyrande, le roi-liche ; 123 cartes ajoutées au jeu de test | `tests/.../Fixtures/` | — |

Il y a **254 tests, tous réussis**. Les comportements fiables sont conservés : synchronisation, Rembobinage, confidentialité, Sombres dons, létal, garde des plans, budgets.

## 9. Limites méthodologiques

- **Banc d'essai** : interrompu par le crédit épuisé, avec seulement 11 paires. Ses positions de danger sont celles qui ont inspiré le module (circularité).
- **Juge** : un LLM d'une autre famille. Il a vu les conseils sur les 20 positions de l'audit et sur 3 des 11 paires, et il a jugé les Découvertes et les mulligans sans contexte de partie.
- **Contre-expertise** : d'abord simulée par le coordinateur, puis refaite par un sous-agent indépendant, de la même famille de modèles que les agents A à D.
- **Danger « probable »** : il n'a pas été étudié. Il demanderait des listes de menaces par archétype, avec le risque de deviner la main adverse.
- **Volume** : 44 parties, donc aucune conclusion possible sur le taux de victoire.

## 10. Recommandation finale

1. **Garder l'architecture actuelle**, sans refonte. Le modèle décide bien sur les positions courantes, et ce qui lui manquait était surtout un **signal local instantané** sur le tour adverse, maintenant ajouté.
2. **Recharger le crédit OpenAI**, puis, avant d'activer le bilan de danger dans le message du modèle, faire la validation proposée par l'agent D :
   - environ 1 $ sur des positions **jamais utilisées pour concevoir** le module : la réserve contre l'IA et les nouvelles parties ;
   - un critère binaire fixé à l'avance ;
   - un test de signe ;
   - le bruit du modèle mesuré.
3. **Ensuite seulement**, et chaque fois mesurés sur le banc fidèle : les échanges contre les Provocations (en « conditionnel » d'abord), le message allégé et la lecture plus précoce de la suite du plan.
4. **Ne pas investir** dans un simulateur, MCTS, le vote de plusieurs modèles ou un effort élevé.

### Le plafond, en sept questions

1. **L'architecture est-elle proche de son meilleur niveau ?** ~~Pour la décision du modèle, probablement oui.~~ **Corrigé : indécidable.** Le juge n'était pas aveugle, et aucune variante plus forte n'a été testée sur des positions difficiles. Pour le système, non : le danger à l'écran, le danger après la ligne conseillée, les échanges et la mesure ont encore des leviers.
2. **Les gains possibles sans changer de modèle** : le danger et les ressources adverses (faits en affichage), les échanges, l'allègement et le cache, la suite du plan plus tôt, le contexte des Découvertes. Ils sont ciblés, pas massifs.
3. **Le gain d'un calcul plus déterministe** : réel mais borné. Pour le létal, c'est surtout de la vitesse (7 derniers tours de victoire sur 38, agent E). Au-delà, la limite est la couverture des effets de cartes (55 % des phases d'attaque ont un effet non simulé), pas la recherche.
4. **Le gain d'un modèle plus performant** : **inconnu**, rien n'a été testé. Le peu d'effet de l'effort de raisonnement suggère des rendements décroissants sur ces positions.
5. **Les principaux blocages** (corrigé) :
   - **la mesure** : sans juge aveugle ni positions difficiles indépendantes, on ne sait pas où est la marge du modèle ;
   - **l'absence de modèle du tour adverse**, et un signal de danger qui n'arrivait pas à l'écran ;
   - **la latence**, comme coût en temps : 12 à 34 s par tour. Ce n'est pas une perte de conseils, puisque la première action s'affiche avant ton coup dans 93 % des décisions avec appel.

   Le raisonnement n'est pas mis hors de cause : Nozdormu est une erreur de jugement sur une information publique que le modèle avait.
6. **Des améliorations substantielles à effort raisonnable ?** Aucune n'est démontrée **substantielle** ; plusieurs sont **ciblées et mesurables**.
7. **Ce qui serait très coûteux pour un gain marginal** : simulateur ou MCTS, vote de modèles, effort élevé, Fast mode (coût ×2) tant qu'il n'est pas mesuré.

### Frontière qualité / latence / coût / complexité

| Option | Qualité | Latence | Coût | Complexité |
|---|---|---|---|---|
| **Actuel + affichage du danger (fait)** | = sur les décisions du modèle ; meilleur signal de survie | = | = | faible |
| + bilan de danger envoyé au modèle | + possible, non démontré | = | +5 % | faible |
| + échanges contre les Provocations | létal plus tôt | − sur les tours de létal (sans appel) | − | moyenne |
| + message allégé, cache | ? | − | environ −20 % | moyenne |
| effort high | ≈ | ×2,4 | +50 % | nulle |
| simulateur, MCTS | ? | +++ | ++ | très élevée |
