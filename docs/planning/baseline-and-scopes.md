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
4. **Reports & review** *(primary experience — likely the deepest session)* — dashboards/views:
   expiring-soon, expired, inventory-by-location, and the meal-planning / grocery-run framing.
5. **Notifications** — Web Push / VAPID, per-device subscription management, the daily background
   check, threshold logic.

## Recommended sequencing

1. Grill **#1 (domain model)** and **#6 (platform/hosting foundation)** first — together they fix the
   vocabulary and the build constraints.
2. Fan out **#2–#5** in parallel once the spine is stable.
