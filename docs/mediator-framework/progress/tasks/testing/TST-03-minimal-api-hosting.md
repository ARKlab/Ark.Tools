# TST-03 — Prove generated Minimal API hosting

**Depends on:** TST-02
**Scope:** Framework hosting tests only

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Build a `TestServer` from the synthetic container and generated endpoint
   mapping, without calling sample startup code.
2. Cover explicit route, query, body, optional-value, and cancellation-token
   binding. Include a route parameter on a versioned endpoint.
3. Send a request that attempts to set a `[ServerSet]` member and assert the
   handler receives the server-owned value, not the client value.
4. Exercise success, null/not-found, configured status semantics, validation
   failure, business violation, and unexpected exception mapping through the
   framework's configured ProblemDetails path.
5. Test authentication and the transport-agnostic authorization decorator with
   an anonymous principal, an authenticated principal without the policy, and a
   principal with the policy.
6. Test JSON and MessagePack request/response negotiation with the framework
   serializer configuration. Keep these assertions in this project, not in the
   sample application tests.
7. Test generated multipart attachment binding, file-count/size/content-type
   limits, attachment download, and rejection before the handler stores data.
8. Test `IAsyncEnumerable<T>` response behavior: plain JSON array, first item
   available before producer completion, empty sequence, and cancellation
   observed by the producer.
9. Test OpenAPI generation and schema filtering here, including version
   partitioning, server-set omission, polymorphism, NodaTime, and XML
   documentation. Use snapshots only for framework-generated output.

## Outcome

- Minimal API binding, hosting, errors, serialization, OpenAPI, attachments,
  streaming, authorization, and cancellation have framework-owned behavioral
  coverage.

## Acceptance

- [ ] Tests use only synthetic contracts and framework registrations.
- [ ] Each listed binding and error case has a named test with a deterministic
  assertion.
- [ ] Streaming tests prove incremental delivery and cancellation, not merely
  the final array.
- [ ] OpenAPI and wire assertions do not appear in the sample Reqnroll suite.
- [ ] Tests pass with no external HTTP service.

## Tests

- Focused test classes: `MinimalApiBindingTests`, `MinimalApiErrorsTests`,
  `MinimalApiAuthorizationTests`, `MinimalApiSerializationTests`,
  `MinimalApiAttachmentsTests`, `MinimalApiStreamingTests`, and
  `MinimalApiOpenApiTests`.
- Generator snapshot tests remain in
  `tests/Ark.Tools.MediatorFramework.Tests/`; hosting tests invoke the generated
  registration to prove it works at runtime.
- Required scenarios/cases:
  - valid and invalid route/query/body/server-set binding, including a
    versioned route;
  - success, not-found, validation, business, unexpected-error, and
    authorization outcomes with deterministic assertions;
  - JSON/MessagePack negotiation, multipart limits/downloads, incremental
    streaming/cancellation, and filtered versioned OpenAPI output.
- Run the focused project, then the full-solution gates.
