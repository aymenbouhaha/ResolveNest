using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.EvidenceContext;

public static class ContextLimitationRules
{
    private const string PatchPrecision = "patch";
    private const string MinorPrecision = "minor";
    private const string AddressesRelationship = "ADDRESSES";
    private static readonly HashSet<string> ClosedStatuses = new(StringComparer.Ordinal) { "resolved", "closed" };

    public static IReadOnlyList<ContextLimitation> Describe(
        TicketContext ticket,
        IReadOnlyList<ConfirmedBugContext> confirmedBugs,
        IReadOnlyList<CandidateBugContext> candidateBugs,
        IReadOnlyList<KnowledgeArticleContext> knowledgeArticles,
        bool knowledgeContentIncluded)
    {
        var limitations = new List<ContextLimitation>();
        var isClosed = ticket.Status is { } status && ClosedStatuses.Contains(status);

        if (confirmedBugs.Count == 0)
        {
            limitations.Add(new("noConfirmedBug", $"No bug is confirmed against {ticket.Id}. Candidate bugs are reached only through a shared feature and are not confirmed causes.", [ticket.Id]));
        }

        DescribeReportedVersion(ticket, limitations);
        DescribeDocumentedFixes(ticket, isClosed, confirmedBugs, candidateBugs, limitations);

        if (confirmedBugs.Count == 0 && isClosed)
        {
            limitations.Add(new("closedWithoutConfirmedBug", $"Ticket {ticket.Id} is '{ticket.Status}' with no confirmed bug. Its recorded resolution is source text and may describe a cause other than a product defect; candidate bugs are not confirmed defects.", [ticket.Id]));
        }

        var conflicting = candidateBugs.Where(bug => bug.Relevance.Tier == CandidateRelevanceTier.ErrorCodeMatchChronologyConflict).Select(bug => bug.Id).ToList();
        if (conflicting.Count > 0)
        {
            limitations.Add(new("conflictingCandidateSignals", $"These candidates share an error code with the ticket, but the reported version is before their documented introduction release: {string.Join(", ", conflicting)}. Both signals are kept and neither is resolved here.", conflicting));
        }

        if (confirmedBugs.Count == 0 && candidateBugs.All(bug => bug.Relevance.Tier != CandidateRelevanceTier.ErrorCodeMatch))
        {
            limitations.Add(new("noUncontradictedErrorCodeMatch", "No candidate bug shares an error code with the ticket without a release-chronology conflict. The available evidence does not single out a likely defect.", [ticket.Id]));
        }

        var uncorroborated = candidateBugs
            .Where(bug => bug.History.ConfirmedTicketCount == 0 && bug.IntroducedIn.Count == 0 && bug.FixedIn.Count == 0)
            .Select(bug => bug.Id)
            .ToList();
        if (uncorroborated.Count > 0)
        {
            limitations.Add(new("uncorroboratedCandidates", $"These candidates have no confirmed tickets and no documented release, so only a shared feature links them to this ticket: {string.Join(", ", uncorroborated)}.", uncorroborated));
        }

        if (candidateBugs.Any(bug => bug.History.ConfirmedTicketCount > 0))
        {
            limitations.Add(new("historicalConfirmationsAreOtherTickets", $"Confirmed tickets listed under candidate bugs are confirmed for those other tickets, not for {ticket.Id}.", [ticket.Id]));
        }

        DescribeKnowledge(knowledgeArticles, knowledgeContentIncluded, limitations);

        if (ticket.UnresolvedBugRefs.Count > 0)
        {
            limitations.Add(new("unresolvedBugReferences", $"The ticket source references bugs that are not in the graph: {string.Join(", ", ticket.UnresolvedBugRefs)}.", ticket.UnresolvedBugRefs));
        }

        if (candidateBugs.Count > 0)
        {
            limitations.Add(new("relevanceIsHeuristic", "Candidate relevance tiers and version assessments are deterministic heuristics over the recorded evidence, not diagnoses.", []));
        }

        return limitations;
    }

    private static void DescribeReportedVersion(TicketContext ticket, List<ContextLimitation> limitations)
    {
        var version = ticket.ReportedVersion;
        switch (version.Precision)
        {
            case PatchPrecision:
                return;
            case MinorPrecision:
                limitations.Add(new("reportedVersionIsMinorLine", $"The ticket reports '{version.Reported}', a minor version line. The exact patch release is unknown and is not assumed.", [ticket.Id]));
                return;
            default:
                limitations.Add(new("reportedVersionUnknown", version.Reported is null
                    ? "The ticket reports no version."
                    : $"The ticket reports '{version.Reported}', which is not a comparable version.", [ticket.Id]));
                return;
        }
    }

    private static void DescribeDocumentedFixes(
        TicketContext ticket,
        bool isClosed,
        IReadOnlyList<ConfirmedBugContext> confirmedBugs,
        IReadOnlyList<CandidateBugContext> candidateBugs,
        List<ContextLimitation> limitations)
    {
        var bugsWithFix = confirmedBugs.Where(bug => bug.FixedIn.Count > 0).Select(bug => bug.Id)
            .Concat(candidateBugs.Where(bug => bug.FixedIn.Count > 0).Select(bug => bug.Id))
            .ToList();
        if (bugsWithFix.Count == 0)
        {
            return;
        }

        var statusText = ticket.Status is null ? "no recorded status" : $"status '{ticket.Status}'";
        var resolutionText = isClosed
            ? "A documented fix still does not show which release the customer runs."
            : "The evidence does not show that the customer runs a fixed release or that the issue is resolved.";
        limitations.Add(new("documentedFixIsNotCustomerResolution", $"Fix releases are documented for {string.Join(", ", bugsWithFix)}. Ticket {ticket.Id} has {statusText}. {resolutionText}", bugsWithFix));
    }

    private static void DescribeKnowledge(IReadOnlyList<KnowledgeArticleContext> articles, bool knowledgeContentIncluded, List<ContextLimitation> limitations)
    {
        var candidateRoutes = articles
            .SelectMany(article => article.Routes
                .Where(route => route.Relationship == AddressesRelationship && route.Evidence == EvidenceClassification.Candidate)
                .Select(route => (ArticleId: article.Id, BugId: route.TargetId)))
            .ToList();
        if (candidateRoutes.Count > 0)
        {
            var articleIds = candidateRoutes.Select(route => route.ArticleId).Distinct(StringComparer.Ordinal).ToList();
            var bugIds = candidateRoutes.Select(route => route.BugId).Distinct(StringComparer.Ordinal).ToList();
            limitations.Add(new("knowledgeDocumentsCandidateBugs", $"These articles document candidate bugs ({string.Join(", ", bugIds)}): {string.Join(", ", articleIds)}. An article describing a bug does not show that the bug caused this ticket.", [.. articleIds, .. bugIds]));
        }

        if (articles.Count > 0 && !knowledgeContentIncluded)
        {
            limitations.Add(new("knowledgeContentNotLoaded", "Knowledge article content was not loaded for this investigation, so only titles and summaries are available.", articles.Select(article => article.Id).ToList()));
        }
    }
}
