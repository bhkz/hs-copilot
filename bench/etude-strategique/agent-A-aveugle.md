# Agent A : comparaisons à l'aveugle de deux conseils (Partie 1)

Rédigé le 26/10/2026 par l'agent A, expert en stratégie Hearthstone. Les 11 positions viennent de `bench/etude-strategique/paires-aveugles/`. Je ne connais pas l'identité des variantes X et Y, et je n'ai ouvert aucun fichier de clé, de résultat ou de bilan. Les textes des cartes viennent de la base `cards.255130.frFR.json`, en lecture seule.

**Méthode**
- Pour 8 positions, j'ai écrit ma ligne en ne lisant que l'état, avant les conseils.
- Pour P2529cd5b, P282892f8 et P3c57d927, mon premier affichage a montré les conseils en même temps que l'état, par erreur de découpage. J'ai construit ma ligne sur l'état seul, mais je ne peux pas garantir qu'elle soit restée indépendante. Ces trois positions sont marquées « vue » dans le tableau final.

## Résumé

- **Première action** : dans 10 positions sur 11, X et Y donnent la même, et elle est bonne ou acceptable partout. Les suites diffèrent peu : surtout la précision et quelques détails.
- **La seule vraie divergence, P3c57d927** :
  - X joue Nozdormu l'Intemporel, qui donne 10 cristaux de mana à un Prêtre qui a 8 cartes en main. C'est une recommandation légale mais risquée, que je juge discutable.
  - Y attaque d'abord avec le Canon à eau, avant qu'Animatrice ne remplace l'arme, puis développe.
  - **Y est meilleur.**
- **Défaut commun aux deux variantes** (P92e667bd) : elles sacrifient le Gardien 2/1 pour « tester le Secret » sur un 1/1, alors que l'Avant-garde 4/2 tuait le Guide en survivant.
- **Défaut commun, P3f1ccb2b** : aucune des deux ne mentionne que l'adversaire n'a plus qu'une carte dans son deck. La survie y vaut victoire par épuisement.
- **Bilan des préférences** :
  - 8 positions équivalentes ;
  - 1 position où Y est meilleur, nettement (P3c57d927) ;
  - 1 position où X est légèrement meilleur (P2529cd5b) ;
  - 1 position où Y est légèrement meilleur (P79561396).

  Les deux variantes se valent presque. L'écart vient d'une seule décision, et c'est X qui y prend le risque injustifié.

---

## Positions

### P2529cd5b : tour 6, Libre, contre Prêtre (Tyrande 17/30, 9 cartes en main)

**Ma ligne**
1. Animatrice de stade : une 5/3 et une arme pour chaque joueur. On compare les deux armes avant de valider ou d'utiliser le Rembobinage.
2. On pose les Profondeurs sans les activer.
3. Selon l'arme obtenue, Garrosh frappe le visage, ce qui rapproche le Prêtre de la zone Grommash + Profondeurs (6 + 6 + 2 = 14). Il peut aussi frapper l'Auxiliaire 2/3 pour protéger l'Animatrice.
4. On écarte Nozdormu l'Intemporel : il donnerait 10 cristaux à un Prêtre qui a 9 cartes.

Confiance : moyenne-haute.

**Verdicts**
- **X : bon.** Même ligne. La suite prévoit l'attaque du héros avec l'arme si elle n'a pas d'inconvénient. Il signale bien que l'arme adverse peut servir à tuer notre serviteur.
- **Y : bon.** Même ligne, et le calcul « Grommash à 14 » est juste. Il ne prévoit pas d'utiliser l'arme obtenue.

**Préférence** : équivalents, léger avantage à X, qui exploite l'arme reçue. Aucune des deux n'évoque l'option de tuer l'Auxiliaire avec l'arme.

### P282892f8 : tour 8, Libre, contre Chasseur (Rexxar 17/30)

**Ma ligne** : il y a létal. Profondeurs sur Grommash (12, puis 14), puis Grommash et le Wyrm 8/4 au visage : 22 contre 17. Sans le lieu, on arrivait déjà à 20. Aucune Provocation ni aucun Secret n'est visible.

Confiance : haute.

