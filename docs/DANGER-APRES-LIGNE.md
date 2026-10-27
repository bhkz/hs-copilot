# Danger après la ligne conseillée : contrôle hors ligne

Piste 1 de la [synthèse de la contre-expertise](SYNTHESE-CONTRE-EXPERTISE.md), le 26/10/2026.
- **Ce qui a été fait** : tout hors ligne, sur les replays existants.
- **Ce qui n'a pas changé** : aucun appel OpenAI, aucun changement du prompt, aucun conseil remplacé, rien de nouveau dans l'overlay.

**Où trouver les données** :
- **le rapport brut** : [bench/danger-apres-ligne/rapport-brut.md](../bench/danger-apres-ligne/rapport-brut.md) ;
- **une ligne JSON par décision** : [decisions.jsonl](../bench/danger-apres-ligne/decisions.jsonl).

## Réponse courte

**Ce contrôle n'aurait pas ajouté d'avertissements partout. Mais dans tes parties, il n'aurait non plus évité aucune mauvaise décision du conseiller, faute d'en trouver de ce type.**

- **Sur les 1 056 conseils affichés en classé**, dont 664 du modèle, 323 suites de plan et 69 létaux du solveur :
  - **aucun** ne fait perdre la dernière défense simulée contre un létal visible ;
  - **aucun** n'aggrave nettement le danger.
- **Quand le danger visible était élevé** (23 décisions conseillées, dans 7 parties), le conseil défendait, restait neutre, ou il n'existait déjà plus aucune défense simulée.
- **Le seul coup signalé** est le tien contre Rexxar, au tour fatal : l'attaque du Glissedrake au visage, alors que le budget de session bloquait le modèle et qu'**aucun conseil n'était affiché**.
- **La défaite contre Tyrande est hors de portée** de ce contrôle. Le danger venait de la main adverse et de Nozdormu, que le contrôle déclare « non simulable » en nommant le cadeau à l'adversaire.
- **Ce qui a de la valeur est ailleurs.** La recherche trouve la **meilleure ligne défensive en plusieurs coups**, avec la marge qu'elle laisse. Contre Rexxar : tuer le Parle-esprit, tuer le Serpent éternel, puis l'armure, ce qui laisse une marge de 9. L'alerte actuelle ne montre que des défenses en un coup. Cette ligne apparaîtrait dans 8 positions classées sur 1 139.

**Ma recommandation** : ne pas brancher ce contrôle comme mise en garde sur les conseils. Rien dans tes données ne montre qu'il en éviterait une mauvaise, et il ajoute un risque de défense excessive. La suite raisonnable serait d'**afficher la ligne défensive simulée dans l'alerte existante**, seulement quand il y a un létal visible et qu'une défense existe. Il faudrait la valider sur tes prochaines parties, puisque Rexxar est un cas de développement. Je n'ai rien branché.

## 1. Ce que fait le contrôle

Le contrôle mesure, avant et après chaque coup, la marge de l'alerte de danger : tes PV, ton armure et tes Provocations, moins les dégâts adverses visibles au prochain tour. Il calcule ensuite la **meilleure marge atteignable** ce tour, avec les seules défenses simulées :
- les attaques et les dégâts directs sur les menaces ;
- la destruction par le Calamar pas cuit et par le Chronoseigneur Époque ;
- l'armure du pouvoir héroïque ;
- les Provocations jouées ;
- l'épée du Gardien des couvées ;
- les Profondeurs Sanguines ;
- La pièce.

