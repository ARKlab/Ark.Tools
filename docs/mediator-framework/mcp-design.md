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
- Support transport-neutral attachment uploads and downloads without exposing
  HTTP multipart or server file-system details to handlers.
- Make the host responsible for authentication, authorization policy,
  middleware, endpoint routing, and MCP session mode.
- Keep the design compatible with trimming and future Native AOT work.

## Non-goals

- Replacing `ModelContextProtocol.AspNetCore` or implementing an MCP transport.
- Mapping an MCP endpoint from generated code.
- Generating standalone MCP resources or prompts in the first iteration.
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
lockfiles together if a newer stable release is selected. The 2.2.0 API used
below was confirmed against a `net10.0` host; the first Ark package is therefore
`net10.0` only. The SDK's 2.2.0 ASP.NET Core project itself targets
`net10.0;net9.0;net8.0`.

The SDK surface that this design relies on is:

| SDK surface | Design use |
| --- | --- |
| `ModelContextProtocol.AspNetCore` | HTTP transport package; references the core server package. |
| `AddMcpServer()` | Host service registration; returns `IMcpServerBuilder`. |
| `WithHttpTransport(...)` | Host registration for Streamable HTTP and session options. |
| `WithTools<TToolType>()` | Generated explicit registration of an attributed tool type using the SDK's generic, AOT-friendly path. |
| `McpServerToolTypeAttribute` / `McpServerToolAttribute` | Generated tool classes and methods, including tool metadata and structured-content behavior. |
| `DescriptionAttribute` | Generated method and parameter descriptions used for tool and input-schema documentation. |
| `EmbeddedResourceBlock` / `BlobResourceContents` | Download result content for binary attachments; the SDK serializes the blob as base64 with a URI and MIME type. |
| `MapMcp(pattern)` | Host-owned ASP.NET Core endpoint mapping. |
| `WithRequestFilters(...)` / `AddCallToolFilter(...)` | Host/application composition point for MCP request policy and error handling. |
| `AddAuthorizationFilters()` | Host opt-in for SDK support for ASP.NET Core authorization attributes on MCP primitives. |

The generated path uses the SDK's generic `WithTools<TToolType>()` registration
for each generated tool type. It avoids assembly-wide scanning while preserving
the official `[McpServerToolType]` and `[McpServerTool]` metadata model.

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
/// <summary>Searches the book catalogue.</summary>
/// <remarks>Returns matching books ordered by relevance.</remarks>
[McpTool(
    Name = "books.search",
    ReadOnly = true,
    Idempotent = true)]
public sealed record SearchBooksQuery : IQuery<IReadOnlyList<BookSummary>>
{
    /// <summary>Free-text terms to search for.</summary>
    public string? Text { get; init; }

