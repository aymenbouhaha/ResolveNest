using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed class KnowledgeGraphWriter(IDriver driver)
{
    public static readonly IReadOnlyList<string> KnowledgeRelationshipTypes = ["ADDRESSES", "COVERS"];

    public static Dictionary<string, object?> PlannedArticleIdentities(KnowledgeImportPlan plan) =>
        plan.Articles.ToDictionary(
            row => (string)row["id"]!,
            row => (object?)$"{row["sourceSystem"]}|{row["sourceId"]}",
            StringComparer.Ordinal);

    public static Dictionary<string, object?> PlannedRelationshipSources(KnowledgeImportPlan plan)
    {
        var sources = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (type, relationships) in new[] { ("ADDRESSES", plan.Addresses), ("COVERS", plan.Covers) })
        {
            foreach (var relationship in relationships)
            {
                sources[$"{type}|{relationship.StartId}|{relationship.EndId}"] = relationship.Properties["sourceSystem"];
            }
        }

        return sources;
    }

    public Task<GraphWriteResult> WriteAsync(KnowledgeImportPlan plan, CancellationToken cancellationToken) =>
        GraphWriteTransaction.RunAsync(
            driver,
            transaction => RunPreflightAsync(transaction, plan),
            async (transaction, counters, token) =>
            {
                await GraphWriteTransaction.MergeNodesAsync(transaction, KnowledgeGraphQueries.MergeArticles, plan.Articles, counters, token);
                await GraphWriteTransaction.MergeRelationshipsAsync(transaction, KnowledgeGraphQueries.MergeAddresses, "ADDRESSES", plan.Addresses, counters, token);
                await GraphWriteTransaction.MergeRelationshipsAsync(transaction, KnowledgeGraphQueries.MergeCovers, "COVERS", plan.Covers, counters, token);
            },
            cancellationToken);

    public static async Task<IReadOnlyList<string>> FindProblemsAsync(IAsyncTransaction transaction, KnowledgeImportPlan plan)
    {
        var problems = new List<string>();

        foreach (var id in await ReadIdsAsync(transaction, KnowledgeGraphQueries.FindMissingBugs, plan.ReferencedBugIds))
        {
            problems.Add($"missing reference: bug_ref {id} is not a Bug node in the graph; run ingest-seed first or fix the manifest");
        }

        foreach (var id in await ReadIdsAsync(transaction, KnowledgeGraphQueries.FindMissingFeatures, plan.ReferencedFeatureIds))
        {
            problems.Add($"missing reference: feature_ref {id} is not a Feature node in the graph; run ingest-seed first or fix the manifest");
        }

        var articleCursor = await transaction.RunAsync(KnowledgeGraphQueries.FindArticleOwnershipConflicts, new Dictionary<string, object?>
        {
            ["knowledgeOwner"] = KnowledgeGraphBuilder.KnowledgeOwner,
            ["plannedArticleIdentities"] = PlannedArticleIdentities(plan),
        });
        foreach (var record in await articleCursor.ToListAsync())
        {
            problems.Add($"ownership: node {string.Join(":", record["labels"].As<List<string>>())} {record["id"].As<string?>() ?? "(no id)"} ingestedBy={record["ingestedBy"].As<string?>() ?? "(none)"} existing={record["existingIdentity"].As<string?>() ?? "(none)"} planned={record["plannedIdentity"].As<string?>() ?? "(not in this import)"}");
        }

        var relationshipCursor = await transaction.RunAsync(KnowledgeGraphQueries.FindKnowledgeRelationshipOwnershipConflicts, new Dictionary<string, object?>
        {
            ["knowledgeRelationshipTypes"] = KnowledgeRelationshipTypes,
            ["knowledgeOwner"] = KnowledgeGraphBuilder.KnowledgeOwner,
            ["plannedRelationshipSources"] = PlannedRelationshipSources(plan),
        });
        foreach (var record in await relationshipCursor.ToListAsync())
        {
            problems.Add($"ownership: relationship {record["type"].As<string>()} {record["startId"].As<string?>() ?? "(no id)"} -> {record["endId"].As<string?>() ?? "(no id)"} ingestedBy={record["ingestedBy"].As<string?>() ?? "(none)"}");
        }

        return problems;
    }

    private static async Task<GraphPreflightResult> RunPreflightAsync(IAsyncTransaction transaction, KnowledgeImportPlan plan)
    {
        var countCursor = await transaction.RunAsync(KnowledgeGraphQueries.CountExistingNodes);
        var countRecord = await countCursor.SingleAsync();
        var articles = countRecord["articles"].As<long>();
        var other = countRecord["total"].As<long>() - articles;
        return new GraphPreflightResult(articles, other, await FindProblemsAsync(transaction, plan));
    }

    private static async Task<IReadOnlyList<string>> ReadIdsAsync(IAsyncTransaction transaction, string query, IReadOnlyList<string> ids)
    {
        var cursor = await transaction.RunAsync(query, new Dictionary<string, object?> { ["ids"] = ids });
        return (await cursor.ToListAsync()).Select(record => record["id"].As<string>()).ToList();
    }
}
