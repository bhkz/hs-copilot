# MVP 3B : premier copilote en direct

État au 25/10/2026. Le copilote est construit, testé et mesuré sur une partie contre l'IA rejouée à son rythme réel, avec de vrais appels à OpenAI. Une **première vraie partie** a ensuite été jouée : ses défauts et leurs corrections sont dans [CORRECTIFS-PARTIE-REELLE.md](CORRECTIFS-PARTIE-REELLE.md).

## En bref

- **Ce qui marche, vérifié en simulation fidèle.** Le copilote détecte les décisions sans aucune saisie : mulligan, début de tour, choix de Découverte ou de Rembobinage, décision suivante du même tour. Il interroge gpt-6.1-sol en effort low et affiche l'action immédiate dans un overlay qui ne prend jamais le focus du jeu.
- **Délai mesuré.** Sur la partie rejouée, l'action s'affiche en médiane 4,7 s après la décision, et 4,8 s après le début du tour. Le conseil complet arrive en médiane en 9,3 s.
- **Légalité.** Les 14 conseils complets étaient légaux. Une action n'est jamais affichée si elle ne figure pas parmi les actions acceptées par le serveur.
- **Le problème principal.** Tu joues souvent plus vite que l'analyse : 15 appels envoyés sur 29 ont été annulés parce que la décision était déjà passée. L'action est quand même apparue avant ton coup dans 20 des 32 décisions analysées ; 17 autres décisions n'ont pas été analysées, à cause des plafonds.
- **Coût.** 0,22 $ mesurés pour la partie, plus environ 0,22 $ estimés pour les appels annulés, soit environ 0,45 $ par partie de 10 tours. Les plafonds par défaut empêchent de dépasser 0,75 $ par partie.

## Lancer

```powershell
powershell -ExecutionPolicy Bypass -File tools\lancer-copilote.ps1
```

L'onglet « Conseiller » de la fenêtre principale permet de vérifier et de configurer le fournisseur :

- l'état de la clé, lue dans la variable `OPENAI_API_KEY` et jamais écrite dans un fichier ;
- un test de connexion, pour environ 0,0001 $ ;
- le modèle, l'effort, les plafonds, le flux, le mulligan et les choix ;
- le détail complet du dernier conseil et le journal.

Les réglages vont dans `%LocalAppData%\HsCopilot\copilot.json`, section `advisor`. Le mode démo gratuit, `"provider": "simulated"` dans cette section, simule la latence sans appel et sans stratégie, pour essayer l'overlay.

## Ce que fait le copilote

### Quand il analyse

| Moment | Déclenchement | Attente avant l'appel |
|---|---|---|
| Mulligan | choix MULLIGAN proposé par le jeu | 300 ms |
| Début de ton tour, après la pioche | premier bloc d'options du tour | 300 ms |
| Découverte, Rembobinage, autre choix | choix proposé par le jeu | 300 ms |
| Décision suivante du même tour | nouvelles options après une action | 2,5 s sans nouvelle action |
| Ctrl+Alt+R, ou Ctrl+Alt+F10 si R est pris | à la demande | aucune |

Il n'analyse pas dans ces cas :

- partie classée, par défaut ;
- état incomplet ;
- pause ;
- seule action possible : finir le tour ;
- décision identique à celle déjà conseillée, le conseil étant alors réutilisé sans appel ;
- au-delà des plafonds : 3 analyses automatiques de milieu de tour, 50 par partie, 0,75 $ par partie, 3 $ par session ;
- létal démontré par le calcul local : il est affiché tout de suite, sans appel ;
- conseil suivi et rien de nouveau (aucune carte piochée ou générée, aucun serviteur adverse apparu) : l'étape suivante du plan est affichée sans appel, si le serveur la propose ;
- pendant un rattrapage du flux, quand les lignes arrivent en retard ou depuis le tampon du plugin : aucune analyse avant le prochain état reçu en direct.

Depuis les corrections du 25/10, après la première vraie partie, le début du tour, le mulligan et les choix ne sont jamais bloqués par le plafond par tour. Depuis la deuxième partie, ils ne le consomment plus non plus, ni la réanalyse manuelle : les 3 analyses du plafond sont réservées aux décisions de milieu de tour. Un appel annulé de milieu de tour continue de compter ; le lui rendre est une option, car cela coûte environ 45 % de plus. Détails dans [CORRECTIFS-PARTIE-REELLE.md](CORRECTIFS-PARTIE-REELLE.md) et [ANALYSE-PARTIE-2.md](ANALYSE-PARTIE-2.md).

