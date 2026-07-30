# OpenAPI

Generated HTTP contracts produce versioned OpenAPI operations. Contract XML
documentation supplies the public operation, parameter, and schema prose so
the reference document stays aligned with the application surface.

## Configure a document

```csharp
services.AddOpenApi("v1", options =>
{
    options.AddArkNodaTimeSchemas()
        .AddArkServerSetProperties()
        .AddArkXmlDocumentation()
        .AddArkOAuthSecurity(openApiSecurity);
});
```

Map the configured OpenAPI document and your chosen UI in the host. Configure
one document for each supported API version.

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
}
```

The same documentation is emitted to the generated gRPC proto where applicable.
Treat descriptions, response codes, and OAuth scopes as part of the consumer
experience; review them with the contract change.

Add a hand-written operation transformer only for document changes that cannot
be described by contract metadata. Architecture rationale: [design.md](../design.md).
