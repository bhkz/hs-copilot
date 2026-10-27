# Rapport de validation et audit des risques

Date : 25/10/2026, suite au retour sur le MVP 1. Ce document dit exactement ce qui est prouvé, comment le reproduire, ce qui reste incertain et ce qui peut passer en MVP 2 ou 3.

## Verdict

- **Reconstruction de l'état : prouvée** sur 62 parties réelles (3 classées, 59 contre l'IA), avec quatre contrôles indépendants du moteur et zéro écart.
- **Chaîne temps réel : prouvée en simulation fidèle.** La vraie DLL du plugin tourne sous .NET Framework, comme dans HDT, et la vraie fenêtre WPF affiche la partie. Le rythme est celui de la partie d'origine, et les latences sont mesurées.
- **Dernier maillon non exécuté** : une partie jouée dans Hearthstone avec HDT ouvert. Il demande que tu lances le jeu. Le rapport de validation est désormais produit **automatiquement** en fin de partie.
- **Strategy Engine** : les contrats, le validateur, l'invalidation, le garde-fou anti-fuite et le coordinateur sont prêts et testés, **sans aucune IA branchée**.

## Ce qui est prouvé

| Fait | Preuve | Reproduire |
|---|---|---|
| L'état reconstruit correspond à la vue du client | 69 999 vues comparées, 0 écart ; 88 % identiques à l'état courant, le reste à un état antérieur d'au plus 13 révisions | `hscopilot verify "C:\Program Files (x86)\Hearthstone\Logs"` |
| Les tours sont attribués au bon joueur | 2 548 blocs d'options, tous reçus pendant un tour attribué au joueur local (le serveur n'envoie les options qu'au joueur actif) | idem |
| Coûts affichés et mana disponible sont justes | 11 509 cartes de la main comparées aux refus « mana insuffisant » du serveur, 0 désaccord | idem |
| Aucune carte adverse cachée n'est exposée | 33 524 emplacements adverses audités, 0 fuite ; preuve de publicité établie sans le moteur | idem |
| Aucun message sortant ne contiendrait de carte cachée | garde-fou appliqué au message de chaque point de décision des 5 parties de test, 0 identifiant interdit | test `No_outgoing_payload_contains_a_hidden_card_at_any_real_decision_point` |
| Rembobinage | 13 resets appliqués, actions annulées marquées, recommandations invalidées | tests `Rewind_*` |
| Transitions en direct | début, mulligan, pioches, actions, fins de tour et fin de partie détectées dans les deux parcours simulés | rapports de la section « Latences » |
| Transport plugin | 8 478 lignes relayées par la vraie DLL à rythme réel, 0 perdue | parcours « Plugin réel » de la section « Latences » |
| Pipe inaccessible depuis le réseau | `\\127.0.0.1` et `\\NOM-PC` refusés | test `Pipe_server_rejects_clients_coming_through_the_network_path` |

## Latences mesurées (rythme réel, sans Hearthstone)

Deux parcours complets jusqu'à l'affichage dans la fenêtre. Le simulateur réécrit chaque ligne à l'heure courante, comme le ferait Hearthstone.

| Étape | Repli : lecture du Power.log | Plugin réel (DLL, .NET Framework) |
|---|---|---|
| écriture dans le log → affichage | P50 92 ms, P95 143 ms | P50 28 ms, P95 129 ms |
| écriture → réception par l'application | P50 72 ms, P95 119 ms | P50 1 ms, P95 3 ms |
| réception → état publié | voir note | P50 1 ms, P95 109 ms |
| état publié → affiché | P50 21 ms, P95 38 ms | P50 23 ms, P95 32 ms |

- Le P95 d'environ 110 ms vient de la limitation volontaire à une publication toutes les 100 ms.
- La lecture du log ajoute environ 70 ms d'attente de scrutation.
- **Non mesuré** : le délai entre l'action à l'écran et l'écriture dans le Power.log par Hearthstone, ainsi que la latence propre de HDT. Seule une vraie partie les révèle (mesure « log → réception » du rapport).
- Note : la mesure « réception → état publié » du parcours log a été corrigée après ce run. Elle comptait des lots sans changement d'état et affichait un P95 trompeur de 886 ms.

## Défauts trouvés pendant cette validation, puis corrigés

| Défaut | Gravité | Comment il a été trouvé |
|---|---|---|
| **Adversaire IA renommé en cours de partie** : « Aubergiste » devient le nom du boss. Ses changements d'état étaient mis en attente et jamais appliqués (1 386 dans une partie), d'où un tour, un mana et un `CURRENT_PLAYER` adverses faux. 24 parties contre l'IA étaient concernées. | élevée | le test de référence de la Parenté a échoué (Wyrm à 6 sans Dragon joué « au tour précédent ») |
| **Surcharge comptée deux fois** : les cristaux verrouillés sont déjà inclus dans `RESOURCES_USED`. Le mana disponible affiché était 0 au lieu de 3. | moyenne | oracle mana sur le log du 22/10 |
| Ligne client `ReportStuck` inconnue | faible | vérification du log du 22/10 |
| Fin de partie scénarisée contre l'IA (boss déclaré perdant avec 23 PV) prise pour une incohérence | faible (contrôle) | vérification du log du 24/10 |
| Mesures faussées : lots sans effet comptés, fin de tour incluse dans « action → nouvelles options », états incomplets de la mise en place comptés, résultat transitoire « CONCEDED » | faible (mesure) | relecture des rapports simulés |

