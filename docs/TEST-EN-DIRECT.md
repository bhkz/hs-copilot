# Test en direct contre l'IA : procédure pas à pas

Durée : une partie d'entraînement, environ 10 minutes. Coût : environ 0,45 $ d'appels OpenAI pour une partie de 10 tours, mesuré sur une partie rejouée. Il est plafonné à 0,75 $ par partie par les réglages.

Cette partie sert à deux choses : confirmer en direct les corrections faites après ta première vraie partie, surtout si tu joues une carte à Rembobinage, et savoir si un conseil qui arrive après 5 à 10 secondes t'est utile.

## Avant de commencer, une seule fois

1. **Clé OpenAI.** Rien à faire si elle est déjà en place : l'application l'affiche dans l'onglet « Conseiller » (« ✔ Clé détectée »). Sinon, dans PowerShell : `setx OPENAI_API_KEY "ta-clé"`, puis ferme et rouvre PowerShell. La clé n'est jamais écrite dans un fichier du projet.
2. **Plugin HDT.** Déjà installé sur ta machine. En cas de doute : HDT fermé, puis `powershell -ExecutionPolicy Bypass -File tools\install-plugin.ps1 -Enable`.
3. **Hearthstone en mode fenêtré ou fenêtré sans bordure.** Options du jeu, Graphismes, Mode d'affichage. En plein écran exclusif, Windows peut masquer le panneau de conseils.

## La partie test

