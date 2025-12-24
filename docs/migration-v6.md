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

## Specflow removal - Migrate to Reqnroll

Follow the instructions in the [v5 migration](migration-v5.md) to replace Specflow with Reqnroll in your projects

### Optional: Rename SpecFlow references to IntegrationTests

If you were using `SpecFlow` in environment names, configuration files, or test passwords, consider renaming them to more generic terms to align with the Reference project:

1. **Environment variable**: Change `ASPNETCORE_ENVIRONMENT` from `SpecFlow` to `IntegrationTests`
2. **Configuration file**: Rename `appsettings.SpecFlow.json` to `appsettings.IntegrationTests.json`
3. **Test database password**: Update passwords from `SpecFlowLocalDbPassword85!` to `IntegrationTestsDbPassword85!` in:
   - Docker Compose files
   - CI/CD workflows
   - Test configuration files
   - Database connection strings in code

## Migrate to MTPv2

Update `global.json` with

```json
    "test": {
        "runner": "Microsoft.Testing.Platform"
    }
```

Update `<test_project>.csproj` with these new sections.

```xml

  <PropertyGroup Label="Test Settings">
    <IsTestProject>true</IsTestProject>
    
    <OutputType>Exe</OutputType>

    <EnableMSTestRunner>true</EnableMSTestRunner>

    <ExcludeByAttribute>Obsolete,GeneratedCodeAttribute</ExcludeByAttribute>
    <PreserveCompilationContext>true</PreserveCompilationContext>

  </PropertyGroup>

  <ItemGroup Label="Testing Platform Settings">
    <PackageVersion Include="Microsoft.Testing.Platform" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.CrashDump" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.1.0" />
    <PackageReference Include="Microsoft.Testing.Extensions.HangDump" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.HotReload" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.Retry" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="2.0.2" />
    <PackageReference Include="Microsoft.Testing.Extensions.AzureDevOpsReport" Version="2.0.2" />
  </ItemGroup>


```

Update the CI pipeline to use dotnet test

```yaml
      - task: DotNetCoreCLI@2
        displayName: 'Run tests'
        inputs:
          command: 'test'
          projects: ${{ variables.solutionPath }}
          arguments: '--configuration $(BuildConfiguration) --no-build --no-restore --report-trx --coverage --crashdump --crashdump-type mini --hangdump --hangdump-timeout 10m --hangdump-type mini --minimum-expected-tests 1'
          publishTestResults: true
```

Refer to Ark.Reference project or to [official documentation](https://learn.microsoft.com/en-us/dotnet/core/testing/microsoft-testing-platform-intro?tabs=dotnetcli).

## Migrate from SLN to SLNX

Use `dotnet sln migrate` to migrate it.

Update the CI Pipelines to reference the new SLNX file.

More info [here](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/#getting-started)

## Update .editorconfig and Directory.Build.* (recomended) 

Copy `.editorconfig` and `Directory.Build.props` and `Directory.Build.targets` from `samples/Ark.Reference` project into your solution folder.
