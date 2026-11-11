using System.Globalization;

namespace ResolveNest.Api.Features.Tickets;

public sealed record TicketListRequest(int Page, int PageSize, string? Q, string? Status, string Sort)
{
    public long Offset => ((long)Page - 1) * PageSize;

    public static readonly IReadOnlyDictionary<string, string> Orderings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["updatedAt_desc"] = "coalesce(t.updatedAt, t.createdAt) IS NULL ASC, coalesce(t.updatedAt, t.createdAt) DESC, t.id ASC",
        ["updatedAt_asc"] = "coalesce(t.updatedAt, t.createdAt) IS NULL ASC, coalesce(t.updatedAt, t.createdAt) ASC, t.id ASC",
        ["createdAt_desc"] = "t.createdAt IS NULL ASC, t.createdAt DESC, t.id ASC",
        ["createdAt_asc"] = "t.createdAt IS NULL ASC, t.createdAt ASC, t.id ASC",
        ["sourceId_asc"] = "t.sourceId IS NULL ASC, t.sourceId ASC, t.id ASC",
        ["sourceId_desc"] = "t.sourceId IS NULL ASC, t.sourceId DESC, t.id ASC",
    };

    public static Dictionary<string, string[]> Parse(string? page, string? pageSize, string? q, string? status, string? sort, out TicketListRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var parsedPage = ParseNumber(page, 1, int.MaxValue, "page", errors);
        var parsedSize = ParseNumber(pageSize, 25, 100, "pageSize", errors);
        sort ??= "updatedAt_desc";
        if (!Orderings.ContainsKey(sort))
            errors["sort"] = [$"sort must be one of: {string.Join(", ", Orderings.Keys)}."];
        request = new(parsedPage, parsedSize, string.IsNullOrWhiteSpace(q) ? null : q.Trim(), status, sort);
        return errors;
    }

    private static int ParseNumber(string? value, int fallback, int maximum, string name, Dictionary<string, string[]> errors)
    {
        if (value is null) return fallback;
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= maximum) return number;
        errors[name] = [$"{name} must be an integer between 1 and {maximum}."];
        return fallback;
    }
}
