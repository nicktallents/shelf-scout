# Baseline & Scope Map

Output of the baseline grilling session (2026-06-24). This is a **clean-slate reset**: all prior
`CONTEXT.md`/ADR decisions were discarded and must be re-confirmed inside the scope sessions below.
This document is the shared charter the parallel grilling sessions start from — it is **not** a set of
final decisions. Each scope session produces its own ADR(s) and contributes to a fresh `CONTEXT.md`.

## Confirmed baseline (cross-cutting)

These were settled at the baseline level because they constrain multiple scopes. Individual scope
sessions may still refine *how* they are implemented, but should not reopen the decision itself without
flagging it.

| Foundation | Decision |
|---|---|
| **Suite** | Aspirational. This is the **first** app of a future self-hosted suite on a single domain. Cost containment is a primary architectural driver. |
| **Sharing** | **Multi-household**, invite-gated (no open signup). One shared inventory per household; households are isolated from each other. |
| **Auth** | **Federated Google OAuth** via a suite-level auth proxy. Access managed by an approved-users allowlist; no in-app auth. Additional providers are post-v1. |
| **Network** | **Public internet**, gated by the auth proxy. The proxy is therefore a security-critical, internet-facing component. |
| **Primary use case** | Open the app and **review reports of what's coming due** — a meal-planning / grocery-run tool. The review experience is the heart of the app. |
| **Secondary use case** | **Web Push** notifications as a nudge. Accepts iOS constraints: requires home-screen install, iOS 16.4+, explicit permission, and occasional delivery flakiness. |
| **Capture (v1)** | **Manual entry + client-side OCR** of the printed expiry date. All client-side (no recurring cost). |
| **Capture (future)** | Barcode scanning + a **local** AI agent that suggests an expiry date from the item. Deferred. |
| **Core constraint** | No recurring API or infrastructure cost beyond the domain name. |

## Scope map

### 🌳 Grill first — gates everything else

1. **Domain model & data spine** — entities and relationships: User, Household, Membership/roles,
   Location, Item, Meal, lifecycle states (Consumed / Expired / Expiring Soon), Alert Threshold.
   Every other scope reads or writes this; its vocabulary must be fixed before the parallel work
   starts. (Use `/domain-modeling`.)

### 🏗️ Grill alongside the spine — cross-cutting constraints

6. **Platform & hosting foundation** — tech stack (backend / frontend / DB), PWA shell + service
   worker, the shared navigation/conventions future suite apps inherit, Docker Compose deployment,
   the public-facing reverse proxy, backups, cost ceiling. *(Open question: split "stack choice" out
   as its own scope if learning goals warrant it.)*

### ⚙️ Then grill in parallel — each depends on the spine, not on each other

2. **Auth & onboarding** — proxy + Google integration, approved-users allowlist, Invite Token flow,
   joining a household, admin vs. member roles.
3. **Capture** — manual entry UX + client-side OCR pipeline for expiry dates; handling OCR errors.
4. **Reports & review** *(primary experience — the deepest session)* — **GRILLED 2026-06-27.**
   Resolved into the **review/inventory IA** (the Shelf landing, By Status / By Location, search,
   the consume flow incl. Quick Path / Bulk Consume / Undo, and the visual "coming due" language) +
   the **Plan surface** (a derived restock view). See ADR 0016 (review IA) and ADR 0017 (Plan /
   restock + deferred meal-planning). The session split two things out: **Reports → its own scope
   #7**, and **meal-planning → a deferred scope** (no charter yet). A persistent **shopping list was
   considered and dropped** (the derived restock view covers grocery-run with no new data model).
5. **Notifications** — Web Push / VAPID, per-device subscription management, the daily background
   check, threshold logic. *(Resolved — ADR 0014/0015.)*
7. **Reports & analytics** *(split out of #4)* — **GRILLED 2026-06-29.** The past-tense Reports tab:
   a single scroll of headline Waste Rate / monthly Trend / what-you-waste-most / recent removals,
   with the two tiers surfaced as one seamless series (90-day boundary = drill-availability only).
   See ADR 0018 (Reports IA) and ADR 0019 (aggregation semantics). Number-led + CSS bars (charts
   deferred); cross-household and by-location aggregates deferred but data-enabled. Builds on ADR
   0004 + ADR 0016.

**Deferred (not chartered yet):** **meal-planning** ("what can I cook" — local AI + web recipes +
save/bookmark, gated by the no-recurring-cost web-discovery risk; recorded in ADR 0017).

## Recommended sequencing

1. Grill **#1 (domain model)** and **#6 (platform/hosting foundation)** first — together they fix the
   vocabulary and the build constraints.
2. Fan out **#2–#5** in parallel once the spine is stable.
