using System.Text;
using System.Text.Json;
using HsCopilot.Cli;
using HsCopilot.Core.Cards;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;
using HsCopilot.Core.Verification;

Console.OutputEncoding = Encoding.UTF8;

var cli = new Args(args);
if (cli.Command is null or "help" or "--help" or "-h")
{
    PrintHelp();
    return 0;
}

try
{
    return cli.Command switch
    {
        "games" => Commands.Games(cli),
        "state" => Commands.State(cli),
        "verify" => Commands.Verify(cli),
        "anonymize" => Commands.Anonymize(cli),
        "carddb" => Commands.CardDb(cli),
        "live" => await LiveCommand.RunAsync(cli),
        "probe" => ProbeCommand.Run(cli),
        "simulate" => await SimulateCommand.RunAsync(cli),
        "bench" => await BenchCommand.RunAsync(cli),
        "provider" => await BenchCommand.ProviderTest(cli),
        "advisor-audit" => AuditCommand.Run(cli),
        "discover-audit" => DiscoverAuditCommand.Run(cli),
        "stats-dump" => StatsDumpCommand.Run(cli),
        "threat-audit" => ThreatAuditCommand.Run(cli),
        "case-text" => CaseTextCommand.Run(cli),
        "danger-check" => DangerCheckCommand.Run(cli),
        "line-danger" => LineDangerCommand.Run(cli),
        "advisor-probe" => await AdvisorProbeCommand.RunAsync(cli),
        _ => Fail($"Commande inconnue : {cli.Command}"),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Erreur : {ex.Message}");
    return 2;
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    PrintHelp();
    return 1;
}

static void PrintHelp()
{
    Console.WriteLine("""
    hscopilot — outils en ligne de commande de Hearthstone Copilot

      hscopilot games  <fichier>                         Liste les parties d'un Power.log ou .hdtreplay
      hscopilot state  <fichier> [--game N] [--turn T | --line L] [--json] [--deck NOM]
                                                         Reconstruit l'état (début de décision du tour T du joueur local)
      hscopilot verify <fichier|dossier> [--game N]      Contrôles de cohérence (oracle des options, mana, visibilité, fin de partie)
                                                         Un dossier : tous les Power*.log et .hdtreplay qu'il contient
      hscopilot anonymize <entrée> <sortie>              Remplace les BattleTags et identifiants de compte
      hscopilot carddb [--subset-of <fichier>] [--out <fichier.json>]
                                                         Charge la base de cartes ; exporte éventuellement un sous-ensemble
      hscopilot live [--source auto|hdt|log] [--seconds S]
                                                         Écoute le plugin HDT / le Power.log et affiche l'état en continu
      hscopilot simulate <fichier> [--via pipe|log] [--speed 1] [--max-wait 5] [--with-session] [--strategy diagnostic]
                                                         Rejoue une partie comme en direct (plugin simulé ou Power.log écrit
                                                         progressivement) ; avec --with-session : rapport de validation et mesures
      hscopilot probe <fichiers>                         Relève dans des logs réels les faits de mécanique du deck

    Banc d'essai du conseiller (MVP 3A, hors ligne, OPENAI_API_KEY requise pour run et provider) :
      hscopilot bench build <logs|dossier> [--out bench/cases.jsonl] [--per-turn 2] [--all-classes]
                                                         Extrait les points de décision et le coup réellement joué (étiquette)
      hscopilot bench run --out <dossier> [--cases bench/cases.jsonl] [--limit N] [--variants m:effort,...]
                          [--concurrency 4] [--budget 8] [--retry-errors] [--dry-run]
                                                         Interroge chaque variante ; reprise automatique ; plafond de dépense
                          [--same-cases-as <dossier>] [--only-deck-match]
      hscopilot bench report <dossier>                   Synthèse (report.md, summary.json)
      hscopilot bench compare <dossierA> <varianteA> <dossierB> <varianteB>
                                                         Compare deux variantes sur leurs cas communs
      hscopilot bench show <dossier> [--case ID | --flagged]
                                                         Affiche un cas : état, actions légales, coup joué, réponses
      hscopilot provider test [--model M] [--effort E] [--case ID]
                                                         Un appel réel, réponse complète, latence, jetons et coût
      hscopilot provider ping [--model M]                Test de connexion minimal (clé, modèle, latence)

    Simulation du copilote en direct (MVP 3B) :
      hscopilot simulate <fichier> --with-session --strategy openai|simulated|diagnostic [--effort low] [--model M]
                         [--max-calls-per-turn 4] [--budget-game 0.75] [--no-stream] [--timeline fichier.txt]
                                                         Rejoue une partie à son rythme réel avec le conseiller branché ;
                                                         « simulated » : latence simulée, aucun appel payant

    Options communes : --config <copilot.json>  --cards <cards.json|CardDefs.base.xml>
    """);
}
