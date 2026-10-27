# Format GameState v1 (`hscopilot.gamestate/1`)

C'est l'objet que produit `GameStateProjector`, qu'affiche la fenêtre de diagnostic et que recevra le Strategy Engine au MVP 3.
Les classes C# sont dans `src/HsCopilot.Core/State/GameStateSnapshot.cs`. Le JSON est en camelCase, sans valeurs nulles.

Exemple complet réel : [exemples/gamestate-pretre-tour3.json](exemples/gamestate-pretre-tour3.json). Il correspond à la partie classée contre Prêtre, tour 3 du joueur local, au moment où le serveur envoie les actions légales.

## Règles

1. **Seulement de l'observable.** Aucune carte cachée, aucun tag de carte cachée adverse. Les hypothèses vont dans des champs dédiés (`hypothesis`, `hypotheses`), jamais dans `cardId`.
2. **Identifiants de cartes** (`cardId`, par ex. `END_033`) partout ; les noms français ne sont qu'un affichage.
3. **Version** : `gameId` + `revision`. La révision est strictement croissante ; une recommandation future devra citer les deux et être invalidée si l'un change.
4. **Fraîcheur** : `updatedAtRevision` sur les héros, serviteurs et cartes de main ; `legalOptions.revision` et `legalOptions.fresh` ; `lastLogTime` et `linesProcessed`.
5. **Incertitude** : `uncertainty.flags` (code, gravité, message français, nombre) et `uncertainty.incomplete`.

## Champs principaux

