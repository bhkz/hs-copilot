# Deuxième vraie partie avec le copilote : analyse et corrections

Partie du 25/10/2026, de 16 h 37 à 16 h 45. Contre l'IA du jeu (`GT_VS_AI`), Guerrier Dragons contre Guerrier, gagnée au tour 8 de l'adversaire. Conseiller : gpt-6.1-sol, effort low, en flux.

Sources : trace du conseiller (`%LocalAppData%\HsCopilot\advisor\20261025_163706_163706-1579361435.jsonl`), journaux de session (`sessions\20261025_163615_hdt.log` et `20261025_163706_hdt.log`), partie anonymisée dans [ai-depths-wyrm-lethal.log](../tests/HsCopilot.Core.Tests/Fixtures/ai-depths-wyrm-lethal.log). Les tableaux viennent de la commande `hscopilot advisor-audit`, qui rejoue les journaux avec le même moteur.

## Verdict

- **Reconstruction et pioche** : correctes. 383 vues du client, 0 écart. À chaque tour, la carte piochée est dans l'état analysé ; l'appel part 0,1 à 0,3 s après la pioche.
- **Conseils** : 13 conseils complets, tous légaux. 10 suivis à l'identique, mulligan compris. Deux autres l'ont été dans un ordre différent : attaque d'abord, puis la carte conseillée. *Correction du 25/10 au soir : cette version disait que la lecture du mulligan ne permettait pas de conclure. En fait, les cartes « choisies » au mulligan sont celles qu'on garde, et les changements de zone le confirment (voir [SOMBRES-DONS-ET-DECK.md](SOMBRES-DONS-ET-DECK.md) §2).*
- **Une vraie erreur de l'IA**, au tour 7 (révision 152) : elle a conseillé d'attaquer avec le Wyrm pour 8, alors que Profondeurs Sanguines, posée juste avant sur son propre conseil, pouvait lui donner +2 ATQ. Tu as activé le lieu d'abord et infligé 10.
- **L'hypothèse d'un létal certain au tour 8 est fausse.** Tu as bien infligé 10 au tour 7. Au début du tour 8, le meilleur enchaînement déterministe fait 9 dégâts contre 10 PV + armure. Le létal dépendait de l'arme aléatoire d'Animatrice de stade, qui devait avoir au moins 3 ATQ. Elle les a eus.
- **Disponibilité** : le plafond de 3 analyses par tour a été atteint aux tours 5, 7 et 8. Le début de tour et les choix (Découverte, Rembobinage) le consommaient. Au tour 5, l'overlay est resté environ 70 s sur un « EN ATTENTE » trompeur.
- **Létal mal étiqueté** : à la révision 170 (5 PV), le létal héros 3 + Suffusion 2 était certain, mais affiché « plausible », et sans conseil à cause du plafond.

Au tour 8, tu as fini ton tour avec l'IA à 2 PV, comme tu le fais exprès en PvE pour tester tes cartes. L'IA est morte en attaquant ton Animatrice avec son arme. Ce n'est pas compté comme un létal manqué.

## 1. Chronologie

| Tour | Rév. | Conseil de l'IA | Coup joué | Commentaire |
|---|---|---|---|---|
| 1 | 84 | mulligan : remplacer Chronoseigneur et Suffusion | idem : Colporteur gardé, Forgeron et Sombre chevaucheur reçus | (corrigé) |
| 1 | 88 | Jouer Sombre chevaucheur | idem | |
| 1 | 90 | Choisir Drake terrestre (Découverte) | idem | |
| 2 | 98 | Jouer Colporteur de pétales | idem | |
| 3 | 106 | Jouer Forgeron dimensionnel | idem | |
| 4 | 114 | Suffusion → Sous-chef cruel | attaque du Forgeron sur le Sous-chef | autre ordre |
| 4 | 116 | Jouer Drake terrestre | idem | 4 dégâts en fin de tour |
| 5 | 125 | Jouer Gardien des couvées | idem | |
| 5 | 127-132 | (Avant-garde, puis choix du Rembobinage) | | **plafond atteint à 16:40:22**, rien jusqu'à 16:41:32 |
| 6 | 140 | Attaquer la Voltigeuse avec l'arme | idem | |
| 6 | 142 | Wyrm → héros (5) | idem | |
| 7 | 150 | Jouer Profondeurs Sanguines « pour renforcer le Wyrm » | idem | plan cohérent |
| 7 | 152 | **Attaquer avec le Wyrm (8)** | **Activer Profondeurs → Wyrm** | erreur de l'IA, voir §3 |
| 7 | 154 | Attaquer avec le Wyrm (10) | idem | réponse partielle, puis annulée |
| 7 | 156-158 | — | Glissedrake, fin de tour | **plafond atteint** ; 2 mana, Suffusion et pouvoir héroïque inutilisés |
| 8 | 163 | Jouer Animatrice de stade | attaque du Glissedrake, puis Animatrice | autre ordre ; arme 3/3 obtenue |
| 8 | 165-174 | — | héros (3), pouvoir héroïque, fin de tour | **plafond atteint** ; létal certain (5 PV) non signalé |