Avant chaque envoi, le coût estimé d'un appel est réservé. Un appel annulé compte aussi pour un coût estimé, celui des jetons d'entrée.

### Ce qu'il affiche

- **ACTION IMMÉDIATE** : l'action, avec les statistiques actuelles de la cible, par exemple « Activer Profondeurs Sanguines → Écuyère d'Argent 3/1 ». Seule cette action est marquée « acceptée par le serveur à cet instant ».
- **PLAN** pour le tour suivant, **POURQUOI** en une ligne, **ENSUITE (NON VÉRIFIÉ)** : au plus deux étapes.
- **MULLIGAN** : les cartes à remplacer et à garder. **CHOIX** : la carte à prendre, avec son Sombre don s'il y en a un (« Choisir Concert promo-Drake + Somnambule »). Le modèle reçoit le don de chaque proposition et la carte qui en résulte ([SOMBRES-DONS-ET-DECK.md](SOMBRES-DONS-ET-DECK.md)).
- **Létal**, calculé par le solveur local ([TurnSolver](../src/HsCopilot.Core/Strategy/TurnSolver.cs)) :
  - « certain (démontré) » : une séquence de cartes et d'attaques tue sur l'état visible, aucun Secret adverse, et toutes ses mécaniques sont simulées ;
  - « conditionnel » : la séquence tue, mais un Secret peut l'empêcher, ou il faut une arme aléatoire d'au moins N ATQ (Animatrice de stade, avec une estimation de chance) ;
  - « plausible, non démontré » : il faudrait d'abord retirer une Provocation par des échanges, que le solveur ne simule pas ;
  - sinon aucun létal. Le niveau annoncé par le modèle est plafonné à ce calcul, et un létal démontré est affiché même si le modèle l'oublie.
- **MISE EN GARDE** sous le conseil, calculée localement : attaque conseillée alors que Profondeurs Sanguines peut encore donner +2 ATQ à l'attaquant ; létal démontré que le conseil ne suit pas ; fin de tour avec des attaques ou le pouvoir héroïque encore possibles.
- **Pendant l'attente**, une analyse rapide, sans IA et instantanée : létal, dégâts maximum au visage ce tour avec la séquence, ordre conseillé (Profondeurs avant l'attaque), menace adverse, actions encore possibles.
- **TON TOUR ARRIVE** : le serveur a déjà commencé ton tour (pioche comprise) mais Hearthstone rejoue encore le tour adverse ; contre l'IA, cela dure en médiane 9,9 s. Le conseil est déjà calculé sur ton vrai tour ; il devient ACTION IMMÉDIATE quand le client exécute ton tour (lignes PowerTaskList du Power.log). Voir [SYNCHRO-AFFICHAGE.md](SYNCHRO-AFFICHAGE.md).
- **Sans conseil**, le titre dit pourquoi : ANALYSES AUTO ÉPUISÉES, FIN DE TOUR, TOUR ADVERSE, RATTRAPAGE, BUDGET ATTEINT, PAS DE RÉPONSE… L'analyse rapide reste à jour.
- **Dès que tu joues autre chose**, le conseil disparaît avec sa raison, « Conseil retiré : autre action jouée ». Une réponse arrivée trop tard n'est jamais affichée.

L'action apparaît dès qu'elle est lisible dans la réponse en flux. Le conseil, la raison et le plan arrivent ensuite, dans les secondes qui suivent.

## Mesures

### Partie contre l'IA rejouée avec le vrai conseiller OpenAI

La partie de test « Grommash + Profondeurs » : 10 tours, victoire, environ 8 minutes. Elle est rejouée par le pipe comme si le plugin HDT l'envoyait, au rythme d'origine, avec les pauses plafonnées à 60 s. Modèle gpt-6.1-sol, effort low, réglages par défaut. Fichiers : `bench/live/openai-low-simulation.md` et `bench/live/openai-low-chronologie.txt`.

| Mesure | Nombre | P50 | P95 |
|---|---|---|---|
| Décision → action affichée | 20 | 4,7 s | 8,4 s |
| Début du tour → première action affichée | 10 | 4,8 s | 25,4 s |
| Décision → conseil complet | 14 | 9,3 s | 14,4 s |
| Début du tour → premier conseil complet | 10 | 10,6 s | 29,8 s |
| Appel → premier fragment de réponse | 14 | 2,8 s | 6,8 s |
| Durée d'un appel complet | 14 | 8,9 s | 12,8 s |
| Log → affichage de l'état, pour comparaison | 141 | 2 ms | 79 ms |

