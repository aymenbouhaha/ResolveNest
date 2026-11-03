using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed record RequiredConstraint(string Name, string Label);

public sealed record ConstraintCheckResult(IReadOnlyList<string> Present, IReadOnlyList<string> Missing)
{
    public bool AllPresent => Missing.Count == 0;
}

public sealed record GraphCounts(IReadOnlyList<(string Name, long Count)> NodesByLabel, IReadOnlyList<(string Name, long Count)> RelationshipsByType);

public sealed class GraphInspector(IDriver driver)
{
    public static readonly IReadOnlyList<RequiredConstraint> RequiredConstraints =
    [
        new("customer_id_unique", "Customer"),
        new("support_ticket_id_unique", "SupportTicket"),
        new("bug_id_unique", "Bug"),
        new("feature_id_unique", "Feature"),
        new("release_id_unique", "Release"),
        new("knowledge_article_id_unique", "KnowledgeArticle"),
    ];

    private const string ShowConstraints = """
        SHOW CONSTRAINTS
        YIELD name, type, labelsOrTypes, properties
        RETURN name, type, labelsOrTypes, properties
        """;

    private const string CountNodesByLabel = """
        MATCH (node)
        UNWIND labels(node) AS label
        RETURN label, count(*) AS count
        ORDER BY label
        """;

    private const string CountRelationshipsByType = """
        MATCH ()-[relationship]->()
        RETURN type(relationship) AS type, count(*) AS count
        ORDER BY type
        """;

    public async Task<ConstraintCheckResult> CheckConstraintsAsync(CancellationToken cancellationToken)
    {
        var result = await driver.ExecutableQuery(ShowConstraints).ExecuteAsync(cancellationToken);
        var present = new List<string>();
        var missing = new List<string>();
        foreach (var required in RequiredConstraints)
        {
            var matches = result.Result.Any(record =>
                record["name"].As<string>() == required.Name
                && record["type"].As<string>().Contains("UNIQUENESS", StringComparison.Ordinal)
                && record["labelsOrTypes"].As<List<string>>() is [var label] && label == required.Label
                && record["properties"].As<List<string>>() is ["id"]);
            (matches ? present : missing).Add(required.Name);
        }

        return new ConstraintCheckResult(present, missing);
    }

    public async Task<GraphCounts> CountGraphAsync(CancellationToken cancellationToken)
    {
        var nodes = await driver.ExecutableQuery(CountNodesByLabel).ExecuteAsync(cancellationToken);
        var relationships = await driver.ExecutableQuery(CountRelationshipsByType).ExecuteAsync(cancellationToken);
        return new GraphCounts(
            nodes.Result.Select(record => (record["label"].As<string>(), record["count"].As<long>())).ToList(),
            relationships.Result.Select(record => (record["type"].As<string>(), record["count"].As<long>())).ToList());
    }
}
