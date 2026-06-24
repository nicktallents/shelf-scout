# Alert Threshold resolves by location, then household default

The Alert Threshold that drives "Expiring Soon" is resolved per Item:

```
threshold(item) = item.location.alert_threshold_days
                ?? item.household.default_alert_threshold_days
```

`Household.default_alert_threshold_days` is required (default 3). `Location.alert_threshold_days`
is a nullable override on the Location row. Per-category and per-item overrides are reserved as
future, higher-precedence links in the chain but are not built at v1.

## Context

A single per-household number is wrong across storage types. The threshold represents *how
much lead time is needed to act* — which is driven not only by spoilage rate but by
**visibility and planning horizon**: freezer items are out of sight, get forgotten, and must
be planned into meals well ahead, so they warrant a much earlier warning than fridge items.

## Considered options

**Per-household only.** Rejected: one number can't serve milk and frozen meat.

**Per-category as the override axis.** Rejected for v1: the same category behaves completely
differently by storage (frozen vs. fridged dairy), so a category threshold silently assumes a
storage context and a category-over-location rule would warn about frozen milk in 2 days. The
category × location interaction is real and not worth resolving in the v1 spine. Category
still earns its place as a reporting/rollup dimension (ADR-0004); only its use *as a threshold
axis* is deferred.

**Per-item override.** Rejected as the primary mechanism: users won't tune thresholds per
yogurt. Reserved as a top-of-chain override.

**Location override, then household default (chosen).** Location is the axis users actually
reason about ("everything in my freezer lasts long / gets forgotten"), needs no taxonomy, and
the override on the Location *row* lets a "garage freezer" differ from a "kitchen freezer".

## Consequences

Seeded locations start at Fridge 3 / Freezer 30 / Pantry 14; a custom location with a NULL
threshold inherits the household default. Prepared-food "Meals" usually live in the fridge and
inherit its threshold. Seed values come from a single config source: changing them affects
only newly created Households — existing households own their rows, so retuning live households
is a separate bulk-edit concern (owned by the reports/settings scope). Category and per-item
links can be added later without reshaping the chain, but category requires deliberately
resolving the category × location interaction first.
