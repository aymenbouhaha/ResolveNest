using System.Globalization;
using Neo4j.Driver;

namespace ResolveNest.Api.Features.Ingestion;

public sealed record KnowledgeImportPlan(
    int ManifestRecords,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Articles,
    IReadOnlyList<GraphRelationship> Addresses,
    IReadOnlyList<GraphRelationship> Covers,
    IReadOnlyList<string> ReferencedBugIds,
    IReadOnlyList<string> ReferencedFeatureIds,
    IReadOnlyList<IngestionIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == IngestionIssueSeverity.Error);
}

public sealed class KnowledgeGraphBuilder
{
    public const string KnowledgeOwner = "knowledge-ingestion";
    public const int MinimumBodyCharacters = 80;

    public KnowledgeImportPlan Build(KnowledgeSourceSet source, DateTimeOffset ingestedAt)
    {
        var issues = new List<IngestionIssue>();
        var ingestedAtValue = new ZonedDateTime(ingestedAt.ToUniversalTime());
        var articles = new List<IReadOnlyDictionary<string, object?>>();
        var addresses = new List<GraphRelationship>();
        var covers = new List<GraphRelationship>();
        var articleIds = new HashSet<string>(StringComparer.Ordinal);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sourceLocation, article, file) in source.Articles)
        {
            var location = sourceLocation.ToString();
            void Error(string message) => issues.Add(new IngestionIssue(IngestionIssueSeverity.Error, location, message));

            var id = Required(article.Id, "id");
            var sourceSystem = Required(article.SourceSystem, "source_system");
            var sourceId = Required(article.SourceId, "source_id");
            var title = Required(article.Title, "title");
            var summary = Required(article.Summary, "summary");
            var updatedAt = RequiredTimestamp(article.UpdatedAt, "updated_at");
            if (file.Error is not null)
            {
                Error(file.Error);
            }

            if (article.FeatureRefs is null || article.FeatureRefs.Count == 0)
            {
                Error("feature_refs must list at least one feature.");
            }

            if (article.BugRefs is null)
            {
                Error("bug_refs must be an array (use [] when the article addresses no bug).");
            }

            if (id is null || sourceSystem is null || sourceId is null || title is null || summary is null || updatedAt is null || file.Content is null)
            {
                continue;
            }

            var articleKey = SourceNormalization.ArticleKey(sourceId);
            if (articleKey != sourceId || !SourceNormalization.IsCanonicalArticleKey(articleKey))
            {
                Error($"source_id '{sourceId}' must have the canonical form KB-<number>.");
                continue;
            }

            if (id != SourceNormalization.ArticleId(articleKey))
            {
                Error($"id '{id}' does not match the canonical id '{SourceNormalization.ArticleId(articleKey)}'.");
                continue;
            }

            if (!articleIds.Add(id))
            {
                Error($"duplicate article id '{id}'.");
                continue;
            }

            if (!files.Add(file.DisplayPath!))
            {
                Error($"file '{article.File}' is already used by another article.");
                continue;
            }

            var contentProblem = FindContentProblem(file.Content, title);
            if (contentProblem is not null)
            {
                Error(contentProblem);
                continue;
            }

            var featureIds = ResolveReferences(article.FeatureRefs ?? [], "feature_ref", SourceNormalization.FeatureKey, SourceNormalization.IsCanonicalFeatureKey, SourceNormalization.FeatureId, Error);
            var bugIds = ResolveReferences(article.BugRefs ?? [], "bug_ref", SourceNormalization.BugKey, SourceNormalization.IsCanonicalBugKey, SourceNormalization.BugId, Error);
            if (featureIds is null || bugIds is null)
            {
                continue;
            }

            articles.Add(new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["id"] = id,
                ["title"] = title,
                ["summary"] = summary,
                ["content"] = file.Content,
                ["sourceSystem"] = sourceSystem,
                ["sourceId"] = sourceId,
                ["sourceRecord"] = location,
                ["sourcePath"] = file.DisplayPath,
                ["updatedAt"] = updatedAt,
                ["ingestedAt"] = ingestedAtValue,
                ["ingestedBy"] = KnowledgeOwner,
                ["synthetic"] = source.Synthetic,
            });

            foreach (var (featureId, rawReference) in featureIds)
            {
                covers.Add(new GraphRelationship(id, featureId, RelationshipProperties(sourceSystem, location, ingestedAtValue, "sourceFeatureRef", rawReference)));
            }

            foreach (var (bugId, rawReference) in bugIds)
            {
                addresses.Add(new GraphRelationship(id, bugId, RelationshipProperties(sourceSystem, location, ingestedAtValue, "sourceBugRef", rawReference)));
            }

            string? Required(string? value, string fieldName)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                Error($"required field '{fieldName}' is missing or empty.");
                return null;
            }

            ZonedDateTime? RequiredTimestamp(string? value, string fieldName)
            {
                if (Required(value, fieldName) is null)
                {
                    return null;
                }

                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    return new ZonedDateTime(parsed);
                }

                Error($"'{fieldName}' value '{value}' is not an ISO 8601 timestamp.");
                return null;
            }
        }

        foreach (var unlistedFile in source.UnlistedMarkdownFiles)
        {
            issues.Add(new IngestionIssue(IngestionIssueSeverity.Warning, unlistedFile, "Markdown file is not listed in the manifest and was not imported."));
        }

        return new KnowledgeImportPlan(
            source.Articles.Count,
            articles,
            addresses,
            covers,
            addresses.Select(relationship => relationship.EndId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            covers.Select(relationship => relationship.EndId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            issues);
    }

    public static string? FindContentProblem(string content, string title)
    {
        var lines = content.Split('\n');
        var headingIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
        if (headingIndex < 0)
        {
            return "Markdown file is empty.";
        }

        var expectedHeading = $"# {title.Trim()}";
        if (lines[headingIndex].TrimEnd() != expectedHeading)
        {
            return $"Markdown starts with '{lines[headingIndex].TrimEnd()}' but the manifest title requires '{expectedHeading}'.";
        }

        var bodyCharacters = lines.Skip(headingIndex + 1).Sum(line => line.Count(character => !char.IsWhiteSpace(character)));
        return bodyCharacters < MinimumBodyCharacters
            ? $"Markdown body has {bodyCharacters} non-whitespace characters after the title; at least {MinimumBodyCharacters} are required."
            : null;
    }

    private static List<(string Id, string RawReference)>? ResolveReferences(
        IReadOnlyList<string> rawReferences,
        string fieldName,
        Func<string, string> normalizeKey,
        Func<string, bool> isCanonicalKey,
        Func<string, string> canonicalId,
        Action<string> error)
    {
        var resolved = new List<(string Id, string RawReference)>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var valid = true;
        foreach (var rawReference in rawReferences)
        {
            var key = normalizeKey(rawReference ?? string.Empty);
            if (!isCanonicalKey(key))
            {
                error($"{fieldName} '{rawReference}' is not a valid reference.");
                valid = false;
                continue;
            }

            if (!seenKeys.Add(key))
            {
                error($"{fieldName} '{rawReference}' is listed more than once for this article.");
                valid = false;
                continue;
            }

            resolved.Add((canonicalId(key), rawReference!));
        }

        return valid ? resolved : null;
    }

    private static Dictionary<string, object?> RelationshipProperties(string sourceSystem, string location, ZonedDateTime ingestedAt, string rawReferenceKey, string rawReference) =>
        new(StringComparer.Ordinal)
        {
            ["sourceSystem"] = sourceSystem,
            ["sourceRecord"] = location,
            ["ingestedAt"] = ingestedAt,
            ["ingestedBy"] = KnowledgeOwner,
            [rawReferenceKey] = rawReference,
        };
}
