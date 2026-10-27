# Continuité du plan : La pièce et les coups préparatoires

Question du 25/10/2026 : « Quand tu me recommandes La pièce, tu sembles relancer une analyse pour décider quelle carte jouer. »

Réponse : **c'était vrai, et c'est corrigé.** Les deux cas observés en classé ont coûté un second appel. Le défaut ne venait pas du plan du modèle mais de l'annulation de l'appel.

## 1. Ce que montrent les traces

| Partie | Début de réponse | Ton coup | Ce qui a suivi |
|---|---|---|---|
| 2, tour 2 | « Jouer La pièce », 19:36:36.9 | La pièce, 3,4 s plus tard | appel annulé ; nouvel appel ; 4,4 s plus tard, début de réponse « Jouer Forgeron dimensionnel », jamais complété |
| 3, tour 2 | « Jouer La pièce », 19:48:49.9 | La pièce, 3,5 s plus tard | appel annulé ; nouvel appel ; 7,8 s plus tard, début de réponse « Jouer Gorgonzormu » |

**L'IA avait-elle prévu la carte suivante ?** Pour ces deux cas, on ne peut pas le savoir. La suite du plan (`followUp`) arrive à la fin de la réponse, après l'action, et l'appel a été annulé avant.

Le seul autre cas des traces montre que le plan existait et fonctionnait. À 18:35, contre l'IA, la réponse complète « Jouer La pièce » est arrivée avant ton coup, avec la suite « Jouer Gardien des couvées, activé par les Dragons en main ». Après La pièce, cette étape s'est affichée en 0,0 s, sans appel (origine « plan »).

Plus généralement, 18 réponses complètes du modèle sur 27 contenaient une suite du plan.

**Pourquoi un nouvel appel ?** Le début de réponse ne contient que l'action. Dès que tu la jouais, la décision analysée était « passée » et l'appel était annulé, pour ne jamais afficher un conseil périmé. Cette règle ne distinguait pas « tu as joué exactement le coup conseillé » de « l'état a changé autrement ».

## 2. La règle : changement attendu ou imprévu

**Changement attendu par le plan.** Depuis l'état analysé :
- tu as joué une seule action, celle du début de réponse (même carte, même cible) ;
- c'est toujours ton tour, et aucun choix n'est à faire (Découverte, Rembobinage…) ;
- rien de nouveau n'est apparu :
  - aucune carte nouvelle en main (pioche ou carte générée) ;
  - aucun nouveau serviteur adverse ;
  - aucun serviteur à toi venu d'ailleurs que ta main ;
  - même nombre de Secrets adverses : un Secret posé, déclenché ou révélé compte comme imprévu ;
  - même nombre de cartes dans la main adverse ;
  - mêmes armes ;
