# PDF export layout tips

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-032 · **Applies to:** Ledgerline 2.1 and later · **Related bugs:** BUG-175, BUG-204, BUG-214

- Use landscape orientation for reports with more than 10 columns.
- Set page margins in **Report settings > Print layout**. Before 2.1.0, margins were ignored (BUG-175).
- On 2.1.0, wide tables were truncated (BUG-204, fixed in 2.1.1).
- On 2.4.0, dashboard PDFs could omit the last row of widgets (BUG-214, fixed in 2.4.1).

For non-Latin characters, see the 2.4.0 release notes.
