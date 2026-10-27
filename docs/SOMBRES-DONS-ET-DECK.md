# Sombres dons, deck, pioche et tours suivants : audit et corrections

Itération du 25/10/2026, après la deuxième partie réelle. Le joueur n'a pas encore testé cette version en direct : rien ci-dessous n'est validé en partie réelle. Les mesures portent sur les Power.log enregistrés, les tests déterministes et 10 appels API ciblés (0,14 $).

## Réponses courtes

- **Sombres dons : le moteur les voyait, le modèle non.** À chaque Découverte, le log relie chaque proposition à son don, et le moteur conservait ce lien. Mais l'état envoyé au modèle ne contenait que la carte de base. À la Découverte du 25/10, le modèle a donc choisi Drake terrestre sans savoir que Concert promo-Drake avait Charge. **Corrigé** : chaque proposition porte maintenant son don, lu dans le log, et la carte qui en résulte. Vérifié sur les 45 Découvertes à Sombre don de tes 5 Power.log : 135 propositions, 135 dons identifiés, et 45 cartes choisies dont les statistiques réelles correspondent au calcul.
- **Deck : le modèle recevait les cartes restantes, mais ne s'en servait pas.** Dans 221 réponses enregistrées, il ne cite jamais une carte restante et ne calcule aucune probabilité. **Ajouté** : des probabilités calculées localement, exactes quand tout le deck est identifié, sinon annoncées comme estimations. Elles ne sont envoyées que si une pioche filtrée est en main ou si le dessus du deck est connu. Sur une décision réelle de pioche, leur effet sur la réponse n'est pas démontré.
- **Tours suivants.** Le modèle raisonnait déjà correctement sur la Parenté et sur la recharge de Profondeurs, grâce aux notes du deck : 2 décisions réelles sur 2. **Ajoutés** quand ils s'appliquent, comme faits exacts : coût du Wyrm au prochain tour, recharge du lieu quand Grommash est en main, et potentiel du prochain tour s'il suffit au létal. Ce dernier est étiqueté comme hypothèse.
- **Mulligan du 25/10 : tu as suivi le conseil.** Le log le prouve : Suffusion et Chronoseigneur sont repartis dans le deck, et Colporteur est resté en main. Le rapport précédent disait le contraire par erreur : ma commande d'audit lisait les cartes « choisies » comme rendues, alors qu'au mulligan ce sont les cartes gardées. C'est corrigé, et la trace enregistre désormais le résultat de chaque mulligan.
- **Calcul local « certain »** : six cas pouvaient produire un « certain » injustifié. Chacun fait maintenant passer le létal en « conditionnel ». La liste et le détail sont au §1.

## Déjà en place ou nouveau

| Élément | Avant cette itération | Nouveau |
|---|---|---|
| Solveur de létal, ordre des actions, quota, suite du plan | implémentés à l'itération précédente, non testés en direct | durcis (§1) |
| Cartes restantes du deck (`knownRemaining`, `knownAdded`) | envoyées au modèle depuis le MVP 1 | types et races utilisés pour les probabilités, retirés du message |
| Enchantements des cartes en main (don déjà appliqué) | visibles | inchangé |
| Don de chaque proposition de Découverte | **absent** de l'état et du message | `pendingChoice.offered[].darkGift` avec résultat calculé |
| Libellé du choix | « Choisir Drake terrestre » | « Choisir Drake terrestre + Courtes griffes » |
| Dessus du deck connu (Beaux rêves) | ignoré | `deck.knownTop`, prochaine pioche annoncée |
| Probabilités de pioche | aucune | `localAnalysis.deck` |
| Faits du tour suivant | notes du deck seulement | `localAnalysis.nextTurn` |
| Résultat du mulligan dans la trace | absent | entrée `résultat-mulligan` |
| Outils | `advisor-audit` | `discover-audit`, `advisor-probe` (comparaison payante plafonnée), décodage des codes de deck |

## 1. Le calcul local ne présente plus comme « certain » ce qu'il ne simule pas

« Certain » signifie : démontré sur l'état visible, avec les seules mécaniques simulées, et sans effet adverse connu qui puisse l'empêcher.

