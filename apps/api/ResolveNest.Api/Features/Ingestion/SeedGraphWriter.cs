using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed class SeedGraphWriter(IDriver driver)
{
    public static readonly IReadOnlyList<string> SeedLabels = ["Customer", "Feature", "Release", "Bug", "SupportTicket"];

    public static readonly IReadOnlyList<string> SeedRelationshipTypes = ["REPORTED", "ABOUT_FEATURE", "CONFIRMED_AS", "AFFECTS", "INTRODUCED_IN", "FIXED_IN"];

    public static Dictionary<string, object?> PlannedNodeIdentities(SeedImportPlan plan)
    {
        var identities = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (label, rows) in PlannedNodesByLabel(plan))
        {
            foreach (var row in rows)
            {
                identities[$"{label}|{row["id"]}"] = $"{row["sourceSystem"]}|{row["sourceId"]}";
            }
        }

        return identities;
    }

    public static Dictionary<string, object?> PlannedRelationshipSources(SeedImportPlan plan)
    {
        var sources = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (type, relationships) in PlannedRelationshipsByType(plan))
        {
            foreach (var relationship in relationships)
            {
                sources[$"{type}|{relationship.StartId}|{relationship.EndId}"] = relationship.Properties["sourceSystem"];
            }
        }

        return sources;
    }

    public Task<GraphWriteResult> WriteAsync(SeedImportPlan plan, CancellationToken cancellationToken) =>
        GraphWriteTransaction.RunAsync(
            driver,
            transaction => RunPreflightAsync(transaction, plan),
            (transaction, counters, token) => WritePlanAsync(transaction, plan, counters, token),
            cancellationToken);

    private static async Task<GraphPreflightResult> RunPreflightAsync(IAsyncTransaction transaction, SeedImportPlan plan)
    {
        var countCursor = await transaction.RunAsync(SeedGraphQueries.CountExistingNodes, new Dictionary<string, object?> { ["seedLabels"] = SeedLabels });
        var countRecord = await countCursor.SingleAsync();
        var seedLabeled = countRecord["seedLabeled"].As<long>();
        var other = countRecord["total"].As<long>() - seedLabeled;
        return new GraphPreflightResult(seedLabeled, other, await FindOwnershipConflictsAsync(transaction, plan));
    }

    private static async Task WritePlanAsync(IAsyncTransaction transaction, SeedImportPlan plan, GraphWriteCounters counters, CancellationToken cancellationToken)
    {
        await GraphWriteTransaction.MergeNodesAsync(transaction, SeedGraphQueries.MergeCustomers, plan.Customers, counters, cancellationToken);
        await GraphWriteTransaction.MergeNodesAsync(transaction, SeedGraphQueries.MergeFeatures, plan.Features, counters, cancellationToken);
        await GraphWriteTransaction.MergeNodesAsync(transaction, SeedGraphQueries.MergeReleases, plan.Releases, counters, cancellationToken);
        await GraphWriteTransaction.MergeNodesAsync(transaction, SeedGraphQueries.MergeBugs, plan.Bugs, counters, cancellationToken);
        await GraphWriteTransaction.MergeNodesAsync(transaction, SeedGraphQueries.MergeTickets, plan.Tickets, counters, cancellationToken);
        foreach (var (type, relationships) in PlannedRelationshipsByType(plan))
        {
            await GraphWriteTransaction.MergeRelationshipsAsync(transaction, RelationshipMergeQuery(type), type, relationships, counters, cancellationToken);
        }
    }

    private static IEnumerable<(string Label, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows)> PlannedNodesByLabel(SeedImportPlan plan) =>
    [
        ("Customer", plan.Customers),
        ("Feature", plan.Features),
        ("Release", plan.Releases),
        ("Bug", plan.Bugs),
        ("SupportTicket", plan.Tickets),
    ];

    private static IEnumerable<(string Type, IReadOnlyList<GraphRelationship> Relationships)> PlannedRelationshipsByType(SeedImportPlan plan) =>
    [
        ("REPORTED", plan.Reported),
        ("ABOUT_FEATURE", plan.AboutFeature),
        ("CONFIRMED_AS", plan.ConfirmedAs),
        ("AFFECTS", plan.Affects),
        ("INTRODUCED_IN", plan.IntroducedIn),
        ("FIXED_IN", plan.FixedIn),
    ];

    private static string RelationshipMergeQuery(string relationshipType) => relationshipType switch
    {
        "REPORTED" => SeedGraphQueries.MergeReported,
        "ABOUT_FEATURE" => SeedGraphQueries.MergeAboutFeature,
        "CONFIRMED_AS" => SeedGraphQueries.MergeConfirmedAs,
        "AFFECTS" => SeedGraphQueries.MergeAffects,
        "INTRODUCED_IN" => SeedGraphQueries.MergeIntroducedIn,
        "FIXED_IN" => SeedGraphQueries.MergeFixedIn,
        _ => throw new InvalidOperationException($"No merge query for relationship type {relationshipType}."),
    };

    private static async Task<IReadOnlyList<string>> FindOwnershipConflictsAsync(IAsyncTransaction transaction, SeedImportPlan plan)
    {
        var conflicts = new List<string>();

        var nodeCursor = await transaction.RunAsync(SeedGraphQueries.FindSeedNodeOwnershipConflicts, new Dictionary<string, object?>
        {
            ["seedLabels"] = SeedLabels,
            ["seedOwner"] = SeedGraphQueries.SeedOwner,
            ["plannedNodeIdentities"] = PlannedNodeIdentities(plan),
        });
        foreach (var record in await nodeCursor.ToListAsync())
        {
            conflicts.Add($"node {string.Join(":", record["labels"].As<List<string>>())} {record["id"].As<string?>() ?? "(no id)"} ingestedBy={record["ingestedBy"].As<string?>() ?? "(none)"} existing={record["existingIdentity"].As<string?>() ?? "(none)"} planned={record["plannedIdentity"].As<string?>() ?? "(not in this import)"}");
        }

        var relationshipCursor = await transaction.RunAsync(SeedGraphQueries.FindSeedRelationshipOwnershipConflicts, new Dictionary<string, object?>
        {
            ["seedRelationshipTypes"] = SeedRelationshipTypes,
            ["seedOwner"] = SeedGraphQueries.SeedOwner,
            ["plannedRelationshipSources"] = PlannedRelationshipSources(plan),
        });
        foreach (var record in await relationshipCursor.ToListAsync())
        {
            conflicts.Add($"relationship {record["type"].As<string>()} {record["startId"].As<string?>() ?? "(no id)"} -> {record["endId"].As<string?>() ?? "(no id)"} ingestedBy={record["ingestedBy"].As<string?>() ?? "(none)"}");
        }

        return conflicts;
    }
}
