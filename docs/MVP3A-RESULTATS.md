# MVP 3A : banc d'essai du conseiller stratégique (hors ligne)

Mesures du 25/10/2026, sur tes parties réelles. Aucune partie en direct n'a été utilisée. La validation du MVP 1 en direct reste **à confirmer par ta partie test**.

## En bref

- **Légalité.** gpt-6.1-sol a donné une action légale dans 100 % des cas, à tous les niveaux d'effort : 36 sur 36 par niveau, plus 8 sur 8 en xhigh. Le petit modèle gpt-6-luna s'est trompé une fois sur 36 ; le validateur a rejeté sa réponse.
- **Létal.** Sur les 9 cas où un létal était disponible sur l'état visible, gpt-6.1-sol l'a engagé 9 fois à chaque niveau d'effort. Deux de ces létaux passaient par une carte de la main. « Engagé » signifie que le premier coup va dans le sens du létal, pas que la séquence entière est garantie.
- **Coups douteux.** Le détecteur n'a signalé aucun coup « douteux » de gpt-6.1-sol. Il en signale 3 dans ton jeu, tous dans deux parties contre l'IA où l'adversaire est resté à 1 ou 3 PV pendant une dizaine de tours. Tu as confirmé avoir laissé ces adversaires en vie volontairement pour tester tes cartes : ce ne sont pas des erreurs, et ils ne servent pas à évaluer ton niveau.
- **Effort de raisonnement.** Monter l'effort ne change le coup que dans une minorité de cas : low et medium choisissent la même action dans 86 % des cas, medium et high dans 89 %. Sur les cas où les niveaux divergent, je n'ai pas trouvé d'avantage net pour high ou xhigh. Leur latence est en revanche incompatible avec le temps réel.
- **Recommandation.** medium par défaut, low pour un mode rapide. high et xhigh ne sont pas justifiés en direct sur cet échantillon.
- **Accord avec toi.** Le modèle choisit exactement ton coup dans 42 à 44 % des cas, quel que soit l'effort, et le même type d'action dans 72 %. Ce n'est pas une mesure de qualité : ton coup n'est pas une vérité de référence.

Rien ici ne prouve qu'un conseil est optimal. Les seuls faits vérifiés par machine sont la légalité de l'action immédiate et les calculs simples du détecteur ; le reste repose sur mon examen cas par cas et sur des indicateurs indirects.

## Protocole

### Données

| Élément | Valeur |
|---|---|
| Logs Hearthstone | 4 fichiers du dossier `Logs` |
| Parties où tu joues Guerrier | 30, dont 2 classées et 28 contre l'IA |
| Points de décision extraits | 582, dont 571 avec ton coup retrouvé |
| Cas du banc d'essai | 36, répartis sur 18 parties, dont 10 en classé |

Un **point de décision** est la fin d'un bloc d'options envoyé par le serveur pendant ton tour : c'est exactement ce que le client te permet de faire à cet instant. Les deux premières décisions de chaque tour sont retenues. La sélection des 36 cas est déterministe et stratifiée : classé, létal disponible, menace adverse, cartes clés du deck, puis complément pseudo-aléatoire stable, avec au plus 5 cas par partie.

### Pas d'information future

- L'état d'un cas est calculé uniquement à partir des lignes du log **antérieures** à la décision. Un test rejoue ces seules lignes et vérifie que l'état obtenu est identique, octet pour octet.
- **Ton coup réel** est la première action envoyée par ton client après ce point. Il sert d'étiquette d'évaluation et n'est jamais transmis au modèle ; un test le vérifie.
- **Aucune information cachée.** Le message envoyé au modèle ne contient que l'état observable, les hypothèses étiquetées et les actions légales. Le garde-fou anti-fuite a contrôlé les 582 messages : aucun identifiant de carte adverse cachée.

### Modèles et réglages

| Réglage | Valeur |
|---|---|
| Fournisseur | OpenAI, Responses API, sortie JSON stricte, `store=false` |
| Modèles | gpt-6.1-sol, efforts low, medium, high et xhigh ; gpt-6-luna medium comme référence bon marché |
| Instructions | règles de coach + fichier `config/strategy/guerrier-dragons.md` (empreintes `89db2eda2c5a` et `2076d066f36e`) |
| Message par décision | environ 8 250 jetons : état, 25 dernières actions, actions légales |
| Répétition | medium lancé deux fois, pour mesurer la variabilité propre au modèle |

Le modèle répond par un `actionId` choisi dans la liste, un conseil de 20 mots au plus, un raisonnement, le plan du tour suivant, l'alternative écartée et un niveau de confiance. Chaque réponse passe par le validateur du Strategy Engine.

### Reproduire