- **aucun effet non prévu** (ajouté après la relecture du 25/10/2026 : une étape légale n'est pas forcément encore pertinente). Seules la carte qui agit et sa cible peuvent changer librement, ainsi que ton héros pour un pouvoir héroïque. Ailleurs, sont imprévus :
  - un serviteur blessé, mort ou renvoyé, ou dont un statut change (Gel, Bouclier divin, Provocation, Silence, Camouflage…) ;
  - le héros adverse blessé, soigné, qui gagne ou perd de l'armure, gelé ou insensible, car le létal en dépend ;
  - ton héros blessé ou qui perd de l'armure ;
  - une attaque ou des PV maximum modifiés sans qu'aucun serviteur concerné par le coup n'arrive sur le plateau ni ne le quitte.

  Sont acceptés : ton héros soigné ou cuirassé (Wyrm du pic venteux : « vous gagnez 5 points d'armure »), et une aura qui apparaît ou disparaît avec le serviteur joué ou tué (Oracle sinistrécaille : « vos autres Murlocs ont +1 ATQ »).

Cette garde s'applique aussi à la reprise du plan après un conseil complet, qui existait avant.

Dans ce cas :
1. L'appel continue. La décision suivante attend, avec le message « Coup conseillé joué : suite du plan attendue de l'analyse en cours… ».
2. La réponse complète est validée sur l'état analysé. Elle n'est jamais affichée, puisque son action est déjà jouée ; elle devient le plan en cours.
3. Sa première étape de suite doit exister dans les nouvelles options du serveur, avec la même source et la même cible. Elle repasse par le validateur : légalité, mises en garde, plafond du létal.
4. Elle s'affiche alors comme conseil d'origine « plan », sans appel.

**Tout autre cas est un changement imprévu** : l'appel est annulé et un nouvel appel part, comme avant. Il en va de même si la réponse n'a pas de suite, si l'étape n'est plus légale, ou en cas d'erreur ou de délai : le nouvel appel part dès la fin de l'appel conservé.

**Ce qui ne change pas** :
- le létal démontré par le calcul local reste prioritaire ;
- la réanalyse manuelle reste disponible ;
- les vérifications de légalité, le retrait des conseils périmés, les quotas et les budgets sont les mêmes.

**Retour à l'ancien comportement** : `"dropCallWhenPartialPlayed": true` dans la section `advisor` de `copilot.json`.

## 3. Mesure sur tes trois parties classées

L'audit `hscopilot advisor-audit` a une nouvelle section, « Débuts de réponse sans conseil complet ». Elle applique la même règle que le conseiller à chaque appel annulé après son début de réponse :

| Partie | Débuts de réponse sans conseil complet | Joués tels quels, sans imprévu | Dont suivis d'un nouvel appel |
|---|---|---|---|
| 1 | 3 | 2 | 1 |
| 2 | 16 | 10 | 8 (dont La pièce) |
| 3 | 15 | 9 | 6 (dont La pièce) |

Sur ces 15 nouveaux appels :
- tu as joué en médiane 4,2 s après le début de réponse ;
- le nouvel appel a mis en médiane 7,7 s avant son premier affichage, et n'est allé au bout que 5 fois.

**Estimation, non mesurée :** dans ces parties, la réponse complète arrivait environ 6 s après le premier fragment (médianes : 9,8 − 4,0 s en partie 2, 7,8 − 1,7 s en partie 3). La suite du plan arriverait donc environ 2 s après ton coup, au lieu de 7,7 s pour le seul début de réponse d'un nouvel appel.

Les changements imprévus détectés sont surtout :
- des choix : Sombre chevaucheur, Suffusion d'ombreflamme, Avant-garde du portail ;
- des cartes ou entités créées : l'épée de Gardien des couvées, le fromage donné par Gorgonzormu, les trois serviteurs aléatoires de Fromage savoureux ;
- des fins de tour.

**Effet de la garde « aucun effet non prévu ».** L'audit a aussi une section « Suites du plan affichées sans appel », qui rejuge chaque reprise passée avec la règle actuelle.
- Sur les 12 reprises réelles (6 en classé, 6 contre l'IA à 18 h 34), une première version stricte en refusait 2. Les deux étaient des faux positifs, car l'effet était écrit sur la carte jouée : l'armure du Wyrm du pic venteux, et l'aura de l'Oracle sinistrécaille tué par Gorgonzormu.
- La version actuelle les accepte toutes les 12, et ne retire aucun des 15 appels évitables.
- En contrepartie, aucune de ces parties ne contenait d'effet vraiment imprévu de ce type : la garde n'est démontrée que par les tests.

## 4. Coût et risque

- **Suite utilisable** : un appel de moins (environ 0,016 $), et l'étape suivante, complète, environ 2 s après ton coup.
- **Suite inutilisable** : le nouvel appel part environ 2 s plus tard qu'avant, à la fin de l'appel conservé. Le coût est quasi identique : un appel terminé coûte environ 0,016 $, un appel annulé est estimé à 0,015 $, car il est facturé quand même.
- **15 est un maximum** : chaque appel évité suppose que le modèle donne une suite légale, ce qui arrive dans 18 réponses complètes sur 27.

## 5. Tests

[PlanContinuityTests.cs](../tests/HsCopilot.Core.Tests/PlanContinuityTests.cs) contient 12 tests sur des états réels :
- La pièce puis Gardien des couvées (partie de 18 h 34) ;
- Profondeurs Sanguines puis le Wyrm (partie de 16 h 37).

| Test | Résultat attendu |
|---|---|
| La pièce jouée sur le début de réponse | un seul appel, Gardien des couvées affiché comme « plan » ; la réponse de La pièce n'est jamais affichée |
| Réponse arrivée avant les nouvelles options | même résultat |
| Carte préparatoire (Profondeurs Sanguines) | activation sur le Wyrm sans nouvel appel |
| Autre action que le début de réponse | annulation, nouvel appel |
| Carte apparue en main | imprévu : nouvel appel |
| Secret adverse déclenché par La pièce | imprévu : nouvel appel |
| Réponse sans suite | nouvel appel dès la fin de l'appel conservé |
| Ancien comportement | deux appels, comme dans tes parties |
| Effets sur d'autres entités | serviteur blessé, statut changé, armure adverse, ton héros blessé : imprévus ; ton héros cuirassé : accepté |
| Aura | acceptée si le serviteur attaqué meurt ; refusée si rien n'arrive ni ne part |
| Appel conservé, effet imprévu (Wyrm blessé avant son activation) | annulation, nouvel appel |
| Conseil complet suivi, effet imprévu | pas de reprise du plan, nouvel appel |

Les deux derniers tests échouent sans la garde. Il y a 236 tests, tous réussis, stables sur cinq passages des tests de minutage. Une simulation complète de la partie 2 n'a montré aucun blocage.

## 6. Limites

- **Pas encore observé en direct.** Les compteurs « appels conservés (début de réponse joué) », « suites de plan reçues après le coup joué » et « suites de plan affichées sans appel » du rapport de partie le montreront.
- **Règle volontairement prudente.** Un effet certain qui crée une carte ou une entité compte comme une information nouvelle et coûte un appel : l'épée de Gardien des couvées, le fromage de Gorgonzormu, la copie de Cauchemar vivant.
- **Pas de simulateur.** La garde ne prédit pas l'effet de la carte : elle exige que rien ne change en dehors de la carte et de sa cible. Conséquences :
  - un effet de zone ou un renforcement de tes autres serviteurs, pourtant écrit sur la carte, coûte un nouvel appel ;
  - un déclencheur adverse qui ne fait que renforcer ses serviteurs passe pour une aura, s'il coïncide avec l'arrivée ou la mort d'un serviteur concerné par le coup ;
  - la main (statistiques des cartes) et le mana ne sont pas comparés.
- **Pertinence de l'étape.** Une étape qui passe la garde n'est pas réévaluée par le modèle ; elle est seulement revérifiée par le validateur (légalité, mises en garde, plafond du létal).
- **Les Découvertes et les autres choix ne sont pas concernés** : la carte choisie est une information nouvelle.

## 7. À vérifier en partie contre l'IA

1. **Quand tu joues second** (La pièce en main) : joue La pièce dès le début de réponse qui la conseille, sans attendre le conseil complet.
2. **À l'écran, attendu** : « Coup conseillé joué : suite du plan attendue… », puis, environ 2 s plus tard, la carte suivante, marquée comme suite du plan et non comme nouvelle analyse.
3. **Le même schéma avec une carte préparatoire** : Profondeurs Sanguines puis activation, ou Forgeron dimensionnel puis la carte renforcée.
4. **Dans le rapport de partie** (`%LocalAppData%\HsCopilot\reports`), regarder trois compteurs :
   - « appels conservés (début de réponse joué) » ;
   - « suites de plan reçues après le coup joué » ;
   - « suites de plan affichées sans appel ».
5. **Après la partie**, passer `hscopilot advisor-audit` : il donne le détail cas par cas. La section « Suites du plan affichées sans appel » indique si chaque reprise respecte la garde.
