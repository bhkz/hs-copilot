using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace HsCopilot.Core.Strategy.Providers;

/// <summary>Prix par million de jetons (dollars US).</summary>
public sealed record ModelPrice(double Input, double? CachedInput, double Output, string Source);

/// <summary>
/// Tarifs connus. Sources : page Models d'OpenAI (developers.openai.com/api/docs/models), consultée le 25/10/2026 ;
/// page Pricing d'Anthropic (platform.claude.com/docs/en/about-claude/pricing), consultée le 26/10/2026.
/// </summary>
public static class ModelPricing
{
    private static readonly Dictionary<string, ModelPrice> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-6-astra"] = new(10.0, null, 50.0, "developers.openai.com, 25/10/2026 (prix de l'entrée en cache non publié : prix plein appliqué)"),
        ["gpt-6.1-sol"] = new(2.0, 0.10, 10.0, "developers.openai.com et annonce GPT-6.1 Sol, 25/10/2026"),
        ["gpt-6-luna"] = new(0.10, 0.01, 0.50, "developers.openai.com et comparateurs, 25/10/2026"),
        // Anthropic : lecture du cache à 5 % du prix d'entrée (Opus et Sonnet 5.5), écriture à 1,25 × (calculée par le fournisseur).
        ["claude-opus-5-5"] = new(4.0, 0.20, 20.0, "platform.claude.com, Pricing, 26/10/2026"),
        ["claude-sonnet-5-5"] = new(2.0, 0.10, 10.0, "platform.claude.com, Pricing, 26/10/2026"),
        ["claude-haiku-5-5"] = new(0.10, 0.01, 0.50, "platform.claude.com, Pricing, 26/10/2026 (messages de moins de 100 000 jetons)"),
    };

    public static IReadOnlyCollection<string> KnownModels => Prices.Keys;

    public static ModelPrice? Find(string model)
    {
        if (Prices.TryGetValue(model, out var p)) return p;
        // Variantes datées (« gpt-6.1-sol-2026-… ») : correspondance par préfixe.
        return Prices.Where(kv => model.StartsWith(kv.Key + "-", StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Value).FirstOrDefault();
    }

    public static void Override(string model, ModelPrice price) => Prices[model] = price;

    /// <summary>Coût d'un appel. Les jetons de raisonnement sont inclus dans les jetons de sortie facturés.</summary>
    public static double? Cost(string model, int inputTokens, int cachedTokens, int outputTokens)
    {
        var p = Find(model);
        if (p is null) return null;
        var uncached = Math.Max(0, inputTokens - cachedTokens);
        var cachedPrice = p.CachedInput ?? p.Input;
        return (uncached * p.Input + cachedTokens * cachedPrice + outputTokens * p.Output) / 1_000_000.0;
    }
}

/// <summary>Configuration du fournisseur OpenAI (variables d'environnement ou réglages, jamais la clé dans un fichier).</summary>
public sealed class OpenAiOptions
{
    public string Model { get; init; } = "gpt-6.1-sol";
    /// <summary>low, medium, high, xhigh, max (selon le modèle).</summary>
    public string ReasoningEffort { get; init; } = "medium";
    public string BaseUrl { get; init; } = "https://api.openai.com/v1";
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(180);
    /// <summary>Plafond des jetons de sortie, raisonnement compris.</summary>
    public int MaxOutputTokens { get; init; } = 24000;
    public int MaxRetries { get; init; } = 2;
    /// <summary>Attente avant la première nouvelle tentative (doublée ensuite).</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(2);
    public string PromptCacheKey { get; init; } = "hscopilot-guerrier-dragons-v1";
    /// <summary>Nombre d'actions récentes de l'historique transmises au modèle.</summary>
    public int HistoryItems { get; init; } = PromptBuilder.DefaultHistoryItems;

