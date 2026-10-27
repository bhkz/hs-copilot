# Agent A : audit stratégique des conseils de HS Copilot (Partie 2)

Rédigé le 26/10/2026 par l'agent A, expert en stratégie Hearthstone. Je juge chaque décision avec la seule information visible au moment où elle a été prise. Je n'ai ouvert ni les journaux, ni les traces, ni les bilans, ni les fichiers de résultats. Les textes de cartes viennent de la base `cards.255130.frFR.json`, en lecture seule.

## Résumé

- **20 positions figées** :
  - 16 conseils bons, dont 2 avec une réserve mineure ;
  - 1 conseil bon pour l'action immédiate, mais avec une suite perfectible ;
  - 3 conseils discutables ;
  - aucune erreur nette.

  Le conseiller est solide sur le létal, la préparation sur deux tours (Grommash et Profondeurs, Parenté), l'ordre des actions qui cherche l'information d'abord, et la prudence face aux Secrets.
- **Ses faiblesses sont des pertes de valeur, pas des contresens** :
  - il dépense des charges de Profondeurs pour 2 dégâts ou un bonus sur une cible faible ;
  - il préfère le visage à un échange qui protégerait un serviteur clé quand il mène au plateau ;
  - ses suites d'attaques ne sont pas toujours optimales : le héros retire un Bouclier divin alors qu'un serviteur à Réincarnation le ferait gratuitement ;
  - il ne prend pas toujours la marge disponible quand le létal est exact.
- **Découvertes à Sombre don** (27 jugées) : 20 choix bons, dont 5 très bons, 7 discutables, aucune erreur sûre. Il valorise bien Beaux rêves sur une carte bon marché, Réveil brutal sur un Cri de guerre fort, et Charge pour la pression.
- **Mulligans** (43 lus, 30 jugés en détail) :
  - les cartes à 6 et 8 mana sont toujours rendues, ce qui est correct ;
  - erreur récurrente : rendre Glissedrake alors qu'un autre Dragon gardé le met à 4 mana (5 cas, contre 4 cas où il le garde) ;
  - mulligans trop agressifs qui rendent des Dragons à 3 mana et affaiblissent les conditions « Dragon en main ».

## Méthode et limites

- Je n'ai ni simulateur ni accès aux tirages. Mes lignes alternatives sont calculées à la main sur l'état visible. Les réponses adverses ne sont qu'estimées d'après la classe, le plateau et les cartes déjà jouées.
- Les fichiers de positions indiquent « liste du deck inconnue » : ni le conseiller ni moi n'avions les probabilités de pioche.
- Découvertes et mulligans : le fichier ne donne que les cartes proposées et le choix, sans plateau, sans main complète, et souvent sans classe adverse. Ces jugements sont donc moins sûrs que ceux des 20 positions.
- **Confiance** : haute = je jouerais ainsi dans presque tous les cas ; moyenne = je préfère une ligne, mais l'autre se défend ; basse = choix proches ou trop dépendants de l'inconnu.

---

## 1. Les 20 positions

### Tableau

