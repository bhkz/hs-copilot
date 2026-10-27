# Bilan des trois premières parties classées avec le copilote

Parties du 25/10/2026, de 19 h 26 à 19 h 55, en classé **Libre** (`GT_RANKED`/`FT_WILD`), avec le Guerrier Dragons. Réglages : effort medium, 8 analyses par tour, 1,75 $ par partie.

Une partie classée à 19 h 22 s'est terminée au tour 1, avec les conseils encore désactivés en classé ; elle n'est pas comptée ici.

Sources :
- audits [audit-pvp1.md](../bench/partie4-pvp/audit-pvp1.md), [audit-pvp2.md](../bench/partie4-pvp/audit-pvp2.md), [audit-pvp3.md](../bench/partie4-pvp/audit-pvp3.md) ;
- Découvertes : [decouvertes.md](../bench/partie4-pvp/decouvertes.md) ;
- rapports de partie dans `%LocalAppData%\HsCopilot\reports`.

## Résumé

| Partie | Adversaire | Résultat | Tours | Conseils complets (dont suite du plan) | Découvertes à Sombre don | Appels envoyés / terminés / annulés | Coût mesuré + annulés estimés |
|---|---|---|---|---|---|---|---|
| 1 | Chasseur de démons (Illidan) | gagnée | 7 | 11 (1) | 2 | 18 / 11 / 7 | 0,19 + 0,10 $ |
| 2 | Chaman (Thrall) | gagnée | 10 | 9 (2) | 1 | 27 / 7 / 19 (+1 en cours à la fin) | 0,11 + 0,28 $ |
| 3 | Chaman | gagnée | 7 | 8 (3) | 2 | 24 / 6 / 18 | 0,11 + 0,27 $ |

**Trois victoires ne prouvent pas que le conseiller joue mieux qu'un joueur expérimenté.** L'échantillon est minuscule, et le niveau des adversaires, les tirages et la variance ne sont pas contrôlés.

Ce que les traces établissent :
- tous les conseils étaient des actions légales ;
- tu as suivi tous les conseils complets sauf un : partie 1, tour 7, tu as attaqué le jeton Satyre illidari 2/2 au lieu du Surveillant satyre 4/2 conseillé ; le conseil complet est arrivé à l'instant où tu jouais ;
- aucun létal certain n'a été manqué ;
- chaque analyse de ton tour contenait la pioche du tour.

## 1. La Guerrière à 28 ATQ (partie 2, tour 5) : pas de létal

| Vérification | Constat (révision 169, 19:40:14) |
|---|---|
| Héros adverse | Thrall : **29 PV** (1 dégât au tour 2), 0 armure. Les 29 PV étaient les siens ; toi, tu en avais 28. |
| Serviteur | Guerrière du Vol infini 28/5, **Ruée**, jouée ce tour-ci |
| Options envoyées par le serveur | attaquer l'Élémentaire radieux 2/3 avec la Guerrière, ou finir le tour. Aucune attaque sur le héros. |
| Provocations, Secrets | aucun |
| Calcul local | 0 dégât possible au héros ; avant de jouer la Guerrière (révision 165), 11 au maximum |
| Conseil de l'IA (19:40:23) | « Attaquer avec Guerrière du Vol infini → Élémentaire radieux 2/3 : supprime sa réduction de coût » |

**Il n'y avait pas de létal, et il n'y avait rien à corriger.** Deux raisons, chacune suffisante :
- une Ruée jouée ce tour-ci ne peut attaquer que des serviteurs, et le serveur ne proposait pas le héros ;
- 28 dégâts ne tuent pas un héros à 29 PV.

L'IA a conseillé la seule attaque possible.

Au tour adverse suivant, à 19:40:33, le Chaman a transformé la Guerrière en Grenouille. Elle n'a jamais pu attaquer le héros.

D'où venaient les 28 ATQ (journal de la partie) :
1. Tour 1 : à la Découverte de Sombre chevaucheur, l'IA a recommandé « Guerrière du Vol infini + Réveil brutal ». Le don était décrit au modèle ainsi : « 3/5 pour 5, ses Cris de guerre se déclenchent deux fois ».
2. Tours 2 et 3 : deux Forgerons dimensionnels lui donnent +2 ATQ chacun en main, 3 → 5 → 7.
3. Tour 5 : son Cri de guerre double son attaque, deux fois grâce au don, 7 → 14 → 28.

## 2. Sombres dons, mulligans, suite du plan

- **Sombres dons : validés en direct sur 5 Découvertes.**
  - Les 15 propositions sont parties au modèle avec leur don ; le moteur a identifié les 15 dons et la table a traduit les 15 effets.
  - Tu as pris les 5 cartes recommandées : Cogneur d'œufs + Cauchemar vivant, Calamar pas cuit + Posture de défense, Guerrière du Vol infini + Réveil brutal, Gorgonzormu + Cauchemar vivant, Berserker hacheuse + Serres de harpie.
  - Une fois en main, les 5 cartes avaient les statistiques calculées.
  - Chaque fois, tu as choisi sur le début de réponse, avant le conseil complet.
