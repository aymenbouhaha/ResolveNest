namespace ResolveNest.Api.Features.Ingestion;

public sealed class KnowledgeIngestionCommand(
    KnowledgeSourceReader reader,
    KnowledgeGraphBuilder builder,
    KnowledgeGraphWriter writer,
    GraphInspector inspector,
    TimeProvider timeProvider)
{
    public const string Name = "ingest-knowledge";
    public const string DefaultKnowledgeDirectory = "data/seed/knowledge";

    public async Task<int> RunAsync(IngestionOptions options, TextWriter output, TextWriter errorOutput, CancellationToken cancellationToken)
    {
        await output.WriteLineAsync($"ResolveNest knowledge ingestion ({(options.DryRun ? "dry run, no database access" : "write")})");

        KnowledgeImportPlan plan;
        try
        {
            var directory = SeedDirectoryResolver.Resolve(options.SourceDirectory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory);
            await output.WriteLineAsync($"Knowledge directory: {directory.DisplayPath}");
            var source = await reader.ReadAsync(directory.FullPath, directory.DisplayPath, cancellationToken);
            plan = builder.Build(source, timeProvider.GetUtcNow());
        }
        catch (SeedSourceException exception)
        {
            await errorOutput.WriteLineAsync($"Source error: {exception.Message} The database was not contacted.");
            return IngestionExitCode.ValidationFailed;
        }
        catch (OperationCanceledException)
        {
            await errorOutput.WriteLineAsync("Cancelled while reading the knowledge files. The database was not contacted.");
            return IngestionExitCode.Cancelled;
        }

        await output.WriteLineAsync($"Manifest records: {plan.ManifestRecords}");
        await output.WriteLineAsync($"Planned nodes: KnowledgeArticle={plan.Articles.Count}");
        await output.WriteLineAsync($"Planned relationships: ADDRESSES={plan.Addresses.Count} COVERS={plan.Covers.Count} (distinct bugs={plan.ReferencedBugIds.Count}, distinct features={plan.ReferencedFeatureIds.Count})");
        await IngestionConsole.WriteIssuesAsync(plan.Issues, output);
        if (plan.HasErrors)
        {
            await errorOutput.WriteLineAsync("Validation failed. The database was not contacted.");
            return IngestionExitCode.ValidationFailed;
        }

        if (options.DryRun)
        {
            await output.WriteLineAsync("Dry run complete. Files and manifest are valid; bug and feature references are checked against the graph only during a real import.");
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
            "KnowledgeArticle",
            output,
            errorOutput);
    }
}