    /// <summary>Maximum number of results.</summary>
    public int Limit { get; init; } = 20;
}
```

The attribute is metadata only. It does not reference `McpServerToolAttribute`
or `McpServerToolTypeAttribute`.

### Metadata

The first version supports these fields:

| `McpToolAttribute` field | Meaning |
| --- | --- |
| `Name` | Optional stable MCP tool name; defaults to a normalized contract name. `[ApiGroup("group")]` prefixes the resolved name as `group.name`. Versioning selects the tool on the corresponding MCP route without changing its name. |
| `ReadOnly` | MCP tool annotation; defaults to `true` for `IQuery<T>` and `false` for `IRequest<T>`/`ICommand`. |
| `Destructive` | MCP tool annotation; defaults to `false` for `IQuery<T>` and `true` for `IRequest<T>`/`ICommand`. |
| `Idempotent` | MCP tool annotation; defaults to `false`. |
| `OpenWorld` | MCP tool annotation; defaults to `true`. |

The generator validates names against MCP tool-name rules and reports duplicate
names across the selected surface. Names are stable API identifiers: changing
one is a breaking MCP surface change even if the C# contract name is unchanged.

`UseStructuredContent` is not an `McpToolAttribute` option. Every generated tool
sets `McpServerToolAttribute.UseStructuredContent = true`; the SDK infers the
output schema from the generated method return type. Commands still use an empty
successful result.

### XML documentation mapping

The generator reads documentation comments from the Roslyn symbols in the
selected contract assemblies. XML documentation is not loaded from files at
runtime and does not require reflection over an XML file. The generator uses
the XML documentation directly:

| Contract symbol | XML element | Generated MCP metadata |
| --- | --- | --- |
| Contract type | `<summary>` | `McpServerToolAttribute.Title`. |
| Contract type | `<remarks>` | `System.ComponentModel.DescriptionAttribute` on the generated tool method. |
| Input property | `<summary>` | `System.ComponentModel.DescriptionAttribute` on the generated method parameter, which the SDK copies to that input-schema property. |

Property `<remarks>` elements are not copied to parameter descriptions. A
missing type summary or remarks leaves that metadata unset and produces
`ARKMF037`; a missing property summary leaves the parameter description unset
without changing its binding. The generated `[Description]` attributes are required because the official SDK
uses them when it creates the tool metadata and input schema from the method.

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
properties, then invokes the typed handler. HTTP binding attributes do not change
this shape: `[HttpRoute]`, `[HttpQuery]`, and `[HttpBody]` are ignored for MCP,
and every public bindable contract property is an MCP argument. They are not
interpreted as route, query-string, or body locations. `ServerSet`, write-only,
indexer, pointer, ref, and unsupported member shapes remain rejected rather than
silently omitted because server-owned values must not be supplied by a caller.
Nullable members and members with defaults remain optional according to the
generated JSON schema. An `IArkAttachment` property is the one exception to
ordinary JSON member binding and is described in [Attachment mapping](#attachment-mapping).

The first version does not interpret HTTP-only binding metadata such as
`HttpRoute`, `HttpQuery`, or `HttpBody`; those annotations do not cause
properties to be dropped or moved into an HTTP envelope. ETags are also ordinary
MCP JSON properties. MCP input is JSON and every public bindable property is
handled uniformly, subject only to the server-owned and unsupported-shape rules
above.

### Attachment mapping

`IArkAttachment` remains the handler-facing abstraction. MCP has no standardized
binary upload parameter for `tools/call`; its `arguments` value is JSON. The
generated bridge therefore exposes an attachment input as a bounded
`McpAttachmentInput` object supplied by `Ark.Tools.MediatorFramework.Mcp`:

```json
{
  "attachment": {
    "name": "report.pdf",
    "mimeType": "application/pdf",
    "blob": "<base64-encoded bytes>"
  }
}
```

`McpAttachmentInput` accepts an opaque file name, MIME type, and base64 `blob`.
The generated wrapper validates and converts it to `ArkAttachment` before
assigning the contract property. A collection property accepts an array of the
same objects. The bridge does not dereference client-supplied URIs, paths, or
network locations; a host or application may define a separate trusted upload
store when inline base64 is unsuitable. Upload size, decoded-size, file-count,
MIME allow-list, and request-body limits are enforced before handler dispatch.
Names are sanitized with the existing `ArkAttachmentName` rules.
The limits use the same `MaxRequestBodySizeBytes`, `MaxFileCount`, and
`AllowedContentTypes` policy shape as the existing attachment endpoint metadata.
That policy is defined in the transport-neutral `Ark.Tools.MediatorFramework`
layer; Minimal API and MCP generators consume it rather than defining
transport-specific limits.

For a top-level `IArkAttachment` response, the generated wrapper reads the
attachment stream within the configured download limit and returns an
`EmbeddedResourceBlock` containing `BlobResourceContents`. A collection response
returns one embedded resource content block per attachment. Each resource has
an opaque `ark://` URI containing a generated attachment identifier and the
sanitized file name, plus the attachment MIME type. The URI is an identifier,
not a server file path or an implicit download endpoint.

The official SDK serializes binary embedded resources as the MCP resource shape
(`uri`, `mimeType`, and base64 `blob`). It does not convert PDFs or other binary
formats to images; rendering is a client decision. The MCP specification allows
embedded resources in tool results and recommends that servers using them
implement the resources capability, but this design does not generate standalone
`resources/list` or `resources/read` handlers. Large downloads should use a
host-owned resource-link or download service instead of unbounded embedding.

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
    .WithArkMcpTools<McpHostContext>();
