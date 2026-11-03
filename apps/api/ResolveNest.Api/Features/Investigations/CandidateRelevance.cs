using System.Text.Json.Serialization;

namespace ResolveNest.Api.Features.Investigations;

[JsonConverter(typeof(JsonStringEnumConverter<CandidateRelevanceTier>))]
public enum CandidateRelevanceTier
{
    [JsonStringEnumMemberName("errorCodeMatch")]
    ErrorCodeMatch,

    [JsonStringEnumMemberName("versionCompatible")]
    VersionCompatible,

    [JsonStringEnumMemberName("featureOnly")]
    FeatureOnly,

    [JsonStringEnumMemberName("errorCodeMatchChronologyConflict")]
    ErrorCodeMatchChronologyConflict,

    [JsonStringEnumMemberName("versionConflict")]
    VersionConflict,
}

[JsonConverter(typeof(JsonStringEnumConverter<ErrorCodeMatchStatus>))]
public enum ErrorCodeMatchStatus
{
    [JsonStringEnumMemberName("matched")]
    Matched,

    [JsonStringEnumMemberName("differentCodes")]
    DifferentCodes,

    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}

[JsonConverter(typeof(JsonStringEnumConverter<VersionCompatibility>))]
public enum VersionCompatibility
{
    [JsonStringEnumMemberName("withinDocumentedRange")]
    WithinDocumentedRange,

    [JsonStringEnumMemberName("indeterminate")]
    Indeterminate,

    [JsonStringEnumMemberName("outsideDocumentedRange")]
    OutsideDocumentedRange,

    [JsonStringEnumMemberName("noReleaseEvidence")]
    NoReleaseEvidence,

    [JsonStringEnumMemberName("reportedVersionUnknown")]
    ReportedVersionUnknown,
}

[JsonConverter(typeof(JsonStringEnumConverter<ReleaseBoundCheck>))]
public enum ReleaseBoundCheck
{
    [JsonStringEnumMemberName("consistent")]
    Consistent,

    [JsonStringEnumMemberName("inconsistent")]
    Inconsistent,

    [JsonStringEnumMemberName("indeterminate")]
    Indeterminate,

    [JsonStringEnumMemberName("notDocumented")]
    NotDocumented,

    [JsonStringEnumMemberName("notEvaluated")]
    NotEvaluated,
}

public sealed record CandidateRelevance(
    CandidateRelevanceTier Tier,
    IReadOnlyList<string> SharedFeatureIds,
    ErrorCodeSignal ErrorCodes,
    VersionSignal Version,
    IReadOnlyList<RelevanceNote> Supporting,
    IReadOnlyList<RelevanceNote> Limitations);

public sealed record ErrorCodeSignal(
    ErrorCodeMatchStatus Status,
    IReadOnlyList<string> MatchedCodes,
    IReadOnlyList<string> TicketCodes,
    IReadOnlyList<string> BugCodes);

public sealed record VersionSignal(
    VersionCompatibility Assessment,
    string? ReportedVersion,
    string? ReportedVersionPrecision,
    IReadOnlyList<string> IntroducedInVersions,
    IReadOnlyList<string> FixedInVersions,
    ReleaseBoundCheck IntroducedCheck,
    ReleaseBoundCheck FixedCheck);

public sealed record RelevanceNote(string Code, string Message);
