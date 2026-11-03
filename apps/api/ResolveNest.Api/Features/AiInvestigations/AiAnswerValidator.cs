using System.Text.Json;
using ResolveNest.Api.Features.EvidenceContext;

namespace ResolveNest.Api.Features.AiInvestigations;

public static class AnswerRejectionReasons
{
    public const string InvalidJson = "invalidJson";
    public const string SchemaViolation = "schemaViolation";
    public const string InvalidCitation = "invalidCitation";
    public const string MissingCitation = "missingCitation";
    public const string InconsistentStatus = "inconsistentStatus";
    public const string UnsupportedConfirmation = "unsupportedConfirmation";
    public const string UnsupportedClassification = "unsupportedClassification";
    public const string CandidatePromotedToConfirmed = "candidatePromotedToConfirmed";
    public const string UnsupportedResolutionClaim = "unsupportedResolutionClaim";
    public const string UnsupportedActionClaim = "unsupportedActionClaim";
}

public sealed class AiAnswerRejectedException(IReadOnlyList<string> reasons)
    : Exception($"AI answer rejected: {string.Join(", ", reasons)}.")
{
    public IReadOnlyList<string> Reasons { get; } = reasons;
}

public sealed record AnswerValidationContext(
    string TicketId,
    string? TicketStatus,
    string? CustomerName,
    IReadOnlySet<string> AllowedCitationIds,
    IReadOnlyDictionary<string, CitationSource> Sources,
    IReadOnlySet<string> ConfirmedBugIds,
    IReadOnlySet<string> CandidateBugIds)
{
    private const string ConfirmedAsRelationship = "CONFIRMED_AS";
    private static readonly HashSet<string> ClosedStatuses = new(StringComparer.Ordinal) { "resolved", "closed" };

    public bool IsTicketClosed => TicketStatus is { } status && ClosedStatuses.Contains(status);

    public static AnswerValidationContext From(InvestigationEvidenceContext context, ProjectedEvidence projection) => new(
        context.Ticket.Id,
        context.Ticket.Status,
        context.Customer?.Name,
        projection.AllowedCitationIds.ToHashSet(StringComparer.Ordinal),
        context.Sources.Where(source => projection.AllowedCitationIds.Contains(source.CitationId)).ToDictionary(source => source.CitationId, StringComparer.Ordinal),
        context.ConfirmedBugs.Select(bug => bug.Id).ToHashSet(StringComparer.Ordinal),
        context.CandidateBugs.Select(bug => bug.Id).ToHashSet(StringComparer.Ordinal));

    public bool IsTargetConfirmation(string citationId) =>
        Sources.TryGetValue(citationId, out var source)
        && source.Relationship is { Type: ConfirmedAsRelationship } relationship
        && relationship.FromId == TicketId
        && ConfirmedBugIds.Contains(relationship.ToId);

    public bool IsHistoricalConfirmation(string citationId) =>
        Sources.TryGetValue(citationId, out var source)
        && (source.Relationship is { Type: ConfirmedAsRelationship } relationship && relationship.FromId != TicketId
            || source.Kind == CitationKind.Record && CandidateBugIds.Contains(source.EntityId));

    public bool IsCandidateEvidence(string citationId) =>
        Sources.TryGetValue(citationId, out var source)
        && (CandidateBugIds.Contains(source.EntityId)
            || source.Relationship is { } relationship && (CandidateBugIds.Contains(relationship.FromId) || CandidateBugIds.Contains(relationship.ToId)));

    public bool IsKnownCitation(string citationId) => AllowedCitationIds.Contains(citationId) && Sources.ContainsKey(citationId);
}

public static class AiAnswerValidator
{
    public const int MaxAnswerCharacters = 3_000;
    public const int MaxStatementCharacters = 800;

    public static ModelAnswer Validate(string modelText, AnswerValidationContext context)
    {
        var answer = Parse(modelText);
        var reasons = new SortedSet<string>(StringComparer.Ordinal);

        CheckShape(answer, reasons);
        if (reasons.Count > 0)
        {
            throw new AiAnswerRejectedException(reasons.ToList());
        }

        CheckCitations(answer, context, reasons);
        CheckStatus(answer, reasons);
        CheckFindingKinds(answer, context, reasons);
        GroundedClaimRules.Check(AllTexts(answer), context, reasons);

        if (reasons.Count > 0)
        {
            throw new AiAnswerRejectedException(reasons.ToList());
        }

        return answer;
    }

