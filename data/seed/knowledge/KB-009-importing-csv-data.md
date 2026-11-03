# Importing data from CSV files

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-009 · **Applies to:** all versions · **Related bug:** BUG-215 (fixed in 2.3.0)

## File requirements

- Encoding: UTF-8. UTF-16 files fail with `Invalid character in row 1`.
- First row: column headers matching the import template.
- Maximum file size: 200 MB.

## Byte-order mark (BOM)

Before 2.3.0, files saved as "CSV UTF-8" by some spreadsheet tools were rejected with `Unexpected column ﻿account_id` (BUG-215). On those versions, save as plain "CSV (comma delimited)". Version 2.3.0 and later strip the BOM automatically.

## Progress display

On 2.3.0 the progress bar can stay at 99% even though the import has completed (fixed in 2.3.1). Check **Import history** for the final status.