```

`ArkGenerateMcpToolsForAssemblyAttribute` is applied to a partial context and
accepts a marker type declared by each contract assembly. Multiple markers
compose one generated surface. A contract without `[McpTool]` is not exposed.

The generated extension is an `IMcpServerBuilder` extension named
`WithArkMcpTools<TContext>`. It registers every generated MCP tool belonging to
the selected `TContext`; there is no `FromAssembly` suffix because the context
is the registration boundary. It is emitted only when the invocation and marker
are present, so a package reference alone does not change a host's surface.
Assembly marker selection remains explicit and works for contracts in
referenced projects.

The generator makes the decorated context implement
`IMcpToolContext`, whose public `RegisterMcpTools` method is the generated
registration sequence. `WithArkMcpTools<TContext>` requires
`where TContext : IMcpToolContext` and invokes its static abstract
`RegisterMcpTools` method. No context instance, runtime method lookup,
`MethodInfo`, or reflection-based fallback is used; a context that is not
generated fails at compile time.

## Generated artifacts

For each selected valid contract, the generator emits nested artifacts inside
the decorated partial context:

1. A generated nested tool type marked `[McpServerToolType]`, with a static
   method marked `[McpServerTool]`.
2. Generated `[System.ComponentModel.Description]` attributes on the tool method
   parameters whose values come from property `<summary>` documentation.
3. A method-level `[Description]` from the contract `<remarks>` documentation.
4. Typed contract construction and dispatch through the appropriate
   `IQueryProcessor`, `IRequestProcessor`, or `ICommandProcessor`.
5. A generated `RegisterMcpTools` method on the context that chains one
   `.WithTools<ToolType>()` call per tool in deterministic order.
6. `IMcpToolContext` on the context, allowing the public
   `WithArkMcpTools<TContext>` builder extension to invoke the registration
   method without reflection.
7. Error boundaries that preserve cancellation and protocol exceptions, map
   mediator failures to `CallToolResult.IsError = true` with safe text and
   shared ProblemDetails structured content, and return a generic message for
   unexpected failures. Mapped 5xx failures are logged as exceptions with
   structured NLog data; expected 4xx failures are not logged.

Conceptually, generated source has this shape (names and argument details are
illustrative):

```csharp
public partial class McpHostContext : IMcpToolContext
{
    public IMcpServerBuilder RegisterMcpTools(IMcpServerBuilder builder)
    {
        return builder
            .WithTools<SearchBooksTool>();
    }

