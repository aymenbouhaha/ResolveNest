namespace ResolveNest.Api.Features.Investigations;

public static class TicketInvestigationMapper
{
    public static TicketInvestigationResponse Combine(
        GraphResultMap evidenceRow,
        GraphResultMap candidatesRow,
        GraphResultMap knowledgeRow,
        bool includeContent)
    {
        var ticket = MapTicket(evidenceRow.OptionalMap("ticket") ?? throw new InvestigationResultException("Investigation result has a found ticket without ticket data."));
        var features = evidenceRow.List("features", MapFeature);
        var confirmedBugs = evidenceRow.List("confirmedBugs", MapConfirmedBug);
        var relevanceContext = CandidateRelevanceEvaluator.CreateContext(ticket, confirmedBugs);
        var candidateBugs = CandidateRelevanceEvaluator.Order(candidatesRow.List("candidateBugs", map => MapCandidateBug(map, relevanceContext)));
        var relatedTickets = candidatesRow.List("relatedTickets", MapRelatedTicket);
        var relatedTicketTotal = candidatesRow.RequiredLong("relatedTicketCount");
        var knowledgeArticles = knowledgeRow.List("articles", MapKnowledgeArticle);

        var counts = new InvestigationResultCounts(
            features.Count,
            confirmedBugs.Count,
            candidateBugs.Count,
            InvestigationQueryLimits.CandidateBugs,
            candidateBugs.Count >= InvestigationQueryLimits.CandidateBugs,
            relatedTickets.Count,
            relatedTicketTotal,
            InvestigationQueryLimits.RelatedTickets,
            relatedTicketTotal > relatedTickets.Count,
            knowledgeArticles.Count,
            includeContent);

        return new TicketInvestigationResponse(
            ticket,
            evidenceRow.OptionalMap("customer") is { } customer ? MapCustomer(customer) : null,
            features,
            confirmedBugs,
            candidateBugs,
            relatedTickets,
            knowledgeArticles,
            counts);
    }

