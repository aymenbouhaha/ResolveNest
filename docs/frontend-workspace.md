# Investigation Workspace (frontend)

Phase 5, Tasks 1 and 2. A React workspace over `GET /api/investigations/tickets/{ticketId}` (see [investigation-api.md](investigation-api.md)) with an "Ask ResolveNest" panel over `POST …/ask` (see [gemini-integration.md](gemini-integration.md) and [Ask ResolveNest](#ask-resolvenest-task-2)). Task 3 adds a human-reviewed reply draft panel; its approval step is the only write, and nothing sends email.

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
- Source text (descriptions, resolutions) is rendered as React text nodes. Guide Markdown is rendered through the restricted renderer described in [Task 4.2](#qa-accessibility-and-polish-task-42), never as raw HTML.

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

## Ask ResolveNest (Task 2)

A panel directly under the ticket overview sends one question about the selected ticket to `POST /api/investigations/tickets/{ticketId}/ask` and shows one structured answer. It never calls the AI provider from the browser, holds no provider configuration, keeps no history and stores nothing.

### Code

| File | Responsibility |
|---|---|
| `types/investigationAnswer.ts` | Mirror of `AiInvestigationAnswer`, `AiFinding`, `AiStatement`, `CitationSource`, `CitedRelationship`, `AiAnswerProvenance`, `AskInvestigationRequest` |
| `api/investigationAskApi.ts` | `askInvestigationQuestion(ticketId, question, { signal })`: shared Axios client, encoded ticket ID, `{ question }` body, 75 s timeout (server: 45 s provider timeout, 60 s HTTP client) |
| `api/investigationAnswerGuard.ts` | Light shape check: known `status`, string `answer`, three statement lists with `citationIds`, `validatedSources` with `citationId`, and `ticketId` equal to the requested ticket |
| `api/investigationAskErrors.ts` | Maps Problem Details (`reason` extension first, then status) to product messages |
| `lib/questionRules.ts` | Client copy of the server's question rules: trimmed 5–1,000 characters, no control characters except tab, newline and carriage return |
| `hooks/useInvestigationAsk.ts` | `idle → asking → answered | failed`, one `AbortController` per request |
| `lib/citationLabels.ts` | Readable labels for cited records and connections; finding-kind labels |
| `lib/answerTextFormatting.ts` | Display-only replacement of prefixed IDs in AI text (`bug:BUG-231` → `BUG-231`, `customer:acme` → `Acme Corporation`) |
| `components/answer/*` | Form, loading, error and answer presentation |

### Request lifecycle

- Nothing is sent when a ticket loads or when an example question is clicked; examples only fill the field.
- Submit (button or Ctrl/Cmd+Enter) is disabled for invalid input and while a request is active. A second submit during a request is ignored by both the disabled button and a ref guard in the hook.
- While asking, the previous answer is replaced by a placeholder that repeats the question, and the field and examples are disabled. "Cancel" aborts the request and returns to idle.
- Ticket switch: the panel is keyed by ticket ID and also unmounts while the next investigation loads. Unmounting aborts the request, and a result is only applied if its controller was not aborted, so a late answer cannot appear on another ticket. The question field resets with the ticket.
- No automatic retry. Retryable errors offer "Please try again", which re-sends the same question once per click.

### Error mapping

| Server response | Shown as | Retry button |
|---|---|---|
| 400 validation | The question could not be sent | no |
| 404 | Ticket not found | no |
| 503 without `reason` (database) | Unable to complete the investigation, ticket information unavailable | yes |
| 503 `aiNotConfigured`, 502 `aiAuthenticationFailed`, 502 `aiModelUnavailable` | AI investigation is not available | no |
| 429 `aiRateLimited` | AI request limit reached. No reset time is shown, even when `Retry-After` is present | yes |
| 504 `aiTimeout`, or the 75 s client timeout | The AI investigation took too long | yes |
| 422 `aiResponseBlocked` | No answer could be produced; rephrase | no |
| 502 `answerRejected` | The answer did not pass evidence checks | yes |
| other 502 `ai*` | The AI investigation is temporarily unavailable. Your existing ticket information is still accessible. | yes |
| 5xx without Problem Details (Vite proxy, API down) or no response | ResolveNest could not reach the investigation service | yes |
| 500 / `evidenceTooLarge` / unexpected shape | Unable to complete the investigation / display the answer | yes |

The status, `reason`, title, validation errors and `validationFailures` codes are listed under the collapsed "Technical details" item. Provider error bodies are never returned by the server, so none can be shown.

### Answer presentation

- Header: "Investigation answer", an "AI-generated" badge, and "Not enough evidence" when `status` is `insufficientEvidence`, followed by a one-line explanation. A 200 response is never presented as conclusive by itself.
- The answer text is split into paragraphs and rendered as React text nodes; no Markdown or HTML rendering.
- Key findings show the finding kind as a badge: Confirmed for this ticket, Confirmed on other cases, Possible, Documented, Background, Conflicting evidence. Uncertainties and suggested next steps ("Suggestions only. Nothing is done automatically.") follow. Each section appears only when the list is non-empty.
- Every statement shows the records it cites as chips, resolved through `validatedSources`. Unknown citation IDs are skipped, never invented.
- "Supporting records" lists cited records ("Product issue BUG-231", "Release 2.3.2", "Troubleshooting guide KB-012") and cited connections ("BUG-231 fixed in version 2.3.2") with their evidence badge. It states that a citation shows where a statement comes from, not that it is correct. With no citations it says the answer is unsupported.
- When this ticket has no confirmed issue and the answer contains `candidate` or `historicalConfirmation` findings, a note states that issues mentioned are possible matches until confirmed. This is derived from the investigation data, not from the AI text, and the AI text itself is not changed.
- Footer: a review reminder when `humanReviewRequired` is true, and a note when `evidenceComplete` is false. Provider, model, token counts and the evidence contract version stay in state but are not shown.

### Live verification (2026-11-03, `gemini-3.5-flash-lite`, 6 calls)

- **TCK-1046**, "Have other customers experienced this issue, and is there a known fix?": `answered`. BUG-231 shown as Possible; TCK-1017 and TCK-1018 as Confirmed on other cases; the 2.3.2 fix and KB-012 as Documented; patch version unknown as uncertainty; next steps to verify the patch version and share the KB-012 workaround. The no-confirmed-issue note appeared. Four rapid submit attempts produced one request.
- **TCK-1006**, "Is this a known product defect or could it be related to configuration?": the first call failed with provider HTTP 503 (shown as temporarily unavailable); the manual retry returned `answered`, citing the configuration resolution (missing `reports:read` scope) as Documented, with no defect claim.
- **TCK-1043**, "What is the most likely cause of the webhook delays…?" (sent with Ctrl+Enter): `insufficientEvidence`, shown with the Not enough evidence badge; BUG-247 and BUG-213 as Possible, KB-018 as Documented, next step to review KB-018 and delivery logs.
- Switching from TCK-1017 to TCK-1046 during a request aborted it (`net::ERR_ABORTED`); no answer appeared on TCK-1046. "Cancel" aborted a request and kept the question.
- With the API stopped: "ResolveNest could not reach the investigation service"; the ticket evidence stayed on screen.
- Error mapping for 400, 404, 503 (both kinds), 429, 504, 422, `answerRejected`, auth failure, proxy 502 and the client timeout was checked by calling `toAskError` in the page with constructed Axios errors, not by triggering them on the server.
- Validation: empty, whitespace, 3 characters and 1,001 characters keep Ask disabled with a message; no `/ask` request on load or on example click. 375 px: no horizontal scroll. No console errors after a fresh load.

Not verified live: rate limiting, provider timeout, blocked responses and `answerRejected` from the real server; screen-reader output.

### Known risks

- The model's summary can read more strongly than its findings. In the Acme run it said the issue "is tracked as BUG-231" while the finding was correctly classified as a candidate. The backend validator's hedge rules accepted it. The UI does not rewrite AI text; it relies on the finding badges, the no-confirmed-issue note and the review reminder. Tightening the validator is a backend decision.
- The ID formatter changes only prefixed identifiers in AI text for display; the original text stays in state.
- Example questions are generic and not tailored to the ticket.

## Customer reply draft (Task 3)

A panel under Ask ResolveNest drives the existing preview and approval endpoints (see [reply-drafting.md](reply-drafting.md)). Preview never writes. Approval is the only write, and it happens only after an explicit confirmation dialog. Nothing in the frontend sends email, and there is no Send button.

### Code

| File | Responsibility |
|---|---|
| `types/replyDraft.ts` | Mirror of `ReplyDraftPreview`, `ContextLimitation`, `ReplyDraftApprovalRequest`, `ApprovedReplyDraft`, `ReplyDraftApprovalResult` |
| `api/replyDraftApi.ts` | `generateReplyDraftPreview(ticketId, guidance, signal)` (75 s, cancellable; body `{}` or `{ reviewerGuidance }`) and `approveReplyDraft(ticketId, token, finalText)` (30 s, **no abort signal**, body `{ previewToken, finalText, approved: true }`) |
| `api/replyDraftGuards.ts` | Shape checks; the preview must have `persisted: false` and a token; the result's `ticketId` must match |
| `api/replyDraftErrors.ts` | Preview errors reuse the Ask mapping with preview wording, plus `draftRejected`; approval errors have their own mapping (below) |
| `lib/replyDraftRules.ts` | Client copy of the server rules: guidance at most 500 characters; final text 40 to 4,000 characters after trimming, no control characters, no `rec:`/`rel:` IDs or placeholders (`{{ }}`, `[Customer Name]`, `[insert …]`, `<insert`) |
| `lib/evidenceLimitationText.ts` | Product wording for the 13 `ContextLimitation` codes; unknown codes fall back to the server message with IDs formatted |
| `hooks/useReplyDraft.ts` | The workflow state machine |
| `lib/navigationLock.ts` | Context that locks ticket navigation and Refresh while an approval is in flight |
| `components/replyDraft/*` | Section, editor, evidence panel, confirmation dialog, saved summary |
| `components/ServiceErrorAlert.tsx` | Shared error alert (also used by Ask ResolveNest now) |

### Workflow and state

```
idle ─► generating ─► reviewing ─► (confirm dialog) ─► approving ─► approved
          │              ▲  │                              │
          ▼              │  └── edit text                  ▼
   generationFailed      └──────────────────────── approvalFailed
```

- Each state carries only what it needs, so the preview token, the draft text and the result cannot drift apart. The token lives only in React state, never in storage or the URL, and is never decoded.
- **No automatic calls.** Nothing is generated on ticket load or when instructions change. "Generate new preview" asks before discarding edits.
- **Duplicate protection.** The submit button is disabled while a request runs, and the hook ignores a second call through a ref guard: one preview at a time, and no preview while approving.
- **Confirmation is bound to the text.** "Approve & save" snapshots the trimmed text. The modal dialog shows that exact text, and confirming sends that snapshot, not whatever the editor holds later. Focus starts on "Keep reviewing" and returns to the Approve button afterwards.
- **Approval is never aborted.** Browser cancellation would not undo a server write, so the approval request has no abort signal. While it runs, the sidebar ticket buttons and Refresh are disabled, a `beforeunload` prompt is registered, and the section stays mounted. Results are applied only while it is mounted.
- **Ticket switch.** Both panels are keyed by ticket ID. Switching aborts an in-flight preview (read-only) and discards the draft, so tokens never cross tickets.
- **Expiry.** `previewExpiresAt` is checked every 15 s. After it passes, approval is disabled and an "expired, nothing was saved" message appears. The server remains the authority (400 `previewTokenExpired`).

### Approval outcome mapping

| Server result | UI | Editable | Action offered |
|---|---|---|---|
| 201 / 200 (`alreadyApproved`) | "Approved draft saved", metadata, "No email has been sent" | no | Copy approved text, Start a new draft |
| 400 `previewTokenExpired` | Expired, nothing saved | no | Start over with a new preview |
| 400 `invalidPreviewToken` / `previewTicketMismatch` | Can no longer be approved, nothing saved | no | Start over |
| 400 validation | Text did not meet requirements, nothing saved | yes | Edit and approve again |
| 404 | Ticket not found, nothing saved | no | Start over |
| 409 `replyDraftConflict` | Already approved with different text | no | Start over |
| 503 `replyDraftStorageNotInitialized` | Saving not set up | no | Start over |
| 503 without reason (database; the server says nothing was committed) | Not saved | yes | Try saving again (manual) |
| No response, timeout, 5xx without Problem Details (proxy), other 5xx, or a 2xx with an unexpected body | **We could not confirm whether the draft was saved** | no | Re-send the same approval (manual) |

The "outcome unknown" case relies on the server's idempotency. The same token with the same text returns 200 with the stored record if the first request was saved, or 201 if it was not. The text is locked so a re-send cannot produce a 409. Nothing retries automatically.

### Review presentation

- The draft editor is labelled "AI-generated · needs your review". `insufficientEvidence` shows "Holding reply" with an explanation.
- Unchanged text shows: passed automated checks at generation, which does not prove it is correct; verify every claim.
- Edited text (compared after CRLF→LF and trim, the same normalization the server uses) shows the required warning *"Your edits have not been automatically checked against the investigation evidence. Review the final text before saving."*, an "Edited by you" badge, and "Restore generated text". The confirmation dialog repeats the warning.
- "Information used to prepare this draft" is a separate, dashed panel marked "Internal · not part of the reply". It holds the statements the draft relies on (with record chips), uncertainties, investigation limitations in product wording, cited records and connections, and review details (human review required, not saved, approvable-until, condensed evidence). Model, tokens and provider stay in state only.
- The saved summary shows the ticket, `approvedAt`, edited-by-reviewer, whether the saved text is the checked AI text (`approvedTextValidated`), customer delivery (`notSent` → "Not sent…") and the draft reference. No other timestamps or statuses are invented.

### Verification (2026-11-03)

- `npm run build` and `npm run lint` pass.
- `ReplyDraft` count read-only before and after all preview work: 0 → 0.
- **TCK-1046 preview** (with the guidance "Keep the reply concise and ask the customer to confirm their installed patch version."): the draft says the behavior "may be related to a known issue documented in KB-012", mentions the 2.3.2 fix, asks for the exact patch version and offers the XLSX workaround. It contains no bug ID and no resolution claim. The internal panel lists BUG-231 as unconfirmed and the patch version as unknown. Three rapid submits sent one request.
- **TCK-1043 preview:** acknowledges the delays, points to KB-018 and names no cause; the uncertainty "exact cause … remains unconfirmed" is internal.
- **TCK-1006 preview:** two attempts both ended in server 504 `aiTimeout` (Gemini did not answer within 45 s). The UI showed "Preview generation took too long … Nothing was saved" with a manual retry. No TCK-1006 draft was produced, so Scenario B is **not verified**.
- Editing: the warning, badge and restore work. Empty, 10-character and `[Customer Name]` texts disable approval with messages. The dialog showed the exact edited text and the no-email notice; Escape closed it without a request.
- Switching from TCK-1017 to TCK-1043 during a preview aborted it (`net::ERR_ABORTED`), and the new ticket started clean.
- Approval error mapping for every row above was checked by calling `toApprovalError` in the page with constructed Axios errors.
- 375 px: no horizontal overflow, no console errors.

### Live approval (Task 3.1, 2026-11-03, authorized by the user)

- `ReplyDraft` count before: 2. Both were earlier manual UI approvals on TCK-1046 by the user. A fresh preview left it at 2.
- Reviewed and edited TCK-1046 draft ("Kind regards," added, 711 characters). The dialog showed TCK-1046 (Acme Corporation), the exact text, the no-email notice and the edit warning, and was confirmed through the UI.
- One `approve` request → **201 Created**, `alreadyApproved: false`, draft `reply-draft:4c36c7eea28b430ba0773096e1697a42`, `editedByHuman: true`, `approvedTextValidated: false`, `customerDelivery: notSent`.
- UI: "Approved draft saved", "No email has been sent", the matching metadata and the approved text. The Approve button is gone and navigation was unlocked afterwards.
- Neo4j (read-only query): count 3; exactly one `HAS_REPLY_DRAFT` link from `ticket:TCK-1046`; metadata equal to the response. The stored `approvedTextSha256` equals the SHA-256 of the reviewed text computed in the browser.
- No mail library or mail code exists in the API or web sources; the API log line reads "No message was sent."
- Duplicate, conflicting, expired, invalid-token, storage-failure and unknown-ticket approvals are covered by the 64 deterministic reply-draft tests (mocked store and rolled-back Neo4j), not by live requests.
- **Still not verified live:** the navigation lock and `beforeunload` while an approval is in flight (the request completed too quickly to observe), an uncertain outcome against the real server, and client-side expiry after 30 minutes.

### Known risks

- TCK-1006 reply previews timed out twice in a row on the server. The cause (provider latency or model behavior for this ticket) is in the backend and was not changed here.
- The navigation lock covers the sidebar, the mobile Sheet and Refresh. A browser reload or closing the tab during approval only gets the `beforeunload` prompt; the outcome is then unknown to the UI. The draft can still be found in the database by ticket, but there is no UI to read approved drafts (out of scope).
- There is no approver identity (`approvalBasis: unauthenticatedLocalRequest`), as documented for the backend.

## QA, accessibility and polish (Task 4.2)

### Troubleshooting guide Markdown

`components/GuideMarkdown.tsx` renders guide content with `react-markdown` 10.1 and `remark-gfm` 4.0. GFM is needed because 9 of the 31 guides use tables, including KB-015 for the 403 scenario. No other plugins are used.

Safeguards:
- `skipHtml` drops every raw HTML node. `rehype-raw` is not used, and `dangerouslySetInnerHTML` is not used anywhere.
- `allowedElements` lists headings, paragraphs, emphasis, lists, blockquote, code, pre, links, hr, br and table elements; anything else is unwrapped to its text. `img` is not allowed, so no remote content is ever loaded.
- `urlTransform` keeps only `http:`, `https:` and `mailto:` hrefs. `javascript:` (any case or spacing), `data:`, `vbscript:` and relative URLs become empty, and an empty href renders as plain text.
- Kept links open with `target="_blank" rel="noopener noreferrer nofollow"`.
- An error boundary falls back to the plain text if rendering throws.
- Guide headings map to `h4`–`h6`, so they stay below the card's `h3` in the page outline.
- The renderer is loaded with `React.lazy` the first time a guide is expanded. It is a 164 KB (49 KB gzip) chunk, and the main bundle stays at about 529 KB (164 KB gzip). This is the only code split.

A hostile-input render in the browser (script tags, `onerror`/`onclick` attributes, an iframe, a remote image, and `javascript:`, `JAVASCRIPT:`, ` javascript:`, `data:`, `vbscript:` and relative links) produced no script, iframe, image or event-handler attribute, executed nothing, and kept only the https and mailto links.

### Fixes from the audit

| Finding | Fix |
|---|---|
| Guides showed raw Markdown (`#`, `**`, table pipes) | Restricted renderer above |
| Guides sat in a two-column grid next to the summary rail: about 294 px of reading width at 1440 px | Single-column guide list (about 664 px) |
| "Ask ResolveNest", "Customer reply draft" and the three summary-rail titles were `div`s, so they were missing from the heading outline, and "Investigation coverage" (`h3`) nested under the wrong `h2` | Rendered as `h2` with the same styling |
| Reply evidence panel jumped from `h2` to `h4` | Panel `h3`, blocks `h4` |
| Every evidence badge was focusable only to show a tooltip: 29 of 66 tab stops on Acme | Badges are no longer focusable; the visible label and icon carry the meaning, the hover tooltip stays, and keyboard and screen-reader users get the definitions as text in "How to read the labels". 41 tab stops remain. |
| Closing the "Discard your edits?" dialog left focus on `<body>` | Focus returns to "Generate new preview" |

### Checks run (2026-11-04)

- `npm run build` and `npm run lint` pass. No source comments, `dangerouslySetInnerHTML`, `rehype-raw` or browser storage in `src`.
- **Structure:** every textarea has a label, every button and link has a name, there are no duplicate IDs, every `aria-describedby` target exists, and the landmarks are `header`, `main`, and `nav` "Primary" and "Breadcrumb".
- **Contrast** (computed from the theme tokens): muted text 6.9–7.6:1; badge text on its surface: confirmed 5.1, documented 5.5, candidate 4.9, context 6.9; primary button 6.2; destructive on card 4.8. All pass WCAG AA (4.5:1).
- **Focus:** each of the 41 tab stops shows an outline or ring when focused. The first Tab reaches the visible "Skip to investigation" link.
- **Overflow:** none at 375 px and 768 px for all four tickets with every accordion expanded. At 375 px the reply editor is 311 px wide. The approval dialog fits inside the viewport (x 25–351, y 133–679 of 375×812), its buttons stack, the text box scrolls at 228 px, and focus starts on "Keep reviewing".
- **Unchanged evidence:** TCK-1046 still shows no confirmed issue and BUG-231 as Possible #1. No classification, order or wording logic changed.
- **Console:** no console errors or warnings while switching all four tickets.
- **Gemini:** one call (a TCK-1046 preview) to measure the mobile dialog. No approval was sent, and no record was created.

### Not verified in this session

- Focus restoration after the approval or discard dialog closes. The browser pane was hidden, so the exit animation never completed and Radix did not finish closing. Closing worked visibly in Task 3, and the focus-return code is the same pattern for both dialogs.
- Screen-reader output (no screen reader was run), automated axe-style testing (not installed, per the task constraints), and visual screenshots after the guide change at 768 px.
- At 32 px, button heights meet WCAG 2.2 AA's 24 px minimum target size, but not the 44 px often recommended for touch.

## Known limitations

- The selected ticket is not in the URL, so a reload returns to Acme.
- Only the four demo tickets are selectable; there is no ticket search API.
- The main bundle is about 529 KB (164 KB gzip). The only split chunk is the guide Markdown renderer (164 KB, 49 KB gzip), loaded on first expand.
- `npm audit` lists `deepmerge` and `minimatch` advisories through the `shadcn` CLI package, a runtime dependency because `index.css` imports `shadcn/tailwind.css`. They are not in the browser bundle, and the new Markdown packages add none.
- `npm audit` reports a moderate `minimatch` advisory through the existing ESLint toolchain (dev only), unrelated to the packages added here.
