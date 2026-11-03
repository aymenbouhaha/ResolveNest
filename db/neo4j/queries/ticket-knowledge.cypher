OPTIONAL MATCH (ticket:SupportTicket {id: $ticketId})
RETURN
  $ticketId AS requestedTicketId,
  ticket IS NOT NULL AS found,
  COLLECT {
    CALL (ticket) {
      MATCH (ticket)-[:ABOUT_FEATURE]->(feature:Feature)<-[link:COVERS]-(article:KnowledgeArticle)
      RETURN article, {relationship: 'COVERS', targetId: feature.id, basis: 'ticket feature', evidence: 'documented', relationshipSourceRecord: link.sourceRecord} AS route
      UNION
      MATCH (ticket)-[:CONFIRMED_AS]->(bug:Bug)<-[link:ADDRESSES]-(article:KnowledgeArticle)
      RETURN article, {relationship: 'ADDRESSES', targetId: bug.id, basis: 'confirmed bug of the ticket', evidence: 'confirmed', relationshipSourceRecord: link.sourceRecord} AS route
      UNION
      MATCH (ticket)-[:ABOUT_FEATURE]->(:Feature)<-[:AFFECTS]-(bug:Bug)<-[link:ADDRESSES]-(article:KnowledgeArticle)
      WHERE NOT (ticket)-[:CONFIRMED_AS]->(bug)
      RETURN DISTINCT article, {relationship: 'ADDRESSES', targetId: bug.id, basis: 'candidate bug affecting a ticket feature', evidence: 'candidate', relationshipSourceRecord: link.sourceRecord} AS route
    }
    WITH article, route
    ORDER BY route.relationship, route.targetId
    WITH article, collect(DISTINCT route) AS routes
    ORDER BY article.id
    RETURN {
      id: article.id,
      type: 'KnowledgeArticle',
      title: article.title,
      summary: article.summary,
      updatedAt: article.updatedAt,
      sourceSystem: article.sourceSystem,
      sourceId: article.sourceId,
      sourceRecord: article.sourceRecord,
      sourcePath: article.sourcePath,
      reachedVia: routes,
      content: CASE WHEN $includeContent THEN article.content ELSE null END
    }
  } AS articles
