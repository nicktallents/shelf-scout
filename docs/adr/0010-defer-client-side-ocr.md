# ADR 0010 — Defer client-side OCR of printed expiry dates to post-v1

- **Status:** Accepted
- **Date:** 2026-06-25
- **Scope:** #3 Capture
- **Deciders:** nicktallents

## Context

The confirmed baseline (`docs/planning/baseline-and-scopes.md`) pinned v1 capture as
**manual entry + client-side OCR** of the printed expiry date, with barcode and a local-AI
date suggestion deferred. This ADR **reopens that baseline decision** — as the scope-charter
rules explicitly permit, provided the change is flagged — and concludes that OCR should join
barcode and AI in the deferred bucket. The reasoning is recorded here in full because the
analysis, not the conclusion, is the durable artifact.

Tesseract.js is the only realistic v1 OCR engine: cloud OCR violates the no-recurring-cost
constraint, and the browser-native Text Detection API is too patchily supported to rely on.
So "client-side OCR" effectively means "Tesseract.js," and the decision turns on whether
Tesseract-on-packaging earns its place.

## Decision

**Defer OCR to post-v1.** v1 capture is **manual entry + the layered learned-suggestion +
name autocomplete** (ADR 0009, ADR 0011) — full stop.

### Why — the speed analysis

We timed the two paths for a packaged good honestly:

- **Manual:** type the name (autocomplete assists) → the expiry control is **already
  pre-filled** with the learned suggestion → glance at the printed date and either accept or
  tap/type it → Add.
- **OCR:** type the name **anyway** → tap scan → physically rotate the package so the date is
  in the reticle → hold steady → capture → wait 1–3 s for Tesseract on a phone → verify →
  correct on misread → Add.

Two findings fall out:

1. **OCR doesn't touch the bottleneck.** The **name** must be typed every time — it's the
   most variable field — and OCR does nothing for it. OCR attacks only the **date**, and the
   date was never the slow part: a human reads "best by Aug 02" instantly. The cost of a date
   is *input*, not *reading*, and "aim + capture + wait + verify + maybe correct" is routinely
   **slower** than reading the printed date and tapping it. Reticle-targeted OCR (the most
   accurate camera UX, since a fixed ROI removes competing lot codes) is, for the field it
   targets, a wash-to-net-loss on speed.
2. **The deferred barcode path is the accelerator that actually matters** — a barcode lookup
   returns the **name and category**, i.e. the unavoidable manual part. OCR attacks a
   non-bottleneck; barcode attacks *the* bottleneck.

### Why — reliability and cost

Tesseract was built for clean document scans; printed food dates are its worst case
(low-contrast inkjet on curved/reflective plastic, dot-matrix fonts, tiny type, surrounded by
lot codes it will happily grab instead). Realistic accuracy is mediocre, which forced an OCR
design where a miss must cost the user nothing (mandatory confirm, instant fallback to the
manual control). That is real complexity — multi-MB WASM bundle, crop preprocessing, character
whitelisting, date-regex extraction, a confirm UX, camera-permission flows — in service of a
feature that is, at best, a wash on speed.

### Why deferring paints nothing into a corner

A generic camera-capture surface gets built for **barcode** anyway; OCR can later ride on that
same surface. Deferring loses nothing structurally — it only resequences. The learned-suggestion
slot (ADR 0011) is deliberately **source-agnostic**, so OCR (and later AI) plug into the same UI
slot when they arrive.

### The case we considered and rejected

Keeping a thin OCR in v1 for **first-time purchases** of packaged goods — where there's no
learned delta yet (the suggestion is just a weak location-seed) and the exact printed date
matters. Rejected: even there the edge is thin, because reading-and-tapping is fast, and it does
not justify the complexity.

## Consequences

- v1 capture is leaner, zero-cost, and lower-risk; the genuine capture win (autocomplete +
  learned deltas making manual entry near-zero-tap) is unaffected.
- Dropping OCR also **dissolves the MM/DD-vs-DD/MM date-parsing problem**, which was almost
  entirely an OCR-string concern — manual entry via a native date control picks an unambiguous
  calendar day.
- The future capture roadmap is reordered: **barcode first** (it hits the name/identity
  bottleneck), then OCR and AI as additive sources on the shared camera surface and the
  source-agnostic suggestion slot.
- The baseline document's "Capture (v1)" line is now superseded by this ADR; future readers
  should treat v1 capture as manual-only.
