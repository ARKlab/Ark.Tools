# OpenAPI

The generator creates one OpenAPI document per API version, expands versioned
routes, and derives tags and operation names from contracts. XML documentation
flows into schemas and operations. Ark transformers add NodaTime,
polymorphism, and `[ServerSet]` schemas; OAuth2 and Scalar provide interactive
discovery.

```csharp
services.AddOpenApi("v1", ConfigureOpenApi);
services.AddOpenApi("v2", ConfigureOpenApi);
...
options.AddArkNodaTimeSchemas()
    .AddArkServerSetProperties()
    .AddArkXmlDocumentation()
    .AddArkOAuthSecurity(_openApiSecurity);
```

Source: [`SampleStartup.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs).

Map `/openapi/{documentName}.json`/`.yaml` and Scalar as in the sample. Scalar
is an operator UI, not an authentication service; configure OAuth2 for your
identity provider. A handwritten operation transformer is the escape hatch.
Rationale: [`design.md`](../design.md).
