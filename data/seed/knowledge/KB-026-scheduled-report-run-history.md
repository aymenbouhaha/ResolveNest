# Reading scheduled report run history

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-026 · **Applies to:** all versions · **Related bugs:** BUG-165, BUG-206, BUG-255

| Status | Meaning |
|---|---|
| `succeeded` | Report generated and emailed |
| `failed_query` | The report query failed. Check the report definition |
| `failed_owner` | The schedule owner is deactivated (BUG-165, open). Transfer the schedule to an active user |
| `skipped` | The previous run was still running |

## Known issues

- Before 2.3.1, editing a schedule while it ran could trigger a second run (BUG-206).
- On 2.4.0 and 2.4.1, the schedule preview could show the previous run's data (BUG-255).

KB-011 covers email delivery problems.
