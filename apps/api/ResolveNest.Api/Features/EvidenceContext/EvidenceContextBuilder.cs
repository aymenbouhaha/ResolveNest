using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.EvidenceContext;

public static class OmissionReasons
{
    public const string ContextItemLimit = "contextItemLimit";
    public const string QueryLimit = "queryLimit";
    public const string ContentItemLimit = "contentItemLimit";
    public const string ContentNotLoaded = "contentNotLoaded";
}

public sealed class EvidenceContextBuilder
{
    public const string ContractVersion = "evidence-context/1";

    private const string ReportedRelationship = "REPORTED";
    private const string AboutFeatureRelationship = "ABOUT_FEATURE";
    private const string ConfirmedAsRelationship = "CONFIRMED_AS";
    private const string AffectsRelationship = "AFFECTS";
    private const string IntroducedInRelationship = "INTRODUCED_IN";
    private const string FixedInRelationship = "FIXED_IN";
    private const string AddressesRelationship = "ADDRESSES";
    private const string CoversRelationship = "COVERS";
    private const string ReleaseType = "Release";
    private const string FeatureType = "Feature";
    private const string BugType = "Bug";
    private const int CoversRoutePriority = 10_000;
    private const int UnselectedRoutePriority = 20_000;

    private readonly TicketInvestigationResponse investigation;
    private readonly EvidenceContextLimits limits;
    private readonly CitationCatalog catalog = new();
    private readonly SourceTextBudget budget;
    private readonly List<EvidenceOmission> omissions = [];

    private EvidenceContextBuilder(TicketInvestigationResponse investigation, EvidenceContextLimits limits)
    {
        this.investigation = investigation;
        this.limits = limits;
        budget = new SourceTextBudget(limits.SourceTextCharacterBudget);
    }

    private InvestigatedTicket SourceTicket => investigation.Ticket;

    public static InvestigationEvidenceContext Build(TicketInvestigationResponse investigation, EvidenceContextLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(investigation);
        return new EvidenceContextBuilder(investigation, limits ?? EvidenceContextLimits.Default).Assemble();
    }

    private InvestigationEvidenceContext Assemble()
    {
        var ticket = BuildTicket();
        var customer = BuildCustomer();
        var features = investigation.Features.Select(BuildFeature).ToList();
        var confirmedBugs = investigation.ConfirmedBugs.Select(BuildConfirmedBug).ToList();
        var candidateBugs = BuildCandidateBugs();
        var knowledgeArticles = BuildKnowledgeArticles(candidateBugs);
        var relatedTickets = BuildRelatedTickets();

        var limitations = ContextLimitationRules.Describe(ticket, confirmedBugs, candidateBugs, knowledgeArticles, investigation.Counts.KnowledgeContentIncluded);
        var completeness = new ContextCompleteness(
            budget.TotalCharacters,
            budget.UsedCharacters,
            budget.Exhausted,
            budget.TruncatedExcerpts,
            budget.BudgetLimitedCitationIds.ToList(),
            omissions.Count == 0 && budget.TruncatedExcerpts == 0,
            omissions.ToList());

        return new InvestigationEvidenceContext(
            ContractVersion,
            ticket,
            customer,
            features,
            confirmedBugs,
            candidateBugs,
            relatedTickets,
            knowledgeArticles,
            limitations,
            catalog.ToOrderedList(),
            completeness);
    }

    private TicketContext BuildTicket()
    {
        var ticket = SourceTicket;
        var citationId = catalog.AddRecord(ticket.Id, ticket.Type, ticket.SourceSystem, ticket.SourceId, ticket.SourceRecord);
        return new TicketContext(
            ticket.Id,
            citationId,
            ticket.Status,
            ticket.Priority,
            new ReportedVersionContext(ticket.ReportedVersion, ticket.ReportedVersionNormalized, ticket.ReportedVersionPrecision),
            ticket.CreatedAt,
            ticket.ResolvedAt,
            TechnicalErrorCodes.Extract(ticket.Subject, ticket.Description),
            ticket.UnresolvedBugRefs.ToList(),
            budget.Excerpt(citationId, "subject", ticket.Subject, limits.ShortTextCharacters),
            budget.Excerpt(citationId, "description", ticket.Description, limits.TicketDescriptionCharacters),
            budget.Excerpt(citationId, "resolution", ticket.Resolution, limits.TicketResolutionCharacters));
    }

