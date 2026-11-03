using Neo4j.Driver;

namespace ResolveNest.Api.Features.Investigations;

public sealed class InvestigationResultException(string message) : Exception(message);

public sealed class GraphResultMap(IReadOnlyDictionary<string, object?> values, string location)
{
    public static GraphResultMap FromRecord(IRecord record, string queryName) =>
        new(record.Keys.ToDictionary(key => key, key => (object?)record[key]), queryName);

    public string RequiredString(string key) =>
        OptionalString(key) ?? throw Invalid(key, "a non-null string");

    public string? OptionalString(string key) => Value(key) switch
    {
        null => null,
        string text => text,
        _ => throw Invalid(key, "a string"),
    };

    public bool RequiredBool(string key) => Value(key) is bool flag ? flag : throw Invalid(key, "a boolean");

    public long RequiredLong(string key) => Value(key) is long number ? number : throw Invalid(key, "an integer");

    public DateTimeOffset? OptionalDateTime(string key) => Value(key) switch
    {
        null => null,
        ZonedDateTime zoned => zoned.ToDateTimeOffset(),
        _ => throw Invalid(key, "a date or datetime"),
    };

    public EvidenceClassification RequiredEvidence(string key) => RequiredString(key) switch
    {
        "confirmed" => EvidenceClassification.Confirmed,
        "documented" => EvidenceClassification.Documented,
        "candidate" => EvidenceClassification.Candidate,
        "context" => EvidenceClassification.Context,
        _ => throw Invalid(key, "a known evidence classification"),
    };

    public GraphResultMap? OptionalMap(string key) => Value(key) switch
    {
        null => null,
        IReadOnlyDictionary<string, object> map => new GraphResultMap(map.ToDictionary(entry => entry.Key, entry => (object?)entry.Value), $"{location}.{key}"),
        IDictionary<string, object> map => new GraphResultMap(map.ToDictionary(entry => entry.Key, entry => (object?)entry.Value), $"{location}.{key}"),
        _ => throw Invalid(key, "a map"),
    };

    public IReadOnlyList<GraphResultMap> Maps(string key) => Items(key)
        .Select((item, index) => item switch
        {
            IReadOnlyDictionary<string, object> map => new GraphResultMap(map.ToDictionary(entry => entry.Key, entry => (object?)entry.Value), $"{location}.{key}[{index}]"),
            IDictionary<string, object> map => new GraphResultMap(map.ToDictionary(entry => entry.Key, entry => (object?)entry.Value), $"{location}.{key}[{index}]"),
            _ => throw Invalid($"{key}[{index}]", "a map"),
        })
        .ToList();

    public IReadOnlyList<string> Strings(string key) => Items(key)
        .Select((item, index) => item as string ?? throw Invalid($"{key}[{index}]", "a string"))
        .ToList();

    public IReadOnlyList<T> List<T>(string key, Func<GraphResultMap, T> map) => Maps(key).Select(map).ToList();

    private IReadOnlyList<object?> Items(string key) => Value(key) switch
    {
        null => [],
        string => throw Invalid(key, "a list"),
        System.Collections.IEnumerable items => items.Cast<object?>().ToList(),
        _ => throw Invalid(key, "a list"),
    };

    private object? Value(string key) =>
        values.TryGetValue(key, out var value) ? value : throw new InvestigationResultException($"Investigation result {location} has no '{key}' field.");

    private InvestigationResultException Invalid(string key, string expected) =>
        new($"Investigation result field {location}.{key} is not {expected}.");
}
