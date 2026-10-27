# Vérification du MVP 1 : reconstruction d'un état de jeu réel

Objectif du cahier des charges : « Avant d'implémenter tout le système, prouver que l'extracteur peut reconstruire correctement un tour réel. »

> Mis à jour le 25/10/2026 après la validation complémentaire. Synthèse, audit des risques et suite : [RAPPORT-VALIDATION.md](RAPPORT-VALIDATION.md).

## Données utilisées

- 2 replays HDT de parties classées du 25/10/2026 avec le deck « Guerrier dragons » (contre Prêtre et contre Voleur).
- 4 Power.log bruts (sessions du 22, du 24 et du 25/10/2026) : **62 parties distinctes**, dont 59 contre l'IA, 3 classées, et 13 Rembobinages.
- 5 parties anonymisées (BattleTags et identifiants de compte remplacés) servent de fixtures aux tests : `tests/HsCopilot.Core.Tests/Fixtures/`.

## Méthode : des contrôles indépendants du moteur

1. **Oracle des vues client.**
   - À chaque bloc d'actions légales et à chaque choix envoyé, le client imprime pour chaque entité concernée sa zone, sa position, son identifiant de carte et son propriétaire.
   - On vérifie que ce quadruplet est **exactement** un état par lequel le moteur est passé.
   - Le client imprime sa vue animée, qui peut être en retard : on accepte l'état courant, ou un état resté valide jusqu'à 20 révisions avant (retard maximal observé : 13).
   - Limite : les deux côtés viennent du même flux serveur, il ne s'agit pas d'une comparaison avec l'écran.
2. **Oracle des tours.** Le serveur n'envoie des options qu'au joueur dont c'est le tour. À chaque bloc d'options, le moteur doit donc attribuer le tour au joueur local.
3. **Oracle mana/coûts.** Le serveur refuse une carte de la main avec `REQ_ENOUGH_MANA` si son coût dépasse le mana disponible, et l'accepte sinon. On vérifie que le coût affiché reconstruit, réductions comprises, et le mana reconstruit donnent la même conclusion.
4. **Audit de visibilité.** À chaque frontière de paquet, on projette l'état. Toute identité de carte adverse exposée en main, deck ou Secret doit avoir été affichée publiquement auparavant. La preuve est établie **sans le moteur**, à partir des références imprimées par le client et des révélations explicites (`REVEALED=1`).
5. **Références de joueur.** Aucun changement d'état ne doit rester sans destinataire (nom de joueur non associé).
6. **Fin de partie.** Le perdant doit avoir 0 PV ou moins, sauf abandon ou fin scénarisée contre l'IA.
7. **Statistiques HDT.** Résultat, nombre de tours et pièce sont comparés aux enregistrements de HDT (`DeckStats.xml`).
8. **Relecture manuelle** d'un tour, avec retour aux lignes brutes du log.

Commande pour reproduire : `hscopilot verify "C:\Program Files (x86)\Hearthstone\Logs"` (un fichier ou un dossier).

## Résultats

| Contrôle | Volume | Écarts |
|---|---|---|
| Parties distinctes analysées | 62 | 0 partie en échec |
| Vues client comparées à l'état reconstruit | 69 999 | **0** (88 % identiques à l'état courant, retard maximal 13 révisions) |
| Blocs d'options reçus pendant un tour attribué au joueur local | 2 548 | **0** |
| Cartes de la main jugées jouables ou non par le serveur | 11 509 | **0** |
| Emplacements adverses cachés audités | 33 524 | **0 fuite** |
| Lignes non comprises par le parseur | — | **0** |
| Rembobinages appliqués | 13 | 0 incohérence |

Comparaison avec HDT :

| Partie | HDT (`DeckStats.xml`) | Moteur |
|---|---|---|
| Classée contre Prêtre | défaite, 5 tours, pas de pièce | défaite (abandon), tour 5, premier joueur : vous |
| Classée contre Voleur | défaite, 9 tours, pièce | défaite (abandon), tour 9, premier joueur : adversaire, La pièce jouée |

## Tour de référence relu à la main

Partie classée contre Prêtre, tour 3 du joueur local (TURN=5), au moment où le serveur envoie les options. Le test `Reconstructs_real_turn_3_of_priest_game` fige ces valeurs.

