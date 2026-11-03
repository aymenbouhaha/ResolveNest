namespace ResolveNest.Api.Features.Ingestion;

public sealed class SeedIngestionSettings
{
    public const string SectionName = "SeedIngestion";

    public List<DocumentedUnresolvedBugReference> DocumentedUnresolvedBugReferences { get; init; } = [];
}

public sealed class DocumentedUnresolvedBugReference
{
    public string TicketId { get; init; } = string.Empty;

    public string BugRef { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
