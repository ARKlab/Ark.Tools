# Task 5 Report

## Files

- `src/mediator-framework/Ark.Tools.MediatorFramework.Mcp.Generators/McpToolGenerator.cs`
  - Restricted the type-targeted MCP marker pipeline to `TypeDeclarationSyntax`.
  - Added the `McpMarkerParser` tracking name.
- `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`
  - Extended invalid-target coverage to the MCP generator.
  - Added a tracked-stage assertion proving invalid method targets never reach the MCP marker parser.
  - Reused one named-stage output helper for invocation filtering and cache assertions.

The starting revision already contained the Task 5 type filters for Minimal API, gRPC, Rebus, API surface, Azure Functions endpoints, and messaging networks; the syntax-only Minimal API/gRPC/Rebus mapping filters; and the named gRPC cache test.

## TDD Evidence

- Red: the new MCP parser-filter test failed because `McpMarkerParser` did not exist.
- Mutation red: with the tracked stage present but the predicate restored to `true`, the test failed with two parser outputs (valid type and invalid method).
- Green: `McpGeneratorParsesOnlyTypeAttributeTargets` passed with one parser output.

## Verification

- Task 5 focused tests: 4 passed.
- Generator-focused tests: 123 passed.
- Full `Ark.Tools.MediatorFramework.Tests`: 403 passed.
- Full solution build: succeeded with 0 warnings and 0 errors after restoring missing assets.
- Inspected emitted Azure Functions, messaging, and Rebus `.g.cs` outputs.

Commands used the .NET 10 Microsoft Testing Platform form:

```text
dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore --filter "FullyQualifiedName~Generator"
dotnet test --project tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore
dotnet restore -m:1 --verbosity quiet
dotnet build --no-restore -m:1 --verbosity quiet
```

## Tracking Names

- Mapping filters: `MinimalApiMappingParser`, `GrpcMappingParser`, `RebusMappingParser`
- Attribute filter: `McpMarkerParser`
- Cache proof: `GrpcEndpointParser`, `GrpcModel`, `GrpcOutput`

## Commit

- `a2e80b74f7827c97adc08538f21e00020c901ad3` — `fix(Mediator): filter generator inputs safely`

## Self-review

- Scope is limited to generator input safety and reusable named-stage test infrastructure.
- Valid generated output and diagnostics remain covered.
- No dependencies or public APIs changed.
- Secret scan found no secrets.

## Concerns

- The requested positional `dotnet test <project>` syntax is incompatible with this repository's Microsoft Testing Platform configuration; the equivalent `--project`/`FullyQualifiedName` form passed.
- Parallel solution restore/build attempts exposed existing package-lock/project-reference races; sequential restore/build passed.
- Automated code review was unavailable because its configured model was missing, and CodeQL timed out.
