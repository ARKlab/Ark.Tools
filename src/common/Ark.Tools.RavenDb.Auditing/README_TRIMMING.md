# Trimming Support Status

## Not Trimmable

This library is **NOT trimmable** and this is by design.

### Reason

This library fundamentally relies on runtime reflection and dynamic type discovery that cannot be made trim-safe:

1. **Assembly scanning** - Uses `Assembly.GetTypes()` to discover all types implementing `IAuditableEntity` at runtime
2. **Dynamic code** - Uses C# `dynamic` keyword to access properties on audit entities at runtime

### Technical Details

- **Ex.cs line 28**: `assemblies.SelectMany(x => x.GetTypes())` - Discovers auditable entity types from loaded assemblies
- **RavenDbAuditProcessor.cs lines 99, 111**: Dynamic property access on audit entities

### Impact

Applications using this library in trimmed deployments will need to:
- Ensure all auditable entity types are explicitly preserved
- The library itself cannot be trimmed

### Alternative Approach

To make this library trim-safe would require:
- Explicit registration of all auditable entity types (breaking change)
- Refactoring away from dynamic types to strong-typed code (significant redesign)

The cost of making this library trimmable outweighs the benefits for its typical usage scenarios.
