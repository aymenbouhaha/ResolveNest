using ResolveNest.Api.Features.Ingestion;

namespace ResolveNest.Api.Features.Investigations;

public static class ReportedVersionAssessor
{
    private enum PositionToRelease
    {
        Before,
        AtOrAfter,
        Straddles,
        Unknown,
    }

    public static VersionSignal Assess(InvestigatedTicket ticket, IReadOnlyList<ReleaseEvidence> introducedIn, IReadOnlyList<ReleaseEvidence> fixedIn)
    {
        var reported = SourceNormalization.Version(ticket.ReportedVersion);
        var isReportedVersionUsable = reported.Precision is VersionPrecision.Patch or VersionPrecision.MinorLine;

        var introducedCheck = CheckBound(reported, isReportedVersionUsable, introducedIn, PositionToRelease.AtOrAfter, PositionToRelease.Before);
        var fixedCheck = CheckBound(reported, isReportedVersionUsable, fixedIn, PositionToRelease.Before, PositionToRelease.AtOrAfter);

        var compatibility = (introducedCheck, fixedCheck) switch
        {
            (ReleaseBoundCheck.NotDocumented, ReleaseBoundCheck.NotDocumented) => VersionCompatibility.NoReleaseEvidence,
            _ when !isReportedVersionUsable => VersionCompatibility.ReportedVersionUnknown,
            _ when introducedCheck == ReleaseBoundCheck.Inconsistent || fixedCheck == ReleaseBoundCheck.Inconsistent => VersionCompatibility.OutsideDocumentedRange,
            _ when introducedCheck == ReleaseBoundCheck.Indeterminate || fixedCheck == ReleaseBoundCheck.Indeterminate => VersionCompatibility.Indeterminate,
            _ => VersionCompatibility.WithinDocumentedRange,
        };

        return new VersionSignal(
            compatibility,
            ticket.ReportedVersion,
            ticket.ReportedVersionPrecision,
            introducedIn.Select(release => release.Version).OfType<string>().ToList(),
            fixedIn.Select(release => release.Version).OfType<string>().ToList(),
            introducedCheck,
            fixedCheck);
    }

    private static ReleaseBoundCheck CheckBound(
        VersionReference reported,
        bool isReportedVersionUsable,
        IReadOnlyList<ReleaseEvidence> releases,
        PositionToRelease consistentPosition,
        PositionToRelease inconsistentPosition)
    {
        if (releases.Count == 0)
        {
            return ReleaseBoundCheck.NotDocumented;
        }

        if (!isReportedVersionUsable)
        {
            return ReleaseBoundCheck.NotEvaluated;
        }

        var positions = releases.Select(release => PositionOf(reported, release.Version)).Distinct().ToList();
        if (positions is [var single] && single == consistentPosition)
        {
            return ReleaseBoundCheck.Consistent;
        }

        return positions is [var only] && only == inconsistentPosition
            ? ReleaseBoundCheck.Inconsistent
            : ReleaseBoundCheck.Indeterminate;
    }

    private static PositionToRelease PositionOf(VersionReference reported, string? releaseVersion)
    {
        var release = SourceNormalization.Version(releaseVersion);
        if (release.Precision != VersionPrecision.Patch
            || !TryParseParts(release.Normalized, out var releaseParts)
            || !TryParseParts(reported.Normalized, out var reportedParts))
        {
            return PositionToRelease.Unknown;
        }

        if (reported.Precision == VersionPrecision.Patch)
        {
            return (reportedParts[0], reportedParts[1], reportedParts[2]).CompareTo((releaseParts[0], releaseParts[1], releaseParts[2])) < 0
                ? PositionToRelease.Before
                : PositionToRelease.AtOrAfter;
        }

        var lineComparison = (reportedParts[0], reportedParts[1]).CompareTo((releaseParts[0], releaseParts[1]));
        return lineComparison switch
        {
            < 0 => PositionToRelease.Before,
            > 0 => PositionToRelease.AtOrAfter,
            _ when releaseParts[2] == 0 => PositionToRelease.AtOrAfter,
            _ => PositionToRelease.Straddles,
        };
    }

    private static bool TryParseParts(string? normalizedVersion, out long[] parts)
    {
        parts = [];
        if (normalizedVersion is null)
        {
            return false;
        }

        var segments = normalizedVersion.Split('.');
        var parsed = new long[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            if (!long.TryParse(segments[index], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out parsed[index]))
            {
                return false;
            }
        }

        parts = parsed;
        return true;
    }
}