    private CustomerContext? BuildCustomer()
    {
        if (investigation.Customer is not { } customer)
        {
            return null;
        }

        var recordId = catalog.AddRecord(customer.Id, customer.Type, customer.SourceSystem, customer.SourceId, customer.SourceRecord);
        var reportedId = catalog.AddRelationship(customer.Id, customer.Type, ReportedRelationship, SourceTicket.Id, customer.Evidence, customer.Path, customer.RelationshipSourceRecord);
        return new CustomerContext(customer.Id, customer.Name, customer.Plan, customer.Deployment, customer.Evidence, customer.Path, [recordId, reportedId]);
    }

    private FeatureContext BuildFeature(TicketFeature feature)
    {
        var recordId = catalog.AddRecord(feature.Id, feature.Type, feature.SourceSystem, null, feature.SourceRecord);
        var aboutFeatureId = catalog.AddRelationship(SourceTicket.Id, SourceTicket.Type, AboutFeatureRelationship, feature.Id, feature.Evidence, feature.Path, feature.RelationshipSourceRecord);
        return new FeatureContext(feature.Id, feature.Name, feature.Evidence, feature.Path, [recordId, aboutFeatureId]);
    }

    private ConfirmedBugContext BuildConfirmedBug(ConfirmedBug bug)
    {
        var recordId = catalog.AddRecord(bug.Id, bug.Type, bug.SourceSystem, bug.SourceId, bug.SourceRecord);
        var confirmationId = catalog.AddRelationship(SourceTicket.Id, SourceTicket.Type, ConfirmedAsRelationship, bug.Id, bug.Evidence, bug.Path, bug.RelationshipSourceRecord);
        var affectsIds = bug.AffectedFeatures
            .Select(feature => catalog.AddRelationship(bug.Id, bug.Type, AffectsRelationship, feature.Id, feature.Evidence, CitationCatalog.DefaultPath(bug.Type, AffectsRelationship, FeatureType), feature.RelationshipSourceRecord))
            .ToList();

        var title = budget.Excerpt(recordId, "title", bug.Title, limits.ShortTextCharacters);
        var workaround = budget.Excerpt(recordId, "workaround", bug.Workaround, limits.WorkaroundCharacters);
        var introducedIn = BuildReleases(bug.Id, bug.Type, IntroducedInRelationship, bug.IntroducedIn);
        var fixedIn = BuildReleases(bug.Id, bug.Type, FixedInRelationship, bug.FixedIn);

        return new ConfirmedBugContext(
            bug.Id,
            bug.Evidence,
            bug.Path,
            bug.SourceStatus,
            bug.Severity,
            bug.ConfirmedBy,
            bug.ConfirmedAt,
            bug.AffectedFeatures.Select(feature => feature.Id).ToList(),
            introducedIn,
            fixedIn,
            title,
            workaround,
            [recordId, confirmationId, .. affectsIds]);
    }

    private List<ReleaseContext> BuildReleases(string bugId, string bugType, string relationshipType, IReadOnlyList<ReleaseEvidence> releases)
    {
        var contexts = new List<ReleaseContext>(releases.Count);
        foreach (var release in releases)
        {
            var recordId = catalog.AddRecord(release.Id, ReleaseType, null, null, null);
            var relationshipId = catalog.AddRelationship(bugId, bugType, relationshipType, release.Id, release.Evidence, CitationCatalog.DefaultPath(bugType, relationshipType, ReleaseType), release.RelationshipSourceRecord);
            var evidenceText = budget.Excerpt(relationshipId, "evidenceText", release.EvidenceText, limits.ReleaseEvidenceCharacters);
            contexts.Add(new ReleaseContext(release.Id, release.Version, release.ReleasedAt, release.Evidence, evidenceText, [relationshipId, recordId]));
        }

        return contexts;
    }