## 2. Synchronisation avec la pioche

| Tour | Carte piochée | Pioche (log) | Analyse lancée | Conseil complet | Âge à l'affichage |
|---|---|---|---|---|---|
| 1 | Avant-garde du portail (corrigé : Sombre chevaucheur venait du mulligan) | 16:37:36.2 | 16:37:36.4 | 16:37:46.1 | 9,8 s |
| 2 | Suffusion d'ombreflamme | 16:38:09.2 | 16:38:09.3 | 16:38:16.0 | 6,7 s |
| 3 | Animatrice de stade | 16:38:38.8 | 16:38:39.0 | 16:38:46.8 | 8,0 s |
| 4 | Gardien des couvées | 16:38:59.9 | 16:39:00.1 | 16:39:10.3 | 10,4 s |
| 5 | Glissedrake visionnaire | 16:39:39.9 | 16:39:40.0 | 16:39:49.0 | 9,1 s |
| 6 | Profondeurs Sanguines | 16:41:39.8 | 16:41:39.9 | 16:41:50.5 | 10,7 s |
| 7 | Le Grand Dracorex | 16:42:16.0 | 16:42:16.2 | 16:42:27.4 | 11,3 s |
| 8 | Gardien des couvées | 16:44:11.5 | 16:44:11.6 | 16:44:24.5 | 13,1 s |

Les trois causes possibles d'un conseil qui ignorerait la pioche ont été vérifiées :

- **État incomplet** : aucun cas. Aucune analyse n'a été lancée avant l'arrivée de la carte, et aucun refus « état incomplet ».
- **Réponse tardive** : aucune réponse arrivée après un changement d'état n'a été affichée. L'âge de 6 à 13 s est le temps de réponse du modèle (8,8 s en moyenne).
- **Carte ignorée par le modèle** : aucun cas. Par exemple, Profondeurs, piochée au tour 6, est au centre du plan du tour 7.

**Ajouté** : chaque conseil porte maintenant ses diagnostics. Ce sont la révision analysée, l'heure du log de l'état, les cartes piochées ce tour présentes dans l'état, l'heure d'envoi et l'âge à l'affichage. Ils figurent dans la trace (champs `analyzedRevision`, `stateLogTime`, `drawnThisTurn`, `sentUtc`, `ageMs`) et dans l'onglet « Conseiller ».

## 3. Le cas du tour 7 : Profondeurs Sanguines puis Wyrm

État à la révision 152 :

- Wyrm du pic venteux 8/5, prêt à attaquer ;
- Profondeurs Sanguines posée, activable sur lui ;
- adversaire à 18 PV ;
- 6 mana ; en main : Suffusion d'ombreflamme, Animatrice de stade, Glissedrake visionnaire, Le Grand Dracorex.

- À la révision 150, l'IA avait conseillé « Joue Profondeurs Sanguines pour renforcer le Wyrm du pic venteux avant son attaque ».
- À la révision 152, elle a conseillé « Attaquer avec Wyrm → héros (18 PV) : infliger 8 dégâts ». C'est contraire à son propre plan, et cela fait 2 dégâts de moins.
- Tu as joué « Activer Profondeurs Sanguines → Wyrm », puis l'attaque à 10. L'adversaire est passé de 18 à 8 PV.

Calcul local actuel sur cet état :

- dégâts maximum au visage : 12 (Suffusion 2, Profondeurs sur le Wyrm, Wyrm 10) contre 18 PV : pas de létal ;
- ordre : Profondeurs avant l'attaque du Wyrm, +2 ;
- Animatrice : il faudrait une arme d'au moins 6 ATQ, soit environ 7 % des armes, 14 % avec le second tirage. Cette ligne n'est pas annoncée comme létal, seulement signalée « peu probable ».

