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

The sample's
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs`
is the source to copy for a TestServer host. It builds the production container,
uses the normal startup registration/mapping, starts an in-process host, and
provides both an `HttpClient` and a gRPC message handler.

```csharp
using var context = new SampleTestContext();
context.Client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

using var response = await context.Client.GetAsync("/api/v1/greetings/" + id);
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

The generated proto should be consumed by a separate test client project. The
sample's
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient/Ark.MediatorFramework.Sample.GrpcClient.csproj`
uses `Grpc.Tools` with `GrpcServices="Client"` and references the exported
schema. Create the in-process gRPC client from the test host:

```csharp
using var context = new SampleTestContext();
using var channel = GrpcChannel.ForAddress(
    "http://localhost",
    new GrpcChannelOptions { HttpHandler = context.CreateGrpcHandler() });
var client = new GreetingsV1.GreetingsV1Client(channel);

var reply = await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.CopyFrom(id.ToByteArray()) },
    new Metadata { { "Authorization", "Bearer " + token } }).ResponseAsync;

reply.Message.Should().Be("Hello Ada");
```

The channel is disposed with the test, requests never leave the process, and
the call follows the generated protobuf contract. Test a gRPC failure with an
`RpcException`:

```csharp
var action = async () => await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.Empty }).ResponseAsync;

var exception = await action.Should().ThrowAsync<RpcException>();
exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
```

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
