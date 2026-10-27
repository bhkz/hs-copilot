# Diagnostic des défaites du 25-26/10/2026 et du danger adverse

Sources : journaux des parties, traces du conseiller, états reconstruits (`hscopilot state`), récits tour par tour tirés des lignes GameState. Aucun appel payant.

## En bref

| Défaite | Ce qui a tué | Visible à la fin de ton tour | Erreur identifiable |
|---|---|---|---|
| 22 h 47, Tyrande (Prêtre) | combo de 15 dégâts depuis la main, à 10 cristaux | 12 dégâts contre 23 PV | **oui, du conseiller** : il a conseillé Nozdormu l'Intemporel, qui a donné 10 cristaux à l'adversaire et rendu le combo possible |
| 0 h 21, Rexxar (Chasseur) | uniquement le plateau, l'arme et le pouvoir | **25 dégâts contre 25 PV** | **oui, du système** : aucun conseil n'est arrivé ce tour-là ; l'alerte locale « Menace » existante était incomplète et a disparu en cours de tour (correction du 26/10, voir le verdict) ; une ligne défensive existait |
| 23 h 07, Uther (abandon) | — | 21 contre 17 | position très probablement perdue ; une défense gardait 2 PV de marge |
| 0 h 09, le roi-liche (abandon) | — | 20 contre 3 | perdue |
| 2 h 12, Jaina (abandon) | — | 10 contre 9, pendant le tour adverse | perdue ; la chute précédente venait d'Aessina, carte cachée |

## 1. Tyrande, 22 h 47 : mort en un tour avec 21 PV + 2 armure

**Ton dernier tour (tour 7, TURN=13).**
- **Toi** : 21 PV, arme Canon à eau 4/3, Profondeurs Sanguines en jeu, 7 cristaux. En main : Nozdormu l'Intemporel 10/8 (4, carte créée), Animatrice de stade 5/3 (4), Grommash Hurlenfer 6/11 (8) et Gardien des couvées (2).
- **L'adversaire** : 12 PV, arme Puissance de Menethil 4/1, Auxiliaire de l'ombre 2/2 et Devin 6/6 avec Provocation et Râle d'agonie. Il a **8 cartes en main** et en est à 6 cristaux, donc 7 au tour suivant.
- **Actions légales** : jouer Nozdormu, Animatrice ou Gardien des couvées ; attaquer Devin avec ton héros ; Gain d'armure ; Profondeurs Sanguines sur l'Auxiliaire ou sur Devin.
- **Conseil du modèle** : « Jouer Nozdormu l'Intemporel », par un début de réponse, puis « Gain d'armure ! » comme suite du plan. Sa raison : « La 10/8 conteste Devin tout en préparant Grommash ». **Le texte de la carte n'est jamais pris en compte : « fait passer les cristaux de mana de chaque joueur à 10 ».**
- **Ce que tu as joué** : Nozdormu, puis Gain d'armure, et fin du tour. Tu étais alors à 21 PV + 2 armure.

**Le tour fatal (TURN=14).**

| Heure | Action adverse | Tes PV |
|---|---|---|
| 22:53:59 | Devin t'attaque | 23 → 17 |
| 22:54:00 | Auxiliaire de l'ombre t'attaque | 17 → 15 |
| 22:54:02 | Sol'etos, le cycle de renaissance (5 cristaux) : 8/8 Provocation ; invoque une copie de lui-même ; Râle d'agonie, 5 dégâts à un adversaire aléatoire | 15 |
| 22:54:07 | « Engluez-les ! » (4 cristaux) : détruit chaque serviteur ; les deux Sol'etos meurent | 15 → 10 → 5 |
| 22:54:17 | Sentence de non-mort (1 cristal) : rejoue un Râle d'agonie, donc encore 5 dégâts | 5 → 0 |

**Dégâts visibles à la fin de ton tour** : 12 (Devin 6, Auxiliaire 2, arme 4), contre 23 PV. **Dégâts venus de cartes inconnues** : 15, par un combo de trois cartes **qui coûtait 10 cristaux**.

