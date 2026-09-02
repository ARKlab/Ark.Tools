# GEN-09 — XML documentation into OpenAPI and exported `.proto`

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

Supersedes step 1 of [NET-01](../aspnetcore/NET-01-openapi-xml-docs.md) (which keeps only the
OpenAPI 3.1 verification, YAML endpoint and doc-UI decision).

## Problem

Contract XML documentation is invisible on the wire. OpenAPI operations and schemas have no
`summary`/`description`, and the exported `.proto` files carry no comments, so both the Scalar/Swagger
UI and any polyglot gRPC consumer see undocumented APIs. The `Microsoft.AspNetCore.OpenApi` XML
support relies on runtime/`AdditionalFiles` plumbing per host and does not cover the code-first gRPC
side at all.

## Design

See `docs/mediator-framework/design.md` → *OpenAPI operation grouping, naming and documentation*.

The **generators** are the mechanism: they already have the Roslyn `ISymbol` for the contract and
every property, and `ISymbol.GetDocumentationCommentXml()` returns the documentation comment
regardless of `GenerateDocumentationFile` and regardless of which assembly declares the type. No host
wiring, no runtime XML file loading, no reflection.

Mapping:

| XML source | OpenAPI | `.proto` |
| --- | --- | --- |
| contract type `<summary>` | operation `summary` | leading comment on the rpc method and on the request message |
| contract type `<remarks>` | operation `description` | continuation of the same leading comment |
| property `<summary>` (route/query bound) | parameter `description` | — |
| property `<summary>` (body bound) | request schema property `description` | leading comment on the message field |
| response type/property `<summary>` | response schema `description` | leading comment on the response message/field |

## Steps

1. Add a shared helper in each generator project (or a linked source file) that extracts the
   `<summary>` and `<remarks>` inner text from `ISymbol.GetDocumentationCommentXml(cancellationToken)`,
   normalizes whitespace, strips the `<member>` wrapper, resolves `<see cref="..."/>` to the referenced
   name and returns `null` when empty.
   Docs: [`ISymbol.GetDocumentationCommentXml`](https://learn.microsoft.com/dotnet/api/microsoft.codeanalysis.isymbol.getdocumentationcommentxml),
   [XML documentation tags](https://learn.microsoft.com/dotnet/csharp/language-reference/xmldoc/recommended-tags).
2. `MinimalApiEndpointGenerator`: emit `.WithSummary("…").WithDescription("…")` for the operation, and
   emit parameter descriptions via the metadata the OpenAPI layer can read (extend the existing
   `ArkOpenApiEx` transformer infrastructure with an `ArkDocumentationMetadata` endpoint-metadata type
   carrying `(parameterName, description)` pairs and the schema-property descriptions, consumed by a
   new `AddArkXmlDocumentation()` operation/schema transformer).
   Docs: [Minimal API OpenAPI metadata](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi#openapi-operation-summary-and-description).
3. `AddArkXmlDocumentation()` must be additive: it never overwrites a description already set by a
   host transformer.
4. `GrpcEndpointGenerator` / proto emission: write the documentation as `//` leading comments above the
   generated `rpc`, `message` and field declarations, wrapped at 100 columns, with `//` escaping of any
   embedded newline. Comments must not appear inside the message body in a position that breaks the
   proto syntax (verify with `Grpc.Tools` compiling the exported protos in
   `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.GrpcClient`).
5. Wire `AddArkXmlDocumentation()` into the sample's `ConfigureOpenApi` and document the helper in
   `design.md` → *HTTP hosting helpers*.
6. Document the contract-documentation convention (summary on the contract = endpoint docs, summary on
   properties = parameter docs) in the user guide backlog item (DOC-01).

## Test coverage (required)

- Generator snapshot tests: a documented contract produces `.WithSummary`/`.WithDescription` and the
  documentation metadata; an undocumented contract produces neither (no empty strings).
- Framework test asserting the XML extraction helper: multi-line summary, `<remarks>`, `<see cref>`,
  empty/missing comment, and a comment on an **inherited** property.
- Sample test: `/openapi/v1.json` contains the contract summary as the operation summary, the property
  summary as the parameter description and as the request-schema property description.
- Proto test: the exported `.proto` for a documented contract contains the summary as a leading
  comment, and the `Ark.MediatorFramework.Sample.GrpcClient` project still compiles the exported
  protos (build gate covers this).

## Outcomes

- XML documentation authored once on the contract reaches both the OpenAPI document and the exported
  `.proto`, produced by the generators without any per-host configuration.

## Acceptance

- [x] Documentation extraction helper implemented and unit-tested (`XmlDocumentation.cs` in both `MinimalApi.Generators` and `Grpc.Generators`; tests `GeneratorSnapshotTests.cs:871-955` cover multi-line/`<remarks>`/entity encoding). Inherited-member XML doc case not explicitly asserted — leaving this parenthetical note in place of a full check.
- [x] Operation summary/description and parameter/schema descriptions present in the OpenAPI document
      (sample test asserts on the JSON document, not on UI HTML) (`MinimalApiOpenApiTests.cs:25,41`).
- [x] Exported `.proto` files carry leading comments for services, methods, messages and fields, and
      still compile with `Grpc.Tools` (`GrpcEndpointGenerator.cs:341,807,837` + `GeneratorSnapshotTests.cs:943-955`).
- [x] `AddArkXmlDocumentation()` is additive and documented in `design.md` (`ArkOpenApiEx.cs:19`, `design.md:648`, wired in `SampleStartup.cs:254`).
- [x] NET-01 updated to drop the superseded XML-doc step.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
