# Paginating large API result sets

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-024 · **Applies to:** Ledgerline 2.0 and later · **Related bugs:** BUG-163, BUG-224

## Cursor pagination (recommended)

Pass the `next_cursor` value from each response until it is `null`. Cursors stay stable when records change during paging.

## Offset pagination (legacy)

Offset pagination is still supported, but it can skip or repeat records when data changes between requests.

## Known issues

- Before 2.0.2, offset pagination skipped records when sorting by `updated_at` (BUG-163).
- From 2.2.0 until the fix in 2.3.0, cursor pagination with page sizes above 200 could repeat the boundary row (BUG-224).

Always de-duplicate records by `id` in integrations.
