# ADR 0020 — No in-app app-switcher; suite cohesion via an extractable shell + tokens

- **Status:** Accepted
- **Date:** 2026-07-02
- **Scope:** #6 Platform & hosting foundation
- **Deciders:** nicktallents
- **Amends:** the App shell / Suite conventions in `CONTEXT.md`; supersedes the app-switcher in issue #2 (stories 27–28) and issue #10.

## Context

The original suite conventions (and issues #2/#10) put an **app-switcher** in every app's top
bar — a menu linking the other suite subdomains — as the visible signal that the apps are one
product. At N=1 it lists only Shelf Scout, so in v1 it is a single-entry menu that navigates to
the page you are already on. The suite is self-hosted for one household; the honest way anyone
moves between two apps is a bookmarked subdomain, not a mid-task in-app switch. We also asked
whether **sign-out** — a rare, deliberate action on a personal phone with a 30-day sliding SSO
session (ADR 0012) — earns a permanent slot on the persistent chrome.

## Decision

- **No app-switcher.** It comes out of the reference template entirely — no menu, no reserved
  slot. App #2 does not inherit it.
- **Cross-app navigation is deferred to a future launcher** (a separate root-domain app, its own
  repo). Individual suite apps do **not** link to it; there is no "back to suite home" affordance.
- **Sign-out moves off the top bar into an account/settings screen**, reached by tapping an
  **account affordance** (initials/email) on the top-bar right. Sign-out still hits Authentik's
  session-invalidation flow (suite-wide). This account screen is also the human-visible proof of
  the `whoami` identity chain.
- **Suite cohesion is carried by shared CSS-variable theming tokens + a copied shell skeleton**
  (header layout + account/settings pattern), not by any in-app cross-navigation.
- **The tokens and shell are built to be extraction-ready from day one**: tokens live in one
  self-contained theme layer (no per-component hardcoded palette/spacing); the header/account
  shell is factored via slots/props, not coupled to Shelf-Scout stores or domain types. Extraction
  into a shared library on second use (per the existing convention) is then a file move, not a
  rewrite.

## Rationale

- A single-entry switcher is dead furniture in v1 and, baked into the template, becomes dead
  furniture in app #2 too.
- If cross-app discovery ever becomes a real itch at N≥2, a launcher page is a cleaner answer than
  a menu embedded in every app — and it is a pure addition with nothing to retrofit.
- Sign-out is rare and deliberate; one layer down (account screen) is where every mainstream app
  puts it and satisfies story 14's "consistent place in the shell."

## Consequences

- The v1 skeleton renders a persistent top bar (app name + account affordance) and an account
  screen (signed-in user + sign-out) — **no bottom tab bar yet** (nothing to put in it; it arrives
  with the first real feature).
- Suite look-and-feel now depends on app #2 **copying** clean tokens + shell, not on a runtime
  dependency — consistent with "no shared package at N=1; extract on second use."
- **Revisit trigger:** if a launcher app is built, decide then whether apps link back to it.
