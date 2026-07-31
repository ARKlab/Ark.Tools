# Versioning

Version contracts rather than silently changing a released route, protobuf
field, or response shape. `[Versioning(Introduced = version)]` is a transport-independent,
inclusive contract lifetime. Its optional `Retired` property is the first version in
which the contract is unavailable. Apply it once to a contract exposed over
one or more transports.

## Supersede an operation

```csharp
[Versioning(Introduced = 1, Retired = 2)]
[HttpEndpoint("GET", "/api/v{version}/greetings/{id}")]
[GrpcMethod("GetGreeting")]
[GrpcService("Greetings")]
public sealed record GetGreetingV1Query : IQuery<GreetingResponseV1>;

[Versioning(Introduced = 2)]
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
| `[Versioning(Introduced = 1)]` | Version 1 | First version that exposes the contract. |
| `Retired = 2` | Never retired | First version that no longer exposes the contract. `[Versioning(Introduced = 1, Retired = 2)]` therefore serves version 1 only. |

The lifetime applies consistently to every generated transport on that
contract. Do not place a version lifetime on a particular route or gRPC method:
if two transports deliberately need different lifetimes, model separate
contracts and explicitly migrate consumers.

## Decide whether a new version is required

Use this compatibility policy when deciding whether a change needs a new
contract version:

| Change | Compatible? | Guidance |
| --- | --- | --- |
| Add an optional field | Yes | Keep the existing default and serialization rules. |
| Add a field with a default value | Yes | Existing clients continue to send/receive the default. |
| Add a required field | No | Introduce a replacement contract. |
| Remove a field | No | Older clients or responses still depend on it. |
| Change an optional field's default | No | Existing clients can observe different behavior without changing their payload. |
| Add an enum entry | No | Strict clients may reject a value they do not know. |

Changing a field type, semantics, authorization, or status behavior is also a
new wire contract. Create a replacement type rather than altering the original
type. Retain the old handler or adapt it internally until its retirement period
ends. Support for enum representations that can evolve without treating new
entries as breaking is tracked in
[GEN-12](../progress/tasks/generator-dx/GEN-12-evolvable-enums.md).

For protobuf, preserve all released field numbers even after a route has been
retired. For HTTP, keep response and error shapes stable for the version that is
still served. Review the API-surface snapshot with the versioning change.

Architecture rationale: [design.md](../design.md).
