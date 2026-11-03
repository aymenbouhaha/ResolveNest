# IP allowlists for API access

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-016 · **Applies to:** Enterprise plans

Organizations can restrict API access to known IP ranges in **Settings > Security > API allowlist**.

When a request comes from an address outside the allowlist, the API returns `403 Forbidden` with `error: ip_not_allowed`, even if the token and scopes are valid.

## Adding an address

1. Find the public egress IP of the calling server (not its private address).
2. Add it as a single address or CIDR range.
3. Changes apply within one minute.

## Common causes

- An integration moved to a new server or cloud region.
- Outbound traffic now goes through a different NAT gateway or proxy.
