# Verifying webhook signatures

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-027 · **Applies to:** all versions · **Related bug:** BUG-213 (fixed in 2.4.0)

Each delivery carries a `Ledgerline-Signature` header: an HMAC-SHA256 of the raw request body, computed with your endpoint's signing secret.

## Common mistakes

1. Computing the HMAC over re-serialized JSON instead of the raw bytes.
2. Using the signing secret of a different endpoint.
3. Rejecting deliveries during secret rotation.

## Secret rotation

Since 2.4.0, the previous secret stays valid for 24 hours after rotation. Before 2.4.0, rotation invalidated the old secret immediately (BUG-213), so signature checks failed until receivers were updated.

Signature failures are a receiver configuration problem. They do not delay delivery, but the receiver may reject events, which triggers retries.