| Simulé (validé sur l'état réel suivant, §3) | Rend l'évaluation **partielle** | Rend le coup **non évalué** |
|---|---|---|
| Combats, avec Bouclier divin, Toxicité et Vol de vie, y compris celui des armes | Secret adverse inconnu, sur toute attaque ou carte jouée | Cris de guerre et sorts aux effets non chiffrés : Animatrice de stade, Fromage savoureux, Grand Dracorex, Nozdormu… |
| Dégâts directs ciblés (Frappe anticipée, Suffusion, Wyrm et son armure, Armurière) | Râle d'agonie ou Réincarnation d'un serviteur qui meurt | Cartes qui aident aussi l'adversaire, nommées comme telles |
| Corps des serviteurs dont le texte ne touche pas le plateau pendant ton tour : pioche, Découverte, bonus en main, coût, effet de fin de tour | Déclencheur en jeu qui réagit aux actions (Poing-crochet 3000…) | Lieux autres que les Profondeurs |
| Armure, épée 2/2, destruction par le Calamar et par le Chronoseigneur | Aura en jeu quand un serviteur meurt | |
| | Effet non simulé de ton arme | |
| | Serviteur à Ruée ou Charge joué : son attaque n'est pas simulée | |
| | Attaque adverse qui dépend du tour (« pendant le tour de votre adversaire ») | |
| | Carte non simulée de ta main qui pourrait défendre, quand le verdict dit qu'aucune défense ne reste | |

**Verdicts** :
- **danger visible faible** : marges supérieures à 5. Ce n'est jamais « ligne sûre », puisque la main adverse n'est pas comptée.
- **défend** ;
- **neutre** ;
- **aucune défense simulée** : déjà perdu sur le visible ;
- **perd la dernière défense** (critère strict) : une défense simulée évitait le létal visible, et après le coup il n'en reste plus ;
- **aggrave nettement** (critère large) : la meilleure marge baisse d'au moins 3 points et finit à 2 ou moins ;
- **létal ce tour** : jamais signalé ;
- **non évalué** : le coup n'est pas simulé.

## 2. Données

- **46 parties classées** rejouées depuis les journaux HDT, avec la trace des conseils affichés. C'est 1 139 décisions, dont 1 056 reliées à un conseil affiché. Les 1 056 libellés ont tous été reliés à une action légale.
- **3 parties contre l'IA** avec trace, soit 58 décisions.
- **547 coups figés contre l'IA**, sans trace ni issue : leurs journaux bruts n'existent plus. Ils ne servent qu'à mesurer la fréquence des signalements.
- **Doublons** : les renvois de HDT au démarrage d'une session sont écartés par identifiant de partie.
- **Origine du conseil** : celui affiché en premier pour l'état exact de la décision, qu'il s'agisse d'un début de réponse, d'un conseil complet, d'une suite de plan ou d'un létal du solveur.

## 3. Validation de la simulation contre la réalité

Chaque coup joué a été simulé, puis comparé à l'état réel de la décision suivante du même tour.

| Statut | Transitions | Marge identique à la réalité | Meilleure marge identique |
|---|---|---|---|
| complète | 485 | **484 (100 %)** | 475 (98 %) |
| partielle | 231 | 210 (91 %) | 211 (91 %) |

- **Le seul écart en « complète »** est une anomalie du journal : la décision suivante arrive 2 min 15 plus tard, probablement après une reconnexion.
- **Trois écarts ont été corrigés** en chemin :
  - le déclencheur de Poing-crochet 3000 (+4 armure) ;
  - le Vol de vie d'une arme (Lames de guerre des Aldrachi) ;
  - l'aura du Loup alpha redoutable après une mort.
- **Les évaluations partielles sont réellement incertaines** : 9 % d'écarts, contre 0,2 % pour les complètes.
- **Deux tests** (§9) refont ces vérifications sur toutes les parties des fixtures :
  - la marge de départ est celle de l'alerte affichée ;
  - chaque coup « complet » donne l'état réel suivant.

## 4. Les conseils affichés, y compris les suites de plan

| Verdict | Conseils | complète | partielle | non évalué | modèle | plan | solveur |
|---|---|---|---|---|---|---|---|
| danger visible faible | 899 | 674 | 225 | — | 590 | 309 | 0 |
| non évalué | 70 | — | — | 70 | 60 | 10 | 0 |
| létal ce tour | 69 | 41 | 22 | 6 | 0 | 0 | 69 |
| défend | 7 | 2 | 5 | — | 5 | 2 | 0 |
| neutre | 7 | 2 | 5 | — | 7 | 0 | 0 |
| aucune défense simulée | 4 | 3 | 1 | — | 2 | 2 | 0 |
| **perd la dernière défense** | **0** | | | | | | |
| **aggrave nettement** | **0** | | | | | | |

**Les 323 suites de plan**, affichées sans nouvel appel :
- 309 en danger visible faible ;
- 10 non évaluées ;
- 2 qui défendent ;
- 2 en « aucune défense simulée », toutes deux après que le danger est apparu, dans des parties abandonnées.

Aucune suite de plan ne perd une défense.

**Côté parties contre l'IA**, il n'y a aucun signalement :
- sur les 22 conseils des 3 parties avec trace ;
- sur les 547 coups figés.

## 5. Les coups que tu as joués

| Partie | Tour | Coup | Conseil | Verdict | Marge maintenant → après | Meilleure défense avant → après | Issue |
|---|---|---|---|---|---|---|---|
| Rexxar, 0 h 44 | 16.3 | Attaquer avec Glissedrake visionnaire → Rexxar | **aucun** (budget atteint) | aggrave nettement, **partielle** (Secret inconnu) | 0 → 0 | 9 → 2 | mort au tour adverse suivant |

C'est le seul coup joué signalé, sur 1 139 en classé et 547 contre l'IA. Le Secret s'est bien déclenché, et la marge réelle est tombée à −2 au coup suivant. C'est exactement l'incertitude que signalait le statut partiel.

## 6. La séquence du tour : meilleure défense au début, marge réelle à la fin

Tu suis le conseil sur l'immense majorité des coups. La marge réelle en fin de tour mesure donc la séquence conseillée telle qu'elle a été jouée.

| Partie | Tour | Marge au début | Meilleure défense au début | Marge réelle à la fin | Conseils suivis | Issue |
|---|---|---|---|---|---|---|
| 21 h 35 (Rexxar) | 6 | 5 | 13 | 13 | 1 sur 3 | survie, victoire |
| 21 h 45 (Orweyna) | 5 | 5 | 10 | 8 | 2 sur 3 | survie, victoire |
| 21 h 45 (Orweyna) | 6 | 0 | 8 | 8 | 5 sur 5 | victoire au tour suivant |
| 23 h 07 (Uther) | 6 | 2 | 14 | 14 | 3 sur 3 | survie |
| 23 h 07 (Uther) | 8 | 4 | 13 | 13 | 3 sur 7 | survie |
| 23 h 07 (Uther) | 9 | −6 | 0 | (abandon) | 2 sur 4 | abandon |
| 0 h 09 (roi-liche) | 9 | 2 | 9 | **2** | 3 sur 5 | survie |
| 0 h 09 (roi-liche) | 10 | −25 | −8 | (abandon) | 3 sur 6 | abandon |
| Rexxar, 0 h 31 | 16 | −2 | 9 | **0** | 0 sur 5 (aucun conseil) | **mort** |
| 1 h 07 (Garrosh, roi des pirates) | 6 | 5 | 25 | 16 | 7 sur 8 | victoire au tour suivant |
| 2 h 12 (Jaina) | 13 | −4 | −1 | −1 | 2 sur 3 | abandon |

Deux tours perdent au moins 3 points de marge et finissent à 2 ou moins :
- **contre Rexxar**, sans conseil affiché ;
- **contre le roi-liche à 0 h 09, tour 9** : trois conseils de développement (Avant-garde, Dragonnet, Avant-garde) ont utilisé le mana au lieu de l'armure, de l'attaque du Wyrm et des Profondeurs. La marge passe de 9 possibles à 2. Tu as survécu au tour adverse, puis tu as abandonné face à 25 dégâts visibles de trop, ce qu'une marge de 9 n'aurait pas changé.

Le contrôle coup par coup ne voyait pas ce cumul : aucun coup ne franchissait le seuil seul. Le bilan par tour le voit.

## 7. Contre-exemples : l'agressivité malgré le danger

Il n'y en a pas assez pour une statistique, mais assez pour fixer une règle. **Ne jamais exiger la défense maximale.**

- **Orweyna, 21 h 45, tour 6** : marge 0. Le conseil, Wyrm du pic venteux au visage d'Orweyna, est offensif, mais ses 5 points d'armure portent la marge à 5. Victoire au tour suivant.
- **Garrosh, roi des pirates, 1 h 07, tour 6** : marge 5 au début. Le conseil défend d'abord, puis envoie le reste au visage. Le tour finit à 16, au lieu des 25 possibles. Victoire au tour suivant.
- **Orweyna, 21 h 45, tour 5** : le conseil n'a pas pris la défense maximale (10 possibles, 8 obtenus). Victoire.
- **Zek’voz, 2 h 02, tour 8** : marge 0, défense simulée jusqu'à 32. Le conseil attaque l'Aile du Vide, et la partie est gagnée ce tour-là.

**Une règle « défends dès que la marge est de 5 ou moins »** aurait contredit ces quatre lignes gagnantes. C'est pourquoi le critère retenu ne signale que le passage vers un létal visible.

**Aucun conseil n'a proposé d'attaquer au visage** avec une marge de 1 à 5. On ne peut donc pas mesurer de faux positifs sur ce type de position.

## 8. Revue contradictoire des cas détectés

> Revue faite par moi, qui ai aussi conçu le contrôle. **Elle n'est pas indépendante.** Rexxar et Tyrande ont servi à concevoir le module : ils ne valident rien.

| Cas | Ce que dit le contrôle | Objection | Ce qui en reste |
|---|---|---|---|
| **Rexxar, tour 16.3** (développement) | ton attaque au visage fait passer la meilleure défense de 9 à 2 | la défense « à 9 » attaquait aussi, et le Secret pouvait se déclencher sur elle. Elle n'était pas garantie, d'où le statut partiel. Et la main de Rexxar n'est pas comptée | un signal juste, mais sur ton coup, pas sur un conseil : un contrôle des conseils n'aurait rien vu, puisqu'il n'y en avait pas. Seule l'alerte de danger, avec la ligne défensive, aurait pu t'aider |
| **Tyrande, Nozdormu** (développement) | non évalué, cadeau à l'adversaire nommé ; marge visible de 9 à 11 | le danger mortel était dans la main adverse | **hors de portée** : seul le signal sur les 10 cristaux, déjà affiché, couvre ce cas |
| **Roi-liche, 0 h 09, tour 9** | bilan du tour : 9 possibles, 2 obtenus | « neutre » coup par coup ; les deux décisions de fin de tour n'ont pas pu être reliées à un coup ; et l'adversaire a ensuite montré 25 dégâts de trop | une perte réelle, mais sans conséquence démontrée sur l'issue |
| **Uther, tour 9** | marge −6 ; meilleure défense 0, puis 2 après les Profondeurs | l'Animatrice, non évaluée, donnait aussi une arme à l'adversaire | partie déjà perdue sur le visible ; abandon |
| **Jaina, 2 h 12, tour 13** | aucune défense simulée, statut complet | aucune carte de ta main ne pouvait défendre | déjà perdu : le contrôle ne reproche rien au conseil |

**Objections au contrôle lui-même** :
- **La recherche est une borne basse.** Une défense par une carte non simulée lui échappe. Elle le signale, mais elle peut dire « aucune défense » à tort.
- **La mesure du danger reprend les approximations de l'alerte** :
  - la protection des Provocations est approchée ;
  - une attaque adverse qui change selon le tour, comme celle du Rampeur du goudron, est surestimée ;
  - un serviteur qui ne peut pas attaquer les héros est compté.

  Ces deux derniers cas sont maintenant signalés comme incertains.
- **Le critère large a été réglé après avoir vu Rexxar.** Le seul cas qu'il détecte est celui qui l'a inspiré : c'est circulaire.
- **Données** : ce sont tes parties de développement, et aucun jugement humain n'a été fait.

## 9. Mesures demandées

| Mesure | Valeur |
|---|---|
| Signalements stricts sur les conseils | **0 sur 1 056** : borne haute à 95 % de 0,3 % des conseils, soit 3 sur 1 056 |
| Signalements larges sur les conseils | **0 sur 1 056** |
| Signalements sur les coups joués | 1 sur 1 139 en classé (Rexxar, sans conseil), 0 sur 547 contre l'IA |
| Alertes pertinentes | 1 : le seul signalement a été suivi d'une mort. Une précision sur 1 cas ne veut rien dire |
| Faux positifs | aucun observé ; avec si peu d'alertes, ce n'est pas estimable |
| Positions en danger (marge de 5 ou moins, hors létal) | 29 sur 1 139 |
| Létal visible évitable par une défense simulée | 8 positions sur 1 139, dans 4 parties. Avec un conseil affiché, la ligne conseillée a défendu (Orweyna) ou gagné ce tour (Zek’voz). Contre Uther, la meilleure défense simulée laissait au mieux +2, et tu as abandonné. Contre Rexxar, il n'y avait aucun conseil |
| Statut des 1 056 conseils | complète 722 (68 %), partielle 258 (24 %), **non évalué 76 (7 %)** |
| Coût du calcul | médiane **0,03 ms**, P95 3,3 ms, **maximum 355 ms** (11 572 états explorés), aucune recherche tronquée ; 3,5 s pour toute l'analyse |

En direct, le maximum de 355 ms serait à plafonner : le nombre d'états explorés est déjà borné.

## 10. Modifications et tests

| Fichier | Contenu |
|---|---|
| `src/HsCopilot.Core/Strategy/LineDanger.cs` (nouveau) | simulation prudente, meilleure défense, verdicts et statuts ; **utilisé seulement hors ligne** |
| `src/HsCopilot.Core/Strategy/ThreatAssessment.cs` | quatre expressions rendues internes pour être partagées, sans changement de comportement |
| `src/HsCopilot.Cli/LineDangerCommand.cs` (nouveau) | `hscopilot line-danger <journaux…> [--traces dossier] [--cases positions.jsonl] --out rapport.md --jsonl decisions.jsonl` |
| `tests/HsCopilot.Core.Tests/LineDangerTests.cs` (nouveau) | 6 tests, listés ci-dessous |

Les 6 tests de `LineDangerTests.cs` :
1. **La marge de départ** est celle de l'alerte, sur toutes les décisions des fixtures.
2. **Chaque coup « complet »** donne l'état réel suivant, sur toutes les fixtures.
3. **Rexxar**, en trois points :
   - l'attaque au visage passe de 9 à 2, en évaluation partielle à cause du Secret ;
   - tuer le Serpent défend ;
   - finir le tour perd la dernière défense.
4. **Le Râle d'agonie de l'Ours** rend l'évaluation partielle.
5. **Nozdormu** est non évalué, avec le cadeau à l'adversaire nommé.
6. **Un létal démontré** n'est jamais signalé.

**260 tests réussis.** Rien n'est branché dans l'application en direct.

## 11. Limites

- Une seule action est simulée à la fois, et la séquence est jugée sur l'état réel de fin de tour. La suite prévue par le modèle n'est pas rejouée, car la trace n'en garde que le texte.
- La main, les tirages et les Secrets adverses ne sont pas chiffrés.
- Les 547 coups figés contre l'IA viennent de parties où tu laissais parfois l'IA en vie pour tester des cartes : ils ne mesurent qu'une fréquence.
- Aucune position indépendante du développement n'a été jugée.

## 12. Suite possible, à valider avant tout branchement

1. **Afficher la meilleure ligne défensive simulée dans l'alerte de danger existante**, seulement quand :
   - il y a un létal visible ;
   - une défense simulée existe ;
   - le létal n'est pas démontré pour toi.

   C'est le cas de 8 positions classées sur 1 139. Il faudrait d'abord le mesurer sur tes prochaines parties, et ne jamais changer le conseil lui-même.
2. **Ajouter à l'audit de partie le bilan par tour** (§6), pour suivre les pertes de marge cumulées sans rien afficher.
3. **Ne pas brancher le contrôle comme mise en garde sur les conseils**, tant qu'une position indépendante ne montre pas un conseil qui perd une défense.
