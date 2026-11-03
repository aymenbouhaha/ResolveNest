# Investigation Workspace (frontend)

Phase 5, Task 1. A read-only React workspace over `GET /api/investigations/tickets/{ticketId}` (see [investigation-api.md](investigation-api.md)). It has no AI question answering, no reply drafting and no writes.

Code: `apps/web/src/`.

## Stack and setup

| Package | Version | Why |
|---|---|---|
| `tailwindcss`, `@tailwindcss/vite` | 4.3.x | Tailwind v4 through the official Vite plugin; no `tailwind.config` file |
| `shadcn` (CLI 4.23, style `radix-nova`) | 4.23.x | Generated, editable component source in `src/components/ui/`. The package is a runtime dependency because `index.css` imports `shadcn/tailwind.css` |
| `radix-ui`, `class-variance-authority`, `cn`, `tw-animate-css`, `@fontsource-variable/geist` | added by `shadcn init` | Accessible primitives, variants, class merging (`cn` replaces `clsx` + `tailwind-merge` in this CLI version), animations, font |
| `lucide-react` | 1.x | Icons |
| `axios` | 1.21.x | HTTP client |

- `@/*` resolves to `src/*` in `tsconfig.json`, `tsconfig.app.json` and `vite.config.ts` (`fileURLToPath(new URL('./src', import.meta.url))`, because the config is ESM and has no `__dirname`).
- The `/api` proxy to `http://localhost:5080` is unchanged.
- Installed primitives, each used by the UI: accordion, alert, badge, button, card, separator, sheet, skeleton, tooltip.
- ESLint disables `react-refresh/only-export-components` for `src/components/ui/**` only, because the generated button and badge also export their `cva` variants. Generated files stay as the CLI wrote them, so later `shadcn add` runs do not conflict.

## Theme

Light theme only. Slate neutrals and an indigo primary replace the CLI's neutral palette in `src/index.css`. Evidence classes have their own semantic tokens (`--evidence-{confirmed|documented|candidate|context}` plus `-surface` and `-border`), exposed to Tailwind as `bg-evidence-*`, `text-evidence-*`, `border-evidence-*`: emerald for confirmed, sky for documented, amber for candidate, slate for context. Every badge also carries a text label and an icon, so color is never the only signal. The generated `.dark` block is kept but nothing toggles it.

## Structure

```
src/
├── App.tsx                         selected ticket state, TooltipProvider
├── lib/axios.ts                    shared Axios instance
├── components/layout/              AppShell (sidebar + mobile Sheet), AppSidebar, AppHeader
├── components/ui/                  shadcn primitives
└── features/investigations/
    ├── api/                        fetchTicketInvestigation, response guard, error normalization
    ├── hooks/useInvestigation.ts   request lifecycle
    ├── types/investigation.ts      TypeScript mirror of TicketInvestigationResponse
    ├── lib/                        labels, formatters, limitation summary
    ├── components/                 evidence sections and cards
    ├── demoTickets.ts              the four static demo ticket IDs and labels
    └── InvestigationWorkspace.tsx
```

The demo list holds only ticket IDs and navigation labels. Every fact on screen comes from the API response.

## HTTP layer

- One Axios instance: `baseURL: '/api'`, 20 s timeout, `Accept: application/json`. No interceptors, no credentials, no retries: there is no authentication design yet, and retry is a user action.
- The ticket ID is passed through `encodeURIComponent` (`ticket%3ATCK-1046`), which the API accepts.
- `includeContent=true` is always sent. It adds about 6 KB for Acme and lets the knowledge cards show the article Markdown on demand without a second request.
- A light structural guard checks the payload (`ticket.id`, `counts`, the five lists, `customer` object or null). A mismatch becomes an `unexpectedResponse` error rather than a render crash. It is not full schema validation.

### Error mapping

| Condition | Kind | Retry offered |
|---|---|---|
| Cancelled (`ERR_CANCELED`) | ignored | – |
| Timeout (`ECONNABORTED`, `ETIMEDOUT`) | `timeout` | yes |
| No response | `backendUnavailable` | yes |
| 502/504, or a 5xx without Problem Details (the Vite proxy answers 502 `text/plain` when the API is down) | `backendUnavailable` | yes |
| 400 | `invalidRequest`, with the `errors` map as detail | no |
| 404 | `notFound`, with the API's `detail` | no |
| 503 | `databaseUnavailable` | yes |
| other 5xx with Problem Details | `serverError` | yes |
| payload fails the guard | `unexpectedResponse` | yes |

The error panel shows the friendly message, the ticket ID and a technical detail line (status, Problem Details title and detail).

## Request lifecycle

`useInvestigation(ticketId)` keeps one settled result tagged with a request key (`ticketId#attempt`). The rendered state is that result only when its key matches the current key; otherwise it is loading. Each effect run creates an `AbortController`, aborts it on cleanup, and ignores results from an aborted controller. So:

- switching tickets aborts the previous request and shows the skeleton immediately;
- a late response for an earlier ticket can never be rendered;
- `reload()` bumps `attempt`, which re-runs the request (Retry and Refresh buttons);
- no `setState` runs synchronously inside the effect.

No query library is used; one hook covers the four requirements.

## Evidence presentation rules

