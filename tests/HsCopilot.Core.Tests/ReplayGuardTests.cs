using System.Globalization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Logs;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using HsCopilot.Shared;

namespace HsCopilot.Core.Tests;

/// <summary>
/// Non-régression de la vraie partie du 25/10/2026 (anonymisée) : après un Rembobinage, HDT relit la partie et la renvoie
/// au plugin en retirant le passage rembobiné. L'état doit rester celui du vrai log.
/// </summary>
public class ReplayGuardTests
{
    private const string Fixture = "ai-rewind-hdt-resend.log";
    // Bornes observées dans les journaux de la vraie partie : HDT a renvoyé le début de la partie jusqu'à 15:50:55.818,
    // puis a repris à 15:51:09.580, après la réinitialisation du Rembobinage.
    private static readonly TimeSpan DroppedFrom = TimeSpan.Parse("15:50:55.8184588", CultureInfo.InvariantCulture);
    private static readonly TimeSpan ResumeAt = TimeSpan.Parse("15:51:09.5798319", CultureInfo.InvariantCulture);
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static TimeSpan TimeOf(string line) => TimeSpan.Parse(line.Substring(2, line.IndexOf(' ', 2) - 2), CultureInfo.InvariantCulture);

    /// <summary>Flux reçu de HDT pendant la vraie partie : la partie jusqu'au Rembobinage, puis le renvoi tronqué, puis la suite.</summary>
    private static List<string> HdtStream()
    {
        var lines = TestData.Lines(Fixture);
        var resume = lines.ToList().FindIndex(l => TimeOf(l) >= ResumeAt);
        var dropped = lines.ToList().FindIndex(l => TimeOf(l) >= DroppedFrom);
        Assert.True(dropped > 0 && resume > dropped);
        return lines.Take(resume).Concat(lines.Take(dropped)).Concat(lines.Skip(resume)).ToList();
    }

    private static GameStateSnapshot Final(IEnumerable<string> lines, bool guard)
    {
        var engine = TestData.NewEngine();
        var g = new ReplayGuard();
        foreach (var raw in lines)
        {
            var parsed = PowerLogLineParser.TryParse(raw);
            if (parsed is null) continue;
            if (guard && !g.Accept(parsed, raw, engine.HasGame, ReplayFile.IsCreateGame(raw))) continue;
            engine.Process(parsed);
        }
        return TestData.Project(engine);
    }

    private static string Essentials(GameStateSnapshot s) => string.Join(" | ",
        s.GameId, s.Game.Result, s.Game.ResetCount,
        $"moi {s.Local?.Hero?.Health}+{s.Local?.Hero?.Armor}", $"adv {s.Opponent?.Hero?.Health}+{s.Opponent?.Hero?.Armor}",
        "main " + string.Join(",", s.Local?.Hand.Cards.Select(c => c.EntityId) ?? Array.Empty<int>()),
        "plateau " + string.Join(",", s.Local?.Board.Select(m => m.EntityId) ?? Enumerable.Empty<int>()),
        "plateau adv " + string.Join(",", s.Opponent?.Board.Select(m => m.EntityId) ?? Enumerable.Empty<int>()));

    [Fact]
    public void Real_game_log_is_reconstructed_with_its_rewind()
    {
        var truth = Final(TestData.Lines(Fixture), guard: false);
        Assert.Equal("WON", truth.Game.Result);
        Assert.Equal(1, truth.Game.ResetCount);
    }

    [Fact]
    public void Hdt_resend_after_a_rewind_is_ignored_and_the_state_matches_the_real_log()
    {
        var truth = Final(TestData.Lines(Fixture), guard: false);
        var guarded = Final(HdtStream(), guard: true);
        Assert.Equal(Essentials(truth), Essentials(guarded));
    }

    [Fact]
    public void Without_the_guard_the_resend_produces_a_wrong_state()
    {
        // Ce que faisait la version testée en direct : la partie est reconstruite depuis le renvoi tronqué.
        var truth = Final(TestData.Lines(Fixture), guard: false);
        var unguarded = Final(HdtStream(), guard: false);
        Assert.Equal(0, unguarded.Game.ResetCount);
        Assert.NotEqual(Essentials(truth), Essentials(unguarded));
    }

