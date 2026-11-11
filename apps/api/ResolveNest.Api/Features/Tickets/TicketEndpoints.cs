using Microsoft.AspNetCore.Http.HttpResults;
using Neo4j.Driver;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.Tickets;

public static class TicketEndpoints
{
    public static IServiceCollection AddTickets(this IServiceCollection services) => services.AddSingleton<TicketListService>();

    public static IEndpointRouteBuilder MapTickets(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tickets", GetTicketsAsync);
        return endpoints;
    }

    public static async Task<Results<Ok<TicketListResponse>, ValidationProblem, ProblemHttpResult>> GetTicketsAsync(
        string? page, string? pageSize, string? q, string? status, string? sort,
        TicketListService tickets, ILogger<TicketListService> logger, CancellationToken cancellationToken)
    {
        var errors = TicketListRequest.Parse(page, pageSize, q, status, sort, out var request);
        if (errors.Count > 0) return TypedResults.ValidationProblem(errors, title: "Invalid ticket list request");
        try
        {
            return TypedResults.Ok(await tickets.ListAsync(request, cancellationToken));
        }
        catch (Neo4jException exception)
        {
            logger.LogError("Ticket listing failed with a database error ({ErrorType}, code {ErrorCode}).", exception.GetType().Name, exception.Code);
            return TypedResults.Problem(title: "Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvestigationResultException exception)
        {
            logger.LogError("Ticket listing returned an unexpected result shape: {Problem}", exception.Message);
            return TypedResults.Problem(title: "Ticket listing failed", statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
