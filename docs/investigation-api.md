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

   One transaction gives the three results a consistent snapshot. Read mode makes Neo4j reject any write.
3. Each query must return exactly one row. `GraphResultMap` reads it field by field: required fields must be present and correctly typed, optional fields become `null`, Neo4j temporal values become ISO 8601 `DateTimeOffset`s, and `evidence` must be one of the four known values. Anything else is a 500, never a silently passed-through value.
4. `TicketInvestigationMapper` combines the three rows into typed records. It does not filter, reorder, rank or deduplicate; the queries already return deterministic, duplicate-free lists, and the API keeps their order.

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
| `CandidateBug` | `id`, `type`, `title`, `sourceStatus`, `severity`, `workaround`, `evidence`, `path`, `reason`, `sharedFeatures[]`, `sourceSystem`, `sourceId`, `sourceRecord`, `introducedIn[]`, `fixedIn[]`, `confirmedTicketCount`, `confirmedCustomerCount`, `confirmedTickets[]`, `confirmedTicketLimit`, `confirmedTicketsTruncated` | B `candidateBugs` |
| `SharedFeature` | `featureId`, `featureName`, `relationshipSourceRecord` | B |
| `CandidateConfirmedTicket` | `id`, `type`, `subject`, `status`, `reportedVersion`, `reportedVersionPrecision`, `customerId`, `sameCustomer`, `evidence`, `path`, `confirmedBy`, `confirmedAt`, `sourceSystem`, `sourceRecord`, `relationshipSourceRecord` | B |
| `RelatedTicket` | `id`, `type`, `subject`, `status`, `createdAt`, `reportedVersion`, `reportedVersionPrecision`, `customerId`, `sameCustomer`, `evidence`, `path`, `reason`, `sharedFeatureIds[]`, `confirmedBugIds[]`, `sourceSystem`, `sourceRecord` | B `relatedTickets` |
| `KnowledgeArticleEvidence` | `id`, `type`, `title`, `summary`, `updatedAt`, `sourceSystem`, `sourceId`, `sourceRecord`, `sourcePath`, `reachedVia[]`, `content` | C `articles` |
| `KnowledgeRoute` | `relationship`, `targetId`, `basis`, `evidence`, `relationshipSourceRecord` | C |
| `InvestigationResultCounts` | `features`, `confirmedBugs`, `candidateBugs`, `candidateBugLimit`, `candidateBugLimitReached`, `relatedTickets`, `relatedTicketTotal`, `relatedTicketLimit`, `relatedTicketsTruncated`, `knowledgeArticles`, `knowledgeContentIncluded` | Computed from the rows |

The fields are exactly those the queries return; some records lack `sourceId` (features, related tickets) because the query does not return it, and the API does not add it. `type` is the graph label echoed by the query.

### Ordering

Taken unchanged from the queries:
- `features`, `confirmedBugs`, `knowledgeArticles`, release lists and `confirmedTickets`: by `id`.
- `candidateBugs`: `confirmedTicketCount` descending, then `id`. This is a factual count, not a relevance score.
- `relatedTickets`: `createdAt` descending, then `id`.
- `reachedVia`: `relationship`, then `targetId`.

### Limits and truncation

| List | Limit | How truncation is shown |
|---|---|---|
| `candidateBugs` | 25 | `counts.candidateBugLimitReached` is `true` when 25 were returned. The query returns no total, so the API can only say the limit was reached, not how many were cut. |
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

## Markdown inclusion

With `includeContent=false` (the default) every `knowledgeArticles[].content` is `null`; the field is still present so the contract does not change shape. With `includeContent=true` it holds the article's full Markdown exactly as stored on the `KnowledgeArticle` node (the Acme response grows from about 64 KB to about 70 KB). Content is returned only for articles reached through the graph routes above.

Article content, ticket descriptions and resolutions are untrusted source text. The API returns them as plain JSON strings, never interprets them, and never logs article content. Consumers (the React UI, a later AI retrieval service) must treat them as data and must not follow instructions found in them.

## Acme scenario (verified 2026-11-03)

`GET /api/investigations/tickets/ticket:TCK-1046` returns 200:

- `ticket`: status `open`, `reportedVersion` `"2.3"`, normalized `2.3`, precision `minor`, `sourceRecord` `data/seed/tickets.json#/records/46`.
- `customer`: `customer:acme` (Acme Corporation), `documented`.
- `features`: `feature:csv-export`, `documented`.
- `confirmedBugs`: empty.
- `candidateBugs`: BUG-231 (21 confirmed tickets from 21 customers), BUG-238 (6), BUG-159 (3), BUG-233 (3), BUG-205 (2), BUG-261 (2), all `candidate`.
- BUG-231: `introducedIn` `release:2.3.0`, `fixedIn` `release:2.3.2` (2026-08-04, `documented`, with the PR-5120 evidence text). Its 21 `confirmedTickets` (TCK-1017 … TCK-2159) are all `confirmed`, none from Acme, `confirmedTicketsTruncated: false`.
- `relatedTickets`: 50 of 53, `relatedTicketsTruncated: true`.
- `knowledgeArticles`: KB-003, KB-012, KB-014, KB-031, KB-036, KB-039. KB-012 is reached via COVERS csv-export (`documented`) and ADDRESSES BUG-231 (`candidate`).

`ticket:TCK-1017` returns BUG-231 in `confirmedBugs` (`confirmed`, with `confirmedBy`, `confirmedAt` and relationship provenance), not in `candidateBugs`, and KB-012's ADDRESSES route is `confirmed`.

## Local verification

The local-only test project `tests/ResolveNest.Api.InvestigationApiTests/` (excluded through `.git/info/exclude`, not in `ResolveNest.slnx`) hosts the API in memory with `WebApplicationFactory` against the local Neo4j:

```bash
dotnet test tests/ResolveNest.Api.InvestigationApiTests
```

It covers the Acme and TCK-1017 scenarios against independent count queries, 400/404/503 behavior, `includeContent`, evidence values and provenance formats, ordering, duplicates, determinism, the health endpoints, cancellation, embedded queries matching the files on disk, and unchanged graph counts.

## Known limitations

- **No diagnosis:** candidates are structural only. No symptom, error-code or version matching, and no ranking beyond the query's factual count. Telling BUG-231 apart from BUG-238 for Acme is later work.
- **Versions uncompared:** the API returns ticket and release versions but does not decide whether a reported version falls in a bug's affected range.
- **Candidate total unknown:** when 25 candidates are returned, the total beyond the limit is not known.
- **Fixed limits:** 25 candidates, 25 confirmed tickets per candidate, 50 related tickets, defined in Cypher.
- **Cancellation granularity:** a query already running on the server is not interrupted; see [Cancellation](#cancellation).
- **Single customer assumed:** the queries assume at most one REPORTED customer per ticket. If the data ever violated that, the query would return several rows and the API would answer 500.
- **No authentication, no caching, no OpenAPI document** yet.
- **Knowledge retrieval** follows only ADDRESSES and COVERS; `entity-knowledge.cypher` is not exposed.
