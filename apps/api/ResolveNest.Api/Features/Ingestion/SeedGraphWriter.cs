using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed record RequiredConstraint(string Name, string Label);

public sealed record ConstraintCheckResult(IReadOnlyList<string> Present, IReadOnlyList<string> Missing)
{
    public bool AllPresent => Missing.Count == 0;
}

public enum GraphWriteOutcome
{
    Committed,
    OwnershipConflict,
    NotCommitted,
    CommitOutcomeUnknown,
}

public sealed record GraphWriteResult(
    GraphWriteOutcome Outcome,
    int NodesCreated,
    int RelationshipsCreated,
    int PropertiesSet,
    long ExistingSeedNodes,
    long ExistingOtherNodes,
    IReadOnlyList<string> Conflicts,
    bool WasCancelled,
    bool RollbackConfirmed,
    string? FailureMessage);

public sealed record GraphCounts(IReadOnlyList<(string Name, long Count)> NodesByLabel, IReadOnlyList<(string Name, long Count)> RelationshipsByType);

public sealed class SeedGraphWriter(IDriver driver)
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

    public static readonly IReadOnlyList<string> SeedLabels = ["Customer", "Feature", "Release", "Bug", "SupportTicket"];

    public static readonly IReadOnlyList<string> SeedRelationshipTypes = ["REPORTED", "ABOUT_FEATURE", "CONFIRMED_AS", "AFFECTS", "INTRODUCED_IN", "FIXED_IN"];

    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromMinutes(2);

    public async Task<ConstraintCheckResult> CheckConstraintsAsync(CancellationToken cancellationToken)
    {
        var result = await driver.ExecutableQuery(SeedGraphQueries.ShowConstraints).ExecuteAsync(cancellationToken);
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

    public async Task<GraphWriteResult> WriteAsync(SeedImportPlan plan, CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession();
        var counters = new WriteCounters();
        long existingSeedNodes = 0;
        long existingOtherNodes = 0;

        IAsyncTransaction transaction;
        try
        {
            transaction = await session.BeginTransactionAsync(config => config.WithTimeout(TransactionTimeout));
        }
        catch (Exception exception) when (exception is Neo4jException or InvalidOperationException)
        {
            return Failure(GraphWriteOutcome.NotCommitted, counters, 0, 0, [], false, true, $"could not begin a transaction: {exception.Message}");
        }

        await using (transaction)
        {
            try
            {
                (existingSeedNodes, existingOtherNodes) = await CountExistingNodesAsync(transaction);
                var conflicts = await FindOwnershipConflictsAsync(transaction, plan);
                if (conflicts.Count > 0)
                {
                    var rolledBack = await TryRollBackAsync(transaction);
                    return Failure(GraphWriteOutcome.OwnershipConflict, counters, existingSeedNodes, existingOtherNodes, conflicts, false, rolledBack, null);
                }

                await MergeNodesAsync(transaction, SeedGraphQueries.MergeCustomers, plan.Customers, counters, cancellationToken);
                await MergeNodesAsync(transaction, SeedGraphQueries.MergeFeatures, plan.Features, counters, cancellationToken);
                await MergeNodesAsync(transaction, SeedGraphQueries.MergeReleases, plan.Releases, counters, cancellationToken);
                await MergeNodesAsync(transaction, SeedGraphQueries.MergeBugs, plan.Bugs, counters, cancellationToken);
                await MergeNodesAsync(transaction, SeedGraphQueries.MergeTickets, plan.Tickets, counters, cancellationToken);
                foreach (var (type, relationships) in PlannedRelationshipsByType(plan))
                {
                    await MergeRelationshipsAsync(transaction, RelationshipMergeQuery(type), type, relationships, counters, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (exception is Neo4jException or InvalidOperationException or OperationCanceledException)
            {
                var rolledBack = await TryRollBackAsync(transaction);
                return Failure(GraphWriteOutcome.NotCommitted, counters, existingSeedNodes, existingOtherNodes, [], exception is OperationCanceledException, rolledBack, exception.Message);
            }

            try
            {
                await transaction.CommitAsync();
            }
            catch (Exception exception) when (exception is Neo4jException or InvalidOperationException)
            {
                return Failure(GraphWriteOutcome.CommitOutcomeUnknown, counters, existingSeedNodes, existingOtherNodes, [], false, false, exception.Message);
            }
        }

        return new GraphWriteResult(GraphWriteOutcome.Committed, counters.NodesCreated, counters.RelationshipsCreated, counters.PropertiesSet, existingSeedNodes, existingOtherNodes, [], false, false, null);
    }

    public async Task<GraphCounts> CountGraphAsync(CancellationToken cancellationToken)
    {
        var nodes = await driver.ExecutableQuery(SeedGraphQueries.CountNodesByLabel).ExecuteAsync(cancellationToken);
        var relationships = await driver.ExecutableQuery(SeedGraphQueries.CountRelationshipsByType).ExecuteAsync(cancellationToken);
        return new GraphCounts(
            nodes.Result.Select(record => (record["label"].As<string>(), record["count"].As<long>())).ToList(),
            relationships.Result.Select(record => (record["type"].As<string>(), record["count"].As<long>())).ToList());
    }

    private static GraphWriteResult Failure(
        GraphWriteOutcome outcome,
        WriteCounters counters,
        long existingSeedNodes,
        long existingOtherNodes,
        IReadOnlyList<string> conflicts,
        bool wasCancelled,
        bool rollbackConfirmed,
        string? failureMessage) =>
        new(outcome, counters.NodesCreated, counters.RelationshipsCreated, counters.PropertiesSet, existingSeedNodes, existingOtherNodes, conflicts, wasCancelled, rollbackConfirmed, failureMessage);

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

    private static async Task<(long SeedLabeled, long Other)> CountExistingNodesAsync(IAsyncTransaction transaction)
    {
        var cursor = await transaction.RunAsync(SeedGraphQueries.CountExistingNodes, new Dictionary<string, object?> { ["seedLabels"] = SeedLabels });
        var record = await cursor.SingleAsync();
        var seedLabeled = record["seedLabeled"].As<long>();
        return (seedLabeled, record["total"].As<long>() - seedLabeled);
    }

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

    private static async Task MergeNodesAsync(
        IAsyncTransaction transaction,
        string query,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        WriteCounters counters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.RunAsync(query, new Dictionary<string, object?> { ["rows"] = rows });
        counters.Add(await cursor.ConsumeAsync());
    }

    private static async Task MergeRelationshipsAsync(
        IAsyncTransaction transaction,
        string query,
        string relationshipType,
        IReadOnlyList<GraphRelationship> relationships,
        WriteCounters counters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rows = relationships
            .Select(relationship => new Dictionary<string, object?>
            {
                ["startId"] = relationship.StartId,
                ["endId"] = relationship.EndId,
                ["properties"] = relationship.Properties,
            })
            .ToList();
        var cursor = await transaction.RunAsync(query, new Dictionary<string, object?> { ["rows"] = rows });
        var record = await cursor.SingleAsync();
        var written = record["written"].As<int>();
        counters.Add(await cursor.ConsumeAsync());
        if (written != rows.Count)
        {
            throw new InvalidOperationException($"{relationshipType}: wrote {written} of {rows.Count} relationships; at least one endpoint node was not found.");
        }
    }

    private static async Task<bool> TryRollBackAsync(IAsyncTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
            return true;
        }
        catch (Exception exception) when (exception is Neo4jException or InvalidOperationException)
        {
            return false;
        }
    }

    private sealed class WriteCounters
    {
        public int NodesCreated { get; private set; }
        public int RelationshipsCreated { get; private set; }
        public int PropertiesSet { get; private set; }

        public void Add(IResultSummary summary)
        {
            NodesCreated += summary.Counters.NodesCreated;
            RelationshipsCreated += summary.Counters.RelationshipsCreated;
            PropertiesSet += summary.Counters.PropertiesSet;
        }
    }
}
