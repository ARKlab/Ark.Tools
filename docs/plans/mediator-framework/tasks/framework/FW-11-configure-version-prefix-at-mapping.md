# FW-11 — Configure the version route prefix at mapping time

**Status**: Completed · **Category**: framework · **Priority**: Post-release

## Problem

Every versioned HTTP contract currently repeats the common `/api/v{version}/`
prefix. The mapping application should configure that prefix once while
preserving contract-local route fragments and generated version expansion.

## Outcomes

- A mapping option configures the common version prefix once.
- Contracts declare only their resource route.
- Existing explicit templates remain migratable without changing handler code.

## Acceptance

- [x] Add a mapping configuration option for the version prefix.
- [x] Apply it consistently to generated HTTP routes and OpenAPI documents.
- [x] Define precedence and migration behavior for explicit prefixes.
- [x] Add generator and integration tests.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
