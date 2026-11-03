# Configuring SAML single sign-on

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-005 · **Applies to:** Ledgerline 2.1 and later · **Related bug:** BUG-212 (fixed in 2.2.1)

## Setup checklist

1. Create a SAML application in your identity provider (IdP) using the metadata URL from **Settings > Security > SSO**.
2. Assign users or groups to the application in the IdP. Users who are not assigned see `User not assigned to this application` from the IdP.
3. Map the IdP group attribute if you use SCIM role provisioning.

## Clock skew

Ledgerline accepts assertions whose `NotBefore` time is up to 3 minutes ahead of its own clock (tolerance added in 2.2.1 for BUG-212). If the IdP clock drifts further, sign-in fails with `Assertion not yet valid`, or with a redirect loop on versions before 2.2.1.

Make sure the IdP host synchronizes time with NTP.

## SSO enforcement

Enabling **Require SSO for all users** disables legacy user API keys. Move integrations to scoped service tokens first (KB-015).
