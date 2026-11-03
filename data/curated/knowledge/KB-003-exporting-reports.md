# Exporting reports: CSV, XLSX and PDF

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-003 · **Applies to:** Ledgerline 2.1 and later

## Choosing a format

| Format | Best for | Limits |
|---|---|---|
| CSV | Loading data into other systems | Plain values only, no formatting |
| XLSX | Sharing with spreadsheet users | 1,048,576 rows per sheet |
| PDF | Printing and archiving | Wide tables may need landscape orientation |

## Exporting from the report viewer

1. Open the report and apply filters.
2. Select **Export** and pick a format.
3. The file downloads when generation finishes.

The **Export** menu needs the `Reports: export` permission. See KB-007.

## Column order

CSV columns follow the report definition, not the order you drag columns into in the viewer. To change the exported order, edit the report definition.

## Troubleshooting

- Error `ERR_EXPORT_SERIALIZE` on 2.3.0 or 2.3.1: see KB-012.
- `Export timed out` on very large reports: see KB-014.
