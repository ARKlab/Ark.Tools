# Azure Functions isolated-worker hosting specification

Status: **decided** — decisions are recorded in
[`progress/azure-functions-decision-log.md`](progress/azure-functions-decision-log.md).

## Goal and scope

An application references one additional transport package and hosts the same
`[HttpEndpoint]` contracts in a .NET isolated Azure Functions app. The application
contracts, handlers, validators, authorization decorators, response DTOs, route
templates and version lifetimes remain unchanged. A dedicated source generator
emits HTTP-trigger functions; a runtime package handles the ASP.NET Core
request/response translation that cannot be generated economically.

In scope:

- .NET isolated worker with ASP.NET Core HTTP integration;
- `net10.0` and Azure Functions runtime v4;
- JSON HTTP endpoints generated from the existing `[HttpEndpoint]`,
  `[Versioning]`, `[HttpQuery]`, `[ServerSet]` and `[ETag]` metadata;
- route, query, JSON body and multipart binding;
- authentication, transport-agnostic authorization and user-context propagation;
- validation and domain failures as the same `application/problem+json` shapes;
- command/request/query status semantics, ETags, uploads, downloads and JSON
  streaming where the Functions host supports the required behavior;
- a sibling Functions host in `samples/Ark.MediatorFramework.Sample`;
- outbound-only Rebus and local plus Functions-runtime integration testing.

Out of scope:

- MessagePack, including MessagePack content negotiation and buffered MessagePack
  streaming; a contract with `AcceptsMessagePack = true` is rejected by the
  Functions generator;
- hosting Rebus workers or generated Rebus receive handlers in the Function app;
- gRPC endpoints in the Function app;
- non-HTTP Azure Functions bindings generated from mediator contracts;
- OpenAPI document generation and UI hosting;
- replacing Azure Functions access keys, App Service Authentication, API
  Management, or platform authorization.

## Microsoft platform constraints

The design follows Microsoft's isolated-worker guidance:

- use `FunctionsApplication.CreateBuilder(args)` and
  `ConfigureFunctionsWebApplication()`;
- use `Microsoft.AspNetCore.Http.HttpRequest`, `HttpResponse` and ASP.NET Core
  result types rather than the built-in `HttpRequestData` projection;
- generate `[Function]` and `[HttpTrigger]` attributes because Functions discovers
  routes from trigger metadata, not ASP.NET Core endpoint routing;
- do not depend on ASP.NET Core middleware, route groups or the Minimal API
  endpoint pipeline: Microsoft explicitly states that ASP.NET Core integration
  does not expose those features;
- configure ASP.NET Core JSON through `AddMvc().AddJsonOptions(...)`; worker
  serializer options do not configure ASP.NET Core HTTP serialization;
- remove the Functions default `api` prefix in `host.json` when generated routes
  already contain `/api`, avoiding a visible `/api/api/...` route;
- use Azure Functions Core Tools to exercise the real Functions host locally.

Primary references:

- [Guide for running C# Azure Functions in the isolated worker model](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Azure Functions HTTP trigger](https://learn.microsoft.com/azure/azure-functions/functions-bindings-http-webhook-trigger)
- [Develop Azure Functions locally using Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [App Service Authentication and Authorization](https://learn.microsoft.com/azure/app-service/overview-authentication-authorization)

Package versions are selected during implementation from the .NET support table
in the isolated-worker guide and pinned centrally. No preview dependency is
permitted without an explicit decision.

## Package and project shape

Add two projects:

| Project | Responsibility |
| --- | --- |
| `Ark.Tools.MediatorFramework.AzureFunctions` | Host marker/configuration API, registration helpers, request binding, dispatch scope, authentication, result writing, ProblemDetails, ETag and attachment helpers |
| `Ark.Tools.MediatorFramework.AzureFunctions.Generators` | Incremental discovery and generation of one HTTP-trigger method per active route/version |

The runtime package includes the generator as an analyzer, matching the existing
Minimal API package shape. It references the transport-neutral mediator package,
the shared ProblemDetails package and the minimum Azure Functions/ASP.NET Core
surface required by generated code. It does not reference the Minimal API runtime:
sharing behavior by calling Minimal API results or route builders would couple the
new host to APIs that Azure Functions does not execute.

The Function host opts in with one or more shared assembly-level HTTP host markers.
Each marker selects a contract assembly through a marker type and may include or
exclude exact contracts. Empty include/exclude lists preserve assembly-wide
discovery; multiple markers compose one host surface from multiple contract
assemblies. All markers in a host carry the same version prefix because trigger
attributes require compile-time constants. The Minimal API generator supports the
same selection model and prefix while its existing mapping API remains available
for backward compatibility. No Azure Functions attribute is added to the
Application assembly.

## Generation model

The generator reuses the existing HTTP contract semantics but owns its emission:

1. Find the host opt-in markers and resolve their selected contract assemblies,
   inclusions and exclusions. Diagnose invalid or conflicting selections.
2. Validate the same handler kinds, verbs, route placeholders, version lifetimes,
   attachment shapes, ETag shapes and duplicate operation constraints as the
   Minimal API generator.
   Report a compile-time error and emit no Function for a selected contract with
   `AcceptsMessagePack = true`. An unselected contract produces no Functions
   diagnostic.
3. Apply the host's version prefix to templates that do not already contain a
   `{version}` segment, preserving authoritative explicit templates.
4. Expand `Introduced`/exclusive `Retired` lifetimes into concrete trigger routes.
5. Normalize trigger routes to omit the leading slash.
6. Emit a stable, unique `[Function("...")]` name and an
   `[HttpTrigger(..., Methods = [...], Route = "...")] HttpRequest` parameter.
7. Emit only thin contract-specific metadata and a call into a typed runtime
   helper. Do not duplicate binding, authentication, exception or response logic
   in every generated method.

Generated function names are deterministic and include contract identity plus
concrete API version. Collisions and unsupported Function metadata are
compile-time diagnostics. Existing API-surface snapshots gain Azure Functions
route lines so route changes are reviewed.

The Functions generator and Minimal API generator must consume a shared internal
HTTP endpoint model or equivalent shared tests before feature work proceeds.
Copying the current semantic-analysis implementation into a fourth independent
generator is not acceptable because parity would immediately begin to drift.

## Invocation pipeline

Each generated method delegates in this order:

1. establish one `AsyncScopedLifestyle` SimpleInjector scope for the invocation;
2. authenticate a non-anonymous endpoint and populate the principal used by
   `IContextProvider<ClaimsPrincipal>`;
3. bind route, query, body, headers or multipart form data into the existing
   request envelope;
4. overwrite `[ServerSet]` members and apply HTTP-owned values such as ETag
   preconditions after client binding;
5. resolve the exact generated handler interface from SimpleInjector;
6. execute it with the Functions invocation cancellation token using `async` and
   `await`;
7. translate the result, status, headers and body;
8. map exceptions with `ExceptionProblemDetailsMapper`, log the exception
   server-side and emit the same safe ProblemDetails contract;
9. dispose attachment streams and the invocation scope according to the same
   ownership rules as the Minimal API host.

Binding parity:

| Contract source | Azure Functions behavior |
| --- | --- |
| Route | Read from `HttpRequest.RouteValues`; use invariant conversion and reject invalid values with 400 |
| Query | Follow the existing body-less and `[HttpQuery]` rules, including collections |
| JSON body | Deserialize the complete envelope with host-configured ASP.NET Core JSON options, then overwrite route/query/server-owned members |
| `[ServerSet]` | Never trust client input; reset after deserialization |
| `[ETag]` | `If-Match`/`If-None-Match` override the model exactly as on Minimal API |
| Attachment | Read multipart form files, sanitize names, enforce request/file-count/content-type limits, and expose `IArkAttachment` |

Malformed JSON, missing required input, conversion failures, multipart limit
violations and unsupported media types return the same status and
`application/problem+json` category as Minimal API.

## Authentication and authorization

Azure Functions `AuthorizationLevel` protects a trigger with Functions keys; it is
not ASP.NET Core policy authorization. The transport package must not present one
as the other.

The parity profile uses `AuthorizationLevel.Anonymous` at trigger discovery and
performs application authentication for every contract where
`HttpEndpointAttribute.AllowAnonymous` is false. The helper invokes the configured
ASP.NET Core authentication service, challenges failures as 401, assigns the
resulting principal to `HttpContext.User`, and then invokes the handler. Existing
transport-agnostic authorization decorators enforce application permissions and
produce 403 through the shared exception mapper.

Platform protection remains composable:

- deployments may require App Service Authentication before the worker;
- Function keys or API Management can be added as a second perimeter only when
  clients accept a different credential contract;
- injected platform identity headers are not trusted unless the documented App
  Service Authentication integration is explicitly enabled and tested.

The sample uses direct bearer authentication. Easy Auth identity reconstruction is
a separately registered and tested opt-in profile; it never trusts a caller-supplied
identity header without the documented trusted deployment precondition.

## Responses and parity matrix

| Capability | Required Functions behavior |
| --- | --- |
| Request/query success | Existing 200/custom status behavior |
| Null result | Existing query 404 and request 204/custom behavior |
| Command | 204 for inline command; 202 when existing contract semantics enqueue work |
| Validation/domain errors | Same shared ProblemDetails status, content type and extensions |
| Unexpected error | Structured server log; generic production 500 ProblemDetails |
| ETag | Same quoted header, conditional request handling and 304 behavior |
| Upload | Single/multiple multipart files with the existing hardening rules |
| Download | Stream, sanitized filename and content type; null is 404 |
| JSON stream | Incremental JSON array and cancellation if the Functions ASP.NET Core response path proves it does not buffer |
| MessagePack | Not supported; a selected `AcceptsMessagePack = true` contract is a compile-time error |

Streaming parity is evidence-based: the implementation task must first prove
first-item flush and cancellation through Core Tools. If the platform buffers the
response, that task stops and records the limitation rather than claiming parity.

## OpenAPI exclusion

OpenAPI is excluded until a suitable Microsoft or community-supported Azure
Functions mechanism is available. The Functions host does not use
`Microsoft.AspNetCore.OpenApi`, because no ASP.NET Core endpoint metadata graph
exists for it to inspect. It also does not use
`Microsoft.Azure.Functions.Worker.Extensions.OpenApi`; that extension is in
maintenance mode, supports OpenAPI only through 3.0.1, and requires duplicated
attributes on generated Function methods.

Reference:
[Azure Functions OpenAPI extension maintenance notice and supported versions](https://github.com/Azure/azure-functions-openapi-extension#azure-functions-openapi-extension).

The framework does not maintain a custom generator/runtime OpenAPI pipeline.
Callable endpoint parity remains required; OpenAPI parity is not part of this
workstream.

## Rebus in a Function host

The sample Function app configures Rebus as an outbound-only client:

- Azure deployment uses `UseAzureServiceBusAsOneWayClient` and generated
  `ConfigureArkRebusRouting<TAssemblyMarker>()`;
- tests use the repository's drainable in-memory one-way transport without changing
  its existing behavior; incompatible semantics require a separately named
  `DrainableV2`;
- it does not call generated Rebus handler registration;
- it does not configure an input queue, workers, subscriptions, retry processing
  or an outbox processor;
- messages are consumed by a separate worker process.

This constraint prevents a scale-to-zero Function host from accidentally becoming
a long-running competing consumer. HTTP handlers may send commands/events through
`IBus`; they may not depend on receiving a reply in the same Function process.

## Sample and testing specification

Add `Ark.MediatorFramework.Sample.AzureFunctions` beside `WebInterface`. It
references the same Application package and demonstrates the same versioned
greeting, validation, authorization, ProblemDetails, ETag, single/multi-file
upload, download, streaming and outbound Rebus workflow. It contains `host.json`
with an empty route prefix and JSON-only HTTP configuration.

Testing has three layers:

1. **Generator tests** compile representative contracts and assert trigger name,
   method, concrete route, version expansion and diagnostics.
2. **Runtime helper tests** use `DefaultHttpContext` plus the real application
   container to cover binding, auth, dispatch, errors, headers and files without
   mocking Azure worker internals.
3. **Functions boundary tests** start Azure Functions Core Tools against the built
   sample, wait on a health endpoint, call it with `HttpClient`, and shut it down
   deterministically. These tests compare selected responses byte-for-byte or
   semantically with the Minimal API host and prove route discovery, host
   configuration, serialization, authorization and streaming behavior.

Core Tools availability must be explicit in CI. The complete Functions boundary
suite runs on every pull request and fails if Core Tools or the host is unavailable;
the test assembly must not silently pass when the host was never started. The
implementation updates `.github/workflows/ci.yml` with a pinned Core Tools install
and a dedicated boundary-test job or step; adding tests without wiring that workflow
is incomplete.

## Definition of feature parity

Parity does not mean shared generated source. It means that, for every supported
JSON HTTP contract, both hosts expose the same:

- verb and external route;
- active API versions;
- route/query/body/multipart binding and server-owned-field protection;
- authentication requirement and application authorization outcome;
- handler and decorator chain;
- success/null/error status;
- JSON or ProblemDetails body;
- ETag, content type, content disposition and relevant caching headers;
- cancellation and streaming behavior where the platform proof passes.

A committed matrix enumerates every sample `[HttpEndpoint]` and records the
Minimal API and Functions test proving each applicable capability. Any intentional
gap is a named limitation, never an untested assumption.
