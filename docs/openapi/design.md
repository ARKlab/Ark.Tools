# OpenAPI modernization design

## Goals

- Reduce production security surface by supporting always-on OpenAPI document generation and static server hosting.
- Introduce new OpenAPI generation support now with `Microsoft.AspNetCore.OpenApi` and `Asp.Versioning.OpenApi`.
- Preserve current Swashbuckle, Swagger UI, and Redoc capabilities until the .NET 12 removal window in 2027.
- Support OpenAPI 3.0 and 3.1 where the underlying generator supports them.
- Keep frontend concerns separate from generator concerns.
- Preserve Ark.Tools-specific extensions currently implemented as Swashbuckle filters.

## Non-goals

- Removing Swashbuckle before .NET 12.
- Removing Swagger UI or Redoc before .NET 12.
- Replacing application API versioning strategy.
- Adding a production runtime-generated OpenAPI JSON endpoint requirement.
- Generating client SDKs directly from Ark.Tools.

## Architecture

### Packages

| Package | Status | Purpose |
| --- | --- | --- |
| `Ark.Tools.AspNetCore.Swashbuckle` | Legacy, preserved until .NET 12 | Current Swashbuckle filters and Swagger UI integration. Mark public entry points as deprecated once replacements exist. |
| `Ark.Tools.AspNetCore.OpenApi` | New enhanced path, introduced now | Microsoft OpenAPI generator integration, always-on build generation, static spec hosting, transformer implementations, versioning integration. |
| `Ark.Tools.AspNetCore.OpenApi.Swashbuckle` or compatibility namespace | Transitional | Optional adapter layer for users who need Swashbuckle while adopting generator-neutral Ark.Tools options. |
| API-hosted frontend integrations | New | Swagger UI, Scalar, and Redoc setup patterns consuming API-hosted static specs. |

### Generator-neutral options

Define a single conceptual Ark.Tools OpenAPI options model that is independent of Swashbuckle filters:

| Option area | Behavior |
| --- | --- |
| Document metadata | Title, description, contact, license, terms, version formatting, OpenAPI version. |
| API versioning | Document per API version, URL substitution, deprecation/sunset metadata, default document selection. |
| XML comments | Include current assembly and selected referenced assemblies. |
| Security | Add document-level schemes and operation-level requirements from authentication/authorization metadata. |
| Default responses | Add missing default problem responses for common status codes. |
| Serialization/schema mappings | NodaTime, flags enums, matrix arrays, nullable handling, System.Text.Json metadata alignment. |
| OData cleanup | Remove OData media types from non-OData operations. |
| Operation ID policy | Stable, deterministic operation IDs compatible with existing generated clients. |
| UI extensions | Redoc and Scalar vendor extensions such as logo, tag groups, code samples, badges, and document metadata. |
| Build artifact output | Configure always-on document names, output folder, test artifacts, and static hosting path. |

### Extension mapping

| Current Swashbuckle extension | Microsoft OpenAPI design equivalent | Notes |
| --- | --- | --- |
| `DefaultResponsesOperationFilter` | Operation transformer | Use `GetOrCreateSchemaAsync` for `ProblemDetails` and `ValidationProblemDetails` on .NET 10+. |
| `SupportNodaTimeExtensions.MapNodaTimeTypes` | Schema transformer | Apply string formats/examples for NodaTime types and nullable variants. |
| `SupportFlaggedEnums` | Operation transformer | Detect query `[Flags]` enum parameters from ApiExplorer metadata and set simple array style. |
| `MatrixSchemaFilter` | Schema transformer | Represent `double?[,]` as nested arrays. |
| `PrettifyOperationIdOperationFilter` | Operation transformer | Preserve existing operation ID shape unless an application opts into a new strategy. |
| `SetVersionInPaths` | Versioning integration / document transformer | Prefer `Asp.Versioning.OpenApi` and `SubstituteApiVersionInUrl`; retain transformer fallback for compatibility. |
| `SwaggerDefaultValues` | Operation transformer | Mark deprecated operations, copy parameter descriptions/default values, required flags, and remove unsupported response content types. |
| `FixODataMediaTypeOnNonOData` | Operation transformer | Preserve behavior for mixed OData/non-OData apps. |
| `IncludeXmlCommentsForAssembly` | Microsoft XML comments configuration and transformer support | Use first-party XML comment support where possible; document project-file configuration for referenced assemblies. |
| Obsolete no-op security filters | No replacement | Keep obsolete until .NET 12; direct users to generator-neutral security options. |

## Always-on generation and static hosting design

OpenAPI publication should use generated artifacts instead of anonymous runtime generation endpoints. Generation starts now and should run for development, test, CI, and production builds.

Required behavior:

- Enable OpenAPI JSON generation for every normal application build, not only release/publish builds.
- Generate one JSON file per API version/document.
- Store generated artifacts in a deterministic output path that tests can locate, such as `artifacts/openapi` or another configured docs output folder.
- Make the generated JSON available to unit/integration tests for snapshot tests and compatibility assertions.
- Host the generated JSON from the API as static files under stable routes such as `/openapi/{documentName}.json`.
- Support OpenAPI 3.1 by default for .NET 10+ enhanced generation, with OpenAPI 3.0 available for clients that require it.
- Document runtime `OpenApiVersion` configuration and build-time `OpenApiGenerateDocumentsOptions` configuration in the planned `docs/openapi/build-time-generation.md` and `docs/openapi/getting-started.md` guides.
- Avoid production use of `MapOpenApi` or any runtime-generated spec endpoint unless explicitly enabled for diagnostics.
- Provide guidance for guarding startup side effects when build-time generation invokes the app entrypoint.
- Add optional Spectral linting guidance for CI.

