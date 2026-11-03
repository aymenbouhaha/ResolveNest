# Troubleshooting scheduled report delivery

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-011 · **Applies to:** all versions · **Related bug:** BUG-222 (open)

## Report not received

1. Check **Schedule > Run history** for the run status.
2. If the run succeeded, check the recipient's spam folder and the bounce list in **Settings > Email**.
3. If the run is missing, check the schedule timezone.

## Email arrives without the attachment

Attachments larger than 10 MB are currently dropped without an error (BUG-222). Enable **Send as download link** in the schedule settings to receive a secure link instead.

## Empty CSV attachment

On 2.3.0 and 2.3.1, CSV attachments for reports with custom date fields can be 0 bytes. See KB-012.
