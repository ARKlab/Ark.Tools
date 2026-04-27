# Migration from Swashbuckle to Microsoft OpenAPI

Ark.Tools now uses `Microsoft.AspNetCore.OpenApi` as the default generator in `ArkStartupWebApiCommon`.
Existing applications can keep the legacy Swashbuckle generator by overriding:

```csharp
public override bool UseSwashbuckleOpenApi => true;
```

Swagger UI and Redoc are still hosted by the API and read the same `/swagger/docs/{documentName}` specification route.
The route serves build-generated JSON. In production builds, runtime document generation is not mapped and the route returns `404` if the generated file is missing.

## Build-time generation

Application projects that use the Microsoft OpenAPI path should reference `Microsoft.Extensions.ApiDescription.Server`.
This enables OpenAPI JSON generation during normal `dotnet build`.

Recommended project settings:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/bin/$(Configuration)/$(TargetFramework)/</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--openapi-version OpenApi3_1</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

Build-time generation starts the application entry point with a mock server.
Guard startup code that performs external side effects, migrations, outbound calls, or hosted background work during document generation.

## Step-by-step migration

1. Remove any `UseSwashbuckleOpenApi` override, or leave it set to `false`, so `ArkStartupWebApiCommon` registers Microsoft OpenAPI.
2. Add `Microsoft.Extensions.ApiDescription.Server` to the application project.
3. Set `OpenApiDocumentsDirectory` to the application output directory and keep `OpenApiGenerateDocumentsOptions` on `OpenApi3_1` so production serves static, build-generated specs from `/swagger/docs/{documentName}`.
4. Keep the build-time `AddOpenApi(...)` calls in the application project. The Microsoft source generator needs these calls in the application assembly, while Ark.Tools keeps the runtime serving path guarded by the generator selection in the base startup class.
5. Move Swashbuckle `ISchemaFilter` and `IOperationFilter` customizations to `ConfigureMicrosoftOpenApi` with `IOpenApiSchemaTransformer` and `IOpenApiOperationTransformer`.
6. Keep Swagger UI options and OAuth UI configuration. Add Microsoft OpenAPI document transformers for security schemes previously added with `ConfigureSwaggerGen`.
7. Build the application and verify the expected `{ProjectName}_v*.json` files are generated in the application output directory.

The `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.WebInterface` project demonstrates these steps by:

- generating OpenAPI files at build time,
- configuring schema and operation transformers in `ConfigureMicrosoftOpenApi`,
- preserving Swagger UI OAuth setup, and
- adding OAuth security schemes with Microsoft OpenAPI document transformers, and
- skipping hosted background workers while `dotnet-getdocument` generates the specification.

## Swashbuckle attributes

Swashbuckle-specific attributes are not interpreted automatically by Microsoft OpenAPI.
Prefer ASP.NET Core metadata, XML comments, and Ark.Tools/Microsoft OpenAPI transformers for new code.

| Swashbuckle attribute | Recommended replacement | Notes |
| --- | --- | --- |
| `SwaggerOperationAttribute` | Drop-in compatibility is registered by default. | Ark.Tools maps `Summary`, `Description`, `OperationId`, and `Tags` through a Microsoft OpenAPI operation transformer. Prefer XML comments or native endpoint metadata for new code. |
| `SwaggerResponseAttribute` | Drop-in compatibility is registered by default; `ProducesResponseTypeAttribute` is preferred for new code. | Ark.Tools maps status code, description, response type/schema, and explicit content types through a Microsoft OpenAPI operation transformer. Use `ProducesResponseTypeAttribute` only when you do not need Swashbuckle's description/content-type metadata. |
| `SwaggerParameterAttribute` | XML comments, `FromQuery`/`FromRoute` metadata, or an operation transformer | Use native binding metadata for required and source information. |
| `SwaggerRequestBodyAttribute` | ASP.NET Core request metadata plus XML comments or an operation transformer | Prefer deterministic transformers for examples and descriptions. |
| `SwaggerSchemaAttribute` | System.Text.Json metadata, XML comments, or `IOpenApiSchemaTransformer` | Swashbuckle schema filters remain legacy-only. |
| `SwaggerTagAttribute` | Controller/action grouping metadata or document/operation transformers | Use generator-neutral tag metadata where possible. |
| `SwaggerSchemaFilterAttribute` | `IOpenApiSchemaTransformer` | Treat this as Swashbuckle-only during the transition. |

## Swashbuckle filters

| Swashbuckle extension point | Microsoft OpenAPI replacement |
| --- | --- |
| `IDocumentFilter` | `IOpenApiDocumentTransformer` |
| `IOperationFilter` | `IOpenApiOperationTransformer` |
| `ISchemaFilter` | `IOpenApiSchemaTransformer` |
| `IExamplesProvider<T>` from `Swashbuckle.AspNetCore.Filters` | Prefer a generator-neutral example model or an operation/schema transformer that writes examples deterministically. |

Current Ark.Tools defaults provide Microsoft OpenAPI equivalents for default problem responses, NodaTime schema formats, flags enum query parameters, OData media type cleanup, versioned document selection, and stable operation IDs.

## Compatibility guidance

- Use the Microsoft OpenAPI default for new applications.
- Override `UseSwashbuckleOpenApi` only when existing Swashbuckle filters or attributes cannot be migrated immediately.
- Keep Swagger UI and Redoc pointed at `/swagger/docs/{documentName}` so UI routes do not change while the generator changes.
- Move custom Swashbuckle filters to generator-neutral Ark.Tools options or Microsoft OpenAPI transformers before the .NET 12 removal window.
