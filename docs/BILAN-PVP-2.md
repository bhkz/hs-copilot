# Bilan des parties classées au 25/10/2026, 22 h, et audit des statistiques

## Résumé

- **8 parties classées en Libre, 8 victoires, aucune défaite** : les 3 de 19 h 26 à 19 h 55 ([BILAN-PVP-1.md](BILAN-PVP-1.md)) et 5 de 21 h 08 à 22 h 00.
- **Statistiques des serviteurs : pas de défaut trouvé.**
  - La reconstruction est exacte : comparée à une relecture indépendante des lignes brutes, 1 407 valeurs, 0 écart.
  - Le modèle reçoit ces valeurs telles quelles.
  - Les libellés affichés portent les statistiques du moment : 101 libellés, 0 écart.
- **Un défaut réel, corrigé : une fausse alerte du contrôle de confidentialité.** Il n'y avait pas de fuite (voir 3).
- **Le Rembobinage est enfin observé en direct.** Dans trois parties, les lignes renvoyées par HDT ont été ignorées et l'état est resté exact.
- **L'appel conservé fonctionne en direct.** Quand tu joues sur le début de réponse, la suite du plan arrive en 2,3 s en médiane.

## 1. Les huit parties

| Partie | Heure | Adversaire | Tours | Fin | Conseils complets suivis |
|---|---|---|---|---|---|
| 1 | 19 h 26 | Illidan | 7 | abandon adverse | 9 sur 10 |
| 2 | 19 h 35 | Thrall | 10 | abandon adverse | 8 sur 8 |
| 3 | 19 h 47 | Chaman | 7 | abandon adverse | 7 sur 7 |
| 4 | 21 h 08 | Dame Liadrin | 8 | abandon adverse | 16 sur 16, plus 1 affiché au moment de l'abandon |
| 5 | 21 h 17 | Roi Anduin | 14 | héros adverse tué | 20 sur 20 |
| 6 | 21 h 35 | Rexxar | 8 | abandon adverse | 11 sur 11, plus 1 affiché au moment de l'abandon |
| 7 | 21 h 45 | Orweyna | 7 | abandon adverse | 13 sur 13, plus 1 affiché au moment de l'abandon |
| 8 | 21 h 51 | Kel'Thuzad | 10 | abandon adverse | 14 sur 14, plus 1 affiché au moment de l'abandon |

**Conseils complets (actions et choix, mulligans à part) : 98 suivis sur 99 jouables.**
- La seule exception est déjà connue : partie 1, Garrosh a attaqué le jeton plutôt que le Surveillant satyre.
- 4 autres conseils étaient à l'écran quand l'adversaire a abandonné.

Les débuts de réponse ne sont pas comptés ici. Tu as dit improviser parfois en fin de tour ; ces coups-là ne sont pas imputés au conseiller.

**Ce que ces victoires ne prouvent pas.**
- 7 parties sur 8 se terminent par un abandon : on ne connaît ni le rang ni le niveau des adversaires.
- Le Libre n'est pas le format couvert par les guides de stratégie.
- Suivre un conseil n'est pas la preuve qu'il était le meilleur coup.
- La série mérite d'être étudiée, pas d'être attribuée au conseiller.

## 2. Statistiques des serviteurs : reconstruction, transmission, affichage

