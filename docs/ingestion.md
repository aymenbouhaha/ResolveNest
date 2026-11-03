# Ingestion

Two manual commands of the existing API executable load the synthetic dataset into Neo4j according to [graph-schema.md](graph-schema.md). Neither runs on API startup, and neither has an HTTP endpoint.

| Command | Source | Writes |
|---|---|---|
| `ingest-seed` | `data/seed/` CSV and JSON | Customer, Feature, Release, Bug, SupportTicket; REPORTED, ABOUT_FEATURE, CONFIRMED_AS, AFFECTS, INTRODUCED_IN, FIXED_IN |
| `ingest-knowledge` | `data/seed/knowledge/manifest.json` and its Markdown files | KnowledgeArticle; ADDRESSES (→ Bug), COVERS (→ Feature) |

Run `ingest-seed` first, because knowledge articles reference existing Bug and Feature nodes. After that, both commands can be repeated in any order.

## Running (Git Bash, from the repository root)

Prerequisites:
- Neo4j is running (`docker compose up -d`).
- The constraints are applied ([graph-schema.md](graph-schema.md#apply-and-verify-locally)).
- `Neo4j:Password` is set in user secrets.

```bash
dotnet build ResolveNest.slnx

# Structured seed data
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed --dry-run
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed

# Knowledge articles
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-knowledge --dry-run
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-knowledge
```

`--dry-run` reads and validates every source file without connecting to Neo4j. For knowledge articles, the bug and feature references are checked for format and duplicates during a dry run, but whether they exist in the graph is checked only in the write preflight.

`--seed-dir <path>` (seed) and `--knowledge-dir <path>` (knowledge) override the defaults `data/seed` and `data/seed/knowledge`. A relative path resolves against the repository root, found by walking up to `ResolveNest.slnx`, because `dotnet run` does not keep the caller's working directory. Provenance never contains a local absolute path:
- A directory inside the repository is recorded relative to the root (for example `data/seed/tickets.json#/records/46`).
- A directory outside it is recorded as `external/<directory name>/…`.

| Exit code | Outcome | Database state |
|---|---|---|
| 0 | Committed and graph counts reported (or dry run passed) | Import committed (or untouched) |
| 1 | Source or validation errors | Not contacted |
| 2 | Preflight failed: constraints missing, the database could not be reached, an ownership conflict, or a missing referenced node | Nothing written. Problems found inside the transaction trigger a rollback |
| 3 | Write failed before commit | Not committed. The output says whether the explicit rollback was confirmed. Uncommitted work is never persisted |
| 4 | Commit failed, or an unexpected driver failure while writing | **Unknown.** Inspect the graph before re-running |
| 5 | Committed, but reading graph counts afterwards failed | Import committed. Only the report is missing |
| 64 | Invalid arguments | Not contacted |
| 130 | Cancelled (Ctrl+C) before commit | Not committed (same rollback reporting as 3) |

A transaction is reported as rolled back only when it never reached commit. A failure during or after `CommitAsync` is reported as an unknown outcome.

## Design

All code lives in `apps/api/ResolveNest.Api/Features/Ingestion/`. `apps/api/ResolveNest.Api/Program.cs` hands control to `IngestionCommandLine` when the first argument is `ingest-seed` or `ingest-knowledge`, so `app.Run()` is never reached. No new packages are used.

Shared primitives (both commands):

| Type | Responsibility |
|---|---|
| `IngestionCommandLine` | Recognizes the verbs, parses `--dry-run` and the directory option, handles Ctrl+C and dispatches. |
| `SeedDirectoryResolver` | Resolves a source directory and its provenance display path. |
| `GraphInspector` | Constraint preflight (`SHOW CONSTRAINTS` must list the six named uniqueness constraints on `id`) and graph counts after the commit. |
| `GraphWriteTransaction` | One explicit write transaction (2-minute timeout): preflight inside the transaction, rollback if it reports problems, `UNWIND … MERGE` batches, commit. Returns an explicit `GraphWriteOutcome`. Relationship batches fail if `count(relationship)` differs from the rows sent. |
| `IngestionConsole` | Prints warnings and errors, maps outcomes to exit codes, and reports counts with separate post-commit error handling. |
| `SourceNormalization` | Canonical keys and IDs, reference formats and version parsing. |

Command-specific types:

| Seed | Knowledge | Role |
|---|---|---|
| `SeedSourceReader`, `CsvTableParser` | `KnowledgeSourceReader` | Read the sources |
| `SeedGraphBuilder` → `SeedImportPlan` | `KnowledgeGraphBuilder` → `KnowledgeImportPlan` | Validate and normalize into an import plan |
| `SeedGraphWriter`, `SeedGraphQueries` | `KnowledgeGraphWriter`, `KnowledgeGraphQueries` | Ownership preflight and parameterized writes |
| `SeedIngestionCommand` | `KnowledgeIngestionCommand` | Orchestrate a run |

Each command writes in a single transaction, so each import is all-or-nothing. The seed import is about 430 nodes and 980 relationships, and the knowledge import is 30 nodes and 83 relationships. A much larger dataset would need bounded batches.

### Writes and property ownership

All Cypher is constant text, and values travel only as parameters.
- Nodes: `MERGE (node:Label {id: row.id}) SET node += row`.
- Relationships: `MATCH` both endpoints, `MERGE` the directed relationship, then `SET relationship += row.properties`.

`SET +=` updates only the keys each ingestion owns, so properties written by other features are preserved. Knowledge ingestion never writes to Bug or Feature nodes. It only `MATCH`es them as relationship endpoints.

| Source change | Behavior on the next import |
|---|---|
| A field's value changes | The owned property is overwritten. |
| An optional field becomes `null` or empty | The key is sent as `null`, and `SET +=` removes that property. |
| A property is dropped from the contract in code | It stays on existing nodes. |
| A record or link disappears from the source | It **stays** in the graph. Ingestion is upsert-only and has no deletion or synchronization step. |

### Ownership markers and preflight

Every record carries `ingestedBy`, next to `sourceSystem`, `sourceId` (nodes), `sourceRecord` and `ingestedAt`:

| Ingestion | `ingestedBy` | Owns labels | Owns relationship types |
|---|---|---|---|
| Seed | `seed-ingestion` | Customer, Feature, Release, Bug, SupportTicket | REPORTED, ABOUT_FEATURE, CONFIRMED_AS, AFFECTS, INTRODUCED_IN, FIXED_IN |
| Knowledge | `knowledge-ingestion` | KnowledgeArticle | ADDRESSES, COVERS |

Each preflight runs inside the write transaction, before any write, and inspects only the labels and types its own ingestion owns. Each record there must be either:
1. **In this import:**
   - for nodes: same id, with existing `sourceSystem|sourceId` equal to the plan
   - for relationships: same `TYPE|startId|endId`, with matching `sourceSystem`
   - the record must carry this ingestion's marker
2. **Or an earlier record of this ingestion no longer in the source:** it carries the marker, and it is left untouched.

Anything else stops the run with exit code 2 before writing anything:
- a record owned by another ingestion
- conflicting provenance
- a missing marker
- a missing `sourceId` or `sourceSystem`

All comparisons use explicit `coalesce(…, false)`, so Cypher null logic can never hide a conflict.

The two ingestions differ in one deliberate way. Seed ingestion still accepts unmarked seed records whose identity matches the plan exactly. This covers records written before the marker existed; today every seed record is marked. Knowledge ingestion has no such allowance: an unmarked KnowledgeArticle is a conflict.

Knowledge ingestion also verifies, in the same preflight, that every referenced `bug:` and `feature:` id exists as a `Bug` or `Feature` node. Missing endpoints are reported as problems (exit 2). The command never creates placeholder nodes.

### Seed normalization rules

| Input | Rule |
|---|---|
| `customer_ref`, `feature_refs` | Trim, lowercase. The raw value is kept (`sourceCustomerRef`, `sourceFeatureRef`). |
| `bug_ref`, ticket `source_id` | Trim, uppercase. The raw value is kept (`sourceBugRef`). |
| Versions | Trim and strip `v`/`V`. `x.y.z` is exact, `x.y` is a minor line, anything else is unparseable. Tickets store `reportedVersion` (raw), `reportedVersionNormalized` and `reportedVersionPrecision`. |
| Bug `introduced_in` / `fixed_in` | Only an exact, known release creates a relationship. A minor line or unparseable value gives a warning. An unknown exact version is an error. Raw values are kept as `sourceIntroducedIn` / `sourceFixedIn`. |
| Canonical IDs | `id` must equal the prefix plus the normalized key. |
| Duplicate ticket records | The latest `updated_at` wins regardless of file order. Equal `updated_at` with different content is an error. |
| Confirmed links | Only `confirmed_bug_links` creates `CONFIRMED_AS`. Free text is never parsed. |

A confirmed bug reference whose bug is missing from the export is a **validation error**, unless it is listed in `SeedIngestion:DocumentedUnresolvedBugReferences` in `apps/api/ResolveNest.Api/appsettings.json`. Today the only entry is TCK-1002 → BUG-190. For a documented reference:
- A warning is printed.
- No placeholder and no `CONFIRMED_AS` are created.
- The raw value is kept as `unresolvedBugRefs` on the ticket.

An incomplete entry is an error, and an entry that no longer occurs in the source is a warning.

## Knowledge articles

### Source contract

The manifest's `records` are the source of truth. Each record has:
- `id`
- `source_system`
- `source_id`
- `file`
- `title`
- `summary`
- `feature_refs[]`
- `bug_refs[]`
- `updated_at`

The envelope's `synthetic` flag applies to every article.

### KnowledgeArticle properties

| Property | Source |
|---|---|
| `id` | manifest `id`, which must equal `kb:` + `source_id` (for example `kb:KB-012`) |
| `title`, `summary` | manifest |
| `content` | The complete Markdown file: heading, banner, tables, lists and code spans unchanged. A leading UTF-8 BOM is removed, and CRLF/CR line endings are normalized to LF so the content is identical on every platform. This is the single property for future evidence retrieval. |
| `sourceSystem`, `sourceId` | manifest |
| `sourceRecord` | manifest record pointer, for example `data/seed/knowledge/manifest.json#/records/5` (it asserts the metadata and relationships) |
| `sourcePath` | the Markdown file, for example `data/seed/knowledge/KB-012-csv-export-err-export-serialize.md` |
| `updatedAt` | manifest `updated_at` as `DATETIME` |
| `ingestedAt` | time of the import run |
| `ingestedBy` | `knowledge-ingestion` |
| `synthetic` | manifest envelope flag |

No chunks, embeddings or full-text indexes are created.

### Relationship mapping

Each relationship comes only from an explicit manifest reference. Article text is never searched.

| Manifest field | Relationship | Target | Properties |
|---|---|---|---|
| each `bug_refs[]` entry | `(KnowledgeArticle)-[:ADDRESSES]->(Bug)` | `bug:` + trimmed, uppercased ref | `sourceSystem`, `sourceRecord`, `ingestedAt`, `ingestedBy`, `sourceBugRef` (raw) |
| each `feature_refs[]` entry | `(KnowledgeArticle)-[:COVERS]->(Feature)` | `feature:` + trimmed, lowercased ref | same, with `sourceFeatureRef` (raw) |

An article may address several bugs and cover several features. Each distinct reference produces exactly one relationship. Listing the same reference twice in one article is an error, not a merge.

### Validation (before any database access)

Any of these failures is an error, and the database is not contacted:

| Check | Error when |
|---|---|
| Manifest | Not valid UTF-8 or JSON, or no `records` array |
| Required fields | `id`, `source_system`, `source_id`, `file`, `title`, `summary` or `updated_at` missing or empty; `updated_at` not ISO 8601; `feature_refs` missing or empty; `bug_refs` missing (an empty array is allowed) |
| Identity | `source_id` not `KB-<3+ digits>` in canonical case; `id` ≠ `kb:<source_id>`; duplicate article id; two records sharing a file |
| File path | Rooted or drive-qualified path; an empty, `.` or `..` segment; not `.md`; resolves outside the knowledge directory; missing; a link (reparse point) |
| Content | Not valid UTF-8; first non-empty line is not exactly `# <manifest title>`; fewer than 80 non-whitespace characters after the heading |
| References | `bug_ref` not `BUG-<digits>` or `feature_ref` not a lowercase slug after normalization; the same reference twice in one article |

A Markdown file in the directory that the manifest does not list gives a warning and is not imported.

## Tests (local only)

`tests/ResolveNest.Api.IngestionTests/` is an xUnit project excluded by `.git/info/exclude` and not part of `ResolveNest.slnx`. Run it with `dotnet test tests/ResolveNest.Api.IngestionTests`. It has 41 tests.

- **Seed builder and resolver (11):** unresolved-reference rules, nullable keys, ownership markers, display paths, and validation of the committed dataset.
- **Seed Neo4j behavior (6), rolled back:**
  - property preservation and null removal
  - coexistence with knowledge data
  - rejection of re-owned, conflicting or unmarked foreign seed records
- **Knowledge validation (19), using temporary directories:**
  - multiple references, and articles with no bugs
  - path traversal, absolute and drive paths, non-`.md` files
  - missing file and invalid UTF-8
  - heading mismatch and an empty body
  - duplicate ids and duplicate references
  - malformed references, a non-canonical id and invalid JSON
  - CRLF normalization
  - the committed manifest producing 30 articles, 44 ADDRESSES and 39 COVERS, with exact KB-012 content
- **Knowledge Neo4j behavior (5), rolled back:**
  - full write and counts
  - a foreign property on Bug preserved
  - seed preflight and seed merge afterwards, with knowledge relationships intact
  - a repeated write creating nothing
  - missing bug and feature references reported
  - articles that are seed-owned, conflicting or unmarked rejected
  - re-owned or unmarked ADDRESSES and COVERS rejected

## Verified results

Neo4j 2026.10.1 Community, 2026-11-03.

**Before knowledge ingestion:** 428 nodes and 977 relationships, all seed-owned, 6 constraints, 0 KnowledgeArticle.

**Expected from the manifest:** 30 articles, 44 `bug_refs` and 39 `feature_refs`.

**Dry run:** 30 articles planned, ADDRESSES=44 and COVERS=39 (44 distinct bugs, 12 distinct features), 0 warnings, 0 errors.

| Run | nodesCreated | relationshipsCreated | Existing before |
|---|---|---|---|
| `ingest-knowledge` #1 | 30 | 83 | 0 KnowledgeArticle, 428 other |
| `ingest-knowledge` #2 | **0** | **0** | 30 KnowledgeArticle, 428 other |
| `ingest-seed` afterwards | **0** | **0** | 428 seed-labeled, 30 other |
| `ingest-knowledge` after seed | **0** | **0** | 30 KnowledgeArticle, 428 other |

Final graph: **458 nodes and 1,060 relationships** (977 + 44 + 39).

| Label | Nodes | | Relationship | Count |
|---|---|---|---|---|
| Customer | 40 | | REPORTED | 300 |
| Feature | 12 | | ABOUT_FEATURE | 362 |
| Release | 16 | | CONFIRMED_AS | 155 |
| Bug | 60 | | AFFECTS | 67 |
| SupportTicket | 300 | | INTRODUCED_IN | 52 |
| KnowledgeArticle | 30 | | FIXED_IN | 41 |
| | | | ADDRESSES | 44 |
| | | | COVERS | 39 |

Checks after the runs:
- **Exact pairs:** the 83 article→target pairs in the graph match the 83 manifest references exactly, with 0 missing and 0 not from the manifest.
- **Content:** each of the 30 stored `content` values has the same length as its normalized source file. A local test compares the full KB-012 text.
- **Metadata:** all 30 articles have every property, `ingestedBy = knowledge-ingestion` and `synthetic = true`. All 83 knowledge relationships carry provenance.
- **Seed records:** all 428 seed nodes and 977 seed relationships are still `seed-ingestion`.
- **Acme path:** `customer:acme` → TCK-1046 (0 `CONFIRMED_AS`) → csv-export ← AFFECTS ← BUG-231 → INTRODUCED_IN 2.3.0, FIXED_IN 2.3.2. KB-012 now ADDRESSES BUG-231 and COVERS csv-export. BUG-231 still has 21 confirmed tickets.
- **Invalid input:**
  - An external copy with a `../../outside.md` file and a `BUG 12` reference got 2 errors and exit 1, and the database was not contacted.
  - A copy referencing `BUG-999` and `billing` got 2 missing-reference problems and exit 2, and the transaction was rolled back.
  - The graph stayed at 458/1,060.
- **Timestamps:** every run, seed or knowledge, rewrites `ingestedAt` on the nodes and relationships it owns with that run's timestamp. All other properties, including `updatedAt` from the source, stay stable across repeated runs.

## Limitations

- **Upsert only:** removed source records, articles and links persist. There is no deletion or synchronization.
- **Knowledge depends on the seed:** knowledge ingestion requires the seed graph, and missing Bug or Feature nodes abort the run. A dry run cannot check references against the graph.
- **Ownership is a convention:** it is enforced by the preflights and Neo4j cannot enforce it. A writer that sets another ingestion's marker would be treated as that ingestion.
- **Seed legacy allowance:** seed still accepts unmarked records that match the plan (see above).
- **Content checks:** Markdown is stored verbatim apart from line-ending and BOM normalization. It is not parsed, rendered or sanitized. Treat it as untrusted input when it is later shown or sent to an LLM.
- **Unexercised paths:** exit codes 3, 4 and 5 and Ctrl+C were reviewed but not triggered against a live database.
