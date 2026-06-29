# ADR 0017 — Plan surface: v1 Restock and deferred meal-planning

- **Status:** Accepted
- **Date:** 2026-06-27
- **Scope:** #4 Reports & review (Plan surface)
- **Deciders:** nicktallents
- **Builds on:** ADR 0016 (review IA / the Plan nav slot), ADR 0004 (Disposition detail < 90d /
  Consumption Stat), ADR 0011 (the deferred local-AI / reserved Category tier), `CONTEXT.md`
  (Removed/Disposition, Item = one row per physical unit).

## Context

The charter frames Shelf Scout as a "meal-planning / **grocery-run** tool" and asks whether
meal-planning splits into its own scope, whether there's a dedicated planning surface, a
shopping-list adjacency, and a "what can I cook from what's expiring" view. This ADR resolves the
**Plan tab** — the forward-looking surface — deciding what ships in v1 and what defers.

The key reframings from grilling:
- **Meal-planning is the app's *purpose*, not a module** — "what can I cook" with no recipe model
  (the spine has no Meal entity / ingredient composition) collapses to "look at what's expiring,"
  which the Shelf already shows. But the user *does* want a genuine future "what can I cook" feature
  backed by a **local AI model** finding **recipes on the web**, with a **save/bookmark** action.
  That is a real, separable module — so meal-planning splits out, **deferred**.
- **A shopping list is explicitly not wanted** (the user uses other apps for that). But a
  **derived, read-only restock list** is wanted for v1 — and unlike a shopping list it needs **no
  new data model**, only the Disposition history already kept.

## Decision

### The Plan tab stays, and gets real v1 content

The Plan tab is retained (it also keeps the bottom nav at five slots so the center "+" stays
symmetric — ADR 0016). In v1 it renders:

1. A working **Restock** surface (below), derived entirely from existing data.
2. A **"coming soon" teaser** for the future meal-planning feature, grounded in the user's *real*
   Expiring Soon set ("when this lands, we'll find recipes for your spinach, sour cream, +3 more").
   The teaser must read unmistakably as a preview — **no fake recipe results, no dead buttons**.

So the Plan tab is not vaporware at v1; the teaser is the only not-yet-functional part.

### Restock — a derived, read-only depletion view (v1)

Computed from the Disposition history; **nothing new is persisted**. All tiers are
**expiry-independent** (restock is about running out, not about freshness):

- **Out — restock now:** a `name` whose **last active unit was Removed within the recency window**
  (start ~14 days, tunable), with **zero active units remaining** — *regardless of removal reason*.
  Consumed-to-zero and discarded-to-zero both count (you're out of carrots whether you ate them or
  tossed the rest of the bag).
- **About to run out:** a `name` where **`active_count ≤ units removed of that name within the
  window`** — i.e. you've recently used at least as many as you have left. This is **velocity-aware
  without rate estimation**: fast movers (yogurt: 2 left, 5 removed) surface early; slow movers
  (tomato sauce: 1 left, 0 removed) only at the last unit; perennial singletons (one ketchup, never
  drawn down) never surface. Same data and window as the Out tier, one comparison.

The list is **read-only and self-clearing** — no checkboxes, no "add to cart" (no shopping list by
design); entries age out naturally as dispositions pass the recency window, so it never nags about
something you ran out of months ago.

**Future refinement (recorded, not built):** an explicit **days-of-cover** model (estimate
consumption rate → project the zero date → surface within a lead window) is more precise but
*jumpy* against sparse home-inventory data; revisit once there's enough history to estimate rate
reliably.

### Meal-planning splits into its own deferred scope

- **"What can I cook"** is a genuine future module: a **local AI model** that finds **recipes on the
  web** (not a stored recipe list), prioritizing **expiring-soon** items as the ingredient list,
  with a **save/bookmark** action (term unlocked — "save"/"bookmark"/other, decided in that scope).
  Its reserved home is the Plan tab.
- **Gating open risk (punted):** "identify recipes on the web" collides with the core no-recurring-
  cost constraint. Local *inference* is free and consistent with the already-deferred capture AI
  (ADR 0011's reserved Category tier — possibly one local model serving both jobs). But the web step
  is the load-bearing unknown: a paid recipe/search API is **out by the cost rule**, and scraping is
  brittle/ToS-gray. **No mechanism is chosen** — this is the explicit risk that gates the future
  meal-planning scope.
- **Reserved future vocabulary:** a saved-recipe entity (URL + title + maybe a cached snapshot;
  household- or user-scoped) owned by that scope. Named loosely; nothing depends on it at v1.

### Shopping list — considered and dropped

A persistent shopping list (aspirational items you don't possess — a *new* data model distinct from
`Item`) was considered for the grocery-run framing and **dropped**: the user has other apps for it,
and the derived Restock view satisfies the "what are we out of" need with zero new data. The
**rebuy-into-a-list nudge** dies with it.

## Considered options

- **Drop the Plan tab entirely (four-slot nav).** Rejected: makes the center "+" asymmetric, and the
  future "what can I cook" feature needs a reserved home. The derived Restock list also gives the tab
  genuine v1 value.
- **Build a "what can I cook" / recipe feature in v1.** Rejected: no recipe model in the spine, and
  the web-discovery cost question is unresolved. Deferred to its own scope.
- **Ship a persistent shopping list in v1.** Rejected: new entity + CRUD + rebuy plumbing the user
  doesn't want, against a feature served elsewhere.
- **Restock tier = pure last-unit, regardless of velocity.** Rejected: floods the list with every
  singleton in the house and fires too late for fast-moving items.
- **Restock tier = full days-of-cover model in v1.** Rejected for v1: jumpy against sparse data;
  recorded as a future refinement.
- **Gate the restock "about to run out" tier on expiry.** Rejected: the user wants depletion
  surfaced independent of freshness (you can run low on a long-dated staple).

## Consequences

- The Plan tab ships useful at v1 (Restock) without any new persisted data — it's a query over
  Disposition history plus active counts.
- The recency window is a single shared tunable across both restock tiers; its value (start ~14d) is
  a config knob, not a modeled constant.
- Meal-planning is a known, named, deferred scope with one explicit gating risk (web-discovery
  cost); whoever picks it up starts from that risk, not a blank page. No charter is written yet — it
  will be handled later.
- "Grocery-run" as a charter framing is satisfied by the derived Restock view; no shopping-list data
  model is introduced now or planned.
