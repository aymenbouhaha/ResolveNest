# Large exports and timeouts

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-014 · **Applies to:** all versions · **Tracking bug:** BUG-238 (open)

## Symptoms

Exports of more than roughly 500,000 rows fail after about two minutes with `Export timed out`. Smaller exports of the same report succeed.

This error is different from `ERR_EXPORT_SERIALIZE` (KB-012), which fails immediately regardless of size.

## Cause

Interactive exports are generated within a single request that has a 120-second gateway timeout.

## Workarounds

1. Split the export by date range (for example one export per quarter).
2. Use the asynchronous export API: `POST /v2/exports`, then poll `GET /v2/exports/{id}` and download the file when its status is `ready`. The token needs the `exports:create` scope (KB-015).

A permanent fix is tracked in BUG-238. No fix release has been announced.
