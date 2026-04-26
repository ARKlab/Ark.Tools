# OpenAPI modernization research

## Scope

This document summarizes research for modernizing Ark.Tools OpenAPI support while preserving the current Swashbuckle-based Swagger UI and Redoc capabilities until the planned .NET 12 removal window in 2027.

## Current Ark.Tools OpenAPI surface

The current package is `Ark.Tools.AspNetCore.Swashbuckle` and targets .NET 10. It depends on Swashbuckle, `Microsoft.OpenApi`, `Microsoft.AspNetCore.OData`, and `Asp.Versioning.OData.ApiExplorer`.

Supported extensions and behaviors observed in the current package:

| Area | Current capability |
| --- | --- |
| XML comments | `IncludeXmlCommentsForAssembly` discovers XML documentation files from the application base directory. |
| API versioning | `SetVersionInPaths` substitutes `v{api-version}` with the OpenAPI document version. `SwaggerDefaultValues` marks deprecated operations from ApiExplorer metadata and handles default values / required parameters. |
| Operation IDs | `PrettifyOperationIdOperationFilter` generates operation IDs from HTTP method and relative path. |
| Default responses | Adds `400`, `401`, `403`, and `500` responses with `ProblemDetails` / `ValidationProblemDetails` JSON schemas when missing. |
| OData | `FixODataMediaTypeOnNonOData` removes OData media types from non-OData controllers to avoid Swagger UI default `Accept` headers causing `406`. |
| NodaTime | Maps common NodaTime types to string schemas with suitable formats/examples. |
| Flags enums | Represents query-string `[Flags]` enum parameters as simple, non-exploded arrays. |
| Matrix schema | Represents `double?[,]` as a nested array schema. |
| Compatibility no-ops | `RequiredSchemaFilter`, `SecurityRequirementsOperationFilter`, and `AddUserImpersonationScope` are obsolete and point users to Swashbuckle-native configuration. |
| Frontends | The historical approach supports Swagger UI and Redoc against Swashbuckle-generated specifications. |

## Microsoft OpenAPI in .NET 8, 9, and 10

Microsoft's first-party OpenAPI stack is now centered on `Microsoft.AspNetCore.OpenApi` and `Microsoft.Extensions.ApiDescription.Server`.

Key findings:

- ASP.NET Core supports OpenAPI document generation for controller-based and Minimal API apps through `Microsoft.AspNetCore.OpenApi`.
- The package supports runtime OpenAPI endpoints and transformer APIs for document, operation, and schema customizations.
- The Microsoft package does **not** ship a UI. Microsoft documentation shows using Swagger UI, Redoc, or Scalar as consumers of the generated document.
- Build-time document generation is enabled by adding `Microsoft.Extensions.ApiDescription.Server` and setting `OpenApiGenerateDocuments=true`.
- Build-time generation runs during `dotnet build` by launching the application entrypoint with a mock server. This is not purely static analysis; startup paths still need to be safe for document generation.
- Runtime document endpoints regenerate the document per request unless cached. Removing those endpoints from production reduces anonymous reflection/document-generation surface.
- .NET 10 adds first-party OpenAPI 3.1 and JSON Schema draft 2020-12 support, YAML runtime output, `IOpenApiDocumentProvider`, stronger XML comment support, and `GetOrCreateSchemaAsync` for transformers.
- Build-time YAML output is documented as not supported yet, so JSON should be the canonical generated artifact until this changes.
- The Microsoft guidance is explicit that OpenAPI UI endpoints should only be enabled in development environments to limit information disclosure.
- The first-party stack supports trimming and Native AOT scenarios better than Swashbuckle.

Sources:

