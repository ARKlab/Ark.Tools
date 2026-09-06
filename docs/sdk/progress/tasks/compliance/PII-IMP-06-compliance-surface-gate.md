# PII-IMP-06 — Compliance surface inventory and gate

**Category**: compliance-tooling · **Priority**: medium
**Depends on**: PII-IMP-04
**Scope**: SOURCE GENERATOR + ANALYZER RULES + CI GATE + TESTS
**Status**: Implemented; full-solution acceptance remains pending.
**Design**: [Compliance inventory](../../../privacy-by-default-prd.md#610-compliance-inventory),
[Decision PII‑04](../../../privacy-by-default-prd.md#17-decisions)

## Problem

Classification that nobody reviews decays. A committed, diffable inventory turns
"what personal data does this service hold?" from an archaeology exercise into a
file, and makes every addition a reviewed change.

## Execution map

- **`ArkComplianceSurface.txt`**, deterministic and stable-sorted, one line per
  classified member: declaring type, member, classification, purpose notes, and
  the egress targets it is serialised to.
- **Separate from `ArkApiSurface.txt`** (decision PII‑04): different audience and
  cadence, and a privacy diff must not hide inside an API diff.
- **`ARKPII020`**: the committed surface file does not match the compilation —
  new or changed classified data was not reviewed.
- **`ARKPII021`**: a member was removed from the surface while still classified,
  or its classification was weakened.
- **Workflow**: follow the existing `ArkApiSurface.txt` baseline flow, with its
  own generated intermediate and committed baseline per project.

## Implementation steps

1. Implement the generator, reusing the `ApiSurfaceGenerator` determinism rules.
2. Implement the two comparison diagnostics against the committed baseline.
3. Emit the generated inventory to the intermediate output and document the
   manual baseline-copy flow.
4. Let the ordinary CI build enforce every committed baseline.

## Required test coverage

- Byte-identical output across repeated builds and across target frameworks.
- Adding a classified member without updating the baseline fails the build with
  `ARKPII020`; manually copying the generated intermediate inventory fixes it.
- Weakening a classification is reported by `ARKPII021`.
- Egress targets from PII-IMP-03 appear on the member's line.

## Outcomes

- A reviewable, committed record of personal data per assembly.
- Evidence usable for GDPR Article 30 records of processing.

## Implemented workflow

`ComplianceSurfaceGenerator` ships in the existing compliance generator assembly.
Its `buildTransitive/Ark.Tools.Compliance.Surface.targets` enables the gate when
imported, adds the baseline as an `AdditionalFile`, and makes the gate properties
visible to Roslyn. The compliance package/SDK wires this target into consumers;
source-project references must import it explicitly because `ProjectReference`
does not flow NuGet build assets.

```sh
# Normal compilation rejects unreviewed additions, removals, or changes.
dotnet build path/to/Service.csproj

# Generate the reviewed inventory without applying it.
dotnet build path/to/Service.csproj -p:ArkComplianceSurfaceUpdating=true

# Copy the generated intermediate inventory to the committed baseline.
cp path/to/Service/obj/Debug/net10.0/ArkComplianceSurface.current.txt \
   path/to/Service/ArkComplianceSurface.txt
```

The updating property bypasses only the two baseline diagnostics, not unrelated
compiler/analyzer errors. Copy the generated inventory to
`ArkComplianceSurface.txt` only after inspecting the diff. Normal builds write
`obj/<configuration>/<framework>/ArkComplianceSurface.current.txt`;
the generated `ArkComplianceSurface.g.cs` remains inspectable under the configured
compiler-generated-files directory even when baseline drift stops compilation.
Like the existing API snapshot, the accepted file may include the generated C#
comment wrapper. A header-only inventory is valid.

`ArkComplianceSurfaceEnabled=false` is an explicit migration opt-out, not an
acceptance operation. The global `EnableArkToolsCompliance=false` switch
overrides the surface gate. The ordinary CI build rejects drift or a missing
baseline in newly opted-in consumers.

### Inventory format and coverage

The versioned format starts with `COMPLIANCE-SURFACE 1`. Each subsequent line has
six tab-separated columns:

```text
CLASSIFIED    declaring-type    member    classifications    notes    egress
```

The spaces above illustrate column boundaries; actual separators are tabs.
Columns, classifications, notes, and egress are ordered ordinally. No timestamp,
assembly version, target framework, machine path, or current culture participates.
Backslashes, tabs, line breaks, and comment terminators in notes are escaped.
UTF-8 output is byte-identical between .NET 8 and .NET 10 for the same declared
surface. CRLF and LF baselines are accepted.

- Fields, properties (including positional records), and declared method
  parameters are included when classified directly, through their containing
  type, or through their value-object/nullable/collection element type.
  Inherited class and overridden property classifications are retained.
  Compiler-generated backing fields are excluded.
- The four Ark classifications are tracked independently. Removing a
  classification or downgrading `SensitivePersonalData` to `PersonalData` or
  `PersonalData` to `Pseudonymous` produces `ARKPII021`, alongside ordinary drift.
  `Secret` is not interchangeable with personal-data classifications.
- A missing/malformed/duplicate baseline or any changed entry produces
  `ARKPII020`. An entry absent from an existing baseline additionally produces
  `ARKPII021`: without historical source, a new classified declaration and a
  manually omitted classified member cannot safely be distinguished.
- Notes contain XML summaries and named/constant purposes from direct
  `member.Reveal(...)` calls. Dynamic purpose arguments are recorded as dynamic,
  not evaluated.
- Egress includes the in-box sensitive-value JSON converter, explicit
  `Register<T>`/`RegisterBuiltIn` registrations for Dapper, Newtonsoft.Json,
  protobuf-net, and MessagePack, explicit serializer member attributes, and
  reachable HTTP/gRPC/Rebus/message/event contract DTOs. Known ignore attributes
  suppress the corresponding serializer, and ignored HTTP/gRPC DTO paths do not
  propagate their transport target.

This is a static declared-capability inventory, not runtime taint tracking.
Reflection-only serializer registration, dynamically selected serializers,
indirect/local-variable `Reveal` aliases, custom classification taxonomies, and
runtime transport configuration require separate review. Framework-conditional
declarations that genuinely differ intentionally fail verification against a
single shared baseline.

### Focused validation

- Generator and compliance test-project builds succeeded with zero warnings.
- All 18 surface regression tests passed. They cover deterministic ordering, framework metadata,
  source-compilable output, missing/matching/malformed/duplicate baselines,
  acceptance mode, additions, classification removal/weakening/strengthening,
  nullable/collection/record/inheritance handling, purpose-note escaping,
  serializer registrations and ignores, and transport graph traversal.
- A standalone consumer and an automated MSBuild regression fixture were restored
  and built against real `net8.0;net10.0`
  reference assemblies: the missing baseline failed with `ARKPII020`, the update
  target repaired it, verification succeeded, and `cmp` confirmed byte-identical
  inventories. Both emitted `.g.cs` files were inspected.
- Full-solution build/test commands were deliberately not run by this task.

## Acceptance

- [x] `ArkComplianceSurface.txt` is generated deterministically and separately
  from the API surface.
- [x] `ARKPII020/021` gate baseline drift.
- [x] The update target and CI step exist and are documented.
- [x] The [task board](../README.md) status for PII-IMP-06 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
