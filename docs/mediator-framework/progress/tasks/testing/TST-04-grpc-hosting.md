# TST-04 — Prove generated gRPC hosting

**Depends on:** TST-02
**Scope:** Framework hosting tests and generated client support

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Export the synthetic gRPC service proto during the build and generate a
   client with `Grpc.Tools`, following the existing sample client project
   without referencing sample proto files.
2. Host the generated service in an in-process gRPC server and call it through
   the generated client and `Grpc.Net.Client`.
3. Cover unary request/response binding, route/version lifetime as represented
   by generated services, NodaTime and polymorphic message fields, metadata
   authentication, and user-context propagation.
4. Cover `ValidationException`, business violation, not-found, authorization,
   and concurrency failures through the rich `google.rpc.Status` details owned
   by the framework.
5. Cover server streaming incrementally, cancellation, empty streams, and
   client-streaming attachment uploads including metadata-first validation.
6. Cover opaque ETag/concurrency metadata where the framework owns the mapping;
   persistence-specific row-version behavior remains in the sample SQL tests.
7. Assert exported proto text and generated client shape in framework tests, not
   in application tests.

## Outcome

- gRPC host and wire behavior is tested independently of the sample, including
  generated clients, rich errors, streaming, uploads, authentication, and
  cancellation.

## Acceptance

- [ ] The client is generated from the build-exported synthetic proto.
- [ ] No test constructs the code-first service contract as a substitute for the
  generated client.
- [ ] Rich error tests inspect the documented status/detail fields rather than
  internal exception strings.
- [ ] Streaming and upload tests prove incremental/cancellation behavior.
- [ ] Proto export and generated client builds are deterministic from a clean
  checkout.

## Tests

- Focused test classes: `GrpcUnaryTests`, `GrpcErrorsTests`,
  `GrpcAuthorizationTests`, `GrpcStreamingTests`, `GrpcUploadTests`, and
  `GrpcProtoExportTests`.
- Run the client project before the hosting test project when invoking tests
  directly.
- Required scenarios/cases:
  - generate the client from exported proto text and use it for unary,
    versioned, metadata-authenticated calls;
  - inspect rich validation/business/not-found/authorization/concurrency
    details;
  - verify incremental server streaming, cancellation, client-streaming
    uploads, and exported proto/client shape.
- Run the full-solution gates.
