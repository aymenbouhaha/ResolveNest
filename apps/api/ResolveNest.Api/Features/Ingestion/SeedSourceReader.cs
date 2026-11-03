using System.Text.Json;

namespace ResolveNest.Api.Features.Ingestion;

public sealed class SeedSourceReader
{
    private static readonly JsonSerializerOptions SourceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<SeedSourceSet> ReadAsync(string seedDirectory, string displayDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(seedDirectory))
        {
            throw new SeedSourceException($"Seed directory '{displayDirectory}' does not exist.");
        }

        var customers = await ReadCsvAsync(seedDirectory, displayDirectory, "customers.csv", cancellationToken);
        var features = await ReadCsvAsync(seedDirectory, displayDirectory, "features.csv", cancellationToken);
        var releases = await ReadCsvAsync(seedDirectory, displayDirectory, "releases.csv", cancellationToken);
        var tickets = await ReadJsonAsync<TicketSource>(seedDirectory, displayDirectory, "tickets.json", cancellationToken);
        var bugs = await ReadJsonAsync<BugSource>(seedDirectory, displayDirectory, "bugs.json", cancellationToken);

        return new SeedSourceSet(customers, features, releases, tickets, bugs);
    }

    private static async Task<IReadOnlyList<CsvSourceRow>> ReadCsvAsync(
        string seedDirectory,
        string displayDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var content = await ReadFileAsync(Path.Combine(seedDirectory, fileName), cancellationToken);
        return CsvTableParser.Parse(content, $"{displayDirectory}/{fileName}");
    }

    private static async Task<IReadOnlyList<LocatedRecord<TRecord>>> ReadJsonAsync<TRecord>(
        string seedDirectory,
        string displayDirectory,
        string fileName,
        CancellationToken cancellationToken)
    {
        var displayPath = $"{displayDirectory}/{fileName}";
        var content = await ReadFileAsync(Path.Combine(seedDirectory, fileName), cancellationToken);

        JsonSourceDocument<TRecord>? document;
        try
        {
            document = JsonSerializer.Deserialize<JsonSourceDocument<TRecord>>(content, SourceJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new SeedSourceException($"{displayPath} is not valid JSON: {exception.Message}", exception);
        }

        if (document?.Records is null)
        {
            throw new SeedSourceException($"{displayPath} has no 'records' array.");
        }

        var synthetic = document.Synthetic == true;
        return document.Records
            .Select((record, index) => new LocatedRecord<TRecord>(new SourceLocation(displayPath, $"/records/{index}"), record, synthetic))
            .ToList();
    }

    private static async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new SeedSourceException($"Seed file '{path}' does not exist.");
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }
}
