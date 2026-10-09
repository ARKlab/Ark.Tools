# ARKMF058: gRPC contract is not bindable

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `gRPC contract '{0}' cannot be bound: its {1} type '{2}' {3}`

## What it checks

A contract exposed through `[GrpcMethod]` is served by protobuf-net code-first gRPC.
protobuf-net binds a method only when every message is a protobuf contract type:

- the request type, unless the request carries an `IArkAttachment` upload;
- the response type, unless the response is an `IArkAttachment` download;
- the item type of an `IAsyncEnumerable<T>` streaming response.

A command response (`google.protobuf.Empty`) is always bindable. Scalars and other
types without `[ProtoContract]`, such as `string` or `int`, are not messages, so a
response or stream item like `IAsyncEnumerable<int>` is not bindable.

A closed generic contract, such as `Page<Book>` where `Page<T>` has
`[ProtoContract]`, is also reported. protobuf-net can serve it, but the generator
cannot export it to `.proto`, so the served and exported services would disagree.
The message then says the type is a generic protobuf contract; use a non-generic
contract type, for example `BookPage`.

The generator emits no server code and no `.proto` entry for the contract. The
diagnostic is reported at the `[GrpcMethod]` attribute when the contract is in
the compilation. For a contract in a referenced assembly, it is reported at the
`MapArkGrpcServicesFromAssembly<TMarker>()` call or the
`[ArkGenerateGrpcForAssembly]` attribute that selects the assembly.

## How to fix it

Add `[ProtoContract]` and stable `[ProtoMember(n)]` numbers to the type named in
the message, or wrap a scalar in a `[ProtoContract]` message. If the contract should
not be served over gRPC, remove `[GrpcMethod]`.

### Incorrect

```csharp
[GrpcMethod("StreamNumbers")]
[ProtoContract]
public sealed record StreamNumbers : IQuery<StreamNumbers, IAsyncEnumerable<int>>;
```

### Correct

```csharp
[GrpcMethod("StreamNumbers")]
[ProtoContract]
public sealed record StreamNumbers : IQuery<StreamNumbers, IAsyncEnumerable<NumberItem>>;

[ProtoContract]
public sealed record NumberItem
{
    [ProtoMember(1)]
    public int Value { get; init; }
}
```

## Suppression and configuration

Do not suppress this rule: a suppressed contract is still not served over gRPC.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF058.md`.
