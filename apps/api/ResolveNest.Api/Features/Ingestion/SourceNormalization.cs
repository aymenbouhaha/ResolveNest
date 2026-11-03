using System.Text.RegularExpressions;

namespace ResolveNest.Api.Features.Ingestion;

public enum VersionPrecision
{
    Missing,
    Patch,
    MinorLine,
    Unparseable,
}

public sealed record VersionReference(string? Raw, VersionPrecision Precision, string? Normalized)
{
    public bool IsExact => Precision == VersionPrecision.Patch;

    public string? PrecisionName => Precision switch
    {
        VersionPrecision.Patch => "patch",
        VersionPrecision.MinorLine => "minor",
        _ => null,
    };
}

public static partial class SourceNormalization
{
    public static string CustomerKey(string raw) => raw.Trim().ToLowerInvariant();

    public static string FeatureKey(string raw) => raw.Trim().ToLowerInvariant();

    public static string BugKey(string raw) => raw.Trim().ToUpperInvariant();

    public static string TicketKey(string raw) => raw.Trim().ToUpperInvariant();

    public static string CustomerId(string customerKey) => $"customer:{customerKey}";

    public static string FeatureId(string featureKey) => $"feature:{featureKey}";

    public static string BugId(string bugKey) => $"bug:{bugKey}";

    public static string TicketId(string ticketKey) => $"ticket:{ticketKey}";

    public static string ReleaseId(string exactVersion) => $"release:{exactVersion}";

    public static string ArticleKey(string raw) => raw.Trim().ToUpperInvariant();

    public static string ArticleId(string articleKey) => $"kb:{articleKey}";

    public static bool IsCanonicalArticleKey(string articleKey) => ArticleKeyPattern().IsMatch(articleKey);

    public static bool IsCanonicalBugKey(string bugKey) => BugKeyPattern().IsMatch(bugKey);

    public static bool IsCanonicalFeatureKey(string featureKey) => FeatureKeyPattern().IsMatch(featureKey);

    public static VersionReference Version(string? raw)
    {
        if (raw is null)
        {
            return new VersionReference(null, VersionPrecision.Missing, null);
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
        {
            trimmed = trimmed[1..];
        }

        if (PatchVersionPattern().IsMatch(trimmed))
        {
            return new VersionReference(raw, VersionPrecision.Patch, trimmed);
        }

        if (MinorLineVersionPattern().IsMatch(trimmed))
        {
            return new VersionReference(raw, VersionPrecision.MinorLine, trimmed);
        }

        return new VersionReference(raw, VersionPrecision.Unparseable, null);
    }

    public static int CompareExactVersions(string left, string right)
    {
        var leftParts = left.Split('.').Select(int.Parse).ToArray();
        var rightParts = right.Split('.').Select(int.Parse).ToArray();
        for (var index = 0; index < 3; index++)
        {
            var comparison = leftParts[index].CompareTo(rightParts[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex PatchVersionPattern();

    [GeneratedRegex(@"^\d+\.\d+$")]
    private static partial Regex MinorLineVersionPattern();

    [GeneratedRegex(@"^KB-\d{3,}$")]
    private static partial Regex ArticleKeyPattern();

    [GeneratedRegex(@"^BUG-\d+$")]
    private static partial Regex BugKeyPattern();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex FeatureKeyPattern();
}
