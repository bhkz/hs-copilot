# Synthèse finale : contre-expertise indépendante et confrontation

Le 26/10/2026 après-midi, l'étude stratégique ([ETUDE-STRATEGIQUE.md](ETUDE-STRATEGIQUE.md)) a été soumise à une contre-expertise indépendante, l'agent E.
- **Ce qu'est l'agent E** : un vrai sous-agent, distinct, avec un mandat explicitement contradictoire. Il avait accès aux rapports, au code et aux données brutes.
- **Ce qu'il a fait** : il a recalculé les chiffres et exécuté le vrai code du projet (sur une copie de la DLL). Il n'a fait aucun appel OpenAI et n'a rien modifié.
- **Où trouver son travail** : son rapport est [agent-E-contre-expertise-independante.md](../bench/etude-strategique/agent-E-contre-expertise-independante.md) ; ses scripts et sorties sont dans [agent-E/](../bench/etude-strategique/agent-E/).
- **Sa version simulée** du matin est marquée comme remplacée.

## Réponse courte

**Tu avais raison sur les deux points qui te faisaient tiquer.**
- « Plafond probable du modèle » n'était pas justifié.
- « La recherche exhaustive couvre déjà ton tour » était faux : le solveur ne cherche que les dégâts au visage.

**Conclusion révisée** :
- **pour le système : B, des optimisations ciblées** ;
- **pour le modèle : preuves insuffisantes**, et non « plateau ». Rien ne permet aujourd'hui de dire qu'il est proche de son maximum, ni qu'un autre réglage ferait mieux.

**Le défaut le plus grave venait de mon propre correctif.**
- L'alerte de danger était bien calculée en 0,17 s, mais l'overlay l'effaçait dès qu'un début de réponse, une étape de plan ou un conseil s'affichait.
- Contre Tyrande, « Jouer Nozdormu » aurait effacé l'alerte sur Nozdormu.
- C'est **corrigé**, avec 3 tests sur le vrai affichage. Les tests passent de 251 à 254. **Relance l'application** pour en profiter.

**La prochaine marge ne vient pas d'un modèle plus puissant**, mais de deux choses :
1. **mieux composer ce que le système calcule déjà** : alerte à l'écran, danger vérifié après la ligne conseillée, échanges pour le létal, étapes de plan contrôlées. Tout cela est gratuit et mesurable hors ligne ;
2. **une mesure capable de trancher** : un juge aveugle au conseil, sur des positions difficiles qui n'ont pas servi au développement.

Ensuite seulement, un test payant ciblé sur les décisions difficiles : environ 1,7 $.

## 1. Ce que la contre-expertise a réfuté, et ce que j'ai vérifié moi-même

| Affirmation de l'étude | Verdict de l'agent E | Ma vérification | Statut |
|---|---|---|---|
| Alerte de danger « faite », « maintenue pendant le tour » | **réfutée** : visible seulement pendant l'analyse | lu dans `AdviceDisplay.cs` : les vues « partiel » et « prêt » n'avaient aucun fait. Le coordinateur transmettait pourtant bien les faits avec chaque conseil | **corrigé** dans le code |
| Agent A « à l'aveugle sur 20 conseils réels » | **faux** : le juge voyait le conseil | 20 fiches sur 20 contiennent « Conseil donné en partie » | **corrigé** dans l'étude |
| « Plafond probable pour le modèle » | **non démontré** | — | **retiré** |
| « La recherche exhaustive couvre ton tour » | **faux** : visage seulement, sans échange, sans Ruée | cohérent avec le texte envoyé au modèle (« échanges, survie et valeur ne sont pas évalués ») | **corrigé** |
| « La latence est le vrai goulot » ; « 26 % des appels » | **mal mesuré** : première action affichée avant ton coup dans 93 % des décisions avec appel ; tu attends le conseil 3 à 4 s ; le coût est du temps (12 à 34 s par tour) | non recalculé par moi | **accepté**, avec la réserve de la §3 |
| « 55 % des conseils affichés sont des étapes de plan » | **31 %** des premières actions affichées, débuts de réponse compris | les deux chiffres sont exacts, mais 55 % ne compte que les conseils complets : c'est le mauvais dénominateur pour « ce que tu vois » | **corrigé** |
| V1 « pas plus lent » | **en apparié, plus lent dans 8 paires sur 11, +2,0 s en médiane** | recalculé : identique | **corrigé** |
| Rexxar : « le défaut était la latence » | **incomplet** : le budget de session bloquait le modèle aux tours 14-15 ; ensuite, aucun début de réponse en 12,3 s ; l'ancien signal « Menace » était resté visible 12 s plus tôt dans le tour sans effet | cohérent avec le bilan de la nuit (budget atteint à 0 h 41 dans cette partie) | **accepté** |
| Nozdormu classé comme « danger » | c'est aussi une **erreur de jugement** sur un texte public que le modèle avait | — | **accepté** : le signal chiffré compense, mais le raisonnement n'est pas hors de cause |