    [Fact]
    public void Guard_skips_older_lines_and_duplicates_at_the_same_time_but_keeps_new_games()
    {
        var g = new ReplayGuard();
        PowerLogLine L(string t, string body = "TAG_CHANGE Entity=1 tag=X value=1") =>
            PowerLogLineParser.TryParse($"D {t} GameState.DebugPrintPower() - {body}")!;
        string R(PowerLogLine l) => $"D {l.Time:hh\\:mm\\:ss\\.fffffff} GameState.DebugPrintPower() - {l.Body}";

        var create = L("10:00:00.0000000", "CREATE_GAME");
        Assert.True(g.Accept(create, R(create), inGame: false, isCreateGame: true));
        var a = L("10:05:00.0000000", "TAG_CHANGE Entity=1 tag=A value=1");
        var b = L("10:05:00.0000000", "TAG_CHANGE Entity=1 tag=B value=1");
        Assert.True(g.Accept(a, R(a), true, false));
        Assert.True(g.Accept(b, R(b), true, false));
        // Renvoi : CREATE_GAME identique, lignes plus anciennes, doublons au même horodatage.
        Assert.False(g.Accept(create, R(create), true, true));
        Assert.False(g.Accept(L("10:01:00.0000000"), R(L("10:01:00.0000000")), true, false));
        Assert.False(g.Accept(a, R(a), true, false));
        var c = L("10:05:00.0000000", "TAG_CHANGE Entity=1 tag=C value=1");
        Assert.True(g.Accept(c, R(c), true, false));          // nouvelle ligne au même horodatage : reprise
        Assert.Equal(1, g.Episodes);
        Assert.Equal(3, g.Skipped);
        // Deux lignes identiques au même horodatage dans le flux normal restent acceptées.
        Assert.True(g.Accept(c, R(c), true, false));
        // Nouvelle partie, même plus tard que minuit : acceptée.
        var next = L("00:10:00.0000000", "CREATE_GAME");
        Assert.True(g.Accept(next, R(next), true, true));
    }

    [Fact]
    public void Times_across_midnight_compare_correctly()
    {
        Assert.True(ReplayGuard.Delta(TimeSpan.Parse("00:00:02"), TimeSpan.Parse("23:59:58")) > TimeSpan.Zero);
        Assert.True(ReplayGuard.Delta(TimeSpan.Parse("23:58:00"), TimeSpan.Parse("00:05:00")) < TimeSpan.Zero);
    }

    [Fact]
    public async Task Session_keeps_its_engine_when_hdt_resends_the_game_after_a_rewind()
    {
        var config = new CopilotConfig { VisualClockFromLog = false, Source = "hdt", PipeName = "HsCopilot.Test." + Guid.NewGuid().ToString("N"), JournalEnabled = false, ReportsEnabled = false };
        await using var session = new CopilotSession(config, TestData.Cards);
        var finished = new TaskCompletionSource<GameStateSnapshot>();
        session.SnapshotUpdated += s => { if (s.Game.GameOver) finished.TrySetResult(s); };
        session.Start();
        using (var client = new NamedPipeClientStream(".", config.PipeName, PipeDirection.Out))
        {
            await client.ConnectAsync(5000);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = 1, HdtVersion = "1.59.7", PluginVersion = "test" }, Camel) + "\n");
            foreach (var chunk in HdtStream().Chunk(300))
                await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = chunk.ToList() }, Camel) + "\n");
            await w.FlushAsync();
            Assert.Same(finished.Task, await Task.WhenAny(finished.Task, Task.Delay(15000)));
        }
        var s = await finished.Task;
        var truth = Final(TestData.Lines(Fixture), guard: false);
        Assert.Equal(1, s.Game.ResetCount);
        Assert.Equal(truth.Opponent!.Hero!.Health, s.Opponent!.Hero!.Health);
        Assert.Equal(truth.Local!.Hand.Cards.Select(c => c.EntityId), s.Local!.Hand.Cards.Select(c => c.EntityId));
        Assert.True(session.CurrentMetrics.Counter("flux : lignes renvoyées ignorées") > 1000);
        Assert.Contains(session.Status.Log, l => l.Contains("renvoie des lignes déjà traitées"));
    }
}