| Champ | Contenu |
|---|---|
| `gameId`, `revision`, `linesProcessed`, `lastLogTime`, `openBlocks` | identité et position dans le flux ; `openBlocks > 0` signifie qu'une action est en cours de résolution |
| `source` | transport (`hdt-plugin`, `log-tail`, `replay-file`), build client, build et taille de la base de cartes |
| `game` | type et format de partie, `gameTurn` (tag TURN), `turnNumber` = (TURN + 1) / 2, étape, joueur actif, premier joueur, mulligan terminé, fin de partie, résultat, abandon, nombre de Rembobinages |
| `local` / `opponent` | `heroClass`, `hero` (PV, max, armure, attaque, gel…), `heroPower` (coût, utilisé), `weapon` (attaque, durabilité), `mana` : disponible = RESOURCES + TEMP_RESOURCES − RESOURCES_USED (les cristaux verrouillés par la Surcharge sont déjà comptés dans RESOURCES_USED), max, utilisé, temporaire, surcharge |
| `board[]` | serviteurs **et lieux** dans l'ordre : `cardId`, `entityId`, `position`, stats actuelles et de base, races, mots-clés actifs (`TAUNT`, `SPELLPOWER+2`…), enchantements, `state` (épuisé, attaques ce tour, gel, sommeil, recharge du lieu, `serverAllowsAction`) |
| `hand` | `count` + cartes : pour le joueur local, coût affiché/de base, `costModified`, stats, `poweredUp` (condition remplie selon le client), `created`, `createdByCardId`, enchantements ; pour l'adversaire, position, tour d'arrivée en main et `knowledge` (`hidden`, `public-seen`, `public-seen-stale`) |
| `deck` | nombre ; si la liste est connue (HDT ou configuration) : `knownRemaining` = liste − cartes d'origine sorties du deck, `knownAdded`, `unknownCount`, `warning` en cas d'incohérence |
| `secrets[]` | Secrets (adverses : `cardId` null tant qu'ils ne sont pas révélés, classe connue) ; `publicSecretZone[]` pour les Quêtes, Sigils et Objectifs |
| `graveyard[]`, `revealedCards[]` | cartes vues publiquement |
| `thisTurn`, `lastTurn` | cartes **jouées depuis la main** ce tour et au dernier tour terminé, avec types joués (Parenté) et, pour les serviteurs, s'ils sont encore en jeu (Chronoseigneur Époque) |
| `legalOptions` | actions acceptées par le serveur maintenant : `kind` (`play`, `attack`, `hero-power`, `location`), carte, cibles valides, sous-options ; `canEndTurn` ; `fresh` |
| `pendingChoice` | choix en attente du joueur local (Découverte, Rembobinage…) avec les cartes proposées |
| `history[]` | actions chronologiques : début de tour, mulligan (cartes gardées et remplacées pour le joueur local, nombres seulement pour l'adversaire), carte jouée, attaque, pouvoir héroïque, lieu, déclenchement, morts, choix, Rembobinage (`rewound` sur les actions annulées), fin de partie |

## Indicateurs d'incertitude

| Code | Gravité | Signification |
|---|---|---|
| `NO_GAME` | critique | aucune partie dans le flux |
| `JOINED_MID_GAME` | critique | début de partie manquant |
| `LOCAL_PLAYER_UNKNOWN` | critique | joueur local non identifié ; tout est traité comme adverse |
| `CARD_DB_EMPTY` | critique | pas de base de cartes |
| `PARSER_EXCEPTION`, `LOCAL_PLAYER_CONFLICT`, `SOURCE_GAP` | critique | erreur interne, preuves contradictoires, lignes perdues par le plugin |
| `LOCAL_PLAYER_HEURISTIC` | info | joueur local déduit, pas encore confirmé par le serveur |
| `PLAYER_ALIAS_INFERRED` | info | un nouveau nom de joueur a été attribué à l'adversaire par élimination (l'IA change de nom en cours de partie) |
| `PENDING_PLAYER_NAME` | avertissement | des changements d'état citent un nom de joueur non associé : tours et mana possiblement faux |
| `OPTIONS_STALE` | info | actions légales datant d'une révision antérieure, pendant notre tour |
| `GAME_RESET_APPLIED` | info | Rembobinage appliqué depuis l'instantané serveur |
| `DECKLIST_UNKNOWN` | info | liste du deck inconnue |
| `DECKLIST_MISMATCH` | avertissement | liste incohérente avec les cartes vues |
| `UNKNOWN_CARD_IDS` | avertissement | identifiants absents de la base (patch plus récent ?) |
| `UNPARSED_LINES`, `UNKNOWN_ENTITY_REFERENCED`, `BLOCK_END_WITHOUT_START`, `CHOSEN_WITHOUT_CHOICE`, `UNRESOLVED_PLAYER_NAME`, `DUPLICATE_ENTITY_CREATE`, `RESET_OUTSIDE_RESET_BLOCK` | avertissement | flux inattendu, incomplet ou désordonné |
| `HDT_MISMATCH` | avertissement | écart avec la vue de HDT, relevé au repos |

## Extraits réels

Tour 3 contre Prêtre (révision 22). Les champs `text` et les listes vides sont omis ici.

```json
"game": { "gameType": "GT_RANKED", "formatType": "FT_WILD", "gameTurn": 5, "turnNumber": 3,
          "step": "MAIN_ACTION", "activePlayer": "local", "firstPlayer": "local", "mulliganDone": true },
"local": {
  "mana": { "available": 3, "max": 3, "used": 0, "temporary": 0, "overloadLocked": 0, "overloadOwed": 0 },
  "hand": { "count": 5, "cards": [
    { "entityId": 38, "position": 1, "cardId": "END_033", "name": "Glissedrake visionnaire", "type": "MINION",
      "cost": 4, "baseCost": 7, "costModified": true, "attack": 5, "health": 8, "poweredUp": true,
      "enteredTurn": 1, "created": false, "knowledge": "own", "updatedAtRevision": 21 }, "…" ] }
},
"opponent": {
  "hand": { "count": 5, "cards": [
    { "entityId": 79, "position": 3, "enteredTurn": 1, "knowledge": "hidden",
      "hypothesis": "probablement La pièce (règle : le second joueur la reçoit en début de partie)" }, "…" ] },
  "lastTurn": { "gameTurn": 4, "minionsPlayed": [
    { "entityId": 21, "cardId": "EDR_852", "name": "Chevalier floramer", "note": "encore en jeu" } ] }
},
"legalOptions": { "optionsId": 9, "revision": 22, "fresh": true, "canEndTurn": true, "totalOptions": 12,
  "playable": [ { "index": 5, "kind": "location", "entityId": 42, "cardId": "CORE_REV_990",
                  "name": "Profondeurs Sanguines",
                  "targets": [ { "entityId": 21, "cardId": "EDR_852", "name": "Chevalier floramer", "side": "opponent" } ] }, "…" ] },
"uncertainty": { "incomplete": false, "localPlayerEvidence": "SentByClient", "flags": [] }
```