**Ajouté** : le même conseil afficherait maintenant la mise en garde « Avant cette attaque : Activer Profondeurs Sanguines → Wyrm du pic venteux (allié) (+2 ATQ, soit 10 au lieu de 8). » En plus :

- le calcul local est envoyé au modèle (`localAnalysis`), avec son ordre recommandé ;
- le conseil précédent du tour aussi (`previousAdvice`), pour qu'il reste cohérent avec son plan.

## 4. L'hypothèse du létal au tour 8

- **Ce que dit l'hypothèse** : si Profondeurs avait été utilisée au tour 7, le tour 8 était un létal certain.
- **Ce que montrent les journaux** : Profondeurs a bien été utilisée, et le Wyrm a infligé 10. Au début du tour 8, l'adversaire avait 10 PV + armure, car il a gagné 2 d'armure pendant son tour.
- **Meilleur enchaînement déterministe au tour 8** : épée du Gardien des couvées 2, Suffusion 2, Glissedrake 5, soit 9 contre 10. Ce n'est pas un létal certain.
- **Létal conditionnel** : jouer Animatrice de stade, puis Suffusion, Glissedrake et le héros avec l'arme aléatoire. Il fallait une arme d'au moins 3 ATQ, bonus +1 compris : environ 76 % par tirage, 94 % avec le second tirage du Rembobinage. L'estimation porte sur les 234 armes à collectionner, la réserve exacte du tirage n'étant pas connue.
- **Ce qui s'est passé** : l'arme obtenue était une 3/3. Avec Glissedrake 5 et l'arme 3, l'adversaire est tombé à 2 PV. La Suffusion restait en main : létal certain, que tu as volontairement laissé.

## 5. Quota et disponibilité de l'assistant

Avant :

- le début de tour et les choix passaient outre le plafond, mais le consommaient ;
- au tour 5, début de tour, Avant-garde du portail et choix du Rembobinage ont épuisé les 3 analyses ;
- aux tours 7 et 8, la fin du tour s'est jouée sans conseil, sous un titre « EN ATTENTE » qui ne disait pas pourquoi.

Corrections, sans relever le plafond ni toucher aux plafonds financiers (50 analyses et 0,75 $ par partie, 3 $ par session) :

1. **Le début de tour, le mulligan, les choix et la réanalyse manuelle ne consomment plus le quota.** Les 3 analyses du plafond sont réservées aux décisions de milieu de tour.
2. **Létal certain : affiché aussitôt, sans appel**, avec la séquence complète. Il est recalculé à chaque action. La réanalyse manuelle interroge quand même le modèle.
3. **Suite du plan sans appel.** Le modèle désigne maintenant ses étapes suivantes par entités (`followUp` : texte, `sourceEntityId`, `targetEntityId`). L'étape suivante s'affiche sans nouvel appel si trois conditions sont réunies :
   - ton dernier coup est exactement le conseil ;
   - rien de nouveau n'est apparu : aucune carte arrivée en main, aucun serviteur adverse apparu, Secrets et armes inchangés ;
   - le serveur propose cette étape.
4. **Titres explicites** : ANALYSES AUTO ÉPUISÉES, avec le compteur « 3/3 analyses automatiques de milieu de tour » et le raccourci actif (Ctrl+Alt+F10 sur ton PC). Aussi FIN DE TOUR, TOUR ADVERSE, RATTRAPAGE, BUDGET ATTEINT, PAS DE RÉPONSE, ÉTAT INCOMPLET. L'analyse rapide reste à jour dessous : dégâts maximum, ordre, actions encore possibles.

Mesure sans appel API : la partie a été rejouée à son rythme réel, deux fois, avec le fournisseur simulé (9 ± 3 s, aucune stratégie). La première fois en mode « version précédente » (`--previous-version`), la seconde avec la version actuelle.

| | Version précédente | Version actuelle |
|---|---|---|
| Appels envoyés | 18 | 20 |
| Conseils affichés | 14 | 19, dont 3 létaux démontrés sans appel |
| Décisions refusées pour plafond | 6 (tours 5, 7, 7, 8, 8, 8) | 1 (fin du tour 7) |
| Tour 5 après le Rembobinage | rien pendant 70 s | conseil 10 s après |
| Tour 8, adversaire à 5 puis 2 PV | rien | « Létal démontré : Suffusion → héros, puis la suite » à chaque décision |

