# Graph schema

ResolveNest's Neo4j knowledge graph contract for the MVP. The schema file is
[`db/neo4j/schema.cypher`](../db/neo4j/schema.cypher). It holds uniqueness constraints only. Synthetic source data is described in [seed-data.md](seed-data.md). Ingestion of all six labels and eight relationship types (`ingest-seed` and `ingest-knowledge`) is described in [ingestion.md](ingestion.md).

## Node labels

Every node has a canonical string `id`. Future ingestion sets it, and it is never a Neo4j internal element ID.

| Label | Example `id` | Primary properties (planned) |
|---|---|---|
| `Customer` | `customer:acme` | `name`, `plan`, `region` |
| `SupportTicket` | `ticket:TCK-1001` | `subject`, `description`, `status`, `priority`, `reportedVersion`, `createdAt`, `resolvedAt` |
| `Bug` | `bug:BUG-231` | `title`, `description`, `status` (`open`/`fixed`/`wontfix`), `severity`, `workaround` |
| `Feature` | `feature:csv-export` | `name`, `area` |
| `Release` | `release:2.3.0` | `version`, `releasedAt`, `notes` |
| `KnowledgeArticle` | `kb:KB-012` | `title`, `summary`, `content` (full Markdown), `sourcePath`, `updatedAt` |

`reportedVersion` on a ticket is a plain string the customer gave us. It is deliberately not a relationship to `Release`, because customers often report versions loosely ("2.3", "latest").

## Canonical identity

- Format: `<type-prefix>:<natural key>`, lowercase prefix. Examples: `ticket:TCK-1001`, `release:2.3.0`.
- The natural key comes from the source record (ticket number, bug key, semantic version, KB slug, customer slug). It must be deterministic, so re-importing the same record yields the same `id`.
- The prefix makes IDs unambiguous on their own, for example in AI citations, logs and API responses (`2.3.0` alone could be anything).
- Ingestion writes nodes with `MERGE (n:Label {id: $id})`. It never writes them with `CREATE`, so imports stay idempotent.
- Neo4j element IDs (`elementId()`) are never stored, exposed in APIs or used as references. They can change across database rebuilds.

## Relationships

All relationships are directed as listed. There is at most one relationship of a given type between two nodes, and ingestion `MERGE`s on the (start, type, end) triple.

