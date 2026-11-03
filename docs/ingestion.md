# Seed ingestion

Ingestion loads `data/seed/` into Neo4j according to [graph-schema.md](graph-schema.md). It is a manual command of the existing API executable. It never runs on API startup and has no HTTP endpoint.

## Running it (Git Bash, from the repository root)

Prerequisites:
- Neo4j is running (`docker compose up -d`).
- The constraints are applied ([graph-schema.md](graph-schema.md#apply-and-verify-locally)).
- `Neo4j:Password` is set in user secrets.

```bash
dotnet build ResolveNest.slnx

# Validate only: reads and checks every file, prints the plan, never connects to Neo4j
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed --dry-run

# Import (idempotent; safe to re-run)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed
```

`--seed-dir <path>` overrides the default `data/seed`. A relative path resolves against the repository root, which is found by walking up to `ResolveNest.slnx`, because `dotnet run` does not keep the caller's working directory. Provenance never contains a local absolute path:
- A directory inside the repository is recorded relative to the root, for example `data/seed/tickets.json#/records/46`.
- A directory outside the repository is recorded as `external/<directory name>/…`.

| Exit code | Outcome | Database state |
|---|---|---|
| 0 | Committed and graph counts reported (or dry run passed) | Import committed (or untouched) |
| 1 | Source or validation errors | Not contacted |
| 2 | Preflight failed: constraints missing, the database could not be reached, or an ownership conflict | Nothing written. A conflict is detected inside the transaction, which is then rolled back |
| 3 | Write failed before commit | Not committed. The output says whether the explicit rollback was confirmed. Uncommitted work is never persisted |
| 4 | Commit failed, or an unexpected driver failure while writing | **Unknown.** Inspect the graph before re-running |
| 5 | Committed, but reading graph counts afterwards failed | Import committed. Only the report is missing |
| 64 | Invalid arguments | Not contacted |
| 130 | Cancelled (Ctrl+C) before commit | Not committed (same rollback reporting as 3) |

A transaction is reported as rolled back only when it never reached commit. Once `CommitAsync` has been called, a failure is reported as an unknown outcome, never as a rollback.

## Design

The code lives in `apps/api/ResolveNest.Api/Features/Ingestion/`. Nothing in it runs until `apps/api/ResolveNest.Api/Program.cs` sees the `ingest-seed` verb, which bypasses `app.Run()`. It adds no new packages: it uses `System.Text.Json`, a small built-in CSV parser and the existing `Neo4j.Driver` singleton.

| Step | Type | Responsibility |
|---|---|---|
| 1. Resolve and read | `SeedDirectoryResolver`, `SeedSourceReader`, `CsvTableParser` | Resolve the seed directory and its provenance display path. Read `customers.csv`, `features.csv`, `releases.csv`, `tickets.json` and `bugs.json`, attaching a location to every record. |
| 2. Validate and normalize | `SeedGraphBuilder`, `SourceNormalization` | Required fields, enums, timestamps, canonical IDs, duplicates, reference resolution, release ordering, ticket de-duplication and documented unresolved references. Produces a `SeedImportPlan` plus warnings and errors. |
| 3. Gate | `SeedIngestionCommand` | Any error stops the run before Neo4j is contacted. |
| 4. Constraint preflight | `SeedGraphWriter.CheckConstraintsAsync` | `SHOW CONSTRAINTS` must list the six named uniqueness constraints on `id`. |
| 5. Ownership preflight and write | `SeedGraphWriter.WriteAsync` | One explicit write transaction (2-minute timeout): count existing nodes, check ownership, then one `UNWIND … MERGE` per label and relationship type, then commit. Returns an explicit `GraphWriteOutcome`. |
| 6. Report | `SeedGraphWriter.CountGraphAsync` | Graph counts by label and type, read after the commit with separate error handling. |

The dataset is about 430 nodes and about 980 relationships, so a single transaction keeps the import all-or-nothing. A much larger dataset would need bounded batches.

### Writes and property ownership

All Cypher is constant text in `SeedGraphQueries`, and values travel only as parameters.

- Nodes: `MERGE (node:Label {id: row.id}) SET node += row`.
- Relationships: `MATCH` both endpoints, `MERGE` the relationship, then `SET relationship += row.properties`. Each statement returns `count(relationship)`, and the transaction fails if that differs from the number of rows sent.

`SET +=` updates only the keys in the row map, which is the fixed set of **seed-owned properties** for that label or type. Properties written by other features (for example an investigation note) are preserved. A local test verifies this in a transaction that is rolled back.

| Source change | Behavior on the next import |
|---|---|
| A field's value changes | The seed-owned property is overwritten. |
| An optional field becomes `null` or empty | The builder always sends every seed-owned key, so the key is sent as `null` and `SET +=` **removes** that property. |
| A property is dropped from the seed contract in code | It stays on existing nodes. Ingestion does not delete keys it no longer writes. |
| A record or link disappears from the source | The node or relationship **stays** in the graph, still marked as seed-owned. Ingestion is upsert-only and has no deletion or synchronization step. Removing stale seed data requires an explicit, approved manual cleanup. |

### Ownership marker and preflight

Every node and relationship written by seed ingestion carries `ingestedBy: "seed-ingestion"`, next to `sourceSystem`, `sourceId` (nodes), `sourceRecord` and `ingestedAt`. A future knowledge ingestion is expected to use its own value, for example `knowledge-ingestion`.

Inside the write transaction, before any write, the preflight checks every node with a seed label (Customer, Feature, Release, Bug, SupportTicket) and every relationship of a seed type (REPORTED, ABOUT_FEATURE, CONFIRMED_AS, AFFECTS, INTRODUCED_IN, FIXED_IN). Each one must be either:

1. **In this import:**
   - for nodes: same `Label|id` as a planned node, with existing `sourceSystem|sourceId` equal to the planned values
   - for relationships: same `TYPE|startId|endId`, with matching `sourceSystem`
   - in both cases, `ingestedBy` must be `seed-ingestion` or absent

   An absent marker is accepted only for records written before the marker existed, and only when their identity matches the plan exactly. The next import adds the marker.
2. **Or an earlier seed record no longer in the source:** marked `seed-ingestion`, and left untouched.

Anything else is a conflict, and the run stops with exit code 2 before writing anything:
- a record owned by another ingestion
- a seed-labeled node with different provenance
- a seed-labeled node with a missing marker that the plan does not include
- a missing `sourceId` or `sourceSystem`

The comparisons use explicit `coalesce(…, false)`, so a missing property can never turn a conflict into a pass through Cypher's null logic. A local test caught that bug during hardening.

Labels and relationship types outside the seed contract (`KnowledgeArticle`, `ADDRESSES`, `COVERS`, or anything a later feature adds) are never inspected or modified. The run reports how many nodes with other labels exist. Seed ingestion never merges into a record owned by another ingestion: such a record triggers a conflict instead.

### Normalization rules

| Input | Rule |
|---|---|
| `customer_ref`, `feature_refs` | Trim, lowercase. The raw value is kept (`sourceCustomerRef`, `sourceFeatureRef`). |
| `bug_ref`, ticket `source_id` | Trim, uppercase. The raw value is kept (`sourceBugRef`). |
| Versions | Trim and strip `v`/`V`. `x.y.z` is exact, `x.y` is a minor line, anything else is unparseable. Tickets store `reportedVersion` (raw), `reportedVersionNormalized` and `reportedVersionPrecision`. |
| Bug `introduced_in` / `fixed_in` | Only an exact, known release creates a relationship. A minor line or unparseable value gives a warning. An unknown exact version is an error. Raw values are kept as `sourceIntroducedIn` / `sourceFixedIn`. |
| Canonical IDs | `id` must equal the prefix plus the normalized key. A mismatch is an error. |
| Duplicate ticket records | The latest `updated_at` wins regardless of file order. Equal `updated_at` with different content is an error. |
| Confirmed links | Only `confirmed_bug_links` creates `CONFIRMED_AS`. Free text is never parsed. |

### Unresolved confirmed bug references

A `confirmed_bug_links` entry whose bug is missing from the export is a **validation error**, unless it is listed in `SeedIngestion:DocumentedUnresolvedBugReferences` in `apps/api/ResolveNest.Api/appsettings.json`:

```json
{ "TicketId": "TCK-1002", "BugRef": "BUG-190", "Reason": "Legacy tracker item archived before the synthetic tracker export; documented in docs/seed-data.md." }
```

For a documented reference:
- No placeholder `Bug` and no `CONFIRMED_AS` are created.
- A warning is printed.
- The raw reference is stored on the ticket as `unresolvedBugRefs: ["BUG-190"]`, so the link stays traceable.

Matching uses the normalized ticket and bug keys. Other effects of the list:
- An entry missing `TicketId`, `BugRef` or `Reason` is an error.
- An entry that no longer occurs in the source is reported as a warning (possibly obsolete).
- The list is configuration rather than seed data so that the committed, generated dataset does not need to change.

## Tests (local only)

`tests/ResolveNest.Api.IngestionTests/` is an xUnit project excluded by `.git/info/exclude` and not part of `ResolveNest.slnx`. Run it with `dotnet test tests/ResolveNest.Api.IngestionTests`.

- **Unit tests (11):**
  - unexpected unresolved reference → error
  - documented reference → warning plus `unresolvedBugRefs`
  - obsolete and incomplete configuration entries
  - nullable seed-owned keys sent as `null`
  - ownership marker on every node and relationship
  - seed directory display paths (inside, external, `..name` siblings)
  - the committed dataset validating with exactly 7 warnings
- **Neo4j tests (6),** each inside a transaction that is always rolled back, using the production Cypher constants:
  - a foreign node property and a foreign relationship property survive the merge
  - a nulled seed-owned property is removed
  - the existing graph plus a knowledge-owned `KnowledgeArticle` and `ADDRESSES` pass the preflight
  - a seed node re-owned by another ingestion is rejected
  - conflicting `sourceSystem`, a foreign unmarked `Bug`, a node missing `sourceId`, a re-owned relationship and an unmarked foreign `CONFIRMED_AS` are all rejected

## Verified results

Neo4j 2026.10.1 Community.

**Initial import (Task 3, 2026-11-03):**
- First run: 428 nodes and 977 relationships created.
- Second run: 0 created.

**Hardening (Task 3A, 2026-11-03):**
- **Before:** 428 nodes, 977 relationships, 6 constraints, no ownership markers.
- **Dry run:** 0 errors and the 7 documented warnings. TCK-1002 → BUG-190 is now reported as documented.
- **Tests:** all 17 local tests pass. Afterwards cypher-shell found no probe nodes, properties or relationships left behind, and counts were still 428/977.
- **Re-imports:** two runs, both with `nodesCreated=0` and `relationshipsCreated=0`. The first added `ingestedBy` to all 428 nodes and 977 relationships.
- **After:** counts unchanged (Customer 40, Feature 12, Release 16, Bug 60, SupportTicket 300; REPORTED 300, ABOUT_FEATURE 362, CONFIRMED_AS 155, AFFECTS 67, INTRODUCED_IN 52, FIXED_IN 41).
- **Acme path preserved:** `customer:acme` → REPORTED (`" Acme"`) → TCK-1046 (`"2.3"`, minor, 0 `CONFIRMED_AS`) → csv-export ← AFFECTS ← BUG-231 (fixed) → INTRODUCED_IN 2.3.0 and FIXED_IN 2.3.2. BUG-231 still has 21 confirmed tickets.
- **TCK-1002:** `unresolvedBugRefs = ["BUG-190"]`, 0 `CONFIRMED_AS`, and no BUG-190 node.
- **Webhooks:** the 31 webhook-delay tickets and BUG-247 have 0 confirmed links.
- **Provenance:** no `sourceRecord` is an absolute path.
- **CLI negative run:** on an external copy with an unexpected `BUG-999` link, the run reported one error and exit code 1 without contacting the database. Provenance was shown as `external/badseed/…`.

## Limitations

- **Upsert only:** removed source records and links persist. There is no deletion or synchronization.
- **Legacy unmarked records:** these are accepted when their identity matches the plan. After the next import every seed record is marked, and this allowance could be removed.
- **Ownership is a convention:** it is enforced by the preflight and Neo4j cannot enforce it. Another writer that sets `ingestedBy: "seed-ingestion"` would be treated as seed data.
- **Unexercised paths:** the exit 3, 4 and 5 paths (failure before commit, commit failure, failure while reporting counts) and Ctrl+C cancellation were reviewed but not triggered against a live database.
- **Not ingested yet:** knowledge articles.
