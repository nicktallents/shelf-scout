# ADR 0012 — Authentik as the combined-edge authentication engine

- **Status:** Accepted
- **Date:** 2026-06-25
- **Scope:** #2 Auth & onboarding
- **Deciders:** nicktallents
- **Supersedes:** the *auth-proxy slot* of ADR 0007 (the `forward_auth` → oauth2-proxy
  decision). ADR 0007's routing/edge/compose/backup decisions remain in force.
- **Amends:** ADR 0001 (the `provider`/`subject` values of the Identity key).

## Context

The baseline is federated Google OAuth via a suite-level auth proxy, public internet behind
that proxy, an approved-users allowlist, no in-app auth. ADR 0007 fixed the *topology* and
ratified **oauth2-proxy** as the `forward_auth` decision service, deferring all auth *policy*
(allowlist, invites, roles) to this scope.

A new cross-project requirement reopened the engine choice: the owner is standing up a UGreen
NAS to host media apps (Jellyfin/Plex + the *arr stack), on the **same domain and edge** as the
suite, and wants to gate **who can reach the app suite vs. who can reach media** on a per-person
basis. oauth2-proxy cannot express this — it is a thin OIDC→headers bridge with no concept of
groups or per-app policy. That forced a re-evaluation of the auth engine for the whole edge.

Constraint unchanged: no recurring cost beyond the domain name.

## Decision

### Engine: Authentik (one IdP for the whole `*.example.com` edge)

The three candidates map cleanly against two hard requirements — **Google federation** (baseline)
and **per-app/per-group access policy** (the suite-vs-media requirement):

| Candidate | Google login | Suite-vs-media gating |
|---|---|---|
| oauth2-proxy | ✓ | ✗ (no policy engine) |
| Authelia | ✗ (cannot act as an OIDC *client* — it is a provider only; verified June 2026, on roadmap but unshipped) | ✓ |
| **Authentik (chosen)** | ✓ (federates Google as a login *source*) | ✓ (per-application access policies, forward-auth outpost) |

Authentik is the only candidate satisfying both. Authelia was eliminated on a verified fact:
it cannot federate to an upstream provider for first-factor login, so adopting it would force
local username/password accounts and abandon the Google baseline.

### Topology: NAS-as-host, combined edge

The NAS becomes the single always-on Docker host. **Caddy** remains the sole internet-facing
component and the only thing publishing ports; it issues a `forward_auth` subrequest to the
**Authentik embedded outpost** (internal-only). On success Caddy forwards to the upstream with
Authentik-injected identity headers (`X-authentik-username`, `X-authentik-email`,
`X-authentik-uid`, `X-authentik-groups`). Authentik's own dependencies (PostgreSQL, Redis) are
internal-only.

### Identity contract (amends ADR 0001)

Authentik now sits between Google and the app, so the app no longer sees Google directly —
**Authentik is the app's IdP; Google is an upstream source Authentik federates.**

