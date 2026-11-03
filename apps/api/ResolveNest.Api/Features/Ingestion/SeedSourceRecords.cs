namespace ResolveNest.Api.Features.Ingestion;

public sealed record SourceLocation(string File, string Pointer)
{
    public override string ToString() => $"{File}#{Pointer}";
}

public sealed record CsvSourceRow(SourceLocation Location, IReadOnlyDictionary<string, string> Values)
{
    public string? Value(string column) =>
        Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

public sealed record LocatedRecord<TRecord>(SourceLocation Location, TRecord Record, bool Synthetic);

public sealed record JsonSourceDocument<TRecord>(bool? Synthetic, string? SourceSystem, string? ExportedAt, IReadOnlyList<TRecord>? Records);

public sealed record ConfirmedBugLinkSource(string? BugRef, string? ConfirmedBy, string? ConfirmedAt);

public sealed record TicketSource(
    string? Id,
    string? SourceSystem,
    string? SourceId,
    string? CustomerRef,
    string? Subject,
    string? Description,
    string? Status,
    string? Priority,
    string? Channel,
    string? ReportedVersion,
    IReadOnlyList<string>? FeatureRefs,
    IReadOnlyList<ConfirmedBugLinkSource>? ConfirmedBugLinks,
    string? InternalNotes,
    string? Resolution,
    string? CreatedAt,
    string? UpdatedAt,
    string? ResolvedAt);

public sealed record BugSource(
    string? Id,
    string? SourceSystem,
    string? SourceId,
    string? Title,
    string? Description,
    string? Status,
    string? Severity,
    IReadOnlyList<string>? FeatureRefs,
    string? IntroducedIn,
    string? IntroducedInEvidence,
    string? FixedIn,
    string? FixEvidence,
    string? Workaround,
    string? CreatedAt,
    string? UpdatedAt);

public sealed record SeedSourceSet(
    IReadOnlyList<CsvSourceRow> Customers,
    IReadOnlyList<CsvSourceRow> Features,
    IReadOnlyList<CsvSourceRow> Releases,
    IReadOnlyList<LocatedRecord<TicketSource>> Tickets,
    IReadOnlyList<LocatedRecord<BugSource>> Bugs);

public sealed class SeedSourceException(string message, Exception? innerException = null) : Exception(message, innerException);
