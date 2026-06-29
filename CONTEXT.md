# Shelf Scout

A self-hosted PWA for tracking food expiration across a shared household inventory,
with reports and push alerts to reduce food waste. First app of a future self-hosted
suite; cost containment and a reusable spine are primary drivers.

This glossary is the fixed vocabulary every scope depends on. When naming a domain
concept anywhere (issues, code, tests, ADRs), use the term as defined here and avoid the
listed synonyms.

## Language

### Identity and people

**User**:
A person with an account in the system. Provider-agnostic: a User owns no external
credential directly. Keyed on an internal surrogate ID; carries a mutable profile cache
(`display_name`, `email`) refreshed from the IdP on login. A User exists independently of
any Household and may belong to zero, one, or many. Never hard-deleted by household
operations.
_Avoid_: Account, person, login

**Identity**:
A federated credential linking the edge IdP to a User. Unique on `(provider, subject)` →
`user_id`. There is exactly one row per User — `('authentik', <uid>)` — where `subject` is
Authentik's stable internal user UID (from the `X-authentik-uid` header). Authentik is the app's
IdP; Google (and any later Apple/Microsoft) is an upstream *source* Authentik federates, so
**upstream-provider linking lives in Authentik** and this table stays single-row-per-User. See
ADR 0012 (and ADR 0001, as amended).
_Avoid_: Credential, account, SSO record

**Household**:
A named group that owns one shared food inventory. The **isolation boundary**: every
Location, Item, custom Category, and setting belongs to exactly one Household, and all
inventory queries are scoped by `household_id`. Households are isolated from one another.
Deleting a Household cascades to all owned data (see Disposition retention).
_Avoid_: Home, family, group, team

**Membership**:
The association between a User and a Household (User ↔ Household is many-to-many), carrying
`role` and `joined_at`. The household creator is an `owner`.
_Avoid_: Access, role, group member

**Role**:
A value on a Membership: `owner` or `member`. A Household may have multiple owners. Owners
gate **member management** (invite/remove) and **household management** (rename/delete).
All inventory actions (Items, Locations, consume) are available to every member regardless
of role. Distinct from the suite/system administrator, which lives at the Authentik `family`
group level (Allowlist), not in this model. Note: *creating* a Household is gated by the
`family` group, while *inviting into* an existing Household is the `owner` role's job.
_Avoid_: admin, permission, group

### Inventory

**Location**:
A named storage area within a Household (e.g. "Fridge", "Garage Freezer"). Free-form
`name` plus a soft `kind` ∈ {fridge, freezer, pantry, other} (nullable, editable hint).
Carries an optional `alert_threshold_days` override. Households are seeded with Fridge,
Freezer, and Pantry on creation; all are editable. Deleting a non-empty Location requires
explicit confirmation and then cascade-deletes its Items; an empty Location deletes freely.
_Avoid_: Storage, zone, area, bin

**Item**:
A single physical food unit stored in one Location with its own expiration date. **One row
per physical unit** — six yogurts are six Items, even with the same date. Tracked by
**presence only**: there is no `quantity` field; quantity is always `COUNT(*)`. Carries
`name`, `expiry_date`, optional `category_id`, `created_by` (attribution only, never a
permission gate), and disposition fields. Bulk entry (fan-out into N rows) is a
capture-layer concern, not a data-model one.
_Avoid_: Food, product, entry, log entry, stock

**Meal**:
Not a distinct entity. A prepared or pre-cooked food stored as a unit (leftovers, a
meal-prepped portion) is an **Item** in a *prepared/leftovers* Category, typically with a
manually set `expiry_date` (no printed date to capture). No recipe or ingredient
composition is modeled in v1.
_Avoid_: Recipe, dish, composed item, plan

**Category**:
A coarse food classification used as the stable grouping key for reports and the durable
rollup (e.g. "yogurt", "milk", "chicken" — not brands). Two tiers: **global** categories
(`household_id` NULL, system-seeded, shared by all households, not editable by households)
and **custom** categories (`household_id` set, defined per household). `Item.category_id`
is nullable ("uncategorized") at v1; a future local AI populates it at capture time, with
the user able to correct the suggestion. Deleting a custom Category sets referencing Items'
`category_id` to NULL.
_Avoid_: Type, tag, label, kind

### Capture

**Add N Copies**:
A capture-layer action that fans out one entry into N `Item` rows sharing name, `expiry_date`,
and Location, each individually editable afterward. The data model carries no quantity (ADR
0002); this is a stepper (default 1) around the insert. The capture running-list collapses the
N rows to "name ×N" for display only. See ADR 0009.
_Avoid_: Quantity, batch add, multiple

