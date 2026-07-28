# Dev workflow: Mode 1 (daily loop) vs Mode 2 (container verify)

Decision of record: [ADR 0021](adr/0021-aspire-dev-orchestration-only.md). This runbook is the
practical "which one do I run" guide; the ADR is the rationale.

There are two ways to run Shelf Scout locally, by design. They answer different questions and
neither replaces the other.

## Mode 1 — Aspire AppHost (daily inner loop)

**Use for:** day-to-day feature work. This is the default.

**What it does:** the `ShelfScout.AppHost` project orchestrates an ephemeral Postgres, the API,
and the Vite frontend as one debug session, with C# and TS/JS breakpoints available across all
three.

**How to run it:** in VS Code, launch **"Aspire: Launch ShelfScout.AppHost"** (`.vscode/launch.json`)
— this is F5. There is no separate compose file to remember; the AppHost (`backend/ShelfScout.AppHost/AppHost.cs`)
wires Postgres → API → Vite and starts all three.

**Cleanup guarantee:** container lifetime is tied to the debug session. Stopping the debugger
tears down Postgres, the API, and the Vite dev server. [Testcontainers'](https://testcontainers.com/)
Ryuk reaper backstops a crashed session, so nothing is left running even if the stop is unclean.
No named volumes are used — the dev database is ephemeral and reseeded every boot.

**Seed data:** sample data (a household, a membership for the `DevelopmentFallback` user,
locations, items) is seeded on startup, gated on `IsDevelopment()`. Expiration dates are computed
relative to `now`, so every fresh boot produces a stable spread across the report buckets. Mode 2
and the test suite do not seed.

## Mode 2 — `compose.verify.yaml` (container verification)

**Use for:** confirming the *container* is correct before a PR or release — not for day-to-day
debugging. There are no breakpoints in this mode; it's run-and-assert.

**What it verifies:** the real multi-stage `Dockerfile` build (the same one production uses),
against a throwaway Postgres, checking:
- the image builds and reaches Postgres over the Docker network,
- migrate-on-startup applies the schema,
- `/health` reports `Healthy`,
- the container runs as non-root uid `1000`,
- the built SPA (`wwwroot`) is served from `GET /`.

**How to run it:**

```powershell
.\scripts\verify.ps1
```

This builds and starts `compose.verify.yaml`, polls `/health` until healthy (or times out), runs
the uid and SPA-serving assertions, prints `PASS`/`FAIL`, and always tears the stack down —
including on failure.

**Cleanup guarantee:** `scripts/verify.ps1` runs `docker compose -f compose.verify.yaml down` in a
`finally` block, so the stack is removed whether verification passes or fails. Postgres has no
named volume — its writable container layer holds `PGDATA`, so `docker compose down` (even
without `-v`) leaves nothing behind.

**Not a production artifact:** `compose.verify.yaml` exists only to exercise the Dockerfile.
Production is the hand-authored per-app compose stack behind Caddy + Authentik described in
ADR 0007 — Mode 2 does not touch it and is not a step towards replacing it.

## Which mode do I use?

| Situation | Mode |
|---|---|
| Writing/debugging a feature, need breakpoints | Mode 1 (Aspire) |
| Confirming the Docker image itself is correct (build, uid, migrations, `/health`, SPA serving) before a PR | Mode 2 (`compose.verify.yaml`) |
| A bug only reproduces inside the container, and Mode 1 breakpoints can't reach it | See "vsdbg escape hatch" below |

## vsdbg-attach-into-container (documented, unbuilt escape hatch)

For the rare bug that only reproduces inside the built container — not in Mode 1's host-native
processes — the escape hatch is attaching VS Code's `vsdbg` to the running container process,
the same way you'd attach to any remote .NET process:

1. Run Mode 2 (or `docker compose -f compose.verify.yaml up -d --build`) to get the real Release
   container running.
2. Attach `vsdbg` to the `dotnet` process inside the container (VS Code's "Attach to process"
   over a remote/container target, or `docker exec` a vsdbg install into the container).
3. Debug against the container's actual filesystem and environment, then tear the stack down
   with `docker compose -f compose.verify.yaml down` as usual.

This is **not implemented** — no launch config, no vsdbg install step, no documented `docker exec`
command exists in this repo yet. Treat the steps above as the shape of the solution if/when
someone needs it, not as a supported command. If it comes up often enough to be worth automating,
that's a new ticket, not a change to Mode 1 or Mode 2.

## Deferred: auth-header-contract check

Mode 2 does not exercise the fail-closed identity-header contract (401 without a header; accepts
`X-authentik-uid` when present). That check belongs at the edge — Caddy/Authentik — which lives
in a future edge/infra repo, not this one. See ADR 0021's Consequences section. This is a known,
accepted gap in Mode 2's coverage, not an oversight.
