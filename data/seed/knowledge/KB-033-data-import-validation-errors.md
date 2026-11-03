# Data import validation errors reference

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-033 · **Applies to:** all versions · **Related bugs:** BUG-157, BUG-183, BUG-259

| Error | Meaning | Fix |
|---|---|---|
| `Invalid character in row 1` | File is not UTF-8 | Save as UTF-8 (KB-009) |
| `Unknown column` | Header does not match the template | Download the current template |
| `Duplicate key` | Two rows have the same external ID | Remove duplicates |
| `Invalid date` | Date format does not match the workspace locale | Use ISO dates (YYYY-MM-DD) |

## Known issues

- Before 2.0.0, text longer than 255 characters was truncated without a warning (BUG-157).
- The import preview can show wrong row numbers when the file contains blank lines (BUG-183, open).
- On 2.4.1, dates in `DD.MM.YYYY` format are rejected even when the locale uses that format (BUG-259, open). Use ISO dates instead.
