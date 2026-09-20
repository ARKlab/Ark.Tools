# Migration to Ark.Tools v7

This guide covers the v6.0.6-to-v7 beta upgrade. Adopt the sections that apply
to the application; the SDK, Compliance, Mediator Framework, and OTel changes
can be introduced independently.

## 1. Adopt `Ark.Tools.Sdk`

Pin the released v7 SDK version in `global.json`:

```json
{
  "msbuild-sdks": {
    "Ark.Tools.Sdk": "<v7-beta-version>"
  }
}
```

Add it beside the primary SDK in every project that needs the full profile:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Sdk Name="Ark.Tools.Sdk" />
</Project>
```

The SDK supplies build safety defaults, analyzers, package and restore policy,
content metadata, and Microsoft Testing Platform defaults. Keep only
application-specific settings in `Directory.Build.props` and
`Directory.Build.targets`. The [SDK reference](sdk/reference.md) lists each
default and its opt-out switch.

## 2. Upgrade an ejected `Ark.ReferenceProject`

Compare an ejected solution with the current
[`samples/Ark.ReferenceProject`](../samples/Ark.ReferenceProject/), then:

1. Replace copied root build policy with `Ark.Tools.Sdk`. Delete copied
   `.editorconfig` and analyzer globalconfig files, `BannedSymbols.txt`, direct
   analyzer/SBOM/Polyfill references, and Microsoft Testing Platform extension
   references now supplied by the SDK.
2. Keep consumer-owned configuration: target frameworks, product version,
   project-specific `NoWarn`, MSTest, Reqnroll, AwesomeAssertions, and
   application content rules.
3. Update `global.json` to the supported .NET SDK and keep the Microsoft Testing
   Platform runner configuration.
4. Update Ark package versions in `Directory.Packages.props` to the released v7
   beta version. The sample's `999.9.9` package versions and commented SDK
   declaration are repository placeholders, not versions to copy.
5. Restore, build, and run the focused tests for changed projects.

The current sample removes 281 lines from its local build props and targets
compared with v6.0.6 by delegating common policy to the SDK.

## 3. Move from Application Insights v2 to OpenTelemetry

For an application without custom `TelemetryClient.Track*` calls or direct
Application Insights processors:

1. Add `Ark.Tools.AspNetCore.OTel`.
2. Call `builder.Services.AddArkAzureMonitorOpenTelemetry(builder.Configuration)`.
3. Configure `ApplicationInsights:ConnectionString` or
   `APPLICATIONINSIGHTS_CONNECTION_STRING`.
4. Add Rebus and ResourceWatcher OTel instrumentation explicitly where used.
5. Update dashboards and alerts to query OTel spans, metrics, attributes, and
   exception events instead of Application Insights item types.

If application code still uses `TelemetryClient.Track*`, retain explicit
Application Insights v3 packages and the matching Ark compatibility package
while those calls are migrated. Do not expect `Ark.Tools.AspNetCore`,
`Ark.Tools.AspNetCore.MinimalApi`, or
`Ark.Tools.ResourceWatcher.WorkerHost.Hosting` to register Application Insights
implicitly.

The [telemetry upgrade guide](otel/upgrade-guide.md) contains the complete
signal mapping and rollout checklist.

## 4. Opt into Compliance

Compliance is disabled by default while the analyzers are beta. To adopt it
solution-wide, set:

```xml
<PropertyGroup>
  <EnableArkToolsCompliance>true</EnableArkToolsCompliance>
</PropertyGroup>
```

Reference `Ark.Tools.Compliance` in projects that declare classified members.
Use `Ark.Tools.Compliance.Abstractions` for classification-only libraries, and
add `Ark.Tools.Compliance.Analyzers` explicitly when not using the SDK. Commit
the generated `ArkComplianceSurface.txt` baseline with the project.

See [Compliance getting started](compliance/getting-started.md) and the
[configuration reference](compliance/configuration.md).

## 5. Adopt Mediator Framework incrementally

The Mediator Framework is net10-only. Do not rewrite a controller layer at
once:

1. Keep existing application request/query contracts and handlers.
2. Mark one suitable contract with an HTTP endpoint attribute and generate the
   Minimal API endpoint.
3. Preserve authorization, validation, status-code, upload, and content
   negotiation behavior with integration tests.
4. Remove the controller and MVC registration only when every action has an
   equivalent generated endpoint.

Keep MVC as the adapter for protocol-specific behavior such as custom
formatters or a required compatibility boundary. The
[MVC migration guide](mediator-framework/migration-from-mvc.md) describes the
endpoint model and parity checks.

## 6. Validate the migration

```bash
dotnet restore
dotnet build --no-restore
dotnet test <affected-test-project> --no-restore
```

For telemetry, also validate span parentage, failed-request capture, Rebus
histograms, ResourceWatcher operation names, and updated alert queries in one
production-like instance before broad rollout.
