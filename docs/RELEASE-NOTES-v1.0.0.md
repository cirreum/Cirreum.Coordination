# Cirreum.Coordination 1.0.0 — standalone atomic-coordination primitives

Dependency-light primitives for the two atomic operations distributed systems keep
reaching for: a single-use claim (`IReplayGuard`) and an atomic fixed-window counter
(`IRequestThrottle`). Usable by any consumer — authentication, messaging, command
pipelines, or an unrelated application — to share one set of primitives and a single
chosen backend.

Strictly additive — initial release. Depends only on
`Microsoft.Extensions.DependencyInjection.Abstractions` (no Cirreum dependencies).

---

## Why this release exists

Replay protection, nonce single-use, idempotency keys, message de-duplication, and
rate limiting all reduce to one of two atomic operations: *claim-if-absent* and
*increment-within-a-window*. Re-implementing those per feature (and per backend) is
where subtle races creep in. `Cirreum.Coordination` gives them one contract, one
in-memory default, and a single place to swap in a distributed backend.

It has **no Cirreum dependencies** — so it is usable outside Cirreum entirely, and
within Cirreum it is a shared primitive rather than an authentication-only detail.

---

## What's new

### `IReplayGuard`

```csharp
// Atomic set-if-absent: the first caller to claim a token wins; a replay loses.
bool firstUse = await replayGuard.TryClaimAsync(nonce, ttl, ct);
```

A single-use claim for replay / nonce protection, idempotency keys, and message
de-duplication.

### `IRequestThrottle`

```csharp
// Atomic fixed-window counter — returns the post-increment count + an allow/block verdict.
ThrottleOutcome outcome = await throttle.RecordAsync(clientId, window, limit, ct);
if (!outcome.Allowed) { /* outcome.RetryAfter hints when to try again */ }
```

An atomic fixed-window counter for rate limiting.

### In-memory backend + `AddCoordination`

```csharp
services.AddCoordination(c => c.UseInMemory());
```

A lock-free in-memory backend (monotonic `TickCount64` deadlines, single-sweeper
eviction) — correct for single-instance and development. Registration is **idempotent
and order-independent**: a component that needs coordination *pulls* it (registering a
fail-closed sentinel if no backend is chosen yet), and the application *chooses* the
backend; the chosen backend always wins.

### `CoordinationPostureValidator`

```csharp
CoordinationPostureValidator.Validate(services);
```

A boot-time check that fails fast when coordination was pulled but no backend was
chosen — turning a silent mis-configuration into a clear startup error.

---

## Compatibility

- **Strictly additive.** Initial release.
- **One dependency:** `Microsoft.Extensions.DependencyInjection.Abstractions`. No Cirreum
  dependencies.
- **Fail-closed throughout:** the sentinel throws rather than silently allow; non-positive
  TTL / window / limit and null / blank token / key are rejected before any work.

---

## Coordinated downstream work

`Cirreum.Coordination.Redis` provides a distributed backend over the same contracts
(`services.AddCoordination(c => c.UseRedis(...))`). Cirreum's authentication track uses
these primitives for SignedRequest replay-nonce protection and request throttling.

---

## See also

- `Cirreum.Coordination.Redis` — a distributed (Redis) backend for the same primitives
