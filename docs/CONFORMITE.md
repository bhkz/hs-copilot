# Conformité aux règles Blizzard

Ce document n'est pas un avis juridique. Il rassemble les textes et faits trouvés en octobre 2026, ce que le projet fait pour s'y conformer, et le risque qui reste.

## Ce que disent les textes

**Contrat de licence utilisateur final Blizzard** (révision du 21 mars 2024), section 1.C :

- Triche : interdit de créer ou d'utiliser des « cheats; i.e. methods not expressly authorized by Blizzard ».
- Bots : interdit « any code and/or software, not expressly authorized by Blizzard, that allows the automated control of a Game ».
- Exploration de données : interdit d'utiliser « any unauthorized process or software that intercepts, collects, reads, or "mines" information generated or stored by the Platform ». Le même article précise : « Blizzard may, at its sole and absolute discretion, allow the use of certain third-party user interfaces. »

Lu littéralement, un logiciel non autorisé qui lit des informations générées par le jeu tombe sous cette clause, y compris un deck tracker qui lit les logs. En pratique, ces outils sont tolérés.

**Positions publiques connues** :

- À propos de HearthArena (aide au draft en Arène), Blizzard a déclaré : « Like any third party programs for Hearthstone, we can't condone or approve of their use. Our Terms of Use don't allow changing game files or automating game play. »
- Ben Brode (ancien directeur du jeu) aurait dit qu'un logiciel faisant ce qu'on peut faire avec un papier et un crayon est acceptable. C'est une citation de seconde main, rapportée sur un forum.
- Le source de HDT montre que Blizzard intervient auprès des éditeurs d'outils : « Blizzard has kindly asked us to stop supporting reconnector plugins ». HDT refuse ces plugins et toute DLL important `iphlpapi` ou `lovepapi`.
- **Aucune position officielle** n'a été trouvée sur l'assistance stratégique par IA en temps réel. Recherches effectuées en octobre 2026.

## Ce que le projet fait

| Exigence | Mise en œuvre | Vérification |
|---|---|---|
| Pas d'automatisation | aucun clic, aucune touche simulée ; l'application ne fait qu'afficher | revue du code : aucune API d'entrée |
| Pas de lecture mémoire du jeu | le projet lit uniquement les lignes du Power.log, via l'API de plugins HDT ou directement dans le fichier | revue du code |
| Pas d'injection | le plugin tourne dans HDT, pas dans Hearthstone ; aucune modification du client | revue du code |
| Pas d'interception réseau | aucun accès au trafic du jeu | revue du code |
| Pas d'information cachée | règles de visibilité par provenance, audit automatique, garde-fou sur tout message sortant | 33 524 emplacements adverses audités sur 62 parties, 0 fuite |
| Pas de serveur exposé | named pipe local avec `PIPE_REJECT_REMOTE_CLIENTS` et ACL utilisateur | connexions via `\\127.0.0.1` et `\\NOM-PC` refusées |
| Compatibilité avec les règles HDT | aucune fonction de reconnexion, aucun appel natif dans le plugin | chargé par HDT 1.59.7 sans avertissement |
| Overlay sans interaction avec le jeu (MVP 3B) | fenêtre WPF à part, au premier plan, style WS_EX_NOACTIVATE : elle ne prend jamais le focus et n'envoie rien à Hearthstone | revue du code : aucune API d'entrée, aucun accès à la fenêtre du jeu |
| Raccourcis clavier (MVP 3B) | RegisterHotKey pour Ctrl+Alt+H, R et P : Windows prévient l'application, rien n'est envoyé au jeu | revue du code |

Points de transparence :

- **HDT lui-même** utilise une lecture mémoire (HearthMirror) pour certaines fonctions, comme la détection de deck ou de collection. Le plugin n'utilise pas ces fonctions de jeu ; il lit seulement la liste du deck actif enregistrée dans HDT et la vue d'état de HDT pour un contrôle facultatif. Il ne peut pas garantir comment HDT a obtenu ces données.
- Depuis le MVP 3, l'état observable de la partie est **envoyé à OpenAI** (ou, si tu choisis un modèle `claude-…`, **à Anthropic**, sans option de non-conservation équivalente à `store=false` dans la requête) à chaque analyse, avec `store=false`. Le GameState ne contient ni BattleTag ni identifiant de compte ; le garde-fou anti-fuite bloque tout message qui contiendrait une carte adverse cachée.

## Risque qui reste

- Un deck tracker reste dans la logique « papier et crayon ». Un assistant qui calcule et propose le meilleur coup en temps réel va au-delà : il s'agit d'une aide extérieure pendant la partie.
- Rien n'indique que Blizzard détecte ou sanctionne ce type d'outil. Rien ne l'autorise non plus, et la clause d'exploration de données couvre la lecture des logs par un logiciel non autorisé.
- Les règlements de tournoi interdisent toute aide extérieure.

## Recommandation

- Conseils en temps réel : les utiliser en **Entraînement, contre l'IA, en partie amicale ou libre**.
- **En classé** : privilégier l'analyse après partie (MVP 5) ; utiliser le temps réel seulement en connaissance de cause.
- **Mis en place** dans le coordinateur de stratégie : les conseils en temps réel sont désactivés par défaut en classé (`GT_RANKED`, détecté dans le GameState). Ils se réactivent par un réglage explicite : `advisor.allowRanked`, ou la case « Conseiller aussi en partie classée » de l'onglet « Conseiller ».
- **Décision de l'utilisateur, 25/10/2026** : conseils en temps réel activés en classé sur sa machine (`allowRanked: true` dans `%LocalAppData%\HsCopilot\copilot.json`). Le risque décrit plus haut reste entier : aucune règle de Blizzard n'autorise explicitement une aide de ce type en partie classée.

## Sources

- [Contrat de licence utilisateur final Blizzard](https://www.blizzard.com/en-us/legal/fba4d00f-c7e4-4883-b8b9-1b4500a402ea/blizzard-end-user-license-agreement)
- [Accord anti-triche Blizzard](https://www.blizzard.com/en-gb/legal/cd5930c0-2784-420c-a23d-1e0d6ff8599b/anti-cheating-agreement)
- [PCGamesN : Blizzard au sujet de l'overlay d'Arène HearthArena](https://www.pcgamesn.com/hearthstone-heroes-of-warcraft/hearthstone-arena-overlay-blizzard-say-we-can-t-condone-or-approve-of-their-use)
- [HearthPwn : discussion « Is deck tracking legal? » (citation de Ben Brode)](https://www.hearthpwn.com/forums/hearthstone-general/general-discussion/108323-is-deck-tracking-legal)
- [Code source HDT, PluginManager.cs (refus des plugins de reconnexion)](https://github.com/HearthSim/Hearthstone-Deck-Tracker/blob/master/Hearthstone%20Deck%20Tracker/Plugins/PluginManager.cs)
- [Wiki HDT : Creating Plugins](https://github.com/HearthSim/Hearthstone-Deck-Tracker/wiki/Creating-Plugins)
