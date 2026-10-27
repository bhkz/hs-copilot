# Première vraie partie : analyse et corrections

Partie d'entraînement contre l'IA, le 25/10/2026 de 15 h 44 à 15 h 52, Guerrier Dragons contre Guerrier, victoire au tour 7. Données analysées :

- les deux rapports de validation de `%LocalAppData%\HsCopilot\reports\` ;
- les deux journaux bruts de `%LocalAppData%\HsCopilot\sessions\` ;
- le vrai Power.log de Hearthstone.

## En bref

- **Le copilote a fonctionné en direct jusqu'au Rembobinage du tour 6.** L'état reconstruit concordait avec les vues imprimées par le client, avec 0 écart sur 304. Les lignes arrivaient en 118 ms en médiane. 7 conseils complets ont été affichés, tous légaux.
- **Un Rembobinage a déclenché le défaut principal.** HDT a relu la partie et l'a renvoyée au plugin, sans le passage rembobiné. HS Copilot a reconstruit la partie à partir de ce renvoi tronqué. L'état est devenu faux, et un conseil a été produit sur cet état.
- **Le moteur n'est pas en cause.** Rejoué sur le vrai Power.log, il reconstruit la partie sans aucun écart, Rembobinage compris.
- **Le plafond de 3 analyses par tour a privé la fin des tours longs de conseils.** 9 décisions sur 23 n'ont pas été analysées.
- **Tout est corrigé et testé sur ta partie anonymisée.** La correction du renvoi reste à confirmer par une partie en direct avec un Rembobinage.

## Ce qui s'est passé

| Heure | Événement | Preuve |
|---|---|---|
| 15:44:51 | Début de partie, flux du plugin HDT | rapport n° 1 : log → réception P50 118 ms, P95 234 ms |
| 15:45 à 15:51 | 14 appels envoyés, 7 conseils complets, 7 appels annulés, 9 décisions non analysées | compteurs du rapport n° 1 |
| 15:51:09.447 | Rembobinage d'Animatrice de stade : le serveur réinitialise la partie | RESET_GAME, ligne 3 558 du journal n° 1 |
| 15:51:09 à 15:51:11 | HDT relit la partie et la renvoie au plugin : les lignes 1 à 3 333, puis directement la suite à partir de 15:51:09.580. Les 1 060 lignes du passage rembobiné manquent, dont la réinitialisation du serveur | comparaison ligne à ligne des journaux avec le Power.log |
| 15:51:11 | HS Copilot voit un CREATE_GAME et reconstruit la partie depuis ce renvoi | journal n° 2 ouvert, rapport n° 2 |
| 15:51:11 à 15:51:51 | État faux : 4 cartes en main au lieu de 2, adversaire à 5 PV au lieu de 8, 4 écarts avec les vues du client. 2 appels, 1 conseil sur cet état | rapport n° 2 : verdict ÉCHEC, contrôle HDT, indicateur UNKNOWN_ENTITY_REFERENCED |

Le rapport n° 2 affiche aussi des latences « log → réception » de 200 s : ce sont les lignes anciennes du renvoi, comptées comme si elles arrivaient en direct.

Les écarts avec HDT dès 15:45, d'une carte en main par exemple, existaient avant le renvoi. Notre état concordait alors avec les vues du client. L'écart vient donc de HDT, dont l'état suit les animations, mais la cause exacte n'est pas établie.

## Tes quatre observations

| Ce que tu as vu | Diagnostic | Nature | Correction |
|---|---|---|---|
| Un conseil pendant le tour adverse, sans ta pioche | Cohérent avec le défaut de renvoi : après le Rembobinage, l'état et donc le conseil étaient faux. Les textes affichés n'étaient pas enregistrés, ce qui empêche de confirmer chaque conseil | défaut de transport | filtre du renvoi ; aucune analyse pendant un rattrapage ; trace des conseils |
| Des attaques sur Garrosh oubliées | Le plan du modèle se limitait à 2 étapes. Au tour 5, trois serviteurs pouvaient attaquer le héros, et les analyses suivantes ont été bloquées par le plafond | limite du modèle et effet du quota | plan jusqu'à 4 étapes ; liste locale « Encore possible » à chaque état ; quota revu |
| Une fin de tour conseillée avec 2 mana et le pouvoir héroïque disponible | Non vérifiable : les conseils n'étaient pas enregistrés. Dans le vrai log, la seule fois où il te restait 2 mana sans autre action, au tour 5, tu as utilisé ton pouvoir | limite possible du modèle | règle explicite dans les instructions ; mise en garde locale si une fin de tour est conseillée alors que des attaques ou le pouvoir restent possibles |
| Le plafond de 3 analyses par tour | Confirmé : 9 décisions sur 23 sans analyse. Les tours 5 et 6 comptaient 5 et 6 décisions, et les appels annulés consommaient le quota | effet du quota | nouvelles règles de quota |

## Corrections

1. **Filtre du renvoi**, dans `Session/ReplayGuard.cs`. Pendant une partie en cours, une ligne plus ancienne que la dernière traitée est ignorée, de même qu'un CREATE_GAME identique à celui de la partie en cours. Le flux reprend à la première ligne nouvelle. Le journal de la session l'indique : « Le flux renvoie des lignes déjà traitées… », puis « Reprise du flux à… ».
2. **Aucune analyse pendant un rattrapage.** Un état issu de lignes en retard ou du tampon du plugin n'est jamais analysé automatiquement. La réanalyse manuelle reste possible.
3. **Quotas.** Le début de tour après la pioche, le mulligan et les choix ne sont jamais bloqués par le plafond par tour. Un appel annulé compte toujours dans le quota, car le rendre coûterait environ 45 % de plus pour peu de décisions couvertes en plus, comme le montre la mesure ci-dessous. Ce remboursement reste disponible en option. Les budgets restent de 0,75 $ par partie et 3 $ par session.
4. **Plan du tour entier.** Le modèle liste jusqu'à 4 actions restantes, attaques et pouvoir héroïque compris. L'overlay en montre 2, puis le nombre des autres, toutes marquées « non vérifié ».
5. **Vérification locale, gratuite et instantanée.** À chaque état, l'analyse rapide nomme ce qui reste possible : « Encore possible : attaque de Dragonnet (3 ATQ), héros adverse possible ; pouvoir héroïque Gain d'armure ! (2) ». Si le conseil est « Finir le tour » alors que des attaques ou le pouvoir restent possibles, l'overlay affiche en orange « Avant de finir le tour, encore possible : … ».
6. **Trace des conseils.** Un fichier `.jsonl` par partie, dans `%LocalAppData%\HsCopilot\advisor\`. Il enregistre chaque conseil affiché, retiré ou non lancé, avec son tour, sa révision, sa latence et son coût.
7. **Mesure « log → affichage ».** Elle ne compte plus que le premier affichage de chaque révision. Le P95 de 7,8 s du rapport n° 1 venait des republications du même état après chaque contrôle HDT.
8. **Script de lancement.** Il recompile si le code a changé et ferme proprement une instance déjà ouverte. Sinon, l'ancienne version aurait été relancée.
9. **Partie classée.** Conseils activés, à ta demande, dans ta configuration, avec une case dans l'onglet « Conseiller ». Le risque est rappelé dans [CONFORMITE.md](CONFORMITE.md).

Le reste est inchangé : l'overlay, la distinction entre l'action immédiate vérifiée par le serveur et le plan non vérifié, et le modèle gpt-6.1-sol en effort low.

## Avant / après

### Ta partie, rejouée avec le flux exact reçu de HDT

Les deux journaux de la session mis bout à bout redonnent exactement ce que le plugin a reçu, renvoi compris. Ce flux a été rejoué dans la version corrigée au rythme d'origine, avec un fournisseur simulé gratuit. Fichier : `bench/correctifs/partie-reelle-flux-hdt-apres-final.md`.

| Contrôle | Pendant ta partie | Même flux, version corrigée |
|---|---|---|
| Rapports pour cette partie | 2, dont un en ÉCHEC | 1 |
| Lignes renvoyées par HDT et ignorées | 0 | 3 338 |
| Rembobinage appliqué | perdu à 15:51:11 | 1 sur 1 |
| Écarts avec les vues du client | 4 | 0 sur 366 |
| Conseils produits sur un état faux | 1 | 0 |
| État en fin de partie | faux, adversaire à 5 PV au lieu de 8 | exact, victoire |

Le verdict de cette simulation reste « À COMPLÉTER », pour deux raisons propres à la simulation : il n'y a pas de HDT pour le contrôle croisé, et les horodatages d'origine ont été gardés pour reproduire le renvoi, ce qui rend la latence « log → réception » sans objet.

### Quotas : même partie, même rythme, latence simulée calée sur la mesure réelle

Partie de test « Grommash + Profondeurs », rejouée au rythme d'origine. Le fournisseur simulé répond en 9 ± 3 s, avec l'action lisible à mi-parcours, comme les vrais appels mesurés. Aucun appel payant. Fichiers : `bench/correctifs/quotas-avant.md`, `quotas-apres.md` et `quotas-option-remboursement.md`.

| Mesure | Avant | Après, réglage par défaut | Option : un appel annulé rend son quota |
|---|---|---|---|
| Décisions analysées | 32 | 33 | 46 |
| Décisions non analysées | 17 | 16 | 3 |
| Appels envoyés | 29 | 30 | 42 |
| Appels annulés après l'envoi | 16 | 14 | 28 |
| Conseils complets | 13 | 16 | 14 |
| Décisions où l'action s'est affichée | 18 | 19 | 21 |
| Début du tour, première action affichée, médiane | 4,2 s | 4,2 s | 5,0 s |
| Coût estimé par partie | 0,44 $ | 0,46 $ | 0,64 $ |

Le coût est estimé ainsi : 0,0155 $ par conseil complet, valeur mesurée sur les vrais appels, plus 0,015 $ par appel annulé après l'envoi, valeur estimée. Les écarts d'un à trois appels entre « avant » et « après » relèvent de la variabilité de la simulation.

**Choix retenu.** Rendre son quota à un appel annulé ne couvre que quelques décisions de plus, parce que tu joues pendant l'analyse, et coûte environ 45 % de plus. Ce n'est donc pas le réglage par défaut, mais une option : `advisor.refundCancelledCalls`. Le réglage par défaut garde le coût d'avant et ajoute :

- la priorité du début de tour, du mulligan et des choix ;
- l'absence d'analyse pendant un rattrapage ;
- la liste locale « Encore possible », recalculée gratuitement à chaque état. Elle comble les décisions non analysées en fin de tour.

### Temps de réponse et coût par appel

Il n'y a pas eu de nouvel appel payant pour ces corrections. Le modèle, l'effort et la taille du message ne changent presque pas : deux règles en plus dans les instructions, soit une centaine de jetons sur environ 8 000. La durée d'un appel reste donc celle mesurée : action lisible en 4 à 5 s, conseil complet en 8 à 9 s.

Le plan jusqu'à 4 étapes ajoute quelques dizaines de jetons en fin de réponse. Il peut allonger le conseil complet d'une fraction de seconde, sans retarder l'action, qui arrive en premier. Cet effet n'a pas été mesuré en appel réel.

## Ce qui reste à vérifier

- **Une partie en direct avec un Rembobinage**, pour confirmer le filtre dans les conditions réelles. Le test de non-régression rejoue ta partie anonymisée, le renvoi de HDT compris, mais ce n'est pas HDT lui-même.
- **La qualité des conseils sur tout un tour.** Le plan reste non vérifié : aucun moteur ne simule encore la suite des actions. La liste locale « Encore possible » sert de filet de sécurité, pas de stratégie.
- **Le coût réel des appels annulés**, toujours estimé.