| Risque trouvé dans le code | Avant | Maintenant |
|---|---|---|
| Secret adverse **connu**, par exemple Bloc de glace | seuls les Secrets inconnus rendaient le létal conditionnel | tout Secret le rend conditionnel, avec son nom |
| Effet adverse qui empêche ou réduit les dégâts : Insensible, « devrait subir des dégâts… à la place », « ne subit que »… | ignoré | conditionnel, avec le nom du serviteur ou de l'arme |
| Provocation à Râle d'agonie ou Réincarnation détruite pour ouvrir la voie (Profondeurs, Chronoseigneur) | comptée comme retirée | la séquence ne peut être que conditionnelle ; une séquence sans ce risque est préférée |
| Drake terrestre ou Voyante réduits au silence | dégâts de fin de tour ou bonus des sorts comptés | ignorés |
| Serviteur avec Charge qui « ne peut pas attaquer les héros » | compté au visage | exclu |
| Gardien des couvées dont le client indique la condition non remplie (POWERED_UP) | épée comptée si notre calcul de races le permettait | épée ignorée |
| Attaque du héros | toute attaque proposée par le serveur | il faut une attaque vers le héros adverse proposée par le serveur |
| Arme aléatoire d'Animatrice | déjà conditionnelle | ajoute « l'adversaire reçoit aussi une arme aléatoire, non simulée » |

**Ajouté sans risque de surestimer** : Charge donnée par le Sombre don Somnambule. Elle n'est comptée que si l'enchantement EDR_100t6e est attaché à la carte, et cet identifiant est vu 12 fois dans tes logs.

Ce qui reste non simulé :
- les effets adverses qui ne correspondent à aucun de ces textes ;
- les échanges ;
- les autres effets aléatoires.

Le solveur ne cherche que des dégâts au visage.

## 2. Mulligan : recommandation, choix effectif, cartes reçues

Preuve, partie du 25/10, lignes 497 à 554 du fichier anonymisé :

```text
16:37:35.5 SendChoices() - id=1 ChoiceType=MULLIGAN
             m_chosenEntities[0]=[Colporteur de pétales id=22 …]
16:37:35.9 HIDE_ENTITY - [Suffusion d’ombreflamme id=4 …] tag=ZONE value=DECK
16:37:35.9 HIDE_ENTITY - [Chronoseigneur Époque id=13 …] tag=ZONE value=DECK
16:37:35.9 SHOW_ENTITY - Entity=14 CardID=END_021   (Forgeron dimensionnel)
16:37:35.9 SHOW_ENTITY - Entity=8  CardID=EDR_456   (Sombre chevaucheur)
16:37:36.2 SHOW_ENTITY - Entity=27 CardID=TIME_003  (Avant-garde du portail : pioche du tour 1)
```

- **Recommandation (trace)** : remplacer Chronoseigneur Époque et Suffusion d'ombreflamme, garder Colporteur de pétales.
- **Choix effectif** : Colporteur gardé (entité « choisie » envoyée au serveur), Suffusion et Chronoseigneur remplacés. Le changement de zone HAND → DECK le confirme.
- **Cartes reçues** : Forgeron dimensionnel et Sombre chevaucheur.
- **Pioche du tour 1** : Avant-garde du portail. Le rapport précédent indiquait Sombre chevaucheur à tort, parce que l'audit comptait les cartes reçues au mulligan comme des pioches. C'est corrigé.

La trace écrit maintenant, une fois par partie, une entrée `résultat-mulligan` avec ces champs : `mulliganAdvisedReplace`, `mulliganKept`, `mulliganReplaced`, `mulliganReceived` et `mulliganFollowed`. Elle n'est écrite qu'une fois l'échange appliqué par le serveur, avant la pioche du tour.

## 3. Sombres dons : audit du pipeline complet

Exemple réel, partie du 25/10, tour 1, Découverte de Sombre chevaucheur.