**La ligne alternative.**
- Sans Nozdormu, l'adversaire n'avait que 7 cristaux : le combo complet (5 + 4 + 1) était impossible.
- Ses meilleures lignes à 7 cristaux donnent au plus environ 17 dégâts : son plateau plus un Râle d'agonie de 5. C'est moins que tes 23 PV.
- Ta survie était donc très probable, sans être certaine, car on ne connaît pas le reste de sa main.

**Ce qui était anticipable.** Le combo lui-même ne l'était pas : trois cartes cachées. Mais la condition qui le rendait possible était **publique et certaine** : Nozdormu donne 3 cristaux de plus à un adversaire qui a 8 cartes en main.

**Ce que la défense aurait coûté.**
- Tu perdais la 10/8, et 2 cristaux de plus pour toi au tour suivant : 10 au lieu de 8.
- Grommash, qui coûte 8, restait jouable au tour suivant sans Nozdormu.
- Le gain offensif de Nozdormu était donc faible, son risque élevé.

**Verdict : erreur stratégique identifiable du conseiller.** Il a ignoré un effet public défavorable à toi. Rien ne prouve que la partie était gagnée sans cette erreur, mais c'est elle qui a rendu le létal adverse possible.

## 2. Rexxar, 0 h 21 : mort en un tour avec 23 PV + 2 armure

**Ton dernier tour (tour 16, TURN=31).**
- **Toi** : 23 PV + 2 armure, arme Lame runique horrible 3/3, 7 cristaux, main vide. En jeu : Forgeron dimensionnel 2/5, Gardien des couvées 2/3 et Glissedrake visionnaire 6/5.
- **L'adversaire** : 17 PV, arme Canon à main 3/1, **un Secret inconnu**, 3 cartes en main et **1 seule carte dans son deck**. En jeu : Prédateur en chasse 4/7, Serpent éternel 7/3 et Ursol 9/7.
- **Actions légales** : attaquer avec ton héros, le Glissedrake ou le Gardien, sur le héros ou sur un serviteur ; Gain d'armure.
- **Conseil** : aucun. Cinq analyses ont été lancées, mais chacune a été dépassée par ton coup suivant avant le premier début de réponse : Forgeron à 13 s, puis une action toutes les 4 à 11 s. Aux tours 14 et 15, le budget de session avait déjà bloqué le modèle.
- **Ce que tu as joué** : Forgeron ; Gardien sur Parle-esprit ; **Glissedrake sur Rexxar**, ce qui déclenche le Secret, Piège explosif, qui inflige 2 dégâts à tous tes personnages ; ton héros sur Rexxar, qui tombe de 17 à 8 ; Gain d'armure. Tu finis à 23 PV + 2 armure.

**Le tour fatal (TURN=32).** Ton héros encaisse 3 de son héros, 9 d'Ursol, 7 du Serpent éternel, 4 du Prédateur en chasse et 2 du pouvoir héroïque : 25.

