namespace ResolveNest.Api.Features.Ingestion;

public static class KnowledgeGraphQueries
{
    public const string CountExistingNodes = """
        MATCH (node)
        RETURN count(node) AS total,
               count(CASE WHEN node:KnowledgeArticle THEN 1 END) AS articles
        """;

    public const string FindArticleOwnershipConflicts = """
        MATCH (article:KnowledgeArticle)
        WITH article, $plannedArticleIdentities[coalesce(article.id, '')] AS plannedIdentity,
             article.sourceSystem + '|' + article.sourceId AS existingIdentity
        WITH article, plannedIdentity, existingIdentity,
             coalesce(plannedIdentity = existingIdentity, false) AS identityMatchesPlan,
             coalesce(article.ingestedBy = $knowledgeOwner, false) AS markedAsKnowledge
        WHERE NOT ((plannedIdentity IS NULL OR identityMatchesPlan) AND markedAsKnowledge)
        RETURN labels(article) AS labels, article.id AS id, article.ingestedBy AS ingestedBy, existingIdentity, plannedIdentity
        LIMIT 20
        """;

    public const string FindKnowledgeRelationshipOwnershipConflicts = """
        MATCH (start)-[relationship]->(end)
        WHERE type(relationship) IN $knowledgeRelationshipTypes
        WITH start, end, relationship,
             $plannedRelationshipSources[type(relationship) + '|' + coalesce(start.id, '') + '|' + coalesce(end.id, '')] AS plannedSourceSystem
        WITH start, end, relationship, plannedSourceSystem,
             coalesce(plannedSourceSystem = relationship.sourceSystem, false) AS sourceMatchesPlan,
             coalesce(relationship.ingestedBy = $knowledgeOwner, false) AS markedAsKnowledge
        WHERE NOT ((plannedSourceSystem IS NULL OR sourceMatchesPlan) AND markedAsKnowledge)
        RETURN type(relationship) AS type, start.id AS startId, end.id AS endId, relationship.ingestedBy AS ingestedBy
        LIMIT 20
        """;

    public const string FindMissingBugs = """
        UNWIND $ids AS id
        OPTIONAL MATCH (bug:Bug {id: id})
        WITH id, bug
        WHERE bug IS NULL
        RETURN id
        ORDER BY id
        """;

    public const string FindMissingFeatures = """
        UNWIND $ids AS id
        OPTIONAL MATCH (feature:Feature {id: id})
        WITH id, feature
        WHERE feature IS NULL
        RETURN id
        ORDER BY id
        """;

    public const string MergeArticles = """
        UNWIND $rows AS row
        MERGE (article:KnowledgeArticle {id: row.id})
        SET article += row
        """;

    public const string MergeAddresses = """
        UNWIND $rows AS row
        MATCH (article:KnowledgeArticle {id: row.startId})
        MATCH (bug:Bug {id: row.endId})
        MERGE (article)-[relationship:ADDRESSES]->(bug)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeCovers = """
        UNWIND $rows AS row
        MATCH (article:KnowledgeArticle {id: row.startId})
        MATCH (feature:Feature {id: row.endId})
        MERGE (article)-[relationship:COVERS]->(feature)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;
}
