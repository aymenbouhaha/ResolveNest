# Sharing reports and folder permissions

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-034 · **Applies to:** Ledgerline 2.4 and later · **Related bugs:** BUG-210, BUG-253

## Shared folders

Since 2.4.0, you can share reports through folders. To open a shared report, a user needs both:

1. Folder membership (Viewer, Editor or Manager).
2. The `Reports: view` role permission.

## API access

When the token's user is not a member of a restricted folder, API calls to reports in that folder return `403 Forbidden`. Some endpoints return `404` instead (BUG-210, open).

## Known issue in 2.4.0

On 2.4.0, users with the Analyst role got `403` on reports in shared folders even when they were folder members (BUG-253, fixed in 2.4.1). This is a product defect, not a permissions configuration problem.
