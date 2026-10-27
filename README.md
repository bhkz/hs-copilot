# HS Copilot : assistant stratégique Hearthstone (prototype)

Objectif final : un assistant temps réel pour mieux jouer le **Guerrier Dragons**. Il détecte les événements de partie sans saisie, reconstruit un état vérifiable, interroge un LLM et affiche au plus trois actions dans un overlay discret. Le joueur exécute toujours lui-même ses actions.

**État au 25/10/2026.** MVP 1 livré et validé sur 62 parties réelles. Une **première vraie partie** a été jouée avec le copilote le 25/10 : la chaîne en direct a fonctionné jusqu'à un Rembobinage, puis un renvoi de HDT a faussé l'état. Ce défaut est corrigé et testé, mais doit encore être confirmé par une nouvelle partie ([docs/CORRECTIFS-PARTIE-REELLE.md](docs/CORRECTIFS-PARTIE-REELLE.md)). Une **deuxième vraie partie**, sans Rembobinage effectif, a montré des conseils légaux et une pioche toujours prise en compte, mais une erreur d'ordre (Wyrm attaqué avant Profondeurs) et un quota épuisé en fin de tour ; un solveur de létal déterministe et un nouveau décompte du quota les corrigent ([docs/ANALYSE-PARTIE-2.md](docs/ANALYSE-PARTIE-2.md)). **MVP 3A** : conseiller OpenAI évalué hors ligne sur tes parties ([docs/MVP3A-RESULTATS.md](docs/MVP3A-RESULTATS.md)). **MVP 3B** : premier copilote en direct, avec overlay, déclenchement automatique et plafonds de coût, mesuré sur des parties rejouées à leur rythme réel ([docs/MVP3B-COPILOTE.md](docs/MVP3B-COPILOTE.md)). Procédure de test dans [docs/TEST-EN-DIRECT.md](docs/TEST-EN-DIRECT.md).

## Ce qui existe