| Position | Tour, adversaire | Conseil (1re action) | Verdict | Ma ligne ou ma réserve | Confiance |
|---|---|---|---|---|---|
| P01b4e5d9 | T9 Libre, Prêtre 28/40 | Frappe → Zéphrys, puis Glissedrake | **bon** | identique ; Dragonnet 3/6 aussi défendable contre un 2e Mot de l'ombre : Ruine | moyenne |
| P1ba3bc76 | T4, Démoniste 30/30 | Informateur 6/7 → Brute 3/5 | **bon** | identique | haute |
| P312a5de4 | T11, Mage 23/30 | Wyrm → Chevaucheuse 4/4 | **bon, suite perfectible** | Voyante (Réincarnation) retire le Bouclier, Garrosh et Profondeurs tuent 2 feux follets | moyenne |
| P35b7c9ae | T5, Chasseur de démons 27/30 | Héritière (7/8 Provocation, Vol de vie) | **bon** | identique, ne pas tuer le Prototype instable | haute |
| P57513a5f | T3, Mage 29/30 | Forgeron (renforce 3 serviteurs en main) | **bon** | identique | haute |
| P6143f321 | T3, Mage 30/30 | Avant-garde | **bon** | Dragonnet 1/4 équivalent contre un Mage | basse (choix proches) |
| P65f491fa | T9, Chasseur 27/30 (Secret) | Grand Dracorex après un échange du Glissedrake | **bon** | identique ; le Champion invaincu remplirait le plateau adverse | moyenne-haute |
| P76f556dd | T7 Libre, Chasseur 30/30 (Secret) | Wyrm visage, Profondeurs posées sans activation | **bon** | tester le Secret avec le Gardien 2/1 plutôt qu'avec l'Avant-garde 4/1 | haute |
| P9f67a3ad | T3, Prêtre 30/30 | Colporteur → Sombreraie, achevée par le Chevaucheur | **bon** | identique, la Voyante est protégée | moyenne-haute |
| Pa26bbab3 | T8, Chasseur 27/30 (Secret) | Dragonnet de bronze (Bouclier divin) → Punisseuse toxique | **bon** | identique, puis Suffusion | haute |
| Paf9a76e3 | T11 Libre, Prêtre 23/40 | Aile de l'effroi + Charge, visage, armure | **bon** | identique | moyenne-haute |
| Pb6503702 | T8 Libre, Paladin 18/40 | Glissedrake → Tirion, puis 18 au visage | **bon, marge non prise** | jouer d'abord le Wyrm de la main au visage : 23 au lieu de 18 exactement | haute |
| Pbb83453d | T4, Paladin 30/30 | Profondeurs → Colporteur réduit au silence | **discutable** | le Colporteur tue Murmie, le Forgeron va au visage : mêmes 3 dégâts, une charge gardée | moyenne |
| Pc0cc9065 | T6 Libre, Chasseur de démons 24/30 | Calamar → Artificier mo'arg | **bon** | identique | moyenne-haute |
| Pcdfaad00 | T7 Libre, Prêtre 44 PV effectifs | Wyrm à 6 au visage, Serpent-nuage au visage | **bon, raisonnable** | Avant-garde + Luisaile + armure aussi défendable contre un contrôle | basse |
| Pe0e079c2 | T10, Chevalier de la mort 24/37 | Sombre chevaucheur d'abord (Découverte) | **bon** | identique : l'information avant les échanges | moyenne-haute |
| Pe281461b | T9 Libre, Prêtre 28/40 | Glissedrake | **bon** | identique | moyenne |
| Pecf9bbff | T3, Démoniste 26/30 | Profondeurs → Gardien, 6 au visage | **discutable (léger)** | Colporteur + 4 au visage en gardant 3 charges ; la ligne du conseil se défend | basse |
| Pf7cf118d | T5, Paladin 27/30 | Forgeron → Voyant 2/3, puis Glissedrake au visage | **discutable (la suite)** | Forgeron → Voyant, puis Glissedrake → Bibliothécaire 4/1, pour protéger Luisaile 7/2 | moyenne |
| Pf8117817 | T7, Mage 15/30 (main vide) | Présage + Profondeurs posées, Glissedrake au visage | **bon (excellent)** | identique : létal redondant au tour suivant | haute |

### Détail par position

**P01b4e5d9, tour 9, Libre, Prêtre.** Le Prêtre vient de jouer Mot de l'ombre : Ruine, qui détruit tous les serviteurs à 5 ATQ ou plus. Il a 8 cartes en main.
- Frappe sur Zéphrys 3/2 retire son seul attaquant et pioche un serviteur (condition remplie). Glissedrake utilise ensuite les 4 cristaux restants.
- Contre un contrôle, se remplacer en retirant une menace est une bonne affaire. La Frappe n'est pas utile pour un létal contre 28 PV.
- La suite « Glissedrake, sauf si la pioche offre mieux » est adaptative, ce qui est bien.
- Nuance : Glissedrake 5/8 est vulnérable à une seconde Ruine. Dragonnet 3/6 ne l'est pas, mais laisserait un cristal inutilisé. Les deux se défendent.

**P1ba3bc76, tour 4, Démoniste.** Deux Brutes 3/5 avec Provocation. Seule l'attaque est légale, il reste 1 mana.
- L'Informateur 6/7 tue une Brute et survit en 6/4. Les deux Sombres chevaucheurs 1/1 mourraient pour 1 dégât chacun : c'est juste de les garder.
- Les deux Provocations ne peuvent pas tomber ce tour : aucun dégât au visage n'est perdu.

