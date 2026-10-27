using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HsCopilot.Core.Strategy.Providers;

/// <summary>Configuration du fournisseur Anthropic (API Claude). La clé est lue dans ANTHROPIC_API_KEY, jamais dans un fichier.</summary>
public sealed class AnthropicOptions
{
    public string Model { get; init; } = "claude-opus-5-5";
    /// <summary>low, medium, high, xhigh, max (output_config.effort).</summary>
    public string Effort { get; init; } = "medium";
    public string BaseUrl { get; init; } = "https://api.anthropic.com/v1";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(180);
    /// <summary>Plafond des jetons de sortie, réflexion comprise (elle est toujours active sur Claude Opus 5.5).</summary>
    public int MaxTokens { get; init; } = 16000;
    public int MaxRetries { get; init; } = 2;
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public int HistoryItems { get; init; } = PromptBuilder.DefaultHistoryItems;

    public const string KeyVariable = "ANTHROPIC_API_KEY";

    public static AnthropicOptions FromEnvironment(string model, string? effort = null) => new()
    {
        Model = model,
        Effort = effort ?? "medium",
        BaseUrl = Environment.GetEnvironmentVariable("HSCOPILOT_ANTHROPIC_BASE_URL") is { Length: > 0 } u ? u : "https://api.anthropic.com/v1",
    };

    /// <summary>Clé présente dans l'environnement, et un extrait pour l'affichage (jamais la clé entière).</summary>
    public static (bool Present, string? Hint) KeyStatus()
    {
        var key = ApiKey.Read(KeyVariable);
        if (string.IsNullOrWhiteSpace(key)) return (false, null);
        return (true, key.Length > 8 ? $"{key[..3]}…{key[^4..]}" : "…");
    }
}

/// <summary>
/// Fournisseur Anthropic (Messages API, sortie contrainte par le même schéma que le fournisseur OpenAI). Mêmes garanties :
/// la validation métier reste faite par <see cref="RecommendationValidator"/> ; en flux, l'action et le conseil sont
/// transmis dès qu'ils sont lisibles. Les consignes sont mises en cache (cache_control) ; la réflexion n'est pas lue.
/// </summary>
public sealed class AnthropicMessagesProvider : IStrategyProvider, IPayloadStrategyProvider, IStreamingStrategyProvider
{
    private const string ApiVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly string _instructions;
    private readonly Func<string?> _apiKey;

    public AnthropicMessagesProvider(AnthropicOptions options, string instructions, HttpClient? http = null, Func<string?>? apiKey = null)
    {
        _options = options;
        _instructions = instructions;
        _http = http ?? new HttpClient { Timeout = options.Timeout };
        _apiKey = apiKey ?? (() => ApiKey.Read(AnthropicOptions.KeyVariable));
    }

    public string Name => $"anthropic:{_options.Model}:{_options.Effort}";
    public AnthropicOptions Options => _options;

    /// <summary>
    /// Schéma du fournisseur OpenAI, converti pour la Messages API : les types « texte ou null » deviennent des anyOf
    /// (forme documentée), l'ordre des propriétés est conservé (l'action et le conseil arrivent d'abord dans le flux).
    /// </summary>
    public static JsonObject OutputSchema() => (JsonObject)ConvertNullable(OpenAiResponsesProvider.OutputSchema())!;

