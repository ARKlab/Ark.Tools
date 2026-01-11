# Ark.Tools.Solid.SimpleInjector

Extension to use Ark.Tools.Solid framework with SimpleInjector dependency injection.

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

For trim-compatible applications, consider:

1. **Direct Handler Registration**: Register and resolve specific handler types explicitly
2. **Static Dispatch**: Use compile-time generic constraints instead of dynamic invocation
3. **Custom Implementation**: Implement `IQueryProcessor`, `ICommandProcessor`, and `IRequestProcessor` without dynamic code

### Related

- See [Trimming Guidelines](../../../../docs/trimmable-support/guidelines.md) for more information
- See [Ark.Tools.Solid](../Ark.Tools.Solid/) for the core framework (which is trimmable)
