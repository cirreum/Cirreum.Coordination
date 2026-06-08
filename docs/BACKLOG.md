# Backlog

Deferred work for **Cirreum.Coordination**. Items here are tracked but not yet ready
to ship — either because the cost outweighs the benefit in isolation, or because they're waiting on a
forcing function (a related change, a consumer upgrade, a coordinated multi-repo rollout).

## How this file works

- Each item is a `###` heading so it can be linked to and parsed.
- Each item declares **`SemVer:`** (`Patch` | `Minor` | `Major` | `Unspecified`),
  **`Trigger:`** (the human-readable condition that will make it ready), and
  **`Noted:`** (the date the item was added).
- The Cirreum DevOps release scripts (`PatchRelease`, `MinorRelease`, `MajorRelease`) surface items
  at-or-below the requested bump level so the operator can decide whether to fold them in before tagging.
- Items that ship: move from this file to `docs/CHANGELOG.md` under `[Unreleased]`. Items that grow into
  design discussions: promote to an ADR.

## Queued

### TimeProvider seam for testable in-memory expiry

- **SemVer:** Minor
- **Trigger:** A consumer needs deterministic, fake-clock testing of TTL / window expiry, or the real-delay
  time-based tests become a maintenance burden.
- **Noted:** 2026-06-07
- The in-memory backend reads wall-clock `Environment.TickCount64` directly, so its expiry tests use real
  `Task.Delay` with generous margins. Injecting a `TimeProvider` (defaulting to `TimeProvider.System`) would
  make expiry deterministic to test and let consumers virtualize time. Additive; default behavior unchanged.
