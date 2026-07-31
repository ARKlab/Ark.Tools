# GEN-11 — Rename HTTP binding attributes

**Status**: Draft · **Category**: generator-dx · **Priority**: Post-release

## Problem

`BindFromQuery` and route binding conventions expose HTTP concepts without
making the transport explicit in the attribute names. Rename them to
`HttpQuery` and the corresponding `HttpRoute` marker while keeping contracts
clear and migration-safe.

## Outcomes

- The public names clearly identify HTTP-only binding.
- Generated binding, OpenAPI, diagnostics, and documentation use the new names.
- A documented compatibility period avoids an unnecessary abrupt migration.

## Acceptance

- [ ] Define `HttpQuery` and `HttpRoute` semantics and XML documentation.
- [ ] Update generators, diagnostics, samples, tests, and user documentation.
- [ ] Decide and document obsolete aliases or a breaking-release migration.
- [ ] Verify non-HTTP contracts do not gain ASP.NET Core dependencies.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
