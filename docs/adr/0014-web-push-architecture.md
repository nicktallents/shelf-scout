# ADR 0014 — Web Push architecture (VAPID, subscriptions, permission UX)

- **Status:** Accepted
- **Date:** 2026-06-26
- **Scope:** #5 Notifications
- **Deciders:** nicktallents
- **Builds on:** ADR 0007 (hosting topology, Docker secrets), ADR 0008 (PWA service
  worker), ADR 0012 (identity contract), `CONTEXT.md` (Push Subscription, User, Household,
  Membership). **Paired with** ADR 0015 (scheduling, dedup & threshold consumption).

## Context

The primary experience is opening the app to **review reports of what's coming due**;
Web Push is the explicitly **secondary** nudge that pulls a user back in. The baseline
accepts iOS's constraints (home-screen install required, iOS 16.4+, explicit permission,
occasional delivery flakiness) and the core constraint of **no recurring cost** — which is
exactly why Web Push (self-hosted VAPID) beats a hosted push/email vendor. This ADR fixes
the **delivery plumbing**: key management, the per-device subscription model, and the
permission funnel. The *when/what to send* lives in ADR 0015.

iOS is the **constrained path, not the target platform.** Android Chrome/Firefox and
desktop Chrome/Edge/Firefox need only the permission grant — no install. The same
`serviceWorker` + `pushManager` + VAPID code path serves every supporting browser; iOS
merely adds an install gate and a flakiness caveat. Safari/iOS 16.4+ is the floor.

## Decision

### VAPID key management

- **One keypair per app** (Shelf Scout owns its own), **not** a suite-wide key.
  Subscriptions are origin-bound (`shelfscout.example.com`), so a shared key buys nothing
  and would couple every future app to one rotation event.
- **Generated once, out-of-band** during deploy setup (a one-time CLI/script step), stored
  as a **Docker secret** (consistent with the suite secrets convention), read by the
  backend at boot. The VAPID `sub` contact (`mailto:`) is a configurable env var.
- The **public key reaches the frontend via a tiny unauthenticated endpoint**
  `GET /api/push/vapid-public-key` — not baked into the build — so the SPA's
  `applicationServerKey` and the backend's signing key can never drift apart.
- **No rotation in v1.** Rotating a VAPID keypair **silently invalidates every existing
  subscription** (the push service rejects signatures that don't match the key the
  subscription was created with). This is documented as a "generate once, don't rotate"
  secret; the only recovery path is clients re-subscribing, which self-heals over time.

### Per-device subscription model

A browser's `pushManager.subscribe()` returns an `endpoint` URL (unique per
device+browser install — this *is* the identity) plus `p256dh` and `auth` keys used to
encrypt the payload. Persisted as:

```
push_subscription
  id              surrogate
  user_id         FK → User           (subscription belongs to the User, not a Membership)
  household_id    the active Household context when the device subscribed (origin context)
  endpoint        text, UNIQUE         ← natural key
  p256dh          text
  auth            text
  created_at
  last_seen_at / last_success_at
  failure_count
```

- **`endpoint` is the natural key; upsert on conflict.** A re-subscribe (permission
  re-grant) refreshes the keys and `user_id` rather than duplicating a row.
- **Owned by the User, not the Membership.** Deleting a Membership does **not** delete the
  device; it just stops that household's pushes to it (below).
- **Membership is the send-time source of truth, not the frozen `household_id`.** The
  stored `household_id` is the *origin* context only. The daily run (ADR 0015) fans out to
  a User's devices for **whichever households they are currently a member of**, so a
  removed member stops receiving a household's alerts even if a stale subscription row
  names it. This kills the "I left the household but still get its alerts" bug class.
- **Cleanup is failure-driven:** a push that returns **410 Gone or 404** means the
  subscription is dead (uninstalled / permission revoked) → **hard-delete the row
  immediately**. This is the primary GC; no expiry timer.

### Per-(User × Household) notification preference

Because a User can belong to several households and may not want all of them, a small
preference row governs opt-in/out per household (and, per ADR 0015, frequency):

```
notification_preference
  user_id, household_id   PK
  enabled        bool default true       ← row absent = enabled (default costs no rows)
  ...                                     (day_mask, last_notified_at, last_fingerprint → ADR 0015)
```

Default is **on for every household a User belongs to**; the user can toggle any
individual household off in-app. Send-time fan-out skips `(user, household)` pairs where
`enabled = false`.