```powershell
$h = "src\HsCopilot.Cli\bin\Debug\net10.0-windows\hscopilot.exe"
& $h bench build "C:\Program Files (x86)\Hearthstone\Logs" --out bench\cases.jsonl
& $h bench run --cases bench\cases.jsonl --limit 36 --variants "gpt-6.1-sol:low,gpt-6.1-sol:medium,gpt-6.1-sol:high,gpt-6.1-sol:medium#2,gpt-6-luna:medium" --budget 8 --out bench\runs\efforts-36
& $h bench report bench\runs\efforts-36
& $h bench show bench\runs\efforts-36 --case "2026_10_24_20_59_43/Power_old.log#10@17630"
```

Chaque dossier d'exécution contient les cas figés, les instructions envoyées, toutes les réponses brutes, `run.json` (empreintes, prix, réglages), `report.md` et `summary.json`. Une exécution interrompue reprend sans refaire les appels déjà payés.

## Résultats mesurés

Dossier `bench/runs/efforts-36` : 36 cas et 180 appels, pour un coût de 2,44 $.

| Variante | Légales | Identique à ton coup | Même type d'action | Identique, classé seul | Létal engagé | Douteux |
|---|---|---|---|---|---|---|
| gpt-6-luna medium | 35/36 | 15/35 | 27/35 | 5/10 | 7/9 | 0 |
| gpt-6.1-sol low | 36/36 | 16/36 | 26/36 | 6/10 | 9/9 | 0 |
| gpt-6.1-sol medium | 36/36 | 15/36 | 26/36 | 6/10 | 9/9 | 0 |
| gpt-6.1-sol medium, 2e passage | 36/36 | 15/36 | 26/36 | 6/10 | 9/9 | 0 |
| gpt-6.1-sol high | 36/36 | 16/36 | 26/36 | 7/10 | 9/9 | 0 |
| ton jeu, même détecteur | n/a | n/a | n/a | n/a | 1/9 | 3 |

| Variante | Latence P50 | P95 | Réponse en 20 s ou moins | Jetons de raisonnement, moyenne | Coût par décision |
|---|---|---|---|---|---|
| gpt-6-luna medium | 12,2 s | 22,6 s | 92 % | 864 | 0,0013 $ |
| gpt-6.1-sol low | 9,8 s | 14,5 s | 100 % | 32 | 0,017 $ |
| gpt-6.1-sol medium | 11,9 s | 22,0 s | 92 % | 141 | 0,017 $ |
| gpt-6.1-sol high | 28,6 s | 65,6 s | 36 % | 904 | 0,026 $ |
| gpt-6.1-sol xhigh, 8 cas | 58,4 s | 127,4 s | 0 % | 2 544 | 0,043 $ |

- **Jetons.** L'entrée fait environ 8 250 jetons par décision ; seules les instructions, soit 1 600 jetons, sont réutilisées depuis le cache d'OpenAI. Le second passage medium a profité d'un cache presque complet, puisque le message était identique : 0,006 $ par décision au lieu de 0,017 $.
- **Variabilité.** Deux passages medium identiques donnent la même action dans 35 cas sur 36. Une différence entre niveaux d'effort supérieure à 1 cas sur 36 n'est donc pas du bruit.

| Accord entre variantes, même action | |
|---|---|
| low et medium | 31/36 |
| low et high | 29/36 |
| medium et high | 32/36 |
| medium et medium, 2e passage | 35/36 |
| gpt-6-luna et gpt-6.1-sol medium | 18/35 |

## L'effort de raisonnement vaut-il sa latence ?

Les niveaux d'effort divergent sur 8 des 36 cas. Ce sont les seuls cas où le choix de l'effort change le conseil ; je les ai relus un par un, et j'ai aussi lancé xhigh dessus (`bench/runs/xhigh-divergents`, 0,34 $).