- **Plugin HDT** (`HsCopilot.Collector`) : relaie les lignes brutes du Power.log lues par Hearthstone Deck Tracker, le deck actif et une vue de contrôle, par un named pipe local refusé au réseau.
- **Repli** sans HDT : lecture directe du Power.log. Deux sources ne sont jamais mélangées.
- **Moteur d'état** : reconstruit la partie (héros, mana, plateau, main, deck restant, Secrets, historique, Rembobinage…) et publie un **GameState versionné**, limité à l'observable.
- **Fenêtre de diagnostic** (WPF, en français) : état en direct, actions légales du serveur, historique, onglet **Validation** (rapport de la partie en cours), contrôle croisé HDT, JSON, journal.
- **Rapport de validation automatique** pour chaque partie, dans `%LocalAppData%\HsCopilot\reports\` : oracles, transitions, latences P50/P95.
- **Simulation** : rejeu d'un `.hdtreplay` ou d'un Power.log tour par tour dans la fenêtre, ou « comme en direct » à son rythme d'origine (`hscopilot simulate`).
- **Contrats du Strategy Engine** : actions légales identifiées, recommandation liée à partie + révision + action, invalidation, garde-fou anti-fuite, coordinateur avec délai d'expiration. Les conseils en temps réel sont désactivés par défaut en classé ; une case de l'onglet « Conseiller » les active, ce que tu as choisi.
- **Conseiller OpenAI** (MVP 3A) : Responses API, sortie JSON stricte, modèle et effort de raisonnement réglables (`HSCOPILOT_OPENAI_MODEL`, `HSCOPILOT_OPENAI_EFFORT`), clé lue dans `OPENAI_API_KEY`, fichier de stratégie `config/strategy/guerrier-dragons.md`.
- **Conseiller Claude, au choix** (26/10/2026) : un modèle `claude-…` choisi dans l'onglet Conseiller (`claude-opus-5-5`, `claude-sonnet-5-5`, `claude-haiku-5-5`) passe par l'API Anthropic (Messages API), avec le même message, le même schéma de sortie, la même validation et les mêmes budgets. La clé est lue dans `ANTHROPIC_API_KEY` (`setx ANTHROPIC_API_KEY "ta-clé"`), distincte d'un abonnement Claude. Ce n'est pas un équivalent mesuré de gpt-6.1-sol : la qualité et la latence sur tes positions restent à mesurer.
- **Banc d'essai hors ligne** : points de décision extraits de tes logs, coup réellement joué gardé comme étiquette, variantes comparées, détecteur de coups douteux, latence, jetons et coût par décision, reprise et plafond de dépense.
- **Fenêtre d'examen** des décisions une par une, avec annotations Bon / Discutable / Mauvais.
- **Copilote en direct** (MVP 3B) : overlay toujours visible qui ne prend jamais le focus du jeu. Il affiche l'action immédiate, vérifiée par le serveur, puis le plan, la raison et au plus deux étapes « non vérifiées ». Il conseille aussi le mulligan, les Découvertes et les Rembobinages. Le déclenchement est automatique, avec attente adaptative, plafonds d'appels et de coût, pause et réanalyse au clavier. L'action s'affiche dès qu'elle arrive dans la réponse en flux, et le létal est étiqueté visible, conditionnel ou plausible.
- **Onglet « Conseiller »** : état de la clé, test de connexion, modèle, effort, plafonds, détail complet du dernier conseil, journal. Un **mode démo** gratuit, avec latence simulée et sans stratégie, permet d'essayer l'overlay.

## Ce qui a été vérifié

Détails : [docs/RAPPORT-VALIDATION.md](docs/RAPPORT-VALIDATION.md) et [docs/VERIFICATION-MVP1.md](docs/VERIFICATION-MVP1.md).

| Contrôle sur 62 parties réelles | Volume | Écarts |
|---|---|---|
| Vues imprimées par le client vs état reconstruit | 69 999 | 0 |
| Blocs d'options reçus pendant un tour attribué au joueur local | 2 548 | 0 |
| Coût et mana reconstruits vs légalité serveur | 11 509 | 0 |
| Emplacements adverses cachés audités | 33 524 | 0 fuite |

| Chaîne temps réel, rythme réel, jusqu'à la fenêtre | log → affichage P50 | P95 |
|---|---|---|
| Plugin réel (DLL sous .NET Framework) | 28 ms | 129 ms |
| Repli par lecture du Power.log | 92 ms | 143 ms |

- **Non vérifié** : une partie jouée dans Hearthstone avec HDT ouvert. La procédure de 5 minutes est à la fin du rapport de validation.
- Les mécaniques du deck (Grommash + Profondeurs, recharge des lieux, Parenté, Glissedrake, Voyante, Wyrm) sont vérifiées comme invariants sur tes parties. Seule la condition exacte de Frappe anticipée reste non tranchée.

## Lancer le copilote

```powershell
powershell -ExecutionPolicy Bypass -File tools\lancer-copilote.ps1
```

Le script ferme proprement une instance déjà ouverte, recompile si le code a changé, vérifie la clé OpenAI et le plugin HDT, puis ouvre l'application et l'overlay. Lance ensuite HDT, puis Hearthstone en mode fenêtré sans bordure, et joue une partie d'entraînement contre l'IA. Procédure complète et rapport attendu : [docs/TEST-EN-DIRECT.md](docs/TEST-EN-DIRECT.md).

## Démarrage rapide

### Prérequis

- Windows 10/11, **SDK .NET 10** (`dotnet --version`). Visual Studio n'est pas nécessaire.
- **Hearthstone Deck Tracker** installé (testé avec 1.59.6 et 1.59.7). Il est requis pour compiler le plugin et recommandé pour le direct.
- Dans `%LocalAppData%\Blizzard\Hearthstone\log.config`, la section `[Power]` doit avoir `FilePrinting=True` et `Verbose=True` (HDT la crée).

### 1. Compiler et tester

```powershell
cd C:\Users\thoma\Desktop\HSguide
dotnet build HsCopilot.sln -c Release
dotnet test HsCopilot.sln -c Release
```

Le projet du plugin référence les DLL de HDT dans `%LocalAppData%\HearthstoneDeckTracker\current`. Sans HDT, compilez seulement les autres projets (`dotnet build src\HsCopilot.App` et `dotnet test tests\HsCopilot.Core.Tests`).

### 2. Installer le plugin HDT

HDT doit être fermé.

```powershell
powershell -ExecutionPolicy Bypass -File tools\install-plugin.ps1 -Enable
```

Le script compile le plugin, le copie dans `%AppData%\HearthstoneDeckTracker\Plugins\HsCopilot\` et l'active dans `plugins.xml`. Il garde une sauvegarde de l'état d'origine (`plugins.xml.hscopilot-backup`). Sans `-Enable`, cochez « HS Copilot Collector » dans HDT, sous Options > Tracker > Plugins. Pour désinstaller : `tools\uninstall-plugin.ps1`.

> Le plugin est déjà installé et activé sur cette machine.

### 3. Configurer (facultatif)

```powershell
New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\HsCopilot" | Out-Null
Copy-Item config\copilot.example.json "$env:LOCALAPPDATA\HsCopilot\copilot.json"
```

Le fichier d'exemple contient ta liste « Guerrier dragons ». Réglages principaux : `source` (`auto`, `hdt`, `log`, `none`), `hearthstoneLogsDir`, `cardDbPath`, `journalEnabled`, `reportsEnabled`, `reportsDir`, `decks`, `activeDeck`.

### 4. Lancer en direct

```powershell
src\HsCopilot.App\bin\Release\net10.0-windows\HsCopilot.App.exe
```

- **Ordre conseillé** : HsCopilot.App, puis HDT, puis Hearthstone.
- **Source active** : l'en-tête indique « Plugin HDT connecté », « Lecture directe du Power.log (repli) » ou « En attente ».
- **Mode `auto`** : sans plugin au bout de 5 s, l'application lit le Power.log elle-même. Dès que le plugin se connecte, elle bascule sur lui.
- **Fin de partie** : le rapport de validation s'affiche dans l'onglet « Validation » et il est enregistré automatiquement.

### 5. Rejouer une partie hors ligne

Bouton « Ouvrir un replay… » (les replays HDT sont dans `%AppData%\HearthstoneDeckTracker\Replays`), ou :

```powershell
src\HsCopilot.App\bin\Release\net10.0-windows\HsCopilot.App.exe --replay "tests\HsCopilot.Core.Tests\Fixtures\ranked-warrior-vs-priest.log" --turn 3 --source none
```

Options : `--config <fichier>`, `--source auto|hdt|log|none`, `--replay <fichier>`, `--turn <N>`.

## Ligne de commande

`src\HsCopilot.Cli\bin\Release\net10.0-windows\hscopilot.exe` :

```text
hscopilot games  <fichier>                         liste les parties d'un Power.log ou .hdtreplay
hscopilot state  <fichier> --turn 3 [--json]       état au début de décision du tour 3 du joueur local
hscopilot verify <fichier|dossier>                 oracles (vues client, tours, mana), visibilité, fin de partie
hscopilot simulate <fichier> [--via pipe|log] [--speed 1] [--max-wait 3] [--with-session] [--strategy diagnostic]
                                                   rejoue une partie comme en direct, avec rapport et mesures