**P312a5de4, tour 11, Mage.**
- Plateau adverse : Chevaucheuse 4/4, Gardienne 4/4 avec Provocation et Bouclier divin, 4 feux follets 1/1, soit 12 ATQ. Le pouvoir héroïque Bénédiction du feu follet invoque d'autres feux follets.
- Wyrm sur la Chevaucheuse est juste : 5 d'armure, et une 4/4 retirée.
- La suite prévue est perfectible : Garrosh retire le Bouclier en encaissant 4, puis Glissedrake tue la Gardienne, et les Profondeurs ne servent pas.
- Meilleure suite :
  1. la Voyante 1/1 retire le Bouclier ; elle meurt puis revient grâce à Horreur persistante, « réincarnée avec tous ses PV et enchantements » ;
  2. Glissedrake tue la Gardienne ;
  3. Garrosh (arme 2/1) tue un feu follet en encaissant 1 ;
  4. les Profondeurs, sur leur dernière charge, tuent un autre feu follet ;
  5. on joue le Dragonnet.
- Résultat : il reste 2 feux follets au lieu de 4, et le héros perd 3 PV de moins.
- Garder la dernière charge « pour une éventuelle pioche de Grommash » vaut peu : environ 1 chance sur 14 au prochain tour, s'il est encore dans le deck.

**P35b7c9ae, tour 5, Chasseur de démons.**
- Héritière (Terreur en éveil) : 5/6 Vol de vie, +2/+2 grâce au Dragonnet blessé, donc une 7/8 Provocation Vol de vie pour 5. Elle prépare aussi la Parenté : Wyrm à 6 au tour suivant.
- Les attaques au visage sont justes. Il ne faut pas tuer le Prototype instable : à sa mort, puis à sa réincarnation, il donne deux fois +1/+1 à la main adverse.

**P57513a5f, tour 3, Mage.** Forgeron renforce trois serviteurs en main, dont la Salamandre (Charge, Bouclier divin, Furie des vents), qui passe à 10 dégâts potentiels. Colporteur + Profondeurs n'apporteraient rien : aucun autre Dragon en jeu.

**P6143f321, tour 3, Mage.** Le Colporteur gelé est à 1 PV et mourra probablement à l'Explosion de feu. Avant-garde (pioche, +1/+1 du Colporteur) et Dragonnet du Vol infini (2/5 Toxicité Réincarnation, très résistant aux sorts de Mage) se valent. Le Gardien laisserait 1 mana et le bonus du Colporteur sans cible.

**P65f491fa, tour 9, Chasseur.**
- Plateau adverse : 5 serviteurs, 24 ATQ, dont 3 Provocations, plus un Secret.
- Le plan nettoie tout le plateau :
  1. Glissedrake tue un Ours 5/6 et teste le Secret ;
  2. le Dracorex frappe l'Elekk, puis inflige 5 à tous les autres, ce qui tue la Punisseuse et le Survivant ;
  3. les deux petits Dragonnets achèvent les restes.
- Coût : le Dragonnet de bronze et le Dragonnet 3/1, contre 5 serviteurs adverses. Le Champion invaincu (13/13 Ruée) aurait rempli le plateau adverse de serviteurs à 1 mana : bonne décision de l'écarter.

**P76f556dd, tour 7, Libre, Chasseur.** Nous sommes à 15 PV effectifs, le plateau adverse est vide et un Secret est en jeu.
- Wyrm au visage : 5 dégâts et 5 d'armure. Les Profondeurs sont posées sans activation, et les attaques donnent 19 au visage.
- L'adversaire tombe à 11, à portée de Grommash + Profondeurs (12) au tour suivant. C'est une préparation sur deux tours exemplaire.
- Réserve mineure : tester le Secret avec le Gardien 2/1 plutôt qu'avec l'Avant-garde 4/1. Le Forgeron coûte 5 au lieu de 3, ce qui laisse penser qu'un Piège givrant a déjà été joué.

**P9f67a3ad, tour 3, Prêtre.**
- Le Colporteur 1/4 frappe la Sombreraie 2/2, que le Sombre chevaucheur 1/1 achève. Coût : un 1/1 sans valeur restante et 2 PV du Colporteur.
- La Voyante, qui donne +2 aux sorts, est préservée et va au visage. Sinon, la Sombreraie aurait échangé contre la Voyante 2/1.

**Pa26bbab3, tour 8, Chasseur.** La Punisseuse 8/6 a volé Toxicité et Réincarnation au Dragonnet.
- Le Dragonnet de bronze retire son Bouclier divin sur l'attaque toxique et lui inflige 4. La Suffusion (2) l'achève, avec une Découverte.
- La Punisseuse revient ensuite sans les effets volés. Glissedrake 7/8 ne doit surtout pas la frapper avant. C'est l'usage optimal du Bouclier divin.

