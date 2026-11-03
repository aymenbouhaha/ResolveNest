# Investigation API

The first read-only REST endpoint over the graph investigation queries described in [graph-investigations.md](graph-investigations.md). It returns one typed, evidence-classified investigation for a support ticket. It contains no AI logic, no ranking and no writes.

Code: `apps/api/ResolveNest.Api/Features/Investigations/`.

## Endpoint

```
GET /api/investigations/tickets/{ticketId}?includeContent=false
```

| Parameter | In | Required | Rules |
|---|---|---|---|
| `ticketId` | path | yes | Canonical ID, exactly `ticket:TCK-<1 to 9 digits>` (case-sensitive). `ticket:TCK-1046` and the URL-encoded `ticket%3ATCK-1046` are both accepted. |
| `includeContent` | query | no | `true` or `false` (case-insensitive), default `false`. Any other value, including an empty one, is a 400. |

There is no authentication yet. The endpoint executes only the three version-controlled queries; request input is passed as Cypher parameters and never becomes query text.

### Git Bash examples (API on :5080)

```bash
curl -s http://localhost:5080/api/investigations/tickets/ticket:TCK-1046
curl -s "http://localhost:5080/api/investigations/tickets/ticket:TCK-1046?includeContent=true"
curl -s http://localhost:5080/api/investigations/tickets/ticket:TCK-1017
curl -si http://localhost:5080/api/investigations/tickets/ticket:TCK-0000
curl -si http://localhost:5080/api/investigations/tickets/TCK-1046
```

## HTTP status behavior

| Status | When | Body |
|---|---|---|
| 200 | The ticket exists | `TicketInvestigationResponse` (below) |
| 400 | `ticketId` is not canonical, or `includeContent` is not a boolean. Validated before Neo4j is called. | `application/problem+json` validation problem with an `errors` map keyed by `ticketId` / `includeContent` |
| 404 | Valid ID, no such ticket (`found: false` from `ticket-evidence.cypher`) | `application/problem+json`, title `Ticket not found` |
| 503 | Any `Neo4jException` (unreachable server, authentication, transaction timeout, query error) | `application/problem+json`, title `Database unavailable`, no detail |
| 500 | The queries returned a shape the mapper does not accept (wrong row count, unknown evidence value, wrong field type, queries disagree on `found`) | `application/problem+json`, title `Investigation failed`, no detail |

Error bodies never contain connection strings, credentials, Cypher text, driver messages or stack traces. The server log records the ticket ID, the exception type and the Neo4j error code; it never logs article content or connection details. The `includeContent` query parameter is bound as a string and parsed by the endpoint, so a malformed value yields the same problem-details 400 in every environment instead of the framework's binding exception.

## How it works

1. The endpoint validates the input and calls `TicketInvestigationService`.
2. The service opens one session in read access mode and runs a single managed read transaction (`ExecuteReadAsync`, 15 s timeout). Inside it, it runs:
   - `ticket-evidence.cypher` first. On `found: false` it stops and returns 404 without running the other two.
   - `ticket-candidates.cypher` with `$ticketId`.
   - `ticket-knowledge.cypher` with `$ticketId` and `$includeContent`.

   One transaction keeps the three reads on one connection with one timeout. It is **not** a snapshot guarantee: Neo4j transactions are read-committed, so a write committed between two of the queries could in principle be seen by the later one. In practice the graph changes only through the manual ingestion commands, and the queries cross-check `found` so a ticket that disappeared mid-request yields a 500, not a mixed answer.

   Write safety comes from the queries themselves, which contain no write clauses (a local test checks this). Read access mode is an extra guard (Neo4j refuses writes in a read transaction on this single-instance setup), not an access-control mechanism; the driver documentation warns against relying on it as one. A read-only database user would be the real control and is not configured yet.
