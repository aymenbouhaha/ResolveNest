using System.Text.Json.Serialization;
using ResolveNest.Api.Features.EvidenceContext;

namespace ResolveNest.Api.Features.AiInvestigations;

[JsonConverter(typeof(JsonStringEnumConverter<AiAnswerStatus>))]
public enum AiAnswerStatus
{
    [JsonStringEnumMemberName("answered")]
    Answered,

    [JsonStringEnumMemberName("insufficientEvidence")]
    InsufficientEvidence,
}

[JsonConverter(typeof(JsonStringEnumConverter<AiFindingKind>))]
public enum AiFindingKind
{
    [JsonStringEnumMemberName("confirmedForTicket")]
    ConfirmedForTicket,

    [JsonStringEnumMemberName("historicalConfirmation")]
    HistoricalConfirmation,

    [JsonStringEnumMemberName("candidate")]
    Candidate,

    [JsonStringEnumMemberName("documented")]
    Documented,

    [JsonStringEnumMemberName("context")]
    Context,

    [JsonStringEnumMemberName("contradiction")]
    Contradiction,
}

public sealed record AiFinding(AiFindingKind Kind, string Statement, IReadOnlyList<string> CitationIds);

public sealed record AiStatement(string Statement, IReadOnlyList<string> CitationIds);

public sealed record ModelAnswer(
    AiAnswerStatus Status,
    string Answer,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<AiStatement> Uncertainties,
    IReadOnlyList<AiStatement> RecommendedNextSteps);

public sealed record AiAnswerProvenance(
    string Provider,
    string Model,
    string? ModelVersion,
    string EvidenceContractVersion,
    bool EvidenceComplete,
    int EvidenceCharacters,
    int? PromptTokens,
    int? OutputTokens,
    bool HumanReviewRequired);

public sealed record AiInvestigationAnswer(
    string TicketId,
    AiAnswerStatus Status,
    string Answer,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<AiStatement> Uncertainties,
    IReadOnlyList<AiStatement> RecommendedNextSteps,
    IReadOnlyList<CitationSource> ValidatedSources,
    AiAnswerProvenance Provenance);

public sealed record AskInvestigationRequest(string? Question);
