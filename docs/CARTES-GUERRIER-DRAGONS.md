# Deck « Guerrier dragons » : effets vérifiés (préparation du MVP 3)

Source des textes : CardDefs de HearthSim téléchargés par HDT, **build de données 255130** (textes français officiels).
Liste : `PlayerDecks.xml` de HDT, deck « Guerrier dragons » (modifié le 24/10/2026). Elle est reprise dans `config/copilot.example.json`.
Colonne « Observé » : comportement constaté dans les logs réels de tes parties.

Ce document n'est pas encore le fichier de stratégie demandé en section 6. Il sert de base vérifiée pour l'écrire au MVP 3.

## Liste et textes actuels

| Carte (ID) | ×| Coût | Stats | Type | Texte actuel |
|---|---|---|---|---|---|
| Profondeurs Sanguines (`CORE_REV_990`) | 2 | 1 | 3 charges | Lieu | Inflige 1 point de dégâts à un serviteur et lui donne +2 ATQ. |
| Sombre chevaucheur (`EDR_456`) | 2 | 1 | 1/1 | **Démon** | Cri de guerre : découvre un Dragon avec un Sombre don si vous avez un Dragon en main. |
| Voyante distordue par le temps (`END_022`) | 2 | 1 | 1/3 | Dragon | A +2 aux dégâts des sorts tant qu'elle est blessée. |
| Colporteur de pétales (`EDR_889`) | 2 | 2 | 1/4 | Dragon | À la fin de votre tour, donne +1/+1 à un **autre** Dragon allié aléatoire. |
| Frappe anticipée (`TIME_750`) | 2 | 2 | — | Sort | Inflige 3 points de dégâts. Si vous avez en main un serviteur coûtant (5) cristaux ou plus, vous piochez un serviteur. |
| Gardien des couvées (`EDR_457`) | 2 | 2 | 2/3 | — | Cri de guerre : si vous avez un Dragon en main, vous équipe d'une épée 2/2. |
| Luisaile (`CORE_EX1_189`) | 1 | 2 | 3/2 | Dragon | Cri de guerre : ajoute un serviteur légendaire aléatoire dans votre main. |
| Suffusion d'ombreflamme (`FIR_939`) | 2 | 2 | — | Sort (Feu) | Inflige 2 points de dégâts. Découvre un serviteur de guerrier avec un Sombre don. |
| Avant-garde du portail (`TIME_003`) | 2 | 3 | 2/2 | Dragon | Rembobinage. Cri de guerre : vous piochez un serviteur aléatoire. Lui donne +2/+2. |
| Dragonnet du Vol infini (`TIME_045`) | 2 | 3 | 1/4 | Dragon | Toxicité. Réincarnation. |
| Forgeron dimensionnel (`END_021`) | 2 | 3 | 2/5 | Dragon | Cri de guerre : donne +2 ATQ aux serviteurs et aux armes dans votre main. |
| Animatrice de stade (`TIME_034`) | 2 | 4 | 3/3 | Dragon | Rembobinage. Cri de guerre : les deux adversaires s'équipent d'une arme aléatoire. Donne +1/+1 à la vôtre. |
| Chronoseigneur Époque (`TIME_714`) | 1 | 6 | 7/5 | Dragon | Cri de guerre : détruit chaque serviteur que votre adversaire a **joué** au tour précédent. |
| Glissedrake visionnaire (`END_033`) | 2 | 7 | 5/8 | Dragon | Insaisissable. Coûte (3) cristaux de moins si vous avez un **autre** Dragon en main. |
| Grommash Hurlenfer (`CORE_EX1_414`) | 1 | 8 | 4/9 | — | Charge. A +6 ATQ s'il est blessé. |
| Le Grand Dracorex (`DINO_401`) | 1 | 8 | 5/12 | Bête | Ruée. Une fois que ce serviteur a attaqué un serviteur adverse, il inflige des dégâts à TOUS les autres serviteurs adverses. |
| Wyrm du pic venteux (`TLC_600`) | 2 | 8 | 6/6 | Dragon | Cri de guerre : inflige 5 points de dégâts et vous gagnez 5 points d'armure. Parenté : coûte (2) cristaux de moins. |

18 cartes sur 30 sont des Dragons. **Sombre chevaucheur est un Démon** et Gardien des couvées n'a pas de type : aucun des deux ne compte comme Dragon en main.

