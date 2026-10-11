# ARKMF060: gRPC contract member does not match its exported .proto type

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `gRPC contract '{0}' member '{1}' {2}`

## What it checks

The generator exports a `.proto` schema for every contract served through
`[GrpcMethod]`, and contract-first clients are generated from it. A member is
reported when protobuf-net would write something other than the exported type:

- a `Guid` below `CompatibilityLevel.Level300`. The schema declares `string`,
  but protobuf-net writes its own `bcl.Guid` message until Level300;
- a Vogen value object whose primitive has no protobuf scalar. Value objects
  over `string`, `Guid`, `bool`, `int`, `long`, `float` and `double` are
  exported as that primitive and registered with protobuf-net when the services
  are mapped; any other primitive, such as `decimal`, cannot be.

The diagnostic is reported at the `[GrpcMethod]` attribute of a contract that
reaches the member, or at the call or attribute that selects a referenced
assembly.

## How to fix it

For a `Guid`, set the compatibility level once in the contracts assembly. This
changes the wire format of every `Guid` in the assembly to its canonical string:

```csharp
[assembly: ProtoBuf.CompatibilityLevel(ProtoBuf.CompatibilityLevel.Level300)]
```

The attribute can also be placed on a single contract type or member.

For a value object, wrap one of the supported primitives, or use a
`[ProtoContract]` message for the member instead.

## Suppression and configuration

Do not suppress this rule: clients generated from the exported schema cannot
read the member.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF060.md`.
