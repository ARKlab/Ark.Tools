# Migration to Ark.Tools v6

## SDK-based SQL Projects

If you are using SDK-based SQL projects in VS 2025+ you need to add 
the following to your csprojs that depends on the SQL Projects (generally Tests projects) to avoid build errors:

```xml
<ProjectReference Include="..\Ark.Reference.Core.Database\Ark.Reference.Core.Database.sqlproj">
    <ReferenceOutputAssembly>false</ReferenceOutputAssembly>
</ProjectReference>
```

## OpenAPI 3.1 / Swashbuckle 10.x

Refer to [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md) for issues related to OpenApi.

The most likely change is from:
```csharp
c.OperationFilter<SecurityRequirementsOperationFilter>();
```

to:
```csharp
c.AddSecurityRequirement((document) => new OpenApiSecurityRequirement()
{
    [new OpenApiSecuritySchemeReference("oauth2", document)] = ["openid"]
});
```

## Replace FluentAssertions with AwesomeAssertions

Replace the following:

- `PackageReference` from `FluentAssertions` to `AwesomeAssertions >= 9.0.0`
- `PackageReference` from `FluentAssertions.Web` to `AwesomeAssertions.Web`
- `HaveStatusCode(...)` => `HaveHttpStatusCode`
- `using FluentAssertions` => `using AwesomeAssertions`
