# Ark.Tools.Solid.SimpleInjector

Extension to use Ark.Tools.Solid framework with SimpleInjector dependency injection.

## Reflection-free dispatch

Queries, requests and commands that implement the self-referencing generic interfaces are
dispatched without reflection or runtime caches, as the processor resolves the handler at
compile time:

```csharp
public sealed record MyQuery(int Id) : IQuery<MyQuery, MyResult>;

// both TQuery and TResult are inferred at the call site
var result = await queryProcessor.ExecuteAsync(new MyQuery(42));
```

The same applies to `IRequest<TSelf, TResponse>` and `ICommand<TSelf>`. The `ARKSOLID001`
analyzer (shipped with the Ark.Tools.Solid package) reports a warning for types still using
the legacy single-generic interfaces and offers a code fix to migrate them.

## Trimming Support

**Status**: ❌ NOT TRIMMABLE

### Reason

This library fundamentally relies on dynamic invocation to call handler methods. The implementation uses:

1. **Runtime Type Construction**: `MakeGenericType` to construct handler types at runtime
2. **Dynamic Invocation**: C# `dynamic` keyword to invoke handler methods without compile-time type information
3. **Dependency Injection Resolution**: Handler instances are resolved from SimpleInjector container at runtime

### Code Pattern

```csharp
dynamic requestHandler = _getHandlerInstance(request);
return requestHandler.Execute((dynamic)request);
```

This pattern requires the C# dynamic binder, which uses `RequiresUnreferencedCode` APIs that are incompatible with trimming.

### Impact

Applications using this library cannot be fully trimmed. The trimmer may remove:
- Handler types that are only referenced through the container
- Handler method implementations
- Dynamic binder metadata

### Alternatives

For trim-compatible applications, use the self-referencing generic interfaces described above
(`IQuery<TSelf, TResult>`, `IRequest<TSelf, TResponse>`, `ICommand<TSelf>`): their processor
overloads use static generic dispatch and are trim-safe. Otherwise consider:

1. **Direct Handler Registration**: Register and resolve specific handler types explicitly
2. **Static Dispatch**: Use compile-time generic constraints instead of dynamic invocation
3. **Custom Implementation**: Implement `IQueryProcessor`, `ICommandProcessor`, and `IRequestProcessor` without dynamic code

### Related

- See [Trimming Guidelines](../../../../docs/trimmable-support/guidelines.md) for more information
- See [Ark.Tools.Solid](../Ark.Tools.Solid/) for the core framework (which is trimmable)
