using System.Text.Json.Serialization;

namespace ResolveNest.Api.Features.Investigations;

[JsonConverter(typeof(JsonStringEnumConverter<EvidenceClassification>))]
public enum EvidenceClassification
{
    [JsonStringEnumMemberName("confirmed")]
    Confirmed,

    [JsonStringEnumMemberName("documented")]
    Documented,

    [JsonStringEnumMemberName("candidate")]
    Candidate,

    [JsonStringEnumMemberName("context")]
    Context,
}

public sealed record TicketInvestigationResponse(
    InvestigatedTicket Ticket,
    ReportingCustomer? Customer,
    IReadOnlyList<TicketFeature> Features,
    IReadOnlyList<ConfirmedBug> ConfirmedBugs,
    IReadOnlyList<CandidateBug> CandidateBugs,
    IReadOnlyList<RelatedTicket> RelatedTickets,
    IReadOnlyList<KnowledgeArticleEvidence> KnowledgeArticles,
    InvestigationResultCounts Counts);

public sealed record InvestigatedTicket(
    string Id,
    string Type,
    string? Subject,
    string? Description,
    string? Status,
    string? Priority,
    string? ReportedVersion,
    string? ReportedVersionNormalized,
    string? ReportedVersionPrecision,
    string? Resolution,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> UnresolvedBugRefs,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord);

public sealed record ReportingCustomer(
    string Id,
    string Type,
    string? Name,
    string? Plan,
    string? Deployment,
    EvidenceClassification Evidence,
    string Path,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord,
    string? RelationshipSourceRecord);

public sealed record TicketFeature(
    string Id,
    string Type,
    string? Name,
    EvidenceClassification Evidence,
    string Path,
    string? SourceSystem,
    string? SourceRecord,
    string? RelationshipSourceRecord);

public sealed record ReleaseEvidence(
    string Id,
    string? Version,
    DateTimeOffset? ReleasedAt,
    EvidenceClassification Evidence,
    string? EvidenceText,
    string? RelationshipSourceRecord);

public sealed record AffectedFeature(
    string Id,
    string? Name,
    EvidenceClassification Evidence,
    string? RelationshipSourceRecord);

public sealed record ConfirmedBug(
    string Id,
    string Type,
    string? Title,
    string? SourceStatus,
    string? Severity,
    string? Workaround,
    EvidenceClassification Evidence,
    string Path,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord,
    string? RelationshipSourceRecord,
    IReadOnlyList<AffectedFeature> AffectedFeatures,
    IReadOnlyList<ReleaseEvidence> IntroducedIn,
    IReadOnlyList<ReleaseEvidence> FixedIn);

public sealed record SharedFeature(
    string FeatureId,
    string? FeatureName,
    string? RelationshipSourceRecord);

public sealed record CandidateConfirmedTicket(
    string Id,
    string Type,
    string? Subject,
    string? Status,
    string? ReportedVersion,
    string? ReportedVersionPrecision,
    string? CustomerId,
    bool SameCustomer,
    EvidenceClassification Evidence,
    string Path,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    string? SourceSystem,
    string? SourceRecord,
    string? RelationshipSourceRecord);

public sealed record CandidateBug(
    string Id,
    string Type,
    string? Title,
    string? SourceStatus,
    string? Severity,
    string? Workaround,
    EvidenceClassification Evidence,
    string Path,
    string Reason,
    IReadOnlyList<SharedFeature> SharedFeatures,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord,
    IReadOnlyList<ReleaseEvidence> IntroducedIn,
    IReadOnlyList<ReleaseEvidence> FixedIn,
    long ConfirmedTicketCount,
    long ConfirmedCustomerCount,
    IReadOnlyList<CandidateConfirmedTicket> ConfirmedTickets,
    int ConfirmedTicketLimit,
    bool ConfirmedTicketsTruncated);

public sealed record RelatedTicket(
    string Id,
    string Type,
    string? Subject,
    string? Status,
    DateTimeOffset? CreatedAt,
    string? ReportedVersion,
    string? ReportedVersionPrecision,
    string? CustomerId,
    bool SameCustomer,
    EvidenceClassification Evidence,
    string Path,
    string Reason,
    IReadOnlyList<string> SharedFeatureIds,
    IReadOnlyList<string> ConfirmedBugIds,
    string? SourceSystem,
    string? SourceRecord);

public sealed record KnowledgeRoute(
    string Relationship,
    string TargetId,
    string? Basis,
    EvidenceClassification Evidence,
    string? RelationshipSourceRecord);

public sealed record KnowledgeArticleEvidence(
    string Id,
    string Type,
    string? Title,
    string? Summary,
    DateTimeOffset? UpdatedAt,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord,
    string? SourcePath,
    IReadOnlyList<KnowledgeRoute> ReachedVia,
    string? Content);

public sealed record InvestigationResultCounts(
    int Features,
    int ConfirmedBugs,
    int CandidateBugs,
    int CandidateBugLimit,
    bool CandidateBugLimitReached,
    int RelatedTickets,
    long RelatedTicketTotal,
    int RelatedTicketLimit,
    bool RelatedTicketsTruncated,
    int KnowledgeArticles,
    bool KnowledgeContentIncluded);
