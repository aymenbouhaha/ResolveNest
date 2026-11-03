# Dashboard performance best practices

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-023 · **Applies to:** all versions · **Related bug:** BUG-257 (fixed in 2.4.2)

## Recommendations

- Keep dashboards under 25 widgets. Split large dashboards by audience.
- Prefer pre-aggregated datasets over raw transaction tables.
- Use dashboard-level filters instead of per-widget filters.
- Avoid widget cache overrides shorter than 5 minutes.

## Known issue in 2.4.0 and 2.4.1

Dashboards with more than 40 widgets could take over 20 seconds to load (BUG-257). Upgrade to 2.4.2.

## Collecting evidence

If a dashboard is still slow, send the dashboard URL, the time of the slow load (UTC) and the browser's network timing export.
