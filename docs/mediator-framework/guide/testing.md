# Testing

Test an application through the interfaces its consumers use. This verifies
generated binding, serialization, authentication, authorization, exception
mapping, and handler dispatch together.

## Boundary test workflow

1. Build a test host with the same application assembly, handler registration,
   decorators, and generated endpoint mapping as production.
2. Create HTTP requests for success, malformed input, unauthenticated,
   unauthorized, and domain-failure cases.
3. Generate a gRPC client from the exported proto and repeat the equivalent
   success and denied cases through gRPC.
4. Run Rebus handlers with the configured message scope and assert the durable
   business result.

```csharp
var response = await client.GetAsync("/api/v1/greetings/" + greetingId);
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

**Outcome:** a test failure identifies a broken public contract or host
configuration, not merely an implementation detail in generated code.

## Test the exceptional paths

Issue valid bearer tokens for authorized calls and tokens without the required
scope for denied calls. Assert the safe public error code and status, not an
internal exception string. Cancel a streamed call and assert that the producer
observes cancellation. Upload files that exceed count, size, or content-type
limits and assert rejection before storage.

Use small unit tests for pure business rules. Keep generator and framework
capability tests with the framework; adopting applications should concentrate
on their public contracts and business outcomes.

Architecture rationale: [design.md](../design.md).
