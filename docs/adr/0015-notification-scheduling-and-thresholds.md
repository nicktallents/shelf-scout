# ADR 0015 — Notification scheduling, de-duplication & threshold consumption

- **Status:** Accepted
- **Date:** 2026-06-26
- **Scope:** #5 Notifications
- **Deciders:** nicktallents
- **Builds on:** ADR 0007 (single-container, single-replica, in-process startup work),
  ADR 0014 (subscriptions, preferences, payload/deep-link), `CONTEXT.md` (Alert Threshold,
  Status, Expiring Soon, Expired, Household). **Paired with** ADR 0014 (delivery plumbing).

## Context

ADR 0014 fixes *how* a push is delivered. This ADR fixes **when it fires, to whom, and with
what content** — the part that determines whether the nudge is trusted or muted. The default
trap is "every morning, push every Expiring Soon item": it nags (the same item alerts for
days; a busy household gets a wall of pushes) and trains users to disable notifications. The
design is therefore built around **silence by default and one civilized summary per day**.

Two facts from the spine constrain it. **(1)** `Alert Threshold` and `Expiring Soon` are
already defined by scope #1 — notifications must **consume** that computed Status, never
invent their own window. **(2)** Times are stored UTC, but "what's expiring" and "fire at a
civilized hour" are **local-date** questions; `expiry_date` is a civil date, not an instant.

## Decision

### Where it runs

An **in-process `BackgroundService`** scheduled with **Quartz.NET** (cron triggers), inside
the single app container. No separate cron container, no new compose service — consistent
with EF migrations already running in-process at startup (ADR 0007), single-replica, and the
no-cost constraint. The job is **idempotent** (see de-duplication), so a missed or
double fire is survivable without distributed locking.

### Timezone & scheduling

- **Timezone lives on the Household** (an IANA zone, e.g. `Pacific/Auckland`), set at
  creation (defaulted, editable). A household is one physical place; per-User timezone is a
  reserved future refinement.