Le fournisseur simulé ne renvoie pas d'étapes désignées par entités. La suite du plan sans appel n'apparaît donc pas dans cette mesure ; elle est testée séparément sur l'état réel du tour 7. Les 2 appels de plus valent environ 0,03 $ par partie au prix observé. La partie réelle a coûté 0,25 $ pour 18 appels, appels annulés estimés compris.

## 6. Vérificateur de létal déterministe

Il s'appelle [TurnSolver](../src/HsCopilot.Core/Strategy/TurnSolver.cs). C'est une exploration exhaustive, avec mémoïsation, des cartes jouables avec le mana disponible. Les attaques au visage et les effets de fin de tour sont ajoutés à la fin.

Mécaniques simulées :

- attaques autorisées par le serveur (Furie des vents comprise) et attaque du héros avec son arme ;
- sorts et cris de guerre « Inflige N points de dégâts », seulement si le serveur propose le héros adverse comme cible ; bonus des sorts compris, Voyante distordue blessée, y compris blessée pendant la séquence par Profondeurs ;
- serviteurs avec Charge, Grommash +6 ATQ s'il est blessé ; Ruée posée ce tour : serviteurs seulement ;
- Profondeurs Sanguines : +2 ATQ et 1 dégât, une activation par tour, jouable puis activable le même tour, ou pour détruire une Provocation à 1 PV ;
- Forgeron dimensionnel : +2 ATQ aux serviteurs avec Charge en main ;
- Gardien des couvées : épée 2/2 si un autre Dragon reste en main et que le héros n'a pas encore attaqué ;
- Chronoseigneur Époque : retire les Provocations jouées au tour adverse précédent ;
- Drake terrestre : 4 au héros adverse en fin de tour, même derrière une Provocation ;
- places libres sur le plateau (7 au plus).

Niveaux :

| Niveau | Condition |
|---|---|
| **certain (démontré)** | la séquence atteint les PV + l'armure adverses, aucun Secret adverse inconnu, héros adverse non Insensible |
| **conditionnel** | la séquence suffit mais un Secret peut l'empêcher, avec un avertissement si tes PV sont bas ; ou il faut une arme aléatoire d'Animatrice d'au moins N ATQ, chance estimée d'au moins 50 % avec le second tirage |
| **plausible, non démontré** | il faudrait d'abord retirer une Provocation par des échanges, non simulés |
| **aucun** | sinon. Une ligne improbable est signalée « peu probable », jamais annoncée comme létal |

Dans une séquence, seule la première étape est garantie légale par le serveur. Les suivantes sont calculées, et marquées « calculé ».

Non simulé, donc jamais présenté comme certain :

- échanges contre des serviteurs ;
- Découverte et autres effets aléatoires, sauf l'arme d'Animatrice traitée comme condition ;
- effets adverses autres que les Secrets et l'Insensible ;
- cartes hors de ce modèle : celles qui mentionnent dégâts, Charge, arme ou ATQ sont signalées « Non simulé par le calcul ».

Le validateur plafonne le niveau annoncé par le modèle à celui du solveur. Il affiche le létal du solveur même si le modèle l'oublie, et ajoute une mise en garde si le conseil ne suit pas un létal démontré.

## 7. Séquencement du tour entier

- **Ordre** : l'analyse rapide et `localAnalysis.ordering` signalent un bonus d'ATQ à jouer avant une attaque. Le validateur met en garde contre une attaque conseillée trop tôt.
- **Instructions du modèle** :
  - règle 13 : ne pas contredire les dégâts du calcul local, et jouer la première étape d'un létal certain ;
  - règle 14 : préparations avant attaques ;
  - règle 15 : poursuivre le plan précédent, ou dire ce qui change.
- **Notes du deck** : « Profondeurs Sanguines avant l'attaque » et « Animatrice de stade pour un létal » ajoutées à [guerrier-dragons.md](../config/strategy/guerrier-dragons.md).

## 8. Tests ajoutés

Ils portent sur les états exacts de cette partie : [TurnSolverTests](../tests/HsCopilot.Core.Tests/TurnSolverTests.cs) et [RealGameRegressionTests](../tests/HsCopilot.Core.Tests/RealGameRegressionTests.cs).

Tour 7 :

- dégâts maximum 12, Profondeurs avant le Wyrm, aucun létal, Animatrice signalée « peu probable » ;
- au début du tour, le plan pose puis active le lieu avant l'attaque ;
- un conseil « Wyrm au visage » reçoit la mise en garde « 10 au lieu de 8 » ; le conseil Profondeurs n'en reçoit pas.

