# FW-10 — Remove HTTP authentication and authorization metadata

**Status**: Draft · **Category**: framework · **Priority**: Post-release

## Problem

`HttpEndpointAttribute` currently exposes `Policy` and `AllowAnonymous`, which
couples application contracts to ASP.NET Core authorization configuration.
Authentication belongs to the ASP.NET Core host. Authorization belongs to
`Ark.Tools.Authorization` and must remain transport-independent.

## Outcomes

- HTTP endpoint contracts contain no authentication or authorization settings.
- Host authentication and transport-independent authorization decorators remain
  the only configuration points.
- Existing authorization behavior has migration guidance and regression tests.

## Acceptance

- [ ] Remove `Policy` and `AllowAnonymous` from the public HTTP attribute.
- [ ] Preserve secure-by-default behavior through `Ark.Tools.Authorization`.
- [ ] Document migration from the removed properties.
- [ ] Add HTTP and non-HTTP authorization coverage.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
