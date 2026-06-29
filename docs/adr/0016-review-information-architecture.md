# ADR 0016 — Review information architecture

- **Status:** Accepted
- **Date:** 2026-06-27
- **Scope:** #4 Reports & review (review half)
- **Deciders:** nicktallents
- **Builds on:** ADR 0003 (computed Status / persisted Disposition), ADR 0005 (Alert Threshold
  precedence), ADR 0002 (Item = one row per physical unit), `CONTEXT.md` (Status, Expiring Soon,
  Expired, Bulk Consume, Add N Copies).
- **Hands off to:** ADR 0017 (Plan surface — restock + deferred meal-planning), the new **Reports &
  analytics** scope (Reports tab content). Notes a prototype-label fix back to scope #3.

## Context

This is the primary experience: open the app and **review what's coming due**. The grilling
explored four shell variants (urgency feed / dashboard cards / tabbed shell / unified + bottom nav)
via an interactive prototype (`prototype/review-landing/`, Variant D). This ADR fixes the review
information architecture — the landing surface, the views, how items group/sort/filter, the visual
language for "coming due," and the consume flow — on top of the already-settled domain spine. It
does **not** decide the Plan surface (ADR 0017) or the Reports tab content (its own scope).

## Decision

### Shell — unified Shelf + bottom nav (Variant D)

- A single **Shelf** page is the landing surface — unified inventory, **not** a split
  Review/Inventory. The review job is done by *ordering*, not by a separate screen.
- **Bottom tab bar:** `Shelf | Plan | [+] | Reports | Settings`, with a **center raised "+"** as a
  first-class Add action (not a per-page FAB). Five slots keep the "+" centered/symmetric.
- The **Household** is always visible as a tappable chip in the Shelf header → opens a switcher
  bottom sheet (memberships list, active one checkmarked, "create new household" at the bottom);
  also reachable from Settings.
- The three-tense IA: **Shelf = now** (what you have) · **Plan = forward** (restock now, cook
  later) · **Reports = past** (what you used/wasted).

### Shelf grouping — By Status (default) / By Location

A toggle on the Shelf page, defaulting to **By Status**.

- **By Status:** all three sections render **expanded**, ordered `Expired → Expiring Soon → Fresh`.
  Fresh sinks to the bottom, so the sort order alone keeps "coming due" at the top — **no collapse
  needed**. Within a section, sort by proximity to today (most urgent first; Expired shows
  longest-past first).
- **By Location:** each Location is **collapsed by default**, because a location mixes statuses and
  a single expiring item otherwise hides under a pile of fresh ones. The collapsed header carries
  **urgency badges** — `Location · total [N expired] [N soon]` — so you can see at a glance which
  location needs you and expand just that one. All-fresh locations sit quietly collapsed.

### Identical units collapse to one ×N row