    private List<CandidateBugContext> BuildCandidateBugs()
    {
        var candidates = investigation.CandidateBugs;
        var selectedIds = SelectCandidateIds(candidates);
        var contexts = new List<CandidateBugContext>(selectedIds.Count);
        for (var index = 0; index < candidates.Count; index++)
        {
            if (selectedIds.Contains(candidates[index].Id))
            {
                contexts.Add(BuildCandidateBug(candidates[index], index + 1));
            }
        }

        RecordItemLimitOmission("candidateBugs", null, candidates.Select(candidate => candidate.Id).ToList(), selectedIds);
        if (investigation.Counts.CandidateBugLimitReached)
        {
            omissions.Add(new EvidenceOmission("candidateBugs", null, OmissionReasons.QueryLimit, null, candidates.Count, []));
        }

        return contexts;
    }

    private HashSet<string> SelectCandidateIds(IReadOnlyList<CandidateBug> candidates)
    {
        var selected = new HashSet<string>(
            candidates.Where(candidate => candidate.Relevance.ErrorCodes.Status == ErrorCodeMatchStatus.Matched).Select(candidate => candidate.Id),
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (selected.Count >= limits.MaxCandidateBugs)
            {
                break;
            }

            selected.Add(candidate.Id);
        }

        return selected;
    }

    private CandidateBugContext BuildCandidateBug(CandidateBug bug, int investigationRank)
    {
        var recordId = catalog.AddRecord(bug.Id, bug.Type, bug.SourceSystem, bug.SourceId, bug.SourceRecord);
        var routeIds = new List<string>();
        foreach (var sharedFeature in bug.SharedFeatures)
        {
            var aboutFeatureId = CitationCatalog.RelationshipCitationId(SourceTicket.Id, AboutFeatureRelationship, sharedFeature.FeatureId);
            if (catalog.Contains(aboutFeatureId))
            {
                routeIds.Add(aboutFeatureId);
            }

            routeIds.Add(catalog.AddRelationship(
                bug.Id,
                bug.Type,
                AffectsRelationship,
                sharedFeature.FeatureId,
                EvidenceClassification.Documented,
                CitationCatalog.DefaultPath(bug.Type, AffectsRelationship, FeatureType),
                sharedFeature.RelationshipSourceRecord));
        }

        var title = budget.Excerpt(recordId, "title", bug.Title, limits.ShortTextCharacters);
        var description = budget.Excerpt(recordId, "description", bug.Description, limits.BugDescriptionCharacters);
        var workaround = budget.Excerpt(recordId, "workaround", bug.Workaround, limits.WorkaroundCharacters);
        var introducedIn = BuildReleases(bug.Id, bug.Type, IntroducedInRelationship, bug.IntroducedIn);
        var fixedIn = BuildReleases(bug.Id, bug.Type, FixedInRelationship, bug.FixedIn);
        var history = BuildHistory(bug);

        return new CandidateBugContext(
            investigationRank,
            bug.Id,
            bug.Evidence,
            bug.Path,
            bug.Reason,
            bug.SourceStatus,
            bug.Severity,
            bug.Relevance,
            introducedIn,
            fixedIn,
            history,
            title,
            description,
            workaround,
            [recordId, .. routeIds.Distinct(StringComparer.Ordinal)]);
    }

