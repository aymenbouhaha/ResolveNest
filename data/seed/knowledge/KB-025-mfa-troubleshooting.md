# Multi-factor authentication troubleshooting

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-025 · **Applies to:** all versions · **Related bug:** BUG-185 (fixed in 2.1.1)

| Symptom | Cause | Fix |
|---|---|---|
| Codes are always rejected | Phone clock drift | Enable automatic time on the device |
| User lost their device | No backup codes | An admin resets MFA in **Users > Security** |
| MFA prompt reappears in a loop | Safari private browsing before 2.1.1 (BUG-185) | Upgrade, or use a normal browser window |
| SSO users are asked for Ledgerline MFA | MFA is enforced in both the IdP and Ledgerline | Disable Ledgerline MFA for SSO users |

Admins cannot see or recover MFA secrets. Resetting MFA forces the user to enroll again.
