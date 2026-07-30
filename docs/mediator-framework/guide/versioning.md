# Versioning

Version contracts rather than silently changing a released route, protobuf
field, or response shape. `IntroducedIn` is inclusive and `RetiredIn` is the
first version in which an endpoint is unavailable.

## Supersede an operation

```csharp
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}", RetiredIn = 2)]
[GrpcMethod("GetGreeting", RetiredIn = 2)]
[GrpcService("Greetings")]
public sealed record GetGreetingV1Query : IQuery<GreetingResponseV1>;

[HttpEndpoint("GET", "/api/v{version}/greetings/{id}", IntroducedIn = 2)]
[GrpcMethod("GetGreeting", IntroducedIn = 2)]
[GrpcService("Greetings")]
public sealed record GetGreetingV2Query : IQuery<GreetingResponseV2>;
```

**Outcome:** version 1 exposes the original HTTP route and gRPC method; version
2 exposes the replacement. The generator expands `{version}` in HTTP routes,
builds version-specific OpenAPI documents, and emits version-specific gRPC
service shapes.

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
