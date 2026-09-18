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
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.

> **Review 2026-09-04**: The successful CI run for `d31898c` (run
> [33882765935](https://github.com/ARKlab/Ark.Tools/actions/runs/33882765935))
> completed the Debug build and test steps. The commands were not rerun locally.
