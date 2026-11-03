using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Neo4j.Driver;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.AiInvestigations;

public static class AiInvestigationEndpoints
{
    public const string AskRoute = "/api/investigations/tickets/{ticketId}/ask";
    public const int MinQuestionCharacters = 5;
    public const int MaxQuestionCharacters = 1_000;
    public const int MaxRequestBodyBytes = 8_192;

    private static readonly JsonSerializerOptions RequestSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    public static IServiceCollection AddAiInvestigations(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));
        services.AddHttpClient<GeminiClient>((serviceProvider, client) =>
        {
            var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GeminiOptions>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds + 15);
        });
        services.AddScoped<AiInvestigationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapAiInvestigations(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(AskRoute, AskAsync);
        return endpoints;
    }

    public static async Task<Results<Ok<AiInvestigationAnswer>, ValidationProblem, ProblemHttpResult>> AskAsync(
        string ticketId,
        HttpRequest request,
        HttpResponse response,
        AiInvestigationService service,
        ILogger<AiInvestigationService> logger,
        CancellationToken cancellationToken)
    {
        var (question, errors) = await ReadQuestionAsync(ticketId, request, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors, title: "Invalid AI investigation request");
        }

        try
        {
            var answer = await service.AskAsync(ticketId, question!, cancellationToken);
            return answer is null
                ? TypedResults.Problem(title: "Ticket not found", detail: $"No support ticket with ID '{ticketId}' exists.", statusCode: StatusCodes.Status404NotFound)
                : TypedResults.Ok(answer);
        }
        catch (Neo4jException exception)
        {
            logger.LogError("AI investigation for {TicketId} failed with a database error ({ErrorType}, code {ErrorCode}).", ticketId, exception.GetType().Name, exception.Code);
            return TypedResults.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvestigationResultException exception)
        {
            logger.LogError("AI investigation for {TicketId} received an unexpected investigation shape: {Problem}", ticketId, exception.Message);
            return TypedResults.Problem(title: "Investigation failed", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (EvidencePayloadTooLargeException exception)
        {
            logger.LogError("AI investigation for {TicketId} was not sent: {Problem}", ticketId, exception.Message);
            return Problem("Evidence too large", "The investigation evidence exceeds the AI request size limit.", StatusCodes.Status500InternalServerError, "evidenceTooLarge");
        }
        catch (GeminiException exception)
        {
            logger.LogWarning("AI investigation for {TicketId} failed: {Failure} (provider HTTP {StatusCode}).", ticketId, exception.Failure, exception.ProviderStatusCode);
            if (exception.RetryAfter is { } retryAfter)
            {
                response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            return ProviderProblem(exception.Failure);
        }
        catch (AiAnswerRejectedException exception)
        {
            return Problem(
                "AI answer rejected",
                "The AI answer failed evidence validation and was not returned.",
                StatusCodes.Status502BadGateway,
                "answerRejected",
                exception.Reasons);
        }
    }

    private static async Task<(string? Question, Dictionary<string, string[]> Errors)> ReadQuestionAsync(string ticketId, HttpRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!CanonicalTicketId.IsValid(ticketId))
        {
            errors["ticketId"] = [$"Ticket ID must be a canonical ID in the format {CanonicalTicketId.Format}, for example ticket:TCK-1046."];
        }

        if (!request.HasJsonContentType())
        {
            errors["body"] = ["The request body must be JSON with Content-Type application/json."];
            return (null, errors);
        }

        if (request.ContentLength > MaxRequestBodyBytes)
        {
            errors["body"] = [$"The request body must be at most {MaxRequestBodyBytes} bytes."];
            return (null, errors);
        }

        AskInvestigationRequest? body;
        try
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaxRequestBodyBytes)
            {
                errors["body"] = [$"The request body must be at most {MaxRequestBodyBytes} bytes."];
                return (null, errors);
            }

            body = JsonSerializer.Deserialize<AskInvestigationRequest>(buffer.ToArray(), RequestSerializerOptions);
        }
        catch (JsonException)
        {
            errors["body"] = ["The request body must be a JSON object with a single string property 'question'."];
            return (null, errors);
        }

        var questionError = ValidateQuestion(body?.Question);
        if (questionError is not null)
        {
            errors["question"] = [questionError];
        }

        return (body?.Question?.Trim(), errors);
    }

    private static string? ValidateQuestion(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return "question is required.";
        }

        var trimmed = question.Trim();
        if (trimmed.Length < MinQuestionCharacters || trimmed.Length > MaxQuestionCharacters)
        {
            return $"question must be between {MinQuestionCharacters} and {MaxQuestionCharacters} characters.";
        }

        return trimmed.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t')
            ? "question must not contain control characters."
            : null;
    }

    public static ProblemHttpResult ProviderProblem(GeminiFailure failure) => failure switch
    {
        GeminiFailure.NotConfigured => Problem("AI assistant not configured", "The AI provider credentials are not configured on the server.", StatusCodes.Status503ServiceUnavailable, "aiNotConfigured"),
        GeminiFailure.InvalidCredentials => Problem("AI provider authentication failed", "The AI provider rejected the server's credentials.", StatusCodes.Status502BadGateway, "aiAuthenticationFailed"),
        GeminiFailure.ModelUnavailable => Problem("AI model unavailable", "The configured AI model is not available to this project.", StatusCodes.Status502BadGateway, "aiModelUnavailable"),
        GeminiFailure.RateLimited => Problem("AI provider rate limit reached", "The AI provider's rate limit or quota was reached. Try again later.", StatusCodes.Status429TooManyRequests, "aiRateLimited"),
        GeminiFailure.Timeout => Problem("AI provider timed out", "The AI provider did not respond in time.", StatusCodes.Status504GatewayTimeout, "aiTimeout"),
        GeminiFailure.ResponseBlocked => Problem("AI response blocked", "The AI provider's safety filters blocked the request or response.", StatusCodes.Status422UnprocessableEntity, "aiResponseBlocked"),
        GeminiFailure.EmptyResponse => Problem("AI response empty", "The AI provider returned no answer.", StatusCodes.Status502BadGateway, "aiEmptyResponse"),
        GeminiFailure.TruncatedResponse => Problem("AI response truncated", "The AI provider stopped before completing the answer.", StatusCodes.Status502BadGateway, "aiTruncatedResponse"),
        GeminiFailure.MalformedResponse => Problem("AI response malformed", "The AI provider returned an unreadable response.", StatusCodes.Status502BadGateway, "aiMalformedResponse"),
        GeminiFailure.RequestRejected => Problem("AI request rejected", "The AI provider rejected the request.", StatusCodes.Status502BadGateway, "aiRequestRejected"),
        _ => Problem("AI provider unavailable", "The AI provider could not be reached.", StatusCodes.Status502BadGateway, "aiProviderUnavailable"),
    };

    public static ProblemHttpResult Problem(string title, string detail, int statusCode, string reason, IReadOnlyList<string>? reasons = null)
    {
        var extensions = new Dictionary<string, object?> { ["reason"] = reason };
        if (reasons is not null)
        {
            extensions["validationFailures"] = reasons;
        }

        return TypedResults.Problem(title: title, detail: detail, statusCode: statusCode, extensions: extensions);
    }
}
