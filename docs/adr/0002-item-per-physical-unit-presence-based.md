# An Item is one physical unit, tracked by presence (no quantity field)

Each physical food unit is its own `Item` row, with its own `expiry_date` and lifecycle.
Six yogurts are six Items. There is no `quantity` column; quantity is always `COUNT(*)`.

## Context

The heart of the app is reviewing "what's coming due", and expiration is a property of a
physical unit — units of the same product genuinely carry different dates. The data is small
regardless of model, so storage compactness is not a real constraint.

## Considered options

**One row with a `quantity` integer and a shared `expiry_date`.** Rejected: a single date
for a stack is a lie when units differ; partial consumption combined with partial expiry
("3 expired, 3 fine") cannot be expressed; and lifecycle Status for a multi-unit row is
ambiguous. It optimizes for identical-bulk staples at the expense of the core perishables
use case.

**One row per physical unit, presence-based, no quantity (chosen).** Per-unit expiry and
lifecycle are exact; "consume one" is marking one row; the coming-due and by-location reports
want row granularity anyway.

## Consequences

A pantry of 40 cans is 40 rows — accepted; the data stays tiny. The capture cost of entering
many near-identical units is pushed to the capture layer as an "add N copies" action that
fans out into N rows sharing the entered date, each individually editable afterward. The data
model carries no bulk concept.
