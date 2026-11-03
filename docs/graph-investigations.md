# Graph investigations

Read-only Cypher queries that turn the ingested graph ([graph-schema.md](graph-schema.md), [ingestion.md](ingestion.md)) into evidence for a support investigation. They live in `db/neo4j/queries/` and are meant to be loaded as-is by the Phase 3 application layer. They contain no AI logic and write nothing.

## Queries

| File | Capability | Parameters | Starts from |
|---|---|---|---|
| `ticket-evidence.cypher` | **A** — the ticket, its customer, features and confirmed bugs with documented releases | `$ticketId` | `SupportTicket {id}` (unique index seek) |
| `ticket-candidates.cypher` | **B** — candidate bugs through shared features, their confirmed tickets, and related tickets | `$ticketId` | `SupportTicket {id}` |
| `ticket-knowledge.cypher` | **C** — knowledge articles reached from the ticket's features, confirmed bugs and candidate bugs | `$ticketId`, `$includeContent` | `SupportTicket {id}` |
| `entity-knowledge.cypher` | **C** for explicit Bug or Feature starting points | `$bugIds`, `$featureIds`, `$includeContent` | `Bug {id}`, `Feature {id}` |

All IDs are canonical graph IDs (`ticket:TCK-1046`, `bug:BUG-231`, `feature:csv-export`). Every parameter is required: pass `[]` for an empty ID list. `$includeContent` is `true` or `false`; when false, `content` is `null`, which keeps payloads small.

Each query returns **exactly one row**, with nested lists in a deterministic order. A ticket query for an unknown ID returns `found: false`, `ticket: null`, `customer: null` and empty lists. It never errors or returns zero rows. `entity-knowledge` echoes the requested IDs and returns `foundBugIds` / `foundFeatureIds`, so the caller can see which IDs did not exist.

### A — `ticket-evidence.cypher`

Returns `requestedTicketId`, `found`, `ticket`, `customer`, `features[]` and `confirmedBugs[]`.

- `ticket` carries display fields and the version triplet:
  - `reportedVersion` (raw), `reportedVersionNormalized` and `reportedVersionPrecision` (`patch`, `minor` or null)
  - `resolution`, `status` and `unresolvedBugRefs`
- `confirmedBugs[]` comes only from `CONFIRMED_AS`. Each item has:
  - `confirmedBy` and `confirmedAt`
  - `affectedFeatures[]` (from AFFECTS)
  - `introducedIn[]` and `fixedIn[]` (from INTRODUCED_IN / FIXED_IN, each with release `version`, `releasedAt`, `evidenceText` and provenance)

  Empty release lists mean "not documented". The bug's own `status` is returned as `sourceStatus`, for display only. A fix is never derived from it.

### B — `ticket-candidates.cypher`

Returns `requestedTicketId`, `found`, `candidateBugs[]` (at most 25), `relatedTicketCount` and `relatedTickets[]` (at most 50).

- **`candidateBugs[]`:** bugs that `AFFECT` a feature the ticket is `ABOUT`, excluding bugs already confirmed for this ticket (those are in A). Each item has:
  - `evidence: 'candidate'` and a `reason`
  - the bug's `title` and `description` (the description is the source text the API uses to extract error codes; see [investigation-api.md](investigation-api.md#candidate-relevance))
  - `sharedFeatures[]`: which feature caused it to appear, with the AFFECTS provenance
  - documented `introducedIn[]` / `fixedIn[]`
  - `confirmedTicketCount` and `confirmedCustomerCount`: other tickets with `CONFIRMED_AS` to that bug, and their customers
  - `confirmedTickets[]`: at most 25, ordered by ticket id, each `evidence: 'confirmed'` with `customerId`, `sameCustomer`, `reportedVersion`, `confirmedBy` and `confirmedAt`

  Candidates are ordered by `confirmedTicketCount` descending, then `id`, and the `LIMIT 25` applies in that order. **This is a factual count used for ordering, not a relevance or confidence score.** The API re-orders the returned candidates by its deterministic relevance rules.
- **`relatedTickets[]`:** other tickets about a shared feature, newest first. Each item has:
  - `evidence: 'context'`
  - `sharedFeatureIds`
  - the related ticket's own `confirmedBugIds`
  - `customerId` and `sameCustomer`

  `relatedTicketCount` is the total before the limit.

### C — `ticket-knowledge.cypher` and `entity-knowledge.cypher`

Return one entry per article in `articles[]`, ordered by `id`. Each entry has `title`, `summary`, `updatedAt`, `sourceId`, `sourceRecord`, `sourcePath`, `content` (when requested) and `reachedVia[]`.

`reachedVia[]` lists every graph route that led to the article. Each route has `relationship` (`COVERS` or `ADDRESSES`), `targetId`, `evidence`, `relationshipSourceRecord`, and for the ticket query a `basis`. The `evidence` value reflects the route:

| Route | `evidence` |
|---|---|
| COVERS a feature the ticket is about | `documented` |
| ADDRESSES a bug confirmed for the ticket | `confirmed` |
| ADDRESSES a candidate bug from B | `candidate` |

