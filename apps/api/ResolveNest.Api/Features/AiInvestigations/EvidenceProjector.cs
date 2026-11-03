using ResolveNest.Api.Features.EvidenceContext;

namespace ResolveNest.Api.Features.AiInvestigations;

public static class EvidenceProjector
{
    public const string ContractVersion = "ai-evidence-projection/1";

    public static EvidenceContextLimits ContextLimits { get; } = new(
        SourceTextCharacterBudget: 9_000,
        MaxCandidateBugs: 4,
        MaxHistoricalExamplesPerBug: 2,
        MaxRelatedTickets: 3,
        MaxKnowledgeArticles: 3,
        MaxArticleContentExcerpts: 2,
        TicketDescriptionCharacters: 1_500,
        TicketResolutionCharacters: 500,
        ShortTextCharacters: 160,
        BugDescriptionCharacters: 500,
        WorkaroundCharacters: 300,
        ReleaseEvidenceCharacters: 200,
        ArticleSummaryCharacters: 200,
        ArticleContentCharacters: 1_500);

    public static ProjectedEvidence Project(InvestigationEvidenceContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var citations = new CitationCollector();

        var ticket = context.Ticket;
        var projectedTicket = new ProjectedTicket(
            ticket.Id,
            ticket.Status,
            ticket.Priority,
            ticket.ReportedVersion.Reported,
            ticket.ReportedVersion.Precision,
            ticket.CreatedAt,
            ticket.ResolvedAt,
            ticket.ErrorCodes,
            Text(ticket.Subject),
            Text(ticket.Description),
            Text(ticket.Resolution),
            citations.Cite([ticket.CitationId]));

        var customer = context.Customer is { } reporting
            ? new ProjectedCustomer(reporting.Id, reporting.Name, reporting.Plan, reporting.Deployment, reporting.Evidence, citations.Cite(reporting.CitationIds))
            : null;

        var features = context.Features
            .Select(feature => new ProjectedFeature(feature.Id, feature.Name, feature.Evidence, citations.Cite(feature.CitationIds)))
            .ToList();

        var confirmedBugs = context.ConfirmedBugs
            .Select(bug => new ProjectedConfirmedBug(
                bug.Id,
                bug.Evidence,
                Text(bug.Title),
                bug.SourceStatus,
                bug.Severity,
                Text(bug.Workaround),
                bug.ConfirmedAt,
                Releases(bug.IntroducedIn, citations),
                Releases(bug.FixedIn, citations),
                citations.Cite(bug.CitationIds)))
            .ToList();

        var candidateBugs = context.CandidateBugs.Select(bug => ProjectCandidate(bug, citations)).ToList();

        var articles = context.KnowledgeArticles
            .Select(article => new ProjectedArticle(
                article.Id,
                Text(article.Title),
                Text(article.Summary),
                Text(article.Content),
                article.Routes.Select(route => new ProjectedRoute(route.Relationship, route.TargetId, route.Evidence, citations.Cite([route.CitationId])[0])).ToList(),
                citations.Cite(article.CitationIds)))
            .ToList();

        var relatedTickets = new ProjectedRelatedTickets(
            context.RelatedTickets.Total,
            context.RelatedTickets.Included
                .Select(related => new ProjectedRelatedTicket(related.Id, related.SameCustomer, related.Status, related.ConfirmedBugIds, Text(related.Subject), citations.Cite(related.CitationIds)))
                .ToList());

        return new ProjectedEvidence(
            ContractVersion,
            projectedTicket,
            customer,
            features,
            confirmedBugs,
            candidateBugs,
            articles,
            relatedTickets,
            context.Limitations.Select(limitation => new ProjectedLimitation(limitation.Code, limitation.Message)).ToList(),
            new ProjectedCompleteness(context.Completeness.IsComplete, context.Completeness.Omissions.Select(DescribeOmission).ToList()),
            citations.Ordered());
    }

    private static ProjectedCandidateBug ProjectCandidate(CandidateBugContext bug, CitationCollector citations)
    {
        var relevance = bug.Relevance;
        var examples = bug.History.Examples
            .Select(example => new ProjectedHistoricalTicket(example.Id, example.CustomerId, example.SameCustomer, example.Status, example.ReportedVersion, example.Evidence, citations.Cite(example.CitationIds)))
            .ToList();

        return new ProjectedCandidateBug(
            bug.InvestigationRank,
            bug.Id,
            bug.Evidence,
            relevance.Tier,
            relevance.ErrorCodes.Status,
            relevance.ErrorCodes.MatchedCodes,
            relevance.Version.Assessment,
            Text(bug.Title),
            Text(bug.Description),
            bug.SourceStatus,
            bug.Severity,
            Text(bug.Workaround),
            Releases(bug.IntroducedIn, citations),
            Releases(bug.FixedIn, citations),
            new ProjectedHistory(bug.History.ConfirmedTicketCount, bug.History.ConfirmedCustomerCount, examples),
            relevance.Supporting.Select(note => note.Message).ToList(),
            relevance.Limitations.Select(note => note.Message).ToList(),
            citations.Cite(bug.CitationIds));
    }

    private static List<ProjectedRelease> Releases(IReadOnlyList<ReleaseContext> releases, CitationCollector citations) => releases
        .Select(release => new ProjectedRelease(release.Version, release.ReleasedAt, release.Evidence, Text(release.EvidenceText), citations.Cite(release.CitationIds)))
        .ToList();

    private static ProjectedText? Text(SourceExcerpt? excerpt) =>
        excerpt is null ? null : new ProjectedText(excerpt.VerbatimText, excerpt.Truncated ? true : null);

    private static string DescribeOmission(EvidenceOmission omission)
    {
        var scope = omission.ParentId is null ? omission.Section : $"{omission.Section} of {omission.ParentId}";
        var available = omission.Available is { } count ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "an unknown number of";
        return $"{scope}: {omission.Included} of {available} included ({omission.Reason})";
    }

    private sealed class CitationCollector
    {
        private readonly SortedSet<string> citationIds = new(StringComparer.Ordinal);

        public IReadOnlyList<string> Cite(IReadOnlyList<string> ids)
        {
            foreach (var id in ids)
            {
                citationIds.Add(id);
            }

            return ids;
        }

        public IReadOnlyList<string> Ordered() => citationIds.ToList();
    }
}
