using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public enum GraphWriteOutcome
{
    Committed,
    PreflightRejected,
    NotCommitted,
    CommitOutcomeUnknown,
}

public sealed record GraphPreflightResult(long ExistingTargetNodes, long ExistingOtherNodes, IReadOnlyList<string> Problems);

public sealed record GraphWriteResult(
    GraphWriteOutcome Outcome,
    int NodesCreated,
    int RelationshipsCreated,
    int PropertiesSet,
    long ExistingTargetNodes,
    long ExistingOtherNodes,
    IReadOnlyList<string> Problems,
    bool WasCancelled,
    bool RollbackConfirmed,
    string? FailureMessage);

public sealed class GraphWriteCounters
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

public static class GraphWriteTransaction
{
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromMinutes(2);

    public static async Task<GraphWriteResult> RunAsync(
        IDriver driver,
        Func<IAsyncTransaction, Task<GraphPreflightResult>> preflight,
        Func<IAsyncTransaction, GraphWriteCounters, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        await using var session = driver.AsyncSession();
        var counters = new GraphWriteCounters();
        var preflightResult = new GraphPreflightResult(0, 0, []);

        IAsyncTransaction transaction;
        try
        {
            transaction = await session.BeginTransactionAsync(config => config.WithTimeout(TransactionTimeout));
        }
        catch (Exception exception) when (exception is Neo4jException or InvalidOperationException)
        {
            return Result(GraphWriteOutcome.NotCommitted, counters, preflightResult, false, true, $"could not begin a transaction: {exception.Message}");
        }

        await using (transaction)
        {
            try
            {
                preflightResult = await preflight(transaction);
                if (preflightResult.Problems.Count > 0)
                {
                    var rolledBack = await TryRollBackAsync(transaction);
                    return Result(GraphWriteOutcome.PreflightRejected, counters, preflightResult, false, rolledBack, null);
                }

                await write(transaction, counters, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception exception) when (exception is Neo4jException or InvalidOperationException or OperationCanceledException)
            {
                var rolledBack = await TryRollBackAsync(transaction);
                return Result(GraphWriteOutcome.NotCommitted, counters, preflightResult, exception is OperationCanceledException, rolledBack, exception.Message);
            }

            try
            {
                await transaction.CommitAsync();
            }
            catch (Exception exception) when (exception is Neo4jException or InvalidOperationException)
            {
                return Result(GraphWriteOutcome.CommitOutcomeUnknown, counters, preflightResult, false, false, exception.Message);
            }
        }

        return Result(GraphWriteOutcome.Committed, counters, preflightResult, false, false, null);
    }

    public static async Task MergeNodesAsync(
        IAsyncTransaction transaction,
        string query,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        GraphWriteCounters counters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cursor = await transaction.RunAsync(query, new Dictionary<string, object?> { ["rows"] = rows });
        counters.Add(await cursor.ConsumeAsync());
    }

    public static async Task MergeRelationshipsAsync(
        IAsyncTransaction transaction,
        string query,
        string relationshipType,
        IReadOnlyList<GraphRelationship> relationships,
        GraphWriteCounters counters,
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

    private static GraphWriteResult Result(
        GraphWriteOutcome outcome,
        GraphWriteCounters counters,
        GraphPreflightResult preflight,
        bool wasCancelled,
        bool rollbackConfirmed,
        string? failureMessage) =>
        new(
            outcome,
            counters.NodesCreated,
            counters.RelationshipsCreated,
            counters.PropertiesSet,
            preflight.ExistingTargetNodes,
            preflight.ExistingOtherNodes,
            preflight.Problems,
            wasCancelled,
            rollbackConfirmed,
            failureMessage);

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
}
