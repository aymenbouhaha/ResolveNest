using Microsoft.AspNetCore.Http.HttpResults;
using Neo4j.Driver;

namespace ResolveNest.Api.Features.Investigations;

public static class InvestigationEndpoints
{
    public const string TicketInvestigationRoute = "/api/investigations/tickets/{ticketId}";

    public static IServiceCollection AddInvestigations(this IServiceCollection services)
    {
        services.AddSingleton<InvestigationQueryCatalog>();
        services.AddSingleton<TicketInvestigationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapInvestigations(this IEndpointRouteBuilder endpoints)
    {
        endpoints.ServiceProvider.GetRequiredService<InvestigationQueryCatalog>();
        endpoints.MapGet(TicketInvestigationRoute, GetTicketInvestigationAsync);
        return endpoints;
    }

    public static async Task<Results<Ok<TicketInvestigationResponse>, ValidationProblem, ProblemHttpResult>> GetTicketInvestigationAsync(
        string ticketId,
        string? includeContent,
        TicketInvestigationService investigations,
        ILogger<TicketInvestigationService> logger,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRequest(ticketId, includeContent, out var includeArticleContent);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors, title: "Invalid investigation request");
        }

        try
        {
            var investigation = await investigations.InvestigateAsync(ticketId, includeArticleContent, cancellationToken);
            return investigation is null
                ? TypedResults.Problem(title: "Ticket not found", detail: $"No support ticket with ID '{ticketId}' exists.", statusCode: StatusCodes.Status404NotFound)
                : TypedResults.Ok(investigation);
        }
        catch (Neo4jException exception)
        {
            logger.LogError("Ticket investigation for {TicketId} failed with a database error ({ErrorType}, code {ErrorCode}).", ticketId, exception.GetType().Name, exception.Code);
            return TypedResults.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvestigationResultException exception)
        {
            logger.LogError("Ticket investigation for {TicketId} returned an unexpected result shape: {Problem}", ticketId, exception.Message);
            return TypedResults.Problem(title: "Investigation failed", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static Dictionary<string, string[]> ValidateRequest(string ticketId, string? includeContent, out bool includeArticleContent)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        includeArticleContent = false;

        if (!CanonicalTicketId.IsValid(ticketId))
        {
            errors["ticketId"] = [$"Ticket ID must be a canonical ID in the format {CanonicalTicketId.Format}, for example ticket:TCK-1046."];
        }

        if (includeContent is not null && !bool.TryParse(includeContent, out includeArticleContent))
        {
            errors["includeContent"] = ["includeContent must be true or false."];
        }

        return errors;
    }
}
