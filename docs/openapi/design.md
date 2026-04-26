# OpenAPI modernization design

## Goals

- Reduce production security surface by supporting build-time OpenAPI document generation and static publication.
- Migrate new OpenAPI generation support toward `Microsoft.AspNetCore.OpenApi` and `Asp.Versioning.OpenApi`.
- Preserve current Swashbuckle, Swagger UI, and Redoc capabilities until the .NET 12 removal window in 2027.
- Support OpenAPI 3.0 and 3.1 where the underlying generator supports them.
- Keep frontend concerns separate from generator concerns.
- Preserve Ark.Tools-specific extensions currently implemented as Swashbuckle filters.

## Non-goals

- Removing Swashbuckle before .NET 12.
- Removing Swagger UI or Redoc before .NET 12.
- Replacing application API versioning strategy.
- Adding a production runtime endpoint requirement for OpenAPI JSON or UI.
- Generating client SDKs directly from Ark.Tools.

## Architecture

### Packages

| Package | Status | Purpose |
| --- | --- | --- |
| `Ark.Tools.AspNetCore.Swashbuckle` | Legacy, preserved until .NET 12 | Current Swashbuckle filters and Swagger UI integration. Mark public entry points as deprecated once replacements exist. |
| `Ark.Tools.AspNetCore.OpenApi` | New enhanced path | Microsoft OpenAPI generator integration, build-time generation guidance, transformer implementations, versioning integration. |
| `Ark.Tools.AspNetCore.OpenApi.Swashbuckle` or compatibility namespace | Transitional | Optional adapter layer for users who need Swashbuckle while adopting generator-neutral Ark.Tools options. |
| Documentation-only frontend recipes | New | Swagger UI, Scalar, and Redoc setup patterns consuming generated/static specs. |

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
| Build artifact output | Configure build-time document names and output folder. |

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

## Build-time generation design

Production OpenAPI publication should use build artifacts instead of anonymous runtime generation endpoints.

Required behavior:

- Enable `OpenApiGenerateDocuments=true` for application projects that opt in.
- Generate one JSON file per API version/document.
- Store generated artifacts in a deterministic output path such as `artifacts/openapi` or a configured docs publishing folder.
- Support OpenAPI 3.1 by default for .NET 10+ enhanced generation, with OpenAPI 3.0 available for clients that require it. The planned `docs/openapi/build-time-generation.md` and `docs/openapi/getting-started.md` guides should document both runtime `OpenApiVersion` configuration and build-time `OpenApiGenerateDocumentsOptions` configuration.
- Keep runtime `MapOpenApi`, `MapScalarApiReference`, `UseSwaggerUI`, and Redoc endpoints development-only by default.
- Provide guidance for guarding startup side effects when build-time generation invokes the app entrypoint.
- Add optional Spectral linting guidance for CI.

## API versioning design

Use `Asp.Versioning.OpenApi` for the enhanced path.

Required behavior:

- Support `GroupNameFormat = "'v'VVV"` or equivalent default formatting.
- Support `SubstituteApiVersionInUrl = true` for URL segment versioning.
- Generate document-per-version output.
- Preserve deprecation metadata and support sunset policy links when available.
- Register frontend document lists from `DescribeApiVersions()` for Scalar and analogous Swagger UI/Redoc configuration.

## Frontend design

### Swagger UI

Status: legacy-compatible and development-friendly.

Supported behavior:

- Continue supporting existing Swashbuckle Swagger UI setup until .NET 12.
- Permit Swagger UI to consume Microsoft-generated `/openapi/{documentName}.json` or static JSON.
- Keep enabled only in development by default.
- Preserve OAuth configuration documentation.

### Scalar

Status: recommended enhanced interactive frontend.

Supported behavior:

- Support `Scalar.AspNetCore` with Microsoft OpenAPI.
- Support multiple documents/API versions.
- Support Scalar-specific transformers from `Asp.Versioning.OpenApi` where useful.
- Keep runtime UI development-only by default; allow static-hosted docs pattern separately.

### Redoc

Status: recommended static/public reference frontend.

Supported behavior:

- Continue supporting Redoc until and beyond .NET 12 as a frontend consuming static OpenAPI JSON.
- Support Redoc vendor extensions through generator-neutral document/operation/schema extension options.
- Document static HTML generation through Redocly CLI or self-hosted Redoc assets.
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

## Compatibility and deprecation policy

- Existing Swashbuckle package remains supported through .NET 10 and .NET 11.
- New enhanced features should target `Ark.Tools.AspNetCore.OpenApi` first.
- Swashbuckle-specific APIs should be marked deprecated after an equivalent enhanced API exists.
- Deprecation messages should point to Microsoft OpenAPI documentation and Ark.Tools migration docs.
- Removal is planned for the .NET 12 major version line in 2027.

## Recommended way forward

Use Microsoft OpenAPI as the strategic generator, build-time JSON as the production artifact, Scalar as the recommended interactive development frontend, and Redoc as the preferred static reference frontend. Keep Swagger UI and Swashbuckle available for compatibility until .NET 12, but avoid adding net-new features that only work in Swashbuckle.
