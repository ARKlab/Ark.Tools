# APP-07 — Adopt composed request and DTO contracts

**Depends on:** APP-03, APP-06  
**Scope:** Sample contracts, generators, drivers, and documentation

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Move sample models into static, versioned namespaces with `Input`, `Create`,
   `Update`, and `Output` records where the model evolves.
2. Move every sample request, query, and command into a static operation
   namespace with a versioned nested contract.
3. Compose request bodies with `[HttpBody]` and keep route/query values on the
   operation envelope.
   Update operations normally compose `Input` directly; do not duplicate the
   route identifier in the update body. Server-owned identifiers and computed
   values belong on `Output` with `[ServerSet]`.
4. Extend Minimal API, gRPC, Azure Functions, and API-surface generators to
   discover inherited properties and composed body members consistently.
5. Update binding drivers and Reqnroll tables so `Current` remains the model
   while a driver creates the request envelope.

## Acceptance

- [x] Every sample request/query/command follows the documented naming shape.
- [x] Direct dispatch and all enabled transports receive the same outer contract.
- [x] A composed body binds without duplicating model fields.
- [x] Server-set, route, query, ETag, attachment, and inherited properties retain
  their existing semantics.
- [x] Driver bindings never resolve a persistence context for business
  assertions.

> **Review 2026-09-02**: Closed. All public sample API request/query contracts now use static operation namespaces with nested `V1` contracts, matching the composed Book naming shape. Existing route, query, attachment, gRPC, MessagePack, Rebus, and direct-dispatch members remain on the same contracts.

## Tests

- Add generator tests for inherited and `[HttpBody]` properties.
- Run sample direct-contract scenarios and framework transport tests.
- Inspect emitted generated source for each changed transport.