## API versioning design

Use `Asp.Versioning.OpenApi` for the enhanced path.

Required behavior:

- Support `GroupNameFormat = "'v'VVV"` or equivalent default formatting.
- Support `SubstituteApiVersionInUrl = true` for URL segment versioning.
- Generate document-per-version output.
- Preserve deprecation metadata and support sunset policy links when available.
- Register frontend document lists from `DescribeApiVersions()` for Scalar and analogous Swagger UI/Redoc configuration, pointing each UI to static spec routes.

## Frontend design

### Swagger UI

Status: legacy-compatible and development-friendly.

Supported behavior:

- Continue supporting existing Swashbuckle Swagger UI setup until .NET 12.
- Use `Swashbuckle.AspNetCore.SwaggerUI` as the bundled UI asset provider while it remains the lowest-risk option.
- Configure Swagger UI to consume API-hosted static JSON such as `/openapi/{documentName}.json`.
- Expose the UI directly from the API wherever API documentation is enabled.
- Preserve OAuth configuration documentation.

### Scalar

Status: recommended enhanced interactive frontend.

Supported behavior:

- Support `Scalar.AspNetCore` with Microsoft OpenAPI.
- Use Scalar's bundled ASP.NET Core assets for API-hosted UI pages.
- Support multiple documents/API versions.
- Support Scalar-specific transformers from `Asp.Versioning.OpenApi` where useful.
- Configure Scalar to consume API-hosted static JSON instead of runtime-generated specs.

### Redoc

Status: recommended static/public reference frontend.

Supported behavior:

- Continue supporting Redoc until and beyond .NET 12 as a frontend consuming static OpenAPI JSON.
- Prefer `Swashbuckle.AspNetCore.ReDoc` during the compatibility window if a bundled Redoc middleware is needed with minimal new dependencies.
- Evaluate `Redoc.AspNetCore` only after package ownership, maintenance, and dependency policy review.
- Support Redoc vendor extensions through generator-neutral document/operation/schema extension options.
- Expose Redoc directly from the API and point it to API-hosted static JSON.
- Do not require Swashbuckle generation for Redoc.

## Supported vendor extensions

Ark.Tools should support emitting frontend extensions through generator-neutral configuration:

| Extension | Frontend | OpenAPI location | Purpose |
| --- | --- | --- | --- |
| `x-logo` | Redoc | `info` | API branding. |
| `x-tagGroups` | Redoc | root | Group tags in the side menu. |
| `x-displayName` | Redoc | tag | Human-friendly tag label. |
| `x-codeSamples` | Redoc | operation | Operation code samples. |
| `x-badges` | Redoc | operation | Operation badges. |
| `x-summary` | Redoc | response | Response summary label. |
| `x-enumDescriptions` | Redoc | schema | Enum value descriptions. |
| Scalar document metadata | Scalar | UI configuration | Title, theme, preferred default document, authentication UI settings. |

## Swashbuckle annotation and filter compatibility

The compatibility window must include more than Swashbuckle's generator filters. It must also account for packages referenced by Ark.Tools and samples:

| Package / feature | Current use | Enhanced-path requirement |
| --- | --- | --- |
| `Swashbuckle.AspNetCore.Annotations` | Referenced by `Ark.Tools.AspNetCore.Swashbuckle`; `EnableAnnotations()` is called in the base startup. | Decide whether each attribute is mapped to Microsoft OpenAPI transformers, replaced with native metadata/XML comments, or documented as Swashbuckle-only until .NET 12. |
| `SwaggerOperation`, `SwaggerResponse`, `SwaggerParameter`, `SwaggerRequestBody`, `SwaggerSchema`, `SwaggerTag` | No direct in-repository attribute usage found, but external consumers may rely on them. | Provide a migration table and compatibility tests for any supported attribute mappings. |
| `SwaggerSchemaFilter` | No direct in-repository usage found. | Prefer generator-neutral schema transformers; document Swashbuckle-only behavior if not mapped. |
| `Swashbuckle.AspNetCore.Filters.IExamplesProvider<T>` | Used by the Reference Project `MultiPartJsonOperationFilter` sample. | Introduce or document a generator-neutral example provider model, or write Microsoft OpenAPI transformers that can resolve existing example providers during the compatibility window. |
| Filters package security/example helpers | Package referenced by Ark.Tools; examples package provides request/response examples, response headers, security requirements, and authorization summary helpers. | Avoid taking a hard dependency in the enhanced package unless the feature is explicitly supported; prefer Ark.Tools-owned abstractions. |

## Compatibility and deprecation policy

- Existing Swashbuckle package remains supported through .NET 10 and .NET 11.
- New enhanced features should target `Ark.Tools.AspNetCore.OpenApi` immediately, starting in the current modernization work.
- Swashbuckle-specific APIs should be marked deprecated after an equivalent enhanced API exists.
- Deprecate Swashbuckle runtime generation separately from UI hosting; Swagger UI and Redoc middleware can remain if they only consume API-hosted static specs.
- Deprecation messages should point to Microsoft OpenAPI documentation and Ark.Tools migration docs.
- Removal is planned for the .NET 12 major version line in 2027.

## Recommended way forward

Use Microsoft OpenAPI as the strategic generator starting now, always-on generated JSON as the testable artifact, API-hosted static JSON as the server contract endpoint, Scalar as the recommended enhanced interactive frontend, Swagger UI as the compatibility frontend, and Redoc as the preferred reference frontend. Keep Swashbuckle generation available for compatibility until .NET 12, but avoid adding net-new features that only work in Swashbuckle.
