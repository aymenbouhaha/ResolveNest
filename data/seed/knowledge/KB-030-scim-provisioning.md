# SCIM provisioning troubleshooting

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-030 · **Applies to:** Ledgerline 2.1 and later · **Related bugs:** BUG-240, BUG-260

## Checklist

- The SCIM token belongs to an Admin service user.
- IdP groups are mapped to Ledgerline roles in **Settings > Security > SCIM**.
- Group names match exactly, including case.

## Known issues

- On 2.3.1, users in groups whose names contain `/` fell back to the Viewer role (BUG-240, fixed in 2.4.0).
- Before 2.4.2, API tokens of deprovisioned users stayed active (BUG-260). After upgrading, review **Settings > API Tokens** for tokens owned by deactivated users.
