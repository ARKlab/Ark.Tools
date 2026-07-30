# Versioning

Version contracts rather than silently changing a released route, protobuf
field, or response shape. `[IntroducedIn(version)]` is a transport-independent,
inclusive contract lifetime. `[RetiredIn(version)]` is the first version in
which the contract is unavailable. Apply them once to a contract exposed over
one or more transports.

## Supersede an operation

```csharp
[IntroducedIn(1)]
[RetiredIn(2)]
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
public sealed record GetGreetingV1Query : IQuery<GreetingResponseV1>;

[IntroducedIn(2)]
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
public sealed record GetGreetingV2Query : IQuery<GreetingResponseV2>;
```

**Outcome:** version 1 exposes the original HTTP route and gRPC method; version
2 exposes the replacement. The generator expands `{version}` in HTTP routes,
builds version-specific OpenAPI documents, and emits version-specific gRPC
service shapes.

| Attribute | Default | Meaning |
| --- | --- | --- |
| `[IntroducedIn(1)]` | Version 1 | First version that exposes the contract. |
| `[RetiredIn(2)]` | Never retired | First version that no longer exposes the contract. `RetiredIn(2)` therefore serves version 1 only. |

The lifetime applies consistently to every generated transport on that
contract. Do not place a version lifetime on a particular route or gRPC method:
if two transports deliberately need different lifetimes, model separate
contracts and explicitly migrate consumers.

## Decide whether a new version is required

Additive optional data may be compatible with existing consumers, but changing a
field type, requiredness, semantics, authorization, or status behavior is a new
wire contract. Create a replacement type rather than altering the original
type. Retain the old handler or adapt it internally until its retirement period
ends.

For protobuf, preserve all released field numbers even after a route has been
retired. For HTTP, keep response and error shapes stable for the version that is
still served. Review the API-surface snapshot with the versioning change.

Architecture rationale: [design.md](../design.md).
