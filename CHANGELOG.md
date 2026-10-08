# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- `ArkAdaptiveSampler` has a constructor that accepts a `TimeProvider`, so tests can control sampling time.
- `ArkGenerateComplianceSqlStandalone` returns the generated `policy-*.compliance.sql` files, so database projects can collect them without passing global properties. See the updated database project example in the SQL policies guide.

### Changed

- Lower per-call overhead on hot paths: root-span sampling in `ArkAdaptiveSampler`, SQL span filtering and query labels, ResourceWatcher activity tags, polymorphic JSON reads, `ToObject<T>` on `JsonElement` and `JsonDocument`, polymorphic `DataTable` shredding, the MVC ETag filter, Minimal API ETag and MessagePack negotiation, Storage Queue header decoding, gRPC business-rule errors, and authorization logging when Trace is off.
- The compliance analyzers do less work per operation, which shortens builds of projects that enable them.
- `Ark.Tools.Sdk` copies `appsettings*.json`, `reqnroll*.json` and `testconfig.json` to the output with `CopyToOutputDirectory=IfDifferent` instead of `Always`. An edited output copy is still restored, but unchanged files are no longer copied on every build, so no-op builds and the Visual Studio up-to-date check can skip the project. Requires MSBuild 17.13 or later.
- `EvolvableEnum` conversions (`FromValue`, the implicit and explicit operators, and `Value`) no longer allocate, and writing names to JSON and Dapper parameters allocates less.
- The MediatorFramework source generators (Minimal API, gRPC, Rebus, MCP, messaging network and Azure Functions) reuse their previous results when code in other files changes, so editing projects that use them is faster in the IDE. The generated code is unchanged.

### Fixed

- The API-surface snapshot records the fields of contracts marked only with `[Message]` or `[Event]`, so renaming or removing a field of such a message fails the API-surface gate again. Run the build with `EmitCompilerGeneratedFiles=true` and copy `ArkApiSurface.current.txt` over `ArkApiSurface.txt` to accept the new entries.
- `ArkAdaptiveSampler` measures elapsed time with a monotonic clock, so system clock changes no longer distort trace rate limiting.
