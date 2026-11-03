using Microsoft.Extensions.Options;
using ResolveNest.Api.Features.EvidenceContext;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.AiInvestigations;

public sealed class EvidencePayloadTooLargeException(int characters, int limit)
    : Exception($"The evidence payload has {characters} characters, above the {limit}-character limit.");

public sealed class AiInvestigationService(
    TicketInvestigationService investigations,
    GeminiClient gemini,
    IOptions<GeminiOptions> options,
    ILogger<AiInvestigationService> logger)
{
    public const string Provider = "gemini";
    public const int MaxUserContentCharacters = 40_000;

    public async Task<AiInvestigationAnswer?> AskAsync(string ticketId, string question, CancellationToken cancellationToken)
    {
        var investigation = await investigations.InvestigateAsync(ticketId, includeContent: true, cancellationToken);
        if (investigation is null)
        {
            return null;
        }

        var context = EvidenceContextBuilder.Build(investigation, EvidenceProjector.ContextLimits);
        var projection = EvidenceProjector.Project(context);
        var userContent = GroundedAnswerPrompt.UserContent(question, projection);
        if (userContent.Length > MaxUserContentCharacters)
        {
            throw new EvidencePayloadTooLargeException(userContent.Length, MaxUserContentCharacters);
        }

        var generation = await gemini.GenerateJsonAsync(
            GroundedAnswerPrompt.SystemInstruction,
            userContent,
            GroundedAnswerPrompt.ResponseSchema(projection.AllowedCitationIds),
            cancellationToken);

        var validationContext = AnswerValidationContext.From(context, projection);
        ModelAnswer answer;
        try
        {
            answer = AiAnswerValidator.Validate(generation.Text, validationContext);
        }
        catch (AiAnswerRejectedException exception)
        {
            logger.LogWarning("AI answer for {TicketId} rejected: {Reasons}.", ticketId, string.Join(", ", exception.Reasons));
            throw;
        }

        var citedIds = answer.Findings.SelectMany(finding => finding.CitationIds)
            .Concat(answer.Uncertainties.SelectMany(statement => statement.CitationIds))
            .Concat(answer.RecommendedNextSteps.SelectMany(statement => statement.CitationIds))
            .ToHashSet(StringComparer.Ordinal);
        var validatedSources = context.Sources.Where(source => citedIds.Contains(source.CitationId)).ToList();

        return new AiInvestigationAnswer(
            investigation.Ticket.Id,
            answer.Status,
            answer.Answer,
            answer.Findings,
            answer.Uncertainties,
            answer.RecommendedNextSteps,
            validatedSources,
            new AiAnswerProvenance(
                Provider,
                options.Value.Model,
                generation.ModelVersion,
                projection.ContractVersion,
                context.Completeness.IsComplete,
                userContent.Length,
                generation.PromptTokens,
                generation.OutputTokens,
                HumanReviewRequired: true));
    }
}
