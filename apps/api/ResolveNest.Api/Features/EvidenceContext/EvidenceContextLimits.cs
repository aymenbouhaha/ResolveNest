namespace ResolveNest.Api.Features.EvidenceContext;

public sealed record EvidenceContextLimits(
    int SourceTextCharacterBudget = 16_000,
    int MaxCandidateBugs = 5,
    int MaxHistoricalExamplesPerBug = 3,
    int MaxRelatedTickets = 5,
    int MaxKnowledgeArticles = 5,
    int MaxArticleContentExcerpts = 3,
    int TicketDescriptionCharacters = 2_000,
    int TicketResolutionCharacters = 600,
    int ShortTextCharacters = 200,
    int BugDescriptionCharacters = 600,
    int WorkaroundCharacters = 400,
    int ReleaseEvidenceCharacters = 300,
    int ArticleSummaryCharacters = 300,
    int ArticleContentCharacters = 1_500)
{
    public static EvidenceContextLimits Default { get; } = new();
}
