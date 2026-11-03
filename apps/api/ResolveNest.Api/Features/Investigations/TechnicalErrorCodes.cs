using System.Text.RegularExpressions;

namespace ResolveNest.Api.Features.Investigations;

public static partial class TechnicalErrorCodes
{
    public static IReadOnlyList<string> Extract(params string?[] texts)
    {
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            foreach (Match match in ProductErrorCode().Matches(text))
            {
                codes.Add(match.Value);
            }

            foreach (Match match in QuotedApiErrorCode().Matches(text))
            {
                codes.Add(match.Groups["code"].Value);
            }
        }

        return codes.ToList();
    }

    [GeneratedRegex(@"\bERR_[A-Z0-9]+(?:_[A-Z0-9]+)*\b", RegexOptions.CultureInvariant)]
    private static partial Regex ProductErrorCode();

    [GeneratedRegex(@"['""](?<code>[a-z][a-z0-9]*(?:_[a-z0-9]+)+)['""]", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedApiErrorCode();
}
