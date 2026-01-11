# Trimming Support Status

## Not Trimmable

This library is **NOT trimmable** and this is by design.

### Reason

This library fundamentally uses C# `dynamic` keyword for handler invocation that cannot be made trim-safe without breaking changes.

### Technical Details

- **Ex.cs line 67-71**: Uses `dynamic` to call authorization resource handler methods
  ```csharp
  dynamic handler = c.GetInstance(handlerType);
  return await handler.GetResouceAsync((dynamic)query, ctk);
  ```

The handler type is constructed at runtime using `MakeGenericType`, and the method is invoked dynamically.

### Impact

Applications using this library in trimmed deployments will need to:
- Ensure all authorization handler types and their methods are explicitly preserved
- The library itself cannot be trimmed

### Alternative Approach

To make this library trim-safe would require:
- Using reflection with explicit method invocation instead of `dynamic` keyword
- Adding `DynamicallyAccessedMembers` attributes to generic parameters
- Potentially breaking changes to the public API

The cost of making this library trimmable outweighs the benefits, as authorization handlers are typically few in number and would be preserved anyway in most applications.

### Related

Similar to `Ark.Tools.Solid.SimpleInjector`, this library uses dynamic invocation patterns that are fundamentally incompatible with trimming.
