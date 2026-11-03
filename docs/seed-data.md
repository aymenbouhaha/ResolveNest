# Seed data

**Everything in `data/curated/` and `data/seed/` is synthetic.** Ledgerline, the product being supported, and every customer, ticket, bug, release and article are fictional. Agent handles such as `agent.l2.03` do not identify real people. Each file carries a `synthetic` flag (a CSV column, a JSON envelope field, or a banner in each Markdown article).

## Layout and generation

| Path | Role |
|---|---|
| `data/curated/` | Hand-authored source of truth: the 10 core customers, 12 features, 16 releases, 50 scenario tickets (TCK-1001 to TCK-1050), 20 core bugs and all 30 knowledge articles plus their manifest. |
| `data/scripts/generate-seed.mjs` | Deterministic generator. It copies the curated records and adds 30 background customers, 40 background bugs and 250 background tickets (TCK-2001 to TCK-2250) from fixed templates. |
| `data/scripts/seed-format.mjs` | CSV parsing and formatting, reference normalization and version comparison, shared by the generator and the validator. |
| `data/scripts/validate-seed.mjs` | Validator for `data/seed/` (see [Validation](#validation)). |
| `data/seed/` | Generated output that ingestion reads. It is committed so the demo works without regenerating. |

The generator uses no random numbers, no clock and no network. Every choice is derived from template and instance indexes (for example `index % 11`), so the same curated input always produces byte-identical output. It deletes and rewrites `data/seed/` on every run. Never edit `data/seed/` by hand: change `data/curated/` or the generator, then regenerate.

Generated tickets belong only to the 30 generated customers. The 10 curated customers keep their hand-written timelines (for example Acme stays on 2.2.1 until it upgrades on 24-25 October 2026), so background tickets cannot contradict the demo story.

## Files, counts and simulated source systems

| File | Records | `source_system` | Graph label |
|---|---|---|---|
| `customers.csv` | 40 (10 curated + 30 generated) | `synthetic-crm` | `Customer` |
| `features.csv` | 12 | `synthetic-product-catalog` | `Feature` |
| `releases.csv` | 16 (1.9.0 to 2.4.2) | `synthetic-release-registry` | `Release` |
| `tickets.json` | 302 raw records, 300 unique tickets | `synthetic-helpdesk` | `SupportTicket` |
| `bugs.json` | 60 (20 curated + 40 generated) | `synthetic-issue-tracker` | `Bug` |
| `knowledge/manifest.json` + 30 `*.md` | 30 | `synthetic-kb` | `KnowledgeArticle` (not ingested yet) |

All timestamps are ISO 8601 UTC. The dataset's "now" is `exported_at` = 2026-11-02T23:00:00Z, and no record is dated after it. Ticket numbers are monotonic within each range (TCK-1xxx is the curated scenario queue, TCK-2xxx the background queue), but the two ranges overlap in time.

Releases: 1.9.0 (2025-07-15), 1.9.1, 2.0.0, 2.0.1, 2.0.2, 2.0.3, 2.1.0, 2.1.1, 2.2.0, 2.2.1, 2.3.0, 2.3.1, 2.3.2 (2026-08-04), 2.4.0, 2.4.1 and 2.4.2 (2026-10-27).

Features: `csv-export`, `pdf-export`, `scheduled-reports`, `dashboards`, `data-import`, `public-api`, `api-tokens`, `sso-login`, `user-roles`, `webhooks`, `connector-sync`, `api-rate-limits`.

## Identity and reference conventions

Every record has a canonical `id` (`customer:acme`, `ticket:TCK-1046`, `bug:BUG-231`, `feature:csv-export`, `release:2.3.2`, `kb:KB-012`) and a raw `source_id` (`ACC-0001`, `TCK-1046`, `BUG-231`, `csv-export`, `v2.3.2`, `KB-012`). Customers are referenced by `account_slug`.

Cross-record references hold **raw values as the source system recorded them**:

| Field | In | Refers to | Relationship |
|---|---|---|---|
| `customer_ref` | tickets | `customers.account_slug` | `REPORTED` |
| `feature_refs[]` | tickets / bugs / KB manifest | `features.source_id` | `ABOUT_FEATURE` / `AFFECTS` / `COVERS` |
| `confirmed_bug_links[].bug_ref` | tickets | `bugs.source_id` | `CONFIRMED_AS` |
| `introduced_in`, `fixed_in` | bugs | `releases.version` | `INTRODUCED_IN`, `FIXED_IN` |
| `bug_refs[]` | KB manifest | `bugs.source_id` | `ADDRESSES` |

Other key fields:
- **`confirmed_bug_links[]`:** `{bug_ref, confirmed_by, confirmed_at}`. These are the only source of `CONFIRMED_AS`.
- **`internal_notes` and `resolution`:** free text. Bug IDs they mention are never links (the validator reports 11 such mentions).
- **Resolution prefixes:** configuration problems start with `Configuration:`, entitlement or plan limits with `Not a defect:`, and unexplained outcomes with `No root cause identified` or `Closed without customer response.`
- **`bugs.status`:** `fixed` exactly when `fixed_in` is set. `introduced_in_evidence` and `fix_evidence` are set exactly when their version is.
- **`releases.notes`:** each release lists exactly the bugs whose `fixed_in` is that release (cross-checked by the validator). Ingestion still takes `FIXED_IN` only from `bugs.json`.

## Scenario A: CSV-export regression (primary demo)

Acme upgraded its dedicated environment from 2.2.1 to "2.3" on 24-25 October 2026, and CSV export of a report with custom date fields now fails.

| Record | Role |
|---|---|
| `customer:acme`, `ticket:TCK-1046` | The demo ticket. `customer_ref` is `" Acme"`, `reported_version` is `"2.3"` (patch unknown), status `open`. **No confirmed bug link**: triage is pending. |
| `bug:BUG-231` | ERR_EXPORT_SERIALIZE on custom date fields. `AFFECTS` csv-export and scheduled-reports. `INTRODUCED_IN` 2.3.0, `FIXED_IN` 2.3.2 (evidence: PR-4471 bisect, PR-5120 and the 2.3.2 release notes). |
| `kb:KB-012` | Known-issue article for BUG-231 with the XLSX workaround. Related: KB-003, KB-011, KB-031 (delimiter configuration), KB-039. |

`CONFIRMED_AS` → BUG-231 links **21 tickets from 21 customers, none of them Acme**:
- Curated: TCK-1017, 1018, 1020, 1024, 1027, 1033.
- Generated: TCK-2121, 2123, 2132, 2133, 2134, 2140, 2141, 2143, 2145, 2151, 2152, 2153, 2157, 2158, 2159.
- All report 2.3.0 or 2.3.1.

**Fix shipped ≠ customer resolved:** TCK-1027, TCK-2151 and TCK-2153 are still `pending_customer` after 2.3.2 shipped. Resolved tickets record the customer's own confirmation after upgrading.

Similar csv-export tickets **without** a confirmed link:
- Curated: TCK-1034, 1046, 1050.
- Generated: TCK-2125, 2126, 2135, 2142, 2147, 2154, 2160. These are vague reports with no error code, and their notes say "possibly BUG-231" as text only.

Distractors in the same feature:
- BUG-238 (timeouts above 500k rows)
- BUG-233 (column order)
- BUG-159 (quote escaping, 2.0.x)
- BUG-205 (timezone)
- BUG-261 (file name)
- Six `Configuration:` tickets about semicolon delimiters

A grounded answer can say:
- Other customers hit the same error on 2.3.0 and 2.3.1.
- BUG-231 is confirmed for 21 of them. It was introduced in 2.3.0 and fixed in 2.3.2.
- Acme matches closely, but its ticket is unconfirmed and its patch version is unknown.
- Recommend checking the version, upgrading to 2.3.2 or later, and using XLSX meanwhile.

It must not claim that Acme's ticket is confirmed or that Acme's issue is resolved.

## Scenario B: HTTP 403 and permissions

The validator finds 57 tickets mentioning 403.

**15 are confirmed product defects** (only these bugs):
- BUG-244, expired token returns 403: TCK-1013, 2099, 2118, 2218, 2250.
- BUG-253, Analyst role gets 403 in shared folders on 2.4.0, fixed in 2.4.1: TCK-2196, 2199, 2205, 2207, 2211, 2219.
- BUG-235, scope picker: TCK-2119, 2124.
- BUG-210, 404 instead of 403: TCK-2139, 2209.

**42 have no bug link.** Most are `Configuration:` or `Not a defect:`, with these causes:
- missing scope
- IP allowlist
- role permission
- starter plan
- revoked token
- SSO enforcement disabling legacy keys

A few are `No root cause identified` or `Closed without customer response.` (the "intermittent 403" tickets with insufficient information).

KB-015, KB-016, KB-007, KB-029 and KB-034 explain the configuration causes. The investigator must not attribute an unlinked 403 to any bug.

## Scenario C: Webhook delivery delays

31 tickets report late webhooks:
- Curated: TCK-1011, 1023, 1030, 1035, 1039, 1043, 1047.
- Generated: 24 tickets.

They span 2.0.x to 2.4.x, cloud and dedicated customers, and statuses `open`, `resolved` and `closed`. **None has a confirmed link.**

The notes offer competing explanations:
- receiver 5xx and 429 responses
- proxies
- "customer suspects the 2.3.0 queue migration", contradicted by pre-2.3.0 reports
- "could be related to BUG-247, not linked"

`bug:BUG-247` (retry backoff ignores Retry-After) is open, `AFFECTS` webhooks and has no confirmed tickets.

Webhook tickets that do have confirmed bugs are about other symptoms:
- secret rotation: BUG-213
- missing `occurred_at`: BUG-256
- test button: BUG-167
- log units: BUG-189

The investigator should describe the pattern, list the hypotheses and say that no cause is confirmed.

## Background issues

Generated tickets also cover:
- **API rate limits:** 429 plan limits as `Not a defect:`, plus BUG-173, BUG-203 and BUG-251.
- **Authentication:** SSO and MFA configuration, plus BUG-161, BUG-185 and BUG-260.
- **Dashboard performance:** BUG-257 (2.4.0 to 2.4.1) and a configuration case.
- **Pagination:** BUG-163 and BUG-224.
- **Report failures:** BUG-165, BUG-153, BUG-206 and BUG-255.
- **Connector sync delays:** BUG-171, BUG-201, BUG-211 and BUG-258, plus ambiguous delays.
- **Integration configuration errors:** expired connector credentials, field mapping, import date formats.
- **Smaller issues:** PDF, roles and token defects.

Every generated bug has at least one confirmed ticket except BUG-247.

## Intentional data-quality anomalies

| Anomaly | Where | Expected ingestion behavior |
|---|---|---|
| Customer ref casing and whitespace | 58 tickets, for example TCK-1046 `" Acme"`, TCK-1038 `"ACME"`, TCK-1024 `"Orchard "`, TCK-1033 `"PINECREST"`, generated `"ALDER"` | Trim and lowercase, then match `account_slug`. Keep the raw value. |
| Bug ref casing | TCK-1020 `"bug-231"` | Trim and uppercase, then match `bugs.source_id`. |
| `v` prefix or surrounding whitespace in versions | 36 tickets (for example TCK-1027 `"v2.3.1"`), BUG-240 `fixed_in` `"v2.4.0"`, BUG-173 `fixed_in` `" 2.0.3"` | Trim and strip `v`/`V`. These resolve to exact releases. |
| Two-part ticket versions | 21 tickets (for example TCK-1046 `"2.3"`) | Store raw. Normalized `"2.3"` with precision `minor`. Never coerce to 2.3.0. |
| Unparseable ticket version | TCK-2144 `"latest"` | Store raw only. Report a warning. |
| Missing ticket version | 4 tickets (for example TCK-1029) | Leave unset. |
| Ambiguous bug releases | BUG-215 `introduced_in` `"2.2"`, BUG-181 `introduced_in` `"2.0"` | No `INTRODUCED_IN`. Report a warning. |
| Missing optional fields | Empty `region` for quillfield and meridian; null `introduced_in`, `workaround` or notes on several records | Leave unset. Create no relationship. |
| Ticket without features | TCK-2097 (`feature_refs: []`) | No `ABOUT_FEATURE`. Report a warning. |
| Duplicate export records | TCK-1023 (older record first, newer later); TCK-2180 (newer record first, an older snapshot appended at the end of the file) | The record with the latest `updated_at` wins regardless of file order. |
| Unresolvable bug ref | TCK-1002 → BUG-190 (archived before the export) | Allowed only because it is listed in `SeedIngestion:DocumentedUnresolvedBugReferences` (`apps/api/ResolveNest.Api/appsettings.json`). Warn, keep the raw ref as `unresolvedBugRefs`, and never create a placeholder `Bug` or `CONFIRMED_AS`. Any other unresolved confirmed ref fails validation. |
| Bug IDs in free text | 11 mentions (for example TCK-1030 → BUG-247, TCK-1034 → BUG-231) | Never create `CONFIRMED_AS` from text. |

The ingestion command implements these rules and reports each anomaly as a warning. See [ingestion.md](ingestion.md).

## Validation

```bash
node data/scripts/generate-seed.mjs
node data/scripts/validate-seed.mjs
```

The validator (Node 20 or later, no dependencies, never connects to Neo4j) runs 93 checks:
- Parsing, record counts and synthetic labels.
- ID format and uniqueness.
- Every cross-file reference, with normalization.
- That tickets come after their customer's start date, and that no timestamp is after `exported_at`.
- Bug status and evidence consistency, release chronology, and release notes against `fixed_in`.
- That ticket versions are already released and inside the confirmed bug's affected range.
- The Scenario A, B and C assertions.
- That the set of anomalies is exactly the documented one.

It prints per-file hashes and one combined hash for `data/seed/`, and exits non-zero on any failure.

Verified on 2026-11-03:
- **Checks:** 93/93 passed.
- **Determinism:** two generator runs produced the same combined hash, `c71a5e3c…d5e3d8`.
- **Negative test:** a mutated copy (an Acme confirmed link, a fabricated BUG-247 link, a future timestamp) failed 6 checks with exit code 1.
