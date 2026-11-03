OPTIONAL MATCH (ticket:SupportTicket {id: $ticketId})
OPTIONAL MATCH (customer:Customer)-[reported:REPORTED]->(ticket)
RETURN
  $ticketId AS requestedTicketId,
  ticket IS NOT NULL AS found,
  CASE WHEN ticket IS NULL THEN null ELSE {
    id: ticket.id,
    type: 'SupportTicket',
    subject: ticket.subject,
    description: ticket.description,
    status: ticket.status,
    priority: ticket.priority,
    reportedVersion: ticket.reportedVersion,
    reportedVersionNormalized: ticket.reportedVersionNormalized,
    reportedVersionPrecision: ticket.reportedVersionPrecision,
    resolution: ticket.resolution,
    createdAt: ticket.createdAt,
    resolvedAt: ticket.resolvedAt,
    unresolvedBugRefs: ticket.unresolvedBugRefs,
    sourceSystem: ticket.sourceSystem,
    sourceId: ticket.sourceId,
    sourceRecord: ticket.sourceRecord
  } END AS ticket,
  CASE WHEN customer IS NULL THEN null ELSE {
    id: customer.id,
    type: 'Customer',
    name: customer.name,
    plan: customer.plan,
    deployment: customer.deployment,
    evidence: 'documented',
    path: 'Customer-[:REPORTED]->SupportTicket',
    sourceSystem: customer.sourceSystem,
    sourceId: customer.sourceId,
    sourceRecord: customer.sourceRecord,
    relationshipSourceRecord: reported.sourceRecord
  } END AS customer,
  COLLECT {
    MATCH (ticket)-[about:ABOUT_FEATURE]->(feature:Feature)
    WITH feature, about
    ORDER BY feature.id
    RETURN {
      id: feature.id,
      type: 'Feature',
      name: feature.name,
      evidence: 'documented',
      path: 'SupportTicket-[:ABOUT_FEATURE]->Feature',
      sourceSystem: feature.sourceSystem,
      sourceRecord: feature.sourceRecord,
      relationshipSourceRecord: about.sourceRecord
    }
  } AS features,
  COLLECT {
    MATCH (ticket)-[confirmed:CONFIRMED_AS]->(bug:Bug)
    WITH bug, confirmed
    ORDER BY bug.id
    RETURN {
      id: bug.id,
      type: 'Bug',
      title: bug.title,
      sourceStatus: bug.status,
      severity: bug.severity,
      workaround: bug.workaround,
      evidence: 'confirmed',
      path: 'SupportTicket-[:CONFIRMED_AS]->Bug',
      confirmedBy: confirmed.confirmedBy,
      confirmedAt: confirmed.confirmedAt,
      sourceSystem: bug.sourceSystem,
      sourceId: bug.sourceId,
      sourceRecord: bug.sourceRecord,
      relationshipSourceRecord: confirmed.sourceRecord,
      affectedFeatures: COLLECT {
        MATCH (bug)-[affects:AFFECTS]->(affected:Feature)
        WITH affected, affects
        ORDER BY affected.id
        RETURN {id: affected.id, name: affected.name, evidence: 'documented', relationshipSourceRecord: affects.sourceRecord}
      },
      introducedIn: COLLECT {
        MATCH (bug)-[introduced:INTRODUCED_IN]->(release:Release)
        WITH release, introduced
        ORDER BY release.id
        RETURN {id: release.id, version: release.version, releasedAt: release.releasedAt, evidence: 'documented', evidenceText: introduced.evidence, relationshipSourceRecord: introduced.sourceRecord}
      },
      fixedIn: COLLECT {
        MATCH (bug)-[fixed:FIXED_IN]->(release:Release)
        WITH release, fixed
        ORDER BY release.id
        RETURN {id: release.id, version: release.version, releasedAt: release.releasedAt, evidence: 'documented', evidenceText: fixed.evidence, relationshipSourceRecord: fixed.sourceRecord}
      }
    }
  } AS confirmedBugs