1. **Les trois choix dans le log.** Le serveur crée trois serviteurs (79, 80, 81) et trois cartes de don (82, 83, 84), puis les relie :
   ```text
   FULL_ENTITY - Creating ID=79 CardID=ETC_099   (Concert promo-Drake, COST 8, ATK 8, HEALTH 8)
   FULL_ENTITY - Creating ID=80 CardID=CATA_999  (Drake terrestre, COST 5, ATK 4, HEALTH 4)
   FULL_ENTITY - Creating ID=81 CardID=OG_317    (Seigneur Aile de mort, COST 10, ATK 12, HEALTH 12)
   FULL_ENTITY - Creating ID=82 CardID=EDR_100t6 … TAG_CHANGE Entity=79 tag=DARK_GIFT_ENTITY value=82
   FULL_ENTITY - Creating ID=83 CardID=EDR_100t2 … TAG_CHANGE Entity=80 tag=DARK_GIFT_ENTITY value=83
   FULL_ENTITY - Creating ID=84 CardID=EDR_100t5 … TAG_CHANGE Entity=81 tag=DARK_GIFT_ENTITY value=84
   ```
2. **Identité de chaque serviteur** : CardID en clair, donc connue du joueur.
3. **Bonus exact** : la carte du don (EDR_100t6 Somnambule, EDR_100t2 Courtes griffes, EDR_100t5 Cauchemar vivant), et son texte officiel dans la base de cartes. Rien n'est déduit du nom du serviteur.
4. **Statistiques et mots-clés modifiés.** Le don s'applique après le choix : le Drake passe à COST 3, avec l'enchantement EDR_100t2e. Avant le choix, les statistiques du log sont celles de base. Le résultat est donc calculé à partir du texte du don, puis vérifié sur les vraies parties (point 8).
5. **GameState.** Avant : `offered` contenait la carte, son coût, ses statistiques et son texte, mais aucun don. Maintenant :
   ```json
   {"name":"Concert promo-Drake","cost":8,"attack":8,"health":8,
    "darkGift":{"cardId":"EDR_100t6","name":"Somnambule","text":"Charge",
                "result":{"cost":8,"attack":8,"health":8,"addedKeywords":["CHARGE"]}}}
   ```
6. **Message envoyé à OpenAI.**
   - Avant : 8 723 caractères, sans don. Message reconstruit avec le même code, qui n'a pas changé pour les choix.
   - Maintenant : 9 848 caractères. Chaque proposition porte son don, et les libellés deviennent « Choisir Concert promo-Drake + Somnambule ».
   - Une phrase décrit ce champ dans les instructions : comparer les trois résultats complets.
7. **Justification du modèle.**
   - En direct, à 16:37:59, sans les dons : « Choisis Drake terrestre pour exercer une pression récurrente sur le héros adverse. » Raisonnement reconstruit sans les dons : « Drake terrestre comble le palier à 5 mana », alors qu'il coûtait 3.
   - Avec les dons (§6), même choix, mais raisonnement exact : « devient une 2/4 pour 3… Concert promo-Drake offre une 8/8 Charge pour 8… mais arrive beaucoup plus tard. Seigneur Aile de mort fournit une 12/12 et une copie 2/2 pour 10, trop lent ».
8. **Vérification sur toutes les vraies Découvertes** (`hscopilot discover-audit`, rapport dans [bench/partie2-strategie/decouvertes-sombres-dons.md](../bench/partie2-strategie/decouvertes-sombres-dons.md)) :
   - 45 Découvertes à Sombre don dans tes 5 Power.log : 26 de Sombre chevaucheur, 19 de Suffusion d'ombreflamme ;
   - 135 propositions, 135 dons identifiés par le tag du serveur ;
   - 45 cartes choisies sur 45 conformes au calcul. On compare coût, attaque, PV et mots-clés quand le joueur reprend la main, et la zone DECK pour Beaux rêves ;
   - une seule différence de coût, due à l'effet propre de la carte : Brute fièvresang, « Coûte (1) de moins pour chaque serviteur blessé ».

Les dons vus dans tes logs sont les 10 de l'annonce officielle de Blizzard (18/02/2025, Into the Emerald Dream).

