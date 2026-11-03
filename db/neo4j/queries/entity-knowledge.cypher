RETURN
  $bugIds AS requestedBugIds,
  $featureIds AS requestedFeatureIds,
  COLLECT {
    MATCH (bug:Bug) WHERE bug.id IN $bugIds
    WITH bug ORDER BY bug.id
    RETURN bug.id
  } AS foundBugIds,
  COLLECT {
    MATCH (feature:Feature) WHERE feature.id IN $featureIds
    WITH feature ORDER BY feature.id
    RETURN feature.id
  } AS foundFeatureIds,
  COLLECT {
    CALL () {
      MATCH (bug:Bug)<-[link:ADDRESSES]-(article:KnowledgeArticle)
      WHERE bug.id IN $bugIds
      RETURN article, {relationship: 'ADDRESSES', targetId: bug.id, evidence: 'documented', relationshipSourceRecord: link.sourceRecord} AS route
      UNION
      MATCH (feature:Feature)<-[link:COVERS]-(article:KnowledgeArticle)
      WHERE feature.id IN $featureIds
      RETURN article, {relationship: 'COVERS', targetId: feature.id, evidence: 'documented', relationshipSourceRecord: link.sourceRecord} AS route
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