### Permission-request UX

The full chain before a single push can land: **(1)** install to home screen (iOS only) →
**(2)** open the installed PWA → **(3)** grant the notification permission → **(4)**
`subscribe()` + persist. Stopping at any step means **no notifications**. The design is
honest about that without nagging, because the report-review experience is the real
product and must be fully valuable with zero notifications.

- **Never auto-prompt on load.** Firing `Notification.requestPermission()` unprompted earns
  reflexive denials, which are **permanent** until the user digs into browser settings.
- **A contextual, dismissible offer + a permanent Settings home** (chosen over a
  permanent shell bell, which clutters for a secondary feature). The contextual offer
  appears at the moment notifications obviously help — e.g. first view of the Expiring
  Soon report — as an inline card, never a modal.
- **State-aware control** that shows the user exactly where they're stuck (detect install
  via `display-mode: standalone`):
  - *Not installed (iOS Safari):* "Install Shelf Scout to your home screen to enable
    reminders" + the Share → Add to Home Screen hint.
  - *Installed, permission `default`:* a "Turn on reminders" button that triggers the
    native prompt **on that tap** (a user gesture maximizes grant rate).
  - *Permission `denied`:* "Reminders are blocked — here's how to re-enable in Settings."
    An honest dead-end; we cannot re-prompt.
  - *Subscribed:* "Reminders on for this device" + a way to turn off.
- **Non-installers / non-granters consciously get nothing**, by design. No email backfill
  in v1 (email is a noted fast-follow, out of scope here). The control always reflects true
  state, so the failure is **never silent to the user** even though it is silent on the
  wire.

### Payload & delivery

- Payloads are **encrypted (aes128gcm)** per the Web Push spec, signed with the VAPID key,
  via a maintained .NET Web Push library (no hand-rolled crypto).
- The payload carries `title`, `body` (the counts), `icon`/`badge`, and `data.url`
  (the household-scoped deep link) + `data.householdId`, plus a **per-household `tag`** so
  the OS collapses a new day's push over yesterday's in the tray rather than stacking.
  Payload *content* rules belong to ADR 0015; the deep-link contract is below.
- **`notificationclick`** in the service worker focuses an existing PWA window and
  navigates it (`clients.matchAll()` → `focus()`), else `openWindow()`, to `data.url`.

### Deep-link contract

Tapping a notification lands on the **household-scoped, actionable-filtered review view**
(scope #4's primary surface — expired + expiring-soon in one combined list), with **zero
extra navigation**. The deep link **must carry `household_id` and switch the app's active
household to it** on open, so a user whose active context is Household A taps a Household B
alert and still sees the right inventory. This is a routing requirement on scope #4
(household is a path param, not ambient state); the page's specifics are #4's to design.

## Consequences

- One self-hosted, zero-recurring-cost delivery path covering all supporting browsers; iOS
  is the only one needing the extra install gate.
- The `endpoint`-keyed, User-owned subscription with membership-driven send-time fan-out
  means device and household concerns stay cleanly separated; stale rows can't leak alerts.
- 410/404-driven deletion keeps the table self-pruning with no timers.
- The "generate once, never rotate" VAPID stance is a real operational constraint that must
  survive future deploy/restore work — a key restored wrong silently breaks all push.
- The honest, state-aware, no-auto-prompt funnel trades reach (non-installers get nothing)
  for trust and for not burning the one-shot permission prompt.

## Rejected alternatives

- **Suite-wide VAPID keypair.** No benefit (subscriptions are origin-bound) and couples all
  apps to one rotation; rejected for per-app keys.
- **VAPID public key baked into the frontend build.** Risks SPA/backend key drift and a
  rebuild on any change; rejected for the tiny public endpoint.
- **Auto-prompting for permission on load.** Higher short-term opt-in, but permanent denials
  and browser penalties; rejected for the contextual soft-ask.
- **A permanent notification bell in the app shell.** Clutters the shell for a secondary
  feature; rejected for contextual-offer + Settings.
- **Subscription keyed to Membership (per device × household).** Multiplies rows and
  re-subscribes on household switch; rejected for User-owned + membership-driven fan-out.
- **Hosted push/email vendor.** Violates the no-recurring-cost constraint; the whole reason
  for self-VAPID. Email remains a possible fast-follow, not a v1 substitute.
