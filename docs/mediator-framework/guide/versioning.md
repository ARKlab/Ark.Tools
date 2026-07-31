# Versioning

Version contracts rather than silently changing a released route, protobuf
field, or response shape. `[Versioning(Introduced = version)]` is a
transport-independent, inclusive contract lifetime. Its optional `Retired`
property is the first version in which the contract is unavailable. Apply it
once to a contract exposed over one or more transports.

## Attribute reference

| Attribute member | Default | Meaning | Observable effect |
| --- | --- | --- | --- |
| `Introduced` | `1` | Inclusive first API version exposing the contract | HTTP, OpenAPI, gRPC, and snapshots start at that version |
| `Retired` | `0` (never retired) | Exclusive first API version that no longer exposes the contract | Contract disappears starting at that version |

`[Versioning(Introduced = 1, Retired = 2)]` therefore means “active only in
version 1”. `[Versioning(Introduced = 2)]` means “active from version 2 onward”.

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
service shapes such as `GreetingsV1` and `GreetingsV2`.

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

Additional practical rules:

- changing a field type is breaking;
- changing a field's semantics is breaking even if the type stays the same;
- changing public auth or error behavior is a public contract change;
- renaming a gRPC service or method is a public contract change.

Support for enum representations that can evolve without treating new entries
as breaking is tracked in
[GEN-12](../progress/tasks/generator-dx/GEN-12-evolvable-enums.md).

## What changes per transport

| Transport | Versioned output |
| --- | --- |
| HTTP | `/api/v1/...`, `/api/v2/...`, separate routes |
| OpenAPI | one document per version, for example `v1` and `v2` |
| gRPC | version-specific generated service names such as `GreetingsV1` and `GreetingsV2` |
| API-surface snapshot | lifetime metadata on contract lines |

The lifetime applies consistently to every generated transport on that
contract. Do not place a version lifetime on a particular route or gRPC method:
if two transports deliberately need different lifetimes, model separate
contracts and explicitly migrate consumers.

## Response-shape example

The sample evolves `GreetingResponse` into `GreetingResponseV2` by adding
`MessageLength` only on the replacement contract. That means:

- v1 clients keep receiving the original response shape;
- v2 clients opt into the new shape explicitly;
- the old protobuf field numbers remain reserved for the v1 contract.

For protobuf, preserve all released field numbers even after a route has been
retired. For HTTP, keep response and error shapes stable for the version that is
still served. Review the API-surface snapshot with the versioning change.

## Versioning checklist

Before accepting a versioned change:

1. Decide whether the change is additive or breaking.
2. Keep old protobuf field numbers stable.
3. Expose both versions during the migration window when needed.
4. Update version-specific OpenAPI documents.
5. Test old and new clients explicitly.
6. Review the API-surface snapshot diff.

Architecture rationale: [design.md](../design.md).