- **Défaut trouvé et corrigé : l'attaque 0 manquait.** L'Avocat commis d'office 0/7 (partie 1, 19:31) est parti vers le modèle sans attaque (« /7 »), et sa version avec le don sans statistiques. Le serveur n'envoie pas une attaque nulle, et la base de cartes n'en a pas pour 673 serviteurs à 0 ATQ. Un serviteur sans attaque connue vaut maintenant 0 ATQ ; un test reproduit le cas.
- **Mulligans : traçabilité validée en direct.** Les trois entrées `résultat-mulligan` donnent le conseil, les cartes gardées, remplacées et reçues, et « conseil suivi ». La pièce est restée gardée et n'a jamais été comptée comme remplacée (parties 2 et 3).
- **Suite du plan sans appel : 6 conseils** (1, 2 et 3 selon la partie), tous suivis. Exemple, partie 3, tour 4 : après le conseil complet du modèle (Gorgonzormu attaque l'Oracle), les deux étapes suivantes sont apparues en 0,3 s sans appel : Forgeron sur l'Éclaireur, puis Frappe anticipée sur le Fouisseur.
- **Létal** : il n'y a eu aucun létal certain dans ces parties. Au tour 7 de la partie 3, le calcul local a trouvé un létal conditionnel, 23 dégâts plus l'arme aléatoire d'Animatrice de stade contre 24 PV. L'IA a conseillé Animatrice, tu l'as jouée, et la partie s'est terminée ce tour-là.

## 3. Synchronisation avec l'écran

| Partie | Retard serveur → écran au début de ton tour (médiane / maximum) | Conseils retenus « TON TOUR ARRIVE » | Entrées de la trace avec l'écran suivi |
|---|---|---|---|
| 1 | 1,5 s / 12,1 s (tour 1, juste après le mulligan) | tours 1 et 3 | 101 sur 103 |
| 2 | 0,6 s / 4,5 s | aucun | 125 sur 127 |
| 3 | 0,6 s / 3,7 s | tour 7 | 106 sur 107 |

- **En classé, le décalage est court**, comme mesuré auparavant : environ 1,6 s, contre 10 s contre l'IA. Le plus souvent, ton tour était déjà affiché quand le premier conseil est arrivé : rien n'était à retenir.
- **Trois fois, un conseil est arrivé avant l'écran et a été retenu** jusqu'à l'apparition de ton tour, puis affiché comme ACTION IMMÉDIATE sans nouvel appel :
  - partie 1, tour 1 : 7,4 s d'avance ;
  - partie 1, tour 3 : 6,2 s d'avance ;
  - partie 3, tour 7 : 1,6 s d'avance (début de réponse « Jouer Animatrice de stade » à 19:54:55.3, tour affiché à 19:54:56.9).

  Les autres mises à jour retenues étaient des « Analyse en cours », au plus 4,3 s (partie 2, tour 9).
- **Plusieurs parties de suite** : la lecture du Power.log a suivi trois parties consécutives dans le même fichier. Les entrées sans suivi sont les 1 ou 2 premières de chaque partie, avant que le client ait exécuté un tour.
- **Défaut de mesure trouvé et corrigé.** Quand tu joues en premier, le serveur te donne la main au tour 1 dès le mulligan. Les mesures de début de tour partaient alors du mulligan : le rapport de la partie 1 indique 39,5 s au lieu de 12,1 s. Les durées « début de tour → premier conseil » et « première action affichée » du tour 1 étaient faussées de la même façon. L'affichage n'était pas touché, car la retenue exigeait déjà la fin du mulligan. Le tour commence maintenant après le mulligan ; un test reproduit le cas.

## 4. La vraie limite en classé : le temps de réponse

Délais depuis le début de ton tour côté serveur, calculés depuis les audits :

| Partie | Premier début de réponse (médiane) | Premier conseil complet (médiane) | Tours sans aucun conseil complet |
|---|---|---|---|
| 1 | 5,7 s | 10,7 s | 0 sur 7 |
| 2 | 7,1 s | 26,9 s | 5 sur 10 |
| 3 | 7,8 s | 33,7 s | 3 sur 7 |

Durée d'un appel au modèle, mesurée par les rapports :

| Partie | Premier fragment (médiane / 95 %) | Réponse complète (médiane / 95 %) |
|---|---|---|
| 1 | 4,4 s / 12,4 s | 9,9 s / 19,0 s |
| 2 | 4,0 s / 8,6 s | 9,8 s / 13,9 s |
| 3 | 1,7 s / 8,7 s | 7,8 s / 15,9 s |

- **Mécanisme.** En classé, tu joues souvent dès le début de réponse. La décision analysée est alors dépassée, l'appel est annulé et sa réponse complète, avec la suite du plan, n'arrive jamais. Le copilote relance un appel sur le nouvel état.
- **Exemple, partie 3, tour 4** :
  1. 16 s après le début du tour, le début de réponse indique « Suffusion d'ombreflamme sur le Fouisseur » ; tu la joues 5 s plus tard, et l'appel est annulé.
  2. La Découverte reçoit un début de réponse en 7 s.
  3. Après ton choix, un nouvel appel met 17 s à donner le premier conseil complet du tour, 48 s après son début.
- **Coût.** Dans les parties 2 et 3, 70 à 75 % des appels envoyés ont été annulés. Leur coût estimé, 0,27 à 0,28 $ par partie, dépasse celui des appels terminés, environ 0,11 $.
- **L'effort ne change pas grand-chose.** En direct, la partie jouée en effort low (16 h 37, contre l'IA) donnait 8,7 s par appel en médiane et 3,2 s pour le premier fragment. C'est du même ordre que ces parties en medium. Sur le banc d'essai de 36 situations ([MVP3A-RESULTATS.md](MVP3A-RESULTATS.md)), low répondait en 9,8 s en médiane, contre 11,9 s en medium. À 95 %, c'était 14,5 s contre 22,0 s. Les deux efforts choisissaient le même coup dans 86 % des cas. Passer en low raccourcirait surtout les réponses les plus lentes ; les annulations resteraient.

