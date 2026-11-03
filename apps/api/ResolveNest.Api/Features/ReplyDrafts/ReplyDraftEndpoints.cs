using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Neo4j.Driver;
using ResolveNest.Api.Features.AiInvestigations;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.ReplyDrafts;

public static partial class ReplyDraftEndpoints
{
    public const string PreviewRoute = "/api/investigations/tickets/{ticketId}/reply-drafts/preview";
    public const string ApproveRoute = "/api/investigations/tickets/{ticketId}/reply-drafts/approve";
    public const int MaxGuidanceCharacters = 500;
    public const int MinFinalTextCharacters = 40;
    public const int MaxFinalTextCharacters = 4_000;
    public const int MaxPreviewBodyBytes = 4_096;
    public const int MaxApprovalBodyBytes = 65_536;

    private static readonly JsonSerializerOptions RequestSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static IServiceCollection AddReplyDrafts(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ReplyPreviewTokens>();
        services.AddSingleton<IReplyDraftStore, Neo4jReplyDraftStore>();
        services.AddScoped<ReplyDraftService>();
        return services;
    }

    public static IEndpointRouteBuilder MapReplyDrafts(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(PreviewRoute, PreviewAsync);
        endpoints.MapPost(ApproveRoute, ApproveAsync);
        return endpoints;
    }

