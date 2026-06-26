# ADR 0011 — Layered shelf-life suggestion resolver

- **Status:** Accepted
- **Date:** 2026-06-25
- **Scope:** #3 Capture
- **Deciders:** nicktallents

## Context

With OCR deferred (ADR 0010), manual entry is the whole v1 capture path, so the **expiry
field** must be near-zero-friction. Expiry is also the one field fed by multiple sources
(manual entry today; OCR and local-AI later), so it needs a single, source-agnostic
suggestion slot. This ADR defines what fills that slot at v1: a learned shelf-life estimate
built from the household's own history, at **zero cost and with no ML** — a thin aggregate
query, not a model. It is deliberately the cheap precursor to the deferred local-AI date
suggestion, which later becomes just a *better source* feeding the same slot.

### The correctness trap

A printed expiry date is **absolute**, but a suggestion must be **relative**. Last month's
"best by Jul 15" is useless next month. What's reusable is the **shelf-life delta**:
`expiry_date − created_at`. So the system learns, per key, the median delta and suggests
`today + delta`.

### Where it earns its keep

- **Packaged goods** carry a printed date; the learned delta is noisy (depends how fresh the
  store's stock was) and largely redundant with reading the label. Weak.
- **Dateless items** (produce, a cut block of cheese, leftovers, meal-prep) have **no printed
  date** — today you'd guess. This is exactly where a learned delta shines, and the one place
  no scan could ever help.

### The v1 data reality

`Item.category_id` is nullable and **mostly NULL at v1** (the AI that fills it is deferred,
and rapid entry discourages hand-categorizing). So a category-grained estimate has almost
nothing to group by. The signals actually available at capture time are **Name** (always
typed) and **Location** (always known — it's sticky and set *first* in the unpack run, so it's
in hand before the name is even typed).

## Decision

A **single shelf-life resolver applied at several grains** — not three separate mechanisms.
Every tier is `blend(seed_prior, observed_median, sample_count)`, so "learns over time" is
intrinsic: with few samples the seed dominates; with many, the household's observed median
does.

**v1 resolution chain (most specific first, falling back when a tier is too sparse):**

```
name × location_kind   (once it has ≥ k samples)
        ↓
name                   (any location)
        ↓
location_kind seed     (static shelf-life prior)
        ↓
global default
```

- **Category is a reserved tier, not a v1 tier.** It is left as a labeled socket that the
  deferred local-AI categorizer turns on when it lands — forward-compatible, but not
  load-bearing now because there is no category data to group by. We do **not** force
  categorization into the burst to populate it.
- **Blend = shrinkage.** Weight `w = n / (n + k)` with **k ≈ 3**; suggestion =
  `w · observed_median + (1 − w) · seed`. History is capped to roughly the **last 12 months /
  last ~10 entries** of a key so stale habits decay.
- **Scope is per-Household.** Learned deltas and name autocomplete never cross the isolation
  boundary; shelf-life habits are household-specific. (Global *Category* seeds still exist;
  only the *learned* layer is per-household.)
- **Shelf-life seeds are a distinct, tunable config prior** — separate from Alert Thresholds.
  The spine's Fridge 3 / Freezer 30 / Pantry 14 are *alert windows*, not shelf lives. Initial
  shelf-life seeds (e.g. Fridge ≈ 7d, Freezer ≈ 90d, Pantry ≈ 180d) come from a single config
  source and are placeholders to tune, used only as the cold-start floor the resolver shrinks
  away from.
- **Computed server-side.** Names and deltas live in Postgres; autocomplete and the
  median-delta resolver are small cached aggregate queries behind `/api`, consistent with the
  single-origin stack. Online-only at v1.

### How it presents in the UI

- The expiry control opens **pre-filled with the resolved suggestion, shown tentatively**
  (ghosted/italic). Accepting it costs **zero taps** — you just hit Add. The wrong-guess risk
  is mitigated because the value is visibly tentative until commit, and every date is editable
  afterward. (Chosen over offering the suggestion as a one-tap chip: the batch-flow thesis is
  to minimize taps at the failure point.)
- **Name autocomplete** suggests existing household names as you type, to drive **convergence**
  ("Greek yogurt" doesn't fragment into four spellings). This is valuable for *every* item and
  is the substrate the future AI keys on for category inference. Matching is
  case/whitespace-insensitive against the household's distinct past names.
- Every confirmed entry feeds back into the resolver, so the estimate self-improves.

## Consequences

- Cold start is solved (seed priors work from day one) while the estimate personalizes over
  time — one mechanism, no separate "static vs learned" modes.
- The resolver guarantees the required `expiry_date` always has an acceptable default, so the
  "expiry is mandatory" rule (ADR 0009) never stalls the burst.
- The suggestion slot is **source-agnostic**: OCR and local-AI later plug in as additional
  sources without reworking the expiry UX; the AI also activates the reserved category tier.
- The learned layer is intentionally a thin query, not ML — no training, no recurring cost,
  trivially tunable (k, window, seeds).

## Rejected alternatives

- **Per-name learned deltas only.** Suffers cold start; useless until a key is re-bought
  several times.
- **Per-category default shelf life only.** Simple and zero-cold-start, but category is mostly
  NULL at v1, so it has nothing to group by — and it ignores household-specific behavior.
- **A category tier as first-class now.** Would require forcing categorization into the rapid
  flow, cutting against the "fastest possible" goal, to populate a tier with no data.
- **Suggestion offered as a chip instead of pre-filled.** Safer against a lazy wrong-accept,
  but adds a tap to the most common path and to dateless entry — the path we most need to be
  frictionless.