- Order and classification come from the API. The UI never re-ranks, filters, promotes or demotes. Potential-issue cards show the API's position (`#1 of 6`).
- Confirmed and potential issues are separate sections. An empty confirmed section says that no confirmed product issue is linked; the potential-issues section says none of its entries is confirmed.
- Source text (descriptions, resolutions, guide Markdown) is rendered as React text nodes; guide Markdown is shown as preformatted text, never as HTML.

## Product language layer (Task 1.1)

The UI speaks in support terms, not in graph or API terms. The DTOs, evidence values, IDs and provenance fields are unchanged and stay in application state; only their presentation changed. The translation lives in small, typed helpers in `features/investigations/lib/`:

| Helper | Responsibility |
|---|---|
| `evidenceLabels.ts` | Evidence class labels and definitions, relevance tier labels and one-sentence summaries, error-code, version and release-bound labels |
| `relevanceNoteText.ts` | Builds each candidate note from its stable `code` and the structured DTO fields (matched codes, versions, counts, feature names). It never parses the backend's English message. An unknown code falls back to the backend message unchanged |
| `knowledgeRouteText.ts` | Turns a guide's route (`COVERS` / `ADDRESSES` + evidence) into a sentence; anything unrecognized becomes a neutral "Related to X" |
| `recordReferences.ts` | Readable references (`ticket:TCK-1046` → `TCK-1046`), customer slugs (`customer:kestrel` → `Kestrel`), and known source systems (`synthetic-issue-tracker` → `Issue tracker`) |
| `investigationLimitations.ts` | Splits ticket-level open questions ("What remains uncertain") from list truncation ("Investigation coverage") |

### Terminology

| Internal | Shown as |
|---|---|
| `confirmed` / `documented` / `candidate` / `context` | Confirmed / Documented / Possible / Background |
| Confirmed bugs | Confirmed issues |
| Candidate bugs | Potential related issues |
| Related tickets | Similar support cases |
| Supporting / limitations notes | Why this may be related / Caveats |
| INTRODUCED_IN / FIXED_IN | Introduced in version / Fixed in version, under "Release history" |
| Knowledge articles, `reachedVia` | Troubleshooting guides, "Why it is listed" |
| `sourceSystem` + `sourceId` | "Source record: Issue tracker · BUG-231" |
| Tiers | Matching error code / Version may match / Same feature only / Error matches, dates don't align / Version mismatch |

Not shown anywhere: graph paths, relationship type names, `sourceRecord` / `relationshipSourceRecord` file paths, canonical prefixed IDs, `reason` strings, the `basis` text, and node labels.

### Avoiding repeated disclaimers

Three candidate notes describe the ticket rather than the issue: `ticketClosedWithoutConfirmedBug`, `ticketHasConfirmedBug` and `patchVersionUnknown`. They are shown once (in "What remains uncertain", in the potential-issues section description, and under the reported version) instead of on every card. When a card has `errorCodeMatchContradictedByIntroduction`, the overlapping `versionOutsideDocumentedRange` caveat is not repeated. The per-card fix caveat stays, because it names that issue's fix release.

### Errors

Primary error text names the investigation service, not HTTP, Axios, the Vite proxy or the database engine. The status code and Problem Details title and detail stay available under a collapsed "Technical details" item.

## Verification (2026-11-03)

Run: `npm run build` and `npm run lint` (both clean); manual testing in the desktop app's browser against the live API and local Neo4j.

- All four demo tickets load through `/api` with the encoded ID. Acme: 0 confirmed, BUG-231 first as a candidate with `ERR_EXPORT_SERIALIZE`, 21 tickets and 21 customers elsewhere, introduced 2.3.0 and fixed 2.3.2, patch version unknown, KB-012 routes `candidate` and `documented`. TCK-1017: BUG-231 confirmed, KB-012 route confirmed. TCK-1006: BUG-244 third as a chronology conflict, configuration resolution shown. TCK-1043: BUG-247 shared feature only.
- Spaced rapid switching across five tickets ended on the last selection with matching content.
- With the API stopped, the proxy 502 shows "Investigation service unavailable" (Task 1.1 wording); "Try again" after restarting the API loads the ticket.
- Task 1.1: the rendered text of all four tickets, with every accordion expanded and all `aria-label`s included, contains no graph, relationship-type, canonical-ID, file-path or source-system terms. Backend order and classifications are unchanged.
- 404 (`ticket:TCK-0000`), 400 (`TCK-1046`) and cancellation map as in the table above (called through the API module in the page).
- 375 px width: no horizontal scroll, Sheet navigation opens, selecting a ticket closes it and loads the ticket. 1440 px: sticky summary rail.
- No console errors. No credentials or backend host in the built bundle.

Not verified: timeout and 503 paths in the browser, screen-reader output, and request abortion as observed in the network log (local responses were too fast to show cancelled entries; stale protection was checked through the rendered result).

## Known limitations

- The selected ticket is not in the URL, so a reload returns to Acme.
- Only the four demo tickets are selectable; there is no ticket search API.
- The main bundle is about 465 KB (149 KB gzip), with no code splitting.
- `npm audit` reports a moderate `minimatch` advisory through the existing ESLint toolchain (dev only), unrelated to the packages added here.
