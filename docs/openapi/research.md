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
- Build-time generation runs during `dotnet build` by launching the application entrypoint with a mock server.
- This is not purely static analysis; startup paths must avoid external side effects, database connections, migrations, `IHostedService` / `BackgroundService` execution, or third-party calls during document generation.
- Runtime document endpoints regenerate the document per request unless cached.
- The modernization target is to generate the document continuously during build/development/test, then have the API host the generated JSON as a static artifact instead of generating it per anonymous request.
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

## Build-time generation and static hosting assessment

Build-time OpenAPI generation should become the default path now for development, tests, CI, and production packaging.

Advantages:

- Avoids exposing an anonymous `/swagger`, `/openapi`, `/scalar`, or `/redoc` document-generation endpoint that performs runtime reflection or document generation.
- Keeps the API-hosted spec endpoint available by serving a generated static JSON file from the API process.
- Produces a deterministic artifact during local development and CI so unit/integration tests can assert compatibility-sensitive outcomes.
- Allows generated specs to be reviewed, linted, diffed, signed, published to portals, or deployed with the API as static files.
- Enables CI validation with Spectral or equivalent OpenAPI linting without relying on runtime document-generation endpoints.
- Aligns with trimming / Native AOT direction because the runtime application no longer needs to preserve OpenAPI generation behavior in production.

Trade-offs:

- The app entrypoint still runs at build time with a mock server, so startup code must avoid external side effects during document generation.
- Build-time generation can fail if configuration, secrets, hosted services, database migrations, or external clients are required during startup.
- Runtime-only document customizations that depend on `HttpContext` or dynamic app state are not supported.
- Replace runtime-only customizations with deterministic configuration because the same generated file is used in development, tests, and production static hosting.
- Build-time YAML output is not available yet; YAML should be created later by conversion if needed.

Recommendation:

- Generate OpenAPI JSON on every normal build for every API version, including development builds.
- Make generated specs available to unit/integration tests for snapshot, schema, and compatibility assertions.
- Host the generated JSON directly from the API as a static file in every environment where docs are enabled.
- Do not use runtime-generated OpenAPI JSON endpoints for production docs.
- Publish the same static spec artifacts to API-hosted UIs and any external documentation portal.

## Swashbuckle vs Microsoft OpenAPI generator

### Swashbuckle status

Swashbuckle remains maintained and v10 added support for ASP.NET Core 10, `Microsoft.OpenApi` v2, and opt-in OpenAPI 3.1 output.
Its v10 migration guide says the change helps users who may migrate to `Microsoft.AspNetCore.OpenApi`, especially for Native AOT.
Swashbuckle remains its own generator stack.

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

### Swagger UI

- Most established and widely recognized frontend.
- Its current README lists OpenAPI 2.0, 3.0, 3.1, and 3.2 compatibility; verify OpenAPI 3.2 specification status before relying on it for contract governance.
- Strengths: interactive `try it out`, OAuth support, broad developer familiarity, plugins, and Swashbuckle packaging.
- Weaknesses: utilitarian UI, production exposure risk, customization often needs JS/CSS/plugins, and the package uses install analytics unless disabled.
- Integration: `Swashbuckle.AspNetCore.SwaggerUI` exposes embedded `swagger-ui` assets and can point to API-hosted static JSON after generation moves to Microsoft OpenAPI.

### Scalar

- Newer frontend with growing .NET ecosystem adoption.
- Microsoft docs show Scalar integration, and `asp.versioning` examples use it.
- Strengths: modern UX, built-in API client, dark mode/theme focus, ASP.NET Core package, and multi-document/version integration.
- Weakness: younger project than Swagger UI and Redoc; long-term enterprise maturity is still developing.
- Integration: `Scalar.AspNetCore` maps `/scalar`, serves embedded static assets, supports document route parameters, and can consume Microsoft OpenAPI endpoints/static specs.

### Redoc

- Mature open-source reference documentation frontend with strong public-docs adoption.
- Strengths: three-panel reference layout, search/navigation, Redoc vendor extensions, and static HTML generation through Redocly CLI.
- Weakness: open-source Redoc lacks built-in interactive try-it console; richer features are in Redocly commercial products.
- Integration: `Swashbuckle.AspNetCore.ReDoc` exposes embedded Redoc assets, and `Redoc.AspNetCore` is a Redoc-only alternative. Both can consume API-hosted static JSON.

Sources:

- [Swagger UI README](https://github.com/swagger-api/swagger-ui/blob/master/README.md)
- [Scalar ASP.NET Core README](https://github.com/scalar/scalar/blob/main/integrations/dotnet/aspnetcore/README.md)
- [Scalar ASP.NET Core endpoint implementation](https://github.com/scalar/scalar/blob/main/integrations/dotnet/aspnetcore/src/Scalar.AspNetCore/Extensions/ScalarEndpointRouteBuilderExtensions.cs)
- [Redoc README](https://github.com/Redocly/redoc/blob/main/README.md)
- [Redoc vendor extensions](https://github.com/Redocly/redoc/blob/main/docs/redoc-vendor-extensions.md)

## UI bundling and API-hosted UI options

The UIs should be exposed by the API directly, with their document URLs pointing to the API-hosted static OpenAPI JSON files. Research findings:

- `Swashbuckle.AspNetCore.SwaggerUI` serves embedded `swagger-ui-dist` assets from its middleware and does not require Swashbuckle generation as long as it is configured with static document URLs.
- `Swashbuckle.AspNetCore.ReDoc` serves embedded Redoc assets from its middleware and can point to a static spec URL.
- This is the lowest-friction Redoc option if Ark.Tools already keeps Swashbuckle UI packages during the compatibility window.
- `Scalar.AspNetCore` serves embedded Scalar assets and supports multiple document names, which fits versioned specs hosted by the API.
- `Redoc.AspNetCore` is a Redoc-only community package that also hosts Redoc from middleware.
- Consider it after evaluating maintenance cadence, package ownership, and dependency policy; it is less aligned with the existing Swashbuckle compatibility stack.

Recommended UI hosting model now:

1. Generate OpenAPI JSON files during build/development/test.
2. Copy or expose those generated files under a stable API route such as `/openapi/{documentName}.json`.
3. Expose Swagger UI, Scalar, and Redoc from API middleware/routes.
4. Configure every UI to read the static JSON route, not a runtime document-generation endpoint.

Sources:

- [Swashbuckle Swagger UI middleware](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.SwaggerUI/SwaggerUIMiddleware.cs)
- [Swashbuckle ReDoc middleware](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.ReDoc/ReDocMiddleware.cs)
- [Scalar ASP.NET Core endpoint implementation](https://github.com/scalar/scalar/blob/main/integrations/dotnet/aspnetcore/src/Scalar.AspNetCore/Extensions/ScalarEndpointRouteBuilderExtensions.cs)
- [Redoc.AspNetCore README](https://github.com/jonashendrickx/Redoc.AspNetCore/blob/main/README.md)

## Swashbuckle annotations and filters investigation

Ark.Tools currently references `Swashbuckle.AspNetCore.Annotations` and `Swashbuckle.AspNetCore.Filters` from `Ark.Tools.AspNetCore.Swashbuckle`. Repository usage observed in this branch:

- `ArkStartupWebApiCommon` calls `EnableAnnotations()`, so the base startup enables Swashbuckle attributes globally.
- No in-repository controllers or DTOs currently use `SwaggerOperation`, `SwaggerResponse`, `SwaggerParameter`, `SwaggerRequestBody`, `SwaggerSchema`, `SwaggerTag`, or `SwaggerSchemaFilter` attributes.
- External consumers may still rely on those attributes because the package reference is public through the Swashbuckle integration.
- The Reference Project sample uses `Swashbuckle.AspNetCore.Filters.IExamplesProvider<T>` inside `MultiPartJsonOperationFilter` to provide examples for multipart JSON form fields.
- `Swashbuckle.AspNetCore.Filters` also provides request/response example attributes, response-header filters, security requirements filters, and authorization-summary filters.
- Ark.Tools has its own default response/security story, but samples may still use the examples abstractions.

Migration impact:

- Microsoft OpenAPI does not automatically consume Swashbuckle-specific annotations or filters.
- The enhanced path needs a compatibility decision for each annotation/filter category.
- Options are native ASP.NET metadata/XML comments, Microsoft OpenAPI transformers that read Swashbuckle attributes, or Swashbuckle-only support until .NET 12.
- `IExamplesProvider<T>` usage in samples should be migrated either to a generator-neutral Ark.Tools example provider abstraction or to Microsoft OpenAPI transformers that can resolve registered example providers.

Sources:

- [Swashbuckle annotations documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-annotations.md)
- [Swashbuckle README package list](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/README.md)
- [Swashbuckle.AspNetCore.Filters README](https://github.com/mattfrear/Swashbuckle.AspNetCore.Filters/blob/master/README.md)

## Recommendation

Adopt a dual-track modernization:

1. Keep `Ark.Tools.AspNetCore.Swashbuckle` functional but mark it as legacy/deprecated for removal in the .NET 12 timeframe.
2. Introduce a generator-neutral Ark.Tools OpenAPI abstraction that can be implemented for both Swashbuckle and `Microsoft.AspNetCore.OpenApi` during the compatibility window.
3. Start introducing `Microsoft.AspNetCore.OpenApi` plus `Asp.Versioning.OpenApi` now, not in .NET 11.
4. Make build-time JSON generation the always-on source for development, tests, CI, and production static hosting.
5. Treat frontends as API-hosted consumers of static OpenAPI artifacts:
   - Swagger UI: compatibility and local ad-hoc testing.
   - Scalar: recommended interactive development UI and default enhanced frontend.
   - Redoc: recommended static/public reference documentation UI.
6. Preserve Swagger UI and Redoc until .NET 12, but route new features through the generator-neutral extension model so they work with Microsoft OpenAPI first.