| Cas | low | medium | high | xhigh | Mon avis |
|---|---|---|---|---|---|
| Classé, tour 3 contre Voleur, 3 mana, Glissedrake à 4 en main | La pièce puis Amalgame | La pièce puis Glissedrake 5/8 | Dragonnet du Vol infini | comme medium | La pièce puis Glissedrake 5/8 au tour 3 me paraît le plus fort. Dragonnet, ton coup, est plus lent. |
| Classé, tour 4 contre Prêtre, Sombre chevaucheur 1/1 adverse | Glissedrake puis Profondeurs | Profondeurs sur le 1/1, puis Glissedrake | comme medium | comme medium | Lignes équivalentes, seul l'ordre change. |
| Tour 6 contre Mage, arme 2/2 et Animatrice de stade en main | attaque à l'arme puis Animatrice | comme low | Animatrice d'abord, pour voir la nouvelle arme | comme low | Discutable. Animatrice d'abord permet de rembobiner une mauvaise arme ; attaquer d'abord garantit 2 dégâts. |
| Tour 10 contre Chasseur, toi à 2 PV, Secret inconnu, 10 mana | armure puis Dracorex | comme low | Dracorex puis armure | 1/1 contre un serviteur pour sonder le Secret, puis Dracorex et armure | xhigh est le plus soigné : il évite d'attaquer le héros, ce qui déclencherait un Piège explosif. low et medium restent sûrs, puisque l'armure passe en premier. |
| Tour 6 contre Mage, Glissedrake gelé, Secret inconnu | Colporteur au visage pour sonder | Duplicateur-inateur d'abord | comme low | comme low | Les deux se défendent. |
| Tour 1, Voyante ou Sombre chevaucheur | Sombre chevaucheur | Voyante | Voyante | Voyante | Équivalent. |
| Rexxar à 1 PV, plusieurs attaquants disponibles | Avant-garde | Chronoseigneur | Chronoseigneur | Chronoseigneur | Équivalent : les deux tuent. |
| Létal 6 contre 6 PV | arme puis Soldat | Soldat puis arme | comme medium | comme medium | Équivalent. |

**Conclusion.** Sur cet échantillon, high et xhigh ne trouvent pas de meilleurs coups de façon systématique. xhigh rejoint medium dans 6 cas sur 8, high dans 5, low dans 3. Seul le cas à 2 PV montre une ligne nettement plus fine en xhigh, et la réponse medium y restait sûre. Le prix en latence est lourd : high répond en 20 s ou moins dans 36 % des cas, xhigh jamais. medium est le meilleur compromis mesuré ; low est presque aussi bon, 31 cas sur 36 identiques, et plus rapide.

## Coups légaux mais discutables : exemples précis

1. **gpt-6-luna, action illégale.** Tour 9 contre Chasseur, cas `…20_59_43/Power_old.log#10@17630`. Il veut attaquer Rexxar avec Nozdormu, qui a Ruée et ne peut frapper que des serviteurs ce tour-ci. Le validateur a rejeté la réponse ; rien n'aurait été affiché.
2. **gpt-6-luna, létal manqué.** Tour 9 contre Druide, cas `…20_59_43/Power_old.log#8@13795`. Malfurion a 3 PV et une Provocation bloque les attaques. Il joue Frappe anticipée sur le Dragon assoupi 6/12 « pour ouvrir une séquence de létal », alors que la même Frappe au visage tuait. Le détecteur l'a signalé.
3. **gpt-6-luna, règle mal appliquée.** Classé, tour 7 contre Voleur, cas `…10_27_21/Power_old.log#4@14992`. Il affirme que l'Amalgame tue le Drake de pierre 2/2, qui a pourtant un Bouclier divin. gpt-6.1-sol, lui, envoie d'abord le Colporteur pour retirer le bouclier, et signale le Vol de vie qui rend 4 PV à l'adversaire.
4. **gpt-6.1-sol medium, second passage.** Tour 6 contre Mage, cas `…10_27_21/Power_old.log#1@7801`. Le héros, à 2 ATQ, attaque un Gardien des couvées 2/3 sans le tuer et subit 2 dégâts. C'est signalé « à vérifier ». Les autres passages attaquent le visage.
5. **gpt-6.1-sol high, ordre risqué.** Le cas à 2 PV ci-dessus : jouer Dracorex avant l'armure laisse une fenêtre où un Secret peut tuer ton héros.

Le détecteur applique des règles explicites : fin de tour prématurée, échange perdant, Profondeurs sur un serviteur adverse qui survit, dégâts sur son camp, létal ignoré, Chronoseigneur sans cible, Frappe sans pioche, alternative identique au choix. Ces règles ont aussi des faux positifs. Par exemple, elles comptent ta Frappe héroïque comme un létal ignoré, alors que ton héros passait à 8 ATQ contre 6 PV.

## Létaux trouvés par le modèle

- **Létal par Grommash.** Tour 9 contre Chasseur, toi à 2 PV, cas `…#10@17630`. Toutes les variantes de gpt-6.1-sol trouvent : Glissedrake 6, Forgeron 3 + 3 avec Furie des vents, Dragonnet 3, puis Grommash depuis la main avec Charge 4, soit 19 contre 17 PV. Le modèle note aussi que Nozdormu ne peut pas frapper le héros. **Aucun Secret adverse n'était en jeu à ce moment**, vérifié dans l'état : un Piège explosif ne pouvait pas te tuer pendant cette séquence. Elle reste une suite de cinq actions que rien ne vérifie automatiquement ; seule la première est garantie légale.
- **Le cas avec un Secret est un autre tour.** Tour 10 contre Chasseur, cas `…#9@21981` : toi à 2 PV, adversaire à 25 PV, un Secret inconnu. Il n'y a aucun létal à ce tour. C'est là que xhigh envoie un 1/1 contre un serviteur plutôt que vers le héros, pour ne pas déclencher un éventuel Piège explosif : cette sonde ne révèle pas un Piège explosif, elle évite de le déclencher.
- **Létal par Frappe anticipée au visage.** Contre Malfurion à 3 PV, voir l'exemple 2. gpt-6.1-sol le trouve à tous les niveaux d'effort.
- **Tes coups ne sont pas jugés ici.** Dans plusieurs parties contre l'IA, tu as volontairement laissé l'adversaire en vie pour tester tes cartes. Ton taux de létal pris, 1 sur 9, vient de là et ne dit rien de ton niveau.