    [McpServerToolType]
    public sealed class SearchBooksTool
    {
        [McpServerTool(
            Name = "books.search",
            ReadOnly = true,
            Destructive = false,
            Idempotent = true,
            OpenWorld = true,
            UseStructuredContent = true)]
        [Description("Searches the book catalogue. Returns matching books ordered by relevance.")]
        [Authorize]
        public static async Task<IReadOnlyList<BookSummary>> ExecuteAsync(
            [Description("Free-text terms to search for.")] string? text,
            [Description("Maximum number of results.")] int limit,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var request = new SearchBooksQuery
            {
                Text = text,
                Limit = limit
            };
            var processor = services
                .GetRequiredService<SimpleInjector.Container>()
                .GetInstance<IQueryProcessor>();
            return await processor
                .ExecuteAsync<IReadOnlyList<BookSummary>>(request, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
```

The method `Description` in the example is generated by concatenating the
contract `<summary>` and `<remarks>`, while parameter descriptions come from the
`SearchBooksQuery.Text` and `SearchBooksQuery.Limit` property summaries. The
final emitted code must use fully qualified names where needed and preserve the
repository's generated-code conventions. It does not use
`WithToolsFromAssembly()` or an assembly reflection scan. The context method and
explicit one-tool registrations also prevent duplicate registration when another
application tool assembly is registered separately by the host.

The generated wrapper catches `OperationCanceledException` only when the MCP
cancellation token is signaled, allowing cancellation to propagate. It
rethrows MCP protocol exceptions. Other failures pass through
`McpToolErrors`: a failure mapped by the shared HTTP ProblemDetails rules to a
4xx status is serialized as safe JSON in an MCP exception message without
exception logging, while unexpected 5xx failures are logged and use a generic
message.

## Incremental generator pipeline

The generator follows the existing `IIncrementalGenerator` pattern:

1. Discover invocations of `WithArkMcpTools<TContext>`.
2. Resolve each `ArkGenerateMcpToolsForAssemblyAttribute` marker to an assembly
   name.
3. Discover `[McpTool]` contracts in the current and selected referenced
   assemblies.
4. Resolve `IRequest<T>`, `IQuery<T>`, or `ICommand` and validate the
   corresponding processor registration.
5. Extract properties, XML descriptions, nullability, defaults, and MCP metadata.
6. Validate names, shapes, handler registrations, and duplicate tools.
7. Emit one deterministic registry and wrapper set.

The selected assembly set is part of the incremental input. Unselected
assemblies do not produce diagnostics for their MCP-capable contracts and do not
affect generated output.

## Dispatch and lifetime semantics

The MCP SDK supplies `IServiceProvider` and the operation cancellation token to
the generated static method. The wrapper resolves the SimpleInjector `Container`
from the host service provider and obtains the appropriate
`IRequestProcessor`, `IQueryProcessor`, or `ICommandProcessor`. It calls the
processor's closed generic `ExecuteAsync` overload where available, or its
ordinary dynamic overload for the standard `IRequest<T>`, `IQuery<T>`, and
`ICommand` contracts. The host must establish the SimpleInjector async scope
before MCP dispatch, using the same composition boundary as the other
transports.

The processors are the supported dynamic-dispatch path; generated code does not
resolve handler types directly. Decorators, validation, authorization
decorators, user context providers, and telemetry therefore remain in the
existing handler graph.

`CancellationToken` is never exposed as a tool argument. Cancellation from the
MCP client must reach the handler and stop downstream work. No generated wrapper
swallows `OperationCanceledException`.

## Results and errors

MCP has no HTTP status-code result for a tool call. The SDK represents ordinary
tool failures as `CallToolResult.IsError = true`; protocol failures remain JSON-RPC
errors. The generated wrapper therefore:

- returns the contract `TResult` for query/request tools so the SDK can
  serialize the declared result schema;
- maps a top-level `IArkAttachment` to an embedded binary resource and an
  attachment collection to multiple embedded resource blocks;
- returns no content for a successful command;
- does not translate domain exceptions into HTTP status codes;
- exposes the shared ProblemDetails title and detail for client-visible
  failures, while unexpected failures use generic safe text;
- lets cancellation propagate as cancellation;
- leaves `McpProtocolException` behavior to the SDK.

The host or shared runtime may add an SDK `CallTool` request filter through
`WithRequestFilters` to map validation and business exceptions to safe tool
error content. This filter is composition, not generated per-tool code.
Unhandled exceptions must be logged with structured NLog integration and return
generic client-safe text. Sensitive exception details, connection strings, and
stack traces must not enter tool results.

The error boundary is explicit:

| Failure | MCP result |
| --- | --- |
| Invalid JSON/schema or missing required argument | SDK validation/protocol error |
| `McpProtocolException` | JSON-RPC protocol error |
| `OperationCanceledException` after client cancellation | Cancellation; no fabricated tool result |
| Validation or known domain failure | `CallToolResult.IsError = true` with `{Title}: {Detail}` text and structured ProblemDetails |
| Unexpected exception | `CallToolResult.IsError = true` with generic safe text and structured ProblemDetails |

The recommended filter maps known validation and domain exceptions to stable,
client-safe text. It must not turn an authentication failure into a successful
tool result, and it must not expose exception messages unless the exception is
explicitly approved for MCP clients.

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
- calling the generated `WithArkMcpTools<TContext>()`;
- mapping `MapMcp("/mcp")` at the desired route;
- applying authentication and authorization to the MCP endpoint;
- calling `AddAuthorizationFilters()` when SDK per-tool authorization
  attributes are intentionally used;
- configuring `AllowedHosts` and restrictive CORS when browser clients are
  allowed;
- establishing the SimpleInjector scope and registering the
  `IContextProvider<ClaimsPrincipal>` implementation;
- composing NLog, OpenTelemetry, ProblemDetails-adjacent logging, rate limits,
  request-size limits, attachment upload/download limits, and health checks;
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
- preserve cancellation and request limits configured by the host;
- bound inline attachment uploads and embedded attachment downloads before
  materializing content;
- validate attachment MIME types and content independently of client metadata;
- never fetch a client-supplied attachment URI from the server.

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
| `ARKMF034` | Error | No compatible mediator processor registration can be resolved. |
| `ARKMF035` | Error | MCP metadata is contradictory or invalid. |
| `ARKMF036` | Error | Marker context is not partial or assembly selection is invalid. |
| `ARKMF037` | Warning | Tool has no description or XML documentation. |
| `ARKMF038` | Error | MCP name collides with a generated tool from another marker. |
| `ARKMF039` | Error | MCP attachment input or output has an unsupported shape. |

Diagnostics point to the `[McpTool]` or marker attribute location. Invalid
contracts are omitted from the registry; valid independent tools continue to
generate.

## Versioning and compatibility

MCP tool names are stable across hosted API versions. A host maps
`/mcp/v{version}`, and each session receives only tools whose contract lifetime
matches the route version:
`Introduced <= version` and (`Retired` is unset or `version < Retired`). The
generator emits one tool per contract and does not append a version suffix.
It also emits the complete tool-name list for each known version, so runtime
filtering uses generated lookups instead of reflecting over tool metadata.

Breaking input or output changes require a new contract and introduction
version, while the old declaration may remain during migration.
`HttpRouteAttribute` and HTTP status metadata do not control MCP tool exposure.

## Testing and release gates

Implementation must add generator snapshot coverage for:

- same-assembly and referenced-assembly marker selection;
- stable names, version-route filtering, and XML-derived descriptions;
- XML property summaries copied to generated parameter descriptions;
- query, request, and command wrappers;
- nullable and defaulted input members;
- structured output schema metadata;
- inline attachment upload conversion, size/MIME limits, and rejected URI input;
- single and collection attachment downloads as embedded text/binary resources;
- duplicate names and invalid names;
- unsupported properties and handler kinds;
- missing processor registrations;
- version-specific tool lists through the mapped MCP routes;
- multiple marker composition without duplicate tools;
- no output when no marker or `[McpTool]` is present.

Runtime/integration coverage must use the official SDK and an ASP.NET Core test
host to verify:

- `ListTools` exposes the generated names and input schemas;
- `CallTool` dispatches through the registered mediator processor;
- decorators and cancellation are preserved;
- structured results are emitted for query/request tools;
- attachment downloads preserve sanitized names, MIME types, and binary bytes;
- command calls return a successful empty result;
- validation and domain failures become safe tool errors;
- endpoint authorization and host policies remain host-owned;
- stateless and stateful SDK transport setup are not conflated;
- generated registration does not require `WithToolsFromAssembly`;
- the generated context method chains one registration per tool.

Release gates are `dotnet build`, `dotnet test`, generated-source inspection
under `obj/.../generated`, and API-surface review for every new public
attribute/helper. The selected MCP package version and all affected lockfiles
must be included in the same dependency change.

The mediator sample is a mandatory integration gate, not a documentation-only
example. Before the package is released, expand
`samples/Ark.MediatorFramework.Sample` so its WebInterface host:

- references the selected `ModelContextProtocol.AspNetCore` and Ark MCP package;
- registers `AddMcpServer().WithHttpTransport()` and the generated
  `WithArkMcpTools<SampleMcpHostContext>()`;
- maps an authenticated `MapMcp("/mcp")` endpoint;
- exposes at least one query, one mutating request/command, and the existing
  cover upload/download contracts;
- is exercised by an ASP.NET Core test host using the official SDK client,
  including tool listing, dispatch, structured output, error handling, and
  embedded binary download assertions.

The sample must remain transport-neutral in its API and application assemblies;
only the WebInterface host receives the MCP dependency.

## Decisions and alternatives

The following decisions close the design questions for the first implementation:

1. **SDK and target:** use `ModelContextProtocol.AspNetCore` 2.2.0 with a
   `net10.0` Ark MCP package. Upgrade only as a coordinated package and
   lockfile change.
2. **Input model:** generate one argument per public bindable contract property.
   HTTP binding metadata is ignored. Constructor-only immutable records are
   supported by generated constructor binding; a single contract-object argument
   is not generated.
3. **Descriptions:** missing contract descriptions produce `ARKMF037` as a
   warning. Explicit attribute metadata wins, then type `<summary>`/`<remarks>`;
   property `<summary>` supplies the parameter description.
4. **Attachments:** use the confirmed `{ name, mimeType, blob }` JSON upload
   shape, the shared attachment policy limits, and SDK embedded resource blocks
   for bounded downloads.
5. **Sample:** the mediator sample integration above must pass before the package
   is added to the default solution build.

Alternatives considered:

- **SDK assembly scanning** (`WithToolsFromAssembly`) is the smallest host
  change, but makes contract selection, metadata, and dispatch runtime
  reflection-based and cannot enforce Ark's marker boundary.
- **Hand-written SDK tools** work immediately, but duplicate wrappers and drift
  from mediator processors and contract versioning.
- **Ark source-generated tools** provide deterministic selection, typed
  processor dispatch, XML metadata, and a single explicit registration surface.

The recommendation is the source-generated Ark bridge. It uses the official SDK
for protocol and HTTP behavior, while keeping discovery, contract binding, and
SimpleInjector processor dispatch in Ark code.

## References

- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [SDK getting started](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/getting-started.md)
- [SDK tools](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md)
- [SDK embedded resources](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/tools/tools.md#embedded-resources)
- [SDK transports](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md)
- [MCP specification: tools](https://modelcontextprotocol.io/specification/2025-11-25/server/tools)
- [Microsoft Learn: .NET AI and MCP](https://learn.microsoft.com/dotnet/ai/get-started-mcp)
