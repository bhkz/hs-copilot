# Contre-expertise (Red Team) : perspective simulée par le coordinateur

> **Remplacée** par la contre-expertise indépendante : [agent-E-contre-expertise-independante.md](agent-E-contre-expertise-independante.md). Plusieurs verdicts ci-dessous sont réfutés, dont P1 (« instantanée, sans risque » : l'alerte était effacée par tout conseil) et P12 (« la recherche exhaustive existe déjà »).

**Avertissement.** L'agent E indépendant a été lancé, mais il s'est arrêté avant d'avoir produit quoi que ce soit : la limite de session Claude était atteinte, avec remise à zéro à 15 h. Cette revue est donc écrite par le coordinateur de l'étude, qui a aussi conçu le module de danger et le banc d'essai. **Elle n'est pas indépendante.** Pour compenser, elle cherche délibérément à réfuter. Elle pourra être refaite par un agent indépendant après 15 h.

## 1. Défauts méthodologiques, par gravité

1. **Circularité du banc d'essai (grave).**
   - Les positions où le bilan de danger change la décision sont celles qui ont inspiré le module : Nozdormu (Tyrande) et Rexxar.
   - La seule différence de première action (1 paire sur 11) est la position Nozdormu.
   - **Ce n'est pas une validation, c'est une reproduction du cas d'origine.** Aucune position indépendante de la conception n'a été jouée par le modèle. La réserve contre l'IA (582 positions) n'a pas pu l'être : crédit OpenAI épuisé.
2. **Échantillon minuscule (grave).**
   - 11 paires complètes, dont 10 identiques.
   - Un test de signe exact sur 1 désaccord ne prouve rien.
   - L'absence de régression observée (aucun létal perdu, aucun témoin devenu défensif) ne vaut que pour ces 11 positions.
3. **Critère automatique trop étroit (moyen).** La « défense en un coup » classe « non défensif » le plan contre Rexxar, qui est pourtant une défense en quatre coups, et la position à 3 PV contre 3, où attaquer était juste. Le chiffre « 2 sur 6 défenses » ne mesure donc pas la qualité.
4. **La statistique « 4 alarmes sur 4, 0 fausse alarme » est en partie tautologique (moyen).**
   - Elle est mesurée sur l'état **à la fin de ton tour**, après tes actions. Un létal adverse visible à ce moment-là tue presque toujours : la mesure confirme surtout que l'adversaire voit son létal.
   - Au moment de décider, les alertes sont plus fréquentes : 28 sur 830 positions classées, 57 sur 582 contre l'IA. Certaines tombent dans des tours gagnés en attaquant, comme la position à 3 PV contre 3.
   - L'utilité réelle dépend d'une question **contrefactuelle non mesurée** : un affichage plus tôt aurait-il changé ton coup ?
5. **Indépendance de l'évaluateur stratégique (moyen).**
   - L'agent A est lui-même un LLM, de la famille Claude et non GPT, ce qui réduit sans l'annuler le risque de biais commun.
   - Il a vu les conseils en même temps que l'état sur 3 positions, dont la position Nozdormu.
   - Ses jugements sur les Découvertes et les mulligans sont faits sans plateau ni main : faibles.
6. **Impact attendu borné (moyen).**
   - Sur 44 parties, 5 défaites, dont 2 jugées évitables par une meilleure gestion du danger.
   - Même parfait, le module concerne quelques parties sur cent. C'est **invisible au taux de victoire** avec les volumes actuels : l'agent D estime 126 à 280 parties par bras pour un effet de cette taille, l'agent B environ 1 200 pour +3 points.

## 2. Verdicts sur les propositions

| Proposition | Verdict | Argument |
|---|---|---|
| P1. Alerte locale de danger corrigée et affichée | **justifiée** | Ce n'est pas une nouvelle idée : c'est la **correction d'une alerte existante, démontrée fausse**. Elle omettait le pouvoir héroïque et tes Provocations, n'apparaissait qu'au-delà du seuil, et a disparu à la décision fatale contre Rexxar. C'est du texte affiché, que le modèle ne reçoit pas : aucun risque de dégrader ses choix. Elle est instantanée, ce qui compte puisque tu joues en médiane toutes les 7 s. |
| P2. Bilan de danger envoyé au modèle | **plausible, non démontrée** | 1 seul changement sur 11, circulaire. Pas de régression observée, coût +5 %, latence identique. À valider sur positions indépendantes avant de l'activer. |
| P3. Mise en garde du validateur | **risquée** | Elle agirait sur la première action, pas sur la ligne entière. Elle pousserait à défendre trop tôt en cours de tour, comme dans la position à 3 PV contre 3. À ne faire qu'après une vérification de l'état final du plan. |
| P4. Cartes qui aident l'adversaire | **justifiée pour l'effet chiffrable seulement** (mise à 10 cristaux) ; **inutile sous sa forme large** | Le motif large signalait l'Animatrice dans 97 % des cas, ce qui est du bruit. Seul l'effet public, certain et chiffrable sur les cristaux mérite un affichage. Ailleurs, risque de surapprentissage à une seule carte. |
| P5. Échanges contre les Provocations dans le solveur | **plausible, risquée pour la confiance** | Le gain porte surtout sur la vitesse : les 25 tours de létal ont tous été trouvés. Or le « létal certain » est l'atout de confiance le plus solide (17 sur 17, 25 sur 25). Modéliser des échanges (Râles d'agonie, Boucliers, Réincarnation) risque un faux « certain ». Si cela se fait, en « conditionnel » d'abord. |
| P6. Banc d'essai fidèle au direct | **justifiée** | Infrastructure de mesure, sans risque pour le joueur ; sans elle, rien de P2, P7 ou P8 n'est mesurable. `+local` est fait ; il manque l'avis précédent, les Découvertes et les mulligans. |
| P7. Message allégé et réordonné | **plausible, non démontrée** | Elle change l'entrée du modèle, donc potentiellement la qualité. Gain d'environ 0,004 $ par appel. Exige une comparaison A/B. |
| P8. Suite du plan lue plus tôt dans le flux | **plausible, non démontrée** | Faire écrire le plan avant l'explication peut changer le raisonnement. Gain d'environ 1,8 s. |
| P9. Effort selon la difficulté, Fast mode | **peu utile en l'état** | low, medium et high choisissent la même action dans 81 à 89 % des cas, et high est trop lent. Le gain de latence de low est faible. Fast mode coûte 2× sans mesure. |
| P10. Contexte des Découvertes et mulligans, consigne Glissedrake | **preuves insuffisantes** | Jugements faits sans contexte de partie. Ajouter une règle au prompt à partir d'une dizaine d'observations, c'est du surapprentissage probable. Enregistrer le contexte d'abord, juger ensuite. |
| P11. Signaler la fatigue adverse | **utile mais mineure** | Un seul cas (Rexxar), mais le fait est public, exact et gratuit. Aucun risque s'il reste informatif. |
| P12. MCTS, ISMCTS, simulateur complet | **inutile ici** | Aucun simulateur à jour. Le coût du script des cartes est permanent. Pour ton seul tour, la recherche exhaustive existe déjà. |
| P13. Vote de plusieurs modèles en direct | **inutile** | Coût multiplié et latence du plus lent, pour un modèle qui se répète déjà (6 conseils sur 6 identiques à ceux de la partie). |