| Pattern | Meaning |
|---|---|
| `(Customer)-[:REPORTED]->(SupportTicket)` | The customer opened this ticket. |
| `(SupportTicket)-[:CONFIRMED_AS]->(Bug)` | A human (support or engineering) **confirmed** that this ticket is caused by this bug. |
| `(SupportTicket)-[:ABOUT_FEATURE]->(Feature)` | The ticket concerns this feature (from the ticket's category or tags). |
| `(Bug)-[:AFFECTS]->(Feature)` | The bug breaks or degrades this feature. |
| `(Bug)-[:INTRODUCED_IN]->(Release)` | The bug first appeared in this release. |
| `(Bug)-[:FIXED_IN]->(Release)` | The bug is fixed in this release. The fix status is derived from this link, not only from `Bug.status`. |
| `(KnowledgeArticle)-[:ADDRESSES]->(Bug)` | The article documents this bug (known issue, workaround or fix notes). |
| `(KnowledgeArticle)-[:COVERS]->(Feature)` | The article explains or troubleshoots this feature. |

The requested `(Bug)->(Release)` pair is split into two types, `INTRODUCED_IN` and `FIXED_IN`. The demo question ("stopped working after 2.3 … has it been fixed?") needs both facts. A single type with a `role` property would make every query filter on a property and would be easy to misread.

### Confirmed link vs. inferred similarity

- `CONFIRMED_AS` is **ground truth**. It exists only when the source data records a confirmed bug link on the ticket. Ingestion never creates it from text matching, and an LLM never creates it.
- **Symptom similarity is inferred at query time and never stored as `CONFIRMED_AS`.** A ticket with no confirmed bug can still be a *candidate* match. For example, it shares a `Feature` with a bug (`(t)-[:ABOUT_FEATURE]->(f)<-[:AFFECTS]-(b)`), its `reportedVersion` is at or after the bug's `INTRODUCED_IN` release, and its text is similar. Investigation results must label these as "possible / unconfirmed" and keep them separate from confirmed links.
- If inferred links are ever persisted (for example after a human accepts a suggestion), they get a distinct relationship type and an explicit user approval. That is out of scope for the MVP.

### Release evidence

- `INTRODUCED_IN` and `FIXED_IN` come only from the bug tracker's `introduced_in` and `fixed_in` fields. Each must normalize to exactly one existing `Release`. Release-note text, ticket versions and ticket text never create them.
- A missing or ambiguous version (for example `"2.2"` with no patch number) creates no relationship. Ingestion reports a warning instead.
- `FIXED_IN` means a fix shipped in that release. It does **not** prove that a particular customer's issue is resolved. That needs the customer to be on a fixed version and the ticket to record a confirmation (`status` and `resolution`).

### Version normalization

- Trim whitespace and strip a leading `v`/`V`: `"v2.3.1"` becomes `2.3.1`.
- A three-part version such as `2.3.0` matches `Release.version` exactly.
- A two-part version such as `"2.3"` means "some 2.3.x". It is never coerced to `2.3.0`. On tickets it is stored as-is. On bug release fields it creates no relationship.
- Always keep the raw string next to the normalized value (`reportedVersion` stays raw on tickets).

## Provenance (for future ingestion)

Each node and relationship written by ingestion carries:

| Property | Example | Purpose |
|---|---|---|
| `sourceSystem` | `synthetic-helpdesk`, `synthetic-issue-tracker`, `synthetic-kb` | Where the record came from (the record's own `source_system`). |
| `sourceId` | `TCK-1001` | The raw identifier in that system, unmodified. |
| `sourceRecord` | `data/seed/customers.csv#row=2`, `data/seed/tickets.json#/records/46`, `data/seed/knowledge/KB-012-csv-export-err-export-serialize.md` | The file plus row, JSON pointer or path that produced this write. |
| `ingestedAt` | `datetime()` | When the record was last written. |
| `ingestedBy` | `seed-ingestion`, `knowledge-ingestion` | Which ingestion owns the record. Each ingestion only updates records it owns (see [ingestion.md](ingestion.md#ownership-markers-and-preflight)). |

Relationships carry `sourceSystem`, `sourceRecord`, `ingestedAt` and `ingestedBy` (from the record that asserted the link). `CONFIRMED_AS` also carries `confirmedBy` and `confirmedAt` from the ticket. Citations in AI answers reference `id` and `sourceRecord`. External seed directories are recorded as `external/<directory name>/…`, never as absolute local paths.

When ingestion normalizes a reference, it keeps the raw value as well (for example `sourceCustomerRef: " Acme"` on the ticket), so you can trace any mismatch back to the source. A documented confirmed bug reference that cannot be resolved creates no relationship. Its raw value is kept on the ticket as `unresolvedBugRefs`.

## What Neo4j enforces vs. what the application must validate

Enforced by Neo4j (Community Edition):

- `id` is unique per label. There are six named constraints, listed below.

Not enforceable in Community Edition, so ingestion code must validate:

- `id` is present, non-empty and a string. Property existence and type constraints are Enterprise-only. A uniqueness constraint ignores nodes that lack the property.
- The `id` prefix matches the label (for example `Bug` nodes start with `bug:`).
- Relationships connect only the label pairs in the table above, in the stated direction. Neo4j has no relationship endpoint constraints.
- One label per node from the six above.
- Enum-like values (`Bug.status`, ticket `status`, `priority`) and the presence of provenance properties.
- A bug's `FIXED_IN` release is not earlier than its `INTRODUCED_IN` release.
- Relationship uniqueness per (start, type, end), which is guaranteed only by using `MERGE`.

## Constraints

| Name | Label | Property |
|---|---|---|
| `customer_id_unique` | `Customer` | `id` |
| `support_ticket_id_unique` | `SupportTicket` | `id` |
| `bug_id_unique` | `Bug` | `id` |
| `feature_id_unique` | `Feature` | `id` |
| `release_id_unique` | `Release` | `id` |
| `knowledge_article_id_unique` | `KnowledgeArticle` | `id` |

Each uniqueness constraint has its own backing RANGE index, which also serves `MATCH`/`MERGE` lookups by `id`. Do not add separate indexes on `id`.

Schema changes are applied manually. The API does not change the schema on startup.

## Apply and verify locally

Run these from the repo root with the Compose Neo4j service running (`docker compose up -d`). The commands read the password from the container's own `NEO4J_AUTH`, so it never appears on your command line.

```bash
# Apply (idempotent; re-running is a no-op)
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}"' < db/neo4j/schema.cypher

# Verify: expect the six constraints above, type NODE_PROPERTY_UNIQUENESS
docker compose exec -T neo4j sh -c 'cypher-shell -u neo4j -p "${NEO4J_AUTH#neo4j/}" "SHOW CONSTRAINTS YIELD name, type, labelsOrTypes, properties RETURN name, type, labelsOrTypes, properties ORDER BY name"'
```

You can also paste `db/neo4j/schema.cypher` into Neo4j Browser at http://localhost:7474.
