# Design: source-generated MCP tools

Status: **proposed**. This document defines the generator and package boundary for
exposing Ark.Tools mediator contracts as tools in an ASP.NET Core Model Context
Protocol (MCP) server. It is a design baseline, not an implementation commitment.

## Goals

- Expose an existing `Ark.Tools.Solid` request, query, or command as one MCP
  tool without changing its handler.
- Keep contracts transport-neutral. Application assemblies must not reference
  ASP.NET Core or the MCP SDK.
- Generate deterministic tool registration and typed dispatch at compile time.
- Use the official MCP SDK's native ASP.NET Core transport rather than
  reimplementing Streamable HTTP, session handling, JSON-RPC, or protocol
  negotiation.
- Preserve SimpleInjector as the handler container and the existing decorator
  pipeline.
- Make the host responsible for authentication, authorization policy,
  middleware, endpoint routing, and MCP session mode.
- Keep the design compatible with trimming and future Native AOT work.

## Non-goals

- Replacing `ModelContextProtocol.AspNetCore` or implementing an MCP transport.
- Mapping an MCP endpoint from generated code.
- Generating MCP resources or prompts in the first iteration.
- Making every mediator contract an MCP tool implicitly.
- Adding MCP concepts such as `McpServer`, `HttpContext`, or
  `RequestContext<T>` to application handlers.
- Claiming that the SDK's internal tool adapter is reflection-free. The Ark
  generator removes Ark contract discovery and handler dispatch reflection; the
  SDK remains responsible for adapting a `Delegate` to an MCP tool.

## MCP SDK baseline

Research snapshot: **2026-08-23**, against the official
[ModelContextProtocol/csharp-sdk](https://github.com/modelcontextprotocol/csharp-sdk)
repository and its current `main` branch. The current stable package identified
for HTTP hosting is `ModelContextProtocol.AspNetCore` **2.2.0**. The implementation
must pin the selected stable version centrally and update the version and
lockfiles together if a newer stable release is selected.

The SDK surface that this design relies on is:

| SDK surface | Design use |
| --- | --- |
| `ModelContextProtocol.AspNetCore` | HTTP transport package; references the core server package. |
| `AddMcpServer()` | Host service registration; returns `IMcpServerBuilder`. |
| `WithHttpTransport(...)` | Host registration for Streamable HTTP and session options. |
| `WithTools(IEnumerable<McpServerTool>)` | Generated explicit tool registration; avoids assembly-wide tool scanning. |
| `McpServerTool.Create(Delegate, McpServerToolCreateOptions?)` | Generated bridge from a typed wrapper delegate to the SDK tool adapter. |
| `McpServerToolCreateOptions` | Generated tool name, description, title, structured-content, and output-schema metadata. |
| `MapMcp(pattern)` | Host-owned ASP.NET Core endpoint mapping. |
| `WithRequestFilters(...)` / `AddCallToolFilter(...)` | Host/application composition point for MCP request policy and error handling. |
| `AddAuthorizationFilters()` | Host opt-in for SDK support for ASP.NET Core authorization attributes on MCP primitives. |

The SDK's attribute discovery path (`[McpServerToolType]`,
`[McpServerTool]`, and `WithToolsFromAssembly()`) is valid native SDK usage, but
is not the default for Ark-generated tools. It scans assemblies and reconstructs
tool metadata at runtime. The generated path supplies an explicit
`IEnumerable<McpServerTool>` so selected contracts, names, schemas, and dispatch
are deterministic.

The SDK's current HTTP integration maps Streamable HTTP at the route supplied to
`MapMcp`. Stateless mode is recommended when tools do not need
server-to-client requests. Stateful mode is required for capabilities such as
server-initiated sampling or elicitation. Legacy SSE is an explicit compatibility
option and is not generated or enabled by Ark.

## Layering

