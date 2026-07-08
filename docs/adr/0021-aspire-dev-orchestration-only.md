# ADR 0021 — Aspire orchestrates dev/test only; production stays the ADR-0007 compose stack

- **Status:** Accepted
- **Date:** 2026-07-08
- **Scope:** #6 Platform & hosting foundation
- **Deciders:** nicktallents
- **Relates to:** ADR 0006 (tech stack), ADR 0007 (hosting topology), ADR 0008 (PWA).
  Does **not** amend `CONTEXT.md` — the terms introduced here (AppHost, "verify stack") are
  tooling, not domain language.

## Context

The daily developer loop needs breakpoint debugging from VS Code, and there was no dev database
story at all: `Program.cs` reads a `"ShelfScout"` connection string that existed in no
`appsettings*.json`, no user-secrets, and no compose file. Tests already spin up an ephemeral
Postgres via Testcontainers (auto-reaped by Ryuk), but the *app* had nothing.

We also wanted to confirm the container configuration is correct (build, non-root uid 1000,
network DB resolution, `wwwroot` serving, migrate-on-startup, `/health`) without giving up
breakpoints. Introducing **.NET Aspire** as a dev-time orchestrator answers both — it maps a
container's lifetime to the debug session (F5 starts Postgres + API + Vite, stop reaps all
three) and its VS Code extension attaches C# and TS/JS debuggers across projects.

Aspire, however, also ships a **deployment** path (`aspire deploy` / manifest generation). Using
it would produce a second, divergent way to run the suite in production — one that knows nothing
of Caddy, Authentik, the shared external network, or the two header-trust security invariants
that ADR 0007 states prescriptively. That is the risk this ADR exists to fence off.

## Decision

- **Aspire is a dev/test orchestrator only.** It lives in a `ShelfScout.AppHost` project that is
  never part of a production build or deployment. We do **not** use `aspire deploy`, Aspire
  manifests, or any Aspire-generated compose/manifest as a production artifact.
- **Production remains exactly the ADR-0007 shape:** the single multi-stage `Dockerfile` → one
  SPA+API container, run behind Caddy + Authentik via hand-written per-app compose on the shared
  external network. The AppHost has no bearing on it.
- **Minimal coupling — no Aspire in the production code path.** The AppHost references the API as
  a project and injects `ConnectionStrings__ShelfScout`; `Program.cs` and the `DbContext`
  registration are unchanged. The API takes **no** Aspire client packages and **no**
  ServiceDefaults. Service discovery, HTTP resilience, and default health endpoints are dead
  weight at one service, and the app already owns its `/health` check. (Dashboard telemetry via
  plain OpenTelemetry is a separate, deferred decision with no Aspire lock-in.)
- **Dev database is ephemeral, session-scoped, and seeded.** No persistent dev volume. Sample
  data (a household, membership for the `DevelopmentFallback` user, locations, items) is seeded
  on startup **gated on `IsDevelopment()`**, with expiration dates computed relative to `now` so
  every fresh boot yields a stable spread across the report buckets. Mode-2 verification
  (Production env) and tests (Testing env) run against a clean DB — they do not seed.

## Rationale

- A dev orchestrator and a production topology answer different questions; letting Aspire own
  both would silently create a deployment path that bypasses the security-critical edge.
- Container lifetime = debug-session lifetime is the cleanest satisfaction of the "temporary
  containers must be cleaned up after the session" requirement; Ryuk backstops crashes, and
  nothing long-lived is created to leak.
- Minimal coupling keeps the production binary free of a dev tool and keeps the ADR-0007
  invariants the single source of truth for how the app is deployed.

## Consequences

- Two ways to run the app locally, by design: **Mode 1** (Aspire, host-native breakpoints, daily)
  and **Mode 2** (`compose.verify.yaml` builds the real Release image + throwaway Postgres, run
  and asserted, no breakpoints). Breakpoints-in-container (vsdbg attach) is a documented,
  unbuilt escape hatch.
- **A future contributor must not "consolidate" on `aspire deploy`.** The AppHost is not a
  deployment tool here; production compose is authored by hand for the edge.
- **Deferred / accepted gap:** Mode 2 does not exercise the fail-closed identity-header contract
  (401 without a header; accepts `X-authentik-uid` when present). That verification belongs to
  the future edge/infra repo. TODO to add a lightweight header check when that repo exists.
- If a second .NET service is ever added that calls the API, revisit ServiceDefaults/service
  discovery then — not before.
