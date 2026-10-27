using System.Runtime.Versioning;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;

namespace HsCopilot.Cli;

[SupportedOSPlatform("windows")]
public static class LiveCommand
{
    /// <summary>Écoute le plugin HDT (ou le Power.log) et affiche l'état à chaque début de décision du joueur local.</summary>
    public static async Task<int> RunAsync(Args a)
    {
        var (cfg, cards) = Commands.LoadEnvironment(a);
        if (a.Get("source") is string src) cfg.Source = src;
        var seconds = a.GetInt("seconds");
        await using var session = new CopilotSession(cfg, cards);
        // Pas de conseiller dans cette commande : l'horloge de l'affichage est inutile.
        cfg.VisualClockFromLog = false;
        var lastKey = "";
        var lastLog = 0;
        session.SnapshotUpdated += s =>
        {
            var key = $"{s.GameId}:{s.Game.GameTurn}:{s.LegalOptions?.OptionsId}";
            var decision = s.LegalOptions is { Fresh: true } && s.Game.ActivePlayer == "local";
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] rév. {s.Revision} {s.Source.Transport} partie {s.GameId} tour {s.Game.TurnNumber} ({StateText.Side(s.Game.ActivePlayer)}) " +
                              $"main {s.Local?.Hand.Count ?? 0} / adv. {s.Opponent?.Hand.Count ?? 0} — {(s.Uncertainty.Incomplete ? "INCOMPLET" : "ok")}");
            if (decision && key != lastKey)
            {
                lastKey = key;
                Console.WriteLine(StateText.Render(s, historyLines: 6));
            }
        };
        session.StatusChanged += st =>
        {
            if (st.Log.Count == 0) return;
            var count = st.Log.Count;
            if (count == lastLog) return;
            lastLog = count;
            Console.WriteLine("  · " + st.Log[^1]);
        };
        session.Start();
        Console.WriteLine($"Écoute en cours (source {cfg.Source}). Ctrl+C pour arrêter.");
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
        try
        {
            await Task.Delay(seconds is int s ? TimeSpan.FromSeconds(s) : Timeout.InfiniteTimeSpan, cts.Token);
        }
        catch (OperationCanceledException) { }
        var final = session.Status;
        Console.WriteLine($"Arrêt. Source {final.ActiveSource}, lignes reçues {final.LinesReceived}, ignorées {final.LinesIgnored}, plugin {(final.PluginConnected ? "connecté" : "non connecté")}.");
        return 0;
    }
}
