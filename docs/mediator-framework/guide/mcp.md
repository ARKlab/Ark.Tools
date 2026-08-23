# MCP tools

MCP exposure is an explicit host concern. Contracts and handlers stay free of
the MCP SDK; the generated bridge selects marked contracts, creates typed
wrappers, and dispatches through the registered mediator processors.

## Mark a contract

```csharp
/// <summary>Searches the book catalogue.</summary>
/// <remarks>Returns matching books ordered by relevance.</remarks>
[McpTool(Name = "books.search")]
public sealed record SearchBooksQuery : IQuery<IReadOnlyList<BookSummary>>
{
    /// <summary>Free-text terms to search for.</summary>
    public string? Text { get; init; }

    /// <summary>Maximum number of results.</summary>
    public int Limit { get; init; } = 20;
}
```

The contract summary and remarks become the concatenated tool description. The
tool title is left unset by default. A property summary becomes the description
of that MCP argument.
HTTP placement attributes such as `[HttpRoute]`, `[HttpQuery]`, and `[HttpBody]`
are ignored: every public bindable property is an MCP argument.

## Compose the host

Declare a partial context in the host project and select the contract assembly:

```csharp
[ArkGenerateMcpToolsForAssembly(typeof(CatalogContractMarker))]
public partial class McpHostContext
{
}
```

Register the generated surface and map one endpoint per API version:

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    .WithArkMcpTools<McpHostContext>();

app.MapMcp("/mcp/{version}").RequireAuthorization();
```

`MapMcp` remains host-owned. Configure authentication, authorization,
`AllowedHosts`, CORS, rate limits, request limits, and the SimpleInjector scope
in the host. A request to `/mcp/1` exposes contracts active in version 1, and
the same endpoint pattern can expose each subsequent version. The generator
emits the per-version tool lists, so filtering does not inspect contract
metadata at runtime. Use stateful
sessions only when the application needs server-to-client MCP requests.

## Attachments

Uploads use the bounded JSON shape `{ name, mimeType, blob }`, where `blob` is
base64. The generated wrapper converts it to `IArkAttachment` and applies the
same request-size, file-count, and MIME policy used by the framework's existing
attachment metadata. Client-supplied paths and URIs are never dereferenced.

An attachment returned by a query or request is emitted as an MCP
`EmbeddedResourceBlock` containing `BlobResourceContents` with an opaque URI,
sanitized name, MIME type, and base64 blob. Downloads are bounded before the
stream is materialized; large files should use a separately authorized
resource-link/download service.

## Errors and tests

Generated wrappers preserve cancellation and existing MCP protocol exceptions.
For mediator failures, the wrapper maps the exception through the shared Ark
`ProblemDetails` mapper and returns an error result
(`CallToolResult.IsError = true`) with `{Title}: {Detail}` text and structured
ProblemDetails, so clients can read `type`, `title`, `status`, `detail`, and
validation or business-rule extensions. Unexpected failures use generic text;
stack traces, connection strings, and sensitive exception messages are never
returned.

The reference implementation is demonstrated in
[`samples/Ark.MediatorFramework.Sample`](../../../samples/Ark.MediatorFramework.Sample/README.md).
Its release gate must list and call generated query, mutation, upload, and
download tools through an ASP.NET Core test host and the official SDK client.
