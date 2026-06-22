# Google SSO via oauth2-proxy — no in-app authentication

The app has no login UI, no passwords, and no user management. All authentication is delegated to oauth2-proxy backed by Google OAuth. The app trusts the `X-Auth-Request-User` header injected by the proxy and uses the Google `sub` claim as the stable primary key for Users.

## Considered options

- **Authentik** — full self-hosted IdP with admin UI, supports Google federation, OIDC provider for downstream apps. Rejected: heavier stack (~500MB+), and its user-management features are unnecessary when Google already owns credentials and the Household model lives in the app DB.
- **Authelia** — lightweight forward-auth gateway. Rejected: OIDC provider support is limited; harder to extract a stable per-user identity for app-level data association.
- **Built-in auth** — passwords, sessions, and password reset in the app itself. Rejected: this app is part of a suite; centralising auth avoids duplicating credential management across apps.

## Access control

oauth2-proxy is configured with an `--authenticated-emails-file` allowlist. Only specific Google addresses in that file can authenticate — any other Google account receives a 403 even after a valid Google login. This is the suite-level gate; Household membership is a separate, app-level concern managed via Invite Tokens.

Adding a person to the suite is a deliberate three-step admin act: (1) approve their device on Tailscale, (2) add their email to the allowlist file and restart the proxy, (3) send them a Household Invite Token. There is no in-app UI for steps 1 or 2.

A User who is on the allowlist but has not yet joined a Household will see an empty state prompting them to create one or wait for an invite. This is expected behaviour.

## Consequences

The Google `sub` is the User's permanent identity. Migrating to a different identity provider later requires a data migration to re-key all user-owned records.
