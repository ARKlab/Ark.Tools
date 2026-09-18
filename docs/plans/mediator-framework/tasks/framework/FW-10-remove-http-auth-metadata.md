# FW-10 — Remove HTTP policy metadata

**Status**: Complete · **Category**: framework · **Priority**: Post-release

## Problem

`HttpEndpointAttribute` currently exposes `Policy` and `AllowAnonymous`.
`Policy` couples application contracts to ASP.NET Core authorization
configuration and should be removed. `AllowAnonymous` is a deliberate
exception: ASP.NET Core requires every generated endpoint to use
`RequireAuthenticatedUser()` by default, so contracts need an explicit
opt-out for public endpoints. Authentication belongs to the ASP.NET Core host.
Authorization policies belong to `Ark.Tools.Authorization` and must remain
transport-independent.

## Outcomes

- HTTP endpoint contracts contain no policy setting; `AllowAnonymous` remains
  the explicit opt-out from the host's authenticated-by-default route policy.
- Host authentication and transport-independent authorization decorators remain
  the configuration points for authentication and authorization.
- Existing authorization behavior has migration guidance and regression tests.

## Acceptance

- [x] Remove `Policy` from the public HTTP attribute.
- [x] Keep `AllowAnonymous` and document that it opts an endpoint out of the
      host default `RequireAuthenticatedUser()` policy.
- [x] Preserve secure-by-default behavior at the ASP.NET Core host level and
      through `Ark.Tools.Authorization`.
- [x] Document migration from `Policy` to host route-group policy and
      transport-independent authorization decorators.
- [x] Add HTTP and non-HTTP authorization coverage.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