- [ASP.NET Core OpenAPI overview](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview?view=aspnetcore-10.0)
- [Generate OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0)
- [Customize OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0)
- [Use generated OpenAPI documents](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/using-openapi-documents?view=aspnetcore-10.0)
- [ASP.NET Core 10 OpenAPI release notes](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#openapi)
- [Built-in OpenAPI proposal](https://github.com/dotnet/aspnetcore/issues/54598)

## Build-time generation security assessment

Build-time OpenAPI generation should be the recommended production path.

Advantages:

- Avoids exposing an anonymous `/swagger`, `/openapi`, `/scalar`, or `/redoc` document-generation endpoint in production.
- Removes a runtime path that introspects endpoint metadata and schemas for every unauthenticated request.
- Allows generated specs to be treated as build artifacts, reviewed, linted, diffed, signed, published to portals, or deployed as static files.
- Enables CI validation with Spectral or equivalent OpenAPI linting without booting the application as a public service.
- Aligns with trimming / Native AOT direction because the runtime application no longer needs to preserve OpenAPI generation behavior in production.

Trade-offs:

- The app entrypoint still runs at build time with a mock server, so startup code must avoid external side effects during document generation.
- Build-time generation can fail if configuration, secrets, hosted services, database migrations, or external clients are required during startup.
- Runtime-only document customizations that depend on `HttpContext` or dynamic app state must be prohibited for generated production artifacts or replaced with deterministic configuration.
- Build-time YAML output is not available yet; YAML should be created later by conversion if needed.

Recommendation:

- Generate OpenAPI JSON at build time for every published API version.
- Do not map runtime OpenAPI JSON/UI endpoints in production by default.
- Permit development-only runtime endpoints for local feedback.
- Publish static docs assets from CI or deployment packaging when production API docs must be accessible.

## Swashbuckle vs Microsoft OpenAPI generator

### Swashbuckle status

Swashbuckle remains maintained and v10 added support for ASP.NET Core 10, `Microsoft.OpenApi` v2, and opt-in OpenAPI 3.1 output. Its v10 migration guide states that the change helps users who may migrate from Swashbuckle to `Microsoft.AspNetCore.OpenApi`, especially for Native AOT, but Swashbuckle remains its own generator stack.

Swashbuckle v10 advantages:

- Mature generator and ASP.NET Core integration.
- Bundled Swagger UI middleware assets.
- Existing Ark.Tools code and user applications already rely on `SwaggerGenOptions`, filters, and Swashbuckle conventions.
- Strong ecosystem knowledge and compatibility with existing Swagger UI/Redoc deployments.

Swashbuckle v10 disadvantages:

- Breaking changes from `Microsoft.OpenApi` v2 surface through Swashbuckle filter APIs.
- Heavy custom filter usage creates migration cost.
- It is no longer the default direction in ASP.NET Core templates from .NET 9 onward.
- Runtime generation and UI endpoints are commonly used, increasing accidental production exposure risk.
- Less aligned with trimming and Native AOT than the Microsoft generator.

Source: [Migrating to Swashbuckle.AspNetCore v10](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md)

### Microsoft generator advantages

- First-party ASP.NET Core direction.
- Supports runtime and build-time generation through official packages.
- Uses transformer APIs that map cleanly to Ark.Tools extension categories: document, operation, and schema.
- Better Native AOT and trimming story.
- Works with `Asp.Versioning.OpenApi` for document-per-version output.
- Can feed any frontend because the UI is decoupled from generation.

### Microsoft generator disadvantages

- No bundled UI; Ark.Tools must integrate or document Swagger UI, Scalar, and Redoc separately.
- Transformer model differs from Swashbuckle filters and requires adapter work.
- Some Swashbuckle conveniences may not have one-to-one equivalents.
- Build-time generation invokes the app entrypoint, so applications need guidance for safe startup behavior.
- OpenAPI 3.1 / `Microsoft.OpenApi` v2 changes affect custom code even when generating OpenAPI 3.0.

## asp.versioning status

`asp.versioning` has caught up with Microsoft's generator through the `Asp.Versioning.OpenApi` package.

Findings:

- The package README says it integrates `Microsoft.AspNetCore.OpenApi` with API Versioning.
- Official examples use `.AddOpenApi(...)`, `MapOpenApi().WithDocumentPerVersion()`, `DescribeApiVersions()`, and Scalar document registration.
- API Explorer options still support `GroupNameFormat` and `SubstituteApiVersionInUrl`, preserving the existing URL-segment versioning pattern.
- The versioning package can add Scalar-specific transformers through `options.Document.AddScalarTransformers()`.

Sources:

- [Asp.Versioning.OpenApi README](https://github.com/dotnet/aspnet-api-versioning/blob/main/src/AspNetCore/WebApi/src/Asp.Versioning.OpenApi/README.md)
- [Minimal OpenAPI example](https://github.com/dotnet/aspnet-api-versioning/blob/main/examples/AspNetCore/WebApi/MinimalOpenApiExample/Program.cs)
- [MVC OpenAPI example](https://github.com/dotnet/aspnet-api-versioning/blob/main/examples/AspNetCore/WebApi/OpenApiExample/Program.cs)

## Frontend comparison

| Frontend | Maturity | Strengths | Weaknesses | Generator integration |
| --- | --- | --- | --- | --- |
| Swagger UI | Most established and widely recognized. Active project with OpenAPI 2.0, 3.0, 3.1, and 3.2 compatibility listed in its README. | Excellent interactive `try it out`, OAuth support, broad developer familiarity, plugin/customization APIs, packaged by Swashbuckle. | UI is utilitarian, production exposure is risky, customization often needs JS/CSS/plugins, package uses install analytics unless disabled. | Can consume Microsoft-generated `/openapi/{doc}.json` or static JSON. Swashbuckle bundles middleware assets. |
| Scalar | Newer but rapidly adopted in the .NET ecosystem. Microsoft docs show Scalar integration and `asp.versioning` examples use it. | Modern UX, built-in API client, dark mode/theme focus, strong ASP.NET Core package, multi-document/version integration. | Younger project than Swagger UI/Redoc; long-term enterprise maturity still developing. | `Scalar.AspNetCore` maps `/scalar`, can consume Microsoft OpenAPI endpoints/static specs, and `asp.versioning` has Scalar transformers. |
| Redoc | Mature open-source reference documentation tool with strong public-docs adoption. | Excellent three-panel reference layout, search/navigation, strong support for vendor extensions (`x-logo`, `x-tagGroups`, `x-codeSamples`, `x-badges`, etc.), static HTML generation through Redocly CLI. | Open-source Redoc lacks built-in interactive try-it console; richer features are in Redocly commercial products. | Can consume any OpenAPI JSON/static file. Extensions should be emitted by generator transformers, not tied to Swashbuckle. |

Sources:

- [Swagger UI README](https://github.com/swagger-api/swagger-ui/blob/master/README.md)
- [Scalar ASP.NET Core README](https://github.com/scalar/scalar/blob/main/integrations/dotnet/aspnetcore/README.md)
- [Scalar ASP.NET Core endpoint implementation](https://github.com/scalar/scalar/blob/main/integrations/dotnet/aspnetcore/src/Scalar.AspNetCore/Extensions/ScalarEndpointRouteBuilderExtensions.cs)
- [Redoc README](https://github.com/Redocly/redoc/blob/main/README.md)
- [Redoc vendor extensions](https://github.com/Redocly/redoc/blob/main/docs/redoc-vendor-extensions.md)

## Recommendation

Adopt a dual-track modernization:

1. Keep `Ark.Tools.AspNetCore.Swashbuckle` functional but mark it as legacy/deprecated for removal in the .NET 12 timeframe.
2. Introduce a generator-neutral Ark.Tools OpenAPI abstraction that can be implemented for both Swashbuckle and `Microsoft.AspNetCore.OpenApi` during the compatibility window.
3. Make `Microsoft.AspNetCore.OpenApi` plus `Asp.Versioning.OpenApi` the recommended generator for new work.
4. Make build-time JSON generation the recommended production publication model.
5. Treat frontends as independent consumers of OpenAPI artifacts:
   - Swagger UI: compatibility and local ad-hoc testing.
   - Scalar: recommended interactive development UI and default enhanced frontend.
   - Redoc: recommended static/public reference documentation UI.
6. Preserve Swagger UI and Redoc until .NET 12, but route new features through the generator-neutral extension model so they work with Microsoft OpenAPI first.