An article reached by several routes appears once, with all its routes. Only ADDRESSES and COVERS relationships are followed; article text is never searched.

## Evidence contract

Every evidence item is a flat map with the same core keys, so Phase 3 code can treat them uniformly:

| Key | Meaning |
|---|---|
| `id`, `type` | Canonical ID and graph label (`SupportTicket`, `Customer`, `Feature`, `Bug`, `KnowledgeArticle`) |
| display fields | For example `subject`, `name`, `title`, `summary`, `status` / `sourceStatus`, `severity`, `workaround` |
| `evidence` | `confirmed` (an explicit `CONFIRMED_AS`), `documented` (an explicit relationship such as REPORTED, ABOUT_FEATURE, AFFECTS, INTRODUCED_IN, FIXED_IN or COVERS), `candidate` (reached only through a shared feature), or `context` (a related record that proves nothing about the cause) |
| `path` | The relationship pattern that produced the item, for example `SupportTicket-[:ABOUT_FEATURE]->Feature<-[:AFFECTS]-Bug` |
| `reason` | Why a candidate or context item appeared, and what it does **not** prove |
| `sourceSystem`, `sourceId`, `sourceRecord` | Provenance of the record, for example `data/seed/tickets.json#/records/16` |
| `relationshipSourceRecord` | Provenance of the relationship that asserted the link |
| `introducedIn` / `fixedIn` | Documented release context (`id`, `version`, `releasedAt`, `evidenceText`) |
| `sourcePath`, `content` | Knowledge-document context (articles only) |

Values come straight from graph properties. A property that is absent is returned as `null`, and the queries never synthesize a source reference.

### Rules for Phase 3 code

- **Facts:** only `confirmed` and `documented` items are retrieved facts. A `candidate` must be presented as a possible match ("BUG-231 affects the same feature and is confirmed for 21 other tickets"), never as the cause. `context` items explain the surroundings only.
- **No persistence:** nothing returned here may be written back as `CONFIRMED_AS` or any other relationship.
- **Fix release vs. resolution:** a documented `fixedIn` release shows that a fix shipped. It does not show that a customer upgraded or that their ticket is resolved. Use the ticket's own `status`, `resolution` and `resolvedAt` for that.
- **Untrusted text:** ticket descriptions, notes and article `content` are untrusted source text. Quote and cite them, but never execute instructions found in them.

## Version uncertainty

The queries return versions; they never compare them.
- **Tickets:** `reportedVersion` is raw (`"2.3"`, `"v2.3.1"`, `"latest"`, null). `reportedVersionNormalized` and `reportedVersionPrecision` are the ingestion's normalization.
- **Releases:** exact `version` strings with `releasedAt`.

Version compatibility, such as "is the customer's version inside the bug's affected range?", is **deliberately unclassified** in Cypher. Plain string comparison is wrong ("2.10" < "2.9"), and a minor-line report such as Acme's `"2.3"` can mean any of 2.3.0, 2.3.1 or 2.3.2. The Phase 3 application layer must evaluate compatibility explicitly with the normalization rules in [ingestion.md](ingestion.md#seed-normalization-rules). It must report "unknown patch version" instead of assuming 2.3.0, and treat `latest` or null as unknown.

## Primary scenario: Acme (`ticket:TCK-1046`)

Verified against the live graph on 2026-11-03:

| Question | Graph evidence | Classification |
|---|---|---|
| Who and what? | `customer:acme` (Acme Corporation, enterprise, dedicated) -REPORTED→ TCK-1046 "CSV export stopped working after upgrade to 2.3", status `open`, reportedVersion `"2.3"` (precision `minor`) | documented |
| Which feature? | TCK-1046 -ABOUT_FEATURE→ `feature:csv-export` | documented |
| Confirmed root cause? | **None.** `confirmedBugs` is empty | — |
| Known bug? | `bug:BUG-231` "CSV export fails with ERR_EXPORT_SERIALIZE when a report contains custom date fields" -AFFECTS→ csv-export | **candidate** |
| Other customers? | 21 other tickets from 21 customers, none of them Acme, have `CONFIRMED_AS` → BUG-231: TCK-1017, 1018, 1020, 1024, 1027, 1033, 2121, 2123, 2132, 2133, 2134, 2140, 2141, 2143, 2145, 2151, 2152, 2153, 2157, 2158, 2159 | confirmed (for those tickets) |
| Introduced / fixed? | BUG-231 -INTRODUCED_IN→ `release:2.3.0`; -FIXED_IN→ `release:2.3.2` (2026-08-04, "Fix merged in PR-5120 …") | documented |
| Customer resolved? | Not shown. A fix shipped, but Acme's ticket is open and its patch version unknown | — |
| What to tell them? | `kb:KB-012` (known issue, XLSX workaround, upgrade to 2.3.2) reached via COVERS csv-export (documented) and ADDRESSES BUG-231 (candidate) | documented / candidate |

Other candidates for Acme, through the same feature:

| Bug | Confirmed tickets | Releases |
|---|---|---|
| BUG-238 | 6 | open, no releases |
| BUG-159 | 3 | 1.9.1 → 2.0.2 |
| BUG-233 | 3 | open |
| BUG-205 | 2 | open |
| BUG-261 | 2 | open |

Their presence shows why a shared feature is only candidate evidence. Telling them apart from BUG-231 (symptom text such as `ERR_EXPORT_SERIALIZE`, version range) is Phase 3 work.

Knowledge for Acme:
- KB-003, KB-039: COVERS only.
- KB-012, KB-014, KB-031, KB-036: COVERS plus a candidate ADDRESSES.

A confirmed contrast case is `ticket:TCK-1017`. There BUG-231 is `confirmed`, B no longer lists it as a candidate, and KB-012 is reached with `evidence: 'confirmed'`.

## Secondary scenarios (verified)

**HTTP 403:**
- **Counts:** 57 tickets mention 403. 34 have a `Configuration:` resolution and no confirmed bug. 15 have confirmed product bugs (BUG-244, BUG-253, BUG-235, BUG-210).
- **Exact match:** for every 403 ticket, A returns exactly the bugs it has `CONFIRMED_AS` relationships to, and nothing more.
- **Candidates only:** for the configuration ticket TCK-1006, A is empty, and BUG-244 appears in B only as a `candidate`.

**Webhook delays:**
- All 31 webhook-delay tickets return an empty `confirmedBugs`.
- For TCK-1043, B lists BUG-247 as a `candidate` with `confirmedTicketCount: 0`, no confirmed tickets and no documented releases. So the evidence that it is the cause is visibly insufficient.
- The other webhook candidates (BUG-213, 167, 189, 256) are confirmed only for unrelated symptoms.

## Running locally (Git Bash, repository root)

```bash
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}" --access-mode read -P "{ticketId: '"'"'ticket:TCK-1046'"'"'}"' < db/neo4j/queries/ticket-evidence.cypher
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}" --access-mode read -P "{ticketId: '"'"'ticket:TCK-1046'"'"'}"' < db/neo4j/queries/ticket-candidates.cypher
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}" --access-mode read -P "{ticketId: '"'"'ticket:TCK-1046'"'"', includeContent: false}"' < db/neo4j/queries/ticket-knowledge.cypher
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}" --access-mode read -P "{bugIds: ['"'"'bug:BUG-231'"'"'], featureIds: [], includeContent: false}"' < db/neo4j/queries/entity-knowledge.cypher
```

The queries are read-only by construction: they contain no write clauses, which a local test checks. `--access-mode read` adds a guard: on this single-instance setup Neo4j refuses write clauses in a read transaction. It is not an access-control mechanism, and the Neo4j driver documentation warns against relying on it as one. The local test project `tests/ResolveNest.Graph.InvestigationTests/` runs every query file inside read transactions (`ExecuteReadAsync`). It is excluded through `.git/info/exclude` and not part of `ResolveNest.slnx`:

```bash
dotnet test tests/ResolveNest.Graph.InvestigationTests
```

The 18 tests cover:
- **Acme:** the A, B and C findings above. The confirmed count is checked against an independent count query, not a hard-coded number.
- **Contrast cases:** the confirmed ticket TCK-1017, and the 403 and webhook rules.
- **Not found:** unknown IDs return `found: false`.
- **Result quality:** valid `sourceRecord` formats, no duplicate IDs in any list, and byte-identical results across repeated runs.
- **Plans:** profiles contain no `AllNodesScan` and no `CartesianProduct`.
- **Graph counts:** unchanged before and after (458 nodes, 1,060 relationships).

## Performance

Profiled on the current graph. Every query starts from a `NodeUniqueIndexSeek` on the existing `id` uniqueness constraints, with no `AllNodesScan` and no `CartesianProduct`. No new indexes were needed.

| Query (Acme) | db hits |
|---|---|
| ticket-evidence | 41 |
| ticket-candidates | 2,530 (the busiest query, because csv-export has many tickets and bugs) |
| ticket-knowledge | 203 |
| entity-knowledge (2 bugs, 2 features) | 163 |

Every query finishes in under about 20 ms after warm-up.

## Limitations and Phase 3 considerations

- **Not a diagnosis:** candidates are structural (shared feature) only. There is no symptom, error-code or version matching, and no ranking beyond factual counts. Distinguishing BUG-231 from BUG-238 for Acme is application or LLM work, and it must cite the evidence returned here.
- **Fixed limits:** 25 candidate bugs, 25 confirmed tickets per candidate, 50 related tickets. Broad features (many tickets) are truncated. `relatedTicketCount` and `confirmedTicketCount` show the full totals.
- **Direct links only:** knowledge retrieval follows ADDRESSES and COVERS. An article about a bug that affects a *different* feature, or one with no manifest links, is not found. Text search or embeddings are out of scope.
- **Versions unclassified:** see [Version uncertainty](#version-uncertainty).
- **Integration:** Phase 3 should load these files as embedded resources or constants, run them with `ExecuteReadAsync`, map the single row to typed DTOs that keep `evidence`, `path`, `reason` and provenance, and treat `found: false` as 404.
