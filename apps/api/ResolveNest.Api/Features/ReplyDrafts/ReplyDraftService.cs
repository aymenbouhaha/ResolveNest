using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ResolveNest.Api.Features.AiInvestigations;
using ResolveNest.Api.Features.EvidenceContext;
using ResolveNest.Api.Features.Investigations;

namespace ResolveNest.Api.Features.ReplyDrafts;

public sealed class PreviewTicketMismatchException() : Exception("The preview token belongs to a different ticket.");

public sealed class ReplyDraftConflictException() : Exception("A different text was already approved for this preview.");

public sealed class ReplyDraftService(
    TicketInvestigationService investigations,
    GeminiClient gemini,
    ReplyPreviewTokens tokens,
    IReplyDraftStore store,
    IOptions<GeminiOptions> options,
    ILogger<ReplyDraftService> logger)
{
    public const string ApprovalBasis = "unauthenticatedLocalRequest";
    public const string CustomerDelivery = "notSent";
    public const string DraftIdPrefix = "reply-draft:";

    public async Task<ReplyDraftPreview?> PreviewAsync(string ticketId, string? reviewerGuidance, CancellationToken cancellationToken)
    {
        var investigation = await investigations.InvestigateAsync(ticketId, includeContent: true, cancellationToken);
        if (investigation is null)
        {
            return null;
        }

        var context = EvidenceContextBuilder.Build(investigation, EvidenceProjector.ContextLimits);
        var projection = EvidenceProjector.Project(context);
        var userContent = ReplyDraftPrompt.UserContent(reviewerGuidance, projection);
        if (userContent.Length > AiInvestigationService.MaxUserContentCharacters)
        {
            throw new EvidencePayloadTooLargeException(userContent.Length, AiInvestigationService.MaxUserContentCharacters);
        }

        var generation = await gemini.GenerateJsonAsync(
            ReplyDraftPrompt.SystemInstruction,
            userContent,
            ReplyDraftPrompt.ResponseSchema(projection.AllowedCitationIds),
            cancellationToken);

        var validationContext = ReplyDraftValidationContext.From(
            AnswerValidationContext.From(context, projection),
            JsonSerializer.Serialize(projection, JsonSerializerOptions.Web));
        ModelReplyDraft draft;
        try
        {
            draft = ReplyDraftValidator.Validate(generation.Text, validationContext);
        }
        catch (AiAnswerRejectedException exception)
        {
            logger.LogWarning("Reply draft for {TicketId} rejected: {Reasons}.", ticketId, string.Join(", ", exception.Reasons));
            throw;
        }

        var citedIds = draft.SupportingClaims.Concat(draft.Uncertainties).SelectMany(item => item.CitationIds).ToHashSet(StringComparer.Ordinal);
        var validatedSources = context.Sources.Where(source => citedIds.Contains(source.CitationId)).ToList();
        var generatedAt = tokens.Now;
        var expiresAt = generatedAt + ReplyPreviewTokens.Lifetime;
        var previewId = Guid.NewGuid().ToString("N");
        var settings = options.Value;

        var token = tokens.Protect(new ReplyPreviewTokenPayload(
            ReplyPreviewTokens.CurrentVersion,
            previewId,
            investigation.Ticket.Id,
            draft.DraftBody,
            validatedSources.Select(source => source.CitationId).ToList(),
            context.Limitations.Select(limitation => limitation.Code).ToList(),
            settings.Model,
            generation.ModelVersion,
            projection.ContractVersion,
            generatedAt,
            expiresAt));

        logger.LogInformation("Reply draft preview {PreviewId} generated for {TicketId}; nothing was persisted.", previewId, ticketId);
        return new ReplyDraftPreview(
            investigation.Ticket.Id,
            previewId,
            draft.Status,
            draft.DraftBody,
            draft.SupportingClaims,
            draft.Uncertainties,
            context.Limitations,
            validatedSources,
            new AiAnswerProvenance(
                AiInvestigationService.Provider,
                settings.Model,
                generation.ModelVersion,
                projection.ContractVersion,
                context.Completeness.IsComplete,
                userContent.Length,
                generation.PromptTokens,
                generation.OutputTokens,
                HumanReviewRequired: true),
            token,
            expiresAt,
            HumanReviewRequired: true,
            Persisted: false);
    }

    public async Task<ReplyDraftApprovalResult?> ApproveAsync(string ticketId, string previewToken, string finalText, CancellationToken cancellationToken)
    {
        var payload = tokens.Unprotect(previewToken);
        if (!string.Equals(payload.TicketId, ticketId, StringComparison.Ordinal))
        {
            throw new PreviewTicketMismatchException();
        }

        var approvedText = NormalizeText(finalText);
        var editedByHuman = !string.Equals(approvedText, NormalizeText(payload.GeneratedText), StringComparison.Ordinal);
        var draft = new ApprovedReplyDraft(
            DraftIdPrefix + payload.PreviewId,
            ticketId,
            ReplyDraftGraph.ApprovedStatus,
            approvedText,
            payload.GeneratedText,
            editedByHuman,
            GeneratedTextValidated: true,
            ApprovedTextValidated: !editedByHuman,
            payload.CitationIds,
            payload.LimitationCodes,
            payload.Model,
            payload.ModelVersion,
            payload.EvidenceContractVersion,
            payload.GeneratedAt,
            tokens.Now,
            ApprovalBasis,
            CustomerDelivery);

        var result = await store.SaveAsync(draft, Sha256(approvedText), cancellationToken);
        switch (result.Outcome)
        {
            case ReplyDraftSaveOutcome.Created:
                logger.LogInformation("Reply draft {DraftId} approved for {TicketId} (edited by human: {Edited}). No message was sent.", draft.DraftId, ticketId, editedByHuman);
                return new ReplyDraftApprovalResult(result.Draft!, AlreadyApproved: false);
            case ReplyDraftSaveOutcome.AlreadyApproved:
                return new ReplyDraftApprovalResult(result.Draft!, AlreadyApproved: true);
            case ReplyDraftSaveOutcome.Conflict:
                logger.LogWarning("Conflicting approval rejected for reply draft {DraftId}.", draft.DraftId);
                throw new ReplyDraftConflictException();
            default:
                return null;
        }
    }

    public static string NormalizeText(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();

    public static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
