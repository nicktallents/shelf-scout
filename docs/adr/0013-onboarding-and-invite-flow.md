# ADR 0013 — Onboarding & the Invite Token flow

- **Status:** Accepted
- **Date:** 2026-06-25
- **Scope:** #2 Auth & onboarding
- **Deciders:** nicktallents
- **Builds on:** ADR 0012 (auth engine & access tiers), ADR 0001 (User/Identity), `CONTEXT.md`
  (Household, Membership, Role, Invite Token).

## Context

The suite is invite-gated multi-household with **no open signup**. ADR 0012 made suite-app
subdomains edge-open to any authenticated Google user, pushing the *real* access gate into the
app. This ADR fixes how a person actually gets *in*: the two entry paths, the Invite Token
lifecycle, the household-less state, and member management.

## Decision

### Two entry paths, two different gates

A newly-authenticated, household-less user reaches the app shell (edge is open) and is in one of
three situations — there is **no half-state in the data**; "household-less" is simply zero
`Membership` rows:

1. **Create a new household** — a **privilege gated by the `family` Authentik group** (read from
   `X-authentik-groups`). The creator becomes the household's first `owner`. This is the
   "approved-users allowlist": being trusted to *originate* a household.
2. **Join an existing household** — authorized by an **Invite Token**, which needs **no
   allowlisting**. The token *is* the authorization.
3. **Neither** (authenticated, not in `family`, holds no token) — a **dead-end** screen
   ("you need an invite"). This is how "no open signup" is preserved despite an open edge: a
   stranger who authenticates can do nothing.

This separation is what lets a trusted person "add people to one app without granting the whole
suite" (ADR 0012): an owner invites someone into *their* household only; the invitee gets a
`Membership` row in this one app's database and nothing else.

### Invite Token lifecycle

- **Generation:** a household **`owner`** generates a token in-app. The app creates a
  high-entropy (≥32-byte, URL-safe) value, **stores only its hash**, and records `household_id`,
  `created_by`, `role_to_grant`, `expires_at`, `redeemed_at` (NULL), `revoked_at` (NULL). The
  **plaintext is shown once** as a URL.
- **Role choice:** the owner **picks the granted role** at generation — `member` (default) or
  `owner` (e.g. a co-owning partner). Matches "a household may have multiple owners".
- **Single-use:** one token = one join, then spent. Inviting three people = three links.
- **Bearer:** possession of the URL is the authorization (not pre-bound to an email). Lowest
  friction; the owner needn't know the invitee's email, and it stays robust if a future provider
  hands out relay addresses.
- **Expiry:** **7-day** default. **Revocable:** an owner can cancel an un-redeemed token
  (`revoked_at`), and sees pending invites.
- **Sharing:** out-of-band (text, chat, email) — the app sends nothing. No email infrastructure =
  no recurring cost.
- **Redemption:** opening the URL → edge → **sign in with Google** (Authentik auto-enrolls; no
  group needed) → confirmation screen ("[Inviter] invited you to join '[Household]'. Join?") →
  on confirm the app validates the token hash (constant-time), checks not expired / not redeemed /
  not revoked, creates `Membership(user, household, role_to_grant, joined_at)`, stamps
  `redeemed_at`. Already a member → friendly no-op. A user may belong to many households, so this
  just adds one.

### Member management

- **Leave:** any member may delete their own `Membership`.
- **Remove:** an `owner` may delete another member's `Membership`.
- **Items & attribution always stay with the household** — Items belong to the Household, not the
  person; `created_by` is attribution only and the `User`/`Identity` are never hard-deleted by
  household operations.
- **Last-owner guard:** the sole owner cannot leave or be demoted while they are the last owner;
  they must first promote another member to `owner` or delete the household. No household may
  become ownerless.
- **Full offboarding** (cut a person off entirely) = remove their Authentik user/groups so they
  can no longer authenticate; their app `Membership` rows can be cleaned up separately by an owner.

### Security (see ADR 0012 for the full posture)

Invite-specific: store only the token hash; constant-time compare; rate-limit redemption;
**redact tokens from logs**; rely on the centrally-set `Referrer-Policy` so the bearer token in
the URL does not leak via `Referer`. High entropy + single-use + 7-day expiry make brute force
and leak-reuse non-issues.

## Considered options

- **Reusable / multi-use invite links.** Rejected as the default: a leaked link is more dangerous
  and revocation is coarser. Single-use keeps one link ↔ one person for clean attribution and
  revocation.
- **Email-bound tokens.** Rejected for v1: more secure but requires knowing the exact invitee
  email up front and would break against relay-style addresses from a future provider. Bearer +
  high entropy + expiry is the right trade for a friends-and-family tool.
- **Always-member invites, promote later.** Rejected: owner-picks-role supports co-owners from
  the first invite with no extra step.
- **Anyone authenticated can create a household.** Rejected: combined with the open edge that
  *is* open signup. Gating creation behind the `family` group is what preserves "no open signup".
- **A strict edge allowlist per friend (oauth2-proxy-style email file / per-user Authentik
  pre-provisioning before they can load the app).** Rejected: high per-friend admin friction for
  no real gain, since the app must enforce household membership anyway.

## Consequences

- "Add a friend" friction splits by intent: a friend who will **run their own** household needs
  one admin action (added to the `family` group); a friend who is **joining** an existing
  household needs **zero** admin-console work (just an invite link).
- No email/SMS infrastructure in v1 — invite delivery is the user's problem, by design.
- Bearer tokens accept a small residual risk (a leaked link is usable once before expiry),
  mitigated as above.
- The app must implement a tiny set of token endpoints (generate, list-pending, revoke, redeem)
  and the household-less landing logic; everything else is plain `Membership` CRUD with the
  last-owner guard.