## 2. Confrontation avec les agents A à D

| Question | A (stratégie) | B (recherche) | C (architecture) | D (évaluation) | E (contre-expertise) | Arbitrage |
|---|---|---|---|---|---|---|
| Le modèle a-t-il atteint un plafond ? | pas d'erreur nette, des pertes de valeur | l'effort change peu (MVP3A) | non traité | 1 $ ne suffit pas pour mesurer | non : juge non aveugle, environ 150 jetons de raisonnement, cas difficiles non testés | **E** : preuves insuffisantes |
| Le juge des conseils | il s'est jugé lui-même « à l'aveugle » sur les paires | — | — | demande un critère fixé à l'avance | le juge doit écrire sa ligne **avant** de voir le conseil, et être étalonné | **E et D** |
| Danger : où agir ? | — | recherche à deux demi-tours, ta ligne puis la riposte visible | alerte **en tête de l'overlay** | positions indépendantes | ni l'un ni l'autre n'a été fait : l'alerte était masquée, et le danger est évalué **avant** ta ligne | **E** ; affichage corrigé, reste le danger après ta ligne |
| Supprimer les appels de milieu de tour trop lents (piste I de C) | — | — | oui | — | non : 89 % de ces appels affichent l'action avant ton coup | **E** (mesuré) ; piste I abandonnée |
| Échanges dans le solveur | utiles pour protéger un serviteur clé | — | 6 létaux sur 25 plus tôt | — | 7 sur 38, vitesse seulement ; un tableau « tue / survit » ne suffit pas, il faut la riposte | **accord** sur le létal ; désaccord sur la valeur d'un évaluateur d'échanges, §3 |
| « 270 points sans conseil (31 %) » | — | — | — | chiffre de D | mauvais dénominateur : ce compteur relève surtout « seule action possible : finir le tour » | **E** (vérifié dans le code par E, non par moi) |

## 3. Désaccords non résolus

1. **Le modèle progresse-t-il sur les décisions difficiles avec plus de raisonnement ?**
   - **Pour** (E) : environ 150 jetons de raisonnement ; dans le seul cas difficile du banc MVP3A, xhigh était meilleur.
   - **Contre** (B, MVP3A) : low, medium et high donnent la même action dans 81 à 89 % des cas.
   - **Ce qui trancherait** : 20 positions difficiles × (medium, high, lignes candidates locales), jugées à l'aveugle (piste 3).
   - **Réserve** : avec 20 positions, seul un effet **fort** est détectable (au moins 15 préférences sur 20 au test de signe). Un résultat nul ne prouverait pas un plateau.