    private HistoricalConfirmations BuildHistory(CandidateBug bug)
    {
        var returned = bug.ConfirmedTickets;
        var examples = returned.Where(ticket => ticket.SameCustomer)
            .Concat(returned.Where(ticket => !ticket.SameCustomer))
            .Take(limits.MaxHistoricalExamplesPerBug)
            .ToList();

        var exampleContexts = new List<HistoricalTicketContext>(examples.Count);
        foreach (var ticket in examples)
        {
            var recordId = catalog.AddRecord(ticket.Id, ticket.Type, ticket.SourceSystem, null, ticket.SourceRecord);
            var confirmationId = catalog.AddRelationship(ticket.Id, ticket.Type, ConfirmedAsRelationship, bug.Id, ticket.Evidence, ticket.Path, ticket.RelationshipSourceRecord);
            exampleContexts.Add(new HistoricalTicketContext(
                ticket.Id,
                ticket.Evidence,
                ticket.Path,
                ticket.CustomerId,
                ticket.SameCustomer,
                ticket.Status,
                ticket.ReportedVersion,
                ticket.ConfirmedAt,
                budget.Excerpt(recordId, "subject", ticket.Subject, limits.ShortTextCharacters),
                [recordId, confirmationId]));
        }

        var includedIds = examples.Select(ticket => ticket.Id).ToHashSet(StringComparer.Ordinal);
        RecordItemLimitOmission("historicalConfirmations", bug.Id, returned.Select(ticket => ticket.Id).ToList(), includedIds);
        if (bug.ConfirmedTicketsTruncated)
        {
            omissions.Add(new EvidenceOmission("historicalConfirmations", bug.Id, OmissionReasons.QueryLimit, bug.ConfirmedTicketCount, returned.Count, []));
        }

        return new HistoricalConfirmations(
            bug.ConfirmedTicketCount,
            bug.ConfirmedCustomerCount,
            returned.Count,
            bug.ConfirmedTicketsTruncated,
            returned.Count(ticket => ticket.SameCustomer),
            exampleContexts,
            returned.Count - examples.Count);
    }

    private List<KnowledgeArticleContext> BuildKnowledgeArticles(IReadOnlyList<CandidateBugContext> selectedCandidates)
    {
        var confirmedBugIds = investigation.ConfirmedBugs.Select(bug => bug.Id).ToHashSet(StringComparer.Ordinal);
        var candidateRanks = selectedCandidates.ToDictionary(candidate => candidate.Id, candidate => candidate.InvestigationRank, StringComparer.Ordinal);

        int RoutePriority(KnowledgeRoute route) => route.Relationship switch
        {
            AddressesRelationship when confirmedBugIds.Contains(route.TargetId) => 0,
            AddressesRelationship when candidateRanks.TryGetValue(route.TargetId, out var rank) => rank,
            CoversRelationship => CoversRoutePriority,
            _ => UnselectedRoutePriority,
        };

        var ordered = investigation.KnowledgeArticles
            .OrderBy(article => article.ReachedVia.Select(RoutePriority).DefaultIfEmpty(UnselectedRoutePriority).Min())
            .ThenBy(article => article.Id, StringComparer.Ordinal)
            .ToList();
        var included = ordered.Take(limits.MaxKnowledgeArticles).ToList();

        var contexts = new List<KnowledgeArticleContext>(included.Count);
        var contentOmittedIds = new List<string>();
        var contentMissingIds = new List<string>();
        for (var index = 0; index < included.Count; index++)
        {
            var article = included[index];
            var includeContent = index < limits.MaxArticleContentExcerpts;
            if (article.Content is null)
            {
                contentMissingIds.Add(article.Id);
            }
            else if (!includeContent)
            {
                contentOmittedIds.Add(article.Id);
            }

            contexts.Add(BuildKnowledgeArticle(article, includeContent));
        }

        RecordItemLimitOmission("knowledgeArticles", null, ordered.Select(article => article.Id).ToList(), included.Select(article => article.Id).ToHashSet(StringComparer.Ordinal));
        if (contentOmittedIds.Count > 0)
        {
            omissions.Add(new EvidenceOmission("knowledgeContent", null, OmissionReasons.ContentItemLimit, included.Count, included.Count - contentOmittedIds.Count - contentMissingIds.Count, contentOmittedIds));
        }

        if (contentMissingIds.Count > 0)
        {
            omissions.Add(new EvidenceOmission("knowledgeContent", null, OmissionReasons.ContentNotLoaded, included.Count, included.Count - contentOmittedIds.Count - contentMissingIds.Count, contentMissingIds));
        }

        return contexts;
    }

