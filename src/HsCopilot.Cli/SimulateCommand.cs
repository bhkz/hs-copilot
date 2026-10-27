using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Config;
using HsCopilot.Core.Session;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Transport;
using HsCopilot.Core.Util;
using HsCopilot.Core.Validation;
using HsCopilot.Shared;

namespace HsCopilot.Cli;

/// <summary>
/// Rejoue une partie enregistrée comme si elle se déroulait maintenant :
/// soit en se faisant passer pour le plugin HDT (pipe), soit en écrivant un Power.log progressivement (repli).
/// Avec --with-session, lance aussi une session complète en interne et produit le rapport de validation et les mesures.
/// </summary>
[SupportedOSPlatform("windows")]
public static class SimulateCommand
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static async Task<int> RunAsync(Args a)
    {
        if (a.Positional.Count == 0) throw new ArgumentException("Fichier manquant.");
        var file = a.Positional[0];
        var (cfg, cards) = Commands.LoadEnvironment(a);
        var games = ReplayFile.SplitGames(ReplayFile.ReadAllLines(file));
        var game = a.GetInt("game") is int gi ? games.First(g => g.Index == gi) : games[^1];
        var speed = double.TryParse(a.Get("speed"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sp) && sp > 0 ? sp : 1.0;
        var maxWait = double.TryParse(a.Get("max-wait"), NumberStyles.Float, CultureInfo.InvariantCulture, out var mw) ? mw : 5.0;
        var via = a.Get("via") ?? "pipe";
        var retime = !a.Has("keep-timestamps");
        var withSession = a.Has("with-session");
        // --whole-file : tout le fichier tel quel, y compris un renvoi de la même partie (cas HDT après un Rembobinage).
        var source = a.Has("whole-file") ? ReplayFile.ReadAllLines(file) : game.Lines;
        // Les lignes PowerTaskList (horloge de l'affichage) suivent le même chemin, dans l'ordre du fichier, sauf --no-visual.
        var visual = !a.Has("no-visual");
        var lines = source.Where(l => l.Contains(" GameState.", StringComparison.Ordinal) || (visual && l.Contains(" PowerTaskList.", StringComparison.Ordinal))).ToList();
        Console.Error.WriteLine($"Simulation : partie {game.Index} ({lines.Count} lignes GameState), via {via}, vitesse ×{speed}, attente max {maxWait} s, horodatages {(retime ? "actualisés" : "d'origine")}.");

        CopilotSession? session = null;
        AdvisorHost? advisor = null;
        var timeline = new List<string>();
        var clock = Stopwatch.StartNew();
        string? logsDir = null;
        var pipeName = a.Get("pipe") ?? (withSession ? "HsCopilot.Sim." + Guid.NewGuid().ToString("N") : PipeProtocol.PipeName);
        if (via == "log")
        {
            logsDir = a.Get("logs-dir") ?? Path.Combine(Path.GetTempPath(), "HsCopilotSim", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(logsDir);
        }

        GameStateSnapshotHolder? holder = null;
        if (withSession)
        {
            cfg.Source = via == "log" ? "log" : "hdt";
            cfg.PipeName = pipeName;
            if (logsDir is not null) cfg.HearthstoneLogsDir = logsDir;
            cfg.JournalEnabled = false;
            // Simulation : les lignes PowerTaskList éventuelles arrivent par le pipe avec le rejeu, jamais du vrai Power.log.
            cfg.VisualClockFromLog = false;
            // Les rapports d'une simulation ne vont jamais dans le dossier des vraies parties (sauf --report-dir).
            cfg.ReportsDir = a.Get("report-dir") ?? Path.Combine(Path.GetTempPath(), "HsCopilotSim", "reports");
            session = new CopilotSession(cfg, cards);
            holder = new GameStateSnapshotHolder();
            if (a.Get("strategy") is string strategy)
            {
                var current = session;
                var settings = cfg.Advisor;
                settings.Effort = a.Get("effort") ?? settings.Effort;
                settings.Model = a.Get("model") ?? settings.Model;
                settings.MaxCallsPerTurn = a.GetInt("max-calls-per-turn") ?? settings.MaxCallsPerTurn;
                if (double.TryParse(a.Get("budget-game"), NumberStyles.Float, CultureInfo.InvariantCulture, out var bg)) settings.MaxCostPerGameUsd = bg;
                if (double.TryParse(a.Get("budget-session"), NumberStyles.Float, CultureInfo.InvariantCulture, out var bs)) settings.MaxCostPerSessionUsd = bs;
                if (a.Has("no-stream")) settings.Streaming = false;
                // Fournisseurs sans stratégie (mesure de la chaîne) : autorisés sur les fixtures classées.
                settings.AllowRanked = strategy != "openai" || a.Has("allow-ranked");
                var delay = TimeSpan.FromMilliseconds(a.GetInt("strategy-delay-ms") ?? 0);
                Func<AdvisorSettings, IStrategyProvider>? factory = strategy switch
                {
                    "diagnostic" => _ => new DiagnosticFirstActionProvider(delay),
                    "simulated" => _ => new SimulatedLatencyProvider(delay == TimeSpan.Zero ? TimeSpan.FromSeconds(10) : delay, TimeSpan.FromMilliseconds(a.GetInt("strategy-jitter-ms") ?? 2500),
                        double.TryParse(a.Get("strategy-partial"), NumberStyles.Float, CultureInfo.InvariantCulture, out var pf) ? pf : 0.45),
                    "openai" => null,
                    _ => throw new ArgumentException($"--strategy {strategy} inconnu (diagnostic, simulated, openai)"),
                };
                if (a.Has("legacy-quota")) settings.LegacyQuota = true;
                if (a.Has("refund-cancelled")) settings.RefundCancelledCalls = true;
                if (a.Has("previous-version")) settings.PreviousVersion = true;
                advisor = new AdvisorHost(settings, () => current.CurrentMetrics, factory, a.Get("advisor-trace"));
                if (advisor.DisabledReason is string why) Console.Error.WriteLine("Conseiller inactif : " + why);
                Console.Error.WriteLine($"Conseiller : {advisor.Coordinator?.ProviderName ?? "aucun"} · {settings.MaxCallsPerTurn} analyses/tour · budget partie {settings.MaxCostPerGameUsd:0.00} $");
                advisor.Updated += u =>
                {
                    var what = u.Phase switch
                    {
                        AdvisorPhases.Ready => $"{u.Recommendation!.Action.Label} | {u.Recommendation.Summary}" + (u.Recommendation.Lethal != "aucun" ? $" [létal {u.Recommendation.Lethal}]" : "")
                            + (u.AwaitingDisplay ? " [TON TOUR ARRIVE : retenu jusqu'à l'affichage]" : u.Message.StartsWith("Ton tour est affiché", StringComparison.Ordinal) ? " [tour affiché : ACTION IMMÉDIATE]" : ""),
                        AdvisorPhases.Partial when u.AwaitingDisplay => $"(partiel) {u.PartialAction?.Label} [TON TOUR ARRIVE]",
                        AdvisorPhases.Partial => $"(partiel) {u.PartialAction?.Label}",
                        _ => u.Message,
                    };
                    lock (timeline) timeline.Add($"{clock.Elapsed:mm\\:ss\\.f} r{u.Revision} {u.Phase,-9} {what}");
                };
            }
            session.VisualProgressChanged += p => advisor?.OnVisualProgress(p);
            session.SnapshotUpdated += s =>
            {
                holder.Latest = s;
                advisor?.OnSnapshot(s);
                // Pas d'interface : on considère l'état « affiché » dès sa publication pour mesurer la chaîne interne.
                session.RecordDisplayed(s);
            };
            session.Start();
            await Task.Delay(300);
        }

        var sw = Stopwatch.StartNew();
        if (via == "log") await FeedLogAsync(lines, logsDir!, speed, maxWait, retime);
        else await FeedPipeAsync(lines, pipeName, speed, maxWait, retime);
        Console.Error.WriteLine($"Envoi terminé en {sw.Elapsed.TotalSeconds:0.0} s.");

        if (session is null) return 0;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && !(holder!.Latest?.Game.GameOver ?? false)) await Task.Delay(200);
        await Task.Delay(1500);
        var coordinator = advisor?.Coordinator;
        var cost = coordinator?.CostSessionUsd ?? 0;
        var calls = coordinator?.CallsSession ?? 0;
        if (advisor is not null) await advisor.DisposeAsync();
        var report = session.BuildReportNow();
        var status = session.Status;
        await session.DisposeAsync();
        Console.WriteLine(report.ToMarkdown());
        if (coordinator is not null)
        {
            Console.WriteLine($"## Conseiller : {coordinator.ProviderName}");
            Console.WriteLine();
            Console.WriteLine($"- appels envoyés : {calls} ; coût mesuré : {cost:0.0000} $");
            Console.WriteLine($"- conseils publiés : {coordinator.Board.Published}");
            foreach (var (k, v) in coordinator.Board.Invalidations) Console.WriteLine($"- conseils retirés ({k}) : {v}");
            foreach (var g in coordinator.Board.Events.GroupBy(e => e.Kind)) Console.WriteLine($"- événements « {g.Key} » : {g.Count()}");
            Console.WriteLine();
            Console.WriteLine("### Chronologie de l'overlay");
            Console.WriteLine();
            Console.WriteLine("```text");
            lock (timeline) foreach (var line in timeline) Console.WriteLine(line);
            Console.WriteLine("```");
            if (a.Get("timeline") is string tl) { lock (timeline) File.WriteAllLines(tl, timeline); }
        }
        Console.WriteLine($"Rapport enregistré : {status.LastReportPath ?? "(aucun)"}");
        return report.Verdict == "ÉCHEC" ? 3 : 0;
    }

    private sealed class GameStateSnapshotHolder
    {
        public volatile HsCopilot.Core.State.GameStateSnapshot? Latest;
    }

    /// <summary>Regroupe les lignes par horodatage identique (Hearthstone les écrit ensemble).</summary>
    private static List<(TimeSpan Time, List<string> Lines)> Bursts(List<string> lines)
    {
        var bursts = new List<(TimeSpan, List<string>)>();
        string? lastTs = null;
        foreach (var l in lines)
        {
            var ts = l.Length > 20 ? l.Substring(2, l.IndexOf(' ', 2) - 2) : "";
            if (ts != lastTs || bursts.Count == 0)
            {
                TimeSpan.TryParse(ts, CultureInfo.InvariantCulture, out var t);
                bursts.Add((t, new List<string>()));
                lastTs = ts;
            }
            bursts[^1].Item2.Add(l);
        }
        return bursts;
    }

    private static string Retime(string line, bool retime)
    {
        if (!retime || line.Length < 4) return line;
        var end = line.IndexOf(' ', 2);
        if (end < 0) return line;
        return line[..2] + DateTime.Now.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture) + line[end..];
    }

    private static async Task Pace(TimeSpan previous, TimeSpan current, double speed, double maxWait)
    {
        if (previous == TimeSpan.Zero) return;
        var wait = (current - previous).TotalSeconds / speed;
        if (wait <= 0) return;
        await Task.Delay(TimeSpan.FromSeconds(Math.Min(wait, maxWait)));
    }

    private static async Task FeedPipeAsync(List<string> lines, string pipeName, double speed, double maxWait, bool retime)
    {
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
        await client.ConnectAsync(10000);
        using var w = new StreamWriter(client, new UTF8Encoding(false));
        long seq = 0;
        async Task Send(PipeMessage m)
        {
            m.Seq = ++seq;
            m.SentAt = DateTime.UtcNow.ToString("o");
            await w.WriteAsync(JsonSerializer.Serialize(m, Camel) + "\n");
            await w.FlushAsync();
        }
        await Send(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = PipeProtocol.Version, Source = "simulateur", HdtVersion = "simulation", PluginVersion = "simulateur" });
        var previous = TimeSpan.Zero;
        foreach (var (time, burst) in Bursts(lines))
        {
            await Pace(previous, time, speed, maxWait);
            previous = time;
            await Send(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = burst.Select(l => Retime(l, retime)).ToList() });
        }
    }

    private static async Task FeedLogAsync(List<string> lines, string logsDir, double speed, double maxWait, bool retime)
    {
        var dir = Directory.CreateDirectory(Path.Combine(logsDir, $"Hearthstone_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}"));
        var path = Path.Combine(dir.FullName, "Power.log");
        Console.Error.WriteLine($"Écriture progressive de {path}");
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        await using var w = new StreamWriter(fs, new UTF8Encoding(false));
        var previous = TimeSpan.Zero;
        foreach (var (time, burst) in Bursts(lines))
        {
            await Pace(previous, time, speed, maxWait);
            previous = time;
            foreach (var l in burst) await w.WriteAsync(Retime(l, retime) + "\r\n");
            await w.FlushAsync();
        }
    }
}
