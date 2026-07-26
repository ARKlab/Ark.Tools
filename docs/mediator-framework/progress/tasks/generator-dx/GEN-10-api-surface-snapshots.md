# GEN-10 — API-surface snapshot gate (contracts, routes, gRPC methods)

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

Nothing detects a breaking wire change during development. Renaming a contract property, changing a
route template, reordering `[ProtoMember]` numbers or retiring a version compiles cleanly and only
breaks consumers at runtime. `Microsoft.CodeAnalysis.PublicApiAnalyzers` solves the equivalent problem
for library APIs (shipped/unshipped `.txt` files under source control, build error on drift) but it
tracks the C# public API, not routes, operation names, proto field numbers or queue names.

## Design

See `docs/mediator-framework/design.md` → *API surface snapshots*.

An analyzer ships with the framework (as an analyzer asset of the transport packages, like the
generators) and compares the **declared transport surface** against two `AdditionalFiles`:

- `ArkApiSurface.Shipped.txt` — released surface, edited only when a release is cut.
- `ArkApiSurface.Unshipped.txt` — surface added/changed since the last release.

Recorded per contract, one stable, sorted, human-readable line per entry:

```
HTTP GET /api/v1/greetings/{id} -> GetGreetingQuery : GreetingDto [policy=RequireAuthenticatedUser] [op=GetGreetingQuery] [tag=Greetings]
HTTP-PARAM GetGreetingQuery.Id : System.Guid route required
GRPC Greetings.V1/GetGreeting (GetGreetingQuery) returns (GreetingDto) unary
GRPC-FIELD GetGreetingQuery.Id = 1 : bytes
REBUS RefreshGreetingCommand -> queue:greetings
CONTRACT GreetingDto.Name : string? server-set=false
```

Diagnostics:

- `ARKAPI001` (error) — surface entry not declared in either file.
- `ARKAPI002` (error) — entry present in a file but no longer produced.
- `ARKAPI003` (warning) — entry present in both shipped and unshipped.
- `ARKAPI004` (error) — `$(ArkApiSurfaceEnforceRelease)` is `true` and
  `ArkApiSurface.Unshipped.txt` is non-empty; the release gate fires until all
  pending entries are moved to `ArkApiSurface.Shipped.txt`.

A code fix ("Add to unshipped API surface") writes the missing lines, so the developer's diff shows the
API change explicitly and reviewers see it in the pull request.

## Steps

1. New project `src/mediator-framework/Ark.Tools.MediatorFramework.ApiSurface.Analyzers` (analyzer +
   code fix, `netstandard2.0`, packed as an analyzer asset of `Ark.Tools.MediatorFramework`), following
   the existing generator projects' csproj conventions.
   Docs: [Roslyn analyzer + code fix tutorial](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix),
   [`AdditionalFiles`](https://learn.microsoft.com/dotnet/csharp/roslyn-sdk/tutorials/how-to-write-csharp-analyzer-code-fix#additional-files),
   [PublicApiAnalyzers design](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md)
   (reference for the shipped/unshipped file protocol and the code-fix UX to mirror).
2. Reuse the generators' semantic analysis to produce surface entries — extract it into a shared,
   source-linked helper rather than duplicating attribute parsing, so the snapshot cannot drift from
   what is actually generated.
3. Line format must be **deterministic and sorted** (ordinal), version-expanded (one line per active
   version), and independent of file order in the compilation.
4. `buildTransitive` props: auto-include `ArkApiSurface.*.txt` from the project directory as
   `AdditionalFiles` so consumers do not hand-wire item groups; the analyzer is a no-op (no diagnostics)
   when neither file exists **and** `$(ArkApiSurfaceRequired)` is not `true`, and errors when the files
   exist. Document how a project opts in (`ArkApiSurfaceRequired=true` + empty files).
5. Wire the release gate: `$(ArkApiSurfaceEnforceRelease)` defaults to `false`; set it to `true` in CI
   for pack/release jobs (e.g. `dotnet build -p:ArkApiSurfaceEnforceRelease=true`). When `true`,
   `ARKAPI004` fires on a non-empty `Unshipped.txt`, blocking release until entries are manually
   promoted to `Shipped.txt`. Document the promotion workflow in `design.md` and in the user guide.
6. Enable it in the sample's Application assembly with committed `ArkApiSurface.Shipped.txt` /
   `ArkApiSurface.Unshipped.txt`, proving the whole loop in-repo.
7. Document the workflow in `design.md` and in the user guide (DOC-01): change API → build fails →
   apply the code fix → the snapshot diff is part of the PR → on release, promote unshipped to
   shipped → `ARKAPI004` clears.

## Test coverage (required)

- Analyzer unit tests (`tests/Ark.Tools.MediatorFramework.Tests`) using the Roslyn testing harness:
  - missing entry → `ARKAPI001`; stale entry → `ARKAPI002`; duplicated entry → `ARKAPI003`;
  - non-empty unshipped + `ArkApiSurfaceEnforceRelease=true` → `ARKAPI004`; empty unshipped + same
    flag → no diagnostic;
  - complete files → no diagnostics;
  - versioned contract produces one entry per active version;
  - `[ServerSet]`, policy, tag/operation name and proto field numbers are part of the entry, so changing
    any of them triggers a diagnostic.
- Code-fix test asserting the generated unshipped file content is exactly the expected sorted lines.
- Repository-level proof: the sample's committed snapshot files match the current surface, so any future
  task that changes routes/protos fails the build until the snapshot is regenerated.

## Outcomes

- Any change to routes, operation names, contract members, proto messages/field numbers or Rebus queues
  fails the build until the developer regenerates the snapshot, making the wire-level diff explicit and
  reviewable in every pull request.
- No unreviewed surface change can ship: CI/CD pack/release jobs set `ArkApiSurfaceEnforceRelease=true`
  and fail until the developer explicitly promotes changes from `Unshipped.txt` to `Shipped.txt`.

## Acceptance

- [ ] Analyzer + code fix implemented, packed as an analyzer asset, no-op when not opted in.
- [ ] `ARKAPI001`/`ARKAPI002`/`ARKAPI003` behave as specified and are unit-tested.
- [ ] `ARKAPI004` fires when `ArkApiSurfaceEnforceRelease=true` and `Unshipped.txt` is non-empty;
      clears once the file is empty; unit-tested.
- [ ] Surface entries cover HTTP routes/params/policy/op name/tag, gRPC service+method+fields+streaming
      kind, Rebus queues and contract members.
- [ ] Sample Application assembly opts in with committed snapshot files; solution build is green with them.
- [ ] Workflow documented in `design.md` and referenced by the user guide task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