1. Lance HS Copilot :

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\lancer-copilote.ps1
   ```

   La première fois, le script compile l'application, ce qui prend environ une minute. La fenêtre « HS Copilot — Diagnostic » s'ouvre, avec le panneau de conseils en haut à droite de l'écran.
2. Lance **HDT**, puis **Hearthstone**.
3. Dans l'onglet « Conseiller », clique sur « Tester la connexion ». Attendu : « ✔ gpt-6.1-sol répond… », pour un coût d'environ 0,0001 $.
4. Lance une partie avec « Guerrier dragons ». Les conseils fonctionnent aussi en classé : tu l'as activé, et la case est dans l'onglet « Conseiller ».
5. Joue normalement en regardant le panneau :
   - **au mulligan**, il propose les cartes à remplacer ;
   - **à chaque début de tour**, l'analyse rapide s'affiche tout de suite, puis l'action immédiate quand elle arrive ;
   - si tu enchaînes vite plusieurs actions, il attend 2,5 secondes sans action avant de réanalyser ;
   - **Ctrl+Alt+R** réanalyse à la demande, **Ctrl+Alt+P** met en pause, **Ctrl+Alt+H** affiche ou masque le panneau. Si une autre application a déjà pris l'un de ces raccourcis, HS Copilot utilise **Ctrl+Alt+F10**, **F11** ou **F9** à la place ; l'onglet « Conseiller » affiche ceux qui sont actifs. Sur ta machine, Ctrl+Alt+R est déjà pris : la réanalyse est donc sur **Ctrl+Alt+F10**.
6. Termine la partie, victoire, défaite ou abandon, et attends 3 secondes.

## Récupérer le rapport

- Onglet « Validation », bouton « Ouvrir le dossier des rapports ». Le rapport de la partie est le fichier `.md` le plus récent de `%LocalAppData%\HsCopilot\reports\`.
- Il contient les oracles de reconstruction, les latences « log → affichage » et, nouveau, les mesures du conseiller : début de tour → première action affichée, décision → conseil, appels envoyés et annulés, coût de la partie.
- Le fichier des conseils de la partie est dans `%LocalAppData%\HsCopilot\advisor\` : un fichier `.jsonl` par partie, avec chaque conseil affiché, retiré ou non lancé, son tour et sa révision.
- Envoie-moi ces deux fichiers, le rapport `.md` et la trace `.jsonl`, et note en deux lignes :
  1. Les conseils arrivaient-ils à temps pour t'être utiles, et à quel moment du tour ?
  2. Un conseil t'a-t-il paru faux ou dangereux ? À quel tour ?

### Auditer une partie (sans appel API)

La commande `advisor-audit` rejoue les journaux de la session avec le même moteur et confronte chaque entrée de la trace à l'état réel : pioche intégrée ou non, âge du conseil, coup joué ensuite, et ce que le calcul local de la version actuelle aurait affiché.

```powershell
$L = "$env:LOCALAPPDATA\HsCopilot"
src\HsCopilot.Cli\bin\Debug\net10.0-windows\hscopilot.exe advisor-audit "$L\advisor\<trace>.jsonl" "$L\sessions\<journal précédent>_hdt.log" "$L\sessions\<journal de la partie>_hdt.log" --out audit.md
```

L'onglet « Conseiller » affiche aussi, pour chaque conseil, la révision analysée, la pioche du tour présente dans l'état, l'heure d'envoi et l'âge du conseil.

Pour vérifier les Découvertes avec Sombre don de tous tes Power.log (don de chaque proposition, carte choisie, statistiques réelles) :

```powershell
src\HsCopilot.Cli\bin\Debug\net10.0-windows\hscopilot.exe discover-audit "C:\Program Files (x86)\Hearthstone\Logs" --out decouvertes.md
```

## Ce qu'il faut vérifier à l'œil

- Le panneau ne prend jamais le clavier ni la souris de Hearthstone, même quand tu cliques sur ses boutons.
- L'action affichée est toujours jouable à cet instant. Dès que tu joues autre chose, elle disparaît : « Conseil retiré ».
- Les étapes « ENSUITE » sont marquées « non vérifié ».
- Un létal est étiqueté **certain (démontré)**, **conditionnel** ou **plausible, non démontré**. Avec un Secret adverse en jeu, il n'est jamais « certain ». Un létal certain s'affiche sans attendre l'IA, avec sa séquence.
- Une attaque conseillée alors que Profondeurs Sanguines peut encore renforcer l'attaquant porte une mise en garde orange « Avant cette attaque : … ».
- Contre l'IA, pendant les animations de fin du tour adverse, le panneau affiche « TON TOUR ARRIVE » ; il passe à « ACTION IMMÉDIATE » quand ton tour apparaît. Un conseil ne doit plus jamais être présenté comme jouable pendant que Hearthstone affiche « Tour adverse ».
- À une Découverte de Sombre chevaucheur ou de Suffusion, le choix affiché nomme le don : « Choisir Drake terrestre + Courtes griffes ».
- Après la partie, la trace contient une ligne « résultat-mulligan » : conseil, cartes gardées, remplacées et reçues.

## Si quelque chose ne marche pas

| Symptôme | Cause probable | Que faire |
|---|---|---|
| « Clé OpenAI absente » | variable non définie, ou définie après le lancement | `setx OPENAI_API_KEY "ta-clé"`, puis relance le script |
| Le panneau reste sur « En attente d'une partie » | HDT ou Hearthstone pas lancés, ou plugin inactif | onglet « Journal » : la source doit être « plugin HDT » ou « Power.log » |
| « conseils en temps réel désactivés en partie classée » | case « Conseiller aussi en partie classée » décochée | coche-la dans l'onglet « Conseiller », puis « Enregistrer et appliquer » |
| « ANALYSES AUTO ÉPUISÉES » | 3 analyses automatiques de milieu de tour déjà faites (le début de tour et les choix ne comptent pas) | réanalyse avec le raccourci indiqué (Ctrl+Alt+R, ou Ctrl+Alt+F10 sur ton PC), ou augmente le plafond dans l'onglet « Conseiller » ; l'analyse rapide reste à jour |
| Panneau invisible en jeu | Hearthstone en plein écran exclusif | passe en fenêtré sans bordure |
| Raccourcis sans effet | combinaison déjà prise par une autre application | regarde les raccourcis actifs dans l'onglet « Conseiller » (secours : Ctrl+Alt+F9, F10, F11), ou utilise les boutons du panneau |
