# Provider-agnostic User identity via a separate Identity table

The app owns a lightweight `User` record keyed on an internal surrogate ID. External
login credentials live in a separate `Identity` table, unique on `(provider, subject)`,
pointing at a `User`. At v1 every User has exactly one Identity: `('google', <sub>)`,
where `subject` is the Google OIDC `sub` claim (opaque, stable, immutable). `email` and
`display_name` are stored on `User` as a mutable, non-authoritative profile cache refreshed
from the token on login. The approved-users allowlist is keyed on email (what a human admin
types); a User + Google Identity are created and bound on first successful login.

## Context

Auth is federated Google OAuth via a suite-level proxy, with additional providers explicitly
post-v1. The spine must not have to be reshaped when a second provider arrives.

## Considered options

**Trust the proxy entirely, no local User table (key everything on `sub`).** Rejected: the
app needs a local identity to anchor Memberships, `created_by`, and Push Subscriptions, and
needs its own audit fields.

**Single `User` table keyed on `google_sub` (or with nullable per-provider columns).**
Rejected: it cannot represent the same person authenticating via two providers without a
schema change and data migration, and it bakes a provider name into the spine. Email keys
were also rejected because email is mutable and reassignable.

**`User` (surrogate PK) + `Identity(provider, subject)` join (chosen).** Costs one extra
table and a join on login now; makes "add a provider" purely additive later, with no change
to anything that references `User`.

## Consequences

The account-linking policy — what to do when a future provider presents an email matching an
existing User (auto-link by verified email vs. explicit "link account") — is deliberately
left open and owned by the auth & onboarding scope. The spine only needs to *permit* linking,
which the Identity table does.
