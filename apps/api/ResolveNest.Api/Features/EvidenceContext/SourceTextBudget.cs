namespace ResolveNest.Api.Features.EvidenceContext;

public sealed class SourceTextBudget(int totalCharacters)
{
    private readonly List<string> budgetLimitedCitationIds = [];

    public int TotalCharacters { get; } = Math.Max(0, totalCharacters);
    public int UsedCharacters { get; private set; }
    public int TruncatedExcerpts { get; private set; }
    public bool Exhausted => UsedCharacters >= TotalCharacters;
    public IReadOnlyList<string> BudgetLimitedCitationIds => budgetLimitedCitationIds;

    private int RemainingCharacters => TotalCharacters - UsedCharacters;

    public SourceExcerpt? Excerpt(string citationId, string field, string? text, int fieldLimit)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var limitedByBudget = RemainingCharacters < Math.Min(fieldLimit, text.Length);
        var length = SafePrefixLength(text, Math.Min(fieldLimit, RemainingCharacters));
        var truncated = length < text.Length;

        UsedCharacters += length;
        if (truncated)
        {
            TruncatedExcerpts++;
        }

        if (limitedByBudget && !budgetLimitedCitationIds.Contains(citationId, StringComparer.Ordinal))
        {
            budgetLimitedCitationIds.Add(citationId);
        }

        return new SourceExcerpt(citationId, field, text[..length], text.Length, truncated);
    }

    private static int SafePrefixLength(string text, int allowed)
    {
        if (text.Length <= allowed)
        {
            return text.Length;
        }

        var length = Math.Max(0, allowed);
        return length > 0 && char.IsHighSurrogate(text[length - 1]) ? length - 1 : length;
    }
}
