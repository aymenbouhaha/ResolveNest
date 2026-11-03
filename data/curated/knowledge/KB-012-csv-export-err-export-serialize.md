# Known issue: CSV export fails with ERR_EXPORT_SERIALIZE (2.3.0 and 2.3.1)

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-012 · **Applies to:** Ledgerline 2.3.0, 2.3.1 · **Fixed in:** 2.3.2 · **Tracking bug:** BUG-231

## Symptoms

- Exporting a report to CSV shows `Export failed (ERR_EXPORT_SERIALIZE)`.
- The downloaded file is 0 bytes.
- Scheduled reports that attach CSV files send an empty attachment.
- XLSX and PDF exports of the same report work.

The problem affects reports that contain at least one **custom date field** with an empty value or a non-ISO display format.

## Cause

The streaming CSV export engine introduced in 2.3.0 did not handle empty or custom-formatted date values.

## Resolution

Upgrade to **2.3.2 or later**. Cloud workspaces received 2.3.2 on 2026-08-04. Dedicated environments must schedule the upgrade.

To check your version, open **Help > About Ledgerline**.

## Workarounds until you upgrade

1. Export the report as **XLSX** instead of CSV.
2. Or remove custom date columns from the report, export to CSV, and re-add them afterwards.
3. For scheduled reports, switch the attachment format to XLSX.

## After upgrading

Re-run the failing export. If CSV export still fails on 2.3.2 or later, the cause is different. Contact support with the report name, the exact error text and the time of the failed export.
