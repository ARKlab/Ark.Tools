# API-surface snapshots

The build can gate public contract, route, and gRPC method changes with an API
snapshot. This makes an accidental wire change visible before release: review
the generated diff, decide whether the change is intentional, and update the
approved snapshot with the repository's snapshot command.

Snapshots distinguish shipped from unshipped surface. Additive changes usually
require a deliberate snapshot update; breaking changes should be versioned or
supersede a contract. The sample's generated API is the review fixture:
[`GreetingContracts.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingContracts.cs).

This guide intentionally does not prescribe a command name where the project
configuration is authoritative; use the analyzer/build output in your branch
to regenerate. The escape hatch is an explicit baseline approval in review, not
silencing the analyzer. Rationale: [`design.md`](../design.md).