    private static JsonNode? ConvertNullable(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject o:
            {
                var copy = new JsonObject();
                foreach (var (k, v) in o) copy[k] = ConvertNullable(v);
                if (copy["type"] is JsonArray types && types.Count == 2 && types.Any(t => t?.GetValue<string>() == "null"))
                {
                    var main = types.First(t => t?.GetValue<string>() != "null")!.GetValue<string>();
                    copy.Remove("type");
                    var description = copy["description"]?.DeepClone();
                    copy.Remove("description");
                    var inner = new JsonObject { ["type"] = main };
                    foreach (var (k, v) in copy) inner[k] = v?.DeepClone();
                    var result = new JsonObject { ["anyOf"] = new JsonArray(inner, new JsonObject { ["type"] = "null" }) };
                    if (description is not null) result["description"] = description;
                    return result;
                }
                return copy;
            }
            case JsonArray a:
                return new JsonArray(a.Select(x => ConvertNullable(x)).ToArray());
            default:
                return node?.DeepClone();
        }
    }

    public string BuildRequestJson(string userPayload, bool stream = false)
    {
        var o = new JsonObject
        {
            ["model"] = _options.Model,
            ["max_tokens"] = _options.MaxTokens,
            // Consignes et notes de stratégie identiques d'un appel à l'autre : mises en cache.
            ["system"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = _instructions,
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
            }),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = userPayload }),
            ["output_config"] = new JsonObject
            {
                ["effort"] = _options.Effort,
                ["format"] = new JsonObject { ["type"] = "json_schema", ["schema"] = OutputSchema() },
            },
        };
        if (stream) o["stream"] = true;
        return o.ToJsonString();
    }

    public Task<StrategyResult> ProposeAsync(DecisionContext context, CancellationToken cancellationToken) =>
        ProposeWithPayloadAsync(PromptBuilder.BuildUserPayload(context, _options.HistoryItems), cancellationToken);

    public Task<StrategyResult> ProposeStreamingAsync(DecisionContext context, Action<StrategyPartial> onPartial, CancellationToken cancellationToken) =>
        ProposeWithPayloadStreamingAsync(PromptBuilder.BuildUserPayload(context, _options.HistoryItems), onPartial, cancellationToken);

    private HttpRequestMessage NewRequest(string body, string key, bool stream)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", key);
        request.Headers.Add("anthropic-version", ApiVersion);
        if (stream) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    /// <summary>429 (limite), 529 (surcharge) et erreurs serveur : une nouvelle tentative.</summary>
    private static bool Transient(HttpStatusCode code) => code == HttpStatusCode.TooManyRequests || (int)code >= 500;

    public async Task<StrategyResult> ProposeWithPayloadAsync(string userPayload, CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new StrategyResult { Error = $"{AnthropicOptions.KeyVariable} absente", Model = _options.Model };
        var body = BuildRequestJson(userPayload);
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        var text = "";
        var retries = 0;
        for (var attempt = 0; ; attempt++)
        {
            using var request = NewRequest(body, key, stream: false);
            response?.Dispose();
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!Transient(response.StatusCode) || attempt >= _options.MaxRetries) break;
            retries++;
            await Task.Delay(_options.RetryBaseDelay * (attempt + 1), cancellationToken).ConfigureAwait(false);
        }
        using (response)
        {
            return Parse(text, (int)response.StatusCode, sw.Elapsed.TotalMilliseconds, retries);
        }
    }

    /// <summary>Coût exact : entrée hors cache, lectures du cache (5 % du prix d'entrée sur Opus 5.5), écritures (1,25 ×), sortie.</summary>
    private double? Cost(string model, int input, int cacheRead, int cacheWrite, int output)
    {
        var p = ModelPricing.Find(model) ?? ModelPricing.Find(_options.Model);
        if (p is null) return null;
        return (input * p.Input + cacheRead * (p.CachedInput ?? p.Input) + cacheWrite * p.Input * 1.25 + output * p.Output) / 1_000_000.0;
    }

    /// <summary>Lecture d'une réponse non diffusée de la Messages API (public pour les tests).</summary>
    public StrategyResult Parse(string json, int httpStatus, double latencyMs, int retries)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return new StrategyResult { Error = $"HTTP {httpStatus} : réponse non JSON", Model = _options.Model, LatencyMs = latencyMs, Retries = retries }; }
        if (root?["error"] is JsonObject err)
            return new StrategyResult { Error = $"HTTP {httpStatus} : {err["message"]?.GetValue<string>()}", Model = _options.Model, LatencyMs = latencyMs, Retries = retries };
        var text = string.Concat((root?["content"]?.AsArray() ?? new JsonArray())
            .Where(b => b?["type"]?.GetValue<string>() == "text").Select(b => b!["text"]?.GetValue<string>() ?? ""));
        var usage = root?["usage"];
        int Int(JsonNode? n) => n is null ? 0 : n.GetValue<int>();
        return Finish(root?["model"]?.GetValue<string>(), root?["id"]?.GetValue<string>(), root?["stop_reason"]?.GetValue<string>(), text,
            Int(usage?["input_tokens"]), Int(usage?["cache_read_input_tokens"]), Int(usage?["cache_creation_input_tokens"]), Int(usage?["output_tokens"]),
            latencyMs, retries);
    }

    private StrategyResult Finish(string? model, string? id, string? stopReason, string text, int input, int cacheRead, int cacheWrite, int output, double latencyMs, int retries)
    {
        model ??= _options.Model;
        var common = new StrategyResult
        {
            Model = model,
            InputTokens = input + cacheRead + cacheWrite,
            CachedInputTokens = cacheRead,
            OutputTokens = output,
            CostUsd = Cost(model, input, cacheRead, cacheWrite, output),
            LatencyMs = latencyMs,
            Retries = retries,
            ResponseId = id,
        };
        if (stopReason is "refusal") return common with { Error = "refus du modèle", RawOutput = text };
        if (stopReason is not ("end_turn" or "stop_sequence")) return common with { Error = $"réponse incomplète : {stopReason ?? "inconnu"}", RawOutput = text };
        if (text.Length == 0) return common with { Error = "aucun texte de sortie" };
        try
        {
            var parsed = JsonSerializer.Deserialize<StrategyOutput>(text);
            return parsed is null ? common with { Error = "sortie vide", RawOutput = text } : common with { Output = parsed, RawOutput = text };
        }
        catch (JsonException ex)
        {
            return common with { Error = "sortie non conforme : " + ex.Message, RawOutput = text };
        }
    }

    /// <summary>Erreur de flux qui vaut une nouvelle tentative : surcharge ou incident du serveur.</summary>
    private static readonly Regex TransientStreamError = new(@"overloaded|api_error|server|temporarily|try again", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<StrategyResult> ProposeWithPayloadStreamingAsync(string userPayload, Action<StrategyPartial>? onPartial, CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new StrategyResult { Error = $"{AnthropicOptions.KeyVariable} absente", Model = _options.Model };
        var body = BuildRequestJson(userPayload, stream: true);
        var sw = Stopwatch.StartNew();
        var retries = 0;
        for (var streamAttempt = 0; ; streamAttempt++)
        {
            HttpResponseMessage? response = null;
            for (var attempt = 0; ; attempt++)
            {
                var request = NewRequest(body, key, stream: true);
                response?.Dispose();
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) break;
                if (!Transient(response.StatusCode) || attempt >= _options.MaxRetries)
                {
                    using (response)
                    {
                        var error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        return Parse(error, (int)response.StatusCode, sw.Elapsed.TotalMilliseconds, retries);
                    }
                }
                retries++;
                await Task.Delay(_options.RetryBaseDelay * (attempt + 1), cancellationToken).ConfigureAwait(false);
            }
            var (result, transient) = await ReadStreamAsync(response, onPartial, sw, retries, cancellationToken).ConfigureAwait(false);
            // Surcharge annoncée dans le flux (overloaded_error) avant tout texte : une nouvelle tentative.
            if (!transient || streamAttempt >= _options.MaxRetries) return result;
            retries++;
            await Task.Delay(_options.RetryBaseDelay * (streamAttempt + 1), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(StrategyResult Result, bool Transient)> ReadStreamAsync(HttpResponseMessage response, Action<StrategyPartial>? onPartial, Stopwatch sw, int retries, CancellationToken cancellationToken)
    {
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = new StringBuilder();
            var data = new StringBuilder();
            double? firstToken = null, partialMs = null;
            StrategyPartial? last = null;
            string? model = null, id = null, stopReason = null;
            int input = 0, cacheRead = 0, cacheWrite = 0, output = 0;
            var transient = false;

            StrategyResult? Handle(string json)
            {
                JsonNode? node;
                try { node = JsonNode.Parse(json); }
                catch (JsonException) { return null; }
                int Int(JsonNode? n) => n is null ? 0 : n.GetValue<int>();
                switch (node?["type"]?.GetValue<string>())
                {
                    case "message_start":
                        var message = node["message"];
                        model = message?["model"]?.GetValue<string>();
                        id = message?["id"]?.GetValue<string>();
                        var u = message?["usage"];
                        input = Int(u?["input_tokens"]);
                        cacheRead = Int(u?["cache_read_input_tokens"]);
                        cacheWrite = Int(u?["cache_creation_input_tokens"]);
                        output = Int(u?["output_tokens"]);
                        return null;
                    case "content_block_delta" when (node["delta"]?["type"]?.GetValue<string>() == "text_delta"):
                        text.Append(node["delta"]?["text"]?.GetValue<string>());
                        firstToken ??= sw.Elapsed.TotalMilliseconds;
                        if (OpenAiResponsesProvider.ExtractPartial(text.ToString()) is { } p && p != last)
                        {
                            last = p;
                            if (p.Summary is not null) partialMs ??= sw.Elapsed.TotalMilliseconds;
                            onPartial?.Invoke(p with { ElapsedMs = sw.Elapsed.TotalMilliseconds });
                        }
                        return null;
                    case "message_delta":
                        stopReason = node["delta"]?["stop_reason"]?.GetValue<string>() ?? stopReason;
                        if (node["usage"]?["output_tokens"] is { } o) output = o.GetValue<int>();   // cumulatif
                        return null;
                    case "message_stop":
                        return Finish(model, id, stopReason, text.ToString(), input, cacheRead, cacheWrite, output, sw.Elapsed.TotalMilliseconds, retries)
                            with { FirstTokenMs = firstToken, PartialMs = partialMs };
                    case "error":
                        var type = node["error"]?["type"]?.GetValue<string>() ?? "";
                        var msg = node["error"]?["message"]?.GetValue<string>() ?? "erreur inconnue";
                        var overloaded = TransientStreamError.IsMatch(type + " " + msg);
                        transient = overloaded && firstToken is null;
                        return new StrategyResult
                        {
                            Error = "flux : " + (overloaded ? $"Anthropic surchargé, erreur de leur côté (« {msg} »)" : msg),
                            Model = model ?? _options.Model, LatencyMs = sw.Elapsed.TotalMilliseconds, Retries = retries,
                        };
                    default:
                        return null;   // ping, content_block_start/stop, réflexion (thinking_delta, signature_delta)
                }
            }

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (line.Length == 0)
                {
                    if (data.Length == 0) continue;
                    var done = Handle(data.ToString());
                    data.Clear();
                    if (done is not null) return (done, transient);
                    continue;
                }
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0) data.Append('\n');
                    data.Append(line.AsSpan(5).TrimStart());
                }
            }
            if (data.Length > 0 && Handle(data.ToString()) is { } tail) return (tail, transient);
            return (new StrategyResult { Error = "flux interrompu avant la fin de la réponse", Model = model ?? _options.Model, LatencyMs = sw.Elapsed.TotalMilliseconds, Retries = retries, RawOutput = text.ToString() }, false);
        }
    }

    /// <summary>Test de connexion : une requête minuscule, sans schéma, pour vérifier la clé, le modèle et la latence.</summary>
    public async Task<PingResult> PingAsync(CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new PingResult(false, $"{AnthropicOptions.KeyVariable} absente de l'environnement.", 0, null, null);
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["max_tokens"] = 1000,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "Réponds uniquement : OK" }),
            ["output_config"] = new JsonObject { ["effort"] = "low" },
        }.ToJsonString();
        var sw = Stopwatch.StartNew();
        try
        {
            using var request = NewRequest(body, key, stream: false);
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var latency = sw.Elapsed.TotalMilliseconds;
            JsonNode? root = null;
            try { root = JsonNode.Parse(text); } catch (JsonException) { /* corps non JSON */ }
            if (root?["error"] is JsonObject err)
                return new PingResult(false, $"HTTP {(int)response.StatusCode} : {err["message"]?.GetValue<string>()}", latency, _options.Model, null);
            if (!response.IsSuccessStatusCode) return new PingResult(false, $"HTTP {(int)response.StatusCode}", latency, _options.Model, null);
            var model = root?["model"]?.GetValue<string>() ?? _options.Model;
            var usage = root?["usage"];
            int Int(JsonNode? n) => n is null ? 0 : n.GetValue<int>();
            var cost = Cost(model, Int(usage?["input_tokens"]), Int(usage?["cache_read_input_tokens"]), Int(usage?["cache_creation_input_tokens"]), Int(usage?["output_tokens"]));
            return new PingResult(true, $"{model} répond (HTTP {(int)response.StatusCode}) en {latency / 1000:0.0} s.", latency, model, cost);
        }
        catch (HttpRequestException ex)
        {
            return new PingResult(false, "réseau : " + ex.Message, sw.Elapsed.TotalMilliseconds, _options.Model, null);
        }
    }
}