```text
+--------------------------------------------------------------+
| Pure Ark.Tools.Solid contracts and handlers                  |
| IRequest<T> | IQuery<T> | ICommand                           |
| No MCP, ASP.NET Core, or transport context                   |
+--------------------------------------------------------------+
                              ^
                              | SimpleInjector/decorators
+--------------------------------------------------------------+
| Generated MCP bridge                                         |
| Contract metadata | typed wrapper delegates | tool registry  |
+--------------------------------------------------------------+
                              ^
                              | official SDK types
+--------------------------------------------------------------+
| ModelContextProtocol.AspNetCore                              |
| AddMcpServer | WithHttpTransport | MapMcp | JSON-RPC/HTTP     |
+--------------------------------------------------------------+
                              ^
+--------------------------------------------------------------+
| Host composition                                              |
| authentication | authorization | middleware | route/policy    |
+--------------------------------------------------------------+
```

The generated bridge is not an HTTP endpoint. MCP clients call the single
Streamable HTTP endpoint mapped by the host; the SDK routes each `tools/call`
request to the generated tool with the matching name.

## Package shape

Add two packages, following the existing Minimal API and gRPC split:

| Project | Target | Responsibility |
| --- | --- | --- |
| `Ark.Tools.MediatorFramework.Mcp` | `net10.0` | MCP contract metadata, generated-registration runtime helpers, SDK references, and package build-transitive analyzer wiring. |
| `Ark.Tools.MediatorFramework.Mcp.Generators` | `netstandard2.0` | Roslyn incremental discovery, validation, and generated tool bridge. |

The runtime package references `Ark.Tools.MediatorFramework`,
`Ark.Tools.Solid`, `SimpleInjector`, and the official MCP packages required by
the generated source. It does not reference an alternate MCP implementation,
Minimal API endpoint mapping, or a second DI abstraction.

The generator is packed under `analyzers/dotnet/cs`, as with the existing
transport packages. The application receives the generator transitively from a
single `PackageReference` to `Ark.Tools.MediatorFramework.Mcp`.

## Contract model

MCP exposure is explicit and independent from HTTP, gRPC, and Rebus exposure.
Add a transport-neutral `McpToolAttribute` to
`Ark.Tools.MediatorFramework`:

```csharp
[McpTool(
    Name = "books.search",
    Description = "Searches the book catalogue.",
    ReadOnly = true,
    Idempotent = true)]
public sealed record SearchBooksQuery : IQuery<IReadOnlyList<BookSummary>>
{
    public string? Text { get; init; }
    public int Limit { get; init; } = 20;
}
```

The attribute is metadata only. It does not reference `McpServerToolAttribute`
or `McpServerToolTypeAttribute`.

### Metadata

The first version supports these fields:

| `McpToolAttribute` field | Meaning |
| --- | --- |
| `Name` | Optional stable MCP tool name; defaults to a normalized contract name. |
| `Description` | Optional model-facing description; otherwise use contract XML documentation when available. |
| `Title` | Optional human-readable display title. |
| `ReadOnly` | MCP tool annotation; defaults conservatively to `false`. |
| `Destructive` | MCP tool annotation; defaults conservatively to `true` for mutating tools. |
| `Idempotent` | MCP tool annotation; defaults to `false`. |
| `OpenWorld` | MCP tool annotation; defaults to `true`. |
| `UseStructuredContent` | Requests an output schema and structured result content. |

The generator validates names against MCP tool-name rules and reports duplicate
names across the selected surface. Names are stable API identifiers: changing
one is a breaking MCP surface change even if the C# contract name is unchanged.

`UseStructuredContent` is recommended for request/query responses. It causes the
generated `McpServerToolCreateOptions` to advertise the response type as the
output schema. Commands use an empty successful result unless an explicit
response contract is introduced in a later design.

### Supported handler kinds

- `IQuery<TResponse>` maps to a tool returning `Task<TResponse>`.
- `IRequest<TResponse>` maps to a tool returning `Task<TResponse>`.
- `ICommand` maps to a tool returning `Task`.

The generator uses the same closed generic handler interfaces and registration
checks as the other mediator transport generators. Unsupported or ambiguous
handler shapes are compile-time errors; the contract is not emitted.

### Input shape

The generated delegate exposes the public input properties of the contract as
tool arguments. This keeps the MCP input schema natural for model callers:

