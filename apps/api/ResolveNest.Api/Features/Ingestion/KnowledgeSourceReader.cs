using System.Text;
using System.Text.Json;

namespace ResolveNest.Api.Features.Ingestion;

public sealed record KnowledgeArticleSource(
    string? Id,
    string? SourceSystem,
    string? SourceId,
    string? File,
    string? Title,
    string? Summary,
    IReadOnlyList<string>? FeatureRefs,
    IReadOnlyList<string>? BugRefs,
    string? UpdatedAt);

public sealed record KnowledgeArticleFile(string? DisplayPath, string? Content, string? Error);

public sealed record LocatedKnowledgeArticle(SourceLocation Location, KnowledgeArticleSource Record, KnowledgeArticleFile File);

public sealed record KnowledgeSourceSet(
    string ManifestDisplayPath,
    bool Synthetic,
    IReadOnlyList<LocatedKnowledgeArticle> Articles,
    IReadOnlyList<string> UnlistedMarkdownFiles);

public sealed class KnowledgeSourceReader
{
    public const string ManifestFileName = "manifest.json";
    public const string ArticleExtension = ".md";

    private static readonly JsonSerializerOptions SourceJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public async Task<KnowledgeSourceSet> ReadAsync(string knowledgeDirectory, string displayDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(knowledgeDirectory))
        {
            throw new SeedSourceException($"Knowledge directory '{displayDirectory}' does not exist.");
        }

        var manifestDisplayPath = $"{displayDirectory}/{ManifestFileName}";
        var manifestPath = Path.Combine(knowledgeDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            throw new SeedSourceException($"Knowledge manifest '{manifestDisplayPath}' does not exist.");
        }

        var manifestText = DecodeStrictUtf8(await File.ReadAllBytesAsync(manifestPath, cancellationToken))
            ?? throw new SeedSourceException($"{manifestDisplayPath} is not valid UTF-8.");

        JsonSourceDocument<KnowledgeArticleSource>? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<JsonSourceDocument<KnowledgeArticleSource>>(manifestText, SourceJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new SeedSourceException($"{manifestDisplayPath} is not valid JSON: {exception.Message}", exception);
        }

        if (manifest?.Records is null)
        {
            throw new SeedSourceException($"{manifestDisplayPath} has no 'records' array.");
        }

        var articles = new List<LocatedKnowledgeArticle>(manifest.Records.Count);
        for (var index = 0; index < manifest.Records.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = manifest.Records[index];
            var file = await LoadArticleFileAsync(knowledgeDirectory, displayDirectory, record.File, cancellationToken);
            articles.Add(new LocatedKnowledgeArticle(new SourceLocation(manifestDisplayPath, $"/records/{index}"), record, file));
        }

        var listedFiles = manifest.Records
            .Select(record => record.File?.Replace('\\', '/'))
            .Where(file => file is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unlistedMarkdownFiles = Directory.EnumerateFiles(knowledgeDirectory, $"*{ArticleExtension}", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null && !listedFiles.Contains(fileName))
            .Select(fileName => $"{displayDirectory}/{fileName}")
            .Order(StringComparer.Ordinal)
            .ToList();

        return new KnowledgeSourceSet(manifestDisplayPath, manifest.Synthetic == true, articles, unlistedMarkdownFiles);
    }

    public static string? FindUnsafePathReason(string? relativeFile)
    {
        if (string.IsNullOrWhiteSpace(relativeFile))
        {
            return "required field 'file' is missing or empty.";
        }

        if (Path.IsPathRooted(relativeFile) || relativeFile.Contains(':'))
        {
            return $"file '{relativeFile}' must be a relative path inside the knowledge directory.";
        }

        if (relativeFile.Split('/', '\\').Any(segment => segment is "" or "." or ".."))
        {
            return $"file '{relativeFile}' contains an empty, '.' or '..' path segment.";
        }

        if (!relativeFile.EndsWith(ArticleExtension, StringComparison.OrdinalIgnoreCase))
        {
            return $"file '{relativeFile}' must be a {ArticleExtension} file.";
        }

        return null;
    }

    private static async Task<KnowledgeArticleFile> LoadArticleFileAsync(
        string knowledgeDirectory,
        string displayDirectory,
        string? relativeFile,
        CancellationToken cancellationToken)
    {
        var unsafeReason = FindUnsafePathReason(relativeFile);
        if (unsafeReason is not null)
        {
            return new KnowledgeArticleFile(null, null, unsafeReason);
        }

        var normalizedRelativeFile = relativeFile!.Replace('\\', '/');
        var displayPath = $"{displayDirectory}/{normalizedRelativeFile}";
        var directoryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(knowledgeDirectory)) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(directoryRoot, normalizedRelativeFile));
        if (!fullPath.StartsWith(directoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            return new KnowledgeArticleFile(displayPath, null, $"file '{relativeFile}' resolves outside the knowledge directory.");
        }

        if (!File.Exists(fullPath))
        {
            return new KnowledgeArticleFile(displayPath, null, $"file '{relativeFile}' does not exist.");
        }

        if (File.GetAttributes(fullPath).HasFlag(FileAttributes.ReparsePoint))
        {
            return new KnowledgeArticleFile(displayPath, null, $"file '{relativeFile}' is a link; links are not followed.");
        }

        var content = DecodeStrictUtf8(await File.ReadAllBytesAsync(fullPath, cancellationToken));
        return content is null
            ? new KnowledgeArticleFile(displayPath, null, $"file '{relativeFile}' is not valid UTF-8.")
            : new KnowledgeArticleFile(displayPath, NormalizeLineEndings(content), null);
    }

    private static string? DecodeStrictUtf8(byte[] bytes)
    {
        try
        {
            var text = StrictUtf8.GetString(bytes);
            return text.StartsWith('﻿') ? text[1..] : text;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');
}
