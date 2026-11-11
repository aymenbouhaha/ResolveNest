using Neo4j.Driver;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.Tickets;

public sealed record TicketCustomerSummary(string Id, string? Name);
public sealed record TicketSummary(string Id, string? SourceId, string? Subject, string? Status, string? Priority, string? ReportedVersion, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt, TicketCustomerSummary? Customer);
public sealed record TicketListResponse(IReadOnlyList<TicketSummary> Items, int Page, int PageSize, long TotalCount);

public sealed class TicketListService(IDriver driver)
{
    private const string Filter = "MATCH (t:SupportTicket) WHERE ($q IS NULL OR toLower(coalesce(t.sourceId, '')) CONTAINS $q OR toLower(coalesce(t.subject, '')) CONTAINS $q) AND ($status IS NULL OR t.status = $status)";

    public async Task<TicketListResponse> ListAsync(TicketListRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ordering = TicketListRequest.Orderings[request.Sort];
        var parameters = new Dictionary<string, object?> { ["q"] = request.Q?.ToLowerInvariant(), ["status"] = request.Status, ["offset"] = request.Offset, ["limit"] = request.PageSize };
        await using var session = driver.AsyncSession(config => config.WithDefaultAccessMode(AccessMode.Read));
        return await session.ExecuteReadAsync(async transaction =>
        {
            var countCursor = await transaction.RunAsync(Filter + " RETURN count(t) AS totalCount", parameters);
            var counts = await countCursor.ToListAsync(cancellationToken);
            var total = (long)counts.Single()["totalCount"];
            var cursor = await transaction.RunAsync(Filter + " WITH t ORDER BY " + ordering + " SKIP $offset LIMIT $limit CALL (t) { OPTIONAL MATCH (c:Customer)-[:REPORTED]->(t) WITH c ORDER BY c.id RETURN head(collect(c)) AS customer } RETURN t { .id, .sourceId, .subject, .status, .priority, .reportedVersion, .createdAt, .updatedAt } AS ticket, customer { .id, .name } AS customer ORDER BY " + ordering, parameters);
            var records = await cursor.ToListAsync(cancellationToken);
            return new TicketListResponse(records.Select(Map).ToList(), request.Page, request.PageSize, total);
        }, config => config.WithTimeout(TimeSpan.FromSeconds(15)));
    }

    public static TicketSummary Map(IRecord record) => Map(GraphResultMap.FromRecord(record, "ticket-list"));

    public static TicketSummary Map(GraphResultMap row)
    {
        var ticket = row.OptionalMap("ticket") ?? throw new InvestigationResultException("Ticket list returned no ticket.");
        var customer = row.OptionalMap("customer");
        return new(ticket.RequiredString("id"), ticket.OptionalString("sourceId"), ticket.OptionalString("subject"), ticket.OptionalString("status"), ticket.OptionalString("priority"), ticket.OptionalString("reportedVersion"), ticket.OptionalDateTime("createdAt"), ticket.OptionalDateTime("updatedAt"), customer is null ? null : new(customer.RequiredString("id"), customer.OptionalString("name")));
    }
}
