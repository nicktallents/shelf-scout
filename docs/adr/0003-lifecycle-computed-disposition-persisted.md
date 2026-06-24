# Lifecycle Status is computed; only the removal event is persisted

An Item's Status (`Fresh / Expiring Soon / Expired / Removed`) is **computed** at read time,
not stored. The only persisted lifecycle facts are the disposition fields: `removed_at`
(timestamp, NULL = active) and `removal_reason` ∈ {`consumed`, `discarded`}.

Precedence:

```
if removed_at is not null     -> Removed (Consumed | Discarded by reason)
else if expiry_date < today    -> Expired
else if expiry_date <= today+T -> Expiring Soon   (T = resolved Alert Threshold)
else                           -> Fresh
```

## Context

`Expiring Soon` and `Expired` are pure functions of a date and the clock. "Consumed" is a
real human event that no date can derive. Conflating the two is the classic trap.

## Considered options

**Store a status/state column.** Rejected: threshold-derived states go stale at every
midnight, forcing a nightly job whose only purpose is to keep a denormalized column truthful.

**Store a single `consumed_at` (or `consumed` boolean).** Rejected: it cannot distinguish
"we ate it" from "we threw it away", which is exactly the signal a waste-reduction app wants.

**Compute the date-derived states; persist `removed_at` + `removal_reason` (chosen).**
Date-derived states are always correct with zero maintenance; one small enum captures both
removal kinds and the timestamp gives "removed recently" for free.

## Consequences

Active inventory is `removed_at IS NULL` — every live view filters on it. Expired-but-not-
removed Items are never auto-deleted; they are real things still present. "Consumed" and
"Discarded" are glossary states, not separate columns. See ADR-0004 for what happens to
removed rows over time.
