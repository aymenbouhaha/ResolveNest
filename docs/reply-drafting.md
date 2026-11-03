# Human-reviewed customer reply drafts

Phase 4, Task 3. Support engineers can generate a customer-reply **preview** with Gemini, edit it, and then explicitly **approve** it. Only approval persists anything, and nothing is ever sent to a customer.

Code: `apps/api/ResolveNest.Api/Features/ReplyDrafts/`. It reuses the Task 1 evidence context and the Task 2 Gemini client, projection, envelope and claim rules.

## Workflow

```
1. POST …/reply-drafts/preview      read-only
   investigation ─► evidence context ─► projection ─► Gemini (structured JSON) ─► draft validator
   ◄── draft body + internal claims, uncertainties, limitations, validated sources
       + previewToken (Data Protection, 30 min)          persisted: false

2. Human reviews and optionally edits the text           (client side; nothing stored)

3. POST …/reply-drafts/approve      the only write
   { previewToken, finalText, approved: true }
   token check ─► ticket binding ─► constraint check ─► one MERGE statement in one write transaction
   ◄── 201 approved draft  |  200 identical replay  |  409 conflicting replay
```

- **Preview never writes.** It runs the read-only investigation, calls Gemini once and returns. It never creates a draft record, even when generation succeeds.
- **Approval is never inferred.** It needs a separate request with `approved: true`, a valid token and the final text. It never calls Gemini and never sends anything.
- **Nothing is held server-side between the two steps.** The unapproved preview travels only inside the encrypted, signed preview token. No cache, table or store for pending drafts was added.

## Endpoints

### `POST /api/investigations/tickets/{ticketId}/reply-drafts/preview`

The request body is optional. Without one, the request still works.

```json
{ "reviewerGuidance": "Keep it short; the customer is in finance." }
```

`reviewerGuidance` is optional, at most 500 characters, with no control characters. It is sent to Gemini inside the untrusted data block and may influence tone only. It is never treated as instructions, and it cannot add facts or allowed versions. Unknown body properties return 400. The body is limited to 4 KB.

200 response (`ReplyDraftPreview`):

| Field | Meaning |
|---|---|
| `ticketId`, `previewId` | The target ticket and a random preview ID (GUID) |
| `status` | `drafted` or `insufficientEvidence` (a holding reply that asks for information) |
| `draftBody` | **Customer-facing** plain text, validated (see below) |
| `supportingClaims[]` | **Internal**: each factual statement the draft relies on, with `citationIds` |
| `uncertainties[]` | **Internal**: what remains unconfirmed, with citations where possible |
| `evidenceLimitations[]` | The context limitations from the evidence context (`code`, `message`, `subjectIds`) |
| `validatedSources[]` | Full `CitationSource` entries for every cited ID |
| `provenance` | Provider, model, model version, projection contract, evidence completeness, characters, token counts, `humanReviewRequired: true` |
| `previewToken`, `previewExpiresAt` | The approval reference, valid for 30 minutes |
| `humanReviewRequired`, `persisted` | Always `true` and `false` |

