# Connector authentication errors

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-022 · **Applies to:** Ledgerline 2.1 and later · **Related bugs:** BUG-171, BUG-258

## Symptoms

- Run history shows `auth_failed` or `invalid_grant`.
- Sync stops and the connector shows **Reconnect required**.

## Causes and fixes

| Cause | Fix |
|---|---|
| Password or client secret changed in the source system | Reconnect the connector with the new credentials |
| OAuth consent revoked by a source administrator | Re-authorize the connector |
| Service account locked or expired | Unlock the account in the source system |

## Known issues

- Before 2.1.0, sync could stall after an OAuth token refresh (BUG-171).
- On 2.4.1 and later, a connector can retry indefinitely when the source keeps returning 401 (BUG-258, open). Disable and reconnect the connector to stop the retries.
