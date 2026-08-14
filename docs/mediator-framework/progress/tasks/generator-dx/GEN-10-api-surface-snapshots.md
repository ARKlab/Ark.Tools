# GEN-10 — API-surface snapshot gate (contracts, routes, gRPC methods)

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

Nothing detects a breaking wire change during development. Renaming a contract property, changing a
route template, reordering `[ProtoMember]` numbers or retiring a version compiles cleanly and only
breaks consumers at runtime. Changes to **nested** message fields — e.g., renaming a field inside a
type that is embedded in a response — are equally invisible.

## Design

See `docs/mediator-framework/design.md` → *API surface snapshots*.

The framework ships a generator + MSBuild target that maintains a **single committed snapshot file**
`ArkApiSurface.txt` in the project directory. Every build recomputes the current surface and fails
if it does not match the committed file. Acceptance is a copy-replace — no manual editing, no
shipped/unshipped split.

**On every build:**

1. A Roslyn `IIncrementalGenerator` traverses the compilation, computes the full sorted contract
   surface (including response contracts, transport metadata and Rebus queues), and emits it as a
   source hint internally. The `.g.cs` file is written to disk only when
   `EmitCompilerGeneratedFiles=true`; the package target supplies the output path.
2. When `EmitCompilerGeneratedFiles=true` is supplied transiently, a `buildTransitive` MSBuild
   target (running after `CoreCompile`) extracts the surface content from the generated output
   and writes `$(IntermediateOutputPath)/ArkApiSurface.current.txt`.
3. A comparison target diffs `ArkApiSurface.current.txt` against the committed `ArkApiSurface.txt`
   in `$(MSBuildProjectDirectory)`:
   - If `ArkApiSurface.txt` does **not exist** → `ARKAPI001` (error).
   - If the files **differ** → `ARKAPI002` (error) with a message pointing to the generated file.
   - If files match → no diagnostic; build proceeds.

**Acceptance workflow:**

```sh
# After any API-changing build failure:
dotnet build -p:EmitCompilerGeneratedFiles=true
cp obj/<Configuration>/<TargetFramework>/ArkApiSurface.current.txt ArkApiSurface.txt
git add ArkApiSurface.txt
```

The committed diff is the full, sorted, human-readable wire surface — reviewers see exactly what
changed without reading generator output. CI fails automatically on any uncommitted surface change,
at every stage (PR builds, release builds). The emission flag is only needed transiently when the
generated file must be inspected or copied.

**Entry format** (ordinal-sorted, deterministic, one entry per line):

```
CONTRACT GetGreetingQuery -> GreetingDto [group=Greetings] [grpc-group=Greetings] [http=GET /api/v{version}/greetings/{id}] [version=1+] [grpc=GetGreeting] [grpc-version=1+]
CONTRACT GetGreetingQuery.Id : Guid
CONTRACT GreetingDto.Name : string?
CONTRACT GreetingDto.Tags[].Value : string
REBUS RefreshGreetingCommand -> queue:greetings
```

**Nested field coverage.** `CONTRACT` entries recurse into embedded message types; every leaf
field in the transitive closure of a contract type produces its own line. A rename anywhere in
the graph produces a visible diff.

**Diagnostics:**

- `ARKAPI001` (error) — `ArkApiSurface.txt` does not exist; run
  `dotnet build -p:EmitCompilerGeneratedFiles=true`, then create it by copying from
  `obj/<Configuration>/<TargetFramework>/ArkApiSurface.current.txt`.
- `ARKAPI002` (error) — `ArkApiSurface.txt` differs from the current surface; run
  `dotnet build -p:EmitCompilerGeneratedFiles=true`, then copy
  `obj/<Configuration>/<TargetFramework>/ArkApiSurface.current.txt` over it to accept.

## Steps

