# Connector field mapping errors

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-037 · **Applies to:** Ledgerline 2.1 and later · **Related bug:** BUG-181 (fixed in 2.2.0)

## Symptoms

- Run history shows `mapping_error: field not found`.
- Records sync, but some fields stay empty.

## Fixes

1. Open **Settings > Connectors > Field mapping** and look for fields marked **Missing in source**.
2. Map the renamed source field again.
3. Run a manual sync.

Before 2.2.0, renaming a source column deleted its mapping (BUG-181). Current versions keep the mapping and flag it.
