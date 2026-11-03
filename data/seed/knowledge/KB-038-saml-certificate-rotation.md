# Rotating the SAML signing certificate

> Synthetic article for the ResolveNest demo. Ledgerline is a fictional product.

**Article:** KB-038 · **Applies to:** Ledgerline 2.0 and later · **Related bug:** BUG-161 (fixed in 2.0.3)

1. In **Settings > Security > SSO**, upload the new IdP certificate as a secondary certificate.
2. Switch the IdP to sign with the new certificate.
3. Once all users can sign in, remove the old certificate.

If sign-in fails with `Invalid signature` right after rotation, the IdP is signing with a certificate that has not been uploaded.

Before 2.0.3, an uploaded certificate did not take effect until the next deployment (BUG-161).
