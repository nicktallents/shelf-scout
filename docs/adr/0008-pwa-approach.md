# ADR 0008 — PWA shell & service worker approach

- **Status:** Accepted
- **Date:** 2026-06-24
- **Scope:** #6 Platform & hosting foundation
- **Deciders:** nicktallents

## Context

Shelf Scout is installed to the iOS home screen and its **primary use case is reviewing
reports of what's coming due**. Its **secondary** use case is Web Push nudges, which on iOS
require a home-screen install, iOS 16.4+, explicit permission, and a **registered service
worker**. So a service worker is mandatory infrastructure regardless of offline ambitions.

The question is how far the service worker goes: shell/asset caching only, offline data
*read*, or full offline read/write sync. Offline data sync is already deferred to v2 in the
project notes. Core constraint: no recurring cost.

## Decision

**v1 ships app-shell asset caching only** (option A): the service worker caches the built
static assets (JS/CSS/icons/shell HTML) so the installed PWA launches instantly and is the
prerequisite for Web Push. **All data still requires the network** — open the app offline and
the shell appears but reports are empty.

- Generated via **`vite-plugin-pwa`** (Workbox under the hood) — near-zero hand-written
  service-worker code, with a manifest (icons, theme color matching the suite theming tokens).
- The data layer is written cleanly so that **offline data *read* (option B)** — caching
  inventory/report API responses for offline review — can be added as a **later release**
  without reworking the service worker's foundations.

## Rationale

- The realistic usage for "review what's coming due" is at home (planning) or at the store —
  both have connectivity. The dead-zone-review scenario is rare enough that paying option B's
  cache-invalidation/staleness tax in v1 isn't justified.
- Option A already delivers what v1 needs: installable home-screen app, instant launch, and the
  registered SW that Web Push depends on.
- Full offline write sync (option C: IndexedDB + background sync + conflict handling) is a
  project of its own and stays deferred to v2.

## Consequences

- The service worker exists from day one (required by Web Push), so adding option B later is an
  extension, not a new capability.
- Offline users see an empty shell in v1 — acceptable given the connectivity profile of the
  primary use case.
- **Revisit trigger:** if real usage includes connectivity gaps (e.g. rural/cabin grocery
  planning), promote option B — cache-first reads for the report views — to a release.
- The PWA manifest and SW registration pattern become part of the suite reference template
  (see `CONTEXT.md` → Suite conventions).

## Rejected (for v1)

- **Option B (offline read) now** — primary use case is reviewing reports, which tempts toward
  it, but connectivity is normally present; deferred to a later release.
- **Option C (offline write sync)** — deferred to v2; significant complexity.
