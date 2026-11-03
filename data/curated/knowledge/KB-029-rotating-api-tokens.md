# Rotating API tokens safely

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-029 · **Applies to:** Ledgerline 2.2 and later · **Related bug:** BUG-179 (open)

1. Create the new token with the same scopes as the old one.
2. Deploy the new token to every worker or environment that uses the integration.
3. Check **Settings > API Tokens > Last used** before revoking the old token.
4. Revoke the old token.

Tokens expire after 90 days unless an admin sets a different expiry. Expired tokens currently return 403 instead of 401 (KB-015).

**Known issue:** the **Last used** timestamp is not updated for read-only requests (BUG-179). For read-only integrations, don't rely on it alone.
