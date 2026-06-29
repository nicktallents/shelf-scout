# Scope Charters

Ready-to-paste opening prompts for the parallel scope grilling sessions. Each charter is self-contained:
paste it as the argument to `/grill-with-docs` in a fresh session. Every session should first read
`docs/planning/baseline-and-scopes.md` for the confirmed baseline.

**Shared rules for every session:**
- Clean slate: re-derive and re-confirm; treat the old `CONTEXT.md`/ADRs (in git history) as inspiration only.
- Honor the confirmed baseline; if you need to reopen a baseline decision, flag it explicitly.
- Core constraint: no recurring cost beyond the domain name.
- Produce: ADR(s) for the decisions made + glossary entries for any new domain terms (fresh `CONTEXT.md`).

---

## 1. Domain model & data spine — GRILL FIRST

> Read `docs/planning/baseline-and-scopes.md`, then grill me on the **domain model and data spine** for
> Shelf Scout. This is the foundation every other scope depends on, so the goal is a precise, fixed
> vocabulary and entity model. Cover: User (keyed on the Google identity), Household, Membership and
> whether roles (admin/member) are needed at all, Location (and whether to pre-seed fridge/freezer/pantry),
> Item (is each physical unit its own record? presence-only or quantity?), Meal (is it just an Item, or
> distinct?), and the lifecycle states — Consumed, Expired, Expiring Soon — and how they're computed vs.
> stored. Pin down the Alert Threshold concept (per-household? per-location? per-item?). Resolve naming
> precisely and record a glossary. Flag anything that other scopes (#2 auth, #3 capture, #4 reports,
> #5 notifications) will need from this model. Produce a fresh `CONTEXT.md` glossary and ADR(s) for the
> non-obvious modeling choices.

## 6. Platform & hosting foundation — GRILL ALONGSIDE #1

> Read `docs/planning/baseline-and-scopes.md`, then grill me on the **platform & hosting foundation** for
> Shelf Scout — this is the first app of a future self-hosted suite on one domain, so decisions here set
> suite-wide patterns. Cover: the tech stack (backend framework, frontend framework, database — blank
> slate, and note I may treat this as a learning vehicle); the PWA shell + service worker (asset caching
> only, or any offline data?); the shared navigation/conventions future suite apps will inherit; Docker
> Compose topology; the public-facing reverse proxy that fronts the auth proxy (and how the auth proxy
> slots in — coordinate with scope #2); backups and data durability; and how every choice stays within the
> no-recurring-cost constraint. Decide whether "stack choice" deserves to be its own separate session.
> Produce ADR(s) for stack, hosting topology, and PWA approach.

## 2. Auth & onboarding — PARALLEL (needs #1's Household/Membership)

> Read `docs/planning/baseline-and-scopes.md`, then grill me on **auth & onboarding** for Shelf Scout.
> Baseline: federated Google OAuth via a suite-level auth proxy, public internet behind that proxy,
> approved-users allowlist, no in-app auth, invite-gated multi-household. Grill: which proxy
> (oauth2-proxy vs. Authelia vs. alternatives) and why; exactly how the app receives identity from the
> proxy and keys Users; how the approved-users allowlist is administered (and the friction of adding a
> friend); the Invite Token flow end-to-end (generation, sharing, redemption, expiry, single-use?); what
> happens to an allowlisted user who hasn't joined a household yet; whether admin/member roles are needed
> (coordinate with #1); and the security posture now that the proxy is internet-facing. Produce ADR(s) for
> the auth approach and the onboarding/invite flow.

## 3. Capture — PARALLEL (needs #1's Item shape)

> Read `docs/planning/baseline-and-scopes.md`, then grill me on **item capture** for Shelf Scout. v1 is
> manual entry + client-side OCR of the printed expiry date (all client-side, no recurring cost; barcode +
> local-AI date suggestion are deferred). Grill: the fastest possible manual-entry UX (name, location,
> expiry) since capture friction is what makes or breaks the primary reports loop; the OCR pipeline
> (library choice, camera UX, how the user confirms/corrects an OCR'd date, failure fallback to manual);
> how ambiguous date formats are handled (MM/DD vs DD/MM, "best by" vs "use by"); whether to support
> bulk/rapid entry of multiple items; and how meals are entered vs. packaged items. Keep the deferred
> barcode/AI path in mind so v1 doesn't paint it into a corner. Produce ADR(s) for the capture approach and
> the OCR pipeline.

## 4. Reports & review — PARALLEL (THE PRIMARY EXPERIENCE — deepest session)

> Read `docs/planning/baseline-and-scopes.md`, then grill me on **reports & review** for Shelf Scout —
> this is the PRIMARY use case: open the app and review what's coming due as a meal-planning / grocery-run
> tool. Grill hard on the actual views: the default landing view, expiring-soon, expired, inventory-by-
> location; how items are sorted/grouped/filtered; what "coming due" means visually and how it ties to the
> Alert Threshold (coordinate with #1). Push on the meal-planning and grocery-run framings specifically —
> is there a dedicated planning surface, a shopping-list adjacency, "what can I cook from what's expiring"?
> Decide whether meal-planning splits out as its own scope. Cover the bulk-consume action and how
> consuming/expiring items flows from these views. This is where most design energy goes — be relentless.
> Produce ADR(s) for the review information architecture and the meal-planning approach.

## 5. Notifications — PARALLEL (needs #1's Item + per-user subscriptions)

> Read `docs/planning/baseline-and-scopes.md`, then grill me on **notifications** for Shelf Scout.
> Baseline: Web Push (VAPID) as a secondary nudge, accepting iOS constraints (home-screen install required,
> iOS 16.4+, explicit permission, occasional delivery flakiness); no recurring cost. Grill: VAPID key
> management; per-device Push Subscription model (a user with multiple devices); the permission-request UX
> and how to handle users who never install to home screen (do they silently get nothing?); the daily
> background check that computes what's expiring and fires pushes (where it runs, timezone handling,
> notification time); de-duplication and frequency (don't nag); threshold logic (coordinate with #1's Alert
> Threshold); and what the notification deep-links into. Keep email as a possible fast-follow in mind but
> out of v1 scope. Produce ADR(s) for the push architecture and the scheduling/threshold logic.

## 7. Reports & analytics — SPLIT OUT OF #4 (needs #4's review IA + ADR 0004's two tiers)

> Read `docs/planning/baseline-and-scopes.md` and ADR 0016 (review IA) + ADR 0004 (two-tier retention),
> then grill me on the **Reports tab** for Shelf Scout — the *past-tense* surface (what we used and wasted),
> distinct from the Shelf (now) and Plan (forward). This was split out of scope #4 once the review IA and
> the Plan/restock surface were settled. Grill: what the Reports landing shows and how the two data tiers
> surface — **Disposition detail** (< 90 days: individual Removed Items, consumed vs discarded, by
> location/category, with the Undo/drill-down window) vs the **Consumption Stat** rollup (beyond 90 days:
> `household × month × location_kind × removal_reason × category → count`); the headline a household
> actually wants (waste rate? consumed-vs-discarded trend? worst-wasted categories?); whether v1 ships any
> charts or stays list/number-only; how global vs custom categories read in the rollup; cross-household /
> suite-level insight from global categories (and whether that's even surfaced at N=1); and the empty/early
> state before 90 days of history exists. Keep the no-recurring-cost constraint and the calm,
> non-guilt-trip tone (ADR 0016) in mind. Produce ADR(s) for the Reports information architecture and any
> reporting/aggregation decisions not already fixed by ADR 0004.

## Deferred — meal-planning ("what can I cook")

> Not chartered yet (handled later). Recorded in ADR 0017: a future Plan-tab module using a **local AI
> model** to find **recipes on the web** (not a stored recipe list), prioritizing expiring-soon items as
> the ingredient list, with a save/bookmark action. **Gating open risk:** the web-discovery step must
> satisfy the no-recurring-cost constraint (paid recipe/search APIs are out by that rule; scraping is
> brittle) — no mechanism chosen. Write a charter here when this is picked up.