**Paf9a76e3, tour 11, Libre, Prêtre.**
- Le plateau adverse est vide après un Cri psychique. Aile de l'effroi avec Charge : 5 au visage, et son Râle d'agonie pioche 2 Dragons, ce qui résiste bien au contrôle. L'armure utilise le reste du mana.
- Animatrice donnerait une arme au Prêtre, et Kronx n'a pas Galakrond à piocher : bonne décision de les écarter.

**Pb6503702, tour 8, Libre, Paladin 18 PV.**
- Le létal existe : Glissedrake tue Tirion 8/8 (Bouclier déjà retiré), puis Wyrm 10 + Forgeron 4 + Forgeron 4 = 18, exactement.
- Il restait 6 mana et un Wyrm en main (5 dégâts). Le jouer d'abord au visage portait le total à 23. Sans Secret visible, le risque était faible, mais la marge ne coûtait rien.
- Le conseiller corrige ce point à la décision suivante : position Pd8d056e7 de la Partie 1.

**Pbb83453d, tour 4, Paladin.**
- Le conseil active les Profondeurs sur le Colporteur réduit au silence (1/4, donc 3/3), puis le Forgeron tue Murmie et le Colporteur frappe le visage pour 3.
- Alternative strictement équivalente en dégâts : le Colporteur 1/4 tue Murmie 1/1, et le Forgeron 3/4 frappe le visage pour 3.
- On garde ainsi une charge sur 2. Grommash est en main, et la charge vaut plus qu'un +2 ATQ permanent sur un 1/4 sans effet.
- Glissedrake est le bon coup de mana. Le Bibliothécaire 4/4 ne peut pas être tué sans perte ce tour : le laisser au Glissedrake du tour suivant est juste.

**Pc0cc9065, tour 6, Libre, Chasseur de démons.** L'Artificier mo'arg double les dégâts des sorts sur tous les serviteurs : c'est la pièce d'un nettoyage de plateau. Le Calamar (3/8 Provocation) le détruit sans échange, et le Colporteur frappe le visage. Garder Gardien et Cogneur pour plus tard est cohérent.

**Pcdfaad00, tour 7, Libre, Prêtre 37 PV + 7 armure.** Le plateau vient d'être nettoyé par une Potion de Kazakus.
- Wyrm à 6 grâce à la Parenté, plus le Serpent-nuage : 10 dégâts et une 6/6.
- Contre un contrôle à 9 cartes, Avant-garde + Luisaile + armure (deux cartes générées, menaces plus petites) se défend autant. Je ne tranche pas.

**Pe0e079c2, tour 10, Chevalier de la mort.**
- Trois Provocations 1/3 bloquent le visage. Grommash est à 14/1 : il meurt sur toute riposte et sera la cible des Sangsues, qui volent des PV à l'adversaire le plus faible.
- Jouer d'abord Sombre chevaucheur, pour voir le Dragon découvert, avant d'engager les échanges est le bon ordre.
- Ensuite, utiliser Grommash sur une Provocation, puisqu'il est condamné, est raisonnable.

**Pe281461b, tour 9, Libre, Prêtre.** Après la Frappe, qui a pioché Grommash, il reste 4 mana : Glissedrake 5/8 Insaisissable est la meilleure présence. Les autres options sont plus faibles : Animatrice arme le Prêtre, Luisaile + armure ou Dragonnet 3/6 apportent moins.

**Pecf9bbff, tour 3, Démoniste, plateau adverse vide.** Profondeurs sur le Gardien : 4/2 permanent, 6 au visage au lieu de 4.
- C'est cohérent avec un plan tempo.
- Mais une charge sur 3 part pour 2 dégâts, et le Gardien tombe à 2 PV. Grommash est dans le deck, et la seconde Profondeurs aussi.
- Je garderais les 3 charges, mais l'écart est faible.

**Pf7cf118d, tour 5, Paladin Murlocs.** Le Forgeron 3/3 tue le Voyant 2/3 et survit : c'est juste.
- Ensuite, le conseil envoie Glissedrake au visage pour 7 et laisse le Bibliothécaire 4/1 et le Végétaileron 1/1.
- Ces deux serviteurs échangeront contre Luisaile 7/2 et le Forgeron 3/1.
- Glissedrake → Bibliothécaire (Glissedrake 7/4) ne laisse qu'un 1/1 à l'adversaire et protège Luisaile, qui frappera pour 7 au tour suivant.
- Quand on mène nettement au plateau, l'échange qui protège un attaquant clé vaut mieux. Le document de stratégie le dit aussi.