```json
{
  "text": "distributed systems",
  "limit": 10
}
```

The wrapper constructs the contract using its accessible `init`/`set`
properties, then invokes the typed handler. `ServerSet`, write-only, indexer,
pointer, ref, and unsupported member shapes are rejected rather than silently
omitted. Nullable members and members with defaults remain optional according to
the generated JSON schema.

The first version does not map HTTP-only binding metadata such as
`HttpRoute`, `HttpQuery`, `HttpBody`, multipart attachments, or ETags. Those
members are either ordinary MCP JSON properties or explicitly rejected when
their semantics cannot be represented safely. MCP input is JSON and is not
treated as an HTTP envelope.

## Host selection and generated API

Generation is opt-in and selects referenced contract assemblies through the same
marker pattern used by the other transports:

```csharp
[ArkGenerateMcpToolsForAssembly(typeof(CatalogContractMarker))]
public partial class McpHostContext
{
}
```

The host opts into the generated registry:

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithArkMcpToolsFromAssembly<McpHostContext>();
```

`ArkGenerateMcpToolsForAssemblyAttribute` is applied to a partial context and
accepts a marker type declared by each contract assembly. Multiple markers
compose one generated surface. A contract without `[McpTool]` is not exposed.

The generated extension is an `IMcpServerBuilder` extension named
`WithArkMcpToolsFromAssembly<TContext>`. It is emitted only when the invocation
and marker are present, so a package reference alone does not change a host's
surface. A second overload for direct current-assembly generation may be added
only if an implementation need is demonstrated; assembly marker selection is
the default because it is explicit and works for contracts in referenced
projects.

## Generated artifacts

For each selected valid contract, the generator emits:

1. A private static wrapper delegate with the contract's typed input arguments,
   `IServiceProvider`, and `CancellationToken`.
2. Typed contract construction and handler resolution through the existing
   SimpleInjector container.
3. One `McpServerTool` instance created with `McpServerTool.Create`.
4. A deterministic registry passed to the SDK's
   `WithTools(IEnumerable<McpServerTool>)`.
5. A public builder extension that registers the complete registry exactly once.

Conceptually, generated source has this shape (names and argument details are
illustrative):

```csharp
public static IMcpServerBuilder WithArkMcpToolsFromAssembly<TContext>(
    this IMcpServerBuilder builder)
{
    return builder.WithTools(
    [
        McpServerTool.Create(
            (Func<string?, int, IServiceProvider, CancellationToken,
                Task<IReadOnlyList<BookSummary>>>)InvokeSearchBooks,
            new McpServerToolCreateOptions
            {
                Name = "books.search",
                Description = "Searches the book catalogue.",
                UseStructuredContent = true,
                OutputSchemaType = typeof(IReadOnlyList<BookSummary>)
            })
    ]);
}
```

The wrapper itself is explicit:

```csharp
private static async Task<IReadOnlyList<BookSummary>> InvokeSearchBooks(
    string? text,
    int limit,
    IServiceProvider services,
    CancellationToken cancellationToken)
{
    var request = new SearchBooksQuery
    {
        Text = text,
        Limit = limit
    };
    var container = services.GetRequiredService<SimpleInjector.Container>();
    var handler = container.GetInstance<IQueryHandler<SearchBooksQuery,
        IReadOnlyList<BookSummary>>>();
    return await handler.ExecuteAsync(request, cancellationToken)
        .ConfigureAwait(false);
}
```

The final emitted code must use fully qualified names where needed and preserve
the repository's generated-code conventions. It must not emit
`[McpServerToolType]`, `WithToolsFromAssembly()`, or an assembly reflection scan.
The explicit SDK registry also prevents duplicate registration when another
application tool assembly is registered separately by the host.

## Incremental generator pipeline

The generator follows the existing `IIncrementalGenerator` pattern:

1. Discover invocations of `WithArkMcpToolsFromAssembly<TContext>`.
2. Resolve each `ArkGenerateMcpToolsForAssemblyAttribute` marker to an assembly
   name.
3. Discover `[McpTool]` contracts in the current and selected referenced
   assemblies.
4. Resolve `IRequest<T>`, `IQuery<T>`, or `ICommand` and their exact typed
   handler interfaces.
5. Extract properties, descriptions, nullability, defaults, and MCP metadata.
6. Validate names, shapes, handler registrations, and duplicate tools.
7. Emit one deterministic registry and wrapper set.

The selected assembly set is part of the incremental input. Unselected
assemblies do not produce diagnostics for their MCP-capable contracts and do not
affect generated output.

## Dispatch and lifetime semantics

The MCP SDK supplies `IServiceProvider` and the operation cancellation token to
the generated delegate. The wrapper resolves the SimpleInjector `Container`
from the host service provider and obtains the exact closed handler type. The
host must establish the SimpleInjector async scope before MCP dispatch, using the
same composition boundary as the Minimal API host.

The generator does not use `IRequestProcessor` or `IQueryProcessor`; those
dynamic processors are the reflection path this transport is designed to avoid.
Decorators, validation, authorization decorators, user context providers, and
telemetry remain in the existing handler graph.

`CancellationToken` is never exposed as a tool argument. Cancellation from the
MCP client must reach the handler and stop downstream work. No generated wrapper
swallows `OperationCanceledException`.

## Results and errors

MCP has no HTTP status-code result for a tool call. The SDK represents ordinary
tool failures as `CallToolResult.IsError = true`; protocol failures remain JSON-RPC
errors. The generated wrapper therefore:

- returns typed query/request values for the SDK to serialize;
- returns no content for a successful command;
- does not translate domain exceptions into HTTP status codes;
- does not expose exception messages unless the host's approved MCP error
  policy permits them;
- lets cancellation propagate as cancellation;
- leaves `McpProtocolException` behavior to the SDK.

The host or shared runtime may add an SDK `CallTool` request filter through
`WithRequestFilters` to map validation and business exceptions to safe tool
error content. This filter is composition, not generated per-tool code.
Unhandled exceptions must be logged with structured NLog integration and return
generic client-safe text. Sensitive exception details, connection strings, and
stack traces must not enter tool results.

## ASP.NET Core host boundary

The generator does not call `MapMcp`, configure Kestrel, or add middleware. A
host owns the complete native integration:

```csharp
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp("/mcp")
    .RequireAuthorization();

