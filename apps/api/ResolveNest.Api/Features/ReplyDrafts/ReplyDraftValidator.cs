using System.Text.Json;
using System.Text.RegularExpressions;
using ResolveNest.Api.Features.AiInvestigations;

namespace ResolveNest.Api.Features.ReplyDrafts;

public static class ReplyDraftRejectionReasons
{
    public const string InternalMetadataLeak = "internalMetadataLeak";
    public const string UnsupportedCommitment = "unsupportedCommitment";
    public const string UnsupportedVersion = "unsupportedVersion";
}

public sealed record ReplyDraftValidationContext(AnswerValidationContext Evidence, IReadOnlySet<string> KnownVersions)
{
    public static ReplyDraftValidationContext From(AnswerValidationContext evidence, string evidencePayload) =>
        new(evidence, ReplyDraftValidator.ExtractVersions(evidencePayload));
}

public static partial class ReplyDraftValidator
{
    public const int MinDraftCharacters = 40;
    public const int MaxDraftCharacters = 4_000;

    private static readonly string[] CustomerTargetTerms = ["you", "your"];

    public static ModelReplyDraft Validate(string modelText, ReplyDraftValidationContext context)
    {
        var draft = Parse(modelText);
        var reasons = new SortedSet<string>(StringComparer.Ordinal);

        CheckShape(draft, reasons);
        if (reasons.Count > 0)
        {
            throw new AiAnswerRejectedException(reasons.ToList());
        }

        CheckCitations(draft, context.Evidence, reasons);
        CheckStatus(draft, reasons);
        GroundedClaimRules.Check(
            new[] { draft.DraftBody }.Concat(draft.SupportingClaims.Select(claim => claim.Statement)).Concat(draft.Uncertainties.Select(item => item.Statement)),
            context.Evidence,
            reasons,
            CustomerTargetTerms);
        CheckCustomerText(draft.DraftBody, context, reasons);

        if (reasons.Count > 0)
        {
            throw new AiAnswerRejectedException(reasons.ToList());
        }

        return draft;
    }

    public static IReadOnlySet<string> ExtractVersions(string text) =>
        VersionNumber().Matches(text).Select(match => match.Groups["version"].Value).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> FindInternalMetadata(string text, string targetTicketId)
    {
        var targetKey = GroundedClaimRules.DisplayKey(targetTicketId);
        var found = new List<string>();
        found.AddRange(BugKey().Matches(text).Select(match => match.Value));
        found.AddRange(TicketKey().Matches(text).Select(match => match.Value).Where(key => key != targetKey));
        found.AddRange(CitationId().Matches(text).Select(match => match.Value));
        found.AddRange(CanonicalId().Matches(text).Select(match => match.Value));
        found.AddRange(InternalTerm().Matches(text).Select(match => match.Value));
        return found;
    }

    private static ModelReplyDraft Parse(string modelText)
    {
        try
        {
            return JsonSerializer.Deserialize<ModelReplyDraft>(modelText, GroundedClaimRules.StrictSerializerOptions)
                ?? throw new AiAnswerRejectedException([AnswerRejectionReasons.InvalidJson]);
        }
        catch (JsonException)
        {
            throw new AiAnswerRejectedException([AnswerRejectionReasons.InvalidJson]);
        }
    }

    private static void CheckShape(ModelReplyDraft draft, SortedSet<string> reasons)
    {
        var body = draft.DraftBody;
        var withinLimits = !string.IsNullOrWhiteSpace(body)
            && body.Trim().Length >= MinDraftCharacters
            && body.Length <= MaxDraftCharacters
            && Enum.IsDefined(draft.Status)
            && draft.SupportingClaims.Count <= ReplyDraftPrompt.MaxSupportingClaims
            && draft.Uncertainties.Count <= ReplyDraftPrompt.MaxUncertainties
            && draft.SupportingClaims.Concat(draft.Uncertainties).All(item => item is not null && AiAnswerValidator.IsValidStatement(item.Statement, item.CitationIds));
        if (!withinLimits)
        {
            reasons.Add(AnswerRejectionReasons.SchemaViolation);
        }
    }

