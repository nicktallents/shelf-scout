# ADR 0009 — Manual-first batch capture

- **Status:** Accepted
- **Date:** 2026-06-25
- **Scope:** #3 Capture
- **Deciders:** nicktallents

## Context

Capture friction is the single biggest risk to the whole app: the primary use case is
reviewing what's coming due, and that loop is empty if groceries never make it into the
system. The highest-risk moment for that failure is the **post-shopping pile** — a burst of
~15 items entered in one sitting before they're put away. The owner confirmed this batch
unpack is the dominant capture moment; incidental one-at-a-time entry and creating a leftover
after cooking are both special cases of the same burst flow.

The domain spine (ADR 0002) already fixes the data shape: an `Item` is one physical unit,
presence-based, no `quantity` field; "many of the same thing" is a capture-layer fan-out into
N rows sharing one date. `Status` is a pure function of one `expiry_date`. `Category` is
nullable and stays mostly NULL at v1 (the AI that fills it is deferred). This ADR fixes the
**UX** that writes those rows; the suggestion engine that proposes dates is ADR 0011, and the
deferral of OCR is ADR 0010.

## Decision

**Optimize the burst, treat everything else as a degenerate case of it.** A batch-optimized
rapid-entry flow handles single entries fine (you just stop after one); a single-item-polished
form makes batch entry miserable (15 modal open/close cycles). So:

- **One rapid-entry loop, no modal.** Cursor lands in **Name** (cleared each cycle, takes
  focus); **Expiry** (cleared each cycle, pre-filled tentatively — see ADR 0011); **Location**
  is **sticky** and carries to the next item (you unpack the fridge run, then the freezer run,
  then the pantry). An explicit **Add** (button + Enter) commits the item, drops it onto a
  running list, ticks a visible count, and snaps the cursor back to Name with Location
  unchanged. Explicit commit (not auto-advance) gives a predictable, undoable boundary.
- **Expiry is hard-required.** A null-expiry Item has no computed `Status` and is invisible to
  the entire reports loop — the whole point of the app. The suggestion resolver (ADR 0011)
  always provides an acceptable default, so "required" never stalls the burst. There are no
  dateless Items.
- **A single semantics-free `expiry_date`.** No `use_by`/`best_by` qualifier is captured. The
  spine's "Status = f(one date)" invariant stays intact; a qualifier only *means* anything if
  Status branches on it (a best-by grace window), which is reports-scope behavior. Households
  that want grace just enter a slightly later date, and the learned delta (ADR 0011) absorbs
  that habit. "Best-by quality-grace" is flagged as a candidate future enhancement for the
  reports/Status scope.
- **Manual date input is a hybrid control:** relative quick-chips (`+3d`, `+1w`, `+2w`, `+1mo`)
  that nudge the current value, plus a **native date input** for an exact printed date.
  Because input is via a native date control, the classic MM/DD-vs-DD/MM ambiguity does not
  arise — you pick a real calendar day, not an ambiguous string.
- **"Add N copies" = a quantity stepper (default 1)** that fans out into N `Item` rows sharing
  name/date/location, each individually editable afterward — exactly as ADR 0002 anticipated.
  No new data-model concept. The running list **collapses** identical rows to "name ×N"
  (display-only grouping); undo of a ×N add removes all N. Differing dates within a multiple
  are handled by two separate adds or a post-hoc edit; there is deliberately **no per-unit date
  grid** at capture.
- **Meals are just the ordinary add flow.** Per the spine, a Meal is not a distinct entity — a
  leftover is an `Item` whose date is a manual estimate. Leftovers are inherently dateless,
  which is exactly where the resolver shines (it learns "chicken curry × Fridge ≈ 4d" vs.
  "× Freezer ≈ 90d"). A single optional **one-tap "Leftover / prepared" marker** sets the
  seeded prepared/leftovers Category — the *one* allowed exception to "don't categorize during
  the burst," because it is the spine's literal definition of a meal and the reports loop will
  key on it. The **consume-the-ingredients half** of cooking is **Bulk Consume**, owned by the
  reports/review scope, not capture. Any combined "I cooked a meal" surface (consume N + drop a
  leftover in one gesture) is a flagged cross-scope handoff; v1 ships the two as independent
  actions.
- **Capture is online-only at v1.** The PWA caches only the app shell (offline data read is a
  later release; offline write sync is v2). Capture needs the network to fetch suggestions and
  POST items. An accepted, documented v1 limitation.

## Consequences

- The fast path for a re-bought item is: type the name (autocomplete assists) → accept the
  pre-filled suggestion → Add. Near-zero taps at the exact failure point.
- The flow never leaves the keyboard context and never shows a modal; a stock-up of 40 cans is
  one name + a stepper, not 40 interactions.
- No dateless Items ever enter the system, so every Item participates in the reports loop.
- The capture layer owns no "meal," "quantity," or "date qualifier" concept — it stays a thin
  writer of `Item` rows, keeping the spine's invariants load-bearing.

## Rejected alternatives

- **Single-item-polished modal form.** Optimizes the rare incidental entry at the expense of
  the common, failure-prone burst.
- **Auto-advance on field completion.** Saves one tap but makes "is this row done?" ambiguous
  and muddies the undo boundary.
- **A `use_by`/`best_by` qualifier field.** Dead metadata unless Status branches on it;
  recoverable for free via the entered date + learned delta.
- **A per-unit date grid for multiples.** Heavy and rare; the "edit afterward" path already
  covers it.
- **A distinct meal/recipe capture pathway.** Contradicts the spine (Meal is not an entity)
  and duplicates the add flow.
