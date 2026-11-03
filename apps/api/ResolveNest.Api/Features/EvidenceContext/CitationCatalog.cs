using System.Text.Json.Serialization;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.EvidenceContext;

[JsonConverter(typeof(JsonStringEnumConverter<CitationKind>))]
public enum CitationKind
{
    [JsonStringEnumMemberName("record")]
    Record,

    [JsonStringEnumMemberName("relationship")]
    Relationship,
}

public sealed record CitedRelationship(
    string Type,
    string FromId,
    string ToId,
    EvidenceClassification Evidence,
    string Path);

public sealed record CitationSource(
    string CitationId,
    CitationKind Kind,
    string EntityId,
    string EntityType,
    string? SourceSystem,
    string? SourceId,
    string? SourceRecord,
    string? SourcePath,
    CitedRelationship? Relationship);

public sealed class CitationCatalog
{
    private const string RecordPrefix = "rec:";
    private const string RelationshipPrefix = "rel:";
    private const string RelationshipSeparator = ">";

    private readonly Dictionary<string, CitationSource> sourcesById = new(StringComparer.Ordinal);

    public static string RecordCitationId(string entityId) => RecordPrefix + entityId;

    public static string RelationshipCitationId(string fromId, string relationshipType, string toId) =>
        RelationshipPrefix + fromId + RelationshipSeparator + relationshipType + RelationshipSeparator + toId;

    public static string DefaultPath(string fromType, string relationshipType, string? toType) => $"{fromType}-[:{relationshipType}]->{toType ?? "()"}";

    public bool Contains(string citationId) => sourcesById.ContainsKey(citationId);

    public string AddRecord(
        string entityId,
        string entityType,
        string? sourceSystem,
        string? sourceId,
        string? sourceRecord,
        string? sourcePath = null)
    {
        var citationId = RecordCitationId(entityId);
        sourcesById.TryAdd(citationId, new CitationSource(citationId, CitationKind.Record, entityId, entityType, sourceSystem, sourceId, sourceRecord, sourcePath, null));
        return citationId;
    }

    public string AddRelationship(
        string fromId,
        string fromType,
        string relationshipType,
        string toId,
        EvidenceClassification evidence,
        string path,
        string? relationshipSourceRecord)
    {
        var citationId = RelationshipCitationId(fromId, relationshipType, toId);
        var relationship = new CitedRelationship(relationshipType, fromId, toId, evidence, path);
        sourcesById.TryAdd(citationId, new CitationSource(citationId, CitationKind.Relationship, fromId, fromType, null, null, relationshipSourceRecord, null, relationship));
        return citationId;
    }

    public IReadOnlyList<CitationSource> ToOrderedList() => sourcesById.Values
        .OrderBy(source => source.CitationId, StringComparer.Ordinal)
        .ToList();
}
