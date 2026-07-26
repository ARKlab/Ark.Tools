# GEN-07 — Automatic proto export without host entry-point wiring

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

`Ark.Tools.MediatorFramework.Grpc` currently exports generated protobuf files by running the built
hosting application with `--ark-export-proto`. Every host must therefore intercept that private
command in its entry point:

```csharp
if (ArkProtoExport.TryHandle(args))
    return;
```

This leaks framework build plumbing into application startup. Omitting the interception starts the
application during `ArkExportProto`, which can hang the build or trigger real startup side effects.
The package also defaults `ArkExportProtoDir` for every consumer, even when the project generates no
gRPC services.

## Design

Proto export is a build concern owned by the gRPC package. Keep `ArkGeneratedProtos` as the generator's
in-assembly manifest, but replace host execution with a package-owned export runner invoked by
`buildTransitive/Ark.Tools.MediatorFramework.Grpc.targets` after `Build`. The runner receives
`$(TargetPath)` and `$(ArkExportProtoDir)`, loads the built assembly without invoking its entry point,
and exports only when `Ark.MediatorFramework.Generated.ArkGeneratedProtos` contains at least one
generated service proto.

The runner also writes the shared `ark/nodatime.proto` and `ark/mediator.proto` assets and preserves
`@(ArkAdditionalProto)` handling. A project with no generated `[GrpcMethod]` service is a successful
no-op: it creates no proto output solely because the package is referenced. `ArkExportProtoDir`
remains the destination override; `ArkExportProto=false` provides the explicit opt-out.

Do not parse generated C# in MSBuild and do not launch `$(TargetPath)`. Keep path traversal checks and
ensure loading the target assembly does not leave it locked after the export process exits.

## Steps

1. Add a package-owned executable export runner (or equivalent isolated build-time process) that
   accepts target assembly and destination paths, locates `ArkGeneratedProtos.GetFiles()`, and writes
   generated plus shared proto assets without executing the host entry point.
2. Change `buildTransitive/Ark.Tools.MediatorFramework.Grpc.targets` to invoke the runner after a
   successful build. Gate export on generated gRPC service assets, preserve incremental build
   behavior, and retain `@(ArkAdditionalProto)` support only when export is active.
3. Remove `ArkProtoExport.TryHandle(args)` from the sample `Program.cs`. Remove or reshape the public
   runtime API if it is no longer needed; any retained public API requires XML documentation and a
   consumer use case independent of the old CLI interception.
4. Add a package-consumer build test with generated gRPC services. Build it without entry-point
   interception and assert its service protos and shared imports are exported.
5. Add a package-consumer build test that references the gRPC package but generates no gRPC service;
   assert the build does not run the host and creates no automatic proto output.
6. Update `design.md`, package descriptions, and the sample README to describe automatic build export,
   destination override, opt-out, and additional hand-written proto behavior.

## Outcomes

- Projects that generate gRPC services automatically export their proto assets during build.
- Hosting projects contain no `ArkProtoExport.TryHandle` entry-point plumbing.
- Merely referencing the gRPC package does not produce proto output or execute application startup.

## Acceptance

- [ ] Sample `Program.cs` contains no `ArkProtoExport.TryHandle` call or proto-export argument handling.
- [ ] Building the sample exports all generated service protos, shared protos, and declared additional
      protos without launching the sample entry point.
- [ ] A consumer with generated gRPC services exports protos automatically with no startup changes.
- [ ] A consumer with no generated gRPC services produces no automatic proto output and has no startup
      side effects.
- [ ] Export destination override, opt-out, incremental rebuild, relative-path validation, and
      `@(ArkAdditionalProto)` behavior have automated tests.
- [ ] Full solution build + tests green; package/consumer tests exercise packed `buildTransitive`
      assets rather than only project-reference behavior.