**Verdicts** : X et Y sont **bons** et identiques.

**Préférence** : équivalents.

### P3c57d927 : tour 7, Libre, contre Prêtre (Tyrande 12/30, Devin 6/6 Provocation, 8 cartes en main) — premières actions différentes

**Ma ligne**
1. Garrosh, avec le Canon à eau 4/3, frappe le Devin. C'est la seule cible possible à cause de la Provocation.
   - Le Devin tombe à 6/2, et le Pirate 1/1 invoqué attaque une cible au hasard.
   - Garrosh perd 6 PV.
2. On joue Animatrice : son arme remplace le Canon, qui a donc servi avant d'être perdu.
3. Gain d'armure.
4. Les Profondeurs restent intactes pour Grommash au tour suivant.
   - Pour aller au visage, il faudra d'abord tuer le Devin, ce qui rend 6 PV au Prêtre et invoque un serviteur à 6 mana.
   - Le létal reste donc conditionnel.

Ne pas attaquer pour économiser 6 PV face à un Prêtre qui joue Attaque mentale (5 dégâts) se défendait aussi.

Confiance : moyenne.

**Verdicts**
- **X : discutable** (Nozdormu, puis Garrosh frappe le Devin, puis armure).
  - Nozdormu donne 10 cristaux aux deux joueurs.
  - Le Prêtre a 8 cartes en main, et nous n'en aurons que 3. L'accélération profite surtout à celui qui a le plus de cartes.
  - Le gain réel, une 10/8 et 10 mana pour Grommash + Gardien au tour suivant, ne compense pas ce risque.
  - X le signale, mais le prend quand même. Il garde en revanche le Canon à eau, ce qui est un vrai avantage de sa ligne.
- **Y : bon.**
  - L'ordre est juste : attaquer avec le Canon à eau avant de remplacer l'arme. Il annonce Garrosh à 15 PV après l'attaque.
  - Il ne promet pas de létal : le Devin soigne et invoque un serviteur.

**Préférence** : **Y meilleur.**