Aucun des oracles initiaux ne détectait le premier défaut : ils portent sur les cartes, pas sur l'entité joueur adverse. Deux contrôles ont été ajoutés pour le couvrir : l'attribution des tours à chaque bloc d'options, et un indicateur `PENDING_PLAYER_NAME` qui signale tout changement resté sans destinataire.

## Réponse aux remarques du retour

- **« Les 33 057 comparaisons utilisent le même flux. »** C'est exact. L'oracle compare les données reçues du serveur à la vue animée que le client imprime : deux traitements distincts d'une même source, pas l'écran. Trois éléments ont été ajoutés :
  - l'oracle des tours, fondé sur un signal indépendant (le serveur n'envoie des options qu'au joueur actif) ;
  - la fenêtre de retard réduite de 40 à 20 révisions, avec l'histogramme publié (maximum observé : 13) ;
  - le contrôle croisé avec HDT, un parseur indépendant écrit par d'autres, effectué au repos en direct et intégré au rapport automatique.

  Aucune comparaison avec l'écran n'existe ; elle reste à faire à l'œil lors de la partie test.
- **« La recharge des lieux est confirmée par Blizzard. »** Je n'ai pas pu vérifier cette source. La règle est en revanche établie par les logs : sur 44 activations réelles, le serveur refuse le lieu le tour même et au tour suivant (149 options sur 149), puis l'accepte toujours ensuite.
- **« Le Grand Dracorex est une Bête. »** C'est confirmé par la base de cartes : 18 Dragons sur 30.

## Ce qui reste incertain

| Point | Pourquoi | Ce qui le lèverait |
|---|---|---|
| Partie réelle de bout en bout (Hearthstone → HDT → plugin → fenêtre) | Hearthstone n'a jamais été lancé pendant le développement | la partie test de 5 minutes (procédure ci-dessous) |
| Latence de Hearthstone et de HDT avant notre code | non mesurable sans le jeu | mesure « log → réception » du rapport automatique |
| Contrôle croisé HDT en conditions réelles | jamais observé (pas de partie) | idem |
| Frappe anticipée avec un Glissedrake réduit | aucun cas discriminant dans les 62 parties : la condition peut porter sur le coût actuel ou de base | un tour avec Glissedrake à 4 comme seul serviteur de base ≥ 5 en main |
| Robustesse aux prochains patchs | nouvelles instructions ou tags possibles | `hscopilot verify` sur le dossier Logs après chaque patch ; les lignes inconnues sont signalées, jamais fatales |
| Séquences de plusieurs actions | aucun moteur de règles multi-actions | MVP 5 ; d'ici là, une action vérifiée puis réévaluation |
| Qualité stratégique des conseils | aucun LLM branché | MVP 3, puis mesure du taux de conseils suivis et légaux |
| Modes Champs de bataille, Arène, spectateur | non étudiés | hors périmètre actuel |

## Audit des risques

| Risque | Impact | État | Mesure en place |
|---|---|---|---|
| Chaîne en direct jamais observée | élevé | ouvert | procédure de 5 min, rapport automatique, simulation fidèle réussie |
| Changement de format du log après un patch | élevé | atténué | instructions inconnues signalées, `verify` sur un dossier, base de cartes mise à jour par HDT |
| Noms de joueurs inattendus | élevé | corrigé | résolution par élimination, indicateur `PENDING_PLAYER_NAME`, oracle des tours |
| Fuite d'information cachée vers une API | élevé | maîtrisé | règles de visibilité par provenance, audit continu en direct, garde-fou qui bloque l'envoi |
| Conseil périmé affiché | élevé | maîtrisé | liaison partie + révision + options, invalidation dès que l'état change, réponse tardive écartée |
| Séquence présentée comme vérifiée | moyen | maîtrisé | les étapes suivantes sont forcées à « non vérifié », mention signalée ; seule l'action immédiate est garantie |
| Conseiller en temps réel en classé | moyen (conformité) | maîtrisé | désactivé par défaut (`AllowRanked = false`), point non conseillé tracé |
| Lenteur du LLM | moyen | à mesurer | délai d'expiration, annulation, mesures « appel stratégie », « décision → conseil », « début de tour → premier conseil » et budget de 75 s |
| Blocage de HDT ou de l'interface | moyen | maîtrisé | plugin non bloquant (file + fil dédié), un seul fil moteur, appel stratégie asynchrone (test : moins de 200 ms même avec un fournisseur lent) |
| Relecture complète par HDT après un Rembobinage | faible | maîtrisé | même identifiant de partie, état reconstruit à l'identique, révisions croissantes |
| Liste de deck différente du deck joué | faible | signalé | indicateur `DECKLIST_MISMATCH` |

