# Repères stratégiques : Guerrier Dragons

Ces repères orientent l'évaluation. Ce ne sont pas des règles absolues : la situation réelle (plateau, PV, main, menaces) prime toujours.
Les mécaniques marquées « vérifié » ont été observées dans les logs réels du joueur (build client 254477, données 255130).

## Mécaniques du deck

- **Dragons en main** (vérifié) : Sombre chevaucheur et Gardien des couvées s'activent si un Dragon est en main ; Glissedrake visionnaire coûte 3 de moins si un **autre** Dragon est en main. Le champ `poweredUp` d'une carte en main indique que la condition est remplie. Sombre chevaucheur est un Démon et Gardien des couvées n'a pas de type : ils ne comptent pas comme Dragons.
- **Parenté** (vérifié) : Wyrm du pic venteux coûte 2 de moins si un Dragon a été joué au tour précédent (`local.lastTurn.typesPlayed` contient DRAGON). Jouer un Dragon ce tour-ci prépare un Wyrm à 6 au tour suivant. Son cri de guerre inflige 5 dégâts à n'importe quel personnage et donne 5 d'armure.
- **Voyante distordue par le temps** (vérifié) : +2 aux dégâts des sorts tant qu'elle est blessée. Frappe anticipée inflige alors 5, Suffusion d'ombreflamme 4. Il est possible de la blesser soi-même (Suffusion ou Profondeurs Sanguines sur elle) pour activer le bonus.
- **Colporteur de pétales** : à la fin de ton tour, +1/+1 à un **autre** Dragon allié aléatoire. Il a besoin d'un autre Dragon en jeu pour avoir de la valeur.
- **Forgeron dimensionnel** : +2 ATQ aux serviteurs et aux armes en main, de façon permanente.
- **Grommash Hurlenfer + Profondeurs Sanguines** (vérifié) : Grommash (Charge, +6 ATQ s'il est blessé) joué puis ciblé par les Profondeurs (1 dégât, +2 ATQ) frappe pour 12, ou 14 s'il a reçu le bonus du Forgeron en main. Il faut 8 mana pour Grommash et un lieu **prêt** (l'activation du lieu ne coûte pas de mana).
- **Profondeurs Sanguines avant l'attaque** (vérifié, partie du 25/10/2026) : activer le lieu sur un serviteur qui n'a pas encore attaqué ajoute 2 dégâts à son attaque (Wyrm 8 → 10). Attaquer d'abord perd ce bonus pour le tour. Le calcul local (`localAnalysis.ordering`) le signale.
- **Animatrice de stade pour un létal** : l'arme est aléatoire (+1/+1 compris). Ce n'est un létal que sous condition (`localAnalysis.conditions` donne l'ATQ minimale et une estimation de chance) ; le Rembobinage permet un second tirage.
- **Recharge des lieux** (vérifié) : un lieu activé est indisponible le reste du tour **et tout le tour suivant** du joueur, puis à nouveau utilisable. Activer les Profondeurs au tour N les rend indisponibles pour un tour Grommash en N+1. Chaque activation consomme une charge (3 au départ).
- **Chronoseigneur Époque** : détruit les serviteurs que l'adversaire a **joués** à son dernier tour (`opponent.lastTurn.minionsPlayed`, mention « encore en jeu »), pas ceux qu'il a seulement invoqués.
- **Rembobinage** (vérifié) : après le cri de guerre d'Avant-garde du portail ou d'Animatrice de stade, un choix permet de rejouer l'effet une fois pour un autre résultat aléatoire.
- **Frappe anticipée** : 3 dégâts ; pioche un serviteur si un serviteur coûtant 5 ou plus est en main. Incertain : un Glissedrake réduit à 4 compte-t-il ? Ne pas compter dessus.
- **Le Grand Dracorex** (Bête, Ruée) : après avoir attaqué un serviteur adverse, inflige des dégâts à tous les autres serviteurs adverses.

## Priorités d'évaluation (indicatives)

1. Létal disponible ou préparable sans risque majeur.
2. Survie : menace de létal adverse visible sur le plateau (attaque totale adverse + arme contre tes PV et ton armure).
3. Utilisation efficace du mana ce tour-ci, sans gaspiller une ressource clé (lieu prêt, Grommash, Chronoseigneur).
4. Préparation du tour suivant : Parenté, Dragon en main pour les activations, lieu prêt pour Grommash.
5. Échanges : préférer les échanges où ton serviteur survit ou tue une menace supérieure.

## Prudence

- Les cartes cachées de l'adversaire (main, deck, Secrets) sont inconnues : ne jamais affirmer ce qu'il possède.
- Seule l'action immédiate est garantie légale par le serveur. Les étapes suivantes d'un plan peuvent devenir impossibles après résolution (effets aléatoires, Découverte, Rembobinage, réactions adverses).
