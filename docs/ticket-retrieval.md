# Persistent ticket retrieval

`GET /api/tickets` returns a compact page of persisted `SupportTicket` records. It does not invoke Gemini, return investigation evidence, persist reply drafts, or modify graph data.

## Query parameters

| Parameter | Contract |
| --- | --- |
| `page` | Positive integer, default `1`; out-of-range pages return an empty items array with the matching total. |
| `pageSize` | Integer from `1` through `100`, default `25`. |
| `q` | Optional trimmed, case-insensitive literal substring of `sourceId` or `subject`; blank means no search. |
| `status` | Optional exact, case-sensitive status value. Unknown values return no matches. |
| `sort` | `updatedAt_desc` (default), `updatedAt_asc`, `createdAt_desc`, `createdAt_asc`, `sourceId_asc`, or `sourceId_desc`. |

Every ordering places missing sort values last and uses ascending canonical ticket ID as its final tiebreaker. Update-time ordering falls back to creation time when update time is absent. The returned `updatedAt` itself remains null when absent. User values are Cypher parameters; only server-defined ordering expressions enter query text.

## Response and errors

The envelope contains `items`, `page`, `pageSize`, and `totalCount`. Each item contains `id`, `sourceId`, `subject`, `status`, `priority`, `reportedVersion`, `createdAt`, `updatedAt`, and `customer`. Dates are serialized ISO 8601 timestamps or null. All summary fields except canonical ticket ID may be null. `customer` is null when no reporting customer exists, otherwise `{ id, name }`, with nullable name.

Customers are retrieved through `(Customer)-[:REPORTED]->(SupportTicket)`. If multiple customers are linked, the lowest canonical customer ID is selected, keeping one item per ticket. Counting happens before customer traversal. Pagination has no overall record cap. Count and page queries execute in one managed read transaction with a 15-second transaction timeout and reuse the singleton Neo4j driver.

Invalid numeric parameters or unsupported sorts return HTTP 400 validation ProblemDetails. Neo4j errors return sanitized HTTP 503 ProblemDetails. Unexpected result shapes use the existing graph-result validation and return sanitized HTTP 500 ProblemDetails. Request cancellation is passed to result consumption.

Offset pagination is deterministic for an unchanged graph. Concurrent changes can shift records between separate page requests; there is no snapshot token or cursor across requests. Search uses substring matching, without a new index or schema change.

## Frontend flow

The typed tickets client uses the shared Axios instance and validates the response shape. `useTickets` owns search, page, loading/error state, retry, and request cancellation. Superseded requests cannot replace current results. Desktop and mobile navigation share the same hook and search text.

The first returned ticket becomes the initial selection. No ticket ID is assumed. Selection is stored independently of the current page, so search, pagination, refreshes, and list failures preserve the current investigation. The existing investigation hook still loads the selected canonical ID through the investigation endpoint. An empty database renders a selection-free state without calling the investigation endpoint.

Search, page changes, list refresh/retry, and ticket switches are disabled during the existing reply workflow navigation lock. Investigation and reply services are unchanged. Production code no longer imports `demoTickets.ts`; that unused file is retained.

## Verification

Focused tests live in `tests/ResolveNest.Api.InvestigationApiTests/TicketListEndpointTests.cs`. The runtime-verification request explicitly authorizes tracking this file, its project and fixture, and the safe browser configuration as an exception to the local-only test policy. Other local tests remain excluded. See `tests/ResolveNest.Api.InvestigationApiTests/README.md` for prerequisites and the read-only verification workflow. Run:

```bash
dotnet test tests/ResolveNest.Api.InvestigationApiTests --filter FullyQualifiedName~TicketListValidationTests
dotnet test tests/ResolveNest.Api.InvestigationApiTests --filter FullyQualifiedName~TicketListEndpointTests
dotnet build ResolveNest.slnx
cd apps/web
npm run build
npm run lint
```

Validation tests cover defaults, invalid parameters, large offsets, null mapping, and sanitized database failures. Endpoint integration tests require an already running, populated local Neo4j instance and cover complete multi-page traversal against persisted IDs, supported orderings, combined filters, literal search input, and empty results. They issue only read queries and compare graph counts before and after traversal.

No frontend test runner is configured; frontend static verification uses TypeScript, production build, and ESLint. The test-only Vite configuration in `tests/browser/ticket-runtime.config.mjs` supports browser verification of pending approval and failure recovery without calling a real mutation endpoint. Successful real reply persistence is outside this read-only verification.
