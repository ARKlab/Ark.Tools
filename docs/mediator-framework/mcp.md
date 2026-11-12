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

The tool hints have defaults that you can override on `[McpTool]`:

| Hint | Default |
| --- | --- |
| `ReadOnly` | `true` for a query, otherwise `false`. |
| `Destructive` | `false` for a query, otherwise `true`, so that clients ask the user to approve each change. |
| `Idempotent` | `true` when the contract has `[HttpEndpoint]` with verb `DELETE` or `PUT`, otherwise `false`. |
| `Confirm` | `true` when the contract has `[HttpEndpoint]` with verb `DELETE`, otherwise `false`. See [Confirmation](#confirmation). |

When contracts live in another assembly, the generator reads their XML
documentation only from `AdditionalFiles`; it never reads files from disk. The
package copies the XML documentation of referenced projects to the intermediate
output as `<AssemblyName>.arkmcpdoc` and passes the copies as `AdditionalFiles`.
The MCP-only extension keeps the files away from generators that read every
`.xml` additional file, such as the ASP.NET Core OpenAPI XML comment generator.
Set `ArkMcpIncludePackageXmlDocs=true` to also pass the XML documentation of
referenced NuGet packages, or `ArkMcpIncludeReferencedXmlDocs=false` to turn
both off. The generator also reads `.xml` and `.arkmcpdoc` files you add to
`AdditionalFiles` yourself; per assembly, the first file by path wins.

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

app.MapMcp("/mcp/v{version}").RequireAuthorization();
```

Generated tools serialize arguments, input schemas and structured results with
the Minimal API JSON options (`Microsoft.AspNetCore.Http.Json.JsonOptions`), so
MCP returns the same shape as the HTTP endpoints. Configure both with
`ConfigureHttpJsonOptions`. The settings of the MCP SDK defaults
(`McpJsonUtilities.DefaultOptions`) are not used; only their resolver is kept, as
a fallback after yours, for the MCP protocol types.

`McpToolSerializerOptions.Get(services)` builds the options the tools use, once per
application: a read-only copy of the Minimal API options with the MCP protocol contracts,
`McpAttachmentInput` and the MCP error contracts appended after your own resolvers. Under Native AOT, or with
`JsonSerializerIsReflectionEnabledByDefault=false`, add your contract types to
the Minimal API resolver chain as you do for the HTTP endpoints; attachment tools
need nothing more.

`MapMcp` remains host-owned. Configure authentication, authorization,
`AllowedHosts`, CORS, rate limits, request limits, and the SimpleInjector scope
in the host. A request to `/mcp/v1` exposes contracts active in version 1, and
the same endpoint pattern can expose each subsequent version. The generator
emits the per-version tool lists, so filtering does not inspect contract
metadata at runtime. [Confirmation](#confirmation) works in stateless mode. Use
stateful sessions only when the application needs other server-to-client MCP
requests.

Endpoint-level `RequireAuthorization()` runs before tool selection. If it is
configured, anonymous clients cannot reach the MCP endpoint, including tools
generated with `[AllowAnonymous]`; omit the endpoint requirement when relying
on per-tool anonymous access.

## Confirmation

A tool with `Confirm = true` asks the user before it runs. The default is on
for contracts with an HTTP `DELETE` endpoint:

```csharp
/// <summary>Deletes a book.</summary>
[HttpEndpoint("DELETE", "/books/{id}")]
[McpTool(Name = "books.delete")]                 // Confirm and Idempotent are on
public sealed record DeleteBookCommand(BookId Id) : ICommand<DeleteBookCommand>;

[McpTool(Name = "books.send", Confirm = true)]   // opt in for any other contract
public sealed record SendBookCommand(BookId Id) : ICommand<SendBookCommand>;

[McpTool(Name = "books.archive", ConfirmMessage = "Archiviare il libro?")]   // your own question; turns Confirm on
public sealed record ArchiveBookCommand(BookId Id) : ICommand<ArchiveBookCommand>;
```

The default question is the English `Confirm <tool name>: <description>`. The
client shows the question to the user as it is: no model reads or translates
it. Set `ConfirmMessage` to write the question in the language of your
product.

The question uses MCP multi round-trip requests (protocol revision
`2026-07-28`), so it needs no session:

1. The first call returns "input required" before the handler runs.
2. The client shows the user the question and, on the next line, the call
   arguments as one JSON object. Only the arguments that the contract declares
   are shown, because the tool ignores the others. A value longer than 200
   characters is cut, and the message says how many characters are hidden. The
   JSON has readable text: accents and apostrophes are not escaped.
3. The client calls the tool again with the answer. If the user accepted, the
   handler runs. If not, the call fails with `Not confirmed: the user declined
   '<tool>'. Do not retry without a new user request.`

The handler and the contract do not change, and no transaction is open while
the user decides. The question comes before validation and before the
authorization policies of the handler, so a user can accept and the call can
then fail with a validation or authorization error. A declined call is
returned as a business-rule violation with status 409.

A client that does not support multi round-trip requests, or that does not
declare form-mode elicitation, cannot be asked. For such a client the tool
runs without confirmation, as the HTTP endpoint does.

> **Confirmation is a user-experience aid, not a security control.** The server
> never enforces it: a client can skip it, send an accepted answer on the first
> call, or change the arguments on the second call. Keep authorization in
> handlers and policies.

Confirmation is for one call. An approval that must survive the call, that
another person gives, or that must be audited is domain state: model it as a
command and a status query.

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
validation or business-rule extensions. The text also carries those extensions
as one JSON object on its last line, because many clients give the model only
the text of an error:

```text
Validation failed: Validation failed:
 -- Value: The value is invalid. Severity: Error
{"errors":{"Value":["The value is invalid."]}}
```

The error details are serialized with the same options as the tool results,
which are your Minimal API options (`ConfigureHttpJsonOptions`), always on one
line. Your converters, source-generated contexts, encoder and naming policies
thus apply. With a dictionary key policy, the keys of `errors` and of the
business-rule details follow it (`errors.value` with camel case), which can
differ from the HTTP ProblemDetails response of the same failure. Under Native AOT, add the types of
your business-rule details to your resolver chain, as for your contracts. A
detail is serialized on its own: one that the options cannot serialize, or
that is nested deeper than 32 levels, is left out with a logged warning, and
the other details stay. The same applies to a dictionary of details that
contains itself or that is nested in dictionaries deeper than 8 levels.

An agent can correct its call from this text alone. Unexpected failures use generic text;
stack traces, connection strings, and sensitive exception messages are never
returned.

The reference implementation is demonstrated in
[`samples/Ark.MediatorFramework.Sample`](../../samples/Ark.MediatorFramework.Sample/README.md).
Its release gate must list and call generated query, mutation, upload, and
download tools through an ASP.NET Core test host and the official SDK client.
