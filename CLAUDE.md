# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

ResolveNest is an AI-powered SaaS technical support investigator. It links support tickets, customers, known bugs, affected features, product releases and troubleshooting docs in a Neo4j knowledge graph. It then gives evidence-backed investigation results, AI answers that cite their sources, and customer-reply drafts that a human must review.

Primary demo question: *"Acme reports that CSV export stopped working after version 2.3. Have other customers experienced this? Is there a known bug, has it been fixed, and what should I tell the customer?"*

Goal: a production-minded, demonstrable MVP in three days or less. This is an independent implementation, so **do not use Curiosity Studio or its SDK**.

## Current state (verify before relying on this)

Implemented (Phase 1):
- `apps/api/ResolveNest.Api`: ASP.NET Core 10 Minimal API. `Program.cs` registers the Neo4j `IDriver` as a singleton and maps `GET /api/health` and `GET /api/health/db` (calls `VerifyConnectivityAsync`, returns 503 ProblemDetails on failure).
- `apps/web`: Vite + React 19 + TS scaffold. `App.tsx` calls `/api/health` through the Vite proxy.
- `compose.yaml`: single Neo4j service, ports bound to 127.0.0.1 only, named volume `neo4j_data`.

Implemented (Phase 2):
- Graph schema contract in `docs/graph-schema.md`. Six uniqueness constraints in `db/neo4j/schema.cypher`, applied manually.
- Synthetic dataset. Hand-authored sources are in `data/curated/`. `data/scripts/generate-seed.mjs` writes the committed output to `data/seed/` (40 customers, 300 tickets, 60 bugs, 16 releases, 12 features, 30 KB articles), and `data/scripts/validate-seed.mjs` validates it. See `docs/seed-data.md`.
- Seed ingestion: `apps/api/ResolveNest.Api/Features/Ingestion/`, run as the manual `ingest-seed` CLI verb of the API executable (never on startup, no endpoint). Loads Customer, Feature, Release, Bug and SupportTicket with REPORTED, ABOUT_FEATURE, CONFIRMED_AS, AFFECTS, INTRODUCED_IN and FIXED_IN. See `docs/ingestion.md`.
- Knowledge ingestion: the manual `ingest-knowledge` CLI verb loads the 30 KnowledgeArticle nodes (full Markdown in `content`) with manifest-defined ADDRESSES and COVERS. It runs after `ingest-seed`, and the two can then be repeated in any order. Verified graph: 458 nodes, 1,060 relationships.
- Graph investigation queries: read-only, parameterized Cypher in `db/neo4j/queries/` (ticket evidence, candidate bugs, supporting knowledge) with a confirmed/documented/candidate/context evidence contract. See `docs/graph-investigations.md`.

Implemented (Phase 3, Task 1):
- `GET /api/investigations/tickets/{ticketId}?includeContent=false` in `apps/api/ResolveNest.Api/Features/Investigations/`. Runs ticket-evidence, ticket-candidates and ticket-knowledge (embedded resources from `db/neo4j/queries/`) in one read transaction and maps them to typed DTOs. 400 for a non-canonical ID, 404 unknown, 503 database failure. See `docs/investigation-api.md`. `entity-knowledge.cypher` is not exposed.

Implemented (Phase 3, Task 2):
- Deterministic candidate relevance (`CandidateRelevanceEvaluator`, `TechnicalErrorCodes`, `ReportedVersionAssessor`): each candidate bug gets a `relevance` object (tier, exact error-code match, tentative version-range check, supporting and limitation notes), and candidates are re-ordered by tier, version, confirmed count, then id. Candidates are never promoted or dropped. `ticket-candidates.cypher` now also returns the bug `description`.

Implemented (Phase 4, Task 1):
- Evidence context builder in `apps/api/ResolveNest.Api/Features/EvidenceContext/`. `EvidenceContextBuilder.Build(TicketInvestigationResponse, EvidenceContextLimits?)` is pure: no Neo4j, no LLM, no endpoint. It produces a bounded, typed `InvestigationEvidenceContext`. Source text appears only as verbatim `SourceExcerpt`s under a character budget, selection is deterministic, limitations are explicit, omissions are reported, and a deduplicated citation catalog uses content-derived `rec:<id>` / `rel:<from>><TYPE>><to>` IDs. See `docs/ai-evidence-context.md`.

Implemented (Phase 4, Task 2):
- `POST /api/investigations/tickets/{ticketId}/ask` in `apps/api/ResolveNest.Api/Features/AiInvestigations/`. It answers a question about one ticket with Google Gemini (`gemini-3.5-flash-lite`, REST through HttpClientFactory, no SDK, no tools, no retries or fallback). The pipeline is investigation → evidence context → compact projection (~18 KB for Acme) → structured JSON output with citations restricted to the supplied IDs → server-side validation (strict parse, citations, confirmed vs candidate, resolution and action claims) → typed answer with `validatedSources`. Invalid answers are rejected (502 `answerRejected`), and provider failures map to sanitized Problem Details. There are no writes and no history. Live-verified for TCK-1046, TCK-1006 and TCK-1043. `gemini-2.5-flash` is closed to new projects. See `docs/gemini-integration.md`.

