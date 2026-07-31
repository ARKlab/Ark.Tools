# GEN-12 — Evolve enum contracts without breaking strict clients

**Status**: Draft · **Category**: generator-dx · **Priority**: Post-release

## Problem

Adding a C# enum member can be a breaking wire change because strict clients
may reject values introduced after they were generated. The framework needs an
explicit opt-in representation that allows unknown values to round-trip safely.

## Outcomes

- Contracts can opt into an evolvable enum representation.
- HTTP JSON, protobuf/gRPC, and generated clients have documented unknown-value
  behavior.
- Existing strict enum contracts retain their current behavior.

## Acceptance

- [ ] Define the opt-in contract/API and its serialization semantics.
- [ ] Preserve unknown values on supported transports where the wire format allows.
- [ ] Generate client types and tests for known and unknown values.
- [ ] Document when strict enums remain appropriate.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
