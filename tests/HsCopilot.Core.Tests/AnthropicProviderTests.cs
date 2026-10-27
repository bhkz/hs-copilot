using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HsCopilot.Core.Config;
using HsCopilot.Core.Strategy;
using HsCopilot.Core.Strategy.Providers;

namespace HsCopilot.Core.Tests;

/// <summary>Fournisseur Anthropic (API Claude) sur des flux simulés : aucun appel réel.</summary>
public class AnthropicProviderTests
{
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Code, string Body)> _responses;
        public readonly List<HttpRequestMessage> Requests = new();
        public readonly List<string> Bodies = new();
        public Handler(params (HttpStatusCode, string)[] responses) => _responses = new(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            var (code, body) = _responses.Dequeue();
            return new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, code == HttpStatusCode.OK ? "text/event-stream" : "application/json") };
        }
    }

    private static string Sse(string type, JsonObject data)
    {
        data["type"] = type;
        return $"event: {type}\ndata: {data.ToJsonString()}\n\n";
    }

    private static readonly string Output = JsonSerializer.Serialize(new StrategyOutput { ActionId = "o1.2", Summary = "Joue \"la\" carte", Why = "tempo", Confidence = "moyenne", Rationale = "r" });

    private static string SuccessStream(string text)
    {
        var cut = text.IndexOf("\"summary\"", StringComparison.Ordinal) + 14;
        return Sse("message_start", new JsonObject
               {
                   ["message"] = new JsonObject
                   {
                       ["id"] = "msg_1", ["model"] = "claude-opus-5-5",
                       ["usage"] = new JsonObject { ["input_tokens"] = 1000, ["cache_read_input_tokens"] = 2000, ["cache_creation_input_tokens"] = 0, ["output_tokens"] = 1 },
                   },
               })
               + Sse("content_block_start", new JsonObject { ["index"] = 0, ["content_block"] = new JsonObject { ["type"] = "thinking", ["thinking"] = "" } })
               + Sse("content_block_delta", new JsonObject { ["index"] = 0, ["delta"] = new JsonObject { ["type"] = "thinking_delta", ["thinking"] = "{\"actionId\":\"o9.9\"" } })
               + Sse("content_block_delta", new JsonObject { ["index"] = 0, ["delta"] = new JsonObject { ["type"] = "signature_delta", ["signature"] = "abc" } })
               + Sse("ping", new JsonObject())
               + Sse("content_block_start", new JsonObject { ["index"] = 1, ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = "" } })
               + Sse("content_block_delta", new JsonObject { ["index"] = 1, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = text[..cut] } })
               + Sse("content_block_delta", new JsonObject { ["index"] = 1, ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = text[cut..] } })
               + Sse("message_delta", new JsonObject { ["delta"] = new JsonObject { ["stop_reason"] = "end_turn" }, ["usage"] = new JsonObject { ["output_tokens"] = 500 } })
               + Sse("message_stop", new JsonObject());
    }

    private static AnthropicMessagesProvider Provider(Handler handler) =>
        new(new AnthropicOptions { Model = "claude-opus-5-5", Effort = "medium", MaxRetries = 1, RetryBaseDelay = TimeSpan.Zero }, "consignes", new HttpClient(handler), () => "test-key");

    [Fact]
    public void The_request_uses_effort_the_same_schema_and_cached_instructions()
    {
        var json = JsonNode.Parse(Provider(new Handler()).BuildRequestJson("{\"etat\":1}", stream: true))!;
        Assert.Equal("claude-opus-5-5", json["model"]!.GetValue<string>());
        Assert.Equal("medium", json["output_config"]!["effort"]!.GetValue<string>());
        Assert.Equal("json_schema", json["output_config"]!["format"]!["type"]!.GetValue<string>());
        Assert.Equal("ephemeral", json["system"]![0]!["cache_control"]!["type"]!.GetValue<string>());
        Assert.Equal("consignes", json["system"]![0]!["text"]!.GetValue<string>());
        Assert.Equal("{\"etat\":1}", json["messages"]![0]!["content"]!.GetValue<string>());
        Assert.True(json["stream"]!.GetValue<bool>());
    }

    [Fact]
    public void The_schema_keeps_the_fields_and_their_order_with_documented_nullable_types()
    {
        var openAi = OpenAiResponsesProvider.OutputSchema();
        var claude = AnthropicMessagesProvider.OutputSchema();
        Assert.Equal(openAi["required"]!.ToJsonString(), claude["required"]!.ToJsonString());
        Assert.Equal(((JsonObject)openAi["properties"]!).Select(p => p.Key), ((JsonObject)claude["properties"]!).Select(p => p.Key));
        Assert.DoesNotContain("[\"string\",\"null\"]", claude.ToJsonString());
        Assert.DoesNotContain("[\"integer\",\"null\"]", claude.ToJsonString());
        Assert.Equal("null", claude["properties"]!["planNextTurn"]!["anyOf"]![1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public async Task Streaming_gives_the_action_early_ignores_thinking_and_computes_the_cost()
    {
        var handler = new Handler((HttpStatusCode.OK, SuccessStream(Output)));
        var partials = new List<StrategyPartial>();
        var r = await Provider(handler).ProposeWithPayloadStreamingAsync("{}", partials.Add, CancellationToken.None);

        Assert.Null(r.Error);
        Assert.Equal("o1.2", r.Output!.ActionId);
        Assert.All(partials, p => Assert.Equal("o1.2", p.ActionId));          // la réflexion n'est jamais lue comme une action
        Assert.Equal("Joue \"la\" carte", partials.Last().Summary);
        Assert.Equal(3000, r.InputTokens);
        Assert.Equal(2000, r.CachedInputTokens);
        Assert.Equal(500, r.OutputTokens);
        Assert.Equal((1000 * 4.0 + 2000 * 0.20 + 500 * 20.0) / 1_000_000, r.CostUsd!.Value, 9);
        Assert.NotNull(r.FirstTokenMs);

        var request = handler.Requests.Single();
        Assert.EndsWith("/v1/messages", request.RequestUri!.ToString());
        Assert.Equal("test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());
    }

    [Fact]
    public async Task An_overload_before_any_text_is_retried_once()
    {
        var overloaded = Sse("error", new JsonObject { ["error"] = new JsonObject { ["type"] = "overloaded_error", ["message"] = "Overloaded" } });
        var handler = new Handler((HttpStatusCode.OK, overloaded), (HttpStatusCode.OK, SuccessStream(Output)));
        var r = await Provider(handler).ProposeWithPayloadStreamingAsync("{}", null, CancellationToken.None);
        Assert.Null(r.Error);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(1, r.Retries);
    }

    [Fact]
    public async Task A_rejected_key_is_reported_without_retry()
    {
        var handler = new Handler((HttpStatusCode.Unauthorized, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\",\"message\":\"invalid x-api-key\"}}"));
        var r = await Provider(handler).ProposeWithPayloadStreamingAsync("{}", null, CancellationToken.None);
        Assert.Equal("HTTP 401 : invalid x-api-key", r.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void A_truncated_answer_is_an_error_and_a_complete_one_skips_the_thinking_block()
    {
        var provider = Provider(new Handler());
        string Body(string stop) => new JsonObject
        {
            ["id"] = "msg_2", ["model"] = "claude-opus-5-5", ["stop_reason"] = stop,
            ["content"] = new JsonArray(new JsonObject { ["type"] = "thinking", ["thinking"] = "…" }, new JsonObject { ["type"] = "text", ["text"] = Output }),
            ["usage"] = new JsonObject { ["input_tokens"] = 100, ["output_tokens"] = 50 },
        }.ToJsonString();
        Assert.Equal("o1.2", provider.Parse(Body("end_turn"), 200, 1, 0).Output!.ActionId);
        Assert.Equal("réponse incomplète : max_tokens", provider.Parse(Body("max_tokens"), 200, 1, 0).Error);
    }

    [Fact]
    public void A_claude_model_goes_through_Anthropic_and_any_other_through_OpenAI()
    {
        var claude = new AdvisorSettings { Model = "claude-opus-5-5", Effort = "medium" };
        Assert.Equal("anthropic:claude-opus-5-5:medium", StrategyProviders.Create(claude, "i").Name);
        Assert.Equal("ANTHROPIC_API_KEY", StrategyProviders.KeyVariable(claude.EffectiveModel));
        Assert.Contains("claude-opus-5-5", ModelPricing.KnownModels);
        Assert.Equal((7400 * 4.0 + 1600 * 0.20 + 1500 * 20.0) / 1_000_000, claude.EstimatedCallCostUsd, 9);

        var gpt = new AdvisorSettings { Model = "gpt-6.1-sol", Effort = "medium" };
        Assert.IsType<OpenAiResponsesProvider>(StrategyProviders.Create(gpt, "i"));
        Assert.Equal("OPENAI_API_KEY", StrategyProviders.KeyVariable(gpt.EffectiveModel));
    }

    [Fact]
    public void The_key_of_the_process_comes_first()
    {
        const string name = "HSCOPILOT_TEST_KEY_ONLY_FOR_TESTS";
        Environment.SetEnvironmentVariable(name, "processus");
        try { Assert.Equal("processus", ApiKey.Read(name)); }
        finally { Environment.SetEnvironmentVariable(name, null); }
        Assert.Null(ApiKey.Read(name));   // ni dans le processus ni dans le profil
    }
}
