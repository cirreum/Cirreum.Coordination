# Cirreum.Coordination v1.0.0 — Migration Guide

> **From:** _(no prior version)_ &nbsp;•&nbsp; **To:** v1.0.0

## Why v1

This is the **initial release** of `Cirreum.Coordination`. There is no earlier
published version, so there is nothing for a consumer to migrate from.

The package provides standalone atomic-coordination primitives (`IReplayGuard`,
`IRequestThrottle`) that were previously carried inside `Cirreum.AuthenticationProvider`.
They were extracted into this dependency-light package so any consumer —
authentication, messaging, command pipelines, or an unrelated application — can share
the same primitives and a single chosen backend. That earlier arrangement was never
published to NuGet, so the extraction is not a consumer-visible migration.

---

## Breaking Changes — Find/Replace Table

None. Initial release.

---

## New Capabilities

See [`docs/RELEASE-NOTES-v1.0.0.md`](RELEASE-NOTES-v1.0.0.md) for the full surface
and usage examples.

---

## Migration Walkthrough

### 1. Add the package reference

```xml
<PackageReference Include="Cirreum.Coordination" Version="1.0.0" />
```

### 2. Register a backend

```csharp
services.AddCoordination(c => c.UseInMemory());
```

A component that needs coordination *pulls* it via `services.AddCoordination()`; the
application *chooses* the backend. `CoordinationPostureValidator.Validate(services)`
fails fast at startup if coordination was pulled but no backend was chosen.

---

## What Didn't Change

Everything — this is the first release.

---

## Downstream Package Impact

Consumers that need a distributed backend can add `Cirreum.Coordination.Redis` (a
separate package). Cirreum's own authentication track takes a `PackageReference` on
`Cirreum.Coordination 1.0.0`.