Errors are the same Problem Details as `/ask` (see [gemini-integration.md](gemini-integration.md#errors-problem-details-each-with-a-reason-extension)): 400, 404, 503 (database or `aiNotConfigured`), 429, 504, 502 and 422. A draft that fails validation returns **502 `draftRejected`**, with `validationFailures` listing the reason codes. The draft text and token are not returned.

### `POST /api/investigations/tickets/{ticketId}/reply-drafts/approve`

```json
{
  "previewToken": "CfDJ8…",
  "finalText": "Hello,\n\n…\n\nThe Support Team",
  "approved": true
}
```

Validation (400 with `errors`, before anything else):

- The ticket ID must be canonical. The body must be JSON, at most 64 KB, with no unknown properties. `approved` must be the JSON boolean `true`; a missing value, `false` or `"true"` is rejected.
- `previewToken` is required.
- `finalText` is required: 40 to 4,000 characters after trimming, no control characters other than newline, carriage return and tab. It must contain no citation IDs (`rec:…`, `rel:…`) and no unfilled placeholders (`{{ }}`, `[Customer Name]`, `[insert …]`, `<insert`).

Responses:

| Status | `reason` | When |
|---|---|---|
| 201 | none | Draft created. Body: `{ draft: ApprovedReplyDraft, alreadyApproved: false }` |
| 200 | none | Same preview approved again with the same final text (idempotent replay). `alreadyApproved: true`, returning the stored record unchanged. |
| 400 | `invalidPreviewToken` | Tampered, foreign, garbage or wrong-version token |
| 400 | `previewTokenExpired` | Token older than 30 minutes |
| 400 | `previewTicketMismatch` | Token issued for another ticket |
| 404 | none | Ticket no longer exists (nothing written) |
| 409 | `replyDraftConflict` | This preview was already approved with **different** text |
| 503 | `replyDraftStorageNotInitialized` | The `reply_draft_id_unique` constraint is missing |
| 503 | none | Neo4j failure (title `Database unavailable`, nothing committed) |

`ApprovedReplyDraft` has these fields:

- `draftId`, `ticketId`, `status` (`approved`)
- `approvedText`, `generatedText`, `editedByHuman`
- `generatedTextValidated` (always `true`), `approvedTextValidated`
- `citationIds`, `limitationCodes`
- `model`, `modelVersion`, `evidenceContractVersion`
- `generatedAt`, `approvedAt`
- `approvalBasis` (`unauthenticatedLocalRequest`), `customerDelivery` (`notSent`)

## Gemini usage

Same provider, model and configuration as `/ask` (`gemini-3.5-flash-lite`, key `Gemini:ApiKey` from User Secrets). Each preview makes one request. There are no tools, no retries and no fallback.

- **Evidence:** the Task 2 projection (`EvidenceProjector`, about 18 KB for Acme), wrapped in the same single-line JSON block between `INVESTIGATION_DATA` markers.
- **System instruction:** `ReplyDraftPrompt.SystemInstruction` is constant. It covers professional, empathetic plain text; acknowledging the problem; candidates only as "may be related"; a documented fix release named for the known issue but not promised as the customer's fix; never saying the issue is resolved while the ticket is open; versions only from the evidence; asking for the exact version when the reported precision is minor; no internal IDs or metadata; no commitments, deadlines, compensation or claimed actions; and `insufficientEvidence` holding replies.
- **Response schema:** `status`, `draftBody`, `supportingClaims[]` and `uncertainties[]`. Citation IDs are restricted by `enum` to the IDs that were supplied.

## Draft validation (AI text only)

`ReplyDraftValidator` checks the generated draft before it is returned. Any failure rejects the whole draft (502 `draftRejected`):

| Check | Code |
|---|---|
| Strict JSON (no unknown properties, all fields required, string enums) | `invalidJson` |
| Body of 40 to 4,000 characters; at most 10 claims and 8 uncertainties; valid statements | `schemaViolation` |
| Every cited ID was supplied | `invalidCitation` |
| Every supporting claim is cited | `missingCitation` |
| `drafted` has claims; `insufficientEvidence` has uncertainties | `inconsistentStatus` |
| Shared claim rules on the body, claims and uncertainties, with "you" and "your" also counted as the customer: no candidate presented as the cause, no "your issue is resolved" while the ticket is open, no "email sent" or "draft saved" | `candidatePromotedToConfirmed`, `unsupportedResolutionClaim`, `unsupportedActionClaim` |
| Body only: no bug keys, no other tickets' keys (the customer's own ticket is allowed), no citation or canonical IDs, no relationship or tier names, no "candidate bug", no agent names. Public `KB-` numbers are allowed. | `internalMetadataLeak` |
| Body only: every version number appears somewhere in the supplied evidence | `unsupportedVersion` |
| Body only: no guarantees, refunds, credits, compensation, "we will fix/release/…", "within N hours", "by Friday" (allowed when stated as "cannot"/"unable") | `unsupportedCommitment` |
| Body only: no "we have escalated/applied/upgraded/patched/contacted…" | `unsupportedActionClaim` |
| Body only, when no bug is confirmed: no unhedged cause statement ("caused by", "the root cause") unless it is investigative wording ("we are investigating whether …") | `candidatePromotedToConfirmed` |

The internal `supportingClaims` and `uncertainties` may name bug IDs. They are for the reviewer and are never inserted into the customer text.

## Human editing

- The reviewer may change the text freely before approving. The server normalizes it by converting CRLF to LF and trimming, then stores it exactly.
- `generatedText` (from the token) and `approvedText` are both stored. `editedByHuman` compares them after normalization.
- `generatedTextValidated: true` means the AI text passed the validator at preview time. `approvedTextValidated` is `true` only when the approved text is identical to that validated text.
- **Edited text is not re-validated.** Approval never calls Gemini or the AI validator, so an edited draft is recorded with `approvedTextValidated: false`. The only checks on human text are the request rules above (length, control characters, citation IDs, placeholders). Treat edited content as the reviewer's responsibility.

## Preview token

- The token comes from ASP.NET Core Data Protection (`IDataProtector`, purpose `ResolveNest.ReplyDrafts.PreviewToken.v1`). It is authenticated encryption, so the client cannot read or modify it. No new package or store was added.
- **Payload:** version, preview ID, ticket ID, the full generated text, the validated citation IDs, limitation codes, model and model version, projection contract version, `generatedAt` and `expiresAt`. Binding to the ticket and to the exact generated preview comes from that payload.
- **Expiry:** checked by the server against `TimeProvider`: 30 minutes, `ReplyPreviewTokens.Lifetime`. Expired and invalid tokens are told apart, and the clock is injectable for tests.
- **Keys:** in Development, Data Protection keys live in the default per-user key ring (on Windows, `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`, DPAPI-protected). Tokens survive an API restart, but not a key-ring reset or another machine. A multi-instance deployment would need a shared key ring.
- **The token is not authentication.** Anyone holding a valid token can approve that one preview within 30 minutes. It proves only that this server generated that preview, not who approves it.

## Approved draft storage

The graph extension is one label and one relationship:

```
(SupportTicket)-[:HAS_REPLY_DRAFT]->(ReplyDraft)
```

| `ReplyDraft` property | Value |
|---|---|
| `id` | `reply-draft:<previewId>` (stable, unique, derived from the token) |
| `ticketId`, `status` | The target ticket; `approved` |
| `approvedText`, `approvedTextSha256` | Final human-approved text and its SHA-256 (hex) |
| `generatedText`, `editedByHuman` | Original AI text from the token; whether they differ |
| `generatedTextValidated`, `approvedTextValidated` | See [Human editing](#human-editing) |
| `citationIds`, `limitationCodes` | Validated citations and context limitation codes at preview time |
| `model`, `modelVersion`, `evidenceContractVersion` | Generation provenance |
| `generatedAt`, `approvedAt`, `createdAt`, `ingestedAt` | Neo4j `datetime` values |
| `approvalBasis` | `unauthenticatedLocalRequest`. No approver identity is recorded, because none is known. |
| `customerDelivery` | `notSent` |
| `ingestedBy`, `sourceSystem`, `sourceId`, `sourceRecord` | `reply-draft-approval`, `resolvenest-api`, the draft ID, `POST /api/investigations/tickets/{ticketId}/reply-drafts/approve` |

`HAS_REPLY_DRAFT` carries `ingestedBy`, `sourceSystem`, `sourceRecord` and `createdAt`.

- **Existing evidence is untouched:** the write only `MATCH`es the ticket. It never sets ticket, bug or other properties. A rolled-back test compares the ticket's properties before and after.
- **Ingestion compatibility:** the seed and knowledge preflights inspect only their own labels and relationship types (see [ingestion.md](ingestion.md#ownership-markers-and-preflight)). `ReplyDraft` and `HAS_REPLY_DRAFT` are not among them, so ingestion counts these as "other nodes, left untouched". Ingestion's constraint check looks for its six required constraints, so a seventh does not affect it.
- **Investigation queries are unaffected:** they traverse only named relationship types, never `HAS_REPLY_DRAFT`.

### Schema change

`db/neo4j/schema.cypher` gains:

```cypher
CREATE CONSTRAINT reply_draft_id_unique IF NOT EXISTS
FOR (n:ReplyDraft) REQUIRE n.id IS UNIQUE;
```

The constraint was applied to the local database on 2026-11-03. `SHOW CONSTRAINTS` lists `reply_draft_id_unique` with type `NODE_PROPERTY_UNIQUENESS` alongside the original six. Graph counts were unchanged (458 nodes, 1,060 relationships, 0 `ReplyDraft`), and ingestion tests still pass (41/41). On any other database, approval returns `503 replyDraftStorageNotInitialized` until the constraint is applied. Apply the whole file; it is idempotent, and existing constraints are no-ops:

```bash
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}"' < db/neo4j/schema.cypher
```

Before each approval, the API checks with a read-only `SHOW CONSTRAINTS` that the constraint exists, has type `NODE_PROPERTY_UNIQUENESS` and covers `ReplyDraft.id`. Once found, the result is cached for the process.

### Transaction and idempotency rules

- Exactly one parameterized statement runs in one managed write transaction (`ExecuteWriteAsync`, 15 s timeout):

  ```
  MATCH ticket → MERGE (draft:ReplyDraft {id}) ON CREATE SET … → MERGE (ticket)-[:HAS_REPLY_DRAFT]->(draft) ON CREATE SET …
  ```

- If the ticket is missing, `MATCH` yields no row and nothing is created (404).
- **Created vs replay:** the result counter `NodesCreated == 1` means created. Otherwise the stored `approvedTextSha256` is compared. Equal means an idempotent replay (200, stored record returned unchanged). Different means a conflict (409, nothing changed).
- **Concurrency:** the uniqueness constraint makes concurrent `MERGE`s on the same ID safe, so at most one node exists per preview. That is why approval refuses to run without the constraint.
- **Failure:** any failure inside the transaction rolls back the node and the relationship together. A rolled-back test throws after the write and confirms nothing remains.
- No Cypher comes from the model. No `DELETE`. No writes during startup or preview.

## No-email policy

No code path sends email or any other message. There is no SMTP, mail SDK or webhook client. `customerDelivery` is always `notSent`. The prompts and validators reject drafts that claim a message was sent. Delivering an approved reply is a manual action outside ResolveNest.

## Verification

### Commands (Git Bash)

```bash
dotnet build ResolveNest.slnx
dotnet publish apps/api/ResolveNest.Api -c Release -o "$TEMP/resolvenest-publish"
dotnet test tests/ResolveNest.Api.InvestigationApiTests --filter "FullyQualifiedName~ReplyDraft"
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --urls http://localhost:5080
```

```bash
curl -s -X POST http://localhost:5080/api/investigations/tickets/ticket:TCK-1046/reply-drafts/preview -H "Content-Type: application/json" -d '{}'
```

```bash
curl -s -X POST http://localhost:5080/api/investigations/tickets/ticket:TCK-1046/reply-drafts/approve -H "Content-Type: application/json" -d '{"previewToken":"<token>","finalText":"<reviewed text>","approved":true}'
```

The approve command writes a real `ReplyDraft` to your graph once the constraint is applied.

### Mocked and rolled-back tests (2026-11-03)

Local only, in `tests/ResolveNest.Api.InvestigationApiTests/`. Gemini is faked, and the endpoint tests use an in-memory `IReplyDraftStore` and a controllable clock.

- **`ReplyDraftUnitTests`** (no database):
  - safe wording accepted, and 13 unsafe sentences rejected with the right codes;
  - structural rejections and a holding reply;
  - version extraction, including dates and IP addresses that must not count as versions;
  - token round trip, tampering, foreign key, wrong version and expiry;
  - text normalization and hashing.
- **`ReplyDraftEndpointTests`** (Neo4j reads, fake Gemini and fake store):
  - Acme preview: validated sources and limitations, with zero store calls;
  - reviewer guidance kept out of the system instruction, and an injected cause or refund draft rejected;
  - nine unsafe drafts rejected with no token or body returned;
  - invented and uncited claims rejected;
  - TCK-1006 configuration draft accepted;
  - input errors, the unknown ticket and the extra-property body make no Gemini call; a preview without a body is accepted;
  - a 429 is sanitized;
  - an unedited approval stores exactly one draft with no Gemini call, and an edited approval stores the exact normalized text with `approvedTextValidated: false`;
  - an identical replay returns 200, a conflicting replay returns 409, and five parallel replays still leave one draft;
  - nine invalid approval bodies are rejected, including a missing or `false` `approved`;
  - tampered, foreign, garbage, mismatched and expired tokens are rejected;
  - storage failures and the missing constraint are sanitized, and the 404 path is covered;
  - graph counts are unchanged.
- **`ReplyDraftStoreTests`** (real Neo4j, every write rolled back):
  - one node and one relationship are created with the expected provenance, and the ticket's properties are unchanged;
  - an identical replay returns `AlreadyApproved`, a different text returns `Conflict`, and only one node and link exist;
  - an unknown ticket creates nothing;
  - a failure after the write leaves no partial record;
  - the constraint-check query recognizes the type Neo4j actually reports (`NODE_PROPERTY_UNIQUENESS`);
  - the write query is constant and parameterized, and the schema file contains the new constraint.

| Check | Result |
|---|---|
| `ResolveNest.Api.InvestigationApiTests` | 271/271 |
| `IngestionTests` | 41/41 |
| `Graph.InvestigationTests` | 18/18 |
| `Api.Tests` | 1/1 |
| `dotnet build`, `dotnet publish` | pass |
| `npm run build`, `npm run lint` | pass |
| `git diff --check` | clean |

### Live Gemini previews (2026-11-03)

Graph before and after: **458 nodes, 1,060 relationships, 0 `ReplyDraft`**. The key was read only from User Secrets and was not found in the API logs.

| Call | Result |
|---|---|
| TCK-1046, first prompt version | 200 `drafted`, 6.5 s, 6,133 / 479 tokens. Cautious and correct, but it did not mention the 2.3.2 fix. Prompt rule 7 was then tightened. |
| TCK-1046, final prompt | 200 `drafted`, 6.9 s, 6,170 / 640 tokens |
| TCK-1043 | 200 `drafted`, 1.9 s, 5,560 / 330 tokens |

- **Final Acme draft:**
  - It acknowledges the ERR_EXPORT_SERIALIZE failure and 0 KB file on the *Finance Close - Accruals* report after the 2.3 upgrade.
  - It says the behavior "may be related to a known issue documented in KB-012" involving custom date fields, and that "a fix for that known issue is included in version 2.3.2".
  - It asks the customer to confirm the exact installed patch version, and offers the KB-012 XLSX workaround.
  - It contains no bug ID and no resolution claim. Validated sources: `rec:bug:BUG-231`, `rec:kb:KB-012`, `rec:release:2.3.2`, `rec:ticket:TCK-1046`, `rel:bug:BUG-231>FIXED_IN>release:2.3.2`. Internal uncertainties: the patch is unknown, and BUG-231 is unconfirmed as the cause.
- **TCK-1043:** a cautious acknowledgement that points to KB-018 and claims no cause. Two weaknesses: it is `drafted` rather than `insufficientEvidence`, and it signs as "ResolveNest support" (the tool's name) instead of using the product context.
- **Live approval was not run.** The constraint has since been applied, but calling approve against the shared database was not permitted in this session, and no real draft has been persisted. Approval is verified only by the mocked and rolled-back tests above.

## Security limitations

- **No authentication or authorization.** Anyone who can reach the API can generate previews (using the Gemini quota) and approve drafts. The preview token is an anti-tampering and binding mechanism, **not** an identity. `approvalBasis` is honestly `unauthenticatedLocalRequest`. Production use needs authentication, per-ticket authorization, audit identity and rate limiting.
- **Token as a bearer capability:** a leaked token allows one approval within 30 minutes. Tokens carry the generated text in encrypted form. Do not log them.
- **Edited text is unvalidated:** the server stores what the human wrote, beyond the basic artifact checks.
- **Heuristic validators:** the leak, commitment, version and cause rules are English-only patterns. Paraphrases can slip through, and the human review step is the backstop.
- **Data to Google:** previews send synthetic ticket, bug and article text to Gemini's free tier, which may be used for product improvement.
- **Local key ring:** Data Protection keys are per-user, on this machine.

## Known limitations

- There is no endpoint to list or read approved drafts, and no way to revoke or supersede one. Each new preview creates a new draft ID.
- `insufficientEvidence` depends on the model's judgment. TCK-1043 produced a cautious `drafted` reply instead.
- Drafts are English only, with no subject line, template or localization.
