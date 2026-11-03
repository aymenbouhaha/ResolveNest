using Microsoft.Extensions.Options;

namespace ResolveNest.Api.Features.Ingestion;

public sealed class SeedIngestionCommand(
    SeedSourceReader reader,
    SeedGraphBuilder builder,
    SeedGraphWriter writer,
    GraphInspector inspector,
    IOptions<SeedIngestionSettings> settings,
    TimeProvider timeProvider)
{
    public const string Name = "ingest-seed";
    public const string DefaultSeedDirectory = "data/seed";

    public async Task<int> RunAsync(IngestionOptions options, TextWriter output, TextWriter errorOutput, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"ResolveNest seed ingestion ({(options.DryRun ? "dry run, no database access" : "write")})");

        SeedImportPlan plan;
        try
        {
            var seedDirectory = SeedDirectoryResolver.Resolve(options.SourceDirectory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
            await output.WriteLineAsync($"Seed directory: {seedDirectory.DisplayPath}");
            var source = await reader.ReadAsync(seedDirectory.FullPath, seedDirectory.DisplayPath, cancellationToken);
            plan = builder.Build(source, timeProvider.GetUtcNow(), settings.Value.DocumentedUnresolvedBugReferences);
        }
        catch (SeedSourceException exception)
        {
            await errorOutput.WriteLineAsync($"Source error: {exception.Message} The database was not contacted.");
            return IngestionExitCode.ValidationFailed;
        }
        catch (OperationCanceledException)
        {
            await errorOutput.WriteLineAsync("Cancelled while reading the seed files. The database was not contacted.");
            return IngestionExitCode.Cancelled;
        }

        await WritePlanSummaryAsync(plan, output);
        if (plan.HasErrors)
        {
            await errorOutput.WriteLineAsync("Validation failed. The database was not contacted.");
            return IngestionExitCode.ValidationFailed;
        }

        if (options.DryRun)
        {
            await output.WriteLineAsync("Dry run complete. Validation passed; the database was not contacted.");
            return IngestionExitCode.Success;
        }

        var constraintExitCode = await IngestionConsole.CheckConstraintsAsync(inspector, output, errorOutput, cancellationToken);
        if (constraintExitCode != IngestionExitCode.Success)
        {
            return constraintExitCode;
        }

        return await IngestionConsole.WriteAndReportAsync(
            () => writer.WriteAsync(plan, cancellationToken),
            inspector,
            "with seed labels",
            output,
            errorOutput);
    }

    private static async Task WritePlanSummaryAsync(SeedImportPlan plan, TextWriter output)
    {
        var counts = plan.SourceCounts;
        await output.WriteLineAsync($"Source records: customers={counts.Customers} features={counts.Features} releases={counts.Releases} tickets={counts.TicketRecords} ({counts.UniqueTickets} unique) bugs={counts.Bugs}");
        await output.WriteLineAsync($"Planned nodes: {IngestionConsole.FormatCounts(plan.NodeCounts.Select(entry => (entry.Label, (long)entry.Count)))}");
        await output.WriteLineAsync($"Planned relationships: {IngestionConsole.FormatCounts(plan.RelationshipCounts.Select(entry => (entry.Type, (long)entry.Count)))}");
        await IngestionConsole.WriteIssuesAsync(plan.Issues, output);
    }
}