    private static void CheckCitations(ModelReplyDraft draft, AnswerValidationContext context, SortedSet<string> reasons)
    {
        if (draft.SupportingClaims.Concat(draft.Uncertainties).SelectMany(item => item.CitationIds).Any(id => !context.IsKnownCitation(id)))
        {
            reasons.Add(AnswerRejectionReasons.InvalidCitation);
        }

        if (draft.SupportingClaims.Any(claim => claim.CitationIds.Count == 0))
        {
            reasons.Add(AnswerRejectionReasons.MissingCitation);
        }
    }

    private static void CheckStatus(ModelReplyDraft draft, SortedSet<string> reasons)
    {
        var consistent = draft.Status switch
        {
            ReplyDraftStatus.Drafted => draft.SupportingClaims.Count > 0,
            _ => draft.Uncertainties.Count > 0,
        };
        if (!consistent)
        {
            reasons.Add(AnswerRejectionReasons.InconsistentStatus);
        }
    }

    private static void CheckCustomerText(string body, ReplyDraftValidationContext context, SortedSet<string> reasons)
    {
        if (FindInternalMetadata(body, context.Evidence.TicketId).Count > 0)
        {
            reasons.Add(ReplyDraftRejectionReasons.InternalMetadataLeak);
        }

        if (ExtractVersions(body).Any(version => !context.KnownVersions.Contains(version)))
        {
            reasons.Add(ReplyDraftRejectionReasons.UnsupportedVersion);
        }

        foreach (var sentence in GroundedClaimRules.Sentences([body]))
        {
            if (Commitment().IsMatch(sentence) && !GroundedClaimRules.IsNegated(sentence) && !Inability().IsMatch(sentence))
            {
                reasons.Add(ReplyDraftRejectionReasons.UnsupportedCommitment);
            }

            if (PerformedAction().IsMatch(sentence) && !GroundedClaimRules.IsNegated(sentence))
            {
                reasons.Add(AnswerRejectionReasons.UnsupportedActionClaim);
            }

            if (context.Evidence.ConfirmedBugIds.Count == 0
                && GroundedClaimRules.ClaimsCause(sentence)
                && !GroundedClaimRules.IsHedgedCauseStatement(sentence)
                && !InvestigativeWording().IsMatch(sentence))
            {
                reasons.Add(AnswerRejectionReasons.CandidatePromotedToConfirmed);
            }
        }
    }

    [GeneratedRegex(@"(?<![\w.])v?(?<version>\d+\.\d+(?:\.\d+)?)(?!\.?\d)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionNumber();

    [GeneratedRegex(@"\bBUG-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BugKey();

    [GeneratedRegex(@"\bTCK-\d+\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TicketKey();

    [GeneratedRegex(@"\b(rec|rel):\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CitationId();

    [GeneratedRegex(@"\b(bug|ticket|customer|feature|release|kb):[A-Za-z0-9]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalId();

    [GeneratedRegex(@"\b(CONFIRMED_AS|ADDRESSES|AFFECTS|INTRODUCED_IN|FIXED_IN|ABOUT_FEATURE|REPORTED|COVERS|HAS_REPLY_DRAFT|errorCodeMatch\w*|versionCompatible|featureOnly|versionConflict|allowedCitationIds)\b|\bagent\.[a-z0-9.]+|\bcandidate bugs?\b|\bevidence context\b", RegexOptions.CultureInvariant)]
    private static partial Regex InternalTerm();

    [GeneratedRegex(@"\b(guarantee[ds]?|refunds?|compensat\w*|service credits?|discounts?)\b|\bwe\s*(will|'ll|shall)\s+(fix|patch|resolve|release|deploy|ship|refund|credit|compensate|upgrade)\b|\bwithin\s+\d+\s+(minutes?|hours?|days?|business days?|weeks?)\b|\bby\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday|tomorrow|end of (the )?(day|week))\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Commitment();

    [GeneratedRegex(@"\b(cannot|can't|unable)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Inability();

    [GeneratedRegex(@"\b(we|i)(?:'ve|\s+have)\s+(already\s+)?(escalated|applied|deployed|upgraded|patched|fixed|restarted|refunded|credited|scheduled|contacted|notified|resolved)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PerformedAction();

    [GeneratedRegex(@"\b(investigat\w*|determin\w*|identif\w*|confirm|understand|find|looking into)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InvestigativeWording();
}
