# Two-tier retention: detail rows expire, a category-grained rollup is durable

Removed Items are kept as full detail for a **90-day** retention window, then hard-deleted by
a periodic sweep. Before deletion, each removed Item is folded into a durable, append/
increment-only **Consumption Stat** aggregate that never expires. Reports read detail inside
the window and the rollup beyond it.

Consumption Stat grain:
`household_id × period (calendar month) × location_kind × removal_reason × category → count`.

## Context

One-row-per-unit means removed rows accumulate forever, but the *fact that consumption/waste
happened* is analytical data worth keeping far longer than the operational row. The aggregate
is permanent and irreversible — whatever is not kept as a dimension is lost for good.

## Considered options

**Keep all detail forever.** Rejected: unbounded growth of personal-ish detail for data that
long-range reports only need in aggregate.

**Roll up using the raw free-text item name.** Rejected: "Yoghurt"/"greek yog"/"Yoplait"
become distinct grain rows forever; cardinality explodes and trends are meaningless.

**Run AI grouping at rollup time and store the result.** Rejected: it freezes one model's
judgment permanently; when the model improves, old aggregates can't be recomputed because the
detail is gone.

**Normalize early into a stable `category` dimension, roll up by category (chosen).** A
coarse, controlled taxonomy is the grouping key. The local AI's job is `name → category` at
capture time, where raw text still exists and a human can correct it; the category rides the
detail row for its 90-day life, so re-categorization is possible before it is frozen. The
rollup stores only clean categories.

Category is a two-tier taxonomy — **global** (system-seeded, shared, household-immutable) and
**custom** (per household) — so the grain is bounded by vocabulary, not spelling, and global
categories permit future cross-household/suite insight.

## Consequences

Long-range reports can answer "consumed vs. wasted, by storage type and food category, over
months". Per-brand history is intentionally out of scope. The rollup deliberately omits item
name, exact dates, and the specific User/Location row. Deleting a Household purges its
Consumption Stats too, giving clean "delete my data" semantics; suite-level analytics, if
built, operate on living households. v1 ships with `category` nullable and no AI; the
dimension is reserved so the AI slots in later with no aggregate migration.
