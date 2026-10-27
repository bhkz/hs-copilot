using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using HsCopilot.Core.Config;
using HsCopilot.Core.Engine;
using HsCopilot.Core.Metrics;
using HsCopilot.Core.Session;
using HsCopilot.Core.State;
using HsCopilot.Core.Validation;
using HsCopilot.Shared;

namespace HsCopilot.Core.Tests;

/// <summary>Validation en direct : une session reçoit une partie réelle par le protocole du plugin et produit son rapport.</summary>
public class LiveValidationTests
{
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private static async Task<(LiveValidationReport Report, SessionStatus Status, string ReportsDir)> RunSession(string fixture)
    {
        var reports = Directory.CreateTempSubdirectory("hscopilot-reports");
        var config = new CopilotConfig
        {
            Source = "hdt",
            VisualClockFromLog = false,
            PipeName = "HsCopilot.Test." + Guid.NewGuid().ToString("N"),
            JournalEnabled = false,
            ReportsDir = reports.FullName,
        };
        await using var session = new CopilotSession(config, TestData.Cards);
        var done = new TaskCompletionSource();
        session.SnapshotUpdated += s =>
        {
            session.RecordDisplayed(s);
            if (s.Game.GameOver) done.TrySetResult();
        };
        session.Start();
        using (var client = new NamedPipeClientStream(".", config.PipeName, PipeDirection.Out))
        {
            await client.ConnectAsync(5000);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeHello, Protocol = 1 }, Camel) + "\n");
            foreach (var chunk in TestData.Lines(fixture).Chunk(200))
                await w.WriteAsync(JsonSerializer.Serialize(new PipeMessage { Type = PipeProtocol.TypeLines, Lines = chunk.ToList(), SentAt = DateTime.UtcNow.ToString("o") }, Camel) + "\n");
            await w.FlushAsync();
            Assert.Same(done.Task, await Task.WhenAny(done.Task, Task.Delay(20000)));
        }
        await Task.Delay(500);
        var report = session.BuildReportNow();
        var status = session.Status;
        return (report, status, reports.FullName);
    }

    private static CheckItem Item(LiveValidationReport r, string startsWith) => r.Checks.Single(c => c.Name.StartsWith(startsWith, StringComparison.Ordinal));

    [Fact]
    public async Task Real_game_through_plugin_protocol_passes_every_reconstruction_check()
    {
        var (r, status, dir) = await RunSession(TestData.RogueGame);
        try
        {
            Assert.Equal(CheckStatus.Ok, Item(r, "Début de partie").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Joueur local confirmé").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Mulligan terminé").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Tours observés").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Pioche en début de tour").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Actions jouées").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Fin de partie").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Vues du client").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Tour attribué au joueur local").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Références de joueur").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Coût et mana").Status);
            Assert.Equal(CheckStatus.Ok, Item(r, "Aucune carte adverse cachée").Status);
            Assert.Equal(CheckStatus.NotObserved, Item(r, "Concordance avec HDT").Status); // pas de vue HDT dans ce test
            Assert.True(r.Finished);
            Assert.Equal("LOST", r.Result);
            Assert.Contains(r.Latencies, l => l.Name == MetricNames.ReceiveToState && l.Count > 0);
            Assert.Contains(r.Latencies, l => l.Name == MetricNames.StateToDisplay && l.Count > 0);
            // Rapport enregistré automatiquement en fin de partie (JSON + Markdown).
            Assert.NotNull(status.LastReportPath);
            Assert.True(File.Exists(status.LastReportPath));
            Assert.Contains("Vues du client = état reconstruit", File.ReadAllText(status.LastReportPath!));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Rewinds_are_reported_with_their_stabilization_time()
    {
        var (r, _, dir) = await RunSession(TestData.RewindGame);
        try
        {
            Assert.Equal(CheckStatus.Ok, Item(r, "Rembobinage appliqué").Status);
            Assert.Contains("2 RESET_GAME", Item(r, "Rembobinage appliqué").Detail);
            Assert.Contains(r.Latencies, l => l.Name == MetricNames.RewindToStable && l.Count >= 1);
            Assert.Equal(0, r.Oracle.Mismatches);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}

/// <summary>Non-régression des défauts trouvés par les contrôles du 25/10/2026.</summary>
public class EngineRegressionTests
{
    private static string L(int second, string method, string body, int indent = 0) =>
        $"D 10:00:{second:00}.0000000 GameState.{method}() - {new string(' ', indent)}{body}";

    private static List<string> Setup(string opponentName) => new()
    {
        L(0, "DebugPrintPower", "CREATE_GAME"),
        L(0, "DebugPrintPower", "GameEntity EntityID=1", 4), L(0, "DebugPrintPower", "tag=CARDTYPE value=GAME", 8),
        L(0, "DebugPrintPower", "Player EntityID=2 PlayerID=1 GameAccountId=[hi=0 lo=0]", 4), L(0, "DebugPrintPower", "tag=CONTROLLER value=1", 8), L(0, "DebugPrintPower", "tag=HERO_ENTITY value=10", 8),
        L(0, "DebugPrintPower", "Player EntityID=3 PlayerID=2 GameAccountId=[hi=0 lo=0]", 4), L(0, "DebugPrintPower", "tag=CONTROLLER value=2", 8), L(0, "DebugPrintPower", "tag=HERO_ENTITY value=11", 8),
        L(0, "DebugPrintPower", "FULL_ENTITY - Creating ID=20 CardID=END_033", 4), L(0, "DebugPrintPower", "tag=CONTROLLER value=1", 8), L(0, "DebugPrintPower", "tag=ZONE value=HAND", 8), L(0, "DebugPrintPower", "tag=CARDTYPE value=MINION", 8),
        L(0, "DebugPrintGame", "PlayerID=1, PlayerName=Moi#1234"),
        L(0, "DebugPrintGame", $"PlayerID=2, PlayerName={opponentName}"),
        L(1, "DebugPrintPowerList", "Count=1"),
    };

    [Fact]
    public void Renamed_ai_opponent_still_receives_its_tag_changes()
    {
        // Contre l'IA, « Aubergiste » devient le nom du boss en cours de partie : ses tours ne doivent pas être attribués au joueur local.
        var lines = Setup("Aubergiste");
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Moi#1234 tag=CURRENT_PLAYER value=0"));
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Seigneur Gargamoelle tag=CURRENT_PLAYER value=1"));
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=GameEntity tag=TURN value=2"));
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Seigneur Gargamoelle tag=RESOURCES value=2"));
        var engine = new GameEngine(new EngineOptions { Cards = TestData.Cards });
        foreach (var l in lines) engine.Process(l);
        Assert.Equal(1, engine.LocalPlayerId);
        Assert.Equal(2, engine.CurrentPlayerId);
        Assert.Equal(2, engine.TurnOwners[2]);
        Assert.Equal(2, engine.PlayerEntity(2)!.GetInt("RESOURCES"));
        Assert.Equal(0, engine.PendingPlayerNameChanges);
        var s = new GameStateProjector(TestData.Cards).Project(engine);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "PLAYER_ALIAS_INFERRED");
    }

    [Fact]
    public void Unresolvable_player_name_is_flagged_not_silently_dropped()
    {
        var lines = Setup("Aubergiste").Select(l => l.Replace("CardID=END_033", "CardID=")).ToList(); // joueur local inconnu
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Seigneur Gargamoelle tag=CURRENT_PLAYER value=1"));
        var engine = new GameEngine(new EngineOptions { Cards = TestData.Cards });
        foreach (var l in lines) engine.Process(l);
        Assert.Equal(1, engine.PendingPlayerNameChanges);
        var s = new GameStateProjector(TestData.Cards).Project(engine);
        Assert.Contains(s.Uncertainty.Flags, f => f.Code == "PENDING_PLAYER_NAME");
    }

    [Fact]
    public void Overload_locked_crystals_are_not_subtracted_twice()
    {
        // Constaté : RESOURCES=6, RESOURCES_USED=3, OVERLOAD_LOCKED=3 en début de tour, cartes à 3 jouables.
        var lines = Setup("UNKNOWN HUMAN PLAYER");
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Moi#1234 tag=RESOURCES value=6"));
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Moi#1234 tag=RESOURCES_USED value=3"));
        lines.Add(L(2, "DebugPrintPower", "TAG_CHANGE Entity=Moi#1234 tag=OVERLOAD_LOCKED value=3"));
        var engine = new GameEngine(new EngineOptions { Cards = TestData.Cards });
        foreach (var l in lines) engine.Process(l);
        var mana = new GameStateProjector(TestData.Cards).Project(engine).Local!.Mana;
        Assert.Equal(3, mana.Available);
        Assert.Equal(3, mana.OverloadLocked);
    }

    [Fact]
    public void Client_stuck_report_line_is_ignored()
    {
        var engine = new GameEngine();
        engine.Process("W 00:42:27.4714214 GameState.ReportStuck() - Stuck for 10s 10ms. {SpellControllers: []}");
        Assert.Equal(0, engine.UnparsedCount);
    }
}