    public static bool IsValidStatement(string statement, IReadOnlyList<string> citationIds) =>
        !string.IsNullOrWhiteSpace(statement)
        && statement.Length <= MaxStatementCharacters
        && citationIds.Count <= GroundedAnswerPrompt.MaxCitationsPerItem
        && citationIds.All(id => !string.IsNullOrWhiteSpace(id))
        && citationIds.Distinct(StringComparer.Ordinal).Count() == citationIds.Count;

    private static ModelAnswer Parse(string modelText)
    {
        try
        {
            return JsonSerializer.Deserialize<ModelAnswer>(modelText, GroundedClaimRules.StrictSerializerOptions)
                ?? throw new AiAnswerRejectedException([AnswerRejectionReasons.InvalidJson]);
        }
        catch (JsonException)
        {
            throw new AiAnswerRejectedException([AnswerRejectionReasons.InvalidJson]);
        }
    }

    private static void CheckShape(ModelAnswer answer, SortedSet<string> reasons)
    {
        var withinLimits = !string.IsNullOrWhiteSpace(answer.Answer)
            && answer.Answer.Length <= MaxAnswerCharacters
            && answer.Findings.Count <= GroundedAnswerPrompt.MaxFindings
            && answer.Uncertainties.Count <= GroundedAnswerPrompt.MaxStatements
            && answer.RecommendedNextSteps.Count <= GroundedAnswerPrompt.MaxStatements
            && Enum.IsDefined(answer.Status)
            && answer.Findings.All(finding => finding is not null && Enum.IsDefined(finding.Kind) && IsValidStatement(finding.Statement, finding.CitationIds))
            && answer.Uncertainties.Concat(answer.RecommendedNextSteps).All(statement => statement is not null && IsValidStatement(statement.Statement, statement.CitationIds));
        if (!withinLimits)
        {
            reasons.Add(AnswerRejectionReasons.SchemaViolation);
        }
    }

    private static void CheckCitations(ModelAnswer answer, AnswerValidationContext context, SortedSet<string> reasons)
    {
        var cited = answer.Findings.SelectMany(finding => finding.CitationIds)
            .Concat(answer.Uncertainties.SelectMany(statement => statement.CitationIds))
            .Concat(answer.RecommendedNextSteps.SelectMany(statement => statement.CitationIds));
        if (cited.Any(id => !context.IsKnownCitation(id)))
        {
            reasons.Add(AnswerRejectionReasons.InvalidCitation);
        }

        if (answer.Findings.Any(finding => finding.CitationIds.Count == 0))
        {
            reasons.Add(AnswerRejectionReasons.MissingCitation);
        }
    }

    private static void CheckStatus(ModelAnswer answer, SortedSet<string> reasons)
    {
        var consistent = answer.Status switch
        {
            AiAnswerStatus.Answered => answer.Findings.Count > 0,
            _ => answer.Uncertainties.Count > 0,
        };
        if (!consistent)
        {
            reasons.Add(AnswerRejectionReasons.InconsistentStatus);
        }
    }

    private static void CheckFindingKinds(ModelAnswer answer, AnswerValidationContext context, SortedSet<string> reasons)
    {
        foreach (var finding in answer.Findings)
        {
            switch (finding.Kind)
            {
                case AiFindingKind.ConfirmedForTicket when !finding.CitationIds.Any(context.IsTargetConfirmation):
                    reasons.Add(AnswerRejectionReasons.UnsupportedConfirmation);
                    if (GroundedClaimRules.MentionedCandidates(finding.Statement, context).Any())
                    {
                        reasons.Add(AnswerRejectionReasons.CandidatePromotedToConfirmed);
                    }

                    break;
                case AiFindingKind.HistoricalConfirmation when !finding.CitationIds.Any(context.IsHistoricalConfirmation):
                    reasons.Add(AnswerRejectionReasons.UnsupportedConfirmation);
                    break;
                case AiFindingKind.Candidate when !finding.CitationIds.Any(context.IsCandidateEvidence):
                    reasons.Add(AnswerRejectionReasons.UnsupportedClassification);
                    break;
            }
        }
    }

    private static IEnumerable<string> AllTexts(ModelAnswer answer) =>
        new[] { answer.Answer }
            .Concat(answer.Findings.Select(finding => finding.Statement))
            .Concat(answer.Uncertainties.Select(statement => statement.Statement))
            .Concat(answer.RecommendedNextSteps.Select(statement => statement.Statement));
}
