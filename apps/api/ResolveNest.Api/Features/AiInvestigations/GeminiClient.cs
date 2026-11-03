using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ResolveNest.Api.Features.AiInvestigations;

public enum GeminiFailure
{
    NotConfigured,
    InvalidCredentials,
    ModelUnavailable,
    RateLimited,
    Timeout,
    ProviderUnavailable,
    RequestRejected,
    ResponseBlocked,
    EmptyResponse,
    TruncatedResponse,
    MalformedResponse,
}

public sealed class GeminiException(GeminiFailure failure, int? providerStatusCode = null, TimeSpan? retryAfter = null)
    : Exception($"Gemini request failed: {failure}.")
{
    public GeminiFailure Failure { get; } = failure;
    public int? ProviderStatusCode { get; } = providerStatusCode;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

public sealed record GeminiGeneration(
    string Text,
    string? FinishReason,
    string? ModelVersion,
    int? PromptTokens,
    int? OutputTokens,
    int? ThoughtTokens);

public sealed record GeminiPart(string Text);

public sealed record GeminiContent(string? Role, IReadOnlyList<GeminiPart> Parts);

public sealed record GeminiThinkingConfig(string ThinkingLevel);

public sealed record GeminiGenerationConfig(
    double Temperature,
    int MaxOutputTokens,
    int CandidateCount,
    string ResponseMimeType,
    JsonNode ResponseJsonSchema,
    GeminiThinkingConfig? ThinkingConfig);

public sealed record GeminiGenerateRequest(
    GeminiContent SystemInstruction,
    IReadOnlyList<GeminiContent> Contents,
    GeminiGenerationConfig GenerationConfig);

public sealed class GeminiClient(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
{
    private const string ApiKeyHeader = "x-goog-api-key";
    private const string JsonMimeType = "application/json";

    private static readonly JsonSerializerOptions RequestSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<string> BlockedFinishReasons = new(StringComparer.Ordinal)
    {
        "SAFETY", "RECITATION", "BLOCKLIST", "PROHIBITED_CONTENT", "SPII", "IMAGE_SAFETY", "LANGUAGE",
    };

    public async Task<GeminiGeneration> GenerateJsonAsync(
        string systemInstruction,
        string userContent,
        JsonNode responseSchema,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.HasApiKey)
        {
            throw new GeminiException(GeminiFailure.NotConfigured);
        }

        var requestBody = new GeminiGenerateRequest(
            new GeminiContent(null, [new GeminiPart(systemInstruction)]),
            [new GeminiContent("user", [new GeminiPart(userContent)])],
            new GeminiGenerationConfig(
                settings.Temperature,
                settings.MaxOutputTokens,
                1,
                JsonMimeType,
                responseSchema,
                string.IsNullOrWhiteSpace(settings.ThinkingLevel) ? null : new GeminiThinkingConfig(settings.ThinkingLevel)));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(settings.Model)}:generateContent");
        request.Headers.Add(ApiKeyHeader, settings.ApiKey);
        request.Content = JsonContent.Create(requestBody, options: RequestSerializerOptions);

        var responseText = await SendAsync(request, timeout.Token, cancellationToken);
        var generation = ParseGeneration(responseText);
        logger.LogInformation(
            "Gemini generation completed with model {ModelVersion}, finish reason {FinishReason}, prompt tokens {PromptTokens}, output tokens {OutputTokens}, thought tokens {ThoughtTokens}.",
            generation.ModelVersion, generation.FinishReason, generation.PromptTokens, generation.OutputTokens, generation.ThoughtTokens);
        return generation;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken timeoutToken, CancellationToken callerToken)
    {
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken);
            var body = await response.Content.ReadAsStringAsync(timeoutToken);
            if (response.IsSuccessStatusCode)
            {
                return body;
            }

            var failure = Classify(response.StatusCode, body, out var retryAfter);
            logger.LogWarning("Gemini request failed with HTTP {StatusCode}, classified as {Failure}.", (int)response.StatusCode, failure);
            throw new GeminiException(failure, (int)response.StatusCode, retryAfter);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            logger.LogWarning("Gemini request timed out.");
            throw new GeminiException(GeminiFailure.Timeout);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Gemini request failed before a response was received ({ErrorType}).", exception.HttpRequestError);
            throw new GeminiException(GeminiFailure.ProviderUnavailable);
        }
    }

    private static GeminiFailure Classify(HttpStatusCode statusCode, string body, out TimeSpan? retryAfter)
    {
        retryAfter = null;
        var error = TryReadError(body);
        switch (statusCode)
        {
            case HttpStatusCode.BadRequest when error.Reasons.Contains("API_KEY_INVALID"):
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                return GeminiFailure.InvalidCredentials;
            case HttpStatusCode.NotFound:
                return GeminiFailure.ModelUnavailable;
            case HttpStatusCode.TooManyRequests:
                retryAfter = error.RetryDelay;
                return GeminiFailure.RateLimited;
            case HttpStatusCode.RequestTimeout:
            case HttpStatusCode.GatewayTimeout:
                return GeminiFailure.Timeout;
            case >= HttpStatusCode.InternalServerError:
                return GeminiFailure.ProviderUnavailable;
            default:
                return GeminiFailure.RequestRejected;
        }
    }

    private static (HashSet<string> Reasons, TimeSpan? RetryDelay) TryReadError(string body)
    {
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        TimeSpan? retryDelay = null;
        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || !error.TryGetProperty("details", out var details)
                || details.ValueKind != JsonValueKind.Array)
            {
                return (reasons, retryDelay);
            }

            foreach (var detail in details.EnumerateArray())
            {
                if (detail.TryGetProperty("reason", out var reason) && reason.ValueKind == JsonValueKind.String)
                {
                    reasons.Add(reason.GetString()!);
                }

                if (detail.TryGetProperty("retryDelay", out var delay) && delay.ValueKind == JsonValueKind.String)
                {
                    retryDelay = ParseDuration(delay.GetString()!);
                }
            }
        }
        catch (JsonException)
        {
        }

        return (reasons, retryDelay);
    }

    private static TimeSpan? ParseDuration(string value) =>
        value.EndsWith('s')
        && double.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
        && seconds is >= 0 and <= 86_400
            ? TimeSpan.FromSeconds(Math.Ceiling(seconds))
            : null;

    private static GeminiGeneration ParseGeneration(string body)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new GeminiException(GeminiFailure.MalformedResponse);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new GeminiException(GeminiFailure.MalformedResponse);
            }

            if (root.TryGetProperty("promptFeedback", out var feedback) && feedback.TryGetProperty("blockReason", out _))
            {
                throw new GeminiException(GeminiFailure.ResponseBlocked);
            }

            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            {
                throw new GeminiException(GeminiFailure.EmptyResponse);
            }

            var candidate = candidates[0];
            var finishReason = OptionalString(candidate, "finishReason");
            if (finishReason is not null && BlockedFinishReasons.Contains(finishReason))
            {
                throw new GeminiException(GeminiFailure.ResponseBlocked);
            }

            if (finishReason == "MAX_TOKENS")
            {
                throw new GeminiException(GeminiFailure.TruncatedResponse);
            }

            var text = ReadAnswerText(candidate);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new GeminiException(GeminiFailure.EmptyResponse);
            }

            root.TryGetProperty("usageMetadata", out var usage);
            return new GeminiGeneration(
                text,
                finishReason,
                OptionalString(root, "modelVersion"),
                OptionalInt(usage, "promptTokenCount"),
                OptionalInt(usage, "candidatesTokenCount"),
                OptionalInt(usage, "thoughtsTokenCount"));
        }
    }

    private static string ReadAnswerText(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Object
            || !content.TryGetProperty("parts", out var parts)
            || parts.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            var isThought = part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True;
            if (!isThought && part.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String)
            {
                text.Append(value.GetString());
            }
        }

        return text.ToString();
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? OptionalInt(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
            ? number
            : null;
}