## 5. Corrections faites

| Défaut constaté | Correction | Vérification |
|---|---|---|
| Mesures de début de tour parties du mulligan quand tu joues en premier | le tour commence après le mulligan ; les compteurs du tour 1 aussi | test `When_you_play_first_your_turn_1_starts_after_the_mulligan_not_during_it`, qui échoue sans la correction |
| Serviteur à 0 ATQ proposé sans attaque | 0 ATQ par défaut pour un serviteur | test `A_discovered_minion_with_0_attack_is_offered_as_0_x`, qui échoue sans la correction ; audit des Découvertes refait : « 0/7 » |
| L'audit prenait les instants d'affichage d'une autre partie quand le Power.log en contient plusieurs | chaque partie de l'écran est un segment ; l'audit garde celui qui suit les tours de la partie auditée | les trois audits sont identiques, que l'écran vienne du Power.log complet ou des journaux enchaînés |
| Dans l'audit, un conseil retenu puis republié comptait deux fois dans « Coups joués » | une ligne par conseil | audit de la partie 1 refait |
| Sorts proposés à une Découverte avec des statistiques vides dans la trace | coût seul pour les sorts | — |
| Deux tests de transport et les simulations écrivaient de faux rapports dans ton dossier `reports` (264 de tests, 8 de simulations) | tests sans rapport, simulations dans un dossier temporaire | les 272 fichiers ont été déplacés, pas supprimés, dans `reports\artefacts-tests-claude` |

**Aucune correction de stratégie n'est justifiée par ces parties** :
- aucun létal manqué ;
- aucune mise en garde d'ordre de jeu déclenchée ;
- aucun conseil illégal.

Il y a 224 tests, tous réussis, et la Release est recompilée.

## 6. Propositions, non faites, à décider

1. **Fait le 25/10/2026 au soir : ne plus perdre la réponse quand tu joues le coup du début de réponse** ([CONTINUITE-DU-PLAN.md](CONTINUITE-DU-PLAN.md)). Sur ces trois parties, 15 nouveaux appels, dont les deux de La pièce, suivaient un début de réponse joué tel quel, sans imprévu.
2. **Effort low en classé** : d'après le point 4, il gagnerait surtout sur les réponses lentes. C'est un réglage de l'onglet Conseiller ; à toi de choisir.
3. **Conformité** : ton relecteur recommande de désactiver les conseils en direct en classé tant que la position de Blizzard n'est pas claire (voir [CONFORMITE.md](CONFORMITE.md)). Ils restent activés, comme tu l'as demandé.

## 7. Limites

- **Échantillon** : trois parties, en Libre. Les sources de stratégie décrivent la méta Standard, pas tes adversaires.
- **Qualité stratégique non mesurée** : seules la légalité, la cohérence avec le calcul local et le suivi des conseils sont vérifiés, sans référence indépendante.
- **Moment de l'image** : le « tour affiché » est l'exécution de l'étape MAIN_ACTION par le client. L'instant exact où l'image change n'est pas dans les logs.
- **Coût des annulations** : c'est une estimation forfaitaire d'environ 0,015 $ par appel, car un flux interrompu ne renvoie pas sa consommation.