    /// <summary>
    /// HSCOPILOT_OPENAI_MODEL, HSCOPILOT_OPENAI_EFFORT, HSCOPILOT_OPENAI_BASE_URL, HSCOPILOT_OPENAI_TIMEOUT_S,
    /// HSCOPILOT_OPENAI_MAX_OUTPUT_TOKENS. La clé est lue dans OPENAI_API_KEY au moment de l'appel.
    /// </summary>
    public static OpenAiOptions FromEnvironment(string? model = null, string? effort = null)
    {
        string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;
        return new OpenAiOptions
        {
            Model = model ?? Env("HSCOPILOT_OPENAI_MODEL") ?? "gpt-6.1-sol",
            ReasoningEffort = effort ?? Env("HSCOPILOT_OPENAI_EFFORT") ?? "medium",
            BaseUrl = Env("HSCOPILOT_OPENAI_BASE_URL") ?? "https://api.openai.com/v1",
            Timeout = TimeSpan.FromSeconds(int.TryParse(Env("HSCOPILOT_OPENAI_TIMEOUT_S"), out var t) ? t : 180),
            MaxOutputTokens = int.TryParse(Env("HSCOPILOT_OPENAI_MAX_OUTPUT_TOKENS"), out var m) ? m : 24000,
        };
    }

    /// <summary>Clé présente dans l'environnement, et un extrait pour l'affichage (jamais la clé entière).</summary>
    public static (bool Present, string? Hint) KeyStatus()
    {
        var key = ApiKey.Read("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return (false, null);
        return (true, key.Length > 8 ? $"{key[..3]}…{key[^4..]}" : "…");
    }
}

/// <summary>Résultat du test de connexion.</summary>
public sealed record PingResult(bool Ok, string Message, double LatencyMs, string? Model, double? CostUsd);

/// <summary>
/// Fournisseur OpenAI (Responses API, sortie structurée stricte). Le modèle ne peut renvoyer qu'un objet conforme
/// au schéma <see cref="StrategyOutput"/> ; la validation métier reste faite par <see cref="RecommendationValidator"/>.
/// En flux, l'action et le conseil sont transmis dès qu'ils sont lisibles, avant la fin de la génération.
/// </summary>
public sealed class OpenAiResponsesProvider : IStrategyProvider, IPayloadStrategyProvider, IStreamingStrategyProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly string _instructions;
    private readonly Func<string?> _apiKey;

    public OpenAiResponsesProvider(OpenAiOptions options, string instructions, HttpClient? http = null, Func<string?>? apiKey = null)
    {
        _options = options;
        _instructions = instructions;
        _http = http ?? new HttpClient { Timeout = options.Timeout };
        _apiKey = apiKey ?? (() => ApiKey.Read("OPENAI_API_KEY"));
    }

    public string Name => $"openai:{_options.Model}:{_options.ReasoningEffort}";
    public OpenAiOptions Options => _options;