3. Each query must return exactly one row. `GraphResultMap` reads it field by field: required fields must be present and correctly typed, optional fields become `null`, Neo4j temporal values become ISO 8601 `DateTimeOffset`s, and `evidence` must be one of the four known values. Anything else is a 500, never a silently passed-through value.
4. `TicketInvestigationMapper` combines the three rows into typed records and runs `CandidateRelevanceEvaluator` on each candidate bug (see [Candidate relevance](#candidate-relevance)). The candidate list is then re-ordered by relevance. Every other list keeps the query's order; nothing is filtered or deduplicated, because the queries already return deterministic, duplicate-free lists.

### Query loading

The three `.cypher` files are compiled into the API assembly as embedded resources (`ResolveNest.Api.Queries.<file name>`, declared in `ResolveNest.Api.csproj`). The files in `db/neo4j/queries/` stay the single source of truth, no Cypher is duplicated in C#, and the queries are present after `dotnet build` and `dotnet publish` regardless of the working directory. `InvestigationQueryCatalog` is resolved when the endpoints are mapped, so a missing or empty resource fails at startup instead of on the first request. `entity-knowledge.cypher` is not embedded or exposed yet.

### Cancellation

The request's `CancellationToken` is checked before the session opens and before each query, and it is passed to the driver's result streaming (`ToListAsync`). The driver's `RunAsync` takes no token, so a query already executing on the server runs to completion or to the 15 s transaction timeout.

## Response structure

JSON property names are camelCase. All lists are always present (possibly empty). Absent graph properties are `null`. No Neo4j element IDs, labels arrays, driver objects or raw records are exposed; every entity is identified by its canonical `id`.

```
TicketInvestigationResponse
├─ ticket              InvestigatedTicket
├─ customer            ReportingCustomer | null
├─ features[]          TicketFeature
├─ confirmedBugs[]     ConfirmedBug
├─ candidateBugs[]     CandidateBug
├─ relatedTickets[]    RelatedTicket
├─ knowledgeArticles[] KnowledgeArticleEvidence
└─ counts              InvestigationResultCounts
```

| Record | Fields | Source |
|---|---|---|
| `InvestigatedTicket` | `id`, `type`, `subject`, `description`, `status`, `priority`, `reportedVersion`, `reportedVersionNormalized`, `reportedVersionPrecision`, `resolution`, `createdAt`, `resolvedAt`, `unresolvedBugRefs[]`, `sourceSystem`, `sourceId`, `sourceRecord` | A `ticket` |
| `ReportingCustomer` | `id`, `type`, `name`, `plan`, `deployment`, `evidence`, `path`, `sourceSystem`, `sourceId`, `sourceRecord`, `relationshipSourceRecord` | A `customer` |
| `TicketFeature` | `id`, `type`, `name`, `evidence`, `path`, `sourceSystem`, `sourceRecord`, `relationshipSourceRecord` | A `features` |
| `ConfirmedBug` | `id`, `type`, `title`, `sourceStatus`, `severity`, `workaround`, `evidence`, `path`, `confirmedBy`, `confirmedAt`, `sourceSystem`, `sourceId`, `sourceRecord`, `relationshipSourceRecord`, `affectedFeatures[]`, `introducedIn[]`, `fixedIn[]` | A `confirmedBugs` |
| `AffectedFeature` | `id`, `name`, `evidence`, `relationshipSourceRecord` | AFFECTS |
| `ReleaseEvidence` | `id`, `version`, `releasedAt`, `evidence`, `evidenceText`, `relationshipSourceRecord` | INTRODUCED_IN / FIXED_IN |
| `CandidateBug` | `id`, `type`, `title`, `description`, `sourceStatus`, `severity`, `workaround`, `evidence`, `path`, `reason`, `sharedFeatures[]`, `sourceSystem`, `sourceId`, `sourceRecord`, `introducedIn[]`, `fixedIn[]`, `confirmedTicketCount`, `confirmedCustomerCount`, `confirmedTickets[]`, `confirmedTicketLimit`, `confirmedTicketsTruncated`, `relevance` | B `candidateBugs`, plus `relevance` computed by the API |
| `CandidateRelevance` | `tier`, `sharedFeatureIds[]`, `errorCodes`, `version`, `supporting[]`, `limitations[]` | Computed, see [Candidate relevance](#candidate-relevance) |
| `ErrorCodeSignal` | `status`, `matchedCodes[]`, `ticketCodes[]`, `bugCodes[]` | Computed |
| `VersionSignal` | `assessment`, `reportedVersion`, `reportedVersionPrecision`, `introducedInVersions[]`, `fixedInVersions[]`, `introducedCheck`, `fixedCheck` | Computed |
| `RelevanceNote` | `code`, `message` | Computed |
| `SharedFeature` | `featureId`, `featureName`, `relationshipSourceRecord` | B |
| `CandidateConfirmedTicket` | `id`, `type`, `subject`, `status`, `reportedVersion`, `reportedVersionPrecision`, `customerId`, `sameCustomer`, `evidence`, `path`, `confirmedBy`, `confirmedAt`, `sourceSystem`, `sourceRecord`, `relationshipSourceRecord` | B |
| `RelatedTicket` | `id`, `type`, `subject`, `status`, `createdAt`, `reportedVersion`, `reportedVersionPrecision`, `customerId`, `sameCustomer`, `evidence`, `path`, `reason`, `sharedFeatureIds[]`, `confirmedBugIds[]`, `sourceSystem`, `sourceRecord` | B `relatedTickets` |
| `KnowledgeArticleEvidence` | `id`, `type`, `title`, `summary`, `updatedAt`, `sourceSystem`, `sourceId`, `sourceRecord`, `sourcePath`, `reachedVia[]`, `content` | C `articles` |
| `KnowledgeRoute` | `relationship`, `targetId`, `basis`, `evidence`, `relationshipSourceRecord` | C |
| `InvestigationResultCounts` | `features`, `confirmedBugs`, `candidateBugs`, `candidateBugLimit`, `candidateBugLimitReached`, `relatedTickets`, `relatedTicketTotal`, `relatedTicketLimit`, `relatedTicketsTruncated`, `knowledgeArticles`, `knowledgeContentIncluded` | Computed from the rows |

The fields are exactly those the queries return; some records lack `sourceId` (features, related tickets) because the query does not return it, and the API does not add it. `type` is the graph label echoed by the query.

### Ordering

Taken unchanged from the queries, except `candidateBugs`:
- `features`, `confirmedBugs`, `knowledgeArticles`, release lists and `confirmedTickets`: by `id`.
- `candidateBugs`: by the relevance rules in [Ordering rules](#ordering-rules). Before Task 2 this list was in query order (`confirmedTicketCount` descending, then `id`); that is now only the third tie-breaker.
- `relatedTickets`: `createdAt` descending, then `id`.
- `reachedVia`: `relationship`, then `targetId`.

### Limits and truncation

| List | Limit | How truncation is shown |
|---|---|---|
| `candidateBugs` | 25 | `counts.candidateBugLimitReached` is `true` when 25 were returned. The query returns no total, so the API can only say the limit was reached, not whether or how many candidates were cut. The limit applies in query order (by confirmed count), before relevance ordering. |
| `candidateBugs[].confirmedTickets` | 25 per candidate | `confirmedTicketsTruncated` is `confirmedTicketCount > confirmedTickets.length`. |
| `relatedTickets` | 50 | `counts.relatedTicketsTruncated` is `relatedTicketTotal > relatedTickets`. |

The limit values live in `InvestigationQueryLimits` and must match the `LIMIT` clauses in `ticket-candidates.cypher`; the local tests compare them.

## Evidence classification

Every evidence item carries `evidence`, serialized as one of four lowercase strings:

| Value | Meaning in this API |
|---|---|
| `confirmed` | An explicit `CONFIRMED_AS` relationship between the ticket in question and the bug. In `confirmedBugs` it is about the target ticket; in `candidateBugs[].confirmedTickets` it is about **that other ticket**, not the target. |
| `documented` | An explicit graph relationship states the fact: REPORTED, ABOUT_FEATURE, AFFECTS, INTRODUCED_IN, FIXED_IN or COVERS. |
| `candidate` | Reached only through a shared feature (`SupportTicket-[:ABOUT_FEATURE]->Feature<-[:AFFECTS]-Bug`). No causality is established. |
| `context` | Related tickets on a shared feature. They help the investigator but prove nothing about the cause. |

The label belongs to the route, not the record. `kb:KB-012` explicitly documents BUG-231 (ADDRESSES), yet for Acme that route is `candidate`, because BUG-231 is only a candidate for Acme's ticket. For TCK-1017, where BUG-231 is confirmed, the same article route is `confirmed`.

Rules the API keeps:
- A candidate is never promoted to confirmed, and a bug never appears in both `confirmedBugs` and `candidateBugs`.
- A documented `fixedIn` release shows a fix shipped. It does not show that the customer upgraded or that their ticket is resolved; use `ticket.status`, `resolution` and `resolvedAt` for that.
- `reportedVersion` is returned raw, with `reportedVersionNormalized` and `reportedVersionPrecision`. A `minor` precision value such as `"2.3"` is never turned into a patch version.
- No confidence percentages, similarity scores or synthesized source references are produced.

## Candidate relevance

`CandidateRelevanceEvaluator` adds a `relevance` object to every candidate bug. It is deterministic C#, runs on the typed query results, makes no extra database call and no LLM call, and never changes a candidate's `evidence` (always `candidate`), `id`, provenance, counts or release lists. It does not remove candidates. It explains why a candidate is worth looking at; it never states that the bug caused the ticket.

### Signals

| Signal | Source | Values |
|---|---|---|
| Shared feature | `sharedFeatures[]` from the query (ABOUT_FEATURE + AFFECTS) | `sharedFeatureIds[]`, always non-empty for a candidate |
| Error codes | Ticket `subject` + `description`; bug `title` + `description` | `errorCodes.status`: `matched`, `differentCodes`, `unavailable` |
| Version | Ticket `reportedVersion` / precision; documented INTRODUCED_IN / FIXED_IN versions | `version.assessment`: `withinDocumentedRange`, `indeterminate`, `outsideDocumentedRange`, `noReleaseEvidence`, `reportedVersionUnknown` |
| Historical confirmations | Existing `confirmedTicketCount` / `confirmedCustomerCount` (full totals, not truncated) | Described in `supporting` or `limitations`; not duplicated as fields |

#### Error codes

Extraction is two exact regular expressions, chosen after surveying every ticket and bug in the dataset:

- **Product error codes:** `ERR_` followed by uppercase snake case, for example `ERR_EXPORT_SERIALIZE`. Case-sensitive; `err_export_serialize` is not a code.
- **API error codes:** lowercase snake-case identifiers inside single or double quotes, for example `'insufficient_scope'`, `'token_expired'`, `'plan_not_entitled'`. Unquoted snake case is ignored because the dataset uses it for field names (`updated_at`, `account_id`).

Codes are deduplicated and sorted ordinally; matching is exact set intersection. There is no fuzzy matching or token similarity. Words such as "error", "failed" and "timeout" are never codes.

HTTP status numbers (403, 429, 500) are deliberately **not** used. In this dataset a bare number is ambiguous ("500 rows", "page size 500", "500,000 rows"), and 57 tickets mention 403 with very different causes (34 of them resolved as configuration, see [graph-investigations.md](graph-investigations.md#secondary-scenarios-verified)), so it is not diagnostic. Ticket `resolution` and `internalNotes` are not read either: matching uses only what the customer reported.

`unavailable` means at least one side has no recognizable code; `ticketCodes` / `bugCodes` show which. `differentCodes` means both sides have codes and none is shared; it is reported as a limitation.

#### Version assessment

The assessment is explicitly **tentative**. A documented INTRODUCED_IN or FIXED_IN release is a fact; whether the customer's version falls in that range is an inference, and the messages say so.

1. The reported version is normalized with the existing `SourceNormalization.Version` (trim, strip `v`). Only `patch` (`2.3.1`) and `minor` (`2.3`) precision are comparable. Null, `latest` and anything unparseable give `reportedVersionUnknown`.
2. Versions are compared as integer tuples, never as strings, so `2.10.0` is after `2.9.0`. Numbers too large to parse make the bound `indeterminate` instead of throwing.
3. Each documented bound is checked separately (`introducedCheck`, `fixedCheck`):

   | Reported | Against release `x.y.z` | Position |
   |---|---|---|
   | Patch `a.b.c` | tuple comparison | before, or at/after |
   | Minor line `a.b` | `a.b` < `x.y` | before (every `a.b.*` is earlier) |
   | Minor line `a.b` | `a.b` > `x.y` | at/after |
   | Minor line `a.b` | `a.b` = `x.y`, `z` = 0 | at/after (`a.b.0` is the first patch) |
   | Minor line `a.b` | `a.b` = `x.y`, `z` > 0 | straddles (some patches before, some after) |

   INTRODUCED_IN is `consistent` when the report is at/after it, `inconsistent` when before. FIXED_IN is `consistent` when the report is before it, `inconsistent` when at/after. A straddle is `indeterminate`. An empty release list is `notDocumented`; an unusable reported version is `notEvaluated`.
4. Combination: no documented bound → `noReleaseEvidence`; any `inconsistent` → `outsideDocumentedRange`; else any `indeterminate` → `indeterminate`; else `withinDocumentedRange`.

A `minor` report is never treated as a specific patch; it gets the `patchVersionUnknown` limitation. A candidate whose version is `outsideDocumentedRange` is kept (version reports can be wrong and issues can regress) and only ordered later. A documented fix always adds `fixDoesNotProveResolution`.

### Tiers

| Tier | Rule |
|---|---|
| `errorCodeMatch` | `errorCodes.status` is `matched` |
| `versionCompatible` | No code match, version `withinDocumentedRange` or `indeterminate` |
| `featureOnly` | No code match, version `noReleaseEvidence` or `reportedVersionUnknown` |
| `versionConflict` | No code match, version `outsideDocumentedRange` |

An exact shared error code is the strongest diagnostic signal in this dataset, so it takes precedence even when the version conflicts; the conflict stays visible in `limitations`. Tiers are categories, not scores.

### Ordering rules

1. Tier, in the order above.
2. Version: `withinDocumentedRange`, then `indeterminate`, then `noReleaseEvidence` / `reportedVersionUnknown`, then `outsideDocumentedRange`.
3. `confirmedTicketCount` descending.
4. `id`, ordinal.

Every key is a value in the response, so a reader can recompute the order. The same input always gives the same order.

### Notes

`supporting[]` and `limitations[]` hold `{code, message}` pairs. Codes are stable identifiers for the UI or a later AI service; messages are plain English built only from values in the response.

| Code | List | When |
|---|---|---|
| `sharedFeature` | supporting | Always |
| `errorCodeMatch` | supporting | Codes matched |
| `versionWithinDocumentedRange` | supporting | Version within range |
| `versionMayBeWithinDocumentedRange` | supporting | Version indeterminate |
| `historicalConfirmations` | supporting | `confirmedTicketCount` > 0 |
| `differentErrorCodes` | limitations | Both sides cite codes, none shared |
| `noErrorCodeInTicket` / `noErrorCodeInBug` | limitations | No code on that side |
| `versionOutsideDocumentedRange` | limitations | Version outside range |
| `noReleaseEvidence` | limitations | No INTRODUCED_IN or FIXED_IN |
| `reportedVersionUnknown` | limitations | Null, `latest` or unparseable report |
| `patchVersionUnknown` | limitations | Reported precision is `minor` |
| `introductionReleaseNotDocumented` | limitations | Only a fix release is documented |
| `fixDoesNotProveResolution` | limitations | A fix release is documented |
| `noHistoricalConfirmations` | limitations | `confirmedTicketCount` = 0 |
| `ticketHasConfirmedBug` | limitations | The target ticket already has a confirmed bug |
| `ticketClosedWithoutConfirmedBug` | limitations | Ticket `resolved` or `closed` with no confirmed bug; its resolution may point to a non-product cause |

### Example: Acme (verified 2026-11-03)

| Order | Bug | Tier | Error codes | Version (`'2.3'`, minor) | Confirmed elsewhere |
|---|---|---|---|---|---|
| 1 | BUG-231 | `errorCodeMatch` | `ERR_EXPORT_SERIALIZE` in both | `indeterminate`: introduced 2.3.0 consistent, fixed 2.3.2 straddles | 21 tickets / 21 customers |
| 2 | BUG-233 | `versionCompatible` | bug has none | `withinDocumentedRange`: introduced 2.1.0, no fix | 3 / 3 |
| 3 | BUG-205 | `versionCompatible` | bug has none | `withinDocumentedRange`: introduced 2.2.1, no fix | 2 / 2 |
| 4 | BUG-238 | `featureOnly` | bug has none | `noReleaseEvidence` | 6 / 6 |
| 5 | BUG-159 | `versionConflict` | bug has none | `outsideDocumentedRange`: fixed in 2.0.2, before the 2.3 line | 3 / 3 |
| 6 | BUG-261 | `versionConflict` | bug has none | `outsideDocumentedRange`: introduced in 2.4.2, after the 2.3 line | 2 / 2 |

BUG-231's notes: supporting `sharedFeature`, `errorCodeMatch`, `versionMayBeWithinDocumentedRange`, `historicalConfirmations`; limitations `patchVersionUnknown`, `fixDoesNotProveResolution`. It stays `evidence: "candidate"`, `confirmedBugs` stays empty, and KB-012 keeps its COVERS (`documented`) and ADDRESSES BUG-231 (`candidate`) routes. What the API supports saying: Acme's ticket cites the same error code as BUG-231, which affects the same feature and is confirmed for 21 other customers; a fix shipped in 2.3.2, but Acme's patch version is unknown and nothing confirms BUG-231 for this ticket.

BUG-238 has more historical confirmations than BUG-233 but ranks lower, because its timeout symptom shares no error code with Acme's ticket and it has no release evidence. Under the old count-only order it was second.

### Secondary scenarios (verified)

- **403, configuration (`ticket:TCK-1006`):** resolved, resolution `Configuration: token was missing the reports:read scope…`, no confirmed bug. BUG-244 is `errorCodeMatch` on `insufficient_scope` (the bug's text really cites it) but `outsideDocumentedRange` (introduced after 2.1.1). Every candidate carries `ticketClosedWithoutConfirmedBug`. The code match is shown as a reason to look, not as a product defect.
- **Webhook delay (`ticket:TCK-1043`):** no confirmed bug. BUG-247 is `featureOnly`, `confirmedTicketCount` 0, with `noErrorCodeInTicket`, `noReleaseEvidence` and `noHistoricalConfirmations`. The other webhook candidates are `versionConflict`.

## Markdown inclusion

With `includeContent=false` (the default) every `knowledgeArticles[].content` is `null`; the field is still present so the contract does not change shape. With `includeContent=true` it holds the article's full Markdown exactly as stored on the `KnowledgeArticle` node (the Acme response grows from about 64 KB to about 70 KB). Content is returned only for articles reached through the graph routes above.

Article content, ticket descriptions and resolutions are untrusted source text. The API returns them as plain JSON strings, never interprets them, and never logs article content. Consumers (the React UI, a later AI retrieval service) must treat them as data and must not follow instructions found in them.

## Acme scenario (verified 2026-11-03)

`GET /api/investigations/tickets/ticket:TCK-1046` returns 200:

- `ticket`: status `open`, `reportedVersion` `"2.3"`, normalized `2.3`, precision `minor`, `sourceRecord` `data/seed/tickets.json#/records/46`.
- `customer`: `customer:acme` (Acme Corporation), `documented`.
- `features`: `feature:csv-export`, `documented`.
- `confirmedBugs`: empty.
- `candidateBugs`, in relevance order: BUG-231 (21 confirmed tickets from 21 customers), BUG-233 (3), BUG-205 (2), BUG-238 (6), BUG-159 (3), BUG-261 (2), all `candidate`. See [Example: Acme](#example-acme-verified-2026-11-03).
- BUG-231: `introducedIn` `release:2.3.0`, `fixedIn` `release:2.3.2` (2026-08-04, `documented`, with the PR-5120 evidence text). Its 21 `confirmedTickets` (TCK-1017 … TCK-2159) are all `confirmed`, none from Acme, `confirmedTicketsTruncated: false`.
- `relatedTickets`: 50 of 53, `relatedTicketsTruncated: true`.
- `knowledgeArticles`: KB-003, KB-012, KB-014, KB-031, KB-036, KB-039. KB-012 is reached via COVERS csv-export (`documented`) and ADDRESSES BUG-231 (`candidate`).

`ticket:TCK-1017` returns BUG-231 in `confirmedBugs` (`confirmed`, with `confirmedBy`, `confirmedAt` and relationship provenance), not in `candidateBugs`, and KB-012's ADDRESSES route is `confirmed`.

## Local verification

The local-only test project `tests/ResolveNest.Api.InvestigationApiTests/` (excluded through `.git/info/exclude`, not in `ResolveNest.slnx`) hosts the API in memory with `WebApplicationFactory` against the local Neo4j:

```bash
dotnet test tests/ResolveNest.Api.InvestigationApiTests
```

It covers the Acme and TCK-1017 scenarios against independent count queries, 400/404/503 behavior, `includeContent`, evidence values and provenance formats, ordering, duplicates, determinism, the health endpoints, cancellation, embedded queries matching the files on disk, and unchanged graph counts. For relevance it adds evaluator unit tests (error-code extraction, a version table including `2.10` vs `2.9`, minor lines, `latest`, null and overflow, tier precedence, tie-breakers, missing signals) and scenario tests for Acme, TCK-1006 and TCK-1043 that check code matches against the raw graph text and that no candidate is dropped or promoted.

## Known limitations

- **No diagnosis:** relevance tiers explain evidence; they do not establish a cause. A human or a later grounded AI step must still decide.
- **Narrow error-code coverage:** only `ERR_*` codes and quoted lowercase API codes are recognized. HTTP status codes and free-text symptoms ("times out", "0 KB file") are not compared, and a quoted lowercase snake-case word that is not really an error code would be treated as one.
- **Error-code precedence:** a code match ranks first even when the version conflicts (BUG-244 for TCK-1006). The conflict is shown, not used to demote.
- **Version assessment is per bound:** it checks the report against INTRODUCED_IN and FIXED_IN independently. It knows nothing about backports or release branches.
- **Relevance runs after the limit:** the query keeps the 25 candidates with the most confirmations; a weak-history but strongly matching bug beyond that limit would not be seen. The current dataset has at most 15 candidates per ticket, so nothing is cut today.
- **Candidate total unknown:** when 25 candidates are returned, the total beyond the limit is not known.
- **Fixed limits:** 25 candidates, 25 confirmed tickets per candidate, 50 related tickets, defined in Cypher.
- **Cancellation granularity:** a query already running on the server is not interrupted; see [Cancellation](#cancellation).
- **Single customer assumed:** the queries assume at most one REPORTED customer per ticket. If the data ever violated that, the query would return several rows and the API would answer 500.
- **No authentication, no caching, no OpenAPI document** yet.
- **Knowledge retrieval** follows only ADDRESSES and COVERS; `entity-knowledge.cypher` is not exposed.