- **Per-household notification hour**, default **8am local**, configurable per household
  later (not a one-way door — if the meal-planning/grocery-run framing pulls it to evening,
  that's a config change).
- The scheduler **ticks hourly** and processes the households whose local time has just
  reached their notification hour. One cheap hourly tick covers every timezone (8am Auckland
  processes Auckland households; 8am LA processes LA households) — **no per-user cron jobs**.
- **Expiring Soon / Expired are computed against the household's local civil "today"** at
  fire time, so an item "expiring June 28" alerts on the household's calendar, not UTC's.

### Per-(User × Household) frequency — a 7-day mask

Frequency is a **personal** preference, so it rides the per-(User × Household)
`notification_preference` row from ADR 0014:

```
notification_preference
  user_id, household_id   PK
  enabled          bool   default true
  day_mask         int    default 0b1111111   ← which weekdays this user may be notified
  last_notified_at timestamptz
  last_fingerprint text                        ← de-dup hash (see below)
```

A **7-day mask** subsumes every requested cadence: **daily** = all 7 days; **weekly digest**
= a single day; **day-of-week selector** = any subset (e.g. Mon/Thu). The household
notification hour governs the *time*; the mask governs the *days*. Default = all 7.

### De-duplication & the "don't nag" rules

1. **One summary per device per day**, never one-per-item. The run produces a single push —
   *"1 expired, 2 expiring soon"* — and detail lives behind the deep link. This fan-in is the
   biggest anti-nag lever.
2. **Suppress-on-unchanged, with a re-nudge interval.** Each run computes a **fingerprint**
   of the actionable set (a hash over the `(item_id × status)` set). An unchanged set is
   suppressed — but only until it has gone `reminder_interval_days` without a push, at which
   point it **re-surfaces** so an ignored item never rots in silence. The decision reuses
   state we already store (`last_fingerprint` = did the set change?; `last_notified_at` = how
   long since we sent?):

   ```
   send = due_today && (changed || (today − last_notified_at) ≥ reminder_interval_days)
   ```

   On send, store the new fingerprint + `last_notified_at`.
3. **`reminder_interval_days` is a single integer that subsumes the whole policy** (chosen
   over a `suppress_unchanged` boolean, which forced a bad choice between daily-nag and
   failing-the-procrastinator). `1` = notify every eligible day; `∞`/null = pure suppress;
   **default `3`** = quiet day-to-day but re-nudge anything left unaddressed every 3 days. It
   is an **app-level config default** (a Shelf Scout configuration value — *not* suite-wide;
   notifications are this app's concern alone), stored so it can later become a per-`(User ×
   Household)` override. The fingerprint is computed and stored regardless, so changing the
   interval is a config change, never a rework. **Reserved refinement:** status-targeted
   escalation (re-nudge only the *Expired* subset while leaving merely-Expiring-Soon quiet) —
   more surgical, deferred to avoid v1 branching. *(Noted nuance: a "weekly, every Monday"
   user's set virtually always shifts week to week, so suppression rarely bites them anyway.)*
4. **Hard frequency floor: at most one push per household per day.** No item-level event
   (e.g. a capture burst) can trigger an extra same-day push.
5. **Idempotency = the same fingerprint.** A double-fire of the job recomputes the same
   fingerprint and suppresses the duplicate — what makes the in-process scheduler safe
   without locks.
6. **Empty days are silent.** Nothing expiring → **no push.** We never send "all clear";
   silence *is* the no-alert signal.

### Threshold consumption & content

- **Notifications own zero threshold logic.** They read the spine's computed `Status`
  (resolved per item by `location.alert_threshold_days ?? household.default_alert_threshold_days`)
  — the same Status the reports use. One source of truth.
- The summary includes **both Expiring Soon and Expired**, counted separately
  (*"1 expired, 2 expiring soon"*). **Fresh** and **Removed** never trigger. **Expired** is
  included because an expired-but-still-present item is the highest-value thing to surface and
  is often a **grocery-list/replacement signal** (the milk's gone — replace it), tying
  straight into the meal-planning framing.
- Threshold determines **content, not eligibility**: the schedule + day-mask decide *whether*
  the run fires; the threshold decides *what's in the set*; an empty set → silence.
- **No event-driven immediacy in v1.** An item that becomes Expiring Soon at 2pm waits for
  the next daily run; capture is bursty and the whole design is a once-daily nudge.
- **Threshold edits self-correct.** Editing a Location's `alert_threshold_days` just changes
  what the next run computes — there is no stored threshold to migrate, only the fingerprint
  of the resulting set.

## Consequences

- A single hourly tick + per-household local hour handles all timezones with no per-user jobs.
- Suppress-on-unchanged + one-summary-per-day + silent-empty-days make the nudge low-noise and
  trustworthy; the fingerprint doubles as crash-safe idempotency.
- The dedup grain is **per-(User × Household)** because a daily user and a weekly user in one
  household have different "what I last told you" baselines — slightly more state than a
  per-household baseline, but required by per-user frequency.
- Notifications stay a pure **consumer** of the spine's Status, so threshold rules live in
  exactly one place and never drift between reports and pushes.
- `reminder_interval_days` gives anti-nag *and* a procrastinator safety net from one integer,
  and keeps both poles (always-notify / pure-suppress) one config change away.

## Rejected alternatives

- **Notify every eligible day regardless of change.** Simpler (less state), but naggier;
  rejected as the default, preserved as the `reminder_interval_days = 1` setting.
- **A `suppress_unchanged` boolean.** Forces an all-or-nothing choice between daily-nag and
  failing the procrastinator; rejected for the `reminder_interval_days` integer, which
  subsumes both poles and adds a sane re-nudge middle.
- **One push per expiring item.** A wall of notifications; rejected for the single summary.
- **A global daily cron at a fixed UTC hour.** Fires at uncivil local hours and mis-dates
  "today" across zones; rejected for the hourly tick + per-household local hour.
- **External cron container / separate scheduler service.** More moving parts and a new
  compose service for a single-replica app; rejected for the in-process `BackgroundService`.
- **Per-User timezone in v1.** A household is one place; rejected as premature, reserved.
- **Event-driven / real-time push as items cross the threshold.** Fights the bursty capture
  flow and the one-per-day cap; rejected for the daily batch.
- **Notifications computing their own alert window.** Would duplicate (and risk diverging
  from) the spine's Alert Threshold; rejected — consume the computed Status only.
