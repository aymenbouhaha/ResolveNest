namespace ResolveNest.Api.Features.Ingestion;

public static class SeedGraphQueries
{
    public const string SeedOwner = "seed-ingestion";

    public const string FindSeedNodeOwnershipConflicts = """
        MATCH (node)
        WHERE any(label IN labels(node) WHERE label IN $seedLabels)
        WITH node, head([label IN labels(node) WHERE label IN $seedLabels]) AS seedLabel
        WITH node, $plannedNodeIdentities[seedLabel + '|' + coalesce(node.id, '')] AS plannedIdentity,
             node.sourceSystem + '|' + node.sourceId AS existingIdentity
        WITH node, plannedIdentity, existingIdentity,
             coalesce(plannedIdentity = existingIdentity, false) AS identityMatchesPlan,
             coalesce(node.ingestedBy, $seedOwner) = $seedOwner AS ownedBySeedOrUnmarked,
             coalesce(node.ingestedBy = $seedOwner, false) AS markedAsSeed
        WHERE NOT (
            (plannedIdentity IS NOT NULL AND identityMatchesPlan AND ownedBySeedOrUnmarked)
            OR (plannedIdentity IS NULL AND markedAsSeed)
        )
        RETURN labels(node) AS labels, node.id AS id, node.ingestedBy AS ingestedBy, existingIdentity, plannedIdentity
        LIMIT 20
        """;

    public const string FindSeedRelationshipOwnershipConflicts = """
        MATCH (start)-[relationship]->(end)
        WHERE type(relationship) IN $seedRelationshipTypes
        WITH start, end, relationship,
             $plannedRelationshipSources[type(relationship) + '|' + coalesce(start.id, '') + '|' + coalesce(end.id, '')] AS plannedSourceSystem
        WITH start, end, relationship, plannedSourceSystem,
             coalesce(plannedSourceSystem = relationship.sourceSystem, false) AS sourceMatchesPlan,
             coalesce(relationship.ingestedBy, $seedOwner) = $seedOwner AS ownedBySeedOrUnmarked,
             coalesce(relationship.ingestedBy = $seedOwner, false) AS markedAsSeed
        WHERE NOT (
            (plannedSourceSystem IS NOT NULL AND sourceMatchesPlan AND ownedBySeedOrUnmarked)
            OR (plannedSourceSystem IS NULL AND markedAsSeed)
        )
        RETURN type(relationship) AS type, start.id AS startId, end.id AS endId, relationship.ingestedBy AS ingestedBy
        LIMIT 20
        """;

    public const string CountExistingNodes = """
        MATCH (node)
        RETURN count(node) AS total,
               count(CASE WHEN any(label IN labels(node) WHERE label IN $seedLabels) THEN 1 END) AS seedLabeled
        """;

    public const string MergeCustomers = """
        UNWIND $rows AS row
        MERGE (node:Customer {id: row.id})
        SET node += row
        """;

    public const string MergeFeatures = """
        UNWIND $rows AS row
        MERGE (node:Feature {id: row.id})
        SET node += row
        """;

    public const string MergeReleases = """
        UNWIND $rows AS row
        MERGE (node:Release {id: row.id})
        SET node += row
        """;

    public const string MergeBugs = """
        UNWIND $rows AS row
        MERGE (node:Bug {id: row.id})
        SET node += row
        """;

    public const string MergeTickets = """
        UNWIND $rows AS row
        MERGE (node:SupportTicket {id: row.id})
        SET node += row
        """;

    public const string MergeReported = """
        UNWIND $rows AS row
        MATCH (customer:Customer {id: row.startId})
        MATCH (ticket:SupportTicket {id: row.endId})
        MERGE (customer)-[relationship:REPORTED]->(ticket)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeAboutFeature = """
        UNWIND $rows AS row
        MATCH (ticket:SupportTicket {id: row.startId})
        MATCH (feature:Feature {id: row.endId})
        MERGE (ticket)-[relationship:ABOUT_FEATURE]->(feature)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeConfirmedAs = """
        UNWIND $rows AS row
        MATCH (ticket:SupportTicket {id: row.startId})
        MATCH (bug:Bug {id: row.endId})
        MERGE (ticket)-[relationship:CONFIRMED_AS]->(bug)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeAffects = """
        UNWIND $rows AS row
        MATCH (bug:Bug {id: row.startId})
        MATCH (feature:Feature {id: row.endId})
        MERGE (bug)-[relationship:AFFECTS]->(feature)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeIntroducedIn = """
        UNWIND $rows AS row
        MATCH (bug:Bug {id: row.startId})
        MATCH (release:Release {id: row.endId})
        MERGE (bug)-[relationship:INTRODUCED_IN]->(release)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;

    public const string MergeFixedIn = """
        UNWIND $rows AS row
        MATCH (bug:Bug {id: row.startId})
        MATCH (release:Release {id: row.endId})
        MERGE (bug)-[relationship:FIXED_IN]->(release)
        SET relationship += row.properties
        RETURN count(relationship) AS written
        """;
}
