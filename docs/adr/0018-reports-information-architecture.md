# ADR 0018 — Reports information architecture

- **Status:** Accepted
- **Date:** 2026-06-29
- **Scope:** #7 Reports & analytics
- **Deciders:** nicktallents
- **Builds on:** ADR 0016 (review IA — the three-tense Shelf/Plan/Reports shell, the calm
  non-guilt tone, the discrete-band palette and no-green rule), ADR 0004 (two-tier retention:
  Disposition detail < 90d → durable Consumption Stat rollup), ADR 0017 (Plan-surface boundary —
  Reports is past-tense, Plan is forward), `CONTEXT.md` (Consumption Stat, Disposition Retention,
  Removed/Disposition, Category, computed Status).
- **Pairs with:** ADR 0019 (reporting aggregation semantics — the computation/data-grain rules this
  IA renders).
- **Hands off to:** the future suite-analytics scope (cross-household insight); the capture scope
  (category population stays AI-deferred); a recorded richer-charts refinement.

## Context

Reports is the **past-tense** third of the three-tense IA fixed by ADR 0016 (`Shelf = now · Plan =
forward · Reports = past`). It is the one tab that is **born empty** — the Shelf has items from day
one, but Reports has nothing until items start being Removed, and no long-range view for months.

Its data comes in the two tiers ADR 0004 established: individual **Removed Items** for a 90-day
**Disposition Retention** window, then a durable **Consumption Stat** rollup at grain
`household × month × location_kind × removal_reason × category → count`. The central design tension
of this scope is how those two tiers — different *granularities*, not just different ages — coexist
in one surface without a visible seam or a "where did my data go" cliff.

The charter also demands the tab honor the calm, **non-guilt** tone (ADR 0016): a waste-reduction
tool earns trust by informing, not scoring or shaming. This ADR fixes the Reports **information
architecture** — the job, the sections, how the tiers surface, the presentation, the color/tone, and
the deferrals. The computation rules (waste-rate denominator, per-tier data grain, rollup label
durability) live in the paired **ADR 0019**.

## Decision

### Job — calm reflection, with an opportunistic diagnostic

