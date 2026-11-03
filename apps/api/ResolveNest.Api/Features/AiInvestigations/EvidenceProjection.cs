using ResolveNest.Api.Features.EvidenceContext;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.AiInvestigations;

public sealed record ProjectedText(string Text, bool? Truncated);

public sealed record ProjectedTicket(
    string Id,
    string? Status,
    string? Priority,
    string? ReportedVersion,
    string? ReportedVersionPrecision,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> ErrorCodes,
    ProjectedText? Subject,
    ProjectedText? Description,
    ProjectedText? Resolution,
    IReadOnlyList<string> Cite);

public sealed record ProjectedCustomer(string Id, string? Name, string? Plan, string? Deployment, EvidenceClassification Evidence, IReadOnlyList<string> Cite);

public sealed record ProjectedFeature(string Id, string? Name, EvidenceClassification Evidence, IReadOnlyList<string> Cite);

public sealed record ProjectedRelease(string? Version, DateTimeOffset? ReleasedAt, EvidenceClassification Evidence, ProjectedText? EvidenceText, IReadOnlyList<string> Cite);

public sealed record ProjectedConfirmedBug(
    string Id,
    EvidenceClassification Evidence,
    ProjectedText? Title,
    string? SourceStatus,
    string? Severity,
    ProjectedText? Workaround,
    DateTimeOffset? ConfirmedAt,
    IReadOnlyList<ProjectedRelease> IntroducedIn,
    IReadOnlyList<ProjectedRelease> FixedIn,
    IReadOnlyList<string> Cite);

public sealed record ProjectedHistoricalTicket(string Id, string? CustomerId, bool SameCustomer, string? Status, string? ReportedVersion, EvidenceClassification Evidence, IReadOnlyList<string> Cite);

public sealed record ProjectedHistory(long ConfirmedTicketCount, long ConfirmedCustomerCount, IReadOnlyList<ProjectedHistoricalTicket> Examples);

public sealed record ProjectedCandidateBug(
    int Rank,
    string Id,
    EvidenceClassification Evidence,
    CandidateRelevanceTier Tier,
    ErrorCodeMatchStatus ErrorCodeMatch,
    IReadOnlyList<string> MatchedErrorCodes,
    VersionCompatibility VersionAssessment,
    ProjectedText? Title,
    ProjectedText? Description,
    string? SourceStatus,
    string? Severity,
    ProjectedText? Workaround,
    IReadOnlyList<ProjectedRelease> IntroducedIn,
    IReadOnlyList<ProjectedRelease> FixedIn,
    ProjectedHistory History,
    IReadOnlyList<string> SupportingSignals,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Cite);

public sealed record ProjectedRoute(string Relationship, string TargetId, EvidenceClassification Evidence, string Cite);

public sealed record ProjectedArticle(
    string Id,
    ProjectedText? Title,
    ProjectedText? Summary,
    ProjectedText? Content,
    IReadOnlyList<ProjectedRoute> Routes,
    IReadOnlyList<string> Cite);

public sealed record ProjectedRelatedTicket(string Id, bool SameCustomer, string? Status, IReadOnlyList<string> ConfirmedBugIds, ProjectedText? Subject, IReadOnlyList<string> Cite);

public sealed record ProjectedRelatedTickets(long Total, IReadOnlyList<ProjectedRelatedTicket> Examples);

public sealed record ProjectedLimitation(string Code, string Message);

public sealed record ProjectedCompleteness(bool IsComplete, IReadOnlyList<string> Omissions);

public sealed record ProjectedEvidence(
    string ContractVersion,
    ProjectedTicket Ticket,
    ProjectedCustomer? Customer,
    IReadOnlyList<ProjectedFeature> Features,
    IReadOnlyList<ProjectedConfirmedBug> ConfirmedBugs,
    IReadOnlyList<ProjectedCandidateBug> CandidateBugs,
    IReadOnlyList<ProjectedArticle> KnowledgeArticles,
    ProjectedRelatedTickets RelatedTickets,
    IReadOnlyList<ProjectedLimitation> Limitations,
    ProjectedCompleteness Completeness,
    IReadOnlyList<string> AllowedCitationIds);