## Contrats du Strategy Engine (priorité 2)

Code : `src/HsCopilot.Core/Strategy/`. Schéma de réponse du LLM : [strategy-output.schema.json](strategy-output.schema.json).

1. **État observable et hypothèses** : le GameState projeté. Les hypothèses sont étiquetées : « probablement La pièce », carte vue puis renvoyée en main, Secret inconnu de classe X.
2. **Actions autorisées maintenant** : chaque option du serveur est déclinée par cible et sous-option, avec un identifiant stable dans la révision (`o9.5.t21`) et un libellé français.
3. **Séquences vérifiées** : une liste vide, avec la mention explicite « moteur de règles multi-actions non implémenté ».
4. **Recommandation** : le fournisseur ne renvoie qu'un `actionId`. Le coordinateur y attache la partie, la révision et les options. Le validateur rejette toute action absente. Le niveau d'incertitude n'est jamais « faible » si une information adverse est cachée. Le conseil est limité à 20 mots, et les étapes suivantes sont toujours « non vérifiées ».

Invalidation, avec la raison comptée dans les mesures : conseil suivi, autre action jouée, état modifié, fin de tour, Rembobinage, nouvelle partie, fin de partie.

## Tests de référence du Guerrier Dragons (priorité 3)

Chaque règle est un invariant vérifié sur toutes les parties de test, avec un nombre minimal d'observations.

| Mécanique | Résultat |
|---|---|
| Grommash + Profondeurs Sanguines | prouvé : 6 ATQ (Forgeron) → 8 → 14 une fois blessé, 14 dégâts au héros ; au tour suivant, 18 |
| Recharge des lieux | prouvé : refus au tour même et au tour suivant, acceptation ensuite (0 contradiction) |
| Parenté du Wyrm | prouvé : coût 6 si et seulement si un Dragon a été joué au tour précédent |
| Glissedrake | prouvé : coût 4 et condition remplie si et seulement si un autre Dragon est en main |
| Frappe anticipée | cohérent dans tous les cas observés ; **cas discriminant absent** (coût actuel ou de base non tranché) |
| Voyante blessée | prouvé par les dégâts réels : Frappe 5 (3 + 2), Suffusion 4 (2 + 2), mot-clé affiché |
| Cibles du Wyrm | prouvé : n'importe quel personnage, y compris les deux héros ; refus si le plateau est plein |
| Rembobinage et invalidation | prouvé : une recommandation antérieure est invalidée avec la raison « Rembobinage » |

## Mesures produit (priorité 4)

| Mesure | Disponible | Où |
|---|---|---|
| Latence de reconstruction P50/P95 | oui | rapport de validation (« réception → état publié », « log → affichage ») |
| Latence des appels LLM P50/P95 | oui, sans LLM réel pour l'instant | « appel stratégie (LLM) » |
| Temps entre début de tour et premier conseil | oui | « début de tour → premier conseil » et compteur « dans le budget du tour » (75 s) |
| Taux d'actions proposées réellement légales | oui | compteurs « légales » et « illégales ou invalides » |
| Taux d'invalidation pour obsolescence | oui | compteurs par raison sur le tableau des conseils |
| Coût API par partie | oui, si le fournisseur renvoie l'usage | « coût API (USD) », jetons envoyés et reçus |

## Ce qui peut passer en MVP 2 ou 3

- **MVP 2 (enregistrement et rejeu)** : prêt à démarrer. Le journal brut par partie, le rejeu de fichiers et le simulateur à rythme réel existent déjà. Il reste un format horodaté avec les annotations HDT et une interface de rejeu temporisé.
- **MVP 3 (LLM)** : les contrats, la validation, l'invalidation, le garde-fou, le délai d'expiration et les mesures sont prêts. Il reste le fournisseur OpenAI (clé déjà présente sur la machine), avec modèle et effort de raisonnement configurables, puis le fichier de stratégie du deck. **Condition recommandée** : passer d'abord la partie test en direct.

## Procédure de la partie test (5 minutes)

1. Lancer `HsCopilot.App`, puis HDT, puis Hearthstone. Le plugin est déjà installé et activé.
2. Jouer une partie en **Entraînement contre l'IA** avec « Guerrier dragons ».
3. Pendant la partie, comparer à l'œil la main, le plateau, les PV et le mana affichés avec l'écran du jeu.
4. En fin de partie, ouvrir l'onglet « Validation ». Le rapport est aussi enregistré dans `%LocalAppData%\HsCopilot\reports\` (Markdown et JSON).
5. Attendus : verdict sans « ÉCHEC », « Concordance avec HDT au repos » en OK, latence « log → réception » mesurée. Le journal brut de la partie (`%LocalAppData%\HsCopilot\sessions\`) permet de rejouer tout écart avec `hscopilot verify`.
