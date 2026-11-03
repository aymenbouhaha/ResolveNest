using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.EvidenceContext;

public sealed record InvestigationEvidenceContext(
    string ContractVersion,
    TicketContext Ticket,
    CustomerContext? Customer,
    IReadOnlyList<FeatureContext> Features,
    IReadOnlyList<ConfirmedBugContext> ConfirmedBugs,
    IReadOnlyList<CandidateBugContext> CandidateBugs,
    RelatedTicketsContext RelatedTickets,
    IReadOnlyList<KnowledgeArticleContext> KnowledgeArticles,
    IReadOnlyList<ContextLimitation> Limitations,
    IReadOnlyList<CitationSource> Sources,
    ContextCompleteness Completeness);

public sealed record SourceExcerpt(
    string CitationId,
    string Field,
    string VerbatimText,
    int SourceLength,
    bool Truncated);

public sealed record ReportedVersionContext(string? Reported, string? Normalized, string? Precision);

public sealed record TicketContext(
    string Id,
    string CitationId,
    string? Status,
    string? Priority,
    ReportedVersionContext ReportedVersion,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ResolvedAt,
    IReadOnlyList<string> ErrorCodes,
    IReadOnlyList<string> UnresolvedBugRefs,
    SourceExcerpt? Subject,
    SourceExcerpt? Description,
    SourceExcerpt? Resolution);

public sealed record CustomerContext(
    string Id,
    string? Name,
    string? Plan,
    string? Deployment,
    EvidenceClassification Evidence,
    string Path,
    IReadOnlyList<string> CitationIds);

public sealed record FeatureContext(
    string Id,
    string? Name,
    EvidenceClassification Evidence,
    string Path,
    IReadOnlyList<string> CitationIds);

public sealed record ReleaseContext(
    string ReleaseId,
    string? Version,
    DateTimeOffset? ReleasedAt,
    EvidenceClassification Evidence,
    SourceExcerpt? EvidenceText,
    IReadOnlyList<string> CitationIds);

public sealed record ConfirmedBugContext(
    string Id,
    EvidenceClassification Evidence,
    string Path,
    string? SourceStatus,
    string? Severity,
    string? ConfirmedBy,
    DateTimeOffset? ConfirmedAt,
    IReadOnlyList<string> AffectedFeatureIds,
    IReadOnlyList<ReleaseContext> IntroducedIn,
    IReadOnlyList<ReleaseContext> FixedIn,
    SourceExcerpt? Title,
    SourceExcerpt? Workaround,
    IReadOnlyList<string> CitationIds);

public sealed record CandidateBugContext(
    int InvestigationRank,
    string Id,
    EvidenceClassification Evidence,
    string Path,
    string Reason,
    string? SourceStatus,
    string? Severity,
    CandidateRelevance Relevance,
    IReadOnlyList<ReleaseContext> IntroducedIn,
    IReadOnlyList<ReleaseContext> FixedIn,
    HistoricalConfirmations History,
    SourceExcerpt? Title,
    SourceExcerpt? Description,
    SourceExcerpt? Workaround,
    IReadOnlyList<string> CitationIds);

public sealed record HistoricalConfirmations(
    long ConfirmedTicketCount,
    long ConfirmedCustomerCount,
    int ReturnedByQuery,
    bool TruncatedByQuery,
    int SameCustomerReturned,
    IReadOnlyList<HistoricalTicketContext> Examples,
    int OmittedExamples);

public sealed record HistoricalTicketContext(
    string Id,
    EvidenceClassification Evidence,
    string Path,
    string? CustomerId,
    bool SameCustomer,
    string? Status,
    string? ReportedVersion,
    DateTimeOffset? ConfirmedAt,
    SourceExcerpt? Subject,
    IReadOnlyList<string> CitationIds);

public sealed record RelatedTicketsContext(
    long Total,
    int ReturnedByQuery,
    IReadOnlyList<RelatedTicketContext> Included);

public sealed record RelatedTicketContext(
    string Id,
    EvidenceClassification Evidence,
    string Path,
    string? CustomerId,
    bool SameCustomer,
    string? Status,
    DateTimeOffset? CreatedAt,
    string? ReportedVersion,
    IReadOnlyList<string> ConfirmedBugIds,
    SourceExcerpt? Subject,
    IReadOnlyList<string> CitationIds);

public sealed record KnowledgeArticleContext(
    string Id,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<KnowledgeRouteContext> Routes,
    bool ContentLoaded,
    SourceExcerpt? Title,
    SourceExcerpt? Summary,
    SourceExcerpt? Content,
    IReadOnlyList<string> CitationIds);

public sealed record KnowledgeRouteContext(
    string Relationship,
    string TargetId,
    string? Basis,
    EvidenceClassification Evidence,
    string CitationId);

public sealed record ContextLimitation(string Code, string Message, IReadOnlyList<string> SubjectIds);

public sealed record EvidenceOmission(
    string Section,
    string? ParentId,
    string Reason,
    long? Available,
    int Included,
    IReadOnlyList<string> OmittedIds);

public sealed record ContextCompleteness(
    int SourceTextCharacterBudget,
    int SourceTextCharactersUsed,
    bool SourceTextBudgetExhausted,
    int TruncatedExcerpts,
    IReadOnlyList<string> BudgetLimitedCitationIds,
    bool IsComplete,
    IReadOnlyList<EvidenceOmission> Omissions);
