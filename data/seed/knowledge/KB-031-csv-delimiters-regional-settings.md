# CSV delimiters and regional settings

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-031 · **Applies to:** all versions · **Related bug:** BUG-159 (fixed in 2.0.2)

## All data appears in one column

In many European locales, spreadsheet tools expect `;` as the list separator, but Ledgerline exports use `,` by default. To fix this, do one of the following:

- Set **Workspace settings > Exports > CSV delimiter** to `;`.
- Open the file with the spreadsheet tool's text import wizard.

This is a configuration setting, not an export failure.

## Special characters

Exports are UTF-8. Older spreadsheet tools may need **UTF-8 with BOM** enabled in the export settings.

Before 2.0.2, quotes inside multi-line text fields were escaped incorrectly (BUG-159).

For `ERR_EXPORT_SERIALIZE`, see KB-012.
