# API returns HTTP 403: checking token scopes and permissions

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-015 · **Applies to:** Ledgerline 2.2 and later

Most `403 Forbidden` responses from the public API are caused by configuration, not by a product defect. Check the `error` field in the response body first.

| `error` value | Meaning | Fix |
|---|---|---|
| `insufficient_scope` | The token lacks the scope the endpoint requires | Create a token with the required scope |
| `ip_not_allowed` | The caller's IP is not on the allowlist | See KB-016 |
| `plan_not_entitled` | The plan does not include API access | API access requires the Business plan or higher |
| `legacy_key_disabled` | SSO enforcement disables legacy user API keys | Move to a scoped service token |

## Required scopes for common endpoints

| Endpoint | Scope |
|---|---|
| `GET /v2/reports` | `reports:read` |
| `PUT /v2/reports/{id}` | `reports:write` |
| `POST /v2/exports` | `exports:create` |
| `GET /v2/accounts` | `accounts:read` |
| `POST /v2/webhooks` | `webhooks:manage` |

A token can never exceed the permissions of the user or service role it belongs to (KB-007).

## Expired tokens

Expired tokens currently return `403 insufficient_scope` instead of `401 token_expired`. This is a known issue tracked as BUG-244. If the scopes look correct, check the token's expiry date in **Settings > API Tokens**.