**Shelf-Life Delta**:
The reusable, *relative* form of an expiry observation: `expiry_date − created_at` for a past
Item. Printed dates are absolute and not reusable; the delta is. The median delta per key
drives the suggested expiry. See ADR 0011.
_Avoid_: Shelf life (ambiguous), age, duration

**Shelf-Life Resolver**:
The single mechanism that proposes an expiry at capture time, applied at several grains and
falling back when a grain is too sparse: `name × location_kind → name → location_kind seed →
global default`. Each tier is `blend(seed_prior, observed_median, sample_count)` (shrinkage
weight `w = n/(n+k)`, k ≈ 3; history capped to ~12 months / ~10 entries), so it learns over
time. Per-Household. Category is a **reserved tier** the deferred AI activates later. The slot
it fills is **source-agnostic** (manual now; OCR/AI later). See ADR 0011.
_Avoid_: Suggestion engine, predictor, model, AI

**Shelf-Life Seed**:
A static, tunable config prior for a `location_kind` (e.g. Fridge ≈ 7d, Freezer ≈ 90d, Pantry ≈
180d), used only as the resolver's cold-start floor. **Distinct from Alert Threshold** — that is
the *alert window* (Fridge 3 / Freezer 30 / Pantry 14), not a shelf life. See ADR 0011.
_Avoid_: Default expiry, threshold

**Leftover Marker**:
An optional one-tap action at capture that sets an Item's Category to the seeded
*prepared/leftovers* Category — the spine's definition of a Meal. The single allowed exception
to "don't categorize during the burst." See ADR 0009.
_Avoid_: Meal flag, recipe, cooked

### Item lifecycle

**Status**:
An Item's lifecycle state, **computed** (never stored) from its disposition, `expiry_date`,
today, and its resolved Alert Threshold, by this precedence:
`Removed (Consumed | Discarded) → Expired → Expiring Soon → Fresh`.
_Avoid_: State, phase

**Fresh**:
An active Item whose `expiry_date` is beyond today + its Alert Threshold. Computed.
_Avoid_: OK, good, valid

**Expiring Soon**:
An active Item where `today ≤ expiry_date ≤ today + threshold(item)`. Computed. The primary
trigger for dashboard review and push notifications.
_Avoid_: Almost expired, near expiry, due soon

**Expired**:
An active Item whose `expiry_date < today` and which has not been Removed. Computed. Not
auto-deleted — it is a real thing still present and demanding action.
_Avoid_: Wasted, overdue, past-date

**Removed / Disposition**:
The persisted fact that an Item has left active inventory. Modeled as `removed_at`
(timestamp; NULL = active) plus `removal_reason` ∈ {`consumed`, `discarded`}. **Active
inventory** = `removed_at IS NULL`. This is the only lifecycle fact stored on an Item.
_Avoid_: Deleted, gone, archived

**Consumed**:
A Removed Item with `removal_reason = consumed` — used or eaten.
_Avoid_: Used, eaten

**Discarded**:
A Removed Item with `removal_reason = discarded` — thrown away / wasted. Distinguished from
Consumed so waste can be reported.
_Avoid_: Wasted, trashed, binned

**Bulk Consume**:
Marking multiple Items as Removed (typically `consumed`) in one operation (e.g. cooking
with several ingredients at once). Entered via **Select** mode on the Shelf; one
`removal_reason` per action (a mixed eat-some/toss-some selection is two passes); a selection
containing collapsed ×N rows prompts per-item quantity before confirming. See ADR 0016.
_Avoid_: Multi-remove, batch delete

**Quick Path**:
The single-Item removal path that does **not** require Select mode: tap a Shelf row → item
detail sheet (Consume / Discard, with the quantity stepper for a ×N row), **or** swipe — right =
Consumed, left = Discard. The common-case complement to Bulk Consume. Every removal (Quick Path
and Bulk Consume) offers **Undo** on its confirmation toast (`removed_at` → NULL, within the
90-day Disposition Retention window). See ADR 0016.
_Avoid_: Swipe action, quick delete

**Leftover Prompt**:
A lightweight sheet shown **only** after a Bulk Consume with `removal_reason = consumed`,
offering to open the Add sheet pre-seeded to the *prepared/leftovers* Category (the spine's Meal;
see Leftover Marker). Not shown on the Quick Path. The fuller "select ingredients → consume → drop
leftover in one gesture" surface is deferred to the meal-planning scope. See ADR 0016.
_Avoid_: Cooked prompt, meal sheet

