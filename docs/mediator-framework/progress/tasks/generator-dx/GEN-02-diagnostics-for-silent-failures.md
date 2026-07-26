# GEN-02 — Diagnostics for silent generator failures (A2 + B2 + B3)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK

## Problem

The generators fail silently in multiple ways — build stays green while the endpoint is missing or wrong:

1. **Verb typo** → POST: `MinimalApiEndpointGenerator.cs` maps unknown verbs to `MapPost` via a `_ => "MapPost"` style fallback (see the `"POST" => "MapPost"` switch around line 376).
2. Types with `[HttpEndpoint]`/`[GrpcEndpoint]`/`[RebusMessage]` that do **not** implement `IRequest<>`/`IQuery<>` (or `ICommand` after FW-01) are silently skipped.
3. Route template placeholders with no matching contract property are silently dropped.
4. Non-record contracts / get-only properties break compilation **inside `.g.cs`** with cryptic CS errors (`with` expressions require records; multipart requires settable props).
5. Rebus: design promises duplicate-registration and conflicting-owner diagnostics; only `ARKMF004` (blank queue) exists (`RebusEndpointGenerator.cs`).

Only `ARKMF001` exists in the MinimalApi generator; IDs 002/003 are unaccounted.

## Steps

1. Create a shared `DiagnosticDescriptors` catalog per generator assembly with reserved IDs, e.g.:
   - `ARKMF010` Error — unknown HTTP verb (remove the POST fallback).
   - `ARKMF011` Error — attributed type doesn't implement a supported handler-kind interface.
   - `ARKMF012` Error — route placeholder `{x}` has no matching property.
   - `ARKMF013` Error — contract must be a record with init/settable properties for body/multipart binding (checked in the generator, before emitting `with`).
   - `ARKMF014` Error — duplicate Rebus registration for the same contract; `ARKMF015` Error — conflicting owner/queue.
   Document all IDs in `docs/analyzers.md` (repo convention) and in `design.md`.
2. Implement each check at model-building time; on error, **skip emission for that contract** and report the diagnostic with the attribute location.
3. Tests: one generator unit test per diagnostic (input source → expected diagnostic id/severity/location) following the existing generator test project layout; plus a green-path test asserting zero diagnostics on the sample contracts.

## Outcomes

- Every currently-silent failure mode is a build **error** pointing at the contract, with a documented ID.

## Acceptance

- [x] Verb typo now fails the build (no POST fallback remains).
- [x] All five failure modes above produce documented diagnostics (tests per diagnostic).
- [x] Sample builds clean (0 new diagnostics).
- [x] `docs/analyzers.md` lists the new IDs; full solution build + tests green.
