# Asynchronous export API

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-039 · **Applies to:** Ledgerline 2.2 and later

Use the asynchronous export API for large CSV exports.

1. Call `POST /v2/exports` with the report ID and format. The token needs the `exports:create` scope.
2. Poll `GET /v2/exports/{id}` until `status` is `ready`.
3. Download the file from `download_url` within 24 hours.

Exports created through the API count toward API rate limits (KB-020). For interactive export timeouts, see KB-014.