## 3. Ce que les autres analyses risquent de surestimer

- **« Le danger est la priorité ».** Il est prioritaire parmi les défauts démontrés, mais il est rare (3 à 4,5 % des décisions). L'essentiel des décisions n'a montré **aucune erreur nette** : 16 conseils bons sur 20 selon l'agent A, aucun illégal, létal fiable. Le module ne transformera pas la force du conseiller ; il retirera un type de défaite.
- **« Le modèle raisonne mal sur le danger ».** Le banc d'essai dit l'inverse pour Rexxar : avec du temps, il trouve la défense, même sans le bilan. Le défaut principal est le **délai par rapport à ton rythme**, pas le raisonnement.
- **« 55 % des conseils sont des étapes non réévaluées ».** C'est vrai. Mais la garde de continuité exige « rien de nouveau » et « aucun effet non prévu », et l'agent C ne relève qu'un défaut sur 45. Le risque est réel mais non démontré.

## 4. Avis sur la conclusion globale

- **« B — optimisations ciblées »** est la conclusion la plus défendable, à une condition : présenter honnêtement ce qui est démontré.
- **Démontré** : la correction d'affichage du danger, la fiabilité de l'état, de la légalité et du létal.
- **Non démontré** : un progrès de qualité stratégique du modèle grâce au contexte.
- **« C — nouveau palier »** n'est pas soutenu : aucune preuve qu'une autre architecture ferait mieux, et un simulateur est hors de portée.
- **« A — plateau »** est tentant pour la partie modèle : rien n'a été trouvé à corriger dans son raisonnement courant. Mais il reste des leviers locaux et démontrables.
- **« Preuves insuffisantes »** s'applique strictement au **gain du LLM** : crédit épuisé, 11 paires.