    public static async Task<Results<Ok<ReplyDraftPreview>, ValidationProblem, ProblemHttpResult>> PreviewAsync(
        string ticketId,
        HttpRequest request,
        HttpResponse response,
        ReplyDraftService service,
        ILogger<ReplyDraftService> logger,
        CancellationToken cancellationToken)
    {
        var errors = TicketErrors(ticketId);
        var (body, bodyError) = await ReadOptionalBodyAsync<ReplyDraftPreviewRequest>(request, MaxPreviewBodyBytes, cancellationToken);
        if (bodyError is not null)
        {
            errors["body"] = [bodyError];
        }

        var guidance = body?.ReviewerGuidance?.Trim();
        if (guidance is { Length: > 0 } && (guidance.Length > MaxGuidanceCharacters || HasControlCharacters(guidance)))
        {
            errors["reviewerGuidance"] = [$"reviewerGuidance must be at most {MaxGuidanceCharacters} characters without control characters."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors, title: "Invalid reply draft preview request");
        }

        try
        {
            var preview = await service.PreviewAsync(ticketId, string.IsNullOrEmpty(guidance) ? null : guidance, cancellationToken);
            return preview is null ? TicketNotFound(ticketId) : TypedResults.Ok(preview);
        }
        catch (Neo4jException exception)
        {
            logger.LogError("Reply draft preview for {TicketId} failed with a database error ({ErrorType}, code {ErrorCode}).", ticketId, exception.GetType().Name, exception.Code);
            return TypedResults.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvestigationResultException exception)
        {
            logger.LogError("Reply draft preview for {TicketId} received an unexpected investigation shape: {Problem}", ticketId, exception.Message);
            return TypedResults.Problem(title: "Investigation failed", statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (EvidencePayloadTooLargeException exception)
        {
            logger.LogError("Reply draft preview for {TicketId} was not sent: {Problem}", ticketId, exception.Message);
            return AiInvestigationEndpoints.Problem("Evidence too large", "The investigation evidence exceeds the AI request size limit.", StatusCodes.Status500InternalServerError, "evidenceTooLarge");
        }
        catch (GeminiException exception)
        {
            logger.LogWarning("Reply draft preview for {TicketId} failed: {Failure} (provider HTTP {StatusCode}).", ticketId, exception.Failure, exception.ProviderStatusCode);
            if (exception.RetryAfter is { } retryAfter)
            {
                response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            return AiInvestigationEndpoints.ProviderProblem(exception.Failure);
        }
        catch (AiAnswerRejectedException exception)
        {
            return AiInvestigationEndpoints.Problem(
                "Reply draft rejected",
                "The generated reply draft failed evidence validation and was not returned.",
                StatusCodes.Status502BadGateway,
                "draftRejected",
                exception.Reasons);
        }
    }

    public static async Task<Results<Created<ReplyDraftApprovalResult>, Ok<ReplyDraftApprovalResult>, ValidationProblem, ProblemHttpResult>> ApproveAsync(
        string ticketId,
        HttpRequest request,
        ReplyDraftService service,
        ILogger<ReplyDraftService> logger,
        CancellationToken cancellationToken)
    {
        var errors = TicketErrors(ticketId);
        var (body, bodyError) = await ReadRequiredBodyAsync<ReplyDraftApprovalRequest>(request, MaxApprovalBodyBytes, cancellationToken);
        if (bodyError is not null)
        {
            errors["body"] = [bodyError];
        }
        else
        {
            ValidateApproval(body!, errors);
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors, title: "Invalid reply draft approval");
        }

        try
        {
            var result = await service.ApproveAsync(ticketId, body!.PreviewToken!, body.FinalText!, cancellationToken);
            if (result is null)
            {
                return TicketNotFound(ticketId);
            }

            return result.AlreadyApproved ? TypedResults.Ok(result) : TypedResults.Created((string?)null, result);
        }
        catch (PreviewTokenException exception)
        {
            return exception.Failure == PreviewTokenFailure.Expired
                ? AiInvestigationEndpoints.Problem("Preview expired", "The preview token has expired. Generate a new preview.", StatusCodes.Status400BadRequest, "previewTokenExpired")
                : AiInvestigationEndpoints.Problem("Invalid preview token", "The preview token is invalid or was not issued by this server.", StatusCodes.Status400BadRequest, "invalidPreviewToken");
        }
        catch (PreviewTicketMismatchException)
        {
            return AiInvestigationEndpoints.Problem("Preview ticket mismatch", "The preview token was issued for a different ticket.", StatusCodes.Status400BadRequest, "previewTicketMismatch");
        }
        catch (ReplyDraftConflictException)
        {
            return AiInvestigationEndpoints.Problem("Reply draft already approved", "This preview was already approved with different text. Generate a new preview to approve another reply.", StatusCodes.Status409Conflict, "replyDraftConflict");
        }
        catch (ReplyDraftStorageNotInitializedException)
        {
            logger.LogError("Reply draft approval for {TicketId} refused: the ReplyDraft uniqueness constraint is missing.", ticketId);
            return AiInvestigationEndpoints.Problem("Reply draft storage not initialized", "Approved reply drafts cannot be stored until the database schema is applied.", StatusCodes.Status503ServiceUnavailable, "replyDraftStorageNotInitialized");
        }
        catch (Neo4jException exception)
        {
            logger.LogError("Reply draft approval for {TicketId} failed with a database error ({ErrorType}, code {ErrorCode}). Nothing was committed.", ticketId, exception.GetType().Name, exception.Code);
            return TypedResults.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static void ValidateApproval(ReplyDraftApprovalRequest body, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(body.PreviewToken))
        {
            errors["previewToken"] = ["previewToken is required."];
        }

        if (body.Approved is not true)
        {
            errors["approved"] = ["approved must be true. Approval is never inferred."];
        }

        var finalText = body.FinalText?.Trim();
        if (string.IsNullOrEmpty(finalText))
        {
            errors["finalText"] = ["finalText is required."];
        }
        else if (finalText.Length < MinFinalTextCharacters || finalText.Length > MaxFinalTextCharacters)
        {
            errors["finalText"] = [$"finalText must be between {MinFinalTextCharacters} and {MaxFinalTextCharacters} characters."];
        }
        else if (HasControlCharacters(finalText))
        {
            errors["finalText"] = ["finalText must not contain control characters."];
        }
        else if (InternalArtifact().IsMatch(finalText))
        {
            errors["finalText"] = ["finalText must not contain citation IDs or unfilled template placeholders."];
        }
    }

    private static Dictionary<string, string[]> TicketErrors(string ticketId)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!CanonicalTicketId.IsValid(ticketId))
        {
            errors["ticketId"] = [$"Ticket ID must be a canonical ID in the format {CanonicalTicketId.Format}, for example ticket:TCK-1046."];
        }

        return errors;
    }

    private static ProblemHttpResult TicketNotFound(string ticketId) =>
        TypedResults.Problem(title: "Ticket not found", detail: $"No support ticket with ID '{ticketId}' exists.", statusCode: StatusCodes.Status404NotFound);

    private static bool HasControlCharacters(string text) =>
        text.Any(character => char.IsControl(character) && character is not '\n' and not '\r' and not '\t');

    private static async Task<(T? Body, string? Error)> ReadOptionalBodyAsync<T>(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
        where T : class =>
        request.ContentLength is 0 || (request.ContentLength is null && !request.HasJsonContentType())
            ? (null, null)
            : await ReadRequiredBodyAsync<T>(request, maxBytes, cancellationToken);

    private static async Task<(T? Body, string? Error)> ReadRequiredBodyAsync<T>(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
        where T : class
    {
        if (!request.HasJsonContentType())
        {
            return (null, "The request body must be JSON with Content-Type application/json.");
        }

        if (request.ContentLength > maxBytes)
        {
            return (null, $"The request body must be at most {maxBytes} bytes.");
        }

        try
        {
            using var buffer = new MemoryStream();
            await request.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > maxBytes)
            {
                return (null, $"The request body must be at most {maxBytes} bytes.");
            }

            var body = JsonSerializer.Deserialize<T>(buffer.ToArray(), RequestSerializerOptions);
            return body is null ? (null, "The request body must be a JSON object.") : (body, null);
        }
        catch (JsonException)
        {
            return (null, "The request body is not valid JSON for this operation.");
        }
    }

    [GeneratedRegex(@"\b(rec|rel):\S+|\{\{|\}\}|\[(insert|customer|name|todo)[^\]]*\]|<insert", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InternalArtifact();
}
