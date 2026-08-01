# AZF-04 — Authentication, authorization and user-context propagation

**Category**: azure-functions · **Priority**: security · **Scope**: FRAMEWORK + SAMPLE

## Problem

Functions access-key authorization is not ASP.NET Core authentication. Isolated
ASP.NET Core integration does not run the normal ASP.NET Core middleware pipeline,
yet non-anonymous contracts must authenticate and handlers must receive the same
principal used by transport-agnostic authorization decorators.

## Prerequisites

- AZF-03 merged.
- AZD-03 and AZD-04 decided with a named sample authentication profile.
- Review Microsoft documentation for App Service Authentication identity headers
  and the repository's `AuthenticationEx`, `HostUserContextProvider` and
  authorization behavioral tests.

## Implementation steps

1. Add registration/options APIs for direct bearer authentication and the separate
   Easy Auth opt-in profile without embedding tenant, audience, keys or secrets in
   the framework. Use direct bearer authentication in the sample.
2. For `AllowAnonymous = false`, invoke the configured ASP.NET Core authentication
   service before binding/dispatch. On no result/failure, produce 401 and the
   correct challenge headers without invoking the handler.
3. For `AllowAnonymous = true`, do not force authentication; preserve an already
   established platform principal if the approved profile supplies one.
4. Assign the authenticated principal to `HttpContext.User` and make it available
   to the existing `IContextProvider<ClaimsPrincipal>` for the full invocation
   scope.
5. Prove transport-agnostic `[RequireScopePolicy]` decorators run unchanged and
   map authenticated-but-forbidden callers to 403.
6. Isolate Easy Auth header parsing in a profile-specific service,
   validate the trusted deployment precondition, reject malformed/oversized
   payloads, and never accept raw identity headers in the direct-bearer profile.
7. Add structured NLog messages for authentication failures without interpolation,
   credentials, tokens or identity-header contents. Use
   `CultureInfo.InvariantCulture`.
8. Document the difference between trigger authorization level, application bearer
   authentication, Easy Auth and API Management.
9. In the sample, configure `Ark.Tools.NLog` through the isolated worker builder so
   structured application logs reach the Functions console/Core Tools and the
   configured Azure sink without interpolation or duplicate providers. Read
   Application Insights settings from Functions configuration, never source, and
   preserve useful worker log levels according to Microsoft's isolated-worker
   logging guidance.

## Caveats

- Do not depend on `UseAuthentication()`/`UseAuthorization()`; Microsoft states the
  ASP.NET Core middleware pipeline is unavailable.
- Never log bearer tokens, Function keys or decoded Easy Auth payloads.
- Do not accept a user principal solely because a caller supplied
  `X-MS-CLIENT-PRINCIPAL`.
- Authorization decorators are application behavior and must not be replaced by a
  generated endpoint policy list.
- Anonymous endpoints still require safe defaults for `HttpContext.User`.

## Required test coverage

- Missing, malformed, expired, wrong-audience and valid bearer cases.
- Anonymous endpoint succeeds without credentials.
- Protected endpoint: unauthenticated is 401, insufficient scope is 403, valid
  scope succeeds.
- Handler-observed user identity matches the authenticated caller.
- No handler/decorator side effect occurs after authentication failure.
- Profile-specific tests prove untrusted Easy Auth headers are not accepted.
- Logs contain no token or secret material.
- Core Tools captures a structured sample log with its named properties; configured
  Azure logging has no duplicate application event.

## Outcomes

- Functions and Minimal API enforce the same public/secured endpoint distinction.
- Existing authorization decorators and audit/user context receive the caller
  identity without Application contract changes.

## Acceptance

- [ ] AZD-03 and AZD-04 are recorded as decided.
- [ ] 401/403/success parity is covered through the runtime helper.
- [ ] `AllowAnonymous` has explicit tested behavior.
- [ ] User identity flows into handlers and auditing.
- [ ] Identity inputs and logs pass security review.
- [ ] The sample demonstrates documented NLog configuration appropriate for local
  Core Tools and Azure Functions hosting.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