2. **La latence est-elle un problème de qualité ?**
   - E montre que tu attends le conseil, donc qu'il n'est pas perdu.
   - Mais l'attente est maximale là où tu as le plus besoin d'aide (15 à 17 s sur les positions difficiles et de danger) et là où l'on voudrait ajouter du calcul. Les tours de plus de 60 s s'approchent du chrono.
   - **Mon avis** : la latence n'est pas l'obstacle principal à la qualité, mais elle **plafonne ce qu'on peut ajouter** sur les décisions difficiles. Ce n'est pas tranché.
3. **L'alerte changera-t-elle tes décisions ?**
   - Contre Rexxar, l'ancienne alerte « Menace » était déjà visible plus tôt dans le tour fatal. Elle n'a pas empêché l'attaque au visage, et elle avait disparu au moment de cette décision.
   - On ne peut pas départager « signal absent » et « signal ignoré ». Seules tes prochaines parties le diront.
4. **Que vaut un évaluateur d'échanges au-delà du létal ?**
   - A et C : il aurait évité deux conseils discutables.
   - E : sur les phases d'attaque simulables, ton arithmétique est déjà presque toujours non dominée (2 sur 87). Il faudrait évaluer la riposte adverse sur tes serviteurs, et 55 % des phases contiennent un effet non simulé.
5. **Envoyer le bilan de danger au modèle (V1) ?**
   - Rien sur 4 paires de danger, +2,0 s en apparié.
   - **Ce qui trancherait** : le protocole de D sur des positions indépendantes.
   - **Ma recommandation** : passer avant par le danger après ta ligne, qui est gratuit et local.
6. **L'indépendance de la contre-expertise elle-même.**
   - E est un sous-agent Claude, comme A à D et comme moi.
   - Le contexte était séparé et le mandat contradictoire, mais ce n'est pas un expert humain.
   - Je n'ai pas recalculé tous ses chiffres : 93 % avant ton coup, 7 tours sur 38, 2 phases dominées sur 87.

## 4. Où est la prochaine marge, et dans quel ordre

Tes deux questions, d'abord.
- **Latence ou faiblesses stratégiques ?** Les deux, mais pas comme je l'avais écrit.
  - La latence coûte surtout du **temps**, pas des conseils perdus.
  - Il reste des faiblesses identifiables : Nozdormu, les pertes de valeur relevées par A, et un danger jamais vérifié **après** la ligne conseillée.
- **Améliorer le difficile sans dégrader le simple ?** Oui, si l'on route par une difficulté calculée localement **avant** l'appel : nombre d'actions légales, danger, Provocations, Secrets. Le simple ne change pas.
  - Le routage existe déjà : solveur, plan, modèle.
  - Une vérification de séquences qui ne fait qu'**ajouter des mises en garde** se borne hors ligne : on compte ses déclenchements sur les positions simples.

Ta piste « comparer des manières de produire le conseil » est celle que les données soutiennent le mieux. Tu reçois déjà quatre « produits » :

| Produit | Part des premières actions affichées | Délai | Ce qui est vérifié | Faiblesse |
|---|---|---|---|---|
| Début de réponse du modèle | 63 % | 5,6 s en médiane | légalité de la 1re action | — |
| Étape de plan | 31 % | 0,2 s | légalité | jamais confrontée au danger |
| Létal du solveur | 7 % | 0,1 s | démontré (26 tours sur 26) | visage seulement, sans échanges |
| Alerte de danger | 3,4 % des positions | 0,17 s | calcul exact sur l'état actuel | effacée par les trois autres (**corrigé**) ; évaluée avant ta ligne |

**Pistes classées** (0 $ sauf mention) :

1. **Fait** : l'alerte reste à l'écran.
   - Elle passe sur la ligne de mise en garde, en gras orange, dans toutes les vues et en mode compact.
   - Elle est omise quand le létal est démontré.
2. **Le danger après la ligne conseillée.** On applique l'action et les étapes structurées de la suite, puis on recalcule le danger.
   - On le mesure d'abord hors ligne sur les 830 + 582 positions, avec les conseils réels.
   - On relit chaque signalement. On ne l'affiche qu'ensuite.
