# GEN-11 — Rename HTTP binding attributes

**Status**: Implemented · **Category**: generator-dx · **Priority**: Post-release

## Problem

`HttpQuery` and route binding conventions expose HTTP concepts without
making the transport explicit in the attribute names. Rename them to
`HttpQuery` and the corresponding `HttpRoute` marker while keeping contracts
clear.

## Outcomes

- The public names clearly identify HTTP-only binding.
- Generated binding, OpenAPI, diagnostics, and documentation use the new names.

## Acceptance

- [x] Define `HttpQuery` and `HttpRoute` semantics and XML documentation.
- [x] Update generators, diagnostics, samples, tests, and user documentation.
- [x] Verify non-HTTP contracts do not gain ASP.NET Core dependencies.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