**Pf8117817, tour 7, Mage à 15 PV, main vide.** Présage (Bouclier divin, Furie des vents) et Profondeurs posées sans activation, Glissedrake au visage : l'adversaire tombe à 10.
- Au tour suivant, Grommash + Profondeurs (12) + Frappe (3) depuis la main, plus Présage (10) et Glissedrake (5) sur le plateau.
- Le létal est redondant et résiste à un nettoyage. La ligne « Frappe + Profondeurs maintenant » descendait plus bas, mais perdait le combo.

---

## 2. Découvertes à Sombre don et mulligans

Rappel : sans plateau ni main, ces jugements portent sur la valeur générale de la carte avec son don, au tour indiqué.

### 2.1 Découvertes à Sombre don (27 jugées sur environ 60)

| N° | Tour | Choix du conseiller | Verdict | Commentaire |
|---|---|---|---|---|
| 1 | 2 | Cogneur d'œufs + Cauchemar vivant | bon | deux corps au tour 4 ; le Cri de guerre (1 dégât, +4 ATQ) se combine avec Grommash ou la Voyante |
| 4 | 6 | Calamar + Posture de défense | bon | retrait et Provocation 3/8, jouable de suite |
| 5 | 1 | Guerrière du Vol infini + Réveil brutal | **très bon** | ATQ doublée deux fois : 12/5 Ruée pour 5 |
| 7 | 1 | Gorgonzormu + Cauchemar vivant | bon | coup à 3 mana, deux corps et un fromage |
| 8 | 4 | Berserker hacheuse + Serres de harpie | bon | Ruée, Bouclier divin, Furie des vents : deux échanges protégés |
| 12 | 8 | Bagarreur + Terreur en éveil | bon | finisseur avec Charge et Vol de vie, 13/10 avec 10 d'armure |
| 14 | 6 | Serpent-nuage + Repos serein | bon | jouable de suite, copie d'un Dragon de la main |
| 16 | 10 | Huissier + Cauchemar vivant | discutable | à 10 mana, Le saccage-boum 10/9 Vol de vie, qui invoque une copie à Ruée, était jouable de suite ; dépend de l'armure et de la main |
| 17 | 11 | Aile de l'effroi + Somnambule | bon | pression contre un Prêtre au plateau vide ; Mère des couvées 7/3 Vol de vie pour 1 mana net valait aussi |
| 20 | 2 | Wyrm + Posture de défense | bon | les trois propositions sont chères ; le Wyrm a une Parenté réaliste |
| 23 | 7 | Totem de Rochepointe + Repos serein | bon | moteur de pioche et d'armure Insaisissable pour 2 |
| 24 | 1 | Luisaile + Beaux rêves | **très bon** | 7/7 pour 2, pioche garantie au tour 2 |
| 25 | 4 | Chasseur de gloire + Somnambule | discutable | Grommash + Repos serein (6/11 Insaisissable, 14 dégâts avec Profondeurs) est un second finisseur plus fort |
| 26 | 6 | Star du rock + Posture de défense | **discutable, probable sous-optimal** | Calamar + Réveil brutal détruit deux serviteurs à 3 ATQ ou moins pour 4 mana ; seul un plateau adverse sans cible justifierait la Star 1/7 |
| 27 | 3 | Plaiedécaille cobalt + Repos serein | bon | Guerrière du Vol de bronze 8/6 Provocation Vol de vie était aussi bonne |
| 36 | 7 | Glissedrake + Cauchemar vivant | bon | — |
| 44 | 6 | Fracasseur + Repos serein | bon | Murozond + Charge restait une option de létal à 9 mana |
| 47 | 3 | Dragonnet de bronze + Beaux rêves | **très bon** | 8/6 Bouclier divin Vol de vie pour 3 |
| 49 | 4 | Wyrm + Réveil brutal | **très bon** | 10 dégâts et 10 d'armure |
| 51 | 5 | Présage + Serres de harpie | bon | — |
| 59 | 6 | Dragonnet de bronze + Posture de défense | discutable | Wyrm insaisissable + Beaux rêves (9/9 Bouclier divin Ruée Insaisissable pour 6) est bien plus fort, sauf urgence défensive immédiate |
| 60 | 8 | Animatrice de fer + Posture de défense | discutable | choix modeste ; Molosse 8/17 au dessus du deck, ou Champion invaincu 13/13 à Réincarnation complète, malgré son inconvénient |
| 66 | 4 | Lancier écailleux + Repos serein | **très bon** | 8/8 Insaisissable pour 4 |
| 67 | 1 | Verdaile illusoire + Horreur persistante | bon | Râle d'agonie déclenché deux fois : 4 Dragons 4/5 placés dans le deck |
| 79 | 6 | Wyrm + Beaux rêves | discutable | Lancier 8/8 Insaisissable était jouable de suite ; son aura donne Provocation aux serviteurs adverses, ce qui gêne un plan visage. Défendable |
| 88 | 9 | Voyante + Horreur persistante | discutable | Luisaile + Réveil brutal donne 2 légendaires pour 2 mana ; la Voyante se justifie seulement pour des dégâts de sorts |
| 95 | 1 | Dragonnet du Vol infini + Posture de défense | bon | écarte Ysera + Beaux rêves, qui serait une pioche morte pendant 8 tours |