3. **Une mesure aveugle**, environ 1 à 2 h de ton temps :
   - 30 positions difficiles hors développement ;
   - tu écris ta ligne d'abord, puis tu juges deux lignes anonymes ;
   - plus un juge LLM étalonné sur des positions à réponse démontrable.
4. **Les échanges contre les Provocations pour le létal**, avec un classement fin des risques. On rejoue les 1 412 positions : aucun « certain » ne doit apparaître hors des derniers tours gagnés.
5. **Les étapes de plan contrôlées** contre la marge de danger, avant d'être affichées.
6. **Payant, environ 1,7 $, seulement après 2 et 3** : 20 positions difficiles pré-enregistrées, avec medium, une relance de medium pour le bruit, high et les lignes candidates locales. Jugement aveugle, critères fixés à l'avance.
7. **Plus tard** : le message allégé et le cache (A/B, environ 1 $), la suite du plan lue dans le flux, le contexte des Découvertes et des mulligans.

## 5. Modification faite après la contre-expertise

| Modification | Fichiers | Tests |
|---|---|---|
| Les faits « Danger au prochain tour » et « … aide aussi l'adversaire » ont une gravité propre, `danger`. L'overlay les met sur la ligne de mise en garde, qui existait déjà, dans toutes les vues (analyse, début de réponse, conseil, suite du plan, sans conseil) et en mode compact, avant la mise en garde éventuelle du conseil. Ils ne sont plus en double parmi les faits gris, et ils sont omis quand un létal certain est affiché. Le XAML n'a pas changé, et rien n'est envoyé au modèle | `Strategy/AdviceDisplay.cs`, `Strategy/QuickAnalysis.cs` | 3 tests sur le vrai `AdviceDisplay` :<br>- **Rexxar, décision fatale** : l'alerte reste dans les quatre vues ;<br>- **« Jouer Nozdormu » conseillé** : l'alerte reste ;<br>- **létal démontré** : pas d'alerte. |

**254 tests réussis.** L'application en cours (13 h 12) n'a pas cette correction : relance-la avec `tools\lancer-copilote.ps1`.

## 6. Le crédit OpenAI : comparer nos comptes à la facturation

Le tableau de bord OpenAI compte par jour UTC (heure française moins 1 h). Voici ce que nos fichiers enregistrent pour ces deux jours.

| Jour (UTC) | Parties : coût réel (jetons reçus) | Parties : coût estimé des appels annulés | Bancs d'essai | Total réel | Total si l'estimation est juste |
|---|---|---|---|---|---|
| 25/10 | 4,63 $ | 4,38 $ | 3,44 $ | 8,07 $ | 12,45 $ |
| 26/10 | 2,29 $ | 2,44 $ | 0,69 $ | 2,98 $ | 5,41 $ |

**Pour lire la comparaison** :
- si le tableau de bord est **proche du total réel**, nos estimations des appels annulés sont trop pessimistes, et les budgets de session coupent trop tôt ;
- s'il est **proche de la dernière colonne**, elles sont justes.

**Ce qui n'est pas compté** :
- les essais hors partie (tests de connexion, sondes, simulations payantes des jours précédents) ;
- les parties sans rapport de fin.

**Sources** :
- les montants « coût API » et « coût estimé des appels annulés » des 60 rapports de `%LocalAppData%\HsCopilot\reports`, classés par heure de début de partie ;
- les `results.jsonl` des bancs d'essai.

## 7. Limites

- **Données** : toutes les mesures portent sur tes parties de développement. Rien n'a encore été mesuré sur des positions indépendantes.
- **Juges** : aucun jugement stratégique n'a encore été fait à l'aveugle au conseil.
- **Indépendance** : l'agent E est un sous-agent Claude, de la même famille que A à D et que moi (§3, point 6).
- **Effet de la correction** : il n'est pas encore observé en direct.