    private static InvestigatedTicket MapTicket(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("subject"),
        map.OptionalString("description"),
        map.OptionalString("status"),
        map.OptionalString("priority"),
        map.OptionalString("reportedVersion"),
        map.OptionalString("reportedVersionNormalized"),
        map.OptionalString("reportedVersionPrecision"),
        map.OptionalString("resolution"),
        map.OptionalDateTime("createdAt"),
        map.OptionalDateTime("resolvedAt"),
        map.Strings("unresolvedBugRefs"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceId"),
        map.OptionalString("sourceRecord"));

    private static ReportingCustomer MapCustomer(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("name"),
        map.OptionalString("plan"),
        map.OptionalString("deployment"),
        map.RequiredEvidence("evidence"),
        map.RequiredString("path"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceId"),
        map.OptionalString("sourceRecord"),
        map.OptionalString("relationshipSourceRecord"));

    private static TicketFeature MapFeature(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("name"),
        map.RequiredEvidence("evidence"),
        map.RequiredString("path"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceRecord"),
        map.OptionalString("relationshipSourceRecord"));

    private static ReleaseEvidence MapRelease(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.OptionalString("version"),
        map.OptionalDateTime("releasedAt"),
        map.RequiredEvidence("evidence"),
        map.OptionalString("evidenceText"),
        map.OptionalString("relationshipSourceRecord"));

    private static AffectedFeature MapAffectedFeature(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.OptionalString("name"),
        map.RequiredEvidence("evidence"),
        map.OptionalString("relationshipSourceRecord"));

    private static ConfirmedBug MapConfirmedBug(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("title"),
        map.OptionalString("sourceStatus"),
        map.OptionalString("severity"),
        map.OptionalString("workaround"),
        map.RequiredEvidence("evidence"),
        map.RequiredString("path"),
        map.OptionalString("confirmedBy"),
        map.OptionalDateTime("confirmedAt"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceId"),
        map.OptionalString("sourceRecord"),
        map.OptionalString("relationshipSourceRecord"),
        map.List("affectedFeatures", MapAffectedFeature),
        map.List("introducedIn", MapRelease),
        map.List("fixedIn", MapRelease));

    private static SharedFeature MapSharedFeature(GraphResultMap map) => new(
        map.RequiredString("featureId"),
        map.OptionalString("featureName"),
        map.OptionalString("relationshipSourceRecord"));

    private static CandidateConfirmedTicket MapCandidateConfirmedTicket(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("subject"),
        map.OptionalString("status"),
        map.OptionalString("reportedVersion"),
        map.OptionalString("reportedVersionPrecision"),
        map.OptionalString("customerId"),
        map.RequiredBool("sameCustomer"),
        map.RequiredEvidence("evidence"),
        map.RequiredString("path"),
        map.OptionalString("confirmedBy"),
        map.OptionalDateTime("confirmedAt"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceRecord"),
        map.OptionalString("relationshipSourceRecord"));

    private static CandidateBug MapCandidateBug(GraphResultMap map, TicketRelevanceContext relevanceContext)
    {
        var title = map.OptionalString("title");
        var description = map.OptionalString("description");
        var sharedFeatures = map.List("sharedFeatures", MapSharedFeature);
        var introducedIn = map.List("introducedIn", MapRelease);
        var fixedIn = map.List("fixedIn", MapRelease);
        var confirmedTicketCount = map.RequiredLong("confirmedTicketCount");
        var confirmedCustomerCount = map.RequiredLong("confirmedCustomerCount");
        var confirmedTickets = map.List("confirmedTickets", MapCandidateConfirmedTicket);
        var relevance = CandidateRelevanceEvaluator.Evaluate(
            relevanceContext, title, description, sharedFeatures, introducedIn, fixedIn, confirmedTicketCount, confirmedCustomerCount);

        return new CandidateBug(
            map.RequiredString("id"),
            map.RequiredString("type"),
            title,
            description,
            map.OptionalString("sourceStatus"),
            map.OptionalString("severity"),
            map.OptionalString("workaround"),
            map.RequiredEvidence("evidence"),
            map.RequiredString("path"),
            map.RequiredString("reason"),
            sharedFeatures,
            map.OptionalString("sourceSystem"),
            map.OptionalString("sourceId"),
            map.OptionalString("sourceRecord"),
            introducedIn,
            fixedIn,
            confirmedTicketCount,
            confirmedCustomerCount,
            confirmedTickets,
            InvestigationQueryLimits.ConfirmedTicketsPerCandidate,
            confirmedTicketCount > confirmedTickets.Count,
            relevance);
    }

    private static RelatedTicket MapRelatedTicket(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("subject"),
        map.OptionalString("status"),
        map.OptionalDateTime("createdAt"),
        map.OptionalString("reportedVersion"),
        map.OptionalString("reportedVersionPrecision"),
        map.OptionalString("customerId"),
        map.RequiredBool("sameCustomer"),
        map.RequiredEvidence("evidence"),
        map.RequiredString("path"),
        map.RequiredString("reason"),
        map.Strings("sharedFeatureIds"),
        map.Strings("confirmedBugIds"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceRecord"));

    private static KnowledgeRoute MapKnowledgeRoute(GraphResultMap map) => new(
        map.RequiredString("relationship"),
        map.RequiredString("targetId"),
        map.OptionalString("basis"),
        map.RequiredEvidence("evidence"),
        map.OptionalString("relationshipSourceRecord"));

    private static KnowledgeArticleEvidence MapKnowledgeArticle(GraphResultMap map) => new(
        map.RequiredString("id"),
        map.RequiredString("type"),
        map.OptionalString("title"),
        map.OptionalString("summary"),
        map.OptionalDateTime("updatedAt"),
        map.OptionalString("sourceSystem"),
        map.OptionalString("sourceId"),
        map.OptionalString("sourceRecord"),
        map.OptionalString("sourcePath"),
        map.List("reachedVia", MapKnowledgeRoute),
        map.OptionalString("content"));
}
