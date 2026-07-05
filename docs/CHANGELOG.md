# Changelog

All notable changes to **Cirreum.Coordination** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added

- `ISignalBroadcaster` — a third coordination primitive alongside `IReplayGuard`/`IRequestThrottle`: an ephemeral publish/subscribe signal for notifying whichever instances are currently listening on a channel. At-most-once, unbuffered — a live nudge, not a durable message (use `Cirreum.Messaging.Distributed` for that). Does not participate in `CoordinationPostureValidator`'s fail-closed posture check — silently falling back to in-process-only delivery is a safe degradation, not a security regression.
- `InMemorySignalBroadcaster` — the built-in single-instance default: a genuine in-process pub/sub bus, not a no-op. `UseInMemory()` now wires all three primitives together.

### Fixed

- `CoordinationPostureValidator`'s "no backend chosen" error text referenced ApiKey's `SelfContained` profile, which was dropped in the 2026-06-08 redesign and never actually consumed `IRequestThrottle`. Removed the stale reference.

## [1.0.0] - 2026-07-03

### Added

- Initial release of **Cirreum.Coordination** — standalone, dependency-light atomic-coordination primitives
  for .NET (depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`). Usable by any consumer
  — authentication, messaging, command pipelines, or an unrelated application — to share the same primitives
  and a single chosen backend.
- `IReplayGuard` — a single-use claim (atomic set-if-absent) for replay / nonce protection, idempotency keys,
  and message de-duplication.
- `IRequestThrottle` — an atomic fixed-window counter for rate limiting, returning the post-increment count,
  an allowed / blocked verdict, and a `RetryAfter` hint.
- Built-in in-memory backend (`InMemoryReplayGuard` / `InMemoryRequestThrottle`): lock-free, with monotonic
  `TickCount64` deadlines and single-sweeper eviction. Correct for single-instance and development
  deployments; selected via `services.AddCoordination(c => c.UseInMemory())`.
- `services.AddCoordination()` / `services.AddCoordination(configure)` — idempotent, order-independent
  registration: a component that needs coordination *pulls* it (registering a fail-closed sentinel if no
  backend is chosen yet), and the application *chooses* the backend; the chosen backend always wins.
- `CoordinationPostureValidator.Validate(services)` — a boot-time check that fails fast when coordination was
  pulled but no backend was chosen, turning a silent mis-configuration into a clear startup error.
- Fail-closed throughout: the sentinel throws rather than silently allow, and non-positive TTL / window /
  limit and null / blank token / key are rejected before any work.
