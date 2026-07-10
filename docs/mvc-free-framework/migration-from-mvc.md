# Migration from MVC controllers

The mediator framework keeps application handlers independent of HTTP, Rebus and
gRPC. Migrate one endpoint at a time: keep the handler and its request/response
contracts, then move transport concerns into generated registrations.

## 1. Classify each controller action

For every action, identify:

- the request or query passed to the application layer;
- the response contract and HTTP status codes;
- authentication, validation and exception behavior;
- uploads, downloads, custom content negotiation and other protocol-specific
  behavior.

Actions that only bind a request, dispatch a handler and return a response are
good candidates for generated Minimal API endpoints. Actions that contain
business logic should first move that logic into a pure handler. Keep the
controller until the new endpoint has equivalent integration coverage.

## 2. Move the application contract and handler

Define the request/query and handler in the application assembly. Add an
explicit `[HttpEndpoint]` marker to the contract and keep the route and verb
stable during the migration:

```csharp
[HttpEndpoint("POST", "/api/v1/greetings")]
public sealed record CreateGreeting(string Name) : IRequest<GreetingResponse>;
```

The source generator emits the endpoint registration. The handler must not
depend on `HttpContext`, MVC model binding, `ServerCallContext` or Rebus
message context. Inject transport-neutral services such as
`IContextProvider<ClaimsPrincipal>` when caller identity is required.

Add `[RebusMessage]` or `[GrpcMethod]` only when the same contract is intentionally
exposed on those transports. These markers are opt-in; an HTTP migration does
not require enabling another transport.

## 3. Replace controller registration

Register the generated endpoints once during application startup, after the
application services and SimpleInjector integration are configured. Remove the
matching `AddControllers`, MVC route and controller registration only after all
actions in that controller have moved.

Generated endpoints preserve the route template and use the existing request
scope. Do not open a second SimpleInjector scope in an endpoint or handler.
Keep authorization and other endpoint metadata on the generated contract or
registration according to the hosting application's existing policy.

For API versions, use a route such as `/api/v1/...` or `/api/v2/...`. The
generator uses that route segment when partitioning OpenAPI documents.

## 4. Preserve MVC where it is the right adapter

Generated Minimal APIs are not a replacement for every protocol adapter.
Retain a hand-written MVC controller when it provides behavior that is
deliberately tied to MVC, such as:

- legacy clients requiring MessagePack content negotiation;
- custom formatters or binder behavior;
- an incremental migration boundary shared by old and new routes.

The controller should remain thin: deserialize and validate the transport
payload, resolve the same pure handler through the existing container, and
translate the result. Do not duplicate business logic or create a second
handler implementation. The sample's
`MessagePackGreetingController` is the compatibility pattern: it negotiates
MessagePack for an existing greeting handler while the normal JSON endpoint is
source-generated.

## 5. Move cross-cutting behavior deliberately

Map MVC behavior to the corresponding transport-neutral or endpoint concern:

| MVC concern | Migration target |
| --- | --- |
| Action filters | Endpoint metadata/middleware or handler decorators |
| Model validation | Handler validation and generated error mapping |
| `HttpContext.User` | `IContextProvider<ClaimsPrincipal>` |
| `IFormFile` | `IArkAttachment` |
| `ProblemDetails` results | Generated exception-to-ProblemDetails mapping |
| Controller constructor services | Handler or hosting composition root |

Keep protocol-specific status codes and headers covered by HTTP integration
tests. Compare the old and new endpoints for successful responses, validation,
not-found behavior, identity propagation, uploads and content negotiation.

## 6. Remove the old controller

After traffic and tests cover the generated endpoint:

1. remove the old route and controller;
2. remove MVC services and formatter packages only when no remaining controller
   needs them;
3. retain the MessagePack adapter, if required by clients;
4. update OpenAPI snapshots and client documentation;
5. verify `dotnet build` and `dotnet test` with package validation and SBOM
   generation enabled.

The migration does not require changing the handler again when the same request
is later exposed over Rebus or code-first gRPC.
