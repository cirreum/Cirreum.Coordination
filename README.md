# Cirreum.Coordination

[![NuGet Version](https://img.shields.io/nuget/v/Cirreum.Coordination.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Coordination/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Cirreum.Coordination.svg?style=flat-square&labelColor=1F1F1F&color=003D8F)](https://www.nuget.org/packages/Cirreum.Coordination/)
[![GitHub Release](https://img.shields.io/github/v/release/cirreum/Cirreum.Coordination?style=flat-square&labelColor=1F1F1F&color=FF3B2E)](https://github.com/cirreum/Cirreum.Coordination/releases)
[![License](https://img.shields.io/badge/license-MIT-F2F2F2?style=flat-square&labelColor=1F1F1F)](https://github.com/cirreum/Cirreum.Coordination/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-003D8F?style=flat-square&labelColor=1F1F1F)](https://dotnet.microsoft.com/)

**Atomic and ephemeral coordination primitives for .NET — single-use claims, fixed-window throttling, and pub/sub signaling over a pluggable backend**

## Overview

**Cirreum.Coordination** provides small, focused coordination primitives with a built-in in-memory default and a pluggable backend — the same charter classic coordination services (ZooKeeper, etcd, Consul) bundle as locks, leases, and watch/notify:

- **`IReplayGuard`** — a single-use claim (atomic set-if-absent): the first caller to claim a token wins; replays within the window are rejected. For nonce / replay protection, idempotency keys, and message de-duplication.
- **`IRequestThrottle`** — an atomic fixed-window counter: records a hit against a key and reports whether the caller is still within the limit. For rate limiting.
- **`ISignalBroadcaster`** — an ephemeral publish/subscribe primitive: notifies whichever instances are currently listening on a channel. At-most-once, unbuffered — a live nudge, not a durable message (for that, use `Cirreum.Messaging.Distributed`).

It depends only on `Microsoft.Extensions.DependencyInjection.Abstractions`, so it works standalone in any .NET application. Add **Cirreum.Coordination.Redis** for distributed, multi-instance coordination.

## Installation

```bash
dotnet add package Cirreum.Coordination
```

## Usage

Choose a backend once, at the application level:

```csharp
// single-node / development
builder.Services.AddCoordination(c => c.UseInMemory());

// distributed (reference Cirreum.Coordination.Redis)
builder.Services.AddCoordination(c => c.UseRedis());
```

Consume the primitives wherever you need them:

```csharp
public sealed class NonceChecker(IReplayGuard replayGuard) {
    public ValueTask<bool> IsFirstUseAsync(string nonce) =>
        replayGuard.TryClaimAsync(nonce, TimeSpan.FromMinutes(2));   // true once, false on replay
}

public sealed class RateLimiter(IRequestThrottle throttle) {
    public async Task<bool> AllowAsync(string clientId) {
        var outcome = await throttle.RecordAsync(clientId, TimeSpan.FromMinutes(1), limit: 100);
        return outcome.Allowed;   // outcome.RetryAfter hints when throttled
    }
}

public sealed class CacheInvalidationSignal(ISignalBroadcaster broadcaster) {
    public ValueTask NotifyAsync(string key) =>
        broadcaster.PublishAsync("cache-invalidated", Encoding.UTF8.GetBytes(key));

    public ValueTask ListenAsync() =>
        broadcaster.SubscribeAsync("cache-invalidated", (payload, _) => {
            var key = Encoding.UTF8.GetString(payload.Span);
            // evict key locally
            return ValueTask.CompletedTask;
        });
}
```

### Pull-and-validate (fail fast on a missing backend)

A component that *requires* coordination can pull the dependency so a mis-configured host fails fast at
startup rather than at the first request:

```csharp
services.AddCoordination();                       // ensure: registers a fail-closed sentinel if no backend yet
// ...later, once every registration has run:
CoordinationPostureValidator.Validate(services);  // throws if coordination was pulled but no backend chosen
```

`AddCoordination()` (ensure) and `AddCoordination(c => c.UseX())` (choose) are idempotent and
order-independent — the chosen backend always wins, regardless of call order. Until a backend is chosen, the
primitives are backed by a fail-closed sentinel that throws rather than silently allow.

### Scoping (sharing one backend across applications and environments)

A distributed backend is one flat keyspace: without a scope, two applications (or a Test and a Production
deployment of the same application) sharing an instance share replay claims, throttle windows, and signal
channels — colliding throttle keys count against each other, and one deployment's signals are delivered to
the other. `CoordinationScope` namespaces all of it:

```csharp
builder.Services.AddCoordination(c => c
    .UseRedis()
    .WithScope("MyApp", "Production"));   // canonical {app}:{env}, or WithScope("any-opaque-value")
```

Distributed adapters fold the scope into every key and channel they touch (the in-memory backend ignores
it — a process is already its own scope). The scope value is opaque to this package; composition surfaces
that know the application's identity (e.g. Cirreum's authentication composition) provide the canonical
`{ApplicationName}:{EnvironmentName}` default automatically when none is registered, and an explicit
`WithScope(...)` always wins.

## Contribution Guidelines

1. **Be conservative with new abstractions**  
   The API surface must remain stable and meaningful.

2. **Limit dependency expansion**  
   Only add foundational, version-stable dependencies.

3. **Favor additive, non-breaking changes**  
   Breaking changes ripple through the entire ecosystem.

4. **Include thorough unit tests**  
   All primitives and patterns should be independently testable.

5. **Document architectural decisions**  
   Context and reasoning should be clear for future maintainers.

6. **Follow .NET conventions**  
   Use established patterns from Microsoft.Extensions.* libraries.

## Versioning

Cirreum.Coordination follows [Semantic Versioning](https://semver.org/):

- **Major** - Breaking API changes
- **Minor** - New features, backward compatible
- **Patch** - Bug fixes, backward compatible

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Cirreum Foundation Framework**  
*Layered simplicity for modern .NET*
