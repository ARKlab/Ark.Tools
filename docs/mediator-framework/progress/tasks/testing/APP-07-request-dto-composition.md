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

- [ ] Every sample request/query/command follows the documented naming shape.
- [ ] Direct dispatch and all enabled transports receive the same outer contract.
- [ ] A composed body binds without duplicating model fields.
- [ ] Server-set, route, query, ETag, attachment, and inherited properties retain
  their existing semantics.
- [ ] Driver bindings never resolve a persistence context for business
  assertions.

## Tests

- Add generator tests for inherited and `[HttpBody]` properties.
- Run sample direct-contract scenarios and framework transport tests.
- Inspect emitted generated source for each changed transport.