### Retention and reporting

**Disposition Retention**:
Removed Items (`removed_at NOT NULL`) are kept for a retention window — **90 days** — to
power recent-history views, drill-down, and undo, then **hard-deleted** by a periodic
sweep. Before deletion each Removed Item is folded into a Consumption Stat. Expired-but-not-
Removed Items are never auto-deleted.
_Avoid_: Archive policy, TTL

**Consumption Stat**:
A durable, append/increment-only aggregate that outlives detail rows, enabling long-range
reports beyond the retention window. Grain:
`household_id × period (calendar month) × location_kind × removal_reason × category → count`.
Deliberately omits item name, exact dates, and the specific User/Location row. Global
categories enable cross-household/suite insights; custom categories aggregate within their
household. Purged when its Household is deleted.
_Avoid_: Rollup table, history, metrics

**Restock Candidate**:
A `name` surfaced on the Plan tab's **derived, read-only** restock list — computed from
Disposition history + active counts, **persisting nothing new** and **independent of expiry**.
Two tiers, both keyed on a shared recency **window** (~14d, tunable): **Out** — the last active
unit was Removed within the window and zero active units remain (*any* `removal_reason`); and
**About to run out** — `active_count ≤ units of that name Removed within the window` (a
velocity-aware depletion signal: fast movers surface early, slow movers only at the last unit,
never-drawn-down singletons never surface). Self-clearing as dispositions age out. A precise
**days-of-cover** model is a recorded future refinement, not built. See ADR 0017.
_Avoid_: Shopping list, grocery list, reorder, low stock

**Alert Threshold**:
The number of days before `expiry_date` at which an active Item becomes Expiring Soon.
Resolved per Item by precedence: `location.alert_threshold_days ?? household.default_alert_threshold_days`.
Per-category and per-item overrides are reserved future links in this chain, not built at
v1. Defaults: `Household.default_alert_threshold_days` = 3; seeded locations Fridge 3 /
Freezer 30 / Pantry 14. Seed values come from a single config source; changing them affects
only newly created Households, not existing ones.
_Avoid_: Notification window, warning period, reminder lead time

### Access and notifications

**Invite Token**:
A time-limited (**7-day default**), **single-use, bearer** credential generated by a Household
`owner`; sharing its URL is the only way for a new User to **join** an existing Household (no
open signup). Stored as a hash; carries the `role_to_grant` the owner picks (member/owner);
revocable while un-redeemed. Redeemed after the recipient signs in (via Authentik/Google) and
confirms. See ADR 0013.
_Avoid_: Invite link, invite code, referral

**Allowlist**:
The set of people trusted to **create** a Household (and reach the whole app suite), implemented
as the Authentik **`family` group** — *not* an in-app table. Joining an existing Household via an
Invite Token needs no Allowlist entry. A separate `media-users` group gates the third-party media
apps on the shared edge. See ADR 0012.
_Avoid_: Approved users, whitelist, access list

**Push Subscription**:
A device-level Web Push registration owned by a **User** (not a Membership), keyed on its
`endpoint` (unique per device+browser install; upsert on conflict). Stores `p256dh`/`auth`
(payload encryption keys) and the `household_id` active when it was created (origin context
only). A User may have many across devices. The stored `household_id` is **not** the send
gate — send-time fan-out is driven by current **Membership**. Hard-deleted on a 410/404 push
response (dead device). See ADR 0014.
_Avoid_: Device token, notification endpoint

**Notification Preference**:
A per-`(User × Household)` row governing push opt-in and cadence: `enabled` (default true —
absent row = enabled) and a 7-day `day_mask` (default all days = daily; one day = weekly
digest; a subset = day-of-week selector). Also holds the de-dup state (`last_notified_at`,
`last_fingerprint`). Frequency is **personal**; the notification *hour* is the Household's.
See ADR 0014/0015.
_Avoid_: Notification settings, subscription preference