| Élément | Valeur reconstruite | Preuve dans le log brut |
|---|---|---|
| Mana | 3/3 | `RESOURCES value=3`, `RESOURCES_USED value=0` |
| Héros local | 30 PV, 2 armure | `ARMOR value=2` (pouvoir héroïque au tour 2) |
| Héros adverse | 40/40 PV | `HEALTH value=40` (effet de début de partie de Briseuse d'âme Azalina, révélée publiquement) |
| Main | Glissedrake visionnaire (4, base 7, condition remplie), Dragonnet du Vol infini (3), Chronoseigneur Époque (6), Forgeron dimensionnel (3), Suffusion d'ombreflamme (2) | 3 cartes de départ + mulligan (Glissedrake gardé, Forgeron et Dragonnet remplacés) + 3 pioches − Profondeurs Sanguines jouées |
| Deck | 24 cartes, 24 restantes connues d'après la liste | 30 − 6 |
| Plateau local | Profondeurs Sanguines, 3 charges, activable | option `error=NONE` ciblant Chevalier floramer |
| Plateau adverse | Chevalier floramer 2/3 | joué au tour adverse précédent |
| Main adverse | 5 cartes cachées, dont une avec l'hypothèse « probablement La pièce » | la Pièce adverse n'est pas identifiée dans le log |
| Dernier tour adverse | Chevalier floramer, encore en jeu | cible de Chronoseigneur Époque |
| Actions légales | Dragonnet, Forgeron, Suffusion (3 cibles), pouvoir héroïque, lieu → Chevalier floramer, fin de tour | bloc `DebugPrintOptions id=9` |

Les mécaniques du deck sont vérifiées comme invariants sur toutes les fixtures : voir [RAPPORT-VALIDATION.md](RAPPORT-VALIDATION.md), section « Tests de référence ».

## Défauts trouvés par ces contrôles, puis corrigés

| Défaut | Comment il a été détecté |
|---|---|
| Tours attribués au mauvais joueur (CURRENT_PLAYER change avant TURN) | relecture du tour de référence : le dernier tour adverse était faux |
| Durabilité d'arme négative (la durabilité est dans HEALTH) | relecture du tour 6 contre Voleur |
| Entités recréées après un Rembobinage gardant leurs anciens tags | avertissement `DUPLICATE_ENTITY_CREATE` sur la fixture Rembobinage |
| Deck restant faux quand une carte change de camp (Exécutrice déguisée « jouable des deux côtés ») | contrôle de cohérence du deck |
| Abandons comptés comme fins incohérentes | contrôle de fin de partie |
| Infos d'une entité révélée en fin de paquet finalisées seulement à la ligne suivante | test synthétique (Quête adverse) |
| Pipe accessible par le chemin réseau `\\127.0.0.1\pipe\…` | test de connexion distante |
| Adversaire IA renommé en cours de partie : ses changements d'état n'étaient jamais appliqués | test de référence de la Parenté |
| Surcharge soustraite deux fois du mana disponible | oracle mana sur le log du 22/10 |

## Chaîne temps réel

| Maillon | Vérifié | Comment |
|---|---|---|
| Plugin chargé par HDT | oui | journal HDT 1.59.7 : « Loading HS Copilot Collector », « Enabled HS Copilot Collector », aucune erreur |
| Plugin → application | oui | connexion, `hello` (HDT 1.59.7.9019), deck actif « Guerrier dragons » (30 cartes), reconnexion automatique |
| DLL du plugin sous .NET Framework, rythme réel, jusqu'à la fenêtre | oui | partie de 231 s rejouée : 8 478 lignes, 0 perdue, log → affichage P95 129 ms, rapport de validation automatique sans écart |
| Repli par lecture du Power.log, rythme réel, jusqu'à la fenêtre | oui | log → affichage P95 143 ms, rapport sans écart |
| Session : plugin, repli sur le log, bascule | oui | tests automatisés (`TransportTests`, `LiveValidationTests`) |
| Pipe inaccessible depuis le réseau | oui | `\\127.0.0.1` et `\\NOM-PC` refusés, `\\.` accepté |
| **Partie jouée dans Hearthstone avec HDT** | **non** | Hearthstone n'a pas été lancé pendant le développement |

La procédure de la partie test et la lecture du rapport automatique sont dans [RAPPORT-VALIDATION.md](RAPPORT-VALIDATION.md).
