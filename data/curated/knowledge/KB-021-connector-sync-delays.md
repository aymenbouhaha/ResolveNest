# Data connector sync schedules and delays

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-021 · **Applies to:** Ledgerline 2.1 and later · **Related bugs:** BUG-201, BUG-211

## How sync works

- Connectors poll the source system every 15 minutes (Business) or 5 minutes (Enterprise).
- Each run imports only records that changed, based on the source system's modification timestamp.
- Large initial syncs can take several hours.

## Diagnosing a delay

1. Open **Settings > Connectors > Run history** and check the last successful run.
2. A run in status `waiting_for_source` means the source API is throttling requests.
3. Compare the source record's modification time with the run's start time.

## Known issues

- Before 2.3.0, a daylight-saving change could trigger a full re-import (BUG-201).
- Before 2.4.0, the run status could show `Completed` while the last batch was still running (BUG-211).

If runs succeed but records arrive late, the cause is usually the source system's own export delay.