1. New project `src/mediator-framework/Ark.Tools.MediatorFramework.ApiSurface` (`netstandard2.0`,
   packed as an analyzer/generator asset of `Ark.Tools.MediatorFramework`), following the
   existing generator projects' csproj conventions.
   Docs: [IIncrementalGenerator](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview),
   [MSBuild inline tasks](https://learn.microsoft.com/visualstudio/msbuild/msbuild-inline-tasks),
   [AdditionalFiles](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix#additional-files).
2. Reuse the generators' semantic analysis to produce surface entries — extract shared surface
   computation into a source-linked helper so the snapshot content cannot drift from what the
   generators actually emit.
3. Surface format rules:
   - Ordinal sort (deterministic across OSes and compilations).
   - Version-expanded: one set of entries per active `Versioning(Introduced, Retired)` version.
   - Recursive nested type traversal: every leaf field produces its own line, with the full
     dotted path (e.g. `CONTRACT Outer.Inner.Field : type`); collection members use `[]`
     notation (e.g. `CONTRACT Outer.Items[].Field : type`).
   - Lines are self-contained; no references between lines.
4. `buildTransitive` targets: the generator writes the current surface to a hint file during
   `CoreCompile`; when `EmitCompilerGeneratedFiles=true` is set, a post-compile target extracts
   it to `$(IntermediateOutputPath)/ArkApiSurface.current.txt`; a comparison target emits
   `ARKAPI001`/`ARKAPI002` and aborts the build on mismatch or missing committed file.
5. Opt-in: the comparison target is a no-op unless `ArkApiSurface.txt` is present **or**
   `$(ArkApiSurfaceEnabled)` is `true`. A project starts tracking by running the build once
   (which creates `ArkApiSurface.current.txt`), copying it to `ArkApiSurface.txt`, and
   committing. Document the bootstrap procedure in `design.md` and in the user guide.
6. Enable it in the sample's Application assembly with a committed `ArkApiSurface.txt`,
   proving the whole loop in-repo.
7. Document the full workflow in `design.md` and in the user guide (DOC-01):
   change API → build fails (`ARKAPI002`) → copy generated file → commit diff.

## Test coverage (required)

- Roslyn generator unit tests (`tests/Ark.Tools.MediatorFramework.Tests`):
  - Surface output is identical across repeated compilations of the same input (determinism).
  - Versioned contract produces one entry per active version.
  - `[ServerSet]`, policy, tag, operation name and proto field numbers appear in the surface.
  - Nested/embedded message fields produce `CONTRACT` and `GRPC-FIELD` entries with full paths.
  - Collection member fields use `[]` notation.
- MSBuild target tests (MSBuild `UsingTask` unit tests or end-to-end):
  - Missing `ArkApiSurface.txt` → `ARKAPI001`.
  - Committed file differs from current surface → `ARKAPI002`.
  - Committed file matches current surface → no diagnostic, build green.
  - Opting in via `ArkApiSurfaceEnabled=true` with no committed file → `ARKAPI001`.
- Repository-level proof: the sample's committed `ArkApiSurface.txt` matches the current
  surface; any future task that changes routes/protos causes `ARKAPI002` until the snapshot
  is updated.

## Outcomes

- Any change to routes, operation names, nested contract members, proto field numbers or Rebus
  queues fails the build immediately, with a diff-ready committed snapshot as the acceptance
  step.
- CI/CD gate is automatic at every stage: PR builds, release builds, pack jobs — no separate
  flag or promotion workflow needed.

## Acceptance

- [ ] Generator + MSBuild targets implemented, packed as build assets, no-op when not opted in.
- [ ] `ARKAPI001`/`ARKAPI002` behave as specified and are unit-tested.
- [ ] Nested type fields produce recursive `CONTRACT`/`GRPC-FIELD` entries; renaming a nested
      field causes `ARKAPI002`.
- [ ] Surface is deterministic across compilations (same input = same output, bit-for-bit).
- [ ] Sample Application assembly opted in with committed `ArkApiSurface.txt`; solution build
      is green with it.
- [ ] Bootstrap workflow documented in `design.md` and referenced by the user guide task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
