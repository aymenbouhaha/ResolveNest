namespace ResolveNest.Api.Features.Investigations;

public sealed record TicketRelevanceContext(InvestigatedTicket Ticket, IReadOnlyList<string> ConfirmedBugIds, IReadOnlyList<string> ErrorCodes)
{
    private static readonly HashSet<string> ClosedStatuses = new(StringComparer.Ordinal) { "resolved", "closed" };

    public bool IsClosedWithoutConfirmedBug => ConfirmedBugIds.Count == 0 && Ticket.Status is { } status && ClosedStatuses.Contains(status);
}

public static class CandidateRelevanceEvaluator
{
    public static TicketRelevanceContext CreateContext(InvestigatedTicket ticket, IReadOnlyList<ConfirmedBug> confirmedBugs) =>
        new(ticket, confirmedBugs.Select(bug => bug.Id).ToList(), TechnicalErrorCodes.Extract(ticket.Subject, ticket.Description));

    public static CandidateRelevance Evaluate(
        TicketRelevanceContext context,
        string? bugTitle,
        string? bugDescription,
        IReadOnlyList<SharedFeature> sharedFeatures,
        IReadOnlyList<ReleaseEvidence> introducedIn,
        IReadOnlyList<ReleaseEvidence> fixedIn,
        long confirmedTicketCount,
        long confirmedCustomerCount)
    {
        var sharedFeatureIds = sharedFeatures.Select(feature => feature.FeatureId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var errorCodes = CompareErrorCodes(context.ErrorCodes, TechnicalErrorCodes.Extract(bugTitle, bugDescription));
        var version = ReportedVersionAssessor.Assess(context.Ticket, introducedIn, fixedIn);

        var supporting = new List<RelevanceNote>();
        var limitations = new List<RelevanceNote>();

        supporting.Add(new("sharedFeature", $"The bug affects {string.Join(", ", sharedFeatureIds)}, which this ticket concerns. A shared feature does not establish the same root cause."));
        DescribeErrorCodes(errorCodes, supporting, limitations);
        DescribeVersion(version, supporting, limitations);
        DescribeHistory(confirmedTicketCount, confirmedCustomerCount, supporting, limitations);
        DescribeTicketState(context, limitations);

        var tier = TierOf(errorCodes, version);
        if (tier == CandidateRelevanceTier.ErrorCodeMatchChronologyConflict)
        {
            limitations.Insert(0, new("errorCodeMatchContradictedByIntroduction", $"The shared error code {string.Join(", ", errorCodes.MatchedCodes)} conflicts with release chronology: the reported version '{version.ReportedVersion}' is before the documented introduction release ({string.Join(", ", version.IntroducedInVersions)}). The documented releases argue against this bug unless the version report is wrong or the defect also exists in earlier releases."));
        }

        return new CandidateRelevance(tier, sharedFeatureIds, errorCodes, version, supporting, limitations);
    }

    public static IReadOnlyList<CandidateBug> Order(IEnumerable<CandidateBug> candidates) => candidates
        .OrderBy(candidate => TierRank(candidate.Relevance.Tier))
        .ThenBy(candidate => VersionRank(candidate.Relevance.Version.Assessment))
        .ThenByDescending(candidate => candidate.ConfirmedTicketCount)
        .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
        .ToList();

    private static ErrorCodeSignal CompareErrorCodes(IReadOnlyList<string> ticketCodes, IReadOnlyList<string> bugCodes)
    {
        var matched = ticketCodes.Intersect(bugCodes, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var status = matched.Count > 0
            ? ErrorCodeMatchStatus.Matched
            : ticketCodes.Count > 0 && bugCodes.Count > 0
                ? ErrorCodeMatchStatus.DifferentCodes
                : ErrorCodeMatchStatus.Unavailable;
        return new ErrorCodeSignal(status, matched, ticketCodes, bugCodes);
    }

    private static CandidateRelevanceTier TierOf(ErrorCodeSignal errorCodes, VersionSignal version)
    {
        if (errorCodes.Status == ErrorCodeMatchStatus.Matched)
        {
            return ContradictsIntroduction(version)
                ? CandidateRelevanceTier.ErrorCodeMatchChronologyConflict
                : CandidateRelevanceTier.ErrorCodeMatch;
        }

        return version.Assessment switch
        {
            VersionCompatibility.WithinDocumentedRange or VersionCompatibility.Indeterminate => CandidateRelevanceTier.VersionCompatible,
            VersionCompatibility.OutsideDocumentedRange => CandidateRelevanceTier.VersionConflict,
            _ => CandidateRelevanceTier.FeatureOnly,
        };
    }

    private static int TierRank(CandidateRelevanceTier tier) => tier switch
    {
        CandidateRelevanceTier.ErrorCodeMatch => 0,
        CandidateRelevanceTier.VersionCompatible => 1,
        CandidateRelevanceTier.FeatureOnly => 2,
        CandidateRelevanceTier.ErrorCodeMatchChronologyConflict => 3,
        _ => 4,
    };

    private static bool ContradictsIntroduction(VersionSignal version) => version.IntroducedCheck == ReleaseBoundCheck.Inconsistent;

    private static int VersionRank(VersionCompatibility assessment) => assessment switch
    {
        VersionCompatibility.WithinDocumentedRange => 0,
        VersionCompatibility.Indeterminate => 1,
        VersionCompatibility.NoReleaseEvidence or VersionCompatibility.ReportedVersionUnknown => 2,
        _ => 3,
    };

    private static void DescribeErrorCodes(ErrorCodeSignal errorCodes, List<RelevanceNote> supporting, List<RelevanceNote> limitations)
    {
        switch (errorCodes.Status)
        {
            case ErrorCodeMatchStatus.Matched:
                supporting.Add(new("errorCodeMatch", $"The ticket text and the bug text both cite {string.Join(", ", errorCodes.MatchedCodes)}."));
                break;
            case ErrorCodeMatchStatus.DifferentCodes:
                limitations.Add(new("differentErrorCodes", $"The ticket cites {string.Join(", ", errorCodes.TicketCodes)} and the bug cites {string.Join(", ", errorCodes.BugCodes)}; no code is shared."));
                break;
            default:
                limitations.Add(errorCodes.TicketCodes.Count == 0
                    ? new("noErrorCodeInTicket", "The ticket text contains no recognizable error code, so error codes cannot be compared.")
                    : new("noErrorCodeInBug", "The bug text contains no recognizable error code, so error codes cannot be compared."));
                break;
        }
    }

    private static void DescribeVersion(VersionSignal version, List<RelevanceNote> supporting, List<RelevanceNote> limitations)
    {
        var reported = version.ReportedVersion is null ? "no version" : $"'{version.ReportedVersion}'";
        var range = DocumentedRange(version);

        switch (version.Assessment)
        {
            case VersionCompatibility.WithinDocumentedRange:
                supporting.Add(new("versionWithinDocumentedRange", $"Tentative: the reported version {reported} falls within the documented range ({range})."));
                break;
            case VersionCompatibility.Indeterminate:
                supporting.Add(new("versionMayBeWithinDocumentedRange", $"Tentative: the reported version {reported} may fall within the documented range ({range}), depending on the exact patch release."));
                break;
            case VersionCompatibility.OutsideDocumentedRange:
                limitations.Add(new("versionOutsideDocumentedRange", OutsideRangeMessage(version, reported, range)));
                break;
            case VersionCompatibility.NoReleaseEvidence:
                limitations.Add(new("noReleaseEvidence", "No introduction or fix release is documented for this bug, so the reported version cannot be compared."));
                break;
            case VersionCompatibility.ReportedVersionUnknown:
                limitations.Add(new("reportedVersionUnknown", $"The ticket reports {reported}, which is not a comparable version, so it cannot be compared with the documented range ({range})."));
                break;
        }

        if (version.ReportedVersionPrecision == "minor")
        {
            limitations.Add(new("patchVersionUnknown", $"The reported version {reported} names a minor line, not a patch release. It is not treated as any specific patch."));
        }

        if (version.IntroducedCheck == ReleaseBoundCheck.NotDocumented && version.FixedCheck != ReleaseBoundCheck.NotDocumented)
        {
            limitations.Add(new("introductionReleaseNotDocumented", "No introduction release is documented, so only the fix release can be compared."));
        }

        if (version.FixedInVersions.Count > 0)
        {
            limitations.Add(new("fixDoesNotProveResolution", $"A fix is documented in {string.Join(", ", version.FixedInVersions)}. This does not show that the customer upgraded or that the ticket is resolved."));
        }
    }

    private static string OutsideRangeMessage(VersionSignal version, string reported, string range) =>
        version.IntroducedCheck == ReleaseBoundCheck.Inconsistent
            ? $"Tentative: the reported version {reported} is before the documented introduction release ({range}). The candidate is kept because version reports can be inaccurate."
            : $"Tentative: the reported version {reported} already includes the documented fix ({range}). The candidate is kept because the report may be inaccurate or the issue may have regressed.";

    private static string DocumentedRange(VersionSignal version)
    {
        var introduced = version.IntroducedInVersions.Count > 0 ? $"introduced in {string.Join(", ", version.IntroducedInVersions)}" : "introduction not documented";
        var fixedPart = version.FixedInVersions.Count > 0 ? $"fixed in {string.Join(", ", version.FixedInVersions)}" : "no fix documented";
        return $"{introduced}; {fixedPart}";
    }

    private static void DescribeHistory(long confirmedTicketCount, long confirmedCustomerCount, List<RelevanceNote> supporting, List<RelevanceNote> limitations)
    {
        if (confirmedTicketCount > 0)
        {
            supporting.Add(new("historicalConfirmations", $"{confirmedTicketCount} other tickets from {confirmedCustomerCount} customers are explicitly confirmed against this bug. This is context, not a confirmation for this ticket."));
        }
        else
        {
            limitations.Add(new("noHistoricalConfirmations", "No other ticket is confirmed against this bug."));
        }
    }

    private static void DescribeTicketState(TicketRelevanceContext context, List<RelevanceNote> limitations)
    {
        if (context.ConfirmedBugIds.Count > 0)
        {
            limitations.Add(new("ticketHasConfirmedBug", $"This ticket is already confirmed against {string.Join(", ", context.ConfirmedBugIds)}. This candidate is additional context only."));
        }
        else if (context.IsClosedWithoutConfirmedBug)
        {
            limitations.Add(new("ticketClosedWithoutConfirmedBug", $"The ticket is {context.Ticket.Status} with no confirmed bug. Its recorded resolution may point to a cause other than a product defect."));
        }
    }
}