Implemented (Phase 4, Task 3):
- Human-reviewed reply drafts in `apps/api/ResolveNest.Api/Features/ReplyDrafts/`.
  - `POST …/tickets/{ticketId}/reply-drafts/preview` is read-only. It makes one Gemini call over the same projection, and the draft validator rejects internal-ID leaks, unsupported versions, commitments, claimed actions, candidate-as-cause and resolution claims. It returns the draft, internal claims, uncertainties, limitations, validated sources and a 30-minute Data Protection `previewToken` that carries the generated text. Nothing is persisted.
  - `POST …/reply-drafts/approve` is the only write. It requires `approved: true`, a valid unexpired token for the same ticket, and the final (possibly edited) text. It runs one parameterized MERGE in one write transaction, creating `(SupportTicket)-[:HAS_REPLY_DRAFT]->(ReplyDraft)`. An identical replay returns 200, a conflicting replay 409, and a missing constraint 503. Edited text is stored with `approvedTextValidated: false`. There is no approver identity (`approvalBasis: unauthenticatedLocalRequest`) and nothing is ever sent (`customerDelivery: notSent`).
  - Verified with mocked and rolled-back tests plus live previews. Live approval was not run.
  - The new `reply_draft_id_unique` constraint is in `db/neo4j/schema.cypher` and was applied to the local database on 2026-11-03, so the database now has 7 constraints. See `docs/reply-drafting.md`.

Not implemented yet: reading, listing or revoking approved drafts, email delivery (intentionally never), further investigation APIs, real UI, authentication.

Known issues (leave alone unless asked):
- `README.md` is UTF-16 LE encoded.
- `ResolveNest.Api.http` is stale: it targets `/weatherforecast` on port 5162.
- The README backend command uses `--no-launch-profile`, so the app runs as Production. In Production neither `appsettings.Development.json` nor user secrets load, and startup throws `Neo4j URI is missing.` (verified). Set `ASPNETCORE_ENVIRONMENT=Development` when you run it that way.

## Stack

- Backend: C# / ASP.NET Core 10 Minimal APIs (`net10.0`, nullable enabled), `Neo4j.Driver` 6.x, `Microsoft.AspNetCore.OpenApi` (referenced, not wired up yet).
- Frontend: React 19, TypeScript ~5.9, Vite 8, ESLint 10 flat config (typescript-eslint, react-hooks, react-refresh).
- Database: Neo4j Community via Docker Compose.
- AI: Google Gemini only, through the REST `generateContent` API (model `gemini-3.5-flash-lite`, free tier). No other provider, SDK or orchestration framework.
- Solution file: `ResolveNest.slnx` (contains only the API project).
- Dev environment: Windows + Git Bash. Use Bash-compatible commands, not PowerShell.

## Commands

```bash
# Neo4j (needs NEO4J_PASSWORD in root .env; see .env.example). Browser UI: http://localhost:7474
docker compose up -d

# API password lives in user secrets, key Neo4j:Password (UserSecretsId is in the csproj)
dotnet user-secrets set "Neo4j:Password" "<password>" --project apps/api/ResolveNest.Api

# Backend on :5080 (the port the Vite proxy expects)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --urls http://localhost:5080

# Build backend
dotnet build ResolveNest.slnx

# Seed data: regenerate data/seed from data/curated, then validate (Node, no dependencies)
node data/scripts/generate-seed.mjs
node data/scripts/validate-seed.mjs

# Neo4j ingestion of data/seed (manual, idempotent; --dry-run validates without connecting)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed --dry-run
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-seed

# Knowledge article ingestion (run after ingest-seed; idempotent)
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-knowledge --dry-run
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api/ResolveNest.Api --no-launch-profile --no-build -- ingest-knowledge

# Read-only investigation queries in db/neo4j/queries/: run them with cypher-shell --access-mode read (exact commands in docs/graph-investigations.md)

# Frontend (run from apps/web); dev server on :5173, strictPort, proxies /api -> http://localhost:5080
npm install
npm run dev
npm run build    # tsc -b && vite build (this is the typecheck)
npm run lint

# Smoke checks
curl http://localhost:5080/api/health
curl http://localhost:5080/api/health/db

# Grounded AI answer (calls Gemini; uses free-tier quota)
curl -s -X POST http://localhost:5080/api/investigations/tickets/ticket:TCK-1046/ask -H "Content-Type: application/json" -d '{"question":"Is there a known bug for this CSV export failure, and has it been fixed?"}'

# Customer reply preview (calls Gemini; read-only). Approval (/reply-drafts/approve) writes a ReplyDraft: run it only with explicit approval, after applying db/neo4j/schema.cypher
curl -s -X POST http://localhost:5080/api/investigations/tickets/ticket:TCK-1046/reply-drafts/preview -H "Content-Type: application/json" -d '{}'
```