Items sharing the same `name + location + expiry_date` collapse to a single **"name ×N"** row
(matching the capture running-list and the spine's "collapses N rows to 'name ×N' for display
only"). Different expiry dates never merge (different urgency). Underlying units stay one-row-per-
unit per ADR 0002; the collapse is display-only. Acting on a partial quantity uses a **quantity
stepper** ("how many?"); units are fungible and removed N-at-a-time.

### Search, not filters

v1 adds a **lightweight name text-search** only — it serves the "do we already have X / what's in
the freezer called…" lookup that even a small inventory needs. **No** category/location/status
**filter facets** at v1 (premature at N=1; the spine defers pagination/filter conventions).
Category stays a **display subtitle** on rows and a grouping key for *Reports*, not a Shelf filter.

### Expired — hold, don't nag

Expired items hold at the top in red **indefinitely**; the app never auto-clears, never escalates,
never adds guilt beyond the red band. Every removal stays a human choice (Consume / Discard). There
is **no "clear all expired"** affordance. Rationale: a waste-reduction tool earns trust by being a
calm dashboard, and any auto-discard would silently corrupt the consumed-vs-discarded distinction
the Consumption Stat depends on (ADR 0004).

### Empty states

- **Nothing coming due** (no Expired, no Expiring Soon): a small affirming `Nothing coming due ✓`
  state above Fresh — not a blank screen, not a full-page illustration hiding the inventory.
- **Whole shelf empty** (new household): a genuine first-run empty state prompting the first add.

### Visual language for "coming due"

- **Three discrete bands = computed Status, one-to-one:** red **Expired**, amber **Expiring Soon**,
  **neutral-quiet** Fresh. Fresh is deliberately *not* a third saturated colour (no positive green),
  so saturation is reserved for the two states that need action. **No within-band gradient** — the
  band maps exactly to `Status`; a continuous ramp would invent a visual axis with no modeled state.
- **Day label = absolute days to/from expiry** ("tomorrow", "25d", "3d ago"). It answers "when does
  this go bad," which is what people plan around.
- **The threshold line is never drawn.** Section membership encodes it.
- **Coordination with scope #1 / Alert Threshold (the deliberate oddity):** Status is
  *threshold-relative* (per-location: Fridge 3 / Freezer 30 / Pantry 14 per ADR 0005) but the day
  label is *absolute*. So a freezer item 25 days out can sit in amber "Expiring Soon" next to a
  fridge item due tomorrow. This is **accepted as correct and rare** — surfacing that a long-dormant
  freezer item is finally approaching its window is exactly the app's job. We explicitly reject any
  relative / "in-window" relabeling (jargon that hides the calendar date).

### Consume flow

- **Quick Path (single item, no Select mode):** tap a row → item detail sheet (Consume / Discard,
  plus the quantity stepper for a ×N row); **or** swipe — right = Consumed, left = Discard. This is
  the common case and must not require entering Select mode.
- **Bulk Consume:** a **Select** toggle in the Shelf header enters checkbox mode; a floating bar
  surfaces **Consumed / Discard** for the selection. **One reason per bulk action** — a mixed
  eat-some/toss-some selection is two passes (rare, not optimized). A selection containing ×N rows
  prompts per-item quantity before confirming.
- **Leftover Prompt:** after a **Bulk Consume with reason = consumed** only, a lightweight sheet
  offers to open the Add sheet **pre-seeded to the Prepared/leftovers Category** (the spine's Meal =
  Item in that category; the Leftover Marker already supports this in scope #3). **Not** shown on the
  Quick Path (a one-off yogurt isn't "I cooked"). The richer combined "select ingredients → consume
  → drop leftover in one gesture" surface is **deferred to the meal-planning scope**, where cooking
  becomes a first-class concept.
- **Undo:** every removal — Quick Path *and* Bulk — shows an **Undo** affordance on its
  confirmation toast that restores the row (`removed_at` → NULL). Swipe is the most accidental-prone
  gesture in the app, and a mis-fired Discard corrupts the waste number we deliberately protect
  (ADR 0004); the 90-day Disposition Retention window is exactly what powers this undo.

### Reserved nav slots and a declined enhancement

- **Plan** and **Reports** slots are reserved here but owned elsewhere — Plan by ADR 0017, Reports
  by the new **Reports & analytics** scope. This ADR only guarantees their place in the shell.
- The Add **entry point** (center "+") is this scope's; the Add **sheet content** is scope #3. One
  prototype-label correction is noted back to #3: the date field must read **"Expiry date"** (the
  prototype's "Best by" drifted from the spine's single, semantics-free `expiry_date` — no
  best-by/use-by qualifier). This is a label fix, not a model change.
- The candidate **best-by quality-grace** Status enhancement handed over by scope #3 is **declined**:
  with a single semantics-free `expiry_date` and no best-by distinction, there is nothing for a grace
  period to attach to.

## Considered options

- **Landing = urgency feed / dashboard cards / tabbed shell.** Rejected in favour of the unified
  Shelf (Variant D). A counts dashboard forces a tap before anything actionable; a Review/Inventory
  split duplicates surfaces; ordering inside one Shelf does the review job with one mental model.
- **Collapse Fresh in By-Status, or hide it behind the grouping toggle.** Rejected: By-Status sort
  order already sinks Fresh; collapsing it adds a tap for no gain, and making the grouping toggle
  silently *filter* rows is confusing. Collapse is reserved for By-Location, where it's load-bearing.
- **Per-unit rows (no ×N collapse).** Rejected: honest to the model but turns a stocked fridge into
  a wall of duplicates; the spine already anticipates display-collapse.
- **Filter facets / no search at all.** Rejected both extremes: facets are over-built at N=1; zero
  search is too austere for the common lookup. Name search is the cheap middle.
- **Escalating / auto-clearing Expired.** Rejected: nags, and auto-discard corrupts the waste stat.
- **Within-band colour gradient / relative "in-window" day labels.** Rejected: invent visual axes
  and jargon that don't map to the model and hide the calendar date.
- **Build the full "I cooked a meal" combined surface in v1.** Rejected: couples consume (#4) with
  capture (#3) for marginal v1 gain; the leftover half is a normal Add, and the nudge on the bulk
  path covers the common case. Deferred to meal-planning.

## Consequences

- The app opens straight into an actionable, urgency-ordered inventory — no summary screen between
  the user and "what's coming due."
- One unified Shelf means one list component serves both review and browse; the only structural
  branch is the By-Location collapse.
- ×N collapse means consume actions must always reason in quantities (the stepper), never assume one
  row = one unit.
- The Undo affordance makes the swipe/quick paths safe enough to be the default, protecting the
  Consumption Stat's integrity.
- Two nav slots (Plan, Reports) are placeholders until their owning scopes land; the shell must
  tolerate "coming soon" content there without looking broken.