    private KnowledgeArticleContext BuildKnowledgeArticle(KnowledgeArticleEvidence article, bool includeContent)
    {
        var recordId = catalog.AddRecord(article.Id, article.Type, article.SourceSystem, article.SourceId, article.SourceRecord, article.SourcePath);
        var routes = article.ReachedVia
            .Select(route => new KnowledgeRouteContext(
                route.Relationship,
                route.TargetId,
                route.Basis,
                route.Evidence,
                catalog.AddRelationship(article.Id, article.Type, route.Relationship, route.TargetId, route.Evidence, CitationCatalog.DefaultPath(article.Type, route.Relationship, RouteTargetType(route.Relationship)), route.RelationshipSourceRecord)))
            .ToList();

        var title = budget.Excerpt(recordId, "title", article.Title, limits.ShortTextCharacters);
        var summary = budget.Excerpt(recordId, "summary", article.Summary, limits.ArticleSummaryCharacters);
        var content = includeContent ? budget.Excerpt(recordId, "content", article.Content, limits.ArticleContentCharacters) : null;

        return new KnowledgeArticleContext(
            article.Id,
            article.UpdatedAt,
            routes,
            article.Content is not null,
            title,
            summary,
            content,
            [recordId, .. routes.Select(route => route.CitationId)]);
    }

    private static string? RouteTargetType(string relationship) => relationship switch
    {
        AddressesRelationship => BugType,
        CoversRelationship => FeatureType,
        _ => null,
    };

    private RelatedTicketsContext BuildRelatedTickets()
    {
        var returned = investigation.RelatedTickets;
        var included = returned.Where(ticket => ticket.SameCustomer)
            .Concat(returned.Where(ticket => !ticket.SameCustomer))
            .Take(limits.MaxRelatedTickets)
            .ToList();

        var contexts = new List<RelatedTicketContext>(included.Count);
        foreach (var ticket in included)
        {
            var recordId = catalog.AddRecord(ticket.Id, ticket.Type, ticket.SourceSystem, null, ticket.SourceRecord);
            contexts.Add(new RelatedTicketContext(
                ticket.Id,
                ticket.Evidence,
                ticket.Path,
                ticket.CustomerId,
                ticket.SameCustomer,
                ticket.Status,
                ticket.CreatedAt,
                ticket.ReportedVersion,
                ticket.ConfirmedBugIds.ToList(),
                budget.Excerpt(recordId, "subject", ticket.Subject, limits.ShortTextCharacters),
                [recordId]));
        }

        RecordItemLimitOmission("relatedTickets", null, returned.Select(ticket => ticket.Id).ToList(), included.Select(ticket => ticket.Id).ToHashSet(StringComparer.Ordinal));
        if (investigation.Counts.RelatedTicketsTruncated)
        {
            omissions.Add(new EvidenceOmission("relatedTickets", null, OmissionReasons.QueryLimit, investigation.Counts.RelatedTicketTotal, returned.Count, []));
        }

        return new RelatedTicketsContext(investigation.Counts.RelatedTicketTotal, returned.Count, contexts);
    }

    private void RecordItemLimitOmission(string section, string? parentId, IReadOnlyList<string> availableIds, IReadOnlySet<string> includedIds)
    {
        var omittedIds = availableIds.Where(id => !includedIds.Contains(id)).ToList();
        if (omittedIds.Count > 0)
        {
            omissions.Add(new EvidenceOmission(section, parentId, OmissionReasons.ContextItemLimit, availableIds.Count, availableIds.Count - omittedIds.Count, omittedIds));
        }
    }
}
