# Migration from Swashbuckle to Microsoft OpenAPI

Ark.Tools now uses `Microsoft.AspNetCore.OpenApi` as the default generator in `ArkStartupWebApiCommon`.
Existing applications can keep the legacy Swashbuckle generator by overriding:

```csharp
public override bool UseSwashbuckleOpenApi => true;
```

Swagger UI and Redoc are still hosted by the API and read the same `/swagger/docs/{documentName}` specification route.
The route first serves build-generated JSON when present and falls back to runtime generation for development and tests.

## Build-time generation

Application projects that use the Microsoft OpenAPI path should reference `Microsoft.Extensions.ApiDescription.Server`.
This enables OpenAPI JSON generation during normal `dotnet build`.

Recommended project settings:

```xml
<PropertyGroup>
  <OpenApiDocumentsDirectory>$(OutputPath)</OpenApiDocumentsDirectory>
  <OpenApiGenerateDocumentsOptions>--openapi-version OpenApi3_1</OpenApiGenerateDocumentsOptions>
</PropertyGroup>
```

Build-time generation starts the application entry point with a mock server.
Guard startup code that performs external side effects, migrations, outbound calls, or hosted background work during document generation.

## Swashbuckle attributes

Swashbuckle-specific attributes are not interpreted automatically by Microsoft OpenAPI.
Prefer ASP.NET Core metadata, XML comments, and Ark.Tools/Microsoft OpenAPI transformers for new code.

| Swashbuckle attribute | Recommended replacement | Notes |
| --- | --- | --- |
| `SwaggerOperationAttribute` | XML comments or endpoint metadata transformed by `IOpenApiOperationTransformer` | Use for summary, description, tags, and operation-specific metadata. |
| `SwaggerResponseAttribute` | ASP.NET Core `ProducesResponseTypeAttribute` | Microsoft OpenAPI reads ApiExplorer response metadata. |
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