hscopilot probe <fichiers>                         relève dans des logs les faits de mécanique du deck
hscopilot anonymize <entrée> <sortie>              retire BattleTags et identifiants de compte
hscopilot carddb [ID ...]                          base de cartes (build, nombre, fiches)
hscopilot live [--source auto|hdt|log]             écoute en continu sans interface
hscopilot bench build <logs|dossier>               extrait les points de décision et ton coup réel
hscopilot bench run --out <dossier> [--limit 36] [--variants gpt-6.1-sol:low,gpt-6.1-sol:medium] [--budget 8]
                                                   interroge chaque variante, reprend après interruption
hscopilot bench report|show <dossier>              synthèse, ou un cas en détail
hscopilot bench compare <dA> <vA> <dB> <vB>        compare deux variantes sur leurs cas communs
hscopilot provider test [--model M] [--effort E]   un appel réel avec latence, jetons et coût
hscopilot provider ping                            test de connexion minimal
hscopilot simulate <fichier> --with-session --strategy openai|simulated [--effort low] [--budget-game 0.6]
                                                   rejoue une partie à son rythme réel avec le conseiller branché
```

Exemples :

```powershell
# Après un patch Hearthstone : revérifier toutes les parties enregistrées
hscopilot verify "C:\Program Files (x86)\Hearthstone\Logs"

