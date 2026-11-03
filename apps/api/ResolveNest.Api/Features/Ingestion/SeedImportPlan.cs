namespace ResolveNest.Api.Features.Ingestion;

public enum IngestionIssueSeverity
{
    Warning,
    Error,
}

public sealed record IngestionIssue(IngestionIssueSeverity Severity, string SourceRecord, string Message);

public sealed record GraphRelationship(string StartId, string EndId, IReadOnlyDictionary<string, object?> Properties);

public sealed record SourceRecordCounts(int Customers, int Features, int Releases, int TicketRecords, int UniqueTickets, int Bugs);

public sealed record SeedImportPlan(
    SourceRecordCounts SourceCounts,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Customers,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Features,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Releases,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Bugs,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Tickets,
    IReadOnlyList<GraphRelationship> Reported,
    IReadOnlyList<GraphRelationship> AboutFeature,
    IReadOnlyList<GraphRelationship> ConfirmedAs,
    IReadOnlyList<GraphRelationship> Affects,
    IReadOnlyList<GraphRelationship> IntroducedIn,
    IReadOnlyList<GraphRelationship> FixedIn,
    IReadOnlyList<string> SourceSystems,
    IReadOnlyList<IngestionIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == IngestionIssueSeverity.Error);

    public IReadOnlyList<(string Label, int Count)> NodeCounts =>
    [
        ("Customer", Customers.Count),
        ("Feature", Features.Count),
        ("Release", Releases.Count),
        ("Bug", Bugs.Count),
        ("SupportTicket", Tickets.Count),
    ];

    public IReadOnlyList<(string Type, int Count)> RelationshipCounts =>
    [
        ("REPORTED", Reported.Count),
        ("ABOUT_FEATURE", AboutFeature.Count),
        ("CONFIRMED_AS", ConfirmedAs.Count),
        ("AFFECTS", Affects.Count),
        ("INTRODUCED_IN", IntroducedIn.Count),
        ("FIXED_IN", FixedIn.Count),
    ];
}