**Notification Run**:
The daily background job (in-process Quartz `BackgroundService`) that ticks **hourly** and,
for each Household reaching its local **notification hour** (default 8am, per-Household IANA
timezone), sends each eligible member **one summary push per device** — *"N expired, M
expiring soon"* — counting both **Expired** and **Expiring Soon** against the household's
local civil "today". Consumes the spine's computed **Status** (never its own threshold).
**Suppress-on-unchanged** via a fingerprint of the actionable set, with an app-level
`reminder_interval_days` (default 3) re-surfacing anything left unaddressed; silent on
empty days; at most one push per household per day. The push deep-links into the
household-scoped actionable review view (scope #4). See ADR 0015.
_Avoid_: Cron, digest job, alert sweep

## Notes for dependent scopes

- **#2 Auth & onboarding**: **resolved** — see ADR 0012 (Authentik combined-edge auth) and ADR
  0013 (onboarding & invite flow). Identity now keys on `('authentik', <uid>)`; the
  account-linking question is answered *in Authentik* (it federates upstream providers), so the
  app stays single-Identity-per-User.
- **#3 Capture**: writes Items (one row per unit; bulk = fan-out via Add N Copies). v1 is
  **manual entry only** — OCR is **deferred** (ADR 0010), so future capture sequencing is
  barcode → OCR → AI on a shared camera surface. Owns the per-Household **Shelf-Life Resolver**
  (ADR 0011) and its source-agnostic suggested-expiry slot; the deferred local-AI `name →
  category` suggestion later activates the resolver's reserved Category tier. Captures a single
  semantics-free `expiry_date` (no use-by/best-by qualifier). **Hands to #4 Reports & review:**
  Bulk Consume (the consume-ingredients half of cooking), any combined "I cooked a meal"
  surface, and a candidate **best-by quality-grace** Status enhancement.
- **#4 Reports & review**: **review half resolved** — see ADR 0016 (review IA: unified Shelf,
  By Status / By Location, search, the consume flow with Quick Path / Bulk Consume / Leftover
  Prompt / Undo, and the discrete-band "coming due" language) and ADR 0017 (Plan surface: the
  derived Restock Candidate list + meal-planning split out as a deferred scope). Reads computed
  Status, active inventory, and Disposition history. **Accepted the best-by quality-grace handoff
  as declined** (semantics-free `expiry_date` leaves nothing for grace to attach to) and noted
  the Add date label back to #3 as **"Expiry date"** (not "Best by"). Owns a future bulk-edit of
  thresholds on existing Households. **Split out:** the past-tense **Reports tab → scope #7
  (Reports & analytics)**, building on the two report tiers (detail < 90 days; Consumption Stat
  beyond). A persistent shopping list was considered and dropped.
- **#5 Notifications**: **resolved** — see ADR 0014 (Web Push architecture: per-app VAPID,
  `endpoint`-keyed User-owned Push Subscriptions with membership-driven fan-out, the
  state-aware no-auto-prompt permission funnel, payload + household-scoped deep-link) and ADR
  0015 (in-process hourly Notification Run, per-Household local timezone/hour, per-`(User ×
  Household)` frequency mask, suppress-on-unchanged de-dup, Expired+Expiring-Soon summary).
  Notifications are a pure **consumer** of the spine's computed Status — zero independent
  threshold logic. **Hands to #4 Reports & review:** the deep-link target must be a
  household-scoped, actionable-filtered view that switches active household on open. Email
  remains a possible fast-follow, out of v1 scope.

## Platform & hosting foundation

Cross-cutting decisions from scope #6. Full rationale in ADR 0006 (stack), ADR 0007 (hosting
topology), ADR 0008 (PWA). Suite-wide unless noted.

- **Stack:** ASP.NET Core Web API + EF Core + Npgsql (backend); Vue 3 + TypeScript + Vite
  (frontend); PostgreSQL (database). Each app is **one container** serving its SPA + API on
  **one origin** (no CORS, one service-worker scope).
- **Routing:** **subdomain-per-app** (`<app>.example.com`) behind a single **wildcard TLS
  cert** (`*.example.com`). SSO cookie set on the parent domain so one login spans the suite.
- **Edge:** **Caddy** (only internet-facing component; auto-HTTPS) + **Cloudflare DNS**
  (wildcard via DNS-01). Caddy uses a custom image bundling the `caddy-dns/cloudflare` module.
