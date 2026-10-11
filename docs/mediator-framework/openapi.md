# OpenAPI and Scalar

OpenAPI is generated from the public HTTP contract metadata and host
configuration. Treat the document as a consumer-facing compatibility artifact,
not as a dump of implementation types.

## 1. Add XML documentation to the contract

```csharp
/// <summary>Creates a greeting for the authenticated user.</summary>
[ApiGroup("Greetings")]
[HttpEndpoint("POST", "/api/v{version}/greetings", SuccessStatusCode = 201)]
public sealed record CreateGreetingRequest : IRequest<CreateGreetingRequest, GreetingResponse>
{
    /// <summary>Gets the name to greet.</summary>
    public required string Name { get; init; }
}
```
Source: [`BookContracts.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookContracts.cs)

Document public properties and response values. The XML comments become
operation and schema descriptions.

## 2. Configure one document per API version

```csharp
services.AddOpenApi("v1", ConfigureOpenApi);
services.AddOpenApi("v2", ConfigureOpenApi);

private static void ConfigureOpenApi(OpenApiOptions options)
{
    options
        .AddArkTypeConverterValueSchemas()
        .AddArkNodaTimeSchemas()
        .AddArkValueObjectSchemas()
        .AddArkComplianceSchemas()
        .AddArkServerSetProperties()
        .AddArkXmlDocumentation()
        .AddArkOAuthSecurity(openApiSecurity)
        .AddArkPolymorphism<Shape, ShapeKind>(
            "kind",
            (ShapeKind.Circle, typeof(Circle)),
            (ShapeKind.Square, typeof(Square)));
}
```
Source: [`SampleStartup.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface/SampleStartup.cs)

Why each option exists:

| Option | Effect |
| --- | --- |
| `AddArkTypeConverterValueSchemas` | Documents custom converter values accurately |
| `AddArkNodaTimeSchemas` | Publishes stable NodaTime formats |
| `AddArkValueObjectSchemas` | Documents Vogen value objects as the schema of the primitive they wrap |
| `AddArkComplianceSchemas` | Documents sensitive value objects as their primitive schema |
| `AddArkServerSetProperties` | Removes server-owned input members |
| `AddArkXmlDocumentation` | Copies contract XML comments |
| `AddArkOAuthSecurity` | Publishes OAuth flows and scopes |
| `AddArkPolymorphism` | Documents discriminator and concrete types |

### Vogen value objects

`AddArkValueObjectSchemas()` describes a Vogen value object with the schema of the
primitive it wraps wherever it appears: as a component, as the items of an array
or list, and as a route or query parameter. Vogen can generate its own mapping
(`[OpenApiMarker<T>]` and `MapVogenTypesIn...()`); registering it as well, before
or after, leaves the document unchanged, because the Ark schemas are applied last.

Call Vogen's generated `MapVogenTypesIn...()` from a method of your own rather than
directly in the `AddOpenApi` lambda. ASP.NET Core's XML comment generator cannot
see Vogen's generated code, so it skips an `AddOpenApi` call whose lambda uses it,
and that document loses its XML descriptions:

```csharp
services.AddOpenApi("v1", options => ConfigureOpenApi(options));

private static void ConfigureOpenApi(OpenApiOptions options)
    => options.MapVogenTypesInVogenOpenApiMarkers().AddArkValueObjectSchemas();
```

## 3. Map JSON, YAML, and Scalar

```csharp
endpoints.MapOpenApi().AllowAnonymous();
endpoints.MapOpenApi("/openapi/{documentName}.yaml").AllowAnonymous();
endpoints.MapScalarApiReference(options =>
{
    options.AddAuthorizationCodeFlow("oauth2", flow => flow
        .WithClientId(openApiSecurity.ClientId)
        .WithAuthorizationUrl(openApiSecurity.AuthorizationUrl.ToString())
        .WithTokenUrl(openApiSecurity.TokenUrl.ToString())
        .WithPkce(Pkce.Sha256));
}).AllowAnonymous();
```
Source: [`SampleStartup.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Hosts/Web/Ark.MediatorFramework.Sample.Core.Web.WebInterface/SampleStartup.cs)

Expected sample URLs:

- `/openapi/v1.json`
- `/openapi/v1.yaml`
- `/openapi/v2.json`
- `/openapi/v2.yaml`
- `/scalar/v1`

The documents can be anonymous while the operations remain protected. Scalar
uses OAuth to authorize operation calls; it does not remove application
authorization.

## 4. Review the generated result

For the create contract, check that the document contains:

- `POST /api/v1/greetings`;
- a `201` success response;
- OAuth requirements when the contract requires a scope;
- only client-controlled input fields;
- response fields and XML descriptions;
- validation and standard error responses.

Do not hand-edit generated OpenAPI. Change contract metadata or host options.
Use a handwritten operation transformer only when metadata cannot express the
required document detail.

## 5. Test the boundary

Application tests verify returned values and typed exceptions. A focused
OpenAPI test fetches `/openapi/v1.json` and asserts operation names, status
codes, security requirements, and schema shape. Keep these assertions out of
Reqnroll application scenarios.
