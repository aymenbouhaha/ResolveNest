# API rate limits and 429 responses

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-020 · **Applies to:** Ledgerline 2.0 and later · **Related bugs:** BUG-173, BUG-203, BUG-251

## Limits

| Plan | Requests per minute per token | Requests per minute per organization |
|---|---|---|
| Business | 120 | 600 |
| Enterprise | 600 | 3,000 |

## Response headers

- `X-RateLimit-Limit`, `X-RateLimit-Remaining` and `X-RateLimit-Reset` (seconds until the window resets).
- `429 Too Many Requests` responses have included `Retry-After` in seconds since 2.3.1 (BUG-203).
- On 2.4.0, `X-RateLimit-Reset` was reported in milliseconds (BUG-251, fixed in 2.4.1).
- Before 2.0.3, burst requests could keep the counter from resetting at the window boundary (BUG-173).

## Common causes of 429

1. Several integrations share one token. Use one token per integration.
2. Parallel workers ignore `Retry-After`. Back off and retry after the advertised delay.
3. Bulk jobs hit the organization-wide limit. Schedule bulk jobs outside business hours.

Rate limits are a plan setting, not a defect. Contact your account manager to discuss higher limits.
