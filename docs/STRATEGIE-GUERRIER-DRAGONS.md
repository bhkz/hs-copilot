# Guerrier Dragons : synthèse stratégique sourcée

Deck analysé : celui du joueur, code `AAECAQcEi6AEqKAEhJ0H8a8HDePmBqr8Bqv8BveDB+iHB9KXB5KkB7etB5iwB4+xB9OyB9ayB+yyBwAA`. C'est un deck Standard de 30 cartes, identique à la liste « Guerrier dragons » de HDT. Document du 25/10/2026.

Chaque recommandation indique sa base :

| Étiquette | Signification |
|---|---|
| **[texte]** | texte officiel de la carte (base 255130) |
| **[vérifié]** | mécanique observée dans tes logs et couverte par un test |
| **[calcul]** | probabilité exacte sur les 30 cartes |
| **[source]** | source externe datée, voir le tableau des sources |
| **[hypothèse]** | raisonnement non démontré |

Rien ici n'est une règle absolue.

## Sources et proximité avec ton deck

| Source | Date | Version, format | Contenu | Proximité |
|---|---|---|---|---|
| [Blizzard : Into the Emerald Dream](https://hearthstone.blizzard.com/en-us/news/24179067) | 18/02/2025 | annonce d'extension | fonctionnement et liste officielle des 10 Sombres dons, règles de présence | officiel, s'applique à Sombre chevaucheur et Suffusion |
| [Vicious Syndicate : Vanilla Dragon Warrior](https://www.vicioussyndicate.com/decks/vanilla-dragon-warrior/) | 18/02/2026 | ère Across the Timeways, Standard | liste seule, sans texte | **13 de tes 17 cartes**, soit 24 cartes sur 30 |
| [Vicious Syndicate : Fire Dragon Warrior](https://www.vicioussyndicate.com/decks/fire-dragon-warrior/) | 07/04/2026 | ère Cataclysm, Standard | liste seule | **12 de tes 17**, soit 22 sur 30, dont Grommash et Voyante |
| [HearthPwn : New Dragon Warrior (SunZero)](https://www.hearthpwn.com/decks/1457124-new-dragon-warrior-diamond-to-high-legend) | 05 au 13/11/2025 | lancement d'Across the Timeways, en-tête Wild | mulligan, plan de jeu | 8 de tes 17 ; liste différente (Blood Fighters, Tortolla) |
| [Hearthstone Top Decks : #39 Legend (Cantelope)](https://www.hearthstonetopdecks.com/decks/dragon-warrior-39-legend-cantelope-across-the-timeways/) | 11/12/2025 | Standard, saison 141 | liste seule | 11 de tes 17, avec Chronoseigneur Époque |
| [vS Data Reaper n°354](https://www.vicioussyndicate.com/vs-data-reaper-report-354/) | 30/07/2026 | après l'affaiblissement de Salamandre dimensionnelle | Guerrier Dragons tombé à 47 % de victoires | autre version du deck, avec Salamandre et Maman cane |
| [vS Data Reaper n°358](https://www.vicioussyndicate.com/vs-data-reaper-report-358/) | 17/09/2026 | méta de septembre | « utterly dominant » ; contres : Chevalier de la mort Œuf, Prêtre Voleur | autre version, « Cannoneer » |
| [Metastats : Dragon Warrior](https://metastats.net/hearthstone/archetype/Dragon-Warrior/) | octobre 2026 | Reign of the Black Empire, Standard | 4 listes, 52 à 166 parties chacune | **7 de tes 17** : Darkrider, Profondeurs, Gardien, Suffusion, Animatrice, Glissedrake, Wyrm |

Ce qui manque :
- La page HSReplay de ton deck et HSGuru refusent l'accès automatique (erreur 403). Leurs taux de victoire, leurs données de mulligan et leurs résultats par classe n'ont donc pas été lus.
- Aucune source n'offre un guide écrit pour ta liste exacte.
- Les listes d'octobre 2026 ont changé de cœur : Salamandre dimensionnelle, Maman cane, Maître canonnier, Fissure torride, Lardeur. Leurs conseils ne s'appliquent pas tels quels à ta version.
- Ta liste ressemble à celles de février à avril 2026. Vu les listes d'octobre, elle n'est probablement plus la version la plus jouée [hypothèse].

## Ton deck en chiffres [texte, calcul]

| Coût | Cartes | Nombre |
|---|---|---|
| 1 | Profondeurs Sanguines ×2, Sombre chevaucheur ×2, Voyante distordue par le temps ×2 | 6 |
| 2 | Colporteur de pétales ×2, Frappe anticipée ×2, Gardien des couvées ×2, Luisaile, Suffusion d'ombreflamme ×2 | 9 |
| 3 | Avant-garde du portail ×2, Dragonnet du Vol infini ×2, Forgeron dimensionnel ×2 | 6 |
| 4 | Animatrice de stade ×2 | 2 |
| 6 | Chronoseigneur Époque | 1 |
| 7 | Glissedrake visionnaire ×2 (4 avec un autre Dragon en main) | 2 |
| 8 | Grommash Hurlenfer, Le Grand Dracorex, Wyrm du pic venteux ×2 (6 avec la Parenté) | 4 |

- **Composition** : 24 serviteurs, dont 18 Dragons ; 4 sorts ; 2 lieux. Sombre chevaucheur est un Démon, Gardien des couvées n'a pas de type, et Le Grand Dracorex est une Bête [vérifié].
- **Dragon en main de départ** [calcul] : au moins un parmi 3 cartes à 94,6 %, parmi 4 cartes à 98,2 %. Les conditions « si tu as un Dragon en main » (Sombre chevaucheur, Gardien des couvées, Glissedrake) sont donc presque toujours actives au départ. Attention : jouer ses Dragons peut ensuite les désactiver.
- **Grommash à temps** [calcul, sans pioche supplémentaire] : vu à 36,7 % au tour 8 si tu commences (11 cartes), à 40 % sinon (12 cartes). Grommash et au moins une Profondeurs : 21 à 25 %. Le combo Grommash + Profondeurs est donc un bonus, pas un plan fiable.
- **Wyrm du pic venteux** : au moins un exemplaire parmi 10 cartes à 56 % [calcul].

## Plan de jeu et conditions de victoire

- **Source** [source : SunZero, 11/2025, autre liste] : deck tempo qui gagne en accumulant des statistiques. On va au visage et on prépare le létal quand assez de dégâts sont en main, sinon on joue pour le plateau. Les décisions qui comptent : le choix des Sombres dons, quand échanger, quand développer.
- **Sources de dégâts directs de ta liste** [texte, vérifié] :
  - Wyrm du pic venteux : 5 dégâts sur n'importe quelle cible et 5 d'armure ;
  - Suffusion d'ombreflamme : 2, ou 4 avec une Voyante blessée ;
  - Frappe anticipée : 3, ou 5 avec la Voyante ;
  - Grommash avec Profondeurs : 12 avec Charge ;
  - épée du Gardien des couvées : 2 ;
  - Drake terrestre découvert : 4 en fin de tour ;
  - arme aléatoire d'Animatrice de stade.
- **Contrôle du plateau** : Le Grand Dracorex (Ruée, puis dégâts à tous les autres serviteurs adverses), Chronoseigneur Époque (détruit les serviteurs joués au tour adverse précédent), échanges avec des serviteurs renforcés par Avant-garde et Forgeron [texte].

## Mulligan

**Général** [source : SunZero, 11/2025, sur une autre liste] :
- garder Sombre chevaucheur ;
- garder Avant-garde du portail si tu as un coup à 1 ou 2 mana ;
- garder Gardien des couvées contre un deck tempo, si tu as aussi un coup à 1 mana et un Dragon ;
- sinon, mulligan agressif.

**Ajustements pour ta liste** [hypothèse, à valider sur tes parties] :
- Profondeurs Sanguines et la Voyante sont des coups à 1 mana qui servent la suite (bonus d'ATQ, bonus des sorts). Elles valent au moins une carte à 2 mana.
- Les cartes à 7 et 8 mana se renvoient, sauf Glissedrake si tu gardes un autre Dragon : il coûte alors 4.

**Par classe adverse** : aucune source datée ne couvre ta liste. Les données HSReplay du deck n'ont pas pu être lues (erreur 403). Le copilote enregistre désormais chaque mulligan (conseil, cartes gardées, remplacées et reçues), ce qui permettra de mesurer ces choix sur tes propres parties.

## Combos et interactions

| Interaction | Effet | Base |
|---|---|---|
| Profondeurs sur un serviteur **avant** son attaque | +2 dégâts : Wyrm 8 → 10 dans ta partie du 25/10 | [vérifié] |
| Grommash, puis Profondeurs sur lui | Grommash blessé : 4 + 6 + 2 = 12 avec Charge, 14 si le Forgeron l'a renforcé en main ; 8 mana et un lieu prêt | [vérifié] |
| Recharge du lieu | activé au tour N, refusé au tour N+1, de nouveau utilisable au tour N+2 | [vérifié sur toutes tes parties] |
| Profondeurs sur la Voyante | Voyante blessée : +2 aux sorts, donc Suffusion 4 et Frappe 5 | [vérifié] |
| Voyante, Profondeurs, Suffusion et Frappe le même tour | 1 + 1 + 2 + 2 = 6 mana pour 9 dégâts au visage | [calcul, solveur] |
| Parenté du Wyrm | jouer un Dragon ce tour : Wyrm à 6 au tour suivant | [vérifié] |
| Glissedrake visionnaire | coûte 3 de moins avec un **autre** Dragon en main | [texte, vérifié] |
| Avant-garde du portail | pioche un serviteur aléatoire du deck, +2/+2 ; Rembobinage pour un second tirage | [texte] |
| Forgeron dimensionnel | +2 ATQ aux serviteurs et armes en main : Grommash, Wyrm, serviteurs découverts | [texte] |
| Animatrice de stade | arme aléatoire pour les deux joueurs, la tienne +1/+1 ; létal seulement conditionnel | [texte, vérifié] |
| Frappe anticipée | pioche un serviteur si tu as en main un serviteur coûtant 5 ou plus ; la surbrillance du jeu indique si la condition est remplie | [texte] ; coût actuel ou de base : non tranché |
| Colporteur de pétales | +1/+1 à un **autre** Dragon allié en fin de tour | [texte, vérifié] |

## Priorités de pioche

- **Quoi piocher** : avec Avant-garde du portail ou Frappe anticipée, le résultat est un serviteur au hasard parmi ceux qui restent. Le copilote calcule désormais la liste exacte des résultats possibles et leur probabilité, avec et sans second tirage [calcul].
- **Piocher n'est pas toujours mieux** : Avant-garde est un corps 2/2 pour 3, et c'est le serviteur pioché qui reçoit +2/+2. Sa valeur se compare à l'autre coup du tour, pas au simple « +1 carte » [texte, hypothèse].
- **Beaux rêves** : la carte découverte part sur le dessus du deck. Elle remplace donc ta prochaine pioche, et le copilote le sait [vérifié].

## Gestion des lieux (Profondeurs Sanguines)

- **Charges et recharge** : 3 charges ; activé, le lieu est indisponible au tour suivant [vérifié]. Si Grommash est en main et que 8 mana arrivent au prochain tour, l'activer maintenant interdit le combo ; le copilote le signale.
- **Usages** :
  - +2 ATQ avant une attaque au visage ;
  - blesser sa propre Voyante ;
  - achever un serviteur adverse à 1 PV, ou retirer une Provocation à 1 PV ;
  - blesser Grommash.
- **À éviter** : viser son propre serviteur à 1 PV (le lieu inflige 1 dégât), ou une Provocation à Râle d'agonie ou Réincarnation en comptant sur un létal certain [vérifié dans le solveur].

## Sombres dons

Liste officielle, et ce que chaque don change pour ce deck :

| Don | Effet | Usage dans ton deck | Base |
|---|---|---|---|
| Somnambule | Charge | dégâts immédiats ; un gros Dragon avec Charge peut finir une partie | [texte] et [hypothèse] |
| Courtes griffes | coûte 2 de moins, −2 ATQ | tempo, et peut activer Parenté ou Glissedrake plus tôt | [texte] et [hypothèse] |
| Beaux rêves | +4/+5, dessus du deck | gros corps garanti au prochain tirage, mais il prend la place de la pioche | [vérifié] |
| Posture de défense | +4 PV et Provocation | défense contre les decks agressifs | [texte] |
| Serres de harpie | Bouclier divin et Furie des vents | double attaque protégée | [texte] |
| Terreur en éveil | +3 ATQ et Vol de vie | course aux PV | [texte] |
| Repos serein | +2/+2 et Insaisissable | protégé des sorts adverses | [texte] |
| Cauchemar vivant | copie 2/2 en le jouant | largeur du plateau ; aussi plus de cibles pour le Colporteur | [texte] |
| Réveil brutal | Cri de guerre ×2 | sur un serviteur à Cri de guerre seulement : un Wyrm infligerait 5 + 5 | [texte] ; non simulé par le solveur |
| Horreur persistante | Réincarnation | résistance au nettoyage du plateau | [texte] |

À la Découverte, compare la carte **avec** son don. Le copilote donne maintenant les trois résultats complets ; avant, il ne voyait que les cartes [vérifié].

## En avance, en retard

- **En avance sur le plateau** [hypothèse, cohérente avec SunZero] :
  - garde les échanges qui protègent Colporteur et Voyante ;
  - prépare un tour de dégâts : Parenté pour le Wyrm, Profondeurs prête pour Grommash ;
  - compte le létal sur deux tours plutôt que de piocher par réflexe.
- **En retard** [hypothèse] :
  - Le Grand Dracorex et Chronoseigneur Époque sont tes cartes de reprise ;
  - le Wyrm donne 5 d'armure ;
  - Posture de défense et Repos serein sont les dons défensifs.

## Erreurs fréquentes

| Erreur | Ce qui s'est passé | Base |
|---|---|---|
| Attaquer avant de jouer le bonus d'ATQ disponible | tour 7 du 25/10 : le conseil de l'IA, corrigé depuis | [vérifié] |
| Activer Profondeurs le tour qui précède le tour de Grommash | perte du combo | [vérifié] |
| Jouer le dernier Dragon de la main avant Gardien des couvées ou Glissedrake | leur condition tombe | [texte] |
| Finir le tour avec du mana et des dégâts au visage inutilisés | tour 7 du 25/10 : 2 mana et Suffusion restés en main | [vérifié] |
| Juger une Découverte sans son don | avant cette version, l'IA le faisait | [vérifié] |

## Plans sur deux ou trois tours

1. **Parenté** : au tour N, joue un Dragon (Avant-garde, Forgeron, Colporteur…). Au tour N+1, Wyrm à 6 : 5 dégâts et 5 d'armure ; le mana restant sert à un autre coup [vérifié].
2. **Grommash** :
   - Profondeurs posée tôt, pour 1 mana ;
   - au tour 7, ne pas l'activer ;
   - au tour 8, Grommash puis Profondeurs : 12 dégâts, 14 avec le Forgeron joué avant, qui renforce Grommash en main [vérifié].
   - Probabilité d'avoir les deux pièces au tour 8 : 21 à 25 % sans pioche supplémentaire [calcul].
3. **Dégâts de sorts** : Voyante au tour N, puis au tour N+1 Profondeurs sur elle, Suffusion et Frappe : 9 dégâts pour 4 mana si le lieu est déjà posé, 5 sinon, à condition que la Voyante survive [calcul].
4. **Découverte qui prépare un combo** :
   - un serviteur avec Charge (Somnambule) se garde pour un tour de létal ;
   - un don Courtes griffes sur un Dragon peut débloquer la Parenté un tour plus tôt.
   - [hypothèse] Le copilote compare désormais les trois résultats.

Ces plans reposent sur ton plateau qui survit et sur l'adversaire. Le copilote les présente comme des hypothèses, jamais comme des certitudes.