- **Auth slot:** Caddy `forward_auth` → **Authentik** (embedded outpost; internal auth-decision
  service; never in the data path or internet-exposed). Authentik federates Google and gates
  per-group access; on success Caddy injects identity headers (`X-authentik-uid`,
  `X-authentik-email`, `X-authentik-username`, `X-authentik-groups`). Auth *policy* (allowlist =
  `family` group, invites, roles) is owned by scope #2 (ADR 0012/0013). Replaces oauth2-proxy
  (ADR 0007's slot, superseded) to support Google federation + suite-vs-media gating on the
  shared NAS edge.
- **Security invariants (must never be undone):** (1) **only Caddy publishes ports**; app,
  Authentik, Postgres, Redis are internal-only — a published app port lets attackers spoof
  identity headers and bypass auth. (2) The app **trusts identity headers only because they can't
  arrive any other way** — Caddy strips client-supplied `X-authentik-*` before injecting its own;
  the app fails closed on a missing header. (3) **In-app multi-tenant scoping** (every query by
  `household_id` from the User's Memberships) is the real data boundary, not the open suite edge.
- **Compose:** an **edge stack** (Caddy + Authentik + its Postgres/Redis) plus **one compose file
  per app**, joined on a shared **external** Docker network (`suite-edge`). Adding an app = one
  compose file + one Caddy block; the edge is untouched.
- **Data isolation:** one Postgres cluster, **one database + one role per app**.
- **Backups:** nightly logical `pg_dump` per database, compressed, **encrypted at rest**,
  **GFS-lite retention (7 daily + 4 weekly)**. **v1 is local-only** (no off-host hardware yet —
  an accepted, documented durability gap); the pipeline is built so adding an off-host push
  (rsync over Tailscale) later is one step. **One-time TODO: restore drill** before trusting it.
- **PWA:** `vite-plugin-pwa` service worker, **asset/shell caching only in v1** (offline data
  read = a later release; offline write sync = v2). The SW is required regardless because Web
  Push depends on it.
- **Cost:** the only recurring cost is the **domain name**. DNS (Cloudflare), TLS (Let's
  Encrypt), auth (Google + self-hosted Authentik), Web Push (self VAPID), OCR (Tesseract.js,
  client-side), and self-hosted Postgres are all free.

## Suite conventions

Patterns every suite app inherits. Shelf Scout is the **reference template** — app #2 copies
it. No shared code package is built at N=1; extract one on second use (when app #2 reveals the
real commonality). Scope #6 decisions.

- **App shell:** a common top bar with app name, **app-switcher** (a menu linking other suite
  subdomains, driven by a small static suite-app list), and signed-in user. **Sign-out** hits
  **Authentik's session-invalidation flow**, clearing the SSO session **suite-wide**, placed
  consistently in the shell.
- **Identity contract:** apps read the authenticated user from the `X-authentik-uid` (key) and
  `X-authentik-email`/`X-authentik-groups` headers injected by Authentik; auth is **stateless
  per-request** (resolve `uid → Identity → User`, lazy-provision, fail closed). **Never trust a
  client-supplied identity header.**
- **Config & secrets:** all config via environment variables; sensitive values (Google client
  secret, Authentik secret key + DB/Redis creds, app DB passwords, VAPID keys) via **Docker
  secrets**, not committed `.env`.
- **Time:** store **UTC** everywhere (`timestamptz`); convert to local only at display.
- **Logging:** structured **JSON to stdout**; log level via env var; no per-app log files.
- **Health:** every app exposes **`/health`**; every compose service defines a `healthcheck`.
- **API:** lives at **`/api`** on the app's own origin (same-origin); errors use **RFC 7807
  ProblemDetails**.
- **Security headers:** CSP, HSTS, `X-Content-Type-Options`, `Referrer-Policy` set centrally at
  **Caddy** for all apps.
- **Database:** `snake_case` object names; `timestamptz` for all times; each app owns its own
  database + role; **EF Core migrations applied on app startup**.
- **Repos & docs:** one git repo per app + a separate **edge/infra repo**; every repo carries
  one `CONTEXT.md` + `docs/adr/`.
- **Frontend skeleton:** Vite + **TypeScript strict** + ESLint/Prettier + **Pinia** + **Vue
  Router** + `vite-plugin-pwa`; theming via shared CSS-variable tokens (palette/spacing).
- **Deferred (premature at N=1):** API versioning, pagination conventions, metrics/observability
  stack, CI/CD, a real design system, i18n.

## Scope #2 (auth & onboarding) — resolved

All scope-#2 policy items are decided: see **ADR 0012** (Authentik combined-edge auth: engine,
identity contract, access tiers, sessions, security posture) and **ADR 0013** (onboarding & the
Invite Token flow: two entry paths, token lifecycle, member management). The engine choice also
revised ADR 0007's auth-proxy slot (superseded) and amended ADR 0001's Identity key. Key
outcomes: allowlist = Authentik `family` group; suite edge open-to-authenticated with in-app
membership as the real gate; 30-day sliding SSO; Google 2FA for users, WebAuthn for admin.
