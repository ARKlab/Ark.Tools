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

- [ ] Every sample request/query/command follows the documented naming shape (only the Book_* contract family in `API/BookContracts.cs:11-127` uses the static versioned `Input`/`Create`/`Update`/`Output` namespace shape; `BookPrintProcessContracts.cs:54,65,73`, `BookReviewContracts.cs:37,54`, `ReadingActivityContracts.cs:54,71`, `BookStreamingContracts.cs:41`, `AttachmentContracts.cs:40,55`, and `AuditContracts.cs:47` remain flat records).
- [x] Direct dispatch and all enabled transports receive the same outer contract (proven for the composed Book_* contracts by `BookTransportBoundaryTests.cs`; not yet demonstrated for the flat contracts above).
- [x] A composed body binds without duplicating model fields (`GeneratorSnapshotTests.cs:385-462` for `[HttpBody]`/inherited properties on the Book_* contracts).
- [x] Server-set, route, query, ETag, attachment, and inherited properties retain
  their existing semantics.
- [x] Driver bindings never resolve a persistence context for business
  assertions (no `Drivers/*.cs` file uses `CreateDataContextAsync`; one *step*, `Steps/BookPrintingProcessSteps.cs:112`, still resolves a context for scenario seeding — tracked under APP-01/APP-08).

## Tests

- Add generator tests for inherited and `[HttpBody]` properties.
- Run sample direct-contract scenarios and framework transport tests.
- Inspect emitted generated source for each changed transport.