Tour 8 :

- au début du tour, létal seulement conditionnel, avec une arme d'au moins 3 ATQ ;
- avec l'arme 3/3, Suffusion et héros forment un létal certain, affiché sans appel et à 0 $ ;
- avec un Secret adverse, ce létal devient conditionnel ;
- un modèle qui manque le létal démontré est signalé, et le létal reste affiché.

Coordinateur :

- suite du plan sans appel après « Jouer Profondeurs », puis refus de la suite quand une nouvelle carte arrive ;
- le début de tour et les choix ne consomment pas le quota ;
- titre « ANALYSES AUTO ÉPUISÉES » et compteur du plafond ;
- `localAnalysis` et `previousAdvice` présents dans le message de milieu de tour.

Autres :

- Drake terrestre compté en fin de tour, létal à 1 PV près ;
- diagnostics (révision, pioche, envoi, âge) dans la mise à jour et la trace ;
- étapes `followUp` lues dans le nouveau format et dans l'ancien ; schéma avec « certain » ;
- pioche du tour présente à chaque début de tour ;
- solveur assez rapide pour toutes les décisions de la partie.

Le jeu de cartes des tests contenait 16 cartes de moins que cette partie, dont Drake terrestre. Elles ont été ajoutées.

## 9. Avant et après, décision par décision

Le calcul local de la version actuelle a été rejoué sur les états réels de la partie (`advisor-audit`, section « Calcul local ») :

| Rév. | PV adverses | Avant | Après |
|---|---|---|---|
| 150 | 18 | aucun calcul de séquence : létal vérifié sur les attaques et une seule carte de la main | « Dégâts maximum 12 : Suffusion, puis Profondeurs, puis activation sur le Wyrm, puis Wyrm 10 » |
| 152 | 18 | conseil « Wyrm (8) » sans mise en garde | même conseil avec « Avant cette attaque : Profondeurs → Wyrm (+2 ATQ, soit 10 au lieu de 8) » |
| 163 | 10 | aucun létal signalé (attaques 5 + Suffusion 2 contre 10) | « Létal conditionnel : arme aléatoire d'au moins 3 ATQ (environ 76 % par tirage, 94 % avec le Rembobinage) », séquence complète |
| 170 | 5 | « plausible », aucun conseil (plafond) | « Létal certain (démontré) : Suffusion → héros (2), puis héros (3) », affiché sans appel |
| 172, 174 | 2 | aucun conseil (plafond) | « Létal certain : Suffusion → héros (2) », sans appel |

## 10. Performance et coût

- **Solveur** : 0,0 à 3,5 ms par décision sur cette partie, 31 ms au tout premier appel (compilation à la volée).
- **Message envoyé au modèle** : environ 1 200 caractères de plus avec `localAnalysis` et `previousAdvice`, environ 7 %. Cela représente environ 350 jetons, soit environ 0,0007 $ par appel hors cache. Les règles 13 à 15 sont dans les instructions, servies depuis le cache.
- **Réponse** : chaque étape `followUp` porte maintenant deux entités, quelques jetons de plus.
- **Létaux démontrés et suites de plan** : 0 $.
- **Mesures** : aucune campagne API n'a été lancée. Toutes les mesures ci-dessus sont locales ou simulées.

## 11. Limites

- La suite du plan sans appel et l'effet de `localAnalysis` sur les choix du modèle n'ont pas été observés avec le vrai modèle. Ils le seront à la prochaine partie, origines « plan » et « solveur » dans la trace.
- Les chances de l'arme aléatoire sont estimées sur toutes les armes à collectionner. La réserve réelle du tirage n'est pas connue.
- Le solveur ne cherche que les dégâts au visage. Il ne juge ni les échanges, ni la survie, ni la valeur. Quand il n'y a pas de létal, la décision reste au modèle et à toi.
- Aucun Rembobinage n'a été effectivement rejoué dans cette partie : la ligne temporelle actuelle a été gardée deux fois. La correction du renvoi de HDT, issue de la première partie, n'est donc toujours pas observée en direct.

## 12. Relancer

Ferme le copilote si besoin, puis relance `tools\lancer-copilote.ps1`. Le script reconstruit l'application si les sources ont changé. Rien à configurer : la clé OpenAI et le réglage « classé » sont conservés.

Pour auditer la prochaine partie, la commande est décrite dans [TEST-EN-DIRECT.md](TEST-EN-DIRECT.md#auditer-une-partie-sans-appel-api).