    /// <summary>
    /// Schéma strict : toutes les propriétés sont requises, les facultatives acceptent null. L'ordre compte pour le flux :
    /// action, conseil, raison, létal et plan arrivent d'abord.
    /// </summary>
    public static JsonObject OutputSchema() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new JsonArray("actionId", "summary", "why", "lethal", "planNextTurn", "followUp", "confidence", "uncertaintyReasons", "rationale",
            "rejectedAlternativeId", "rejectedAlternativeReason", "risk", "replaceIds"),
        ["properties"] = new JsonObject
        {
            ["actionId"] = new JsonObject { ["type"] = "string", ["description"] = "id exact d'une action de legalActions" },
            ["summary"] = new JsonObject { ["type"] = "string", ["description"] = "conseil principal, 20 mots au plus" },
            ["why"] = new JsonObject { ["type"] = "string", ["description"] = "raison principale, 15 mots au plus" },
            ["lethal"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("aucun", "certain", "conditionnel", "plausible") },
            ["planNextTurn"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["followUp"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("text", "sourceEntityId", "targetEntityId"),
                    ["properties"] = new JsonObject
                    {
                        ["text"] = new JsonObject { ["type"] = "string" },
                        ["sourceEntityId"] = new JsonObject { ["type"] = new JsonArray("integer", "null"), ["description"] = "entityId de la carte, du serviteur, du héros ou du lieu qui agit" },
                        ["targetEntityId"] = new JsonObject { ["type"] = new JsonArray("integer", "null"), ["description"] = "entityId de la cible, null sans cible" },
                    },
                },
            },
            ["confidence"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("élevée", "moyenne", "faible") },
            ["uncertaintyReasons"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["rationale"] = new JsonObject { ["type"] = "string" },
            ["rejectedAlternativeId"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["rejectedAlternativeReason"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["risk"] = new JsonObject { ["type"] = new JsonArray("string", "null") },
            ["replaceIds"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" }, ["description"] = "mulligan seulement, sinon liste vide" },
        },
    };

    public string BuildRequestJson(string userPayload, bool stream = false)
    {
        var o = new JsonObject
        {
            ["model"] = _options.Model,
            ["reasoning"] = new JsonObject { ["effort"] = _options.ReasoningEffort },
            ["instructions"] = _instructions,
            ["input"] = userPayload,
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = "strategy_output",
                    ["strict"] = true,
                    ["schema"] = OutputSchema(),
                },
            },
            ["max_output_tokens"] = _options.MaxOutputTokens,
            ["store"] = false,
            ["prompt_cache_key"] = _options.PromptCacheKey,
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
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl.TrimEnd('/') + "/responses")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (stream) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    private static bool Transient(HttpStatusCode code) => code == HttpStatusCode.TooManyRequests || (int)code >= 500;

    public async Task<StrategyResult> ProposeWithPayloadAsync(string userPayload, CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new StrategyResult { Error = "OPENAI_API_KEY absente", Model = _options.Model };
        var body = BuildRequestJson(userPayload);
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string text = "";
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
        var latency = sw.Elapsed.TotalMilliseconds;
        using (response)
        {
            return Parse(text, (int)response.StatusCode, latency, retries);
        }
    }

    /// <summary>
    /// Appel en flux (server-sent events) : chaque fragment de texte est accumulé ; dès que actionId puis summary sont
    /// complets, <paramref name="onPartial"/> est appelé. Le résultat final (usage, coût) vient de l'événement de fin.
    /// </summary>
    public async Task<StrategyResult> ProposeWithPayloadStreamingAsync(string userPayload, Action<StrategyPartial>? onPartial, CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new StrategyResult { Error = "OPENAI_API_KEY absente", Model = _options.Model };
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
            // Erreur passagère annoncée dans le flux (« Our servers are currently overloaded », 26/10/2026 de 15 h 49 à 16 h 05)
            // avant tout texte : une nouvelle tentative, sans risque d'afficher deux fois un début de conseil.
            if (!transient || streamAttempt >= _options.MaxRetries) return result;
            retries++;
            await Task.Delay(_options.RetryBaseDelay * (streamAttempt + 1), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Erreur de flux qui vaut une nouvelle tentative : surcharge ou incident du serveur, jamais un crédit épuisé.</summary>
    private static readonly Regex TransientStreamError = new(@"overloaded|server_error|server error|temporarily unavailable|try again later|timed out", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Lit le flux ; « passagère » : erreur de surcharge reçue avant le premier fragment de texte.</summary>
    private async Task<(StrategyResult Result, bool Transient)> ReadStreamAsync(HttpResponseMessage response, Action<StrategyPartial>? onPartial, Stopwatch sw, int retries, CancellationToken cancellationToken)
    {
        var transientError = false;
        using (response)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var text = new StringBuilder();
            var data = new StringBuilder();
            double? firstToken = null, partialMs = null;
            StrategyPartial? last = null;

            StrategyResult? Handle(string json)
            {
                if (json == "[DONE]") return null;
                JsonNode? node;
                try { node = JsonNode.Parse(json); }
                catch (JsonException) { return null; }
                switch (node?["type"]?.GetValue<string>())
                {
                    case "response.output_text.delta":
                        text.Append(node["delta"]?.GetValue<string>());
                        firstToken ??= sw.Elapsed.TotalMilliseconds;
                        if (ExtractPartial(text.ToString()) is { } p && p != last)
                        {
                            last = p;
                            if (p.Summary is not null) partialMs ??= sw.Elapsed.TotalMilliseconds;
                            onPartial?.Invoke(p with { ElapsedMs = sw.Elapsed.TotalMilliseconds });
                        }
                        return null;
                    case "response.completed" or "response.incomplete" or "response.failed":
                        var final = node["response"]?.ToJsonString() ?? "{}";
                        return Parse(final, 200, sw.Elapsed.TotalMilliseconds, retries) with { FirstTokenMs = firstToken, PartialMs = partialMs };
                    case "error":
                        var message = node["message"]?.GetValue<string>() ?? node["error"]?["message"]?.GetValue<string>() ?? "erreur inconnue";
                        var overloaded = TransientStreamError.IsMatch(message) && !message.Contains("credit", StringComparison.OrdinalIgnoreCase) && !message.Contains("quota", StringComparison.OrdinalIgnoreCase);
                        transientError = overloaded && firstToken is null;
                        return new StrategyResult
                        {
                            Error = "flux : " + (overloaded ? $"OpenAI surchargé, erreur de leur côté (« {message} »)" : message),
                            Model = _options.Model, LatencyMs = sw.Elapsed.TotalMilliseconds, Retries = retries,
                        };
                    default:
                        return null;
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
                    if (done is not null) return (done, transientError);
                    continue;
                }
                if (line.StartsWith("data:", StringComparison.Ordinal))
                {
                    if (data.Length > 0) data.Append('\n');
                    data.Append(line.AsSpan(5).TrimStart());
                }
            }
            if (data.Length > 0 && Handle(data.ToString()) is { } tail) return (tail, transientError);

            // Flux fermé sans événement de fin : on tente de lire la sortie accumulée, sans usage ni coût.
            var latency = sw.Elapsed.TotalMilliseconds;
            try
            {
                var output = JsonSerializer.Deserialize<StrategyOutput>(text.ToString());
                if (output is not null)
                    return (new StrategyResult { Output = output, Model = _options.Model, LatencyMs = latency, Retries = retries, RawOutput = text.ToString(), FirstTokenMs = firstToken, PartialMs = partialMs }, false);
            }
            catch (JsonException) { /* sortie incomplète */ }
            return (new StrategyResult { Error = "flux interrompu avant la fin de la réponse", Model = _options.Model, LatencyMs = latency, Retries = retries, RawOutput = text.ToString() }, false);
        }
    }

    private static readonly Regex StringField = new(@"""(?<k>actionId|summary|why|lethal|planNextTurn)""\s*:\s*""(?<v>(?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    /// <summary>Champs déjà complets dans un début d'objet JSON (chaînes fermées seulement).</summary>
    public static StrategyPartial? ExtractPartial(string jsonPrefix)
    {
        var values = new Dictionary<string, string>();
        foreach (Match m in StringField.Matches(jsonPrefix))
        {
            if (values.ContainsKey(m.Groups["k"].Value)) continue;
            try { values[m.Groups["k"].Value] = JsonSerializer.Deserialize<string>("\"" + m.Groups["v"].Value + "\"") ?? ""; }
            catch (JsonException) { /* échappement incomplet */ }
        }
        if (!values.TryGetValue("actionId", out var action) || action.Length == 0) return null;
        return new StrategyPartial
        {
            ActionId = action,
            Summary = values.GetValueOrDefault("summary"),
            Why = values.GetValueOrDefault("why"),
            Lethal = values.GetValueOrDefault("lethal"),
            PlanNextTurn = values.GetValueOrDefault("planNextTurn"),
        };
    }

    /// <summary>Test de connexion : une requête minuscule, sans schéma, pour vérifier la clé, le modèle et la latence.</summary>
    public async Task<PingResult> PingAsync(CancellationToken cancellationToken)
    {
        var key = _apiKey();
        if (string.IsNullOrWhiteSpace(key)) return new PingResult(false, "OPENAI_API_KEY absente de l'environnement.", 0, null, null);
        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["reasoning"] = new JsonObject { ["effort"] = "low" },
            ["input"] = "Réponds uniquement : OK",
            ["max_output_tokens"] = 300,
            ["store"] = false,
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
            var cost = ModelPricing.Cost(model, Int(usage?["input_tokens"]), Int(usage?["input_tokens_details"]?["cached_tokens"]), Int(usage?["output_tokens"]));
            return new PingResult(true, $"{model} répond (HTTP {(int)response.StatusCode}) en {latency / 1000:0.0} s.", latency, model, cost);
        }
        catch (HttpRequestException ex)
        {
            return new PingResult(false, "réseau : " + ex.Message, sw.Elapsed.TotalMilliseconds, _options.Model, null);
        }
    }

    /// <summary>Lecture d'une réponse de la Responses API (public pour les tests).</summary>
    public StrategyResult Parse(string json, int httpStatus, double latencyMs, int retries)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return new StrategyResult { Error = $"HTTP {httpStatus} : réponse non JSON", Model = _options.Model, LatencyMs = latencyMs, Retries = retries }; }
        if (root?["error"] is JsonObject err)
            return new StrategyResult { Error = $"HTTP {httpStatus} : {err["message"]?.GetValue<string>()}", Model = _options.Model, LatencyMs = latencyMs, Retries = retries };

        var usage = root?["usage"];
        int Int(JsonNode? n) => n is null ? 0 : n.GetValue<int>();
        var input = Int(usage?["input_tokens"]);
        var cached = Int(usage?["input_tokens_details"]?["cached_tokens"]);
        var output = Int(usage?["output_tokens"]);
        var reasoning = Int(usage?["output_tokens_details"]?["reasoning_tokens"]);
        var model = root?["model"]?.GetValue<string>() ?? _options.Model;
        var cost = ModelPricing.Cost(model, input, cached, output) ?? ModelPricing.Cost(_options.Model, input, cached, output);
        var status = root?["status"]?.GetValue<string>();
        var common = new StrategyResult
        {
            Model = model,
            InputTokens = input,
            CachedInputTokens = cached,
            OutputTokens = output,
            ReasoningTokens = reasoning,
            CostUsd = cost,
            LatencyMs = latencyMs,
            Retries = retries,
            ResponseId = root?["id"]?.GetValue<string>(),
        };
        if (status != "completed")
        {
            var reason = root?["incomplete_details"]?["reason"]?.GetValue<string>() ?? status ?? "inconnu";
            return common with { Error = $"réponse incomplète : {reason}" };
        }
        var outputText = root?["output"]?.AsArray()
            .Where(o => o?["type"]?.GetValue<string>() == "message")
            .SelectMany(o => o!["content"]?.AsArray() ?? new JsonArray())
            .FirstOrDefault(c => c?["type"]?.GetValue<string>() == "output_text")?["text"]?.GetValue<string>();
        var refusal = root?["output"]?.AsArray()
            .SelectMany(o => o?["content"]?.AsArray() ?? new JsonArray())
            .FirstOrDefault(c => c?["type"]?.GetValue<string>() == "refusal")?["refusal"]?.GetValue<string>();
        if (outputText is null)
            return common with { Error = refusal is null ? "aucun texte de sortie" : $"refus : {refusal}" };
        try
        {
            var parsed = JsonSerializer.Deserialize<StrategyOutput>(outputText);
            return parsed is null ? common with { Error = "sortie vide", RawOutput = outputText } : common with { Output = parsed, RawOutput = outputText };
        }
        catch (JsonException ex)
        {
            return common with { Error = "sortie non conforme : " + ex.Message, RawOutput = outputText };
        }
    }
}
