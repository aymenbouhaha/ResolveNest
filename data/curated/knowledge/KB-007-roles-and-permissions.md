# Roles and permissions reference

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-007 · **Applies to:** Ledgerline 2.2 and later

## Built-in roles

| Role | Typical use |
|---|---|
| Admin | Full access, including security settings |
| Analyst | Build and export reports and dashboards |
| Viewer | View shared reports and dashboards |

## Custom roles

Custom roles combine individual permissions such as `Reports: view`, `Reports: export`, `Accounts: view` and `Admin: tokens`.

A custom role can be saved with no permissions. This is intentional so that admins can stage onboarding before granting access. Users with an empty role see a blank workspace.

## Roles and API tokens

Service tokens inherit the permissions of their service role. A token scope such as `accounts:read` works only if the role also has `Accounts: view`. Otherwise the API returns `403` (see KB-015).

## Export permissions

The **Export** menu appears only for roles with `Reports: export`.
