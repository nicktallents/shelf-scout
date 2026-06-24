# ADR 0006 — Tech stack

- **Status:** Accepted
- **Date:** 2026-06-24
- **Scope:** #6 Platform & hosting foundation
- **Deciders:** nicktallents

## Context

Shelf Scout is the **first app of an aspirational self-hosted suite** on a single domain.
The stack chosen here is the suite-wide default, not just this app's. The clean-slate reset
(2026-06-24) discarded the prior ADRs; this re-confirms the stack on current grounds.

The owner's posture for this session is **ship-first** ("best tool for the job"), with
learning treated as incidental and flagged ad hoc. The owner is fluent in ASP.NET Core and
Vue, and is deliberately learning PostgreSQL through this project. Core constraint: **no
recurring cost beyond the domain name.**

We also considered whether "stack choice" warranted its own separate grilling session. It
does not: stack, hosting topology, and the PWA approach are tightly coupled (the DB choice
drives backups; the backend choice drives how identity is received from the auth proxy;
SPA-vs-SSR drives the service worker), so splitting stack out would force re-establishing
shared context cold. Stack is decided here as the first and heaviest artifact of this scope.

## Decision

The suite-wide stack is:

- **Backend:** ASP.NET Core Web API + EF Core + Npgsql.
- **Frontend:** Vue 3 + TypeScript + Vite.
- **Database:** PostgreSQL.

**Per-app deployment unit:** each suite app is a **single container** that serves both its
built SPA (published to `wwwroot`, SPA-fallback routing) **and** its API on **one origin**.
No separate static-file container; no CORS between SPA and API.

**Why PostgreSQL over SQLite:** the deciding factor is the *suite*, not learning (though the
owner is independently learning Postgres and wants extra explanation on Postgres-specific
mechanics). One Postgres cluster shared across the suite — **one database per app** — is a
cleaner long-term topology than N SQLite files: it lets any future app use concurrency,
JSONB, or full-text search without re-deciding, and it lets a single backup pipeline cover
the whole suite. The cost is one always-on container, which is acceptable. EF Core migrations
are identical between SQLite and Postgres, so the door to reverting is not closed.

> Note: scope #1 (domain model) also chose Postgres-over-SQLite (its ADR 0003) from the
> domain-data angle. This ADR confirms the same decision on suite-infrastructure grounds; the
> two are consistent, not competing.

## Consequences

- One container per app keeps the suite's "minimal containers" value: SPA + API on one
  origin → no CORS, trivial cookies/headers, one clean service-worker origin scope.
- The backend image rebuilds when the frontend changes (negligible at this scale).
- A shared Postgres instance is a single point of failure for the whole suite — addressed by
  the backup strategy in ADR 0007.
- The frontend skeleton (Vite + TS strict + ESLint/Prettier + Pinia + Vue Router) becomes the
  **reference template** app #2 copies (see `CONTEXT.md` → Suite conventions).
- Postgres-specific decisions (isolation, backups, pooling, extensions) carry extra
  explanation per owner preference; see ADR 0007.

## Rejected alternatives

- **SQLite** — zero extra containers and trivial file-copy backups, but a weak suite-wide
  default the moment two apps or a background worker want the same data.
- **Separate static (nginx) container for the SPA** — unnecessary split; no independent
  scaling or CDN need at hobby scale.
- **A separate "stack choice" session** — rejected; stack is inseparable from topology/PWA.
