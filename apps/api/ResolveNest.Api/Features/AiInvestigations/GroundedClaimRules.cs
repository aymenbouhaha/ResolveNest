using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ResolveNest.Api.Features.AiInvestigations;

public static partial class GroundedClaimRules
{
    public static JsonSerializerOptions StrictSerializerOptions { get; } = CreateStrictOptions();

    public static void Check(IEnumerable<string> texts, AnswerValidationContext context, SortedSet<string> reasons, IReadOnlyList<string>? extraTargetTerms = null)
    {
        var targetPattern = TargetPattern(context, extraTargetTerms ?? []);
        foreach (var sentence in Sentences(texts))
        {
            var mentionsTarget = targetPattern.IsMatch(sentence);

            if (PromotesCandidate(sentence, mentionsTarget, targetPattern, context))
            {
                reasons.Add(AnswerRejectionReasons.CandidatePromotedToConfirmed);
            }

            if (!context.IsTicketClosed && mentionsTarget && ResolvedClaim().IsMatch(sentence) && !ResolutionHedge().IsMatch(sentence))
            {
                reasons.Add(AnswerRejectionReasons.UnsupportedResolutionClaim);
            }

            if (ActionClaim().IsMatch(sentence) && !Negation().IsMatch(sentence))
            {
                reasons.Add(AnswerRejectionReasons.UnsupportedActionClaim);
            }
        }
    }

    public static IEnumerable<string> Sentences(IEnumerable<string> texts) => texts
        .SelectMany(text => SentenceBoundary().Split(text))
        .Where(sentence => !string.IsNullOrWhiteSpace(sentence));

    public static bool IsHedgedCauseStatement(string sentence) => CauseHedge().IsMatch(sentence);

    public static bool ClaimsCause(string sentence) => CauseClaim().IsMatch(sentence);

    public static bool IsNegated(string sentence) => Negation().IsMatch(sentence);

    public static IEnumerable<string> MentionedCandidates(string sentence, AnswerValidationContext context) => context.CandidateBugIds
        .Where(id => !context.ConfirmedBugIds.Contains(id))
        .Where(id => Regex.IsMatch(sentence, $@"(?<![A-Za-z0-9-]){Regex.Escape(DisplayKey(id))}(?![0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    public static string DisplayKey(string canonicalId)
    {
        var separator = canonicalId.IndexOf(':', StringComparison.Ordinal);
        return separator >= 0 ? canonicalId[(separator + 1)..] : canonicalId;
    }

    private static JsonSerializerOptions CreateStrictOptions()
    {
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
        };
        serializerOptions.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return serializerOptions;
    }

    private static bool PromotesCandidate(string sentence, bool mentionsTarget, Regex targetPattern, AnswerValidationContext context)
    {
        if (!MentionedCandidates(sentence, context).Any() || CauseHedge().IsMatch(sentence))
        {
            return false;
        }

        var mentionsOtherTickets = OtherTickets().IsMatch(sentence);
        var claimsCause = CauseClaim().IsMatch(sentence) && (mentionsTarget || !mentionsOtherTickets);
        var claimsTargetConfirmation = mentionsTarget && !mentionsOtherTickets && ConfirmedVerb().IsMatch(sentence);
        var confirmedForTarget = ConfirmedFor().Matches(sentence).Any(match => targetPattern.IsMatch(sentence[match.Index..]));
        return claimsCause || claimsTargetConfirmation || confirmedForTarget;
    }

    private static Regex TargetPattern(AnswerValidationContext context, IReadOnlyList<string> extraTargetTerms)
    {
        var terms = new List<string> { DisplayKey(context.TicketId), "this ticket", "the ticket", "the customer", "customer's", "their issue", "their problem", "their export" };
        terms.AddRange(extraTargetTerms);
        if (!string.IsNullOrWhiteSpace(context.CustomerName))
        {
            terms.Add(context.CustomerName);
            var firstWord = context.CustomerName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            if (firstWord.Length >= 3)
            {
                terms.Add(firstWord);
            }
        }

        var alternation = string.Join('|', terms.Select(Regex.Escape));
        return new Regex($@"\b(?:{alternation})(?![A-Za-z])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [GeneratedRegex(@"(?<=[.!?;])\s+|\r?\n", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBoundary();

    [GeneratedRegex(@"\b(root cause|the cause|caused by|is causing|confirmed cause|responsible for)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CauseClaim();

    [GeneratedRegex(@"\b(is|was|has been|have been)\s+confirmed\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfirmedVerb();

    [GeneratedRegex(@"\bconfirmed\s+(for|against|on|in|as the cause of)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConfirmedFor();

    [GeneratedRegex(@"\b(not|no|never|unconfirmed|cannot|can't|isn't|aren't|wasn't|without|unverified|unclear|uncertain|unknown|may|might|could|possible|possibly|likely|potential|potentially|candidate|suspected|whether|if|until|unless|would)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CauseHedge();

    [GeneratedRegex(@"\b(other|others|another)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OtherTickets();

    [GeneratedRegex(@"\b(is|are|was|were|has been|have been)\s+(now\s+|already\s+)?(resolved|fixed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResolvedClaim();

    [GeneratedRegex(@"\b(not|no|never|unless|if|once|after|when|until|whether|would|should|could|may|might|will|verify|confirm|check|cannot|doesn't|upgrad\w*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ResolutionHedge();

    [GeneratedRegex(@"\b(i|we)\s+(have\s+|already\s+)*(sent|emailed|notified|contacted|replied|saved|scheduled|persisted)\b|\b(email|e-mail|reply|response|draft|message|notification)s?\s+(has\s+been|have\s+been|was|were|is|are)\s+(sent|saved|persisted|delivered|scheduled|emailed)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ActionClaim();

    [GeneratedRegex(@"\b(not|no|never)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Negation();
}
