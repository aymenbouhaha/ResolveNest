using Neo4j.Driver;

namespace ResolveNest.Api.Features.ReplyDrafts;

public enum ReplyDraftSaveOutcome
{
    Created,
    AlreadyApproved,
    Conflict,
    TicketNotFound,
}

public sealed record ReplyDraftSaveResult(ReplyDraftSaveOutcome Outcome, ApprovedReplyDraft? Draft);

public sealed class ReplyDraftStorageNotInitializedException()
    : Exception("The ReplyDraft uniqueness constraint is missing. Apply db/neo4j/schema.cypher.");

public interface IReplyDraftStore
{
    Task<ReplyDraftSaveResult> SaveAsync(ApprovedReplyDraft draft, string approvedTextSha256, CancellationToken cancellationToken);
}

public static class ReplyDraftGraph
{
    public const string Label = "ReplyDraft";
    public const string Relationship = "HAS_REPLY_DRAFT";
    public const string Owner = "reply-draft-approval";
    public const string SourceSystem = "resolvenest-api";
    public const string ConstraintName = "reply_draft_id_unique";
    public const string ApprovedStatus = "approved";

    public const string HasConstraint = """
        SHOW CONSTRAINTS YIELD name, labelsOrTypes, properties, type
        WHERE name = $constraintName AND labelsOrTypes = ['ReplyDraft'] AND properties = ['id'] AND type IN ['NODE_PROPERTY_UNIQUENESS', 'UNIQUENESS']
        RETURN count(*) AS matching
        """;

    public const string SaveApprovedDraft = """
        MATCH (ticket:SupportTicket {id: $ticketId})
        MERGE (draft:ReplyDraft {id: $draftId})
          ON CREATE SET draft += $properties,
                        draft.generatedAt = datetime($generatedAt),
                        draft.approvedAt = datetime($approvedAt),
                        draft.createdAt = datetime($approvedAt),
                        draft.ingestedAt = datetime($approvedAt)
        WITH ticket, draft
        MERGE (ticket)-[link:HAS_REPLY_DRAFT]->(draft)
          ON CREATE SET link.ingestedBy = $owner,
                        link.sourceSystem = $sourceSystem,
                        link.sourceRecord = $sourceRecord,
                        link.createdAt = datetime($approvedAt)
        RETURN draft.ticketId AS ticketId,
               draft.approvedText AS approvedText,
               draft.approvedTextSha256 AS approvedTextSha256,
               draft.generatedText AS generatedText,
               draft.editedByHuman AS editedByHuman,
               draft.generatedTextValidated AS generatedTextValidated,
               draft.approvedTextValidated AS approvedTextValidated,
               draft.citationIds AS citationIds,
               draft.limitationCodes AS limitationCodes,
               draft.model AS model,
               draft.modelVersion AS modelVersion,
               draft.evidenceContractVersion AS evidenceContractVersion,
               toString(draft.generatedAt) AS generatedAt,
               toString(draft.approvedAt) AS approvedAt,
               draft.approvalBasis AS approvalBasis,
               draft.customerDelivery AS customerDelivery,
               draft.status AS status
        """;

    public static Dictionary<string, object?> Parameters(ApprovedReplyDraft draft, string approvedTextSha256, string sourceRecord) => new()
    {
        ["ticketId"] = draft.TicketId,
        ["draftId"] = draft.DraftId,
        ["generatedAt"] = draft.GeneratedAt.ToString("O"),
        ["approvedAt"] = draft.ApprovedAt.ToString("O"),
        ["owner"] = Owner,
        ["sourceSystem"] = SourceSystem,
        ["sourceRecord"] = sourceRecord,
        ["properties"] = new Dictionary<string, object?>
        {
            ["ticketId"] = draft.TicketId,
            ["status"] = draft.Status,
            ["approvedText"] = draft.ApprovedText,
            ["approvedTextSha256"] = approvedTextSha256,
            ["generatedText"] = draft.GeneratedText,
            ["editedByHuman"] = draft.EditedByHuman,
            ["generatedTextValidated"] = draft.GeneratedTextValidated,
            ["approvedTextValidated"] = draft.ApprovedTextValidated,
            ["citationIds"] = draft.CitationIds.ToList(),
            ["limitationCodes"] = draft.LimitationCodes.ToList(),
            ["model"] = draft.Model,
            ["modelVersion"] = draft.ModelVersion,
            ["evidenceContractVersion"] = draft.EvidenceContractVersion,
            ["approvalBasis"] = draft.ApprovalBasis,
            ["customerDelivery"] = draft.CustomerDelivery,
            ["ingestedBy"] = Owner,
            ["sourceSystem"] = SourceSystem,
            ["sourceId"] = draft.DraftId,
            ["sourceRecord"] = sourceRecord,
        },
    };
}