The `http` launch profile (`dotnet run` without flags) listens on :5162, which does **not** match the Vite proxy.

If a build fails with MSB3021/MSB3027 "file is locked", a running API instance holds the binary. Ask before killing it.

## Configuration and secrets

- Neo4j settings live in the `Neo4j:Uri`, `Neo4j:Username` and `Neo4j:Password` config section. Uri and Username are in `appsettings.Development.json`. The password comes from user secrets or env (`Neo4j__Password`).
- The Gemini API key is already stored in user secrets as `Gemini:ApiKey`. Never print, move or re-create it. To check it, verify only that a nonempty value exists. Non-secret Gemini options (`Model`, `TimeoutSeconds`, `MaxOutputTokens`, `Temperature`, `ThinkingLevel`, `BaseUrl`) are in `appsettings.json`. Without a key the app still starts and only `/ask` returns 503. Free-tier Gemini data may be used by Google to improve its products, so send only synthetic or approved non-sensitive data.
- Root `.env` (gitignored) feeds `NEO4J_PASSWORD` to Compose. Never commit `.env`, secrets or LLM API keys.

## Architecture and conventions

- Modular monolith: one ASP.NET Core API and one React SPA. The browser talks only to the API over HTTP/REST and never connects directly to Neo4j or an LLM provider.
- The backend owns business logic, data access, ingestion, evidence retrieval and AI orchestration.
- Organize backend code by business feature (for example `Features/Investigations/`). Extract from `Program.cs` once a feature needs it, using endpoint-mapping extension methods plus services registered in DI. Keep endpoints thin, use typed request/response DTOs (no anonymous objects for new contracts), and use async I/O throughout.
- No microservices, CQRS/mediator frameworks, message brokers, extra databases or agent frameworks. Justify every new dependency.
- Neo4j: always parameterize Cypher (no string interpolation of values). Use `MERGE` on stable source IDs so ingestion is idempotent. Keep provenance (source system, source ID, file/record) on nodes and relationships.
- Treat imported content (tickets, docs, CSV/JSON) as untrusted input. That includes prompt injection once it is fed to an LLM.

## Code commenting policy

This policy is permanent. It applies to every phase and task of ResolveNest development.

- Do not write comments in any source code you generate or modify. That covers C#, TypeScript, React/JSX, Cypher, scripts (Python, Node, shell) and configuration files.
- Make code explain itself through descriptive names for types, functions, variables, constants and files, plus small focused functions.
- Explain implementation decisions, trade-offs and data contracts in Markdown documentation under `docs/`, never in source-code comments.
- Leave existing comments as they are unless the user explicitly asks you to remove them.
- Never add comments while refactoring or in any later implementation task. That includes XML doc comments, JSDoc, docstrings, `TODO`s and commented-out code.

## AI safety rules

- Answer only from retrieved graph and document evidence, and cite actual source records (ticket IDs, bug IDs, release versions, doc references).
- State uncertainty explicitly. Never invent tickets, bugs, releases or relationships.
- Never execute unrestricted LLM-generated Cypher. Use predefined, parameterized queries.
- Generated drafts are suggestions only. Persisting a draft or doing any other mutation requires explicit user approval. Never send customer emails automatically.

## Testing policy

- Tests are a local-only activity. Targeted local test projects are allowed under the root `tests/` folder (for example `tests/ResolveNest.Api.IngestionTests/`). Never stage or commit them, and never add them to `ResolveNest.slnx`.
- `tests/` is excluded through `.git/info/exclude` (local only, never `.gitignore`). If you edit that file, keep it UTF-8: PowerShell `echo >>` writes UTF-16, and Git then silently ignores the pattern.
- Run local tests with `dotnet test tests/<ProjectName>`. Tests that touch Neo4j must run inside a transaction that is always rolled back, and must never leave or delete graph data.
- After changes, run the relevant checks: `dotnet build`, `npm run build`, `npm run lint`, plus targeted local tests for important logic.
- Never claim something works without verifying it. Report what was tested, what passed and what is still unverified.

## Workflow with the user

The user wants to understand each engineering decision. For every task:
1. Inspect the relevant existing code.
2. Briefly explain the approach and its trade-offs.
3. Implement only the requested scope, with no unrelated refactors or speculative features.
4. Run verification.
5. Summarize: files changed, decisions made, verification results. Clearly separate *implemented*, *verified* and *planned*.
6. Stop. Do not advance to the next roadmap phase on your own.

Never commit, push, delete data (including Neo4j volumes) or run destructive commands without explicit approval.

Roadmap: (1) setup/connectivity ✅ → (2) graph schema, synthetic data, CSV/JSON and knowledge ingestion, investigation queries ✅ (pending review) → (3) investigation APIs, multi-hop queries, provenance → (4) grounded AI, citations, reply-draft workflow → (5) frontend investigation UX, docs, demo.