| Compteur | Valeur |
|---|---|
| Décisions analysées | 32 |
| Appels envoyés | 29 |
| Appels annulés avant l'envoi, attente adaptative | 3 |
| Appels annulés après l'envoi, décision passée | 15 |
| Conseils complets publiés | 14, tous légaux |
| Décisions analysées où l'action s'est affichée avant ton coup | 20 sur 32 |
| Analyses non lancées : plafond de 3 par tour | 14 |
| Analyses non lancées : seule la fin de tour possible | 3 |
| Coût mesuré, appels terminés | 0,218 $ |
| Coût estimé, appels annulés après l'envoi | 0,224 $ |

Les P95 « début du tour » incluent les tours où le premier appel a été annulé parce que tu as joué tout de suite. Dans une rediffusion, tes coups sont ceux de la partie d'origine, joués sans copilote. Avec le copilote, tu attendrais peut-être le conseil, et moins d'appels seraient annulés.

### Simulation sans appel, latence fixée à 10 ± 2,5 s

Même partie, même rythme, fournisseur simulé sans stratégie, avant l'ajout de l'attente adaptative. Fichier : `bench/live/simule-10s-simulation.md`.

| Mesure | Valeur |
|---|---|
| Appels envoyés | 37 |
| Appels annulés, décision passée | 23 |
| Conseils complets | 14 |
| Début du tour → première action affichée | P50 4,4 s, P95 25,3 s |

Elle a servi à régler l'attente adaptative et l'annulation immédiate des appels devenus inutiles, avant de dépenser des appels réels.

### Appels de test

Le budget des appels de test était de 1 $ au plus.

| Appel | Coût |
|---|---|
| Test de connexion | 0,0001 $ |
| Un appel en flux | 0,025 $ |
| Partie rejouée, mesuré | 0,218 $ |
| Partie rejouée, estimation des appels annulés | 0,224 $ |
| **Total, estimations comprises** | **0,47 $** |

L'appel en flux isolé a mis 10 s avant son premier fragment, à cause d'un cache vide et de 215 jetons de raisonnement. Sur la partie rejouée, la médiane est de 2,8 s : les instructions sont alors en cache.

## Ce qui reste incomplet

1. **Une seule partie réelle.** Elle a montré un défaut après un Rembobinage, corrigé depuis. La correction n'a pas encore été observée en direct. Le comportement en plein écran exclusif n'est pas vérifié.
2. **Latence trop longue en milieu de tour.** Environ 5 s pour l'action et 9 s pour le conseil complet. Ce sera trop lent si tu enchaînes vite. Pistes : alléger le message, qui fait environ 8 000 jetons ; analyser seulement quand tu marques une pause ; préparer le plan pendant le tour adverse.
3. **Le plafond de 3 analyses par tour peut encore se remplir avec des appels annulés de milieu de tour.** Le début de tour, les choix et la réanalyse manuelle ne le consomment plus ; les létaux démontrés et les suites de plan ne coûtent aucun appel. La suite de plan sans appel n'a pas encore été observée avec le vrai modèle.
4. **Coût réel des appels annulés inconnu.** OpenAI ne renvoie pas d'usage pour un flux interrompu. Le coût affiché en est une estimation prudente.
5. **Séquences calculées, pas vérifiées par le serveur.** Seule l'action immédiate est garantie légale. Le solveur simule les dégâts au visage des cartes du deck (liste dans [ANALYSE-PARTIE-2.md](ANALYSE-PARTIE-2.md)) ; il ne simule ni les échanges, ni la valeur, ni les effets aléatoires autres que l'arme d'Animatrice de stade, ni les Secrets.
6. **Qualité stratégique non mesurée en direct.** Seule la légalité l'est. Ton avis pendant la partie test, et tes annotations dans la fenêtre d'examen, serviront à la juger.
7. **Mode Coach détaillé.** Le raisonnement complet est dans l'onglet « Conseiller », pas encore dans l'overlay. Les réglages visuels de l'overlay se limitent à l'opacité et à la position, dans le fichier.
8. **Partie classée.** Désactivée par défaut, activée sur ta machine à ta demande : case dans l'onglet « Conseiller », ou `advisor.allowRanked`. Voir [CONFORMITE.md](CONFORMITE.md).
9. **Deck actif.** En direct, la liste vient de HDT. Avec la lecture directe du Power.log, sans HDT, il faut une liste dans `copilot.json` pour que le modèle connaisse les cartes restantes.