**Recommandation légale mais mauvaise** : X (Nozdormu l'Intemporel dans cette situation).

### P3f1ccb2b : tour 16, Libre, contre Chasseur (Rexxar 17/30, 1 carte dans son deck, Secret)

**Danger visible** : Prédateur 4 + Parle-esprit 2 + Serpent 7 + Ursol 9 + arme 3 + pouvoir 2 = 27 dégâts, contre nos 25 PV effectifs. Il faut réduire ce total.

**Ma ligne**
1. Le Gardien frappe Ursol, qui tombe à 9/5. C'est aussi le test du Secret avec le moindre serviteur.
2. Glissedrake tue Ursol.
3. Garrosh, avec son arme à 3, tue le Serpent 7/3 en encaissant 7.
4. On joue le Forgeron et on prend l'armure.

Bilan : il nous reste environ 20 PV effectifs face à 11 dégâts visibles.

Points d'attention :
- Ne pas toucher le Prédateur en chasse : quand il subit des dégâts, il détruit un de nos serviteurs au hasard.
- Variante de même marge : Garrosh tue le Parle-esprit au lieu du Serpent.
- Point oublié par les deux variantes : l'adversaire n'a plus qu'une carte dans son deck, nous en avons 10. Chaque tour survécu nous rapproche d'une victoire par épuisement.

Confiance : moyenne-haute.

**Verdicts**
- **X : bon.** Même ligne, décompte juste (20 contre 11). Il pense à éviter l'effet du Prédateur.
- **Y : bon.** Même ligne. Il dit explicitement que le Gardien teste le Secret.

**Préférence** : équivalents. Ni l'un ni l'autre n'exploite l'épuisement adverse dans son plan.

### P79561396 : tour 7, Libre, contre Paladin (30/30, Recrue 5/4 avec Héritage de Régis ; nous à 15 PV)

**Ma ligne**
1. Avant-garde du portail d'abord : on voit la pioche avant de dépenser le reste du mana.
2. Le Dragonnet toxique tue la Recrue 5/4.
3. Avec les 4 cristaux restants :
   - selon la pioche ;
   - sinon Luisaile + Gain d'armure : à 15 PV, contre un Paladin qui vient de jouer Consécration, l'armure compte ;
   - Suffusion (2 dégâts et une Découverte) est une alternative à Luisaile.

Confiance : moyenne.

**Verdicts**
- **X : bon.** Même ordre. Il note que l'Héritage passera à la prochaine recrue.
- **Y : bon.** Même ordre, et sa suite est explicite : échange, puis Luisaile, puis armure.

**Préférence** : équivalents, léger avantage à Y, plus précis.

### P7e725c0d : tour 5, contre Druide (30/30 ; nous à 9 PV)

Le Seigneur de la toile nérub'ar fait payer 2 de plus les serviteurs à Cri de guerre : il taxe notre Wyrm.

**Ma ligne**
1. Le Forgeron 4/5 tue le Seigneur 1/4 et survit en 4/4.
2. Le Forgeron 3/4 tue le Bébé marsuul 2/2 et survit en 3/2.
3. On joue Glissedrake, un Dragon, pour la Parenté : le Wyrm coûtera 6 au tour suivant, une fois la taxe levée.

Il ne reste qu'un 1/1 adverse. Même avec un Rugissement sauvage, cela fait au plus 6 dégâts contre nos 9 PV.

Confiance : haute.

**Verdicts**
- **X : bon.** Il explique pourquoi viser le Seigneur : sans sa taxe, le Wyrm coûtera 6.
- **Y : bon.** Mêmes échanges, et l'état des survivants est exact.

**Préférence** : équivalents, très léger avantage à X, dont le raisonnement est plus complet.

### P89822427 : tour 7, contre Guerrier pirate (8 PV, sans Provocation ni Secret)

**Ma ligne** : létal. Wyrm au visage (5, l'adversaire tombe à 3), puis le Drake terrestre 6 avec Charge. Il reste encore 10 ATQ en réserve.

Confiance : haute.

**Verdicts** : X et Y sont **bons** et identiques.

**Préférence** : équivalents.

### P92e667bd : tour 6, Libre, contre Chasseur (30/30, Secret ; nous à 10 PV)

**Ma ligne**
1. Wyrm 8/10 avec Provocation (Posture de défense) sur le Traqueur phasique.
   - Le Wyrm tue le Traqueur, qui génère des Secrets.
   - Il donne 5 d'armure, soit 15 PV effectifs, et prépare la Parenté.
2. L'Avant-garde 4/2 tue le Guide 1/1 et survit en 4/1.
3. Le Gardien 2/1 va au visage, ou ne bouge pas.

Si le Secret est un Piège givrant, l'Avant-garde revient en main et son Cri de guerre pioche de nouveau : le coût est faible.

Confiance : moyenne.

**Verdicts**
- **X : bon, suite perfectible.** Wyrm juste. Mais le Gardien 2/1 frappe le Guide pour « tester le Secret » et meurt à coup sûr, puisque le Guide a 1 ATQ.
- **Y : bon, suite perfectible.** Même choix que X.

**Préférence** : équivalents.

**Recommandation légale mais sous-optimale** : le sacrifice du Gardien, chez X comme chez Y.

### Pc6600069 : tour 6, Libre, contre Druide (31 PV effectifs, main vide ; nous à 3 PV)

**Ma ligne**
1. Wyrm au visage, ce qui nous fait 8 PV effectifs.
2. Profondeurs sur le Matelot 2/1, qui meurt.
3. Le Forgeron 3/2 tue le Bébé 1/1 et survit en 3/1.
4. Le Forgeron 4/4 et Glissedrake vont au visage : 16 dégâts au total.

Le plateau adverse est vide, et activer le lieu ne coûte rien puisque Grommash ne sera pas jouable à 7 mana au tour suivant.

Confiance : haute.

**Verdicts** : X et Y sont **bons**. Même ligne exactement, et tous deux ont le bon raisonnement : survie d'abord, lieu sans coût d'opportunité.

**Préférence** : équivalents.

### Pc6bba113 : tour 3, Libre, contre Chaman Murlocs (30/30)

**Ma ligne**
1. Gorgonzormu tue le Totem 0/2 avec Provocation.
2. Suffusion sur l'Oracle sinistrécaille 1/1, qui donne +1 ATQ aux autres Murlocs ; elle s'accompagne d'une Découverte.
3. On joue la Voyante.

Le plateau adverse est vide et on gagne une carte. Je préfère garder Frappe (3 dégâts) pour une plus grosse cible.

Confiance : basse : la ligne Forgeron se défend autant.

**Verdicts**
- **X : acceptable** (Forgeron, puis Gorgonzormu sur le Totem). Le Forgeron donne +2 ATQ permanent à trois cartes en main et pose une 2/5. Il laisse en vie l'Oracle, qui renforce chaque Murloc, et il le signale.
- **Y : acceptable.** Même ligne, même réserve signalée.

**Préférence** : équivalents. Mon écart avec eux tient à un style de jeu, pas à une erreur.

### Pd8d056e7 : tour 8, Libre, contre Paladin (18/40, plateau vide)

**Ma ligne** : létal avec marge. Wyrm de la main au visage (18, puis 13), puis Wyrm 10 + Forgeron 4 = 14. Le second Forgeron 4 donne une marge de 5.

Confiance : haute.

**Verdicts** : X et Y sont **bons** et identiques. Ils prennent ici la marge du Wyrm de la main, que le conseil de la décision précédente, dans la même partie, ne prévoyait pas.

**Préférence** : équivalents.

---

## Tableau final

| Position | Ma ligne | Verdict X | Verdict Y | Préférence | Confiance |
|---|---|---|---|---|---|
| P2529cd5b (vue) | Animatrice, Profondeurs posées sans activation, arme selon le résultat ; pas de Nozdormu | bon | bon | équivalents (léger X) | moyenne |
| P282892f8 (vue) | Profondeurs → Grommash 14, puis Wyrm 8 : létal 22/17 | bon | bon | équivalents | haute |
| P3c57d927 (vue) | Garrosh (Canon à eau) → Devin, puis Animatrice, armure ; Profondeurs gardées | **discutable** (Nozdormu accélère le Prêtre) | bon | **Y meilleur** | moyenne |
| P3f1ccb2b | Gardien puis Glissedrake → Ursol, Garrosh → Serpent, Forgeron, armure ; ne pas toucher le Prédateur | bon | bon | équivalents | moyenne-haute |
| P79561396 | Avant-garde, Dragonnet toxique → Recrue, puis Luisaile + armure selon la pioche | bon | bon | équivalents (léger Y) | moyenne |
| P7e725c0d | Forgeron 4/5 → Seigneur, Forgeron 3/4 → Bébé 2/2, Glissedrake | bon | bon | équivalents (très léger X) | haute |
| P89822427 | Wyrm au visage, puis Drake terrestre : létal | bon | bon | équivalents | haute |
| P92e667bd | Wyrm Provocation → Traqueur, Avant-garde → Guide (survit), Gardien au visage | bon, suite perfectible (Gardien sacrifié) | bon, suite perfectible (idem) | équivalents | moyenne |
| Pc6600069 | Wyrm au visage, Profondeurs → Matelot, Forgeron 3/2 → Bébé, le reste au visage | bon | bon | équivalents | haute |
| Pc6bba113 | Gorgonzormu → Totem, Suffusion → Oracle, Voyante | acceptable | acceptable | équivalents | basse |
| Pd8d056e7 | Wyrm au visage, puis Wyrm 10 + Forgeron 4 (+4 de marge) : létal | bon | bon | équivalents | haute |

## Limites

- Pas de simulateur : les échanges sont recomptés à la main, et les Secrets comme les mains adverses restent inconnus.
- Pour 3 positions, ma ligne n'est pas certifiée indépendante (voir Méthode). Pour P282892f8, le létal est indiscutable. Pour P2529cd5b, ma ligne rejoint les deux variantes, donc l'influence est sans effet sur la préférence. Pour P3c57d927, ma préférence pour Y repose sur l'analyse de Nozdormu, qui ne dépend pas de l'ordre de lecture.
- Avec 11 positions, dont 10 à première action identique, on ne peut pas départager statistiquement les deux variantes. La seule différence nette (P3c57d927) porte sur une seule décision.