- The app keys Identity on **`X-authentik-uid`**: `provider = 'authentik'`, `subject = <uid>`
  (Authentik's stable internal user UID). This replaces ADR 0001's `('google', <sub>)`.
- **Upstream-provider linking moves up into Authentik.** Adding Apple/Microsoft later = adding a
  source in Authentik and linking it to the Authentik user *there*; the app keeps seeing one
  stable `uid`. The app's Identity table stays single-row-per-User indefinitely. The "explicit
  link, never auto-link by email" policy that ADR 0001 deferred is now **Authentik
  configuration, not app code**.
- `email`/`display_name` remain a mutable, non-authoritative profile cache, refreshed from the
  header on each request; email mismatches update the cache silently (uid is the key).
- The app is **stateless** with respect to auth: a per-request middleware resolves
  `uid → Identity → User`, **lazy-provisions** a User + Identity on first sight, and **fails
  closed** if the identity header is absent.

### Access tiers

Two worlds, because we control our own apps but not the third-party media apps:

- **Suite apps we build** (Shelf Scout, future apps): subdomains are **edge-open to any
  authenticated user**; each app **self-gates in-app** via its own per-app `Membership`. Per-app
  isolation is *inherent* — membership lives in each app's own database, so an invite to one app
  grants nothing in any other. This is **functional** isolation, not edge-enforced.
- **Third-party media apps**: gated at the edge by the **`media-users`** Authentik group (they
  can't run our invite pattern). **Jellyfin/Plex bypass forward-auth entirely** and use their
  own accounts — forward-auth breaks their native (TV/mobile) clients, which can't navigate a
  browser login portal. Only weak-auth admin UIs (*arr, dashboards) sit behind forward-auth.
- **`family` Authentik group**: the trusted inner circle — full reach to all suite apps and the
  **privilege to create households** (see ADR 0013). This is the "approved-users allowlist",
  relocated from oauth2-proxy's flat email file into an Authentik group.

The escape hatch for the functional-isolation trade-off: if a future suite app ever holds
genuinely sensitive data, promote *that one app* to its own edge group without touching the others.

### Sessions & sign-out

- SSO session cookie on the parent domain (`.example.com`); **30-day sliding** expiry (low data
  sensitivity, frequently-opened PWA).
- **Sign-out** in the app shell hits Authentik's session-invalidation flow, clearing the SSO
  session **suite-wide** (signing out of Shelf Scout signs you out of media too).
- **2FA:** end users rely on **Google's own 2FA** (the federated first factor) — no second
  Authentik prompt in v1. The **Authentik admin account requires WebAuthn/TOTP**.

### Security invariants (must never be undone)

1. **Only Caddy publishes ports.** Authentik, the app, Postgres, Redis are internal-only.
2. **Header trust boundary:** Caddy **strips any client-supplied `X-authentik-*`** before the
   outpost injects its own; the app **fails closed** on a missing identity header. The app must
   never see a client-supplied identity header.
3. **Multi-tenant isolation is the real data boundary:** every query is scoped by `household_id`
   derived from the authenticated User's `Membership` rows; the server never trusts a
   client-supplied `household_id` without verifying membership.
4. **CSRF defense is still required** despite header-based auth: the browser still holds
   Authentik's SSO cookie, so a cross-origin page could trigger an authenticated state-changing
   request. Mitigation: `SameSite=Lax` on the Authentik cookie **plus** an `Origin`/
   `Sec-Fetch-Site` check on every mutating endpoint.
5. **Authentik is the crown jewel** — a larger, statefuller surface than oauth2-proxy. Keep it
   patched; admin behind WebAuthn; admin interface IP-restricted / not casually exposed.
6. Secrets (Authentik secret key, DB/Redis creds, Google client secret) via **Docker secrets**.

## Considered options

- **Keep oauth2-proxy (ADR 0007 ratification).** Rejected: no group/policy engine, so it cannot
  gate suite-vs-media. Would require a *second* auth-decision service for the NAS — two systems,
  no shared SSO.
- **Authelia.** Rejected on a verified fact: cannot act as an OIDC client / federate to Google
  for first-factor login; would force local accounts and break the Google baseline.
- **Separate edges (suite on oauth2-proxy, media on its own gateway).** Rejected: duplicated
  infrastructure, no single sign-on, and the NAS brings a policy-capable gateway into the picture
  regardless — running both is pure overhead.
- **Authentik (chosen).** Heavier to operate (Postgres + Redis + a full IdP to patch) but the
  only option meeting Google-federation **and** per-group gating, and it absorbs the
  multi-provider-linking problem entirely.

## Consequences

- **Provider #2 (Apple/Microsoft) no longer touches the app** — it's an Authentik source change.
  This retires the "adding a provider = reopen the proxy" trigger noted in ADR 0001.
- **Authentik is now the single most security-critical component** and the largest patch surface
  on the edge — an accepted cost of the combined-engine choice.
- **ADR 0007's auth-proxy slot is superseded**; its routing, edge (Caddy/Cloudflare), compose,
  isolation, and backup decisions stand. The `forward_auth` target changes from oauth2-proxy to
  the Authentik outpost; the security invariants carry over with the new header names.
- This ADR currently lives in the Shelf Scout repo for convenience (the edge/infra repo does not
  yet exist). **TODO:** migrate the edge/engine decision to the edge/infra repo when created;
  Shelf Scout should then retain only the *identity-header contract it consumes*.
- **Recurring cost remains zero** beyond the domain (Authentik, Caddy, Cloudflare DNS, Let's
  Encrypt, Google OAuth are all free).
- The app can be built and tested against the **identity-header contract** today (a fixed header
  trusted from the edge); the specific engine behind Caddy is swappable without app rework.