**Bilan**
- 20 bons, dont 5 très bons, et 7 discutables (n° 16, 25, 26, 59, 60, 79, 88). Aucun choix n'est une erreur certaine sans le contexte.
- Tendances justes :
  - Beaux rêves sur une carte bon marché, qui remplace une pioche par un gros corps ;
  - les combos Réveil brutal (Guerrière, Wyrm) ;
  - les dons qui rendent la carte jouable ce tour ;
  - écarter les cartes à 9 ou 10 mana tôt dans la partie.
- Tendance discutable : une préférence pour l'option défensive bon marché (Posture de défense sur un petit corps) face à une option plus puissante d'un ou deux mana de plus. Voir les n° 26, 59 et 60.

### 2.2 Mulligans (43 lus, 30 jugés)

| N° | Main | Conseil | Verdict | Commentaire |
|---|---|---|---|---|
| 1 | Frappe, Profondeurs, Dragonnet | tout rendre | discutable | le Dragonnet est un bon coup à 3 et compte pour les conditions de Dragon |
| 2 | Glissedrake, Grommash, Forgeron, Chevaucheur, Pièce | rendre Grommash | bon | Glissedrake à 4 grâce au Forgeron |
| 3 | Voyante ×2, Forgeron, Animatrice, Pièce | rendre Animatrice et une Voyante | bon | — |
| 4 | Voyante, Frappe, Wyrm, Avant-garde, Pièce | rendre le Wyrm | bon | — |
| 5 | Glissedrake, Forgeron, Animatrice | rendre Animatrice et Glissedrake | **discutable** | Glissedrake coûte 4 avec deux autres Dragons gardés |
| 7 | Frappe ×2, Grommash, Colporteur, Pièce | ne garder que le Colporteur | acceptable | garder une Frappe se défendait |
| 8 | Chronoseigneur, Glissedrake, Frappe, Forgeron, Pièce | ne garder que le Forgeron | **discutable** | Pièce, Forgeron, puis Glissedrake à 4 forment une courbe correcte |
| 9 | Dragonnet, Wyrm, Gardien, Chevaucheur, Pièce | rendre le Wyrm | bon | — |
| 12 | Voyante, Suffusion, Luisaile | rendre la Suffusion | acceptable | Voyante + Suffusion forment un petit combo |
| 13 | Luisaile, Voyante, Chevaucheur, Grommash, Pièce | rendre Grommash | bon | — |
| 16 | Gardien, Forgeron, Animatrice, Colporteur, Pièce | rendre Animatrice | bon | — |
| 17 | Glissedrake, Avant-garde, Animatrice | tout rendre | **sous-optimal** | garder Avant-garde + Glissedrake (3 puis 4), rendre Animatrice |
| 19 | Glissedrake, Avant-garde, Chevaucheur, Wyrm, Pièce | rendre le Wyrm | bon | contredit le n° 17 : le traitement de Glissedrake est incohérent |
| 20 | Gardien, Forgeron, Luisaile | rendre Luisaile | discutable | Luisaile est un coup à 2 et un Dragon qui active le Gardien |
| 21 | Grommash, Dragonnet, Frappe, Dracorex, Pièce | rendre Grommash et Dracorex | bon | — |
| 22 | Grommash, Chevaucheur, Glissedrake (7) | rendre Grommash | **très bon** | le Dragon découvert par le Chevaucheur fera passer Glissedrake à 4 |
| 24 | Frappe, Profondeurs, Animatrice, Gardien, Pièce (contre Mage) | rendre Frappe et Profondeurs | bon | Animatrice active le Gardien |
| 26 | Luisaile, Avant-garde ×2, Chevaucheur, Pièce | rendre un Avant-garde | bon | — |
| 27 | Forgeron, Glissedrake, Avant-garde | ne garder que le Forgeron | **discutable** | même problème avec Glissedrake |
| 28 | Dracorex, Animatrice, Chevaucheur | rendre le Dracorex | bon | — |
| 30 | Wyrm, Dragonnet, Forgeron, Frappe, Pièce | ne garder que le Forgeron | discutable | avec la Pièce, Forgeron puis Dragonnet forment une courbe |
| 31 | Chronoseigneur, Colporteur, Forgeron | rendre le Chronoseigneur | bon | — |
| 32 | Suffusion, Grommash, Gardien, Wyrm, Pièce (contre Paladin) | rendre Grommash et le Wyrm | bon | le Gardien n'a plus de Dragon en main, ce qui est acceptable |
| 33 | Profondeurs, Glissedrake, Avant-garde, Colporteur, Pièce | rendre les Profondeurs | bon | — |
| 34 | Animatrice, Profondeurs, Glissedrake | tout rendre | acceptable | sans coup précoce et en commençant, se défend |
| 35 | Animatrice, Suffusion, Chevaucheur | tout garder | bon | — |
| 37 | Dracorex, Wyrm, Grommash, Voyante, Pièce | rendre les trois cartes à 8 | bon | — |
| 38 | Profondeurs, Colporteur, Wyrm | rendre le Wyrm | bon | — |
| 41 | Luisaile, Gardien, Chevaucheur, Voyante, Pièce | rendre Luisaile | **discutable** | Luisaile est le seul Dragon qui reste après la Voyante ; je garderais les 4 cartes |
| 43 | Avant-garde, Chevaucheur ×2 | tout garder | bon | — |