public sealed class Neo4jReplyDraftStore(IDriver driver) : IReplyDraftStore
{
    private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(15);
    private volatile bool constraintVerified;

    public async Task<ReplyDraftSaveResult> SaveAsync(ApprovedReplyDraft draft, string approvedTextSha256, CancellationToken cancellationToken)
    {
        await EnsureConstraintAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var session = driver.AsyncSession(config => config.WithDefaultAccessMode(AccessMode.Write));
        return await session.ExecuteWriteAsync(
            transaction => SaveInTransactionAsync(transaction, draft, approvedTextSha256),
            config => config.WithTimeout(TransactionTimeout));
    }

    public static async Task<ReplyDraftSaveResult> SaveInTransactionAsync(IAsyncQueryRunner transaction, ApprovedReplyDraft draft, string approvedTextSha256)
    {
        var sourceRecord = $"POST /api/investigations/tickets/{draft.TicketId}/reply-drafts/approve";
        var cursor = await transaction.RunAsync(ReplyDraftGraph.SaveApprovedDraft, ReplyDraftGraph.Parameters(draft, approvedTextSha256, sourceRecord));
        var records = await cursor.ToListAsync();
        var summary = await cursor.ConsumeAsync();
        if (records.Count == 0)
        {
            return new ReplyDraftSaveResult(ReplyDraftSaveOutcome.TicketNotFound, null);
        }

        var record = records[0];
        if (summary.Counters.NodesCreated == 1)
        {
            return new ReplyDraftSaveResult(ReplyDraftSaveOutcome.Created, draft);
        }

        var stored = Read(record, draft.DraftId);
        return record["ticketId"].As<string?>() == draft.TicketId && record["approvedTextSha256"].As<string?>() == approvedTextSha256
            ? new ReplyDraftSaveResult(ReplyDraftSaveOutcome.AlreadyApproved, stored)
            : new ReplyDraftSaveResult(ReplyDraftSaveOutcome.Conflict, null);
    }

    private async Task EnsureConstraintAsync(CancellationToken cancellationToken)
    {
        if (constraintVerified)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await using var session = driver.AsyncSession(config => config.WithDefaultAccessMode(AccessMode.Read));
        var matching = await session.ExecuteReadAsync(async transaction =>
        {
            var cursor = await transaction.RunAsync(ReplyDraftGraph.HasConstraint, new Dictionary<string, object?> { ["constraintName"] = ReplyDraftGraph.ConstraintName });
            var record = await cursor.SingleAsync();
            return record["matching"].As<long>();
        });

        if (matching != 1)
        {
            throw new ReplyDraftStorageNotInitializedException();
        }

        constraintVerified = true;
    }

    private static ApprovedReplyDraft Read(IRecord record, string draftId) => new(
        draftId,
        record["ticketId"].As<string>(),
        record["status"].As<string>(),
        record["approvedText"].As<string>(),
        record["generatedText"].As<string>(),
        record["editedByHuman"].As<bool>(),
        record["generatedTextValidated"].As<bool>(),
        record["approvedTextValidated"].As<bool>(),
        record["citationIds"].As<List<string>>(),
        record["limitationCodes"].As<List<string>>(),
        record["model"].As<string>(),
        record["modelVersion"].As<string?>(),
        record["evidenceContractVersion"].As<string>(),
        DateTimeOffset.Parse(record["generatedAt"].As<string>(), System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse(record["approvedAt"].As<string>(), System.Globalization.CultureInfo.InvariantCulture),
        record["approvalBasis"].As<string>(),
        record["customerDelivery"].As<string>());
}
