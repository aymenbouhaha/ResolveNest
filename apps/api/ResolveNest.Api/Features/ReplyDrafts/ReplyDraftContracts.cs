using System.Text.Json.Serialization;
using ResolveNest.Api.Features.AiInvestigations;
using ResolveNest.Api.Features.EvidenceContext;

namespace ResolveNest.Api.Features.ReplyDrafts;

[JsonConverter(typeof(JsonStringEnumConverter<ReplyDraftStatus>))]
public enum ReplyDraftStatus
{
    [JsonStringEnumMemberName("drafted")]
    Drafted,

    [JsonStringEnumMemberName("insufficientEvidence")]
    InsufficientEvidence,
}

public sealed record ModelReplyDraft(
    ReplyDraftStatus Status,
    string DraftBody,
    IReadOnlyList<AiStatement> SupportingClaims,
    IReadOnlyList<AiStatement> Uncertainties);

public sealed record ReplyDraftPreviewRequest(string? ReviewerGuidance);

public sealed record ReplyDraftPreview(
    string TicketId,
    string PreviewId,
    ReplyDraftStatus Status,
    string DraftBody,
    IReadOnlyList<AiStatement> SupportingClaims,
    IReadOnlyList<AiStatement> Uncertainties,
    IReadOnlyList<ContextLimitation> EvidenceLimitations,
    IReadOnlyList<CitationSource> ValidatedSources,
    AiAnswerProvenance Provenance,
    string PreviewToken,
    DateTimeOffset PreviewExpiresAt,
    bool HumanReviewRequired,
    bool Persisted);

public sealed record ReplyDraftApprovalRequest(string? PreviewToken, string? FinalText, bool? Approved);

public sealed record ApprovedReplyDraft(
    string DraftId,
    string TicketId,
    string Status,
    string ApprovedText,
    string GeneratedText,
    bool EditedByHuman,
    bool GeneratedTextValidated,
    bool ApprovedTextValidated,
    IReadOnlyList<string> CitationIds,
    IReadOnlyList<string> LimitationCodes,
    string Model,
    string? ModelVersion,
    string EvidenceContractVersion,
    DateTimeOffset GeneratedAt,
    DateTimeOffset ApprovedAt,
    string ApprovalBasis,
    string CustomerDelivery);

public sealed record ReplyDraftApprovalResult(ApprovedReplyDraft Draft, bool AlreadyApproved);