| Couche | Vérification | Résultat |
|---|---|---|
| Reconstruction | À chaque point de décision des 8 parties, l'attaque, les PV actuels et les PV maximum de chaque serviteur, et les PV et l'armure de chaque héros, comparés à une relecture indépendante des tags bruts du journal (ATK, HEALTH, DAMAGE, ARMOR) | 263 points, 1 407 valeurs, **0 écart**, Rembobinages compris. L'outil détecte une erreur injectée. |
| Transmission au modèle | Le message envoyé est l'état sérialisé ; nouveau test sur 4 parties enregistrées : chaque serviteur envoyé a l'attaque, les PV et les PV maximum de l'état | conforme, y compris pour les serviteurs blessés et ceux dont l'attaque a été modifiée |
| Affichage du conseil | Les 101 libellés de cible de la trace (« → Bébé marsuul 2/2 », « (37 PV + 2 armure) ») comparés à l'état reconstruit au même instant | **0 écart** |
| Textes du modèle | Chiffres cités dans les résumés et les étapes de plan (« le Forgeron à 3 ATQ », « pour 5 dégâts ») | 8 vérifiables, 8 justes. L'échantillon est petit. |
| Fenêtre principale | Plateau construit à chaque nouvel état publié (au plus 100 ms d'attente), avec attaque, PV, PV maximum et base de la carte | lecture du code ; pas de trace de l'affichage pour une comparaison chiffrée |

**Contrôle croisé avec HDT.** Il signale des écarts dans chaque partie : 3 à 24 relevés. Ils viennent du décalage d'affichage, pas d'une erreur.
- Exemple, partie 5 à 21:22:44.4 : une carte adverse détruit quatre de tes serviteurs. Le moteur l'enregistre aussitôt ; HDT, au rythme des animations, les compte encore trois secondes plus tard.
- Autre exemple : HDT donne 30 PV à un héros adverse qui en avait 40 ; les libellés et la relecture indépendante confirment 40.

**Ce qui peut donner l'impression de statistiques « pas à jour » :**
1. **Le copilote est en avance sur l'écran.** Il suit le serveur, qui précède l'animation de 0,6 à 1,5 s en médiane, et parfois de plusieurs secondes pendant les grosses animations. Un serviteur peut donc y paraître déjà blessé, ou déjà mort, alors que l'écran ne l'a pas encore montré.
2. **Les textes du modèle sont figés.** Le résumé d'un conseil et le texte d'une étape de plan sont écrits une fois, sur l'état analysé. Le libellé d'action, lui, est recalculé (« → cible a/h »).
3. **La fenêtre principale affiche aussi la base de la carte** (« 7 / 5 · base 3/5 »), qu'on peut prendre pour une valeur périmée.

Si tu revois le phénomène, note l'heure et fais une capture : la trace et la relecture indépendante permettent de retrouver l'instant exact.

## 3. Défaut corrigé : fausse alerte de confidentialité (partie 5)

Le rapport de la partie 5 indiquait « Aucune carte adverse cachée exposée : 28 violations ». Toutes concernaient la même carte.

Ce que dit le journal, à 21:31:38 :
1. Pendant une Invocation, le serveur montre le Galakrond du deck adverse (`SHOW_ENTITY`, `REVEALED=1`) ;
2. il l'améliore en « Galakrond, l'Apocalypse » (`CHANGE_ENTITY`) ;
3. puis il le cache de nouveau (`HIDE_ENTITY`, `REVEALED=0`).

L'amélioration a lieu pendant la révélation, donc elle était à l'écran. Le moteur avait raison de la considérer comme publique. Le contrôle, lui, ne retenait que la première identité montrée.

**Correction** : une carte transformée pendant sa révélation est désormais vue par le contrôle. Un test rejoue la séquence ; il vérifie aussi qu'une carte transformée sans révélation reste cachée, et il échoue sans la correction. Les 8 parties donnent maintenant 0 violation.

## 4. Rembobinage : première observation en direct

Les parties 4, 5 et 6 contiennent un Rembobinage. HDT a renvoyé la partie, et le copilote a ignoré les lignes déjà traitées : 3 396, 10 419 et 3 575 lignes.

C'est la correction du 25/10 après-midi, jamais vue en direct jusqu'ici. La relecture indépendante ne trouve aucun écart après ces Rembobinages.

## 5. Appel conservé et suite du plan, en direct

| Parties | Appels conservés après le début de réponse | Suites du plan reçues après ton coup | Suites du plan affichées sans appel | Appels annulés, coût estimé moyen |
|---|---|---|---|---|
| 1 à 3 (avant la correction) | — | — | 1, 2, 3 | 0,22 $ par partie |
| 4 à 8 | 5, 7, 4, 5, 8 | 2, 4, 2, 5, 6 | 8, 10, 4, 11, 10 | 0,16 $ par partie |

- **Délai mesuré** : sur 20 attentes « suite du plan attendue », 15 ont affiché la suite en 2,3 s en médiane (0,1 à 3,2 s). L'estimation était d'environ 2 s. Les 5 autres ont relancé une analyse, faute de suite utilisable.
- **Premier conseil complet du tour, en médiane** : 11,5 à 20,6 s dans les parties 4 à 8, contre 26,7 et 31,6 s dans les parties 2 et 3. Le style de jeu et les parties diffèrent : c'est une observation, pas une preuve de cause.
- **Coût réel** : 0,14 à 0,26 $ par partie.

## 6. Outils ajoutés

- `hscopilot stats-dump <journaux…> --out fichier.jsonl` : statistiques projetées à chaque point de décision.
- Les scripts de relecture indépendante (tags bruts, libellés de la trace, chiffres des textes) sont dans [bench/partie5-pvp/outils](../bench/partie5-pvp/outils), et leurs résultats dans [statistiques.md](../bench/partie5-pvp/statistiques.md). Ils ne font pas partie du produit.

Il y a 239 tests, tous réussis. Aucun appel payant n'a été fait.

## 7. Limites

- **Affichage non enregistré.** L'affichage de la fenêtre principale n'est pas enregistré : sa justesse repose sur la lecture du code et sur l'état publié, vérifié.
- **Textes du modèle peu vérifiables.** Seuls 8 chiffres avaient une forme assez nette pour être contrôlés.
- **Pas de démonstration en direct pour la garde.** La garde « effet non prévu » n'a rencontré aucun cas réel.
- **Version en cours d'exécution.** L'application lancée à 21 h 06 n'a pas la correction du contrôle de confidentialité. Le script de lancement recompilera au prochain démarrage.
