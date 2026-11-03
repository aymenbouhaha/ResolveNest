namespace ResolveNest.Api.Features.Ingestion;

public static class IngestionCommandLine
{
    private const string Usage = """
        Usage:
          ingest-seed      [--seed-dir <path>]      [--dry-run]
          ingest-knowledge [--knowledge-dir <path>] [--dry-run]
        """;

    private static readonly IReadOnlyDictionary<string, (string DirectoryOption, string DefaultDirectory)> Commands =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            [SeedIngestionCommand.Name] = ("--seed-dir", SeedIngestionCommand.DefaultSeedDirectory),
            [KnowledgeIngestionCommand.Name] = ("--knowledge-dir", KnowledgeIngestionCommand.DefaultKnowledgeDirectory),
        };

    public static bool IsInvocation(IReadOnlyList<string> args) => args.Count > 0 && Commands.ContainsKey(args[0]);

    public static bool TryParseOptions(IReadOnlyList<string> args, out IngestionOptions options, out string error)
    {
        var (directoryOption, defaultDirectory) = Commands[args[0]];
        var directory = defaultDirectory;
        var dryRun = false;
        options = new IngestionOptions(directory, dryRun);
        error = string.Empty;

        for (var index = 1; index < args.Count; index++)
        {
            if (args[index] == "--dry-run")
            {
                dryRun = true;
            }
            else if (args[index] == directoryOption && index + 1 < args.Count)
            {
                directory = args[++index];
            }
            else
            {
                error = $"Unknown or incomplete argument '{args[index]}' for {args[0]}.";
                return false;
            }
        }

        options = new IngestionOptions(directory, dryRun);
        return true;
    }

    public static async Task<int> ExecuteAsync(IServiceProvider services, IReadOnlyList<string> args)
    {
        if (!TryParseOptions(args, out var options, out var error))
        {
            await Console.Error.WriteLineAsync(error);
            await Console.Error.WriteLineAsync(Usage);
            return IngestionExitCode.UsageError;
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
            return args[0] == KnowledgeIngestionCommand.Name
                ? await services.GetRequiredService<KnowledgeIngestionCommand>().RunAsync(options, Console.Out, Console.Error, cancellation.Token)
                : await services.GetRequiredService<SeedIngestionCommand>().RunAsync(options, Console.Out, Console.Error, cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
