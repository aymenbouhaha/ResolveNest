OPTIONAL MATCH (ticket:SupportTicket {id: $ticketId})
OPTIONAL MATCH (ticketCustomer:Customer)-[:REPORTED]->(ticket)
RETURN
  $ticketId AS requestedTicketId,
  ticket IS NOT NULL AS found,
  COLLECT {
    MATCH (ticket)-[:ABOUT_FEATURE]->(feature:Feature)<-[affects:AFFECTS]-(bug:Bug)
    WHERE NOT (ticket)-[:CONFIRMED_AS]->(bug)
    WITH bug, collect(DISTINCT {featureId: feature.id, featureName: feature.name, relationshipSourceRecord: affects.sourceRecord}) AS sharedFeatures
    WITH bug, sharedFeatures,
         COUNT { MATCH (other:SupportTicket)-[:CONFIRMED_AS]->(bug) WHERE other <> ticket } AS confirmedTicketCount,
         COUNT { MATCH (otherCustomer:Customer)-[:REPORTED]->(other:SupportTicket)-[:CONFIRMED_AS]->(bug) WHERE other <> ticket RETURN DISTINCT otherCustomer } AS confirmedCustomerCount
    ORDER BY confirmedTicketCount DESC, bug.id
    LIMIT 25
    RETURN {
      id: bug.id,
      type: 'Bug',
      title: bug.title,
      sourceStatus: bug.status,
      severity: bug.severity,
      workaround: bug.workaround,
      evidence: 'candidate',
      path: 'SupportTicket-[:ABOUT_FEATURE]->Feature<-[:AFFECTS]-Bug',
      reason: 'The bug affects a feature this ticket concerns. A shared feature does not establish the same root cause.',
      sharedFeatures: sharedFeatures,
      sourceSystem: bug.sourceSystem,
      sourceId: bug.sourceId,
      sourceRecord: bug.sourceRecord,
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
      },
      confirmedTicketCount: confirmedTicketCount,
      confirmedCustomerCount: confirmedCustomerCount,
      confirmedTickets: COLLECT {
        MATCH (otherCustomer:Customer)-[:REPORTED]->(other:SupportTicket)-[confirmed:CONFIRMED_AS]->(bug)
        WHERE other <> ticket
        WITH other, otherCustomer, confirmed
        ORDER BY other.id
        LIMIT 25
        RETURN {
          id: other.id,
          type: 'SupportTicket',
          subject: other.subject,
          status: other.status,
          reportedVersion: other.reportedVersion,
          reportedVersionPrecision: other.reportedVersionPrecision,
          customerId: otherCustomer.id,
          sameCustomer: otherCustomer = ticketCustomer,
          evidence: 'confirmed',
          path: 'SupportTicket-[:CONFIRMED_AS]->Bug',
          confirmedBy: confirmed.confirmedBy,
          confirmedAt: confirmed.confirmedAt,
          sourceSystem: other.sourceSystem,
          sourceRecord: other.sourceRecord,
          relationshipSourceRecord: confirmed.sourceRecord
        }
      }
    }
  } AS candidateBugs,
  COUNT {
    MATCH (ticket)-[:ABOUT_FEATURE]->(:Feature)<-[:ABOUT_FEATURE]-(other:SupportTicket)
    WHERE other <> ticket
    RETURN DISTINCT other
  } AS relatedTicketCount,
  COLLECT {
    MATCH (ticket)-[:ABOUT_FEATURE]->(feature:Feature)<-[:ABOUT_FEATURE]-(other:SupportTicket)
    WHERE other <> ticket
    WITH other, collect(DISTINCT feature.id) AS sharedFeatureIds
    OPTIONAL MATCH (otherCustomer:Customer)-[:REPORTED]->(other)
    WITH other, sharedFeatureIds, otherCustomer
    ORDER BY other.createdAt DESC, other.id
    LIMIT 50
    RETURN {
      id: other.id,
      type: 'SupportTicket',
      subject: other.subject,
      status: other.status,
      createdAt: other.createdAt,
      reportedVersion: other.reportedVersion,
      reportedVersionPrecision: other.reportedVersionPrecision,
      customerId: otherCustomer.id,
      sameCustomer: otherCustomer = ticketCustomer,
      evidence: 'context',
      path: 'SupportTicket-[:ABOUT_FEATURE]->Feature<-[:ABOUT_FEATURE]-SupportTicket',
      reason: 'The ticket concerns a shared feature. This is context, not evidence of the same cause.',
      sharedFeatureIds: sharedFeatureIds,
      confirmedBugIds: COLLECT { MATCH (other)-[:CONFIRMED_AS]->(bug:Bug) WITH bug ORDER BY bug.id RETURN bug.id },
      sourceSystem: other.sourceSystem,
      sourceRecord: other.sourceRecord
    }
  } AS relatedTickets