**Bilan des mulligans**
- **Ce qui marche** : les cartes à 6 et 8 mana sont rendues dans 100 % des cas lus. Le Sombre chevaucheur est gardé quand un Dragon l'accompagne, et le raisonnement sur les synergies est souvent bon (n° 22).
- **Erreur récurrente n° 1, Glissedrake** : rendu dans les mulligans 5, 8, 17, 27 et 34 alors qu'un autre Dragon gardé le mettait à 4. Gardé dans les mulligans 2, 19, 22 et 33. Le document du deck recommande de le garder dans ce cas, et le conseiller n'applique pas cette règle de façon stable.
- **Erreur récurrente n° 2, mulligans trop agressifs** : en commençant, il rend des Dragons à 3 mana (Dragonnet, Avant-garde) et Luisaile pour « chercher du 1-2 » (n° 1, 17, 20, 27, 30, 41). Il affaiblit ainsi les conditions « Dragon en main » du Chevaucheur, du Gardien et de Glissedrake, qui sont le moteur du deck.
- **Profondeurs Sanguines** : rendue presque toujours (n° 1, 14, 15, 24, 33, 34, 39, 42). C'est cohérent avec la source SunZero (mulligan agressif), mais contraire à l'hypothèse du document du deck (« vaut au moins une carte à 2 mana »). C'est à trancher sur les données de mulligan.

---

## 3. Synthèse

### 3.1 Types d'erreurs récurrentes

| Type | Exemples | Gravité |
|---|---|---|
| Charges de Profondeurs dépensées pour peu : 2 dégâts, ou un bonus sur une cible faible ou réduite au silence. Le conseiller suit la recharge, mais pas la valeur d'une charge | Pbb83453d, Pecf9bbff ; inversement, une dernière charge gardée « pour Grommash » alors qu'elle valait un serviteur (P312a5de4) | faible à moyenne |
| Visage plutôt qu'échange quand on mène au plateau, alors qu'un serviteur adverse à 1 PV va échanger contre un attaquant clé | Pf7cf118d | moyenne |
| Suite d'attaques non optimisée : qui retire le Bouclier, qui survit à quel échange, et sacrifice inutile pour « tester le Secret » | P312a5de4 ; Partie 1 : P92e667bd | faible à moyenne |
| Létal exact sans marge alors que des dégâts gratuits restent disponibles | Pb6503702 | faible ici, coûteuse le jour où un effet caché existe |
| Mulligan : règle de Glissedrake appliquée de façon incohérente ; Dragons à 3 mana rendus | 5 cas pour Glissedrake, 6 pour les Dragons | moyenne, car répétée dans chaque partie |
| Découverte : option défensive bon marché préférée à une option plus forte | Découvertes 26, 59, 60, 88 | incertaine faute de contexte |
| Effets qui aident l'adversaire, sous-estimés dans une variante : Nozdormu l'Intemporel donne 10 cristaux à un Prêtre qui a 8 cartes | Partie 1 : P3c57d927, variante X | moyenne |