# Mesurer toute la chaîne sans Hearthstone (session interne, rapport en sortie)
hscopilot simulate "tests\HsCopilot.Core.Tests\Fixtures\ranked-warrior-vs-rogue.log" --via pipe --speed 1 --max-wait 3 --with-session

# Banc d'essai du conseiller, puis examen des décisions une par une
hscopilot bench build "C:\Program Files (x86)\Hearthstone\Logs" --out bench\cases.jsonl
hscopilot bench run --cases bench\cases.jsonl --limit 36 --variants "gpt-6.1-sol:low,gpt-6.1-sol:medium" --budget 3 --out bench\runs\essai
HsCopilot.App.exe --bench bench\runs\essai
```

## Tests

Il y a 271 tests xUnit dans `tests/HsCopilot.Core.Tests`, sur 12 parties réelles anonymisées et des flux synthétiques :

- **Parsing et base de cartes** : formats de lignes, références d'entités, CardDefs XML, HearthstoneJSON, textes.
- **Parties réelles** : oracles, visibilité, métadonnées comparées à HDT, tour de référence, mulligan, pioche, améliorations, armes, attaques, déclencheurs, Provocation, Rembobinage.
- **Guerrier Dragons** : Grommash + Profondeurs (14 puis 18), recharge des lieux, Parenté, Glissedrake, Frappe anticipée, Voyante blessée, cibles du Wyrm. Chaque règle est vérifiée sur toutes les parties.
- **Strategy Engine** : actions légales identifiées, rejet des actions inventées, liaison à la révision, certitude interdite, 20 mots, étapes non vérifiées, invalidation (action, fin de tour, Rembobinage), état modifié pendant une requête, délai dépassé, non-blocage, classé désactivé, garde-fou anti-fuite.
- **Robustesse et non-régression** : Secret adverse, fuite via reset, joueur local inconnu, partie rejointe en cours, lignes perdues ou permutées, relecture en double, IA renommée, Surcharge, ligne `ReportStuck`.
- **Transport et direct** : lecture du log, pipe, refus des clients distants, bascule log → plugin, session complète avec rapport de validation et mesures.
- **Banc d'essai (MVP 3A)** : état d'un cas reconstruit uniquement à partir des lignes antérieures, coup réel absent du message, aller-retour JSON, sélection déterministe, messages sans carte cachée, détecteur (létal ignoré avec ou sans carte de la main, échange perdant), fournisseur OpenAI simulé (usage, coût, nouvelle tentative, réponse incomplète, clé absente du corps), reprise, plafond de dépense, synthèse, annotations.
- **Copilote en direct (MVP 3B)** : contextes de mulligan et de choix, validation des cartes à remplacer, niveaux de létal et plafonnement de celui annoncé par le modèle, plafonds par tour, de coût et de session, pause, réanalyse manuelle, fin de tour seule, options renvoyées à l'identique, attente adaptative, annulation dès que la décision est passée, coût estimé des appels annulés, affichage progressif sans action inventée, conseil retiré jamais montré comme valable, classé désactivé par défaut et activable, flux SSE simulé, test de connexion, réglages enregistrés.
- **Première vraie partie** : renvoi de la partie par HDT après un Rembobinage, filtré et sans effet sur l'état, rejoué sur la partie réelle anonymisée ; aucune analyse pendant un rattrapage ; priorité au début de tour et aux choix ; remboursement du quota des appels annulés en option ; mise en garde locale avant une fin de tour ; trace des conseils.
- **Synchronisation avec l'écran** : partie réelle avec ses deux flux (GameState et PowerTaskList) ; lignes PowerTaskList jamais transmises au moteur ; tour 4 affiché 10,1 s après son début côté serveur ; conseil et létal local retenus « TON TOUR ARRIVE » puis « ACTION IMMÉDIATE » sans nouvel appel ; appels identiques avec ou sans horloge ; La pièce jamais remplacée au mulligan.
- **Sombres dons, deck et tours suivants** : Découverte réelle à trois dons différents (don lu dans le log, résultat calculé, libellé, message), Beaux rêves et dessus du deck connu, probabilités de pioche exactes ou estimées, code de deck identique à la liste HDT, Parenté et recharge de Profondeurs au tour suivant, prudence du solveur (Secret connu, effet adverse qui empêche les dégâts, Provocation à Râle d'agonie, Drake réduit au silence, Charge par don), résultat du mulligan.
- **Deuxième vraie partie** : solveur de létal sur les états exacts (Profondeurs avant le Wyrm au tour 7, létal conditionnel par l'arme aléatoire puis certain au tour 8, Drake terrestre en fin de tour, Secret) ; mise en garde d'ordre ; létal démontré affiché sans appel ; suite du plan sans appel et refusée si une carte arrive ; début de tour et choix hors quota ; titres « ANALYSES AUTO ÉPUISÉES » ; `localAnalysis` et `previousAdvice` dans le message ; diagnostics de synchronisation ; ancien et nouveau format des étapes.
- **Danger adverse** : défaites réelles contre Rexxar et Tyrande (anonymisées) ; 25 dégâts visibles contre 25 PV, pouvoir héroïque compris ; défense proposée avec son coût ; alerte maintenue après la mort du Parle-esprit ; alerte gardée à l'écran avec le début de réponse, le conseil et la suite du plan ; Nozdormu l’Intemporel signalé (10 cristaux à un adversaire à 8 cartes) sans fausse alerte ; fatigue adverse ; « létal plausible » derrière des Provocations ; variantes `+local` et `+danger` du banc d'essai.
- **Fournisseur Claude** : requête (effort, même schéma, consignes en cache), flux avec action dès qu'elle est lisible et réflexion ignorée, coût avec lectures du cache, surcharge retentée une fois, clé refusée, réponse tronquée, choix du fournisseur selon le modèle.
- **Surcharge d'OpenAI** : « Our servers are currently overloaded » reçu dans le flux avant tout texte → une seule nouvelle tentative, puis un message clair ; jamais de nouvelle tentative après un début de conseil ni sur un crédit épuisé.
- **Danger après un coup (hors ligne)** : la marge de départ est celle de l'alerte sur toutes les fixtures ; chaque coup simulé « complet » redonne l'état réel suivant du log ; contre Rexxar, l'attaque au visage fait passer la meilleure défense de 9 à 2 (partielle à cause du Secret) et finir le tour la perd ; Râle d'agonie et Nozdormu jamais déclarés sûrs ; un létal démontré n'est jamais signalé.

## Documentation

| Document | Contenu |
|---|---|
| [docs/DANGER-APRES-LIGNE.md](docs/DANGER-APRES-LIGNE.md) | danger au prochain tour avant et après chaque coup conseillé ou joué, hors ligne : simulation validée sur l'état réel suivant, aucun conseil qui perd la dernière défense sur 1 056, contre-exemples, revue des défaites, coût du calcul |
| [docs/SYNTHESE-CONTRE-EXPERTISE.md](docs/SYNTHESE-CONTRE-EXPERTISE.md) | contre-expertise indépendante de l'étude : affirmations réfutées et corrigées, confrontation des agents, désaccords non résolus, prochaines marges, comparaison avec la facturation OpenAI |
| [docs/ETUDE-STRATEGIQUE.md](docs/ETUDE-STRATEGIQUE.md) | étude du plafond : 7 dimensions mesurées, 5 perspectives (dont une simulée), recherche sourcée, banc d'essai à budget plafonné, alerte de danger corrigée, conclusion « optimisations ciblées » |
| [docs/DIAGNOSTIC-DEFAITES.md](docs/DIAGNOSTIC-DEFAITES.md) | défaites et abandons reconstitués coup par coup, erreurs identifiables, proposition d’un module local de danger adverse, comptabilité réel / estimé / budget |
| [docs/BILAN-PVP-3.md](docs/BILAN-PVP-3.md) | nuit du 25 au 26/10 : 27 parties classées (Libre puis Standard), défaites analysées, létal « plausible » derrière des Provocations corrigé, budget de session atteint |
| [docs/BILAN-PVP-2.md](docs/BILAN-PVP-2.md) | huit parties classées, audit des statistiques (reconstruction, transmission au modèle, affichage), fausse alerte de confidentialité corrigée, Rembobinage et appel conservé observés en direct |
| [docs/CONTINUITE-DU-PLAN.md](docs/CONTINUITE-DU-PLAN.md) | La pièce et coups préparatoires : appel conservé quand tu joues le début de réponse, changement attendu ou imprévu, mesure sur les parties classées, tests |
| [docs/BILAN-PVP-1.md](docs/BILAN-PVP-1.md) | trois premières parties classées : Guerrière à 28 ATQ (pas de létal), Sombres dons et mulligans en direct, synchronisation, temps de réponse et appels annulés, corrections, limites |
| [docs/SYNCHRO-AFFICHAGE.md](docs/SYNCHRO-AFFICHAGE.md) | conseils et écran : chronologie de la partie de 18 h 34, décalage serveur → écran mesuré (9,9 s contre l'IA), horloge PowerTaskList, avant/après |
| [docs/SOMBRES-DONS-ET-DECK.md](docs/SOMBRES-DONS-ET-DECK.md) | Sombres dons, deck entier, pioche, tours suivants : audit, preuves des logs et des messages, corrections, avant/après sur de vraies décisions |
| [docs/STRATEGIE-GUERRIER-DRAGONS.md](docs/STRATEGIE-GUERRIER-DRAGONS.md) | synthèse stratégique sourcée du deck : sources datées, mulligan, combos, lieux, dons, plans sur plusieurs tours |
| [docs/ANALYSE-PARTIE-2.md](docs/ANALYSE-PARTIE-2.md) | deuxième vraie partie : cohérence des conseils, cas Profondeurs/Wyrm, létal du tour 8, quota, solveur de létal, avant/après |
| [docs/CORRECTIFS-PARTIE-REELLE.md](docs/CORRECTIFS-PARTIE-REELLE.md) | première vraie partie : ce qui s'est passé, causes, corrections, mesures avant/après |
| [docs/TEST-EN-DIRECT.md](docs/TEST-EN-DIRECT.md) | procédure pas à pas de la partie test contre l'IA, rapport à récupérer, dépannage |
| [docs/MVP3B-COPILOTE.md](docs/MVP3B-COPILOTE.md) | copilote en direct : fonctionnement, déclenchement, overlay, mesures sur replays et simulations, fonctions incomplètes |
| [docs/MVP3A-RESULTATS.md](docs/MVP3A-RESULTATS.md) | banc d'essai du conseiller : protocole, résultats mesurés, effort de raisonnement, exemples de coups discutables, limites |
| [docs/RAPPORT-VALIDATION.md](docs/RAPPORT-VALIDATION.md) | ce qui est prouvé, incertain, audit des risques, mesures, procédure de la partie test |
| [docs/VERIFICATION-MVP1.md](docs/VERIFICATION-MVP1.md) | méthode et résultats de la reconstruction, tour de référence, défauts corrigés |
| [docs/ETUDE-TECHNIQUE.md](docs/ETUDE-TECHNIQUE.md) | environnement, API HDT, format du Power.log, Rembobinage, base de cartes |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | composants, source unique, protocole, moteur, visibilité, Strategy Engine, mesures |
| [docs/GAMESTATE.md](docs/GAMESTATE.md) | format GameState v1 et indicateurs d'incertitude ; exemple réel dans `docs/exemples/` |
| [docs/strategy-output.schema.json](docs/strategy-output.schema.json) | réponse attendue d'un fournisseur de stratégie (LLM) |
| [docs/CONFORMITE.md](docs/CONFORMITE.md) | règles Blizzard, mesures, risque restant, recommandation |
| [docs/CARTES-GUERRIER-DRAGONS.md](docs/CARTES-GUERRIER-DRAGONS.md) | textes actuels du deck, mécaniques observées dans tes parties, points incertains |

## Limitations et incertitudes

- **Direct non observé** dans Hearthstone : la chaîne est validée en simulation fidèle, pas encore pendant une vraie partie.
- **Latence avant notre code** (écriture du log par Hearthstone, lecture par HDT) : non mesurée tant qu'aucune partie réelle n'a été jouée.
- **Avance sur l'écran** : l'état suit les données serveur, qui arrivent avant les animations. Il peut avoir quelques secondes d'avance sur l'écran ; c'est voulu pour la latence.
- **Actions légales** : seules celles envoyées par le serveur pour la situation actuelle sont connues. Aucune séquence de plusieurs actions n'est vérifiée (Rules Engine : MVP 5).
- **Copilote jamais testé pendant une vraie partie** : mesuré seulement sur des parties rejouées à leur rythme réel. Aucun conseil n'est prouvé optimal ; seule l'action immédiate est garantie légale.
- **Latence** : sur une partie rejouée, l'action s'affiche en médiane 4,7 s après la décision et le conseil complet en 9,3 s, en effort low. C'est souvent plus long que le temps que tu prends en milieu de tour : la moitié des appels ont été annulés. Voir [docs/MVP3B-COPILOTE.md](docs/MVP3B-COPILOTE.md).
- **Overlay** : invisible si Hearthstone est en plein écran exclusif ; utilise le mode fenêtré sans bordure.
- **Partie rejointe en cours** sans CREATE_GAME : l'état est marqué incomplet, et les entités joueurs sont supposées être 2 et 3.
- **Frappe anticipée** : la condition porte-t-elle sur le coût actuel ou de base ? Aucune partie ne contient le cas qui trancherait.
- **Modes non étudiés** : Champs de bataille, Arène, Mercenaires, spectateur.
- **Patchs** : les lignes inconnues sont signalées (`UNPARSED_LINES`), jamais fatales ; relancer `hscopilot verify` sur le dossier Logs après chaque patch.

## Arborescence

```text
src/Shared/PipeProtocol.cs     protocole commun plugin/application
src/HsCopilot.Collector/       plugin HDT (net472)
src/HsCopilot.Core/            moteur, cartes, GameState, transports, session, validation, mesures, Strategy (contrats, OpenAI), Evaluation (banc d'essai)
src/HsCopilot.App/             fenêtre de diagnostic WPF et fenêtre d'examen du banc d'essai
src/HsCopilot.Cli/             outil en ligne de commande
tests/HsCopilot.Core.Tests/    tests et fixtures (parties réelles anonymisées)
config/copilot.example.json    configuration d'exemple
config/strategy/               repères stratégiques Guerrier Dragons transmis au modèle
bench/                         cas extraits de tes logs et résultats des exécutions (données personnelles)
tools/                         installation et désinstallation du plugin
docs/                          documentation
```

## Prochaines étapes

1. **Partie test en direct contre l'IA**, avec le copilote : [docs/TEST-EN-DIRECT.md](docs/TEST-EN-DIRECT.md). Elle valide le MVP 1 et teste l'utilité d'un conseil qui arrive après 5 à 10 secondes.
2. **MVP 2** : format d'enregistrement horodaté avec annotations HDT, rejeu temporisé dans la fenêtre.
3. **MVP 3A, à toi** : annoter des décisions dans la fenêtre d'examen. Ton jugement est la seule vraie référence de qualité.
4. **Après la partie test** : réduire la latence en allégeant le message et en n'analysant qu'au bon moment, puis améliorer la qualité selon tes annotations et ton retour.
5. **MVP 4** : overlay complet, avec modes Express et Coach et réglages visuels. Le MVP 3B en fournit une première version minimale.
6. **MVP 5** : Rules Engine (vérification de séquences) et analyse après partie.
