# Dashboard filters, dates and drill-down

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-035 · **Applies to:** Ledgerline 2.1 and later · **Related bugs:** BUG-177, BUG-209, BUG-254

- Dashboard dates use the workspace timezone. Before 2.1.1, the date picker defaulted to UTC (BUG-177).
- Since 2.2.1, filters persist in the URL. Before that, browser back navigation reset them (BUG-209).
- Since 2.4.0, filter dropdown values longer than 40 characters are truncated (BUG-254, open). Type in the dropdown to search instead.
