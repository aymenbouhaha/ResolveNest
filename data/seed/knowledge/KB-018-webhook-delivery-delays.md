# Troubleshooting webhook delivery delays

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-018 · **Applies to:** all versions

## How delivery works

- Events are dispatched within seconds of being created.
- A delivery succeeds when the receiver answers with a 2xx status within 10 seconds.
- Failed or slow deliveries are retried after 1, 5, 15 and 45 minutes.
- Ordering between events is **not guaranteed**. Use the `occurred_at` field to order events.

## Diagnosing a delay

1. Open **Settings > Webhooks > Delivery log** and find the event ID.
2. Compare `created_at` with the first attempt time. A gap here is on our side, so include the event IDs when you contact support.
3. Check the receiver's responses. Any `429`, `5xx` or timeout schedules a retry, which appears as a delay of minutes.
4. Check proxies or firewalls in front of the receiver.

## What to send to support

Endpoint URL, at least three delayed event IDs, and the time window in UTC.
