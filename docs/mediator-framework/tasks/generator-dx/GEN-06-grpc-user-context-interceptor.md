# GEN-06 — gRPC user-context interceptor (A5)

**Category**: generator-dx · **Priority**: Non-blocking (pre-release if capacity) · **Scope**: FRAMEWORK + SAMPLE

## Problem

`design.md` specifies a gRPC server interceptor that materializes the authenticated user into the
mediator's `IContextProvider<ClaimsPrincipal>` flow. It doesn't exist; the sample works around it
with an `IHttpContextAccessor`-based provider explicitly labeled "sample simplification"
(`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/HostUserContextProvider.cs`).
This couples gRPC identity to ASP.NET Core's HttpContext bridging and breaks for non-HTTP-bridged scenarios.

## Steps

1. Add a server `Interceptor` in `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc` (next to
   `ArkGrpcErrorInterceptor.cs`), e.g. `ArkGrpcUserContextInterceptor`, that reads
   `ServerCallContext.GetHttpContext().User` and stores it in an ambient holder
   (`AsyncLocal<ClaimsPrincipal?>`-based accessor exposed by the package) for the duration of the call.
2. Provide an `IContextProvider<ClaimsPrincipal>` implementation backed by that ambient holder
   (register via the existing gRPC registration extension so hosts get it with one call).
3. Register the interceptor in the framework's `AddArkGrpc`-style setup (find the existing service
   registration extension in the Grpc package) so it is on by default.
4. Sample: replace `HostUserContextProvider` usage for the gRPC path with the framework provider;
   HTTP path can keep `IHttpContextAccessor` or unify on the same pattern — prefer unification if a
   matching HTTP middleware/filter is trivial.
5. Tests: gRPC call with bearer metadata → handler observes the correct `ClaimsPrincipal` (extend the
   transport parity tests in
   `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/TransportParityTests.cs`).

## Outcomes

- First-class, framework-provided user context for gRPC calls; sample hack removed; design matches implementation.

## Acceptance

- [ ] Interceptor + provider shipped and registered by default in the Grpc package.
- [ ] Sample "simplification" comment/code removed.
- [ ] gRPC handler observes the authenticated principal (test); anonymous call behavior unchanged (401 per SEC-02).
- [ ] Full solution build + tests green; `design.md` reference now accurate.
