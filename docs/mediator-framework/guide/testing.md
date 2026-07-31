# Testing

Test an application through the interfaces its consumers use. This verifies
generated binding, serialization, authentication, authorization, exception
mapping, handler dispatch, and host configuration together.

## Boundary test workflow

1. Build a test host with the same application assembly, handler registration,
   decorators, and generated endpoint mapping as production.
2. Create HTTP requests for success, malformed input, unauthenticated,
   unauthorized, and domain-failure cases.
3. Generate a gRPC client from the exported proto and repeat the equivalent
   success and denied cases through gRPC.
4. Run Rebus handlers with the configured message scope and assert the durable
   business result.
5. Add focused tests for streaming, attachments, or concurrency when those
   features are publicly exposed.

## Build the in-process host once

The sample's
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs`
is the source to copy for a TestServer host. It builds the production container,
uses the normal startup registration and mapping, starts an in-process host,
and provides both an `HttpClient` and a gRPC message handler.

Condensed pattern:

```csharp
var container = SampleComposition.BuildContainer(new InMemNetwork(), useSqlStore: false);
var startup = new SampleStartup(container, configuration, configureFallbackPolicy: true);

_host = new HostBuilder()
    .ConfigureWebHost(web => web
        .UseTestServer()
        .ConfigureServices(startup.ConfigureServices)
        .Configure(startup.Configure))
    .Build();
_host.Start();

Client = _host.GetTestServer().CreateClient();
```

**Outcome:** every test hits the same generated endpoints, middleware, auth,
serializers, and decorators the real host uses.

## HTTP test example

```csharp
using var context = new SampleTestContext();
context.Client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", token);

using var response = await context.Client.GetAsync("/api/v1/greetings/" + id);
response.StatusCode.Should().Be(HttpStatusCode.OK);
```

Typical expectations to assert:

- status code;
- content type (`application/json` or `application/problem+json`);
- JSON body shape;
- auth failures (`401`/`403`) and validation failures (`400`).

## gRPC client for tests

The generated proto should be consumed by a separate test client project. The
sample's
`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient/Ark.MediatorFramework.Sample.GrpcClient.csproj`
uses `Grpc.Tools` with `GrpcServices="Client"` and references the exported
schema. Create the in-process gRPC client from the same test host:

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
the call follows the generated protobuf contract.

## gRPC failure example

```csharp
var action = async () => await client.GetGreetingAsync(
    new GetGreetingQuery { Id = ByteString.Empty }).ResponseAsync;

var exception = await action.Should().ThrowAsync<RpcException>();
exception.Which.StatusCode.Should().Be(StatusCode.Unauthenticated);
```

This proves the generated gRPC surface enforces host auth and rich error
mapping, not only the happy path.

## Feature-specific test patterns from the sample

| Capability | Sample test file | What it proves |
| --- | --- | --- |
| Auth and permission failures | `AuthorizationTests.cs` | `401`, `403`, and gRPC `Unauthenticated` / `PermissionDenied` behavior |
| Paging validation | `PagingTests.cs` | HTTP and gRPC return the same validated pagination rules |
| Streaming | `AsyncEnumerableStreamingTests.cs` | first HTTP item arrives before the producer completes; gRPC can cancel mid-stream |
| Attachments and downloads | `FileDownloadTests.cs` | multipart upload, download content type, and file-count rules |
| ETag and optimistic concurrency | `ConcurrencyRoundtripTests.cs` | HTTP `ETag`, `If-Match`, `412`, and gRPC concurrency parity |

## Streaming test example

The sample proves HTTP streaming is still plain JSON, not SSE framing:

```csharp
using var response = await context.Client.GetAsync(
    new Uri("/api/v1/greetings/stream?count=2&delayMilliseconds=0", UriKind.Relative));
var body = await response.Content.ReadAsStringAsync();

response.StatusCode.Should().Be(HttpStatusCode.OK);
response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
body.Should().NotContain("data:");
```

## Rebus testing guidance

For Rebus, assert business outcomes rather than generated wrapper internals.
The host setup already proves the generated handler registration and scope
creation. Application tests should verify that sending a message produces the
durable effect, outbox entry, or dead-letter behavior your workflow promises.

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
