# Webhook endpoint configuration checklist

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-028 · **Applies to:** all versions · **Related bug:** BUG-167 (fixed in 2.0.1)

- The endpoint URL uses HTTPS with a publicly trusted certificate.
- The endpoint answers within 10 seconds. Queue the work and respond `200` immediately.
- Firewalls allow the published Ledgerline egress IP ranges.
- The endpoint is subscribed to the right event types.
- The test button sends a real delivery. Before 2.0.1 it always reported success (BUG-167), so also check the delivery log.

See KB-018 for delivery delays and KB-027 for signatures.
