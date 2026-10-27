using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Config;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Transport;
using HsCopilot.Shared;

namespace HsCopilot.Core.Tests;

public class TransportTests
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    // ------------------------------------------------------------------ Lecture du Power.log

    [Fact]
    public void Tailer_resumes_at_last_create_game_then_follows_appends()
    {
        var root = Directory.CreateTempSubdirectory("hscopilot-logs");
        try
        {
            var session = Directory.CreateDirectory(Path.Combine(root.FullName, "Hearthstone_2026_10_25_10_27_21"));
            var path = Path.Combine(session.FullName, "Power.log");
            var game = TestData.Lines(TestData.PriestGame);
            var previous = TestData.Lines(TestData.RogueGame).Take(500);
            var noise = new[] { "D 11:00:00.0000000 PowerTaskList.DebugPrintPower() - TAG_CHANGE Entity=1 tag=TURN value=1" };
            File.WriteAllLines(path, previous.Concat(noise).Concat(game.Take(1000)));

            Assert.Equal(path, PowerLogTailer.FindActivePowerLog(root.FullName));
            using var tailer = new PowerLogTailer(root.FullName);
            var received = new List<string>();
            var restarts = 0;
            tailer.LinesReceived += (lines, restart) => { received.AddRange(lines); if (restart) restarts++; };

            tailer.ReadNew(path);
            Assert.Equal(1, restarts);
            Assert.Contains("CREATE_GAME", received[0]);
            var expected = game.Take(1000).SkipWhile(l => !ReplayFile.IsCreateGame(l)).Where(l => l.Contains(" GameState.")).ToList();
            Assert.Equal(expected, received);

            // Ajout d'une ligne incomplète : elle n'est transmise qu'une fois terminée.
            using (var w = new StreamWriter(path, append: true, new UTF8Encoding(false)))
            {
                foreach (var l in game.Skip(1000).Take(10)) w.Write(l + "\r\n");
                w.Write(game[1010][..20]);
            }
            tailer.ReadNew(path);
            Assert.Equal(expected.Count + 10, received.Count);
            File.AppendAllText(path, game[1010][20..] + "\r\n", new UTF8Encoding(false));
            tailer.ReadNew(path);
            Assert.Equal(game[1010], received[^1]);

            // Fichier recréé plus court : relecture depuis le début, signalée comme redémarrage.
            File.WriteAllLines(path, game.Take(5));
            tailer.ReadNew(path);
            Assert.Equal(2, restarts);
        }
        finally
        {
            root.Delete(true);
        }
    }

    // ------------------------------------------------------------------ Named pipe

    [Fact]
    public async Task Pipe_server_receives_ndjson_messages_from_a_local_client()
    {
        var name = "HsCopilot.Test." + Guid.NewGuid().ToString("N");
        await using var server = new PipeServer(name);
        var got = new List<PipeMessage>();
        var done = new TaskCompletionSource();
        server.MessageReceived += m => { lock (got) { got.Add(m); if (got.Count == 3) done.TrySetResult(); } };
        server.Start();

        using (var client = new NamedPipeClientStream(".", name, PipeDirection.Out))
        {
            await client.ConnectAsync(5000);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = 1, PluginVersion = "test" }, Camel) + "\n");
            await w.WriteAsync("ceci n'est pas du JSON\n");
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = new() { "a", "b" } }, Camel) + "\n");
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeGap, Dropped = 3 }, Camel) + "\n");
            await w.FlushAsync();
            Assert.Same(done.Task, await Task.WhenAny(done.Task, Task.Delay(5000)));
        }
        Assert.Equal(new[] { "hello", "lines", "gap" }, got.Select(m => m.Type));
        Assert.Equal(new[] { "a", "b" }, got[1].Lines);
        Assert.Equal(1, server.MalformedMessages);
    }

    [Fact]
    public async Task Pipe_server_rejects_clients_coming_through_the_network_path()
    {
        var name = "HsCopilot.Test." + Guid.NewGuid().ToString("N");
        await using var server = new PipeServer(name);
        server.Start();
        await Task.Delay(300);
        using var remote = new NamedPipeClientStream("127.0.0.1", name, PipeDirection.Out);
        var ex = await Record.ExceptionAsync(() => remote.ConnectAsync(3000));
        Assert.NotNull(ex); // UnauthorizedAccessException quand le service Serveur tourne, sinon délai dépassé
        using var local = new NamedPipeClientStream(".", name, PipeDirection.Out);
        await local.ConnectAsync(3000);
        Assert.True(local.IsConnected);
    }

    // ------------------------------------------------------------------ Session complète

    [Fact]
    public async Task Auto_mode_falls_back_to_the_log_then_switches_to_the_plugin_without_mixing_sources()
    {
        var logs = Directory.CreateTempSubdirectory("hscopilot-auto");
        try
        {
            var dir = Directory.CreateDirectory(Path.Combine(logs.FullName, "Hearthstone_2026_10_25_11_00_00"));
            File.WriteAllLines(Path.Combine(dir.FullName, "Power.log"), TestData.Lines(TestData.RogueGame));
            var config = new CopilotConfig
            {
                Source = "auto",
                VisualClockFromLog = false,
                AutoFallbackSeconds = 1,
                HearthstoneLogsDir = logs.FullName,
                PipeName = "HsCopilot.Test." + Guid.NewGuid().ToString("N"),
                JournalEnabled = false,
                ReportsEnabled = false,   // jamais dans le dossier des rapports du joueur
            };
            await using var session = new CopilotSession(config, TestData.Cards);
            var fromLog = new TaskCompletionSource<GameStateSnapshot>();
            var fromPlugin = new TaskCompletionSource<GameStateSnapshot>();
            session.SnapshotUpdated += s =>
            {
                if (s.Source.Transport == "log-tail" && s.Game.GameOver) fromLog.TrySetResult(s);
                if (s.Source.Transport == "hdt-plugin" && s.Game.GameOver) fromPlugin.TrySetResult(s);
            };
            session.Start();
            Assert.Same(fromLog.Task, await Task.WhenAny(fromLog.Task, Task.Delay(15000)));
            var logSnapshot = await fromLog.Task;
            Assert.Equal("111928-1148177369", logSnapshot.GameId);

            using (var client = new NamedPipeClientStream(".", config.PipeName, PipeDirection.Out))
            {
                await client.ConnectAsync(5000);
                using var w = new StreamWriter(client, new UTF8Encoding(false));
                await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = 1 }, Camel) + "\n");
                foreach (var chunk in TestData.Lines(TestData.PriestGame).Chunk(400))
                    await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = chunk.ToList() }, Camel) + "\n");
                await w.FlushAsync();
                Assert.Same(fromPlugin.Task, await Task.WhenAny(fromPlugin.Task, Task.Delay(10000)));
            }
            var pluginSnapshot = await fromPlugin.Task;
            // Le moteur a été reconstruit à partir du seul flux du plugin : partie différente, révisions toujours croissantes.
            Assert.Equal("111018-695039071", pluginSnapshot.GameId);
            Assert.True(pluginSnapshot.Revision > logSnapshot.Revision);
            Assert.Equal(SourceKind.HdtPlugin, session.Status.ActiveSource);
        }
        finally
        {
            logs.Delete(true);
        }
    }

    [Fact]
    public async Task Session_rebuilds_a_full_game_received_from_the_plugin_protocol()
    {
        var config = new CopilotConfig
        {
            Source = "hdt",
            VisualClockFromLog = false,
            PipeName = "HsCopilot.Test." + Guid.NewGuid().ToString("N"),
            JournalEnabled = false,
            ReportsEnabled = false,   // jamais dans le dossier des rapports du joueur
        };
        await using var session = new CopilotSession(config, TestData.Cards);
        var finished = new TaskCompletionSource<GameStateSnapshot>();
        session.SnapshotUpdated += s => { if (s.Game.GameOver) finished.TrySetResult(s); };
        session.Start();

        using (var client = new NamedPipeClientStream(".", config.PipeName, PipeDirection.Out))
        {
            await client.ConnectAsync(5000);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = 1, HdtVersion = "1.59.6", PluginVersion = "test" }, Camel) + "\n");
            var deck = new HdtDeck { Name = "Guerrier dragons" };
            foreach (var (id, n) in TestData.DragonWarrior.Cards) deck.Cards.Add(new HdtDeckCard { Id = id, Count = n });
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeDeck, Deck = deck }, Camel) + "\n");
            foreach (var chunk in TestData.Lines(TestData.PriestGame).Chunk(400))
                await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = chunk.ToList() }, Camel) + "\n");
            await w.FlushAsync();
            Assert.Same(finished.Task, await Task.WhenAny(finished.Task, Task.Delay(10000)));
        }

        var s = await finished.Task;
        Assert.Equal("hdt-plugin", s.Source.Transport);
        Assert.Equal("111018-695039071", s.GameId);
        Assert.Equal("LOST", s.Game.Result);
        Assert.Equal("HDT", s.Local!.Deck.DecklistSource);
        Assert.Equal(SourceKind.HdtPlugin, session.Status.ActiveSource);
        Assert.Equal(TestData.Lines(TestData.PriestGame).Count, session.Status.LinesReceived);
    }
}
