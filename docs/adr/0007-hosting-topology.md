# ADR 0007 — Hosting & deployment topology

- **Status:** Accepted
- **Date:** 2026-06-24
- **Scope:** #6 Platform & hosting foundation
- **Deciders:** nicktallents

## Context

The suite runs self-hosted on the owner's always-on Docker host. A baseline change in the
2026-06-24 reset moved the suite from **Tailscale-only** to the **public internet, gated by a
suite-level auth proxy**. That makes the edge (reverse proxy + auth proxy) a security-critical,
internet-facing component — the central concern of this ADR. Auth *policy* (allowlist, invite
tokens, role mapping) is owned by scope #2; this ADR fixes only the *topology* those policies
sit in. Core constraint: no recurring cost beyond the domain name.

## Decision

### Routing: subdomain-per-app

Each app is its own origin: `shelfscout.example.com`, `<app>.example.com`, … Chosen over
path-per-app primarily for the **service worker**: a PWA wants to own its origin (SW scope `/`,
clean installed-app identity, simple asset paths). Path-based PWAs collide on registration and
caching and require base-path-aware assets.

- **TLS:** a single **wildcard cert** `*.example.com`, so adding an app needs no cert work.
- **SSO cookie:** the auth proxy sets its session cookie on the parent domain (`.example.com`)
  so one Google login covers every subdomain. (Cookie/session details → scope #2.)

### Edge: Caddy + Cloudflare DNS

- **Caddy** is the reverse proxy and sole internet-facing component: automatic HTTPS with
  automatic renewal, small readable config, Go (memory-safe), secure defaults (HSTS, modern
  TLS). It needs a **custom image bundling the `caddy-dns/cloudflare` module** (built via
  `xcaddy`) for DNS-01.
- **Cloudflare** provides DNS (free, robust API) enabling **wildcard TLS via the DNS-01 ACME
  challenge** — no per-app cert dance, no reliance on inbound port 80 for validation. Register
  the domain at the cheapest registrar; point nameservers at Cloudflare to avoid lock-in.
- Rejected: Traefik (more moving parts / label sprawl than a no-enterprise suite needs);
  nginx + certbot (manual renewal); NPM (extra stateful GUI service).

### Auth proxy slot: `forward_auth` to oauth2-proxy

Caddy is the single front door and terminates TLS. For each request it issues a subrequest to
**oauth2-proxy** ("is this session valid?"). oauth2-proxy owns the Google OAuth dance and the
session cookie; on success Caddy forwards to the app with identity headers (`X-Forwarded-Email`,
`X-Forwarded-User`/`sub`). oauth2-proxy is an internal **auth-decision service** — never in the
data path, never internet-exposed. Adding an app is another `forward_auth` block pointing at the
same oauth2-proxy.

**Non-negotiable security invariants (topology-level):**

1. **Only Caddy publishes ports** (80/443). App and oauth2-proxy containers expose ports *only*
   on the internal Docker network — never via `ports:` to the host. A published app port lets an
   attacker reach the app directly and **spoof identity headers**, bypassing auth entirely.
2. **The app trusts identity headers only because they cannot arrive any other way** — enforced
   by (1) plus Caddy **stripping any client-supplied** `X-Forwarded-*`/identity headers before
   injecting its own. The app must never see a client-supplied identity header.

(Deferred to scope #2: approved-users allowlist, invite-token flow, Google `sub` → app user
mapping, admin/member roles, sign-out URL specifics.)

### Compose structure: edge stack + per-app stacks on a shared external network

- **Edge stack** (`edge/compose.yaml`): Caddy (publishes 80/443 — the only published ports),
  oauth2-proxy (internal), Postgres (internal).
- **App stack** (`shelf-scout/compose.yaml`): the app container (SPA+API), internal only.
- **Shared external network** (e.g. `suite-edge`): Caddy reaches any app; apps reach Postgres;
  nothing but Caddy is reachable from host/internet. Adding an app = one compose file + one
  Caddy block, with the security-critical edge untouched.

### Data isolation & backups

- **Isolation:** one Postgres cluster (one container), **one database + one role per app**.
  Physical isolation between apps; per-app backup/restore granularity; a compromised app
  credential unlocks only that app's database. (A "cluster" is the single server instance, not
  multiple machines.) Preferred over schema-per-app, whose isolation depends only on grants.
- **Backup mechanism:** **nightly logical `pg_dump` per database**, not PITR/WAL archiving. The
  data is hand-entered groceries — losing "since last night" costs a few re-typed items, nowhere
  near PITR's operational weight. Revisit PITR per-cluster only if a future app holds
  loss-sensitive data.
- **Pipeline:** dumps are **compressed, encrypted at rest** (age/gpg; key stored separately),
  and **rotated GFS-lite (7 daily + 4 weekly)**. The job writes encrypted files to a directory;
  "ship off-host" is the final, pluggable step.
- **Off-host:** **none in v1** (no hardware yet). Accepted, documented limitation — local-only
  backups protect against logical errors (bad migration, accidental delete) but **NOT** disk
  failure, host loss, or ransomware. The pipeline is designed so adding an off-host target later
  (rsync over Tailscale to a NAS/second box) is one command, not a redesign.
- **Volumes:** named volume for Postgres `PGDATA`; named volume for Caddy's ACME/cert data.
- **Migrations:** EF Core migrations applied **on app startup** (simple, no separate deploy
  step; safe given single replicas).

## Consequences

- One internet-facing component (Caddy) to harden and patch.
- Suite security rests on the two header-trust invariants; they must survive every future app
  and compose change. They are stated prescriptively here so they can't be silently undone by
  publishing a port.
- **TODO (one-time):** perform a restore drill — restore a `pg_dump` into a throwaway database —
  before trusting the backup pipeline. A backup never restored is not a backup.
- **Known risk accepted for v1:** local-only backups (no off-host copy). Revisit when hardware
  exists.
- Security headers (CSP/HSTS/`X-Content-Type-Options`/`Referrer-Policy`) are applied centrally
  at Caddy for all apps (see `CONTEXT.md` → Suite conventions).
