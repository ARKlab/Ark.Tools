# SDK-IMP-07 — Application settings and Reqnroll profile

**Category**: content · **Priority**: productization
**Depends on**: SDK-IMP-04
**Scope**: SDK CONTENT ITEMS + REQNROLL PROPERTIES + TESTS
**Design**: [Late items and targets](../../design.md#late-items-and-targets),
[Accepted decisions](../../design.md#accepted-decisions)

## Problem

The current shared targets normalize application/test configuration files and
Reqnroll code-behind behavior. The SDK must preserve those semantics without
adding packages, duplicating default items, or affecting non-test projects with
Reqnroll-specific settings.

## Execution map

- **Application settings**: for all projects, remove conflicting default
  `Content` entries and include matching files once as `None`.
  `appsettings.json` and equivalent base matches copy always to output and
  publish; `appsettings.*.json` copies always to output and never to publish.
- **Application escape hatch**: one named switch suppresses all Ark
  application-settings item changes.
- **Reqnroll properties**: for detected tests only, set when empty
  `ReqnrollUseIntermediateOutputPathForCodeBehind=true` and
  `ReqnrollDeleteObsoleteCodeBehindFilesOnClean=true`.
- **Reqnroll content**: for detected tests only, copy `reqnroll*.json` always to
  output. A Reqnroll switch suppresses both properties and item behavior.
- **Test config**: for detected tests only, update `testconfig.json` to
  `CopyToOutputDirectory=PreserveNewest`, behind its own switch.
- **Inertness**: inject no Reqnroll or assertion package. Unmatched globs and
  absent Reqnroll targets produce no generated file, warning, or error.

## Implementation steps

1. Add item transforms that avoid duplicate `Content`/`None` identities under
   the .NET, Web, and Razor SDK default-item imports.
2. Preserve the exact output/publish distinction between base and
   environment-specific appsettings files.
3. Add test-only Reqnroll properties/items and independent Reqnroll/testconfig
   switches.
4. Extend fixtures for library, console, Web, Razor, plain test, and
   consumer-owned Reqnroll projects.
5. Inspect evaluated items plus built and published directories; do not rely
   only on property snapshots.

## Required test coverage

- Each appsettings file has one evaluated item and the accepted output/publish
  metadata under .NET, Web, and Razor SDKs.
- Disabling application-settings behavior leaves primary-SDK defaults intact.
- Non-test projects receive no Reqnroll properties or `reqnroll*.json` item
  changes.
- A plain test project without Reqnroll evaluates and builds cleanly.
- A consumer-owned Reqnroll project uses the two code-behind settings and
  cleans obsolete generated files.
- Reqnroll and testconfig switches affect only their own properties/items.
- No Reqnroll or AwesomeAssertions package appears unless the consumer declares
  it.

## Outcomes

- Application settings retain current output and publish behavior.
- Reqnroll projects are configured correctly while non-Reqnroll tests remain
  unaffected.
- Content handling composes with primary SDK default items without duplicates.

## Acceptance

- [ ] Appsettings output/publish semantics are tested for .NET, Web, and Razor.
- [ ] Reqnroll properties and content are test-only, inert, and independently
  disableable.
- [ ] Testconfig behavior and its switch are tested.
- [ ] Package graphs prove framework/assertion packages remain consumer-owned.
- [ ] Evaluated items contain no duplicates.
- [ ] The [task board](README.md) status for SDK-IMP-07 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
