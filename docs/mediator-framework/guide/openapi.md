# OpenAPI

Generated HTTP contracts produce versioned OpenAPI operations. Contract XML
documentation supplies the public operation, parameter, and schema prose so
the reference document stays aligned with the application surface.

## Configure one document per API version

The sample host configures one document for `v1` and one for `v2` because the
generator expands versioned routes into version-specific HTTP surfaces.

```csharp
services.AddOpenApi("v1", ConfigureOpenApi);
services.AddOpenApi("v2", ConfigureOpenApi);

private void ConfigureOpenApi(OpenApiOptions options)
{
    options
        .AddArkTypeConverterValueSchemas()
        .AddArkNodaTimeSchemas()
        .AddArkServerSetProperties()
        .AddArkXmlDocumentation()
        .AddArkOAuthSecurity(openApiSecurity)
        .AddArkPolymorphism<Shape, ShapeKind>(
            "kind",
            (ShapeKind.Circle, typeof(Circle)),
            (ShapeKind.Square, typeof(Square)));
}
```

## What each OpenAPI option does

| Option | Why it exists | What users see |
| --- | --- | --- |
| `AddArkTypeConverterValueSchemas()` | Documents values serialized through Ark type converters | Correct schema shape instead of opaque strings |
| `AddArkNodaTimeSchemas()` | Adds NodaTime-specific schema metadata | `LocalDate`, `OffsetDateTime`, and friends show the expected format |
| `AddArkServerSetProperties()` | Removes `[ServerSet]` request properties from input schemas | Clients do not see server-owned fields as writable input |
| `AddArkXmlDocumentation()` | Reads XML comments from public contracts and properties | Summaries and property descriptions appear in the document |
| `AddArkOAuthSecurity(...)` | Publishes OAuth metadata and scopes | Swagger/Scalar can authorize calls against the right scheme |
| `AddArkPolymorphism<TBase, TDiscriminator>(...)` | Documents discriminator-based polymorphic payloads | Clients know which discriminator and concrete types are valid |

## Map the documents and UI

```csharp
app.UseEndpoints(endpoints =>
{
    endpoints.MapOpenApi().AllowAnonymous();
    endpoints.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();
    endpoints.MapScalarApiReference(...).AllowAnonymous();
});
```

Expected URLs in the sample:

- `/openapi/v1.json`
- `/openapi/v1.yaml`
- `/openapi/v2.json`
- `/openapi/v2.yaml`
- `/scalar/v1`

**Outcome:** generated routes appear under their API group with consistent
operation names; server-set fields are absent from client request schemas and
supported NodaTime, polymorphic, and OAuth metadata is represented accurately.

## Document the contract, not the endpoint implementation

Put a summary on the contract and summaries on its public properties:

```csharp
/// <summary>Renames a greeting.</summary>
[HttpEndpoint("PATCH", "/api/v{version}/greetings/{id}")]
public sealed record RenameGreetingRequest : IRequest<GreetingResponse>
{
    /// <summary>Gets the greeting to rename.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the replacement text.</summary>
    public string Message { get; init; } = string.Empty;
}
```

The same documentation is emitted to the generated gRPC proto where
applicable. Treat descriptions, response codes, OAuth scopes, and schema shape
as part of the consumer experience; review them with the contract change.

## What users should expect in the document

For a route such as:

```csharp
[ApiGroup("Greetings")]
[HttpEndpoint("POST", "/api/v{version}/greetings", SuccessStatusCode = 201)]
public sealed record CreateGreetingRequest : IRequest<GreetingResponse>;
```

users should expect the OpenAPI document to show:

- a `POST /api/v1/greetings` operation under the `Greetings` tag;
- a `201` success response and documented error responses;
- request schema entries only for client-controlled fields;
- OAuth requirements when the route is authenticated.

Add a hand-written operation transformer only for document changes that cannot
be described by contract metadata. Architecture rationale: [design.md](../design.md).
