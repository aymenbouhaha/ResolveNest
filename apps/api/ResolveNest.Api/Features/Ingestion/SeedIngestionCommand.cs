using Microsoft.Extensions.Options;

namespace ResolveNest.Api.Features.Ingestion;

public sealed record SeedIngestionOptions(string SeedDirectory, bool DryRun);

public static class SeedIngestionExitCode
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

public sealed class SeedIngestionCommand(
    SeedSourceReader reader,
    SeedGraphBuilder builder,
    SeedGraphWriter writer,
    IOptions<SeedIngestionSettings> settings,
    TimeProvider timeProvider)
{
    public const string Name = "ingest-seed";
    private const string DefaultSeedDirectory = "data/seed";
    private const string Usage = "Usage: ingest-seed [--seed-dir <path>] [--dry-run]";

    public static bool IsInvocation(IReadOnlyList<string> args) => args.Count > 0 && args[0] == Name;

    public static async Task<int> ExecuteAsync(IServiceProvider services, IReadOnlyList<string> args)
    {
        if (!TryParseOptions(args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync(error);
            await Console.Error.WriteLineAsync(Usage);
            return SeedIngestionExitCode.UsageError;
        }

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;
        try
        {
            var command = services.GetRequiredService<SeedIngestionCommand>();
            return await command.RunAsync(options, Console.Out, Console.Error, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    public static bool TryParseOptions(IReadOnlyList<string> args, out SeedIngestionOptions options, out string error)
    {
        var seedDirectory = DefaultSeedDirectory;
        var dryRun = false;
        options = new SeedIngestionOptions(seedDirectory, dryRun);
        error = string.Empty;

        for (var index = 1; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--seed-dir" when index + 1 < args.Count:
                    seedDirectory = args[++index];
                    break;
                default:
                    error = $"Unknown or incomplete argument '{args[index]}'.";
                    return false;
            }
        }

        options = new SeedIngestionOptions(seedDirectory, dryRun);
        return true;
    }

    public async Task<int> RunAsync(SeedIngestionOptions options, TextWriter output, TextWriter errorOutput, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"ResolveNest seed ingestion ({(options.DryRun ? "dry run, no database access" : "write")})");

        SeedImportPlan plan;
        try
        {
            var seedDirectory = SeedDirectoryResolver.Resolve(options.SeedDirectory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
            await output.WriteLineAsync($"Seed directory: {seedDirectory.DisplayPath}");
            var source = await reader.ReadAsync(seedDirectory.FullPath, seedDirectory.DisplayPath, cancellationToken);
            plan = builder.Build(source, timeProvider.GetUtcNow(), settings.Value.DocumentedUnresolvedBugReferences);
        }
        catch (SeedSourceException exception)
        {
            await errorOutput.WriteLineAsync($"Source error: {exception.Message} The database was not contacted.");
            return SeedIngestionExitCode.ValidationFailed;
        }
        catch (OperationCanceledException)
        {
            await errorOutput.WriteLineAsync("Cancelled while reading the seed files. The database was not contacted.");
            return SeedIngestionExitCode.Cancelled;
        }

        await WritePlanSummaryAsync(plan, output);
        if (plan.HasErrors)
        {
            await errorOutput.WriteLineAsync("Validation failed. The database was not contacted.");
            return SeedIngestionExitCode.ValidationFailed;
        }

        if (options.DryRun)
        {
            await output.WriteLineAsync("Dry run complete. Validation passed; the database was not contacted.");
            return SeedIngestionExitCode.Success;
        }

        try
        {
            var constraints = await writer.CheckConstraintsAsync(cancellationToken);
            await output.WriteLineAsync($"Preflight: {constraints.Present.Count}/{SeedGraphWriter.RequiredConstraints.Count} required uniqueness constraints present.");
            if (!constraints.AllPresent)
            {
                await errorOutput.WriteLineAsync($"Preflight failed: missing constraints {string.Join(", ", constraints.Missing)}. Apply db/neo4j/schema.cypher first. Nothing was written.");
                return SeedIngestionExitCode.PreflightFailed;
            }
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException or OperationCanceledException)
        {
            await errorOutput.WriteLineAsync($"Preflight failed before any write: {exception.Message}");
            return exception is OperationCanceledException ? SeedIngestionExitCode.Cancelled : SeedIngestionExitCode.PreflightFailed;
        }

        GraphWriteResult result;
        try
        {
            result = await writer.WriteAsync(plan, cancellationToken);
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException)
        {
            await errorOutput.WriteLineAsync($"Write failed unexpectedly ({exception.Message}). Whether the transaction committed is unknown; check the graph before re-running.");
            return SeedIngestionExitCode.CommitOutcomeUnknown;
        }

        var exitCode = await ReportWriteResultAsync(result, output, errorOutput);
        if (exitCode != SeedIngestionExitCode.Success)
        {
            return exitCode;
        }

        try
        {
            var counts = await writer.CountGraphAsync(CancellationToken.None);
            await output.WriteLineAsync($"Graph nodes: {FormatCounts(counts.NodesByLabel)}");
            await output.WriteLineAsync($"Graph relationships: {FormatCounts(counts.RelationshipsByType)}");
            return SeedIngestionExitCode.Success;
        }
        catch (Exception exception) when (exception is Neo4j.Driver.Neo4jException or InvalidOperationException)
        {
            await errorOutput.WriteLineAsync($"The import was committed, but reading graph counts afterwards failed: {exception.Message}");
            return SeedIngestionExitCode.CommittedButReportingFailed;
        }
    }

    private static async Task<int> ReportWriteResultAsync(GraphWriteResult result, TextWriter output, TextWriter errorOutput)
    {
        await output.WriteLineAsync($"Existing nodes before write: {result.ExistingSeedNodes} with seed labels, {result.ExistingOtherNodes} with other labels (left untouched).");
        switch (result.Outcome)
        {
            case GraphWriteOutcome.Committed:
                await output.WriteLineAsync($"Committed: nodesCreated={result.NodesCreated} relationshipsCreated={result.RelationshipsCreated} propertiesSet={result.PropertiesSet}");
                return SeedIngestionExitCode.Success;
            case GraphWriteOutcome.OwnershipConflict:
                await errorOutput.WriteLineAsync($"Preflight failed: {result.Conflicts.Count} seed-labeled record(s) are not owned by seed ingestion or do not match the planned identity (showing up to 20 per kind):");
                foreach (var conflict in result.Conflicts)
                {
                    await errorOutput.WriteLineAsync($"  CONFLICT {conflict}");
                }

                await errorOutput.WriteLineAsync($"Nothing was written. Transaction {(result.RollbackConfirmed ? "rolled back" : "not committed; explicit rollback was not confirmed")}.");
                return SeedIngestionExitCode.PreflightFailed;
            case GraphWriteOutcome.NotCommitted:
                var rollback = result.RollbackConfirmed ? "The transaction was rolled back." : "The transaction was not committed; the explicit rollback was not confirmed, and Neo4j discards uncommitted transactions.";
                if (result.WasCancelled)
                {
                    await errorOutput.WriteLineAsync($"Cancelled before commit. {rollback} No changes were committed.");
                    return SeedIngestionExitCode.Cancelled;
                }

                await errorOutput.WriteLineAsync($"Write failed before commit: {result.FailureMessage} {rollback} No changes were committed.");
                return SeedIngestionExitCode.NotCommitted;
            default:
                await errorOutput.WriteLineAsync($"Commit failed: {result.FailureMessage} The commit outcome is unknown; check the graph before re-running.");
                return SeedIngestionExitCode.CommitOutcomeUnknown;
        }
    }

    private static async Task WritePlanSummaryAsync(SeedImportPlan plan, TextWriter output)
    {
        var counts = plan.SourceCounts;
        await output.WriteLineAsync($"Source records: customers={counts.Customers} features={counts.Features} releases={counts.Releases} tickets={counts.TicketRecords} ({counts.UniqueTickets} unique) bugs={counts.Bugs}");
        await output.WriteLineAsync($"Planned nodes: {FormatCounts(plan.NodeCounts.Select(entry => (entry.Label, (long)entry.Count)))}");
        await output.WriteLineAsync($"Planned relationships: {FormatCounts(plan.RelationshipCounts.Select(entry => (entry.Type, (long)entry.Count)))}");

        var warnings = plan.Issues.Where(issue => issue.Severity == IngestionIssueSeverity.Warning).ToList();
        var errors = plan.Issues.Where(issue => issue.Severity == IngestionIssueSeverity.Error).ToList();
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

    private static string FormatCounts(IEnumerable<(string Name, long Count)> counts) =>
        string.Join(" ", counts.Select(entry => $"{entry.Name}={entry.Count}"));
}
