# ADR 0019 — Reporting aggregation semantics

- **Status:** Accepted
- **Date:** 2026-06-29
- **Scope:** #7 Reports & analytics
- **Deciders:** nicktallents
- **Builds on:** ADR 0004 (two-tier retention + the Consumption Stat grain), ADR 0002 (Item = one row
  per physical unit; presence-only, quantity = `COUNT(*)`), ADR 0003 (computed Status / persisted
  Disposition), `CONTEXT.md` (Removed/Disposition, Consumption Stat, Category, Disposition Retention).
- **Pairs with:** ADR 0018 (Reports information architecture — the surface that renders these
  numbers).

## Context

ADR 0004 fixed *what is retained* (90-day detail → durable Consumption Stat rollup) and the rollup
**grain** (`household × month × location_kind × removal_reason × category → count`). It did **not**
fix the *computation* questions the Reports tab forces: how "waste rate" is defined, what grain each
section reads at, and how a rollup row stays renderable after its source category is deleted. This
ADR records those reporting/aggregation decisions, which ADR 0018's IA depends on.

## Decision

### Waste Rate — count-based, over Removed Items only

> **Waste Rate** (period) = `discarded / (consumed + discarded)`, counting **one per physical unit**
> (ADR 0002), over Items **Removed** within the period.

- **Count-based**, consistent with the presence-only model (six tossed yogurts = 6) and with the
  rollup, whose measure is `count`. So the headline (detail) and the trend (rollup) compute by the
  **same formula** over the same unit.
- **Removed-only denominator — still-present Expired items are excluded.** Per ADR 0016 an Expired
  item holds in red on the Shelf **indefinitely** and is never auto-cleared; it is not Removed until
  a human picks Consume or Discard, so it is in **neither** bucket and the rollup (which only carries
  `removal_reason ∈ {consumed, discarded}`) never sees it. Forgotten expired food is a **Shelf**
  problem, not a Reports number — it does not inflate Waste Rate until actually discarded. Accepted
  as deliberately conservative ("you haven't wasted it until you toss it — you might still eat it")
  and as the only definition that keeps detail and rollup identical and avoids double-counting when
  the item is later discarded.

The "consumed %" shown in the headline is simply `1 − WasteRate` over the same denominator.

### Per-tier grain: name recently, category long-range

The "what you waste most" diagnostic reads at **different grains in the two tiers**, by design:

- **Recent (detail tier, < 90d): rank by item `name`.** The name is on every detail row, needs no AI,
  and is the more actionable diagnostic. This is permitted because ADR 0004 only rejected `name` in
  the **rollup** (unbounded cardinality over years) — the detail tier already holds it for 90 days.
- **Long-range (rollup): group by `category`.** The coarse, controlled taxonomy is exactly what
  bounds rollup cardinality (ADR 0004) and is what survives past 90 days.

This grain change at the retention boundary is the data-layer counterpart of ADR 0018's seamless
series. At v1, with `category` nullable and no AI, the long-range category view is mostly
`Uncategorized`; it enriches as the deferred capture AI populates `category`.

### Rollup stores a durable category **label**, not just a foreign key

So the long-range trend (ADR 0018) renders even after a household deletes a custom category — and so
the rollup is never rewritten — the Consumption Stat persists a **denormalized, durable category
label** at fold-up time, **not** a live FK to the Category row:

- Deleting a custom Category sets referencing **detail** Items' `category_id → NULL` (per
  `CONTEXT.md`), but **already-written rollup rows keep their category identity** by their retained
  label. The rollup is append/increment-only and historical — **we do not rewrite history**.
- A since-deleted (or renamed) category therefore renders in the long-range trend **by the label it
  had when the rows were folded**. Items that were genuinely uncategorized fold under an explicit
  `Uncategorized` key.
- Global categories (system-seeded, shared) fold under a stable shared identity, preserving ADR
  0004's reserved cross-household path; custom categories fold under their household-local label.

This is the one rollup-shape decision ADR 0004 left open: the `category` slot in the grain is
**materialized as a stable label string** (plus enough to distinguish global vs custom for the
reserved suite path), not a dangling reference.

### Folding is reason-preserving and lossless within the grain

The 90-day sweep folds each expiring Removed Item into the matching Consumption Stat cell by
incrementing `count` at its `(household, month-of-removed_at, location_kind, removal_reason,
category-label)` key. `removal_reason` is preserved (the consumed/discarded distinction is the whole
point — ADR 0004), so Waste Rate is computable from the rollup at month grain identically to the
detail tier. Everything **not** in the grain (item name, exact date, specific Location/User row) is
intentionally lost at fold-up, as ADR 0004 specified.

## Considered options

- **Penalize still-present Expired items in Waste Rate ("at risk").** Rejected: mixes present-tense
  into a past-tense tab, double-counts on eventual discard, and drifts toward the nagging ADR 0016
  forbids.
- **Volume/weight-based rate instead of count.** Rejected: the model is presence-only with no
  quantity or weight (ADR 0002); count is the only available and consistent measure.
- **Rank long-range waste by name too (richer detail).** Rejected: reintroduces exactly the rollup
  cardinality explosion ADR 0004 rejected; name lives in the detail tier only.
- **Rollup keeps a live FK to Category.** Rejected: a deleted custom category would dangle and the
  historical trend would lose or mislabel rows; the rollup must be self-contained and immutable.
- **Recompute/rewrite old rollups when categories change or the AI improves.** Rejected: the detail
  is already gone past 90 days, so there is nothing to recompute from (ADR 0004's core consequence);
  the rollup is deliberately append-only.

## Consequences

- Waste Rate has one definition across both tiers, so the headline and the trend can never disagree.
- Forgotten expired food is surfaced only on the Shelf, never silently folded into the Reports waste
  number — keeping Reports purely past-tense and the stat honest.
- The diagnostic is actionable at v1 (by name) without any AI; the durable category trend is the
  part that waits on the deferred AI to become rich.
- The rollup is fully self-contained and immutable: category deletion, rename, or AI improvements
  never corrupt or require rewriting history; "delete my household" still cleanly purges its stats
  (ADR 0004).
- The reserved suite/cross-household path (ADR 0004, ADR 0018) stays open because global-category
  identity is preserved in the durable label, distinguishable from household-local custom labels.