Depuis le MVP 3B, le copilote distingue trois niveaux de létal : **visible**, si les attaques suffisent et qu'aucun Secret adverse n'est en jeu ; **conditionnel**, si un Secret peut l'empêcher, avec un avertissement explicite quand tes PV sont bas ; **plausible**, s'il dépend d'une carte de la main ou de plusieurs étapes non vérifiées. Le niveau annoncé par le modèle ne peut jamais dépasser ce que le calcul établit.

## Effet de ta liste de deck

Les 36 cas ont été construits sans ta liste de deck, donc le modèle ne savait pas quelles cartes il te restait. J'ai relancé medium avec ta liste sur les 30 cas venant de parties conformes à celle-ci (`bench/runs/deck-medium`, 0,58 $).

- 8 parties ont été écartées : tes 7 parties du 22/10, qui utilisaient une autre version du deck avec 11 à 22 cartes hors liste, et une partie avec une carte hors liste.
- Le choix change dans 3 cas sur 30. C'est plus que le bruit, 1 sur 36, mais l'accord avec ton coup ne change pas : 12 sur 30 dans les deux cas. Aucun signalement n'apparaît.
- Le message passe de 8 250 à 8 725 jetons et le coût de 0,017 $ à 0,019 $ par décision.

En direct, la liste du deck actif viendra du plugin HDT ; elle sera juste par construction.

## Limites

- **Pas de vérité de référence.** L'accord avec ton coup mesure la ressemblance, pas la qualité, et tes parties contre l'IA comptent des coups volontairement non optimaux, ce que tu as confirmé. Le vrai juge, c'est ton annotation dans la fenêtre d'examen.
- **Petit échantillon.** 36 cas, 18 parties, une seule famille de modèles. Une différence d'un ou deux cas n'est pas significative.
- **Le détecteur de létal est une borne basse.** Il compte les attaques autorisées et UNE carte de la main au plus. Il ignore les Secrets, les combinaisons de plusieurs cartes et le mana restant après les attaques. « Létal engagé » signifie que le premier coup vise le héros adverse ou joue la carte décisive, pas que la séquence entière a été vérifiée.
- **Une seule action est garantie.** Seule la première action est garantie légale par le serveur. Les suites proposées par le modèle ne sont pas vérifiées et sont affichées comme telles.
- **Latence.** Même en medium, 12 s en médiane et 22 s au P95, c'est lent pour un conseil par action. Il faudra limiter le nombre d'appels par tour et annuler les requêtes devenues obsolètes ; c'est déjà prévu par le coordinateur.
- **Prix.** Ce sont les tarifs publics relevés le 25/10/2026. Le coût réel dépend du cache d'OpenAI, de 0,006 $ à 0,017 $ par décision en medium.
- **Classé.** Analyser après coup tes parties classées enregistrées n'est pas une aide en temps réel. Les conseils en direct restent désactivés en classé par défaut.

## Fenêtre d'examen

```powershell
src\HsCopilot.App\bin\Debug\net10.0-windows\HsCopilot.App.exe --bench bench\runs\efforts-36
```

Elle est aussi accessible par le bouton « Banc d'essai… » de la fenêtre de diagnostic.

- **À gauche** : la liste des cas, avec des filtres (signalés, différents de ton coup, variantes en désaccord, létal, classé, erreurs, non annotés).
- **Au centre** : l'état observable et les actions légales. Ton coup est en vert, les choix des variantes en or.
- **À droite** : ton coup et chaque réponse, avec le conseil, le raisonnement, l'alternative, le plan, les signalements, la latence, les jetons et le coût.

Les boutons Bon, Discutable et Mauvais, avec une note libre, enregistrent dans `annotations.json`. La synthèse les reprend ensuite.

## Sources des prix

developers.openai.com/api/docs/models ; openai.com/index/introducing-gpt-6-1-sol ; benchlm.ai/openai/api-pricing ; saygm.com/blog/openai-api-pricing ; aipricing.guru ; stackwrite.com/blog/openai-api-pricing-2026 ; morphllm.com/openai-api-pricing. Tous consultés le 25/10/2026.
