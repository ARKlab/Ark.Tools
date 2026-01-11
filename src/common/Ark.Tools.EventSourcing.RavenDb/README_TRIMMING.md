# Trimming Support Status

## Not Trimmable

This library is **NOT trimmable** and this is by design.

### Reason

This library integrates with RavenDB, which extensively uses reflection internally. The combination of RavenDB's reflection usage and the event sourcing patterns makes it unsafe to trim.

### Technical Details

**RavenDB Integration Issues**:
- RavenDB's `DocumentConventions.FindCollectionName` uses reflection to determine collection names from types at runtime
- The library configures RavenDB to recognize event sourcing types (`IOutboxEvent`, `AggregateEventStore<,>`) dynamically
- RavenDB performs type discovery and mapping that cannot be statically analyzed by the trimmer

**Event Sourcing Patterns**:
- Runtime type construction via `MakeGenericMethod` for event handler dispatch
- Dynamic collection name resolution based on aggregate types
- Type interface checking (`IsAssignableFromEx`) that requires preserved interface metadata

### Code Locations

- **RavenDbStoreConfigurationExtensions.cs**: Configures RavenDB conventions with type-based collection naming that uses `IsAssignableFromEx`
- **RavenDbAggregateEventProcessor.cs**: Uses `MakeGenericMethod` for dynamic event handler dispatch

### Impact

Applications using this library in trimmed deployments will need to:
- Ensure all aggregate types, event types, and related interfaces are explicitly preserved
- Be aware that RavenDB's reflection-based features may not work correctly with aggressive trimming
- Consider using explicit type preservation configurations or avoiding trimming for applications using this library

### Alternative Approach

To make this library trim-safe would require:
- Replacing RavenDB's dynamic collection naming with explicit registration
- Removing or refactoring the dynamic event handler dispatch mechanism
- Working with RavenDB team to support trimming scenarios
- Breaking changes to the public API

The cost and risk of making this library trimmable significantly outweigh the benefits for its typical usage scenarios.