| Don | Effet (texte officiel) | Proposé | Choisi et vérifié |
|---|---|---|---|
| Terreur en éveil | +3 ATQ et Vol de vie | 12 | 7 |
| Repos serein | +2/+2 et Insaisissable | 6 | 3 |
| Courtes griffes | coûte 2 de moins, −2 ATQ | 14 | 2 |
| Posture de défense | +4 PV et Provocation | 11 | 4 |
| Cauchemar vivant | une copie 2/2 invoquée quand on le joue | 19 | 4 (statistiques ; la copie n'est pas vérifiée) |
| Somnambule | Charge | 18 | 5 (tag CHARGE présent) |
| Réveil brutal | Cris de guerre déclenchés deux fois | 6 | 3 (statistiques ; le double déclenchement n'est pas vérifié) |
| Beaux rêves | +4/+5, placé sur le dessus du deck | 25 | 12 (zone DECK, statistiques) |
| Horreur persistante | Réincarnation avec PV et enchantements | 13 | 1 |
| Serres de harpie | Bouclier divin et Furie des vents | 11 | 4 (tags présents) |

La base de cartes contient deux autres dons : Démons intérieurs (Râle d'agonie : piochez 2) et Écailles du Cauchemar. Ils ne figurent ni dans l'annonce officielle ni dans tes logs : leur texte est transmis tel quel, sans vérification. Un don absent de la table garde son nom et son texte, sans résultat calculé.

D'après l'annonce, Courtes griffes n'est proposé que si l'attaque reste d'au moins 1, et Réveil brutal seulement sur un serviteur à Cri de guerre.

## 4. Le deck entier et la pioche

Ce que le modèle reçoit (`state.local.deck`) :
- **nombre de cartes** ;
- **cartes restantes** : liste HDT, moins toutes les cartes d'origine sorties du deck, même si elles ont changé de camp ;
- **cartes ajoutées connues** ;
- **cartes inconnues** ;
- **avertissement** si une carte sortie n'est pas dans la liste ;
- **nouveau** : `knownTop`, la carte placée sur le dessus du deck par Beaux rêves.

Les cartes jouées ou détruites sont aussi dans le cimetière, l'historique et la main, avec le marqueur « créée » et les enchantements. L'ordre du deck n'est jamais supposé. La seule position annoncée est celle qu'un effet rend publique : Beaux rêves, observé 12 fois dans tes logs. D'autres effets « placez sur le dessus ou le dessous » ne sont pas modélisés et ne sont donc pas annoncés.

Ton code de deck correspond exactement à la liste « Guerrier dragons » de HDT : 30 cartes, Standard, mêmes cartes et mêmes nombres (test `The_users_deck_code_matches_the_deck_selected_in_hdt`). Pendant la partie du 25/10, à chaque décision, aucune carte sortie du deck n'était hors de cette liste et aucune carte du deck n'était inconnue : le deck joué est bien celui-là.

**Usage par le modèle, avant.** Dans 221 réponses enregistrées, aucune ne cite une carte restante du deck, et aucune ne calcule de probabilité. Pourtant, 29 cas contenaient la liste. Le modèle écrit « selon la pioche ». La cause est le modèle, qui ne fait pas ce calcul, et non l'extracteur.

**Ajouté** (`localAnalysis.deck`, [DeckOdds](../src/HsCopilot.Core/Strategy/DeckOdds.cs)). Exemple réel, partie du 25/10, tour 3, 24 cartes dans le deck, liste entièrement identifiée :

```json
{"count":24,"basis":"exact : toutes les cartes du deck sont identifiées, ordre inconnu",
 "nextDraw":{"dragon":"14/24 (58 %)","minion":"19/24 (79 %)","spell":"3/24 (13 %)","minionCost5Plus":"7/24 (29 %)"},
 "filteredDraws":[{"card":"Avant-garde du portail","draws":"un serviteur du deck, au hasard parmi les serviteurs","candidates":19,
   "outcomes":["2/19 chacun (11 %, 20 % avec le second tirage du Rembobinage) : Wyrm du pic venteux, Glissedrake visionnaire, …",
               "1/19 chacun (5 %, 10 % avec le second tirage du Rembobinage) : Grommash Hurlenfer, …"]}]}
```

- **Exact** quand toutes les cartes du deck sont identifiées et que la liste est cohérente.
- **Estimation** sinon, avec le nombre de cartes inconnues.
- **Frappe anticipée** : sa condition (« si vous avez en main un serviteur coûtant 5 ou plus ») est lue dans la surbrillance du client (POWERED_UP). On ne la devine pas.
- **Rembobinage** : le second tirage repart du même deck, donc 1 − (1 − p)².
- **Envoyé seulement si utile** : une pioche filtrée en main, le dessus du deck connu, ou 5 cartes ou moins dans le deck.

## 5. Plusieurs tours

Ajoutés à `localAnalysis.nextTurn` quand ils s'appliquent :
- **mana du prochain tour** ;
- **Parenté**, par exemple : « Wyrm du pic venteux coûtera 8 au prochain tour, ou 6 si tu joues un Dragon ce tour ; Dragons jouables maintenant : Forgeron dimensionnel, Voyante… » ;
- **recharge de Profondeurs quand Grommash est en main** : un lieu activé au tour N est refusé au tour N+1, ce qui est vérifié sur toutes les parties enregistrées ;
- **potentiel du prochain tour si le plateau survit**, seulement s'il atteint les PV adverses. Il est étiqueté « hypothèse », ce n'est jamais un létal.

Situations de ta question :

| Situation | Décision réelle | Constat |
|---|---|---|
| A, Parenté | 24/10, partie 22, tour 5 : deux Wyrm à 8, Forgeron, Voyante et Animatrice jouables | avant comme après, le modèle joue un Dragon (Forgeron) « pour préparer un Wyrm à 6 ». Aucune lacune trouvée |
| B, Grommash, Profondeurs, Forgeron | 24/10, partie 13, tour 7 : Grommash, Profondeurs et Wyrm en main | avant comme après, le modèle garde Profondeurs pour Grommash au tour suivant, d'après les notes du deck. Seul l'ordre change (Glissedrake d'abord). Aucune lacune trouvée |
| C, contrôle du plateau, peu de cartes | aucune décision réelle comparée | non mesuré |
| D, Découverte avec don | 25/10 et 24/10 (§3, §6) | lacune réelle de données, corrigée |

Aucune règle rigide de type « joue toujours X avant Y » n'a été ajoutée. Une seule phrase s'ajoute aux instructions, et elle décrit le nouveau champ des dons.

## 6. Avant et après sur des décisions réelles

Commande : `hscopilot advisor-probe`, effort low, rapports dans [bench/partie2-strategie](../bench/partie2-strategie). Il y a une réponse par variante. Seules les nouvelles informations diffèrent entre les deux messages, et les instructions « avant » n'ont pas la phrase sur les dons. Sur un seul tirage, une différence n'est pas statistiquement significative.

| Décision | Avant | Après | Coût avant / après |
|---|---|---|---|
| 25/10, Découverte : Concert promo-Drake + Charge, Drake + Courtes griffes, Aile de mort + copie | Drake terrestre, « comble le palier à 5 mana » (faux : 3 avec le don) | Drake terrestre, compare correctement les trois résultats | 0,0131 / 0,0141 $ |
| 24/10, Découverte : Nozdormu + Beaux rêves, Dragon féerique + Posture, Ysera + Courtes griffes | Dragon féerique « 3/2 pour 2 » (faux : 3/6 Provocation) | Dragon féerique « 3/6 Provocation… Nozdormu 12/13 remplace la prochaine pioche et reste en sommeil 5 tours » | 0,0085 / 0,0097 $ |
| 25/10, tour 5 : Avant-garde en main | Avant-garde puis Gardien | même choix, sans citer de probabilité | 0,0147 / 0,0156 $ |
| 24/10, tour 7 : Grommash et Profondeurs | Profondeurs posée sans activation, puis Glissedrake | Glissedrake, puis Profondeurs gardée pour Grommash | 0,0178 / 0,0185 $ |
| 24/10, tour 5 : Parenté | Forgeron, « prépare un Wyrm à six manas » | même choix et même raison | 0,0149 / 0,0153 $ |

Coût total : 0,142 $, sous le plafond de 0,50 $. Sur les deux Découvertes, le choix est resté le même, mais il reposait sur une mauvaise lecture des cartes. Dans d'autres cas de tes logs, comme Concert promo-Drake + Charge pour un létal, la même lacune pourrait changer le choix ; ce n'est pas mesuré.

## 7. Taille des messages

Mesure sur toutes les décisions de 3 parties réelles, 97 décisions :

| Partie | Taille moyenne avant → après | Probabilités jointes | Tour suivant joint |
|---|---|---|---|
| 25/10 (Wyrm, Profondeurs) | 17 428 → 17 921 caractères (+2,8 %) | 7 décisions sur 20 | 10 sur 20 |
| Grommash + Profondeurs | 20 631 → 21 099 (+2,3 %) | 6 sur 40 | 25 sur 40 |
| 24/10 (Beaux rêves) | 23 347 → 24 318 (+4,2 %) | 37 sur 37 | 17 sur 37 |

Cela représente environ 150 à 300 jetons de plus, soit 0,0003 à 0,0006 $ par appel au tarif d'entrée hors cache. Le type et la race des cartes restantes servent au calcul local, mais sont retirés du message, parce qu'ils ajoutaient 400 à 600 caractères sans usage observé.

## 8. Tests ajoutés

Il y a 212 tests, tous réussis. 21 sont nouveaux, tous dans [DarkGiftAndDeckTests](../tests/HsCopilot.Core.Tests/DarkGiftAndDeckTests.cs).

**Sombres dons**, sur la Découverte réelle aux trois dons différents :
- trois dons et trois résultats exacts ;
- libellés et message ;
- carte en main après le choix ;
- choix sans don jamais complété ;
- don inconnu sans résultat inventé.

**Deck et pioche** :
- Beaux rêves : dessus du deck connu, puis vidé après la pioche (nouvelle fixture anonymisée du 24/10) ;
- probabilités exactes et estimations ;
- Avant-garde sans condition, avec Rembobinage ;
- envoi seulement si utile ;
- code de deck identique à la liste HDT.

**Tour suivant** :
- Parenté du Wyrm ;
- recharge de Profondeurs avec Grommash ;
- potentiel joint seulement s'il atteint le létal.

**Prudence du solveur** :
- Secret connu ;
- effet adverse « Insensible » et arme qui absorbe les dégâts ;
- Provocation à Râle d'agonie ;
- Drake réduit au silence ;
- Charge par don, seulement avec l'enchantement ;
- Gardien des couvées refusé par le client.

**Mulligan** : conseil, cartes gardées, remplacées et reçues sur la partie réelle.

**Jeu de cartes des tests** : 23 cartes ajoutées, dont tous les Sombres dons.

## 9. Limites

- Rien n'est encore observé en direct : dons dans l'overlay, entrée `résultat-mulligan`, probabilités, faits du tour suivant.
- Les effets « Cauchemar vivant » (copie) et « Réveil brutal » (Cri de guerre doublé) ne sont pas simulés par le solveur. Ils restent une note pour le modèle.
- Le dessus du deck n'est connu que pour Beaux rêves. Un mélange ultérieur du deck n'est pas détecté : l'annonce dit « sauf si le deck a été mélangé depuis ».
- Les probabilités supposent un tirage uniforme parmi les cartes concernées. C'est la règle habituelle de Hearthstone, mais elle n'est pas vérifiée carte par carte.
- La situation C (plateau contrôlé, peu de cartes) n'a pas été comparée sur une décision réelle.
- Le comportement du modèle n'est mesuré que sur 5 décisions, avec un tirage chacune.

## 10. Prochain test en direct

1. Lance `tools\lancer-copilote.ps1`. Il ferme l'ancienne instance et recompile.
2. Joue une partie, idéalement avec Sombre chevaucheur ou Suffusion. À chaque Découverte, l'overlay doit afficher « CHOIX : Choisir X + nom du don ».
3. Après la partie, envoie-moi la trace `.jsonl` de `%LocalAppData%\HsCopilot\advisor`. Elle doit contenir une ligne `résultat-mulligan`, et les conseils de Découverte doivent citer les dons.
4. Facultatif, sans appel API :
   ```powershell
   src\HsCopilot.Cli\bin\Debug\net10.0-windows\hscopilot.exe discover-audit "C:\Program Files (x86)\Hearthstone\Logs" --out decouvertes.md
   ```