### 3.2 Forces observées

- **Létal** : trouvé et ordonné correctement dans tous les cas présents (Pb6503702, et dans la Partie 1 P282892f8, P89822427, Pd8d056e7), sans erreur de calcul.
- **Plans sur deux tours** :
  - Profondeurs posées sans activation avant le tour de Grommash (P76f556dd, Pf8117817) ;
  - Parenté préparée (P35b7c9ae, et dans la Partie 1 P7e725c0d, où le surcoût du Seigneur nérub'ar sur le Wyrm est bien vu).
- **Ordre d'information** : il pioche ou découvre avant d'engager le mana et les échanges (P01b4e5d9, Pe0e079c2, et dans la Partie 1 P79561396).
- **Lecture des mots-clés** :
  - Bouclier divin contre Toxicité (Pa26bbab3) ;
  - Râle d'agonie qui aide l'adversaire (Prototype instable, P35b7c9ae) ;
  - Champion invaincu écarté (P65f491fa) ;
  - attaque avec le Canon à eau avant que l'arme soit remplacée (Partie 1, P3c57d927, variante Y).
- **Danger du tour adverse** : à bas PV, il fait passer la survie d'abord (Partie 1 : Pc6600069, P92e667bd, P3f1ccb2b), avec un décompte correct des dégâts adverses visibles.
- **Sombres dons** : il évalue bien la carte avec son don (Réveil brutal, Beaux rêves, Charge).

### 3.3 Ce qu'il faudrait lui donner

1. **Information**
   - Pour les Découvertes et les mulligans, enregistrer et envoyer le contexte : plateau, main, mana restant après la Découverte, classe adverse, rang dans l'ordre de jeu.
   - Pour chaque proposition, un indicateur « jouable ce tour ».
   - Pour les mulligans, le nombre de Dragons qui resteraient en main après le coup prévu au tour 1 ou 2.
2. **Calculs locaux**
   - **Évaluateur d'échanges** : pour chaque attaquant et chaque cible, indiquer qui meurt et qui survit, l'ATQ adverse retirée, et la liste « serviteur adverse qui peut tuer ton serviteur clé au prochain tour ». Il couvrirait Pf7cf118d et P312a5de4.
   - **Marge de létal** : quand un létal est trouvé, ajouter d'abord les dégâts gratuits disponibles (Wyrm, Frappe) et annoncer la marge.
   - **Valeur d'une charge de lieu** : charges restantes, tours avant Grommash (en main ou dans le deck), et usages restants prévus.
   - **Dégâts maximaux adverses au prochain tour** : plateau, arme, pouvoir héroïque, et effets connus de la classe comme Rugissement sauvage. C'est déjà partiellement présent, et à rendre systématique.
3. **Consignes**
   - Glissedrake : le garder si un autre Dragon gardé le met à 4.
   - Ne pas rendre un Dragon à 3 mana en commençant si la main a moins de deux Dragons.
   - Retirer un Bouclier divin avec le serviteur le moins coûteux ou à Réincarnation.
   - Tester un Secret avec un attaquant qui survit à l'échange, sauf si ce test est gratuit.
   - Liste d'« effets qui aident l'adversaire » à pondérer : Nozdormu l'Intemporel, Animatrice (arme adverse), Champion invaincu, Lancier écailleux (Provocation adverse), Râles d'agonie qui renforcent la main adverse.

### 3.4 Limites de mon jugement

- Pas de simulateur : les lignes alternatives sont vérifiées à la main, et une erreur d'arithmétique reste possible. J'ai recompté chaque échange cité.
- Tirages et mains adverses inconnus. Mes « discutables » tiennent souvent à 1 ou 2 points de valeur, et l'écart de taux de victoire n'est pas mesurable sur un échantillon de cette taille.
- Mécaniques supposées sans vérification dans les logs :
  - un serviteur réincarné compte comme blessé ;
  - le Dracorex inflige des dégâts égaux à son ATQ ;
  - un lieu est activable le tour où il est posé.
- Découvertes et mulligans jugés sans plateau ni classe adverse dans la plupart des cas. Leurs verdicts sont des tendances, pas des fautes établies.
- Les 20 positions ne sont pas un échantillon aléatoire de toutes les décisions : je ne peux pas extrapoler un taux d'erreur global.