Reports is past-tense, so nothing on it is actionable *today* — unlike the Shelf (consume what's due)
and Plan (note what to rebuy). Its job is **occasional reflection** ("how are we doing on waste"),
and *when there's something to learn*, a **diagnostic** ("what do we keep wasting, so we can buy
differently"). The diagnostic is the only past-data insight a household can actually act on, so it is
the lead insight — but delivered in the calm tone, **never** as a guilt-trip or scoreboard. We
explicitly reject a giant red "waste rate" hero; the headline is a quiet number, and the actionable
edge is *what* is wasted, not *how bad* the rate is. Low-frequency use is expected and fine.

### Headline anchored to the calendar month

The landing headline anchors to the **current calendar month** (not a rolling 30/90-day window),
because the durable rollup is itself bucketed by `period = calendar month`. So "this month" (detail)
and "past months" (rollup) are the **same unit** and stack into one trend with no seam artifact, the
hero is always exact and inside the detail tier, and the headline never straddles the retention
sweep. Partial months are labeled honestly ("this month so far").

### The four v1 sections (single scroll, top to bottom)

1. **Headline** — this month's **Waste Rate** (consumed vs discarded split; see ADR 0019), shown as
   a quiet number plus one thin CSS stacked bar.
2. **Trend** — consumed/discarded per calendar month, back through history; the reflection payoff and
   the sole reason the durable rollup exists (it is the only section needing data older than 90 days).
3. **What you waste most** — the diagnostic: a ranked list of the things most often **discarded**.
4. **Recent removals** — the detail tier's drill/inspect list of individual Removed Items (< 90d).

**Dropped: "what you use most."** It sounds affirming but at N=1 it is noise (of course you consume
the most of your staples) and it competes with the diagnostic for attention; the headline already
carries the positive framing.

### The two tiers surface as one seamless monthly series; the seam is drill-availability

Every month in the **Trend** renders **identically** (a consumed/discarded split). The 90-day
retention boundary is **never drawn as a line**; it surfaces *only* as whether a month opens:

- **Recent months (inside 90d)** are **tappable → real Removed Item rows** (the detail tier still has
  receipts).
- **Older months** are the **same visual** but **not tappable** (the rollup kept only totals).

This works because the rollup grain is a strict generalization of the per-month detail view —
`month × location_kind × reason × category → count` *is* "the per-month breakdown with names and
dates stripped" — so one component renders either tier. Drill-down is the natural, honest expression
of the seam: recent months open because we kept the rows; old months don't because we kept only the
counts.

**Drill-down in Reports is inspect-only.** It is read/inspect (and at most a path *to* the Shelf to
re-add), **not** an undo surface. **Undo stays a Shelf concern** — it lives on the consume-flow
confirmation toast (ADR 0016), not here.

### Worst-wasted reads by item name recently, by category long-range

(Formalized in ADR 0019.) Because v1 ships `category` nullable with **no AI** and capture
deliberately discourages categorizing in the burst (ADR 0009/0011), most Items are **Uncategorized**
at launch — a "worst-wasted *categories*" view would be one giant Uncategorized bar. But the detail
tier always has the item **`name`**, which is a *better* diagnostic anyway ("you keep tossing
spinach" beats "you keep tossing produce"). So **"What you waste most" ranks by item `name`** from
the detail tier (< 90d); the **category** dimension is reserved for the durable long-range Trend,
where it gets richer once the deferred AI populates categories. This is the same detail-vs-rollup
seam expressed as a grain coarsening at 90 days — invisible most of the time.

### Presentation — number-led, CSS bars only, no charting library

v1 is **number-led with plain HTML/CSS visuals and zero charting dependency**:

- **Headline** — a large quiet number (consumed %) with the discarded count beside it and one thin
  CSS stacked bar.
- **Trend** — one CSS stacked bar per month, in a short horizontal row. No line chart, no axes.
- **What you waste most** — a ranked list with small inline bars.
- **Recent removals** — a plain list (name, date, consumed/discarded chip, location label), grouped
  by month, tap-to-inspect for < 90d.

A charting library (Chart.js etc.) is **declined for v1**: the no-recurring-cost rule does not gate
it, but a trend line on 1–3 months of sparse home data is noise, a rising red line is the scoreboard
affect ADR 0016 fights, and CSS bars degrade gracefully to a single month where a one-point line
chart looks broken. Richer charts are a **recorded future refinement** (same posture ADR 0017 took
with days-of-cover). CSS bars are tier-agnostic — they render identically from detail or rollup.

### Color and tone — discarded amber, consumed neutral, no red/green in Reports

Reports stays inside ADR 0016's palette discipline. **Discarded = amber** (the established "worth
your attention" hue), **consumed = neutral-quiet**, and **no red and no green appear in Reports at
all**. Red stays exclusively the Shelf's "present + Expired, demanding action now"; green's
celebratory affect is rejected app-wide (the Shelf's no-green rule). Amber gives the waste figure
just enough weight to be the insight without becoming a guilt-trip. A 0% waste rate reads as a quiet
good, not a celebration.

### Global vs custom categories are invisible in own-household reporting

To the household, a category is a category — whether it came from the **global** seed set
(`household_id` NULL) or is a household **custom** category is an implementation detail of the durable
rollup, not something shown. The long-range category trend shows category **names** with **no
global/custom badge, no separate section, no visual tell**. (Rollup label durability on category
deletion is fixed in ADR 0019.)

### Deferred surfaces (dimensions reserved at the data layer, surfaces not built)

- **Cross-household / suite insight — none at v1.** At N=1 it is degenerate (comparing a household to
  itself), and it is a *different, suite-scoped* surface that does not exist yet — it would belong to
  a future suite-analytics context, not this household-scoped tab, and building it here would fight
  the isolation boundary. The capability is already reserved by ADR 0004 (global categories as shared
  identity; rollup keyed by `household_id`). **Deferred, but data-enabled.**
- **By-location analysis — deferred as an aggregate.** "Which storage area is a waste sink" is real
  but low-frequency at N=1. The rollup keeps `location_kind` for free, so it is a cheap future cut.
  v1 surfaces location only as a **row label** on Recent removals, not as an aggregated section.
- **Richer charts** — deferred as above.

### Empty and early states — useful from the first disposition, never gated

Four progressive states, with affirming (not celebratory) copy mirroring ADR 0016's `Nothing coming
due ✓`:

- **Zero removals ever** — a calm first-run explainer ("As you use and clear items, you'll see what
  you use and waste here"). No blank screen, **no fake demo data**, no numbers.
- **Partial first month** — headline labeled "this month so far"; Trend is a single bar (fine, not
  "insufficient data"); if nothing has been discarded, "What you waste most" shows
  **`Nothing discarded this month ✓`**, not an empty void.
- **A few months in (< 90d)** — Trend renders its 1–3 bars with no sample-size scolding; all are
  drillable.
- **Crossing 90 days** — **no special state**; months silently transition from tappable (detail) to
  non-tappable (rollup). The user never waits for or notices the rollup.

Key principle: the tab is **useful from the first disposition** (headline + recent removals), richer
over time, and **never gated** behind a "come back in 90 days" cliff.

## Considered options

- **Headline = diagnostic scoreboard / big red waste rate.** Rejected: the guilt-trip ADR 0016
  forbids. The number stays quiet; the *what* is the insight.
- **Rolling 30/90-day headline window.** Rejected in favor of the calendar month, which matches the
  rollup grain and avoids straddling the retention sweep.
- **Two explicit sections ("Recent activity" vs "History").** Rejected: puts the seam in the user's
  face and forces them to learn two surfaces. The seamless series + drill-availability is honest with
  one mental model.
- **Detail-only v1, rollup hidden until >90d exists.** Rejected: throws away the durable data's whole
  purpose and creates a day-91 cliff.
- **Worst-wasted by category only.** Rejected for v1: near-empty (all "Uncategorized") until the
  deferred AI lands; item name is available now and is the better diagnostic.
- **Real charts (Chart.js / line + pie).** Rejected for v1: noisy on sparse data, scoreboard affect,
  degrades badly at one month. Deferred.
- **Ship by-location and "what you use most" sections.** Rejected: low marginal value at N=1 on a
  rarely-opened tab; dimensions reserved, surfaces deferred.
- **Surface cross-household/suite insight now.** Rejected: degenerate at N=1; wrong scope (suite, not
  household); data-layer door already open via ADR 0004.

## Consequences

- One list/series component renders both tiers; the only structural branch is whether a month is
  drillable, driven purely by the 90-day retention window.
- The tab earns its nav slot from day one (headline + recent removals) and accrues the Trend silently
  as months pass — no lockout, no fake data, no cliff.
- "What you waste most" changes grain at 90 days (name → category); this coarsening is the same seam
  already accepted elsewhere and is invisible in normal use. Its long-range usefulness still depends
  on the deferred capture AI populating `category`.
- Red and green never appear in Reports, preserving red's single app-wide meaning (present + Expired)
  and the no-green discipline.
- Two dimensions (`location_kind`, global-category cross-household) are preserved in the rollup but
  unsurfaced at v1 — each is a cheap future cut, not a migration.
- The Reports tab depends on ADR 0019's computation rules; the two ADRs must stay consistent.
