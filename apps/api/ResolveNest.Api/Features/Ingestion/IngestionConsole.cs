namespace ResolveNest.Api.Features.Ingestion;

public static class IngestionExitCode
{
    public const int Success = 0;
    public const int ValidationFailed = 1;
    public const int PreflightFailed = 2;
    public const int NotCommitted = 3;
    public const int CommitOutcomeUnknown = 4;
    public const int CommittedButReportingFailed = 5;
    public const int UsageError = 64;
    public const int Cancelled = 130;
}

public sealed record IngestionOptions(string SourceDirectory, bool DryRun);

public static class IngestionConsole
{
    public static async Task WriteIssuesAsync(IReadOnlyList<IngestionIssue> issues, TextWriter output)
    {
        var warnings = issues.Where(issue => issue.Severity == IngestionIssueSeverity.Warning).ToList();
        var errors = issues.Where(issue => issue.Severity == IngestionIssueSeverity.Error).ToList();
        await output.WriteLineAsync($"Warnings: {warnings.Count}");
        foreach (var warning in warnings)
        {
            await output.WriteLineAsync($"  WARN  {warning.SourceRecord}  {warning.Message}");
        }

        await output.WriteLineAsync($"Errors: {errors.Count}");
        foreach (var error in errors)
        {
            await output.WriteLineAsync($"  ERROR {error.SourceRecord}  {error.Message}");
        }
    }

    public static async Task<int> CheckConstraintsAsync(GraphInspector inspector, TextWriter output, TextWriter errorOutput, CancellationToken cancellationToken)
    {
        try
        {
            var constraints = await inspector.CheckConstraintsAsync(cancellationToken);
            await output.WriteLineAsync($"Preflight: {constraints.Present.Count}/{GraphInspector.RequiredConstraints.Count} required uniqueness constraints present.");
            if (constraints.AllPresent)
            {
                return IngestionExitCode.Success;
            }

            await errorOutput.WriteLineAsync($"Preflight failed: missing constraints {string.Join(", ", constraints.Missing)}. Apply db/neo4j/schema.cypher first. Nothing was written.");
            return IngestionExitCode.PreflightFailed;
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException or OperationCanceledException)
        {
            await errorOutput.WriteLineAsync($"Preflight failed before any write: {exception.Message}");
            return exception is OperationCanceledException ? IngestionExitCode.Cancelled : IngestionExitCode.PreflightFailed;
        }
    }

    public static async Task<int> WriteAndReportAsync(
        Func<Task<GraphWriteResult>> write,
        GraphInspector inspector,
        string targetDescription,
        TextWriter output,
        TextWriter errorOutput)
    {
        GraphWriteResult result;
        try
        {
            result = await write();
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException)
        {
            await errorOutput.WriteLineAsync($"Write failed unexpectedly ({exception.Message}). Whether the transaction committed is unknown; check the graph before re-running.");
            return IngestionExitCode.CommitOutcomeUnknown;
        }

        var exitCode = await ReportWriteResultAsync(result, targetDescription, output, errorOutput);
        if (exitCode != IngestionExitCode.Success)
        {
            return exitCode;
        }

        try
        {
            var counts = await inspector.CountGraphAsync(CancellationToken.None);
            await output.WriteLineAsync($"Graph nodes: {FormatCounts(counts.NodesByLabel)}");
            await output.WriteLineAsync($"Graph relationships: {FormatCounts(counts.RelationshipsByType)}");
            return IngestionExitCode.Success;
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException)
        {
            await errorOutput.WriteLineAsync($"The import was committed, but reading graph counts afterwards failed: {exception.Message}");
            return IngestionExitCode.CommittedButReportingFailed;
        }
    }

    public static string FormatCounts(IEnumerable<(string Name, long Count)> counts) =>
        string.Join(" ", counts.Select(entry => $"{entry.Name}={entry.Count}"));

    private static async Task<int> ReportWriteResultAsync(GraphWriteResult result, string targetDescription, TextWriter output, TextWriter errorOutput)
    {
        await output.WriteLineAsync($"Existing nodes before write: {result.ExistingTargetNodes} {targetDescription}, {result.ExistingOtherNodes} other (left untouched).");
        switch (result.Outcome)
        {
            case GraphWriteOutcome.Committed:
                await output.WriteLineAsync($"Committed: nodesCreated={result.NodesCreated} relationshipsCreated={result.RelationshipsCreated} propertiesSet={result.PropertiesSet}");
                return IngestionExitCode.Success;
            case GraphWriteOutcome.PreflightRejected:
                await errorOutput.WriteLineAsync($"Preflight failed: {result.Problems.Count} problem(s) found before writing (showing up to 20 per kind):");
                foreach (var problem in result.Problems)
                {
                    await errorOutput.WriteLineAsync($"  CONFLICT {problem}");
                }

                await errorOutput.WriteLineAsync($"Nothing was written. Transaction {(result.RollbackConfirmed ? "rolled back" : "not committed; explicit rollback was not confirmed")}.");
                return IngestionExitCode.PreflightFailed;
            case GraphWriteOutcome.NotCommitted:
                var rollback = result.RollbackConfirmed ? "The transaction was rolled back." : "The transaction was not committed; the explicit rollback was not confirmed, and Neo4j discards uncommitted transactions.";
                if (result.WasCancelled)
                {
                    await errorOutput.WriteLineAsync($"Cancelled before commit. {rollback} No changes were committed.");
                    return IngestionExitCode.Cancelled;
                }

                await errorOutput.WriteLineAsync($"Write failed before commit: {result.FailureMessage} {rollback} No changes were committed.");
                return IngestionExitCode.NotCommitted;
            default:
                await errorOutput.WriteLineAsync($"Commit failed: {result.FailureMessage} The commit outcome is unknown; check the graph before re-running.");
                return IngestionExitCode.CommitOutcomeUnknown;
        }
    }
}
