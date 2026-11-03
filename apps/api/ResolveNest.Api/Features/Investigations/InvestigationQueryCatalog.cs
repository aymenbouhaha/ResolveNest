namespace ResolveNest.Api.Features.Investigations;

public static class InvestigationQueryLimits
{
    public const int CandidateBugs = 25;
    public const int ConfirmedTicketsPerCandidate = 25;
    public const int RelatedTickets = 50;
}

public sealed class InvestigationQueryCatalog
{
    public const string ResourcePrefix = "ResolveNest.Api.Queries.";
    public const string TicketEvidenceName = "ticket-evidence.cypher";
    public const string TicketCandidatesName = "ticket-candidates.cypher";
    public const string TicketKnowledgeName = "ticket-knowledge.cypher";

    public InvestigationQueryCatalog()
    {
        TicketEvidence = LoadEmbeddedQuery(TicketEvidenceName);
        TicketCandidates = LoadEmbeddedQuery(TicketCandidatesName);
        TicketKnowledge = LoadEmbeddedQuery(TicketKnowledgeName);
    }

    public string TicketEvidence { get; }
    public string TicketCandidates { get; }
    public string TicketKnowledge { get; }

    public static string LoadEmbeddedQuery(string fileName)
    {
        var resourceName = ResourcePrefix + fileName;
        using var stream = typeof(InvestigationQueryCatalog).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded investigation query '{resourceName}' is missing.");
        using var reader = new StreamReader(stream);
        var query = reader.ReadToEnd();
        return string.IsNullOrWhiteSpace(query)
            ? throw new InvalidOperationException($"Embedded investigation query '{resourceName}' is empty.")
            : query;
    }
}
