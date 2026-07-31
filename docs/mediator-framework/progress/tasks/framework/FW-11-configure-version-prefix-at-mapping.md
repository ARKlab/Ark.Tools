# FW-11 — Configure the version route prefix at mapping time

**Status**: Draft · **Category**: framework · **Priority**: Post-release

## Problem

Every versioned HTTP contract currently repeats the common `/api/v{version}/`
prefix. The mapping application should configure that prefix once while
preserving contract-local route fragments and generated version expansion.

## Outcomes

- A mapping option configures the common version prefix once.
- Contracts declare only their resource route.
- Existing explicit templates remain migratable without changing handler code.

## Acceptance

- [ ] Add a mapping configuration option for the version prefix.
- [ ] Apply it consistently to generated HTTP routes and OpenAPI documents.
- [ ] Define precedence and migration behavior for explicit prefixes.
- [ ] Add generator and integration tests.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