**Dégâts visibles à la fin de ton tour : 25** (4 + 7 + 9, plus 3 d'arme et 2 de pouvoir héroïque), **contre exactement 25 PV**. Les cartes inconnues n'ont rien apporté : seul le Secret, qui comptait parmi les inconnues de ton tour, t'a coûté 2 PV.

**La ligne défensive.**
- Le Glissedrake attaque le Serpent éternel 7/3 au lieu de Rexxar : il le tue, puis meurt.
- Ton héros n'attaque pas le héros adverse, ce qui ne déclenche pas le Secret.
- Avec Gain d'armure, tu finis à 23 PV + 4 armure, soit 27, contre 18 dégâts visibles : **une marge de 9**.
- L'adversaire devait trouver 9 dégâts de plus dans ses 3 cartes.
- Avec 1 carte dans son deck, il approchait de la fatigue, et toi tu en avais 10.

**Ce qui était anticipable : tout.** Le létal adverse était entièrement sur le plateau, et attaquer le héros d'un Chasseur qui a un Secret est un risque connu.

**Ce que la défense aurait coûté.** Rexxar serait resté à 14 PV au lieu de 8, et tu perdais le Glissedrake. Tu renonçais donc à de la pression, mais tu ne pouvais pas tuer Rexxar avant son tour, et la fatigue jouait pour toi.

**Verdict : erreur identifiable, mais du système plutôt que du modèle.** Le modèle n'a pas répondu à temps. La ligne défensive était nettement meilleure sur la seule information publique.

**Correction du 26/10 (étude stratégique).** Une alerte locale existait bien. Au début du tour, l'analyse rapide affichait « Menace : 25 dégâts sur le plateau adverse contre tes 25 PV ». Mais elle :
- ignorait le pouvoir héroïque ;
- ne s'affichait que si la menace égalait ou dépassait tes PV, et a donc disparu après la mort du Parle-esprit (23 contre 25), alors que le danger restait entier ;
- ne proposait aucune ligne défensive ;
- n'était transmise ni au modèle ni au validateur.

J'avais écrit à tort que rien ne l'avait signalé.

## 3. Les trois abandons

| Partie | Moment | Situation | Restait-il une chance ? |
|---|---|---|---|
| 23 h 07, Uther | ton tour, 4 cristaux restants | toi 15 + 2 ; lui 18 PV, 5 serviteurs (7, 4, 4, 2, 2 ATQ) et une arme 2/2, soit **21 visibles**, plus 3 cartes en main | Une défense existait. Profondeurs sur l'Avant-garde (4/1), qui tue une Recrue 4/3 ; ton héros tue l'autre ; Gain d'armure. Il reste 13 dégâts visibles contre 15 PV. Une marge de 2 face à 3 cartes de Paladin qui renforce ses Recrues, sans pression de ta part : **très probablement perdue, pas mathématiquement** |
| 0 h 09, le roi-liche | ton tour, 4 cristaux | toi 3 PV ; lui 20 dégâts visibles dont un Dévoreur fétide 11/8 Insaisissable ; 15 PV derrière trois Provocations | Gain d'armure ne te mène qu'à 5 PV, et aucun de tes serviteurs ne peut retirer assez d'attaque ce tour-ci : **perdue** |
| 2 h 12, Jaina | **pendant le tour adverse** | à la fin de ton tour : toi 7 + 2 ; lui Aessina 6/8 et quatre Feux follets, soit 10 dégâts visibles contre 9 PV | **perdue** |

**Pour Jaina, la vraie bascule était au tour adverse précédent.**
- Les Feux follets ont vidé ton plateau.
- Aessina (« si 20 serviteurs alliés sont morts pendant cette partie, inflige 20 dégâts répartis ») et le pouvoir héroïque t'ont fait passer de 25 + 6 à 7.
- La carte était cachée. Seul le nombre de serviteurs adverses morts était public, ce qui ne suffit pas à prévoir sans connaître le deck.
- **Pas d'erreur identifiable.**

## 4. Proposition : un petit module local « danger au prochain tour »

**Mesure hors ligne, sur les 35 parties.** J'ai calculé à la fin de chacun de tes tours, 291 au total, les dégâts adverses visibles comparés à tes PV + armure, avec tes Provocations :

| Marge | Fins de tour | Suivies d'une défaite immédiate |
|---|---|---|
| ≤ 0 : létal adverse visible | 4 | 4 (Rexxar, Uther, le roi-liche, Jaina) |
| 1 à 5 | 1 | 0 |
| 6 à 10 | 4 | 0 |
| plus de 10 | 282 | 1 (Tyrande : combo caché) ; les trois coupures de la partie de 0 h 21 sont mises à part, ce ne sont pas des défaites |

**Aucune fausse alarme** dans les parties gagnées. Seul le cas Rexxar était encore jouable au moment de l'alarme ; les trois autres étaient déjà perdus.

**Ce que le module calculerait**, seulement avec l'information publique, en local et en quelques millisecondes :
1. **Dégâts visibles au prochain tour adverse** :
   - serviteurs pouvant attaquer, en excluant les Gelés, les endormis et ceux qui ne peuvent pas attaquer, et en doublant pour Furie des vents ;
   - arme ;
   - pouvoir héroïque infligeant des dégâts ;
   - effets de fin et de début de tour lisibles.
2. **Ta marge** : PV + armure + Provocations, moins ces dégâts, sur l'état actuel et sur l'état après la séquence prévue.
3. **Une ligne défensive minimale**, quand la marge est ≤ 0 ou faible.
   - Parmi les attaques légales, celles qui retirent le plus de dégâts visibles, avec leur coût offensif. Exemple : « Glissedrake → Serpent éternel : −7, Rexxar reste à 14 ».
   - Sans tuer les serviteurs qui punissent les dégâts subis, comme le Prédateur en chasse.
4. **Les cadeaux à l'adversaire** : les cartes de ta main dont le texte profite aussi à l'adversaire (« chaque joueur », « les deux adversaires »). Le module chiffre l'effet quand c'est possible. Exemple : « Nozdormu : l'adversaire passera de 7 à 10 cristaux, avec 8 cartes en main ». Animatrice de stade lui donne aussi une arme.
5. **Les inconnues, sans les chiffrer** : nombre de cartes en main adverses, ses cristaux au prochain tour, ses Secrets inconnus.

**Intégration** :
- un fait local affiché tout de suite, avant toute réponse du modèle : « Danger : 25 dégâts visibles contre 25 PV » ;
- un champ dans l'analyse locale envoyée au modèle ;
- une mise en garde du validateur quand le conseil va au visage alors qu'un létal adverse est visible et qu'une défense existe.

Je n'ajouterais pas de règle au prompt avant d'avoir mesuré l'effet. Ce n'est pas un simulateur : pas de pioche, pas de main adverse, pas d'effets aléatoires.

**Coût** : environ 200 lignes et des tests sur les états enregistrés de Rexxar et de Tyrande, sans appel payant.

## 5. Comptabilité des coûts : réel, estimé, budget

| Notion | Ce que c'est | Où on la voit |
|---|---|---|
| **Coût réel** | Somme des coûts calculés à partir de l'usage renvoyé par OpenAI pour chaque appel **terminé** ; en moyenne 0,0168 $ cette nuit (8 429 jetons envoyés, 481 reçus) | rapport de partie : « coût API (USD) » |
| **Coût estimé des annulations** | 0,015 $ par appel **annulé après envoi** (9 000 jetons d'entrée, aucun en sortie) ; OpenAI ne renvoie pas d'usage pour un flux interrompu ; un appel annulé pendant l'attente avant envoi ne compte rien | rapport : « coût estimé des appels annulés (USD) » |
| **Compteurs « partie » et « session »** | **Réel + estimé, additionnés** | l'overlay (« partie 0,25 $ ») et la fenêtre (« session : … $ ») les affichent **sans distinguer** |
| **Budgets** | Bloquent un appel si compteur + coût réservé d'un appel complet dépasse le plafond. La réserve vaut environ 0,030 $ : 9 000 jetons d'entrée et 1 500 de sortie, au tarif de 2 $, 0,10 $ en cache et 10 $ le million | messages « budget de la partie / de la session atteint » |

**Cette nuit** : 541 appels envoyés, 259 terminés pour 4,35 $ réels, 276 annulés estimés à 4,13 $, et 6 dont l'issue est inconnue (en cours en fin de partie, ou en erreur). À 0 h 41, le plafond de session de 5 $ était fait **d'environ moitié d'estimation**.

**L'estimation est plausible, sans être vérifiée.**
- Les appels terminés de la nuit avaient en moyenne environ 2 560 jetons servis depuis le cache : leur entrée coûtait environ 0,012 $.
- Un appel annulé paie cette entrée, plus la sortie déjà produite, raisonnement compris : entre 0 et environ 480 jetons, soit jusqu'à 0,005 $.
- Son vrai coût se situe donc entre environ 0,012 et 0,017 $, et l'estimation fixe de 0,015 $ tombe dans cette fourchette.
- Seule la facture OpenAI de la nuit permet de trancher : le copilote compte 8,48 $ en tout.

**Corrections possibles, à décider** :
1. **Afficher les deux montants séparément**, par exemple « partie 0,14 $ réel + 0,12 $ estimé ». C'est le minimum pour lever la confusion.
2. **Laisser choisir ce que compte le budget** : réel seul, ou réel + estimé (le comportement actuel, plus prudent).
3. **Recaler l'estimation sur ta facture**, et éventuellement compter les jetons de sortie déjà reçus avant l'annulation.