app.Run();
```

Host responsibilities include:

- calling `AddMcpServer().WithHttpTransport(...)`;
- choosing stateless or stateful sessions;
- calling the generated `WithArkMcpToolsFromAssembly<TContext>()`;
- mapping `MapMcp("/mcp")` at the desired route;
- applying authentication and authorization to the MCP endpoint;
- calling `AddAuthorizationFilters()` when SDK per-tool authorization
  attributes are intentionally used;
- configuring `AllowedHosts` and restrictive CORS when browser clients are
  allowed;
- establishing the SimpleInjector scope and registering the
  `IContextProvider<ClaimsPrincipal>` implementation;
- composing NLog, OpenTelemetry, ProblemDetails-adjacent logging, rate limits,
  request-size limits, and health checks;
- deciding whether legacy SSE compatibility is required.

`MapMcp` returns an endpoint convention builder. The host may apply
`RequireAuthorization`, CORS, rate limiting, display metadata, and other
policies there. Generated tool metadata is not a substitute for endpoint
authentication. A public MCP endpoint is an explicit host decision.

The recommended baseline is Streamable HTTP in stateless mode over HTTPS,
restricted host names, and explicit authorization. Stateful mode is selected
only when a tool or MCP capability requires server-to-client requests or
session state. Legacy SSE is disabled by default because the SDK documents its
weaker backpressure behavior.

## Security and trust boundary

MCP tool arguments are untrusted model/client input. The generated schema helps
clients form valid calls but does not replace handler validation. Existing
FluentValidation and authorization decorators remain authoritative.

The implementation must:

- reject invalid tool names, duplicate tools, and unsupported contract shapes at
  compile time;
- avoid mass assignment by constructing only declared contract members;
- never bind server-owned values from MCP input;
- keep authentication at the ASP.NET Core endpoint and policy checks in the
  application decorator/filter pipeline;
- use restrictive `AllowedHosts` values to prevent DNS rebinding;
- require an explicit, narrow CORS policy for browser-based clients;
- avoid returning internal exception details;
- avoid logging complete tool arguments when they may contain secrets or
  personal data;
- preserve cancellation and request limits configured by the host.

Icons, MCP Apps metadata, Tasks, resources, prompts, and custom protocol
capabilities are not generated in the first version. They can be added as
separate opt-in features without changing the contract-to-handler dispatch
boundary.

## Diagnostics

Reserve a new MCP diagnostic range rather than reusing HTTP, gRPC, or Rebus IDs.
The initial set should include:

| Diagnostic | Severity | Condition |
| --- | --- | --- |
| `ARKMF030` | Error | Invalid MCP tool name. |
| `ARKMF031` | Error | Duplicate MCP tool name in one generated surface. |
| `ARKMF032` | Error | Contract is not a supported request, query, or command. |
| `ARKMF033` | Error | MCP contract has an unsupported input member shape. |
| `ARKMF034` | Error | No exact handler registration can be resolved. |
| `ARKMF035` | Error | MCP metadata is contradictory or invalid. |
| `ARKMF036` | Error | Marker context is not partial or assembly selection is invalid. |
| `ARKMF037` | Warning | Tool has no description or XML documentation. |
| `ARKMF038` | Error | MCP name collides with a generated tool from another marker. |

Diagnostics point to the `[McpTool]` or marker attribute location. Invalid
contracts are omitted from the registry; valid independent tools continue to
generate.

## Versioning and compatibility

MCP tool names are not HTTP routes and do not inherit `{version}` expansion from
`HttpEndpointAttribute`. One `[McpTool]` declaration produces one tool name.
Breaking input or output changes require a new explicit tool name, such as
`books.search.v2`, while the old declaration may remain during migration.

`VersioningAttribute`, `HttpRouteAttribute`, and HTTP status metadata do not
control MCP tool exposure. If a future requirement needs version negotiation,
the design should add an explicit MCP name/alias model rather than silently
creating multiple tools with ambiguous schemas.

## Testing and release gates

Implementation must add generator snapshot coverage for:

- same-assembly and referenced-assembly marker selection;
- deterministic names and descriptions;
- query, request, and command wrappers;
- nullable and defaulted input members;
- structured output schema metadata;
- duplicate names and invalid names;
- unsupported properties and handler kinds;
- missing handler registrations;
- multiple marker composition without duplicate tools;
- no output when no marker or `[McpTool]` is present.

Runtime/integration coverage must use the official SDK and an ASP.NET Core test
host to verify:

- `ListTools` exposes the generated names and input schemas;
- `CallTool` dispatches through the exact SimpleInjector handler;
- decorators and cancellation are preserved;
- structured results are emitted for query/request tools;
- command calls return a successful empty result;
- validation and domain failures become safe tool errors;
- endpoint authorization and host policies remain host-owned;
- stateless and stateful SDK transport setup are not conflated;
- generated registration does not require `WithToolsFromAssembly`.

Release gates are `dotnet build`, `dotnet test`, generated-source inspection
under `obj/.../generated`, and API-surface review for every new public
attribute/helper. The selected MCP package version and all affected lockfiles
must be included in the same dependency change.

## Decisions required before implementation

1. Confirm the stable `ModelContextProtocol.AspNetCore` version and target
   framework support at implementation time.
2. Confirm whether the first release supports only property-based inputs or also
   a single contract-object argument for immutable constructor-only records.
3. Decide whether missing descriptions are warnings or errors for production
   packages.
4. Decide whether structured output is the default for all non-command tools or
   an explicit opt-in.
5. Provide a host integration sample before adding the package to the default
   solution build.

## References

- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [SDK getting started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [SDK tools](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [SDK transports](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md)
- [MCP specification: tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [Microsoft Learn: .NET AI and MCP](https://learn.microsoft.com/dotnet/ai/get-started-mcp)
