# FW-06 — `IAsyncEnumerable<T>` streaming responses

**Category**: framework · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

A handler that returns a large collection must materialize it entirely
(`IQuery<IEnumerable<T>>`), which buffers the whole result in server memory and delays the first byte.
Neither generator recognizes `IAsyncEnumerable<T>` today: the Minimal API generator treats it as an
ordinary response type and the gRPC generator emits a unary method.

## Design

See `docs/mediator-framework/design.md` → *Streaming collection responses*.

A contract whose response type is `IAsyncEnumerable<T>`
(`IQuery<IAsyncEnumerable<T>>` / `IRequest<IAsyncEnumerable<T>>`) is a streaming collection:

| Transport | Shape |
| --- | --- |
| Minimal API + JSON | native System.Text.Json streaming of a JSON array; no buffering, **no Server-Sent Events** |
| Minimal API + MessagePack | buffered into one MessagePack array (the format needs the element count in the array header) |
| gRPC | server-streaming method; `stream` in the exported `.proto` |
| Rebus | not supported — a `[RebusMessage]` contract with a streaming response is a generator error |

`null` result handling does not apply (an `IAsyncEnumerable<T>` is never null-mapped to 404); an empty
sequence is an empty array / an empty stream with an OK trailer.

## Steps

1. Core: detect `System.Collections.Generic.IAsyncEnumerable<T>` as the response type in the shared
   endpoint-model analysis of both generators; carry `IsStreaming` + the element type on the model.
2. Minimal API generator: return the `IAsyncEnumerable<T>` from the endpoint lambda directly —
   ASP.NET Core writes it as a streamed JSON array through the configured `JsonSerializerOptions`.
   Declare `.Produces<IEnumerable<T>>(200)` so the OpenAPI schema stays an array of the element type,
   not an opaque async-enumerable schema.
   Docs: [Minimal API return values](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses#return-values),
   [`JsonSerializer` async streaming](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/how-to#serialize-to-utf-8).
3. MessagePack path (`ArkMessagePackEx`): buffer with `await foreach` into a `List<T>` and serialize
   the list. Add a `ponytail:`-style comment naming the ceiling (unbounded buffer for very large
   sequences) and the upgrade path (a length-prefixed message stream under a distinct content type).
   Apply `MessagePackSecurity.UntrustedData` rules unchanged on the request side.
4. Add a per-endpoint `MaxStreamedItems` guard on `HttpEndpointAttribute` (zero = unlimited) used by
   the MessagePack buffering path to fail fast with a 500 ProblemDetails instead of exhausting memory.
5. gRPC generator: emit the method as server-streaming (`IAsyncEnumerable<T>` return on the
   `[ServiceContract]` method — `protobuf-net.Grpc` maps it to a server-streaming rpc) and emit
   `returns (stream …)` in the exported `.proto`.
   Docs: [protobuf-net.Grpc streaming](https://protobuf-net.github.io/protobuf-net.Grpc/gettingstarted),
   [gRPC server streaming](https://learn.microsoft.com/aspnet/core/grpc/basics#server-streaming-call).
6. Cancellation: pass the request `CancellationToken` into the handler call and enumerate with
   `WithCancellation(ctk)`, so client disconnect stops the producer on both transports.
7. Rebus generator: report a diagnostic (next free `ARKMF0xx`) for a `[RebusMessage]` contract with a
   streaming response.
8. Sample: add one streaming query exposed on HTTP + gRPC (e.g. a paged-free `GetGreetingsStream`),
   yielding items with an observable delay so streaming is testable.

## Test coverage (required)

- Generator snapshots: streaming contract emits the streaming Minimal API mapping with an array schema,
  a server-streaming gRPC method and a `stream` in the exported proto; non-streaming contracts unchanged.
- Rebus streaming diagnostic test.
- Behavioral HTTP test proving the response is **not** buffered: assert the first array element is
  readable before the producer has finished (e.g. read the response stream incrementally with
  `HttpCompletionOption.ResponseHeadersRead`, with a bounded timeout).
- Behavioral HTTP test asserting the JSON body is a plain array (no SSE `data:` framing, content type
  `application/json`).
- MessagePack test asserting the buffered array round-trips and that `MaxStreamedItems` overflow
  produces a ProblemDetails 500.
- gRPC test consuming the server stream through `Ark.MediatorFramework.Sample.GrpcClient` (generated
  from the exported proto) and asserting incremental delivery and cancellation.
- Empty-sequence test on all three wires.

## Outcomes

- Handlers can yield results incrementally; HTTP JSON and gRPC stream them end-to-end, MessagePack
  buffers deliberately with a documented ceiling and a hard item limit, and Rebus rejects the shape at
  compile time.

## Acceptance

- [ ] `IAsyncEnumerable<T>` responses generate streaming Minimal API and server-streaming gRPC paths.
- [ ] Incremental delivery proven by tests on both HTTP JSON and gRPC (not just correct content).
- [ ] No Server-Sent Events framing is introduced.
- [ ] MessagePack buffering documented in code + `design.md`, bounded by `MaxStreamedItems`.
- [ ] Cancellation reaches the handler when the client disconnects (tested).
- [ ] Rebus + streaming response reports a documented diagnostic.
- [ ] OpenAPI shows an array-of-element schema for streaming operations.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
