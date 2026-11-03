using System.Text.RegularExpressions;
using Neo4j.Driver;

namespace ResolveNest.Api.Features.Investigations;

public static partial class CanonicalTicketId
{
    public const string Format = "ticket:TCK-<digits>";

    public static bool IsValid(string? ticketId) => ticketId is not null && Pattern().IsMatch(ticketId);

    [GeneratedRegex(@"^ticket:TCK-[0-9]{1,9}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

public sealed class TicketInvestigationService(IDriver driver, InvestigationQueryCatalog queries)
{
    private static readonly TimeSpan ReadTransactionTimeout = TimeSpan.FromSeconds(15);

    public async Task<TicketInvestigationResponse?> InvestigateAsync(string ticketId, bool includeContent, CancellationToken cancellationToken)
    {
        if (!CanonicalTicketId.IsValid(ticketId))
        {
            throw new ArgumentException($"Ticket ID must match {CanonicalTicketId.Format}.", nameof(ticketId));
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var session = driver.AsyncSession(config => config.WithDefaultAccessMode(AccessMode.Read));
        return await session.ExecuteReadAsync(
            transaction => ReadInvestigationAsync(transaction, ticketId, includeContent, cancellationToken),
            config => config.WithTimeout(ReadTransactionTimeout));
    }

    private async Task<TicketInvestigationResponse?> ReadInvestigationAsync(
        IAsyncQueryRunner transaction,
        string ticketId,
        bool includeContent,
        CancellationToken cancellationToken)
    {
        var ticketParameters = new Dictionary<string, object?> { ["ticketId"] = ticketId };
        var evidenceRow = await ReadSingleRowAsync(transaction, queries.TicketEvidence, InvestigationQueryCatalog.TicketEvidenceName, ticketParameters, cancellationToken);
        if (!evidenceRow.RequiredBool("found"))
        {
            return null;
        }

        var candidatesRow = await ReadSingleRowAsync(transaction, queries.TicketCandidates, InvestigationQueryCatalog.TicketCandidatesName, ticketParameters, cancellationToken);
        var knowledgeParameters = new Dictionary<string, object?> { ["ticketId"] = ticketId, ["includeContent"] = includeContent };
        var knowledgeRow = await ReadSingleRowAsync(transaction, queries.TicketKnowledge, InvestigationQueryCatalog.TicketKnowledgeName, knowledgeParameters, cancellationToken);

        if (!candidatesRow.RequiredBool("found") || !knowledgeRow.RequiredBool("found"))
        {
            throw new InvestigationResultException("Investigation queries disagree on whether the ticket exists.");
        }

        return TicketInvestigationMapper.Combine(evidenceRow, candidatesRow, knowledgeRow, includeContent);
    }

    private static async Task<GraphResultMap> ReadSingleRowAsync(
        IAsyncQueryRunner transaction,
        string query,
        string queryName,
        IDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.RunAsync(query, parameters);
        var records = await cursor.ToListAsync(cancellationToken);
        if (records.Count != 1)
        {
            throw new InvestigationResultException($"Investigation query {queryName} returned {records.Count} rows instead of exactly one.");
        }

        return GraphResultMap.FromRecord(records[0], queryName);
    }
}