## Points du cahier des charges, confrontés aux textes et aux logs

| Point | Texte actuel | Observé dans les logs |
|---|---|---|
| Activation « Dragon en main » | Sombre chevaucheur et Gardien des couvées : un Dragon en main ; Glissedrake : un **autre** Dragon en main (lui-même ne compte pas) | `POWERED_UP=1` sur la carte dans la main quand la condition est remplie : exposé dans `hand[].poweredUp` |
| Glissedrake à coût réduit | −3 avec un autre Dragon en main | coût en main 4 au lieu de 7 (tag `COST`), conforme |
| Parenté du Wyrm | Parenté : −2 si une carte partageant un type a été jouée au tour précédent (ici un Dragon) | Wyrm à 6 au lieu de 8 au tour suivant un Dragonnet joué |
| Voyante blessée | +2 aux dégâts des sorts tant qu'elle est blessée | `SPELLPOWER=2` sur chaque Voyante blessée (19 observations) ; dégâts réels avec une Voyante blessée en jeu : Frappe anticipée 5 (3 + 2), Suffusion 4 (2 + 2) ; une Suffusion a même ciblé ta propre Voyante pour l'activer |
| Colporteur de pétales | fin de **votre** tour, +1/+1 à un **autre** Dragon allié **aléatoire** | déclenchement présent dans l'historique |
| Forgeron dimensionnel | +2 ATQ aux serviteurs **et aux armes** en main | enchantement visible sur les cartes en main (`hand[].enchantments`) |
| Grommash + Profondeurs Sanguines | Grommash 4 ATQ, +6 s'il est blessé ; Profondeurs : 1 dégât et +2 ATQ | observé dans une partie réelle : Grommash à 6 (Forgeron en main) → 8 → 14 une fois blessé, 14 dégâts au héros le tour où il est joué ; au tour suivant, seconde activation : 18. Sans Forgeron : 12. Exige un lieu **prêt** |
| Recharge des lieux | (règle du jeu, absente du texte) | 44 activations réelles : charge −1 et lieu épuisé ; refusé le tour même et au tour joueur suivant (149 options sur 149, `REQ_NOT_EXHAUSTED_LOCATION`), toujours accepté ensuite. Conséquence : activer les Profondeurs au tour N les rend indisponibles pour un tour Grommash en N+1 |
| Chronoseigneur Époque | serviteurs **joués** par l'adversaire au tour précédent | `opponent.lastTurn.minionsPlayed` ne retient que les cartes jouées depuis la main, pas les invocations, avec « encore en jeu » |
| Rembobinage | mot-clé Rembobinage | après le cri de guerre, choix « Ligne temporelle actuelle » / « Ligne temporelle passée » ; si on rembobine, le serveur rétablit l'état et rejoue l'effet (bloc `GAME_RESET`). Tag `USED_REWIND=1` sur la carte ensuite : **un seul** Rembobinage par carte |

## Vérifications complémentaires (logs du 22 au 25/10/2026)

- **Wyrm du pic venteux** : le serveur propose comme cible n'importe quel personnage, y compris les deux héros et les serviteurs alliés (75 options jouables). Il le refuse si ton plateau est plein (`REQ_MINION_CAP`). Coût observé : 8, ou 6 avec la Parenté.
- **Parenté** : sur toutes les parties de test, le Wyrm coûte 6 si et seulement si un Dragon a été joué au tour précédent.
- **Glissedrake** : coût 4 et condition remplie si et seulement si un autre Dragon est en main.
- **Profondeurs Sanguines sur la Voyante** : le lieu a été activé au moins une fois sur une Voyante intacte. Son dégât n'est pas un sort, mais il blesse la Voyante, ce qui active son bonus pour les sorts suivants (les dégâts de sorts majorés ont été observés, l'enchaînement exact lieu puis sort dans un même tour n'a pas été isolé).

## Points restant incertains (à vérifier avant de les coder comme règles)

- **Frappe anticipée et Glissedrake réduit** : sur 21 Frappes observées, la pioche a eu lieu chaque fois qu'un serviteur à coût actuel ≥ 5 était en main, et jamais sans serviteur ≥ 5. Aucune partie ne contient le cas qui tranche : un Glissedrake réduit à 4 comme seul serviteur de base ≥ 5. La règle (coût actuel ou de base) reste inconnue.
- **Durée de recharge** des autres lieux : non observée.
