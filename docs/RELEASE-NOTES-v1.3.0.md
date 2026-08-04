# Cirreum.Coordination 1.3.0 — backends can now fail fast at startup

## Why this release exists

`CoordinationPostureValidator` has always caught one mis-configuration: coordination was
*required* but no backend was chosen. The next gap sat just behind it — a backend *was* chosen,
but a registration it depends on is absent (the Redis backend's `IConnectionMultiplexer`), and
that surfaced only on the first coordinated request. The validator runs at the right time to
catch this; it just had no way for backends to contribute their own checks.

## What's new

**`ICoordinationPostureCheck`** — a backend-contributed boot-time check.
`CoordinationPostureValidator.Validate` now runs every registered check after its own sentinel
scan and throws on the first failure. Checks are registered as **singleton instances** and are
**pure descriptor inspection** — the validator runs before a service provider exists, so it
can only see instances carried on the descriptors themselves, and a check must build nothing
and connect to nothing.

The first contributor ships alongside this release: `Cirreum.Coordination.Redis`'s `UseRedis()`
registers a check that verifies its `IConnectionMultiplexer` (unkeyed, or under the given
`connectionKey`) is actually registered — anchored to the backend registration itself, so a
later `UseXxx()` that replaces Redis disarms the check.

## Compatibility

Fully additive. `Validate`'s existing sentinel behavior is unchanged; hosts that already invoke
it (the authentication umbrella does) pick up backend checks automatically as backends ship
them.

## See also

- `docs/CHANGELOG.md` — the enumerated changes
- `Cirreum.Coordination.Redis` — the first `ICoordinationPostureCheck` contributor
