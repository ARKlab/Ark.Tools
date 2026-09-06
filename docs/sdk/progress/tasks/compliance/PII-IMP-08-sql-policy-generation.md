# PII-IMP-08 — SQL policy attributes and opt-in script generation

**Category**: compliance-persistence · **Priority**: medium
**Depends on**: PII-IMP-04
**Scope**: NEW PACKAGE + ANALYZER RULES + GENERATOR + TESTS
**Design**: [Persistence policy](../../../privacy-by-default-prd.md#66-persistence-policy),
[Decision PII‑05](../../../privacy-by-default-prd.md#17-decisions)

## Problem

PII stored unmasked is the failure that survives every code review, because
nothing in the C# build has an opinion about a column. The mapping is not
convention-based, so the design refuses to guess: policy is declared or it is
not generated.

## Execution map

- **`[SqlDataPolicy(Schema = …, Table = …)]`** opts a type in. Without it a
  classified type is still inventoried and still protected everywhere else; it
  simply produces no SQL.
- **`[SqlColumnPolicy("email_address", StoragePolicy.Masked, …)]`** carries the
  column name **verbatim** — never derived from the property name — and can
  override schema/table per member for split mappings.
- **`ARKPII007`**: a classified member inside a `[SqlDataPolicy]` type with no
  column policy. It fires only there.
- **`ARKPII012`**: classified data crossing an egress with no declared policy,
  covering DTOs and messages that have no SQL mapping.
- **Generated artifact** (decision PII‑05): an opt-in `.sql` **template** using
  SQLCMD variables, applied via SqlPackage/`sqlcmd` or replaced at build time by
  `ArkComplianceSqlToken` MSBuild items, because schemas and label taxonomies
  differ per environment and tenant.

## Implementation steps

1. Add `Ark.Tools.Compliance.Sql` with the two attributes and the
   `StoragePolicy` enum (`None`, `Masked`, `ApplicationEncrypted`).
2. Implement the two diagnostics.
3. Implement the template generator with deterministic ordering, emitting
   dynamic data masking and sensitivity-classification statements.
4. Implement token substitution as MSBuild items and document the SQLCMD path.

## Required test coverage

- A classified type with no `[SqlDataPolicy]` emits nothing and reports nothing
  from `ARKPII007`.
- A `[SqlDataPolicy]` type with an unpolicied classified member fails with
  `ARKPII007`.
- Column names are taken verbatim; a property rename does not change the emitted
  column.
- The emitted script is deterministic and contains unresolved tokens until
  substitution; substituted output is valid T-SQL against the reference
  database.
- `ARKPII012` fires for an undeclared egress and is silenced by a declared
  policy.

## Outcomes

- Storage policy is declared next to the data and generated from one source.
- No generated SQL is ever based on a guessed table name.

## Implementation and deployment

`Ark.Tools.Compliance.Sql` provides `SqlDataPolicyAttribute`,
`SqlColumnPolicyAttribute`, `StoragePolicy`, and the `SqlMask.Default` /
`SqlMask.Email` constants. The generator is shipped with the existing compliance
generator. A type must opt in and each emitted column must have an explicit
column policy. Schema/table overrides support split mappings. An omitted schema
uses `$(ComplianceSchema)`; an omitted label uses `$(ComplianceLabel)`. A table is
never inferred. Invalid or ambiguous mappings fail with `ARKPII207`.

The generator produces a deterministic C# comment manifest, not a side-effecting
file write. The package's `buildTransitive/Ark.Tools.Compliance.Sql.targets`
materializes its SQL payloads under
`obj/<configuration>/<target-framework>/ArkCompliance.Sql/`. Each file has a
stable `policy-<type-identity-hash>.compliance.sql` name. The
`ArkComplianceSqlScript` item exposes the resulting files; set
`ArkComplianceSqlOutputPath` to override the destination. Removing a mapping also
removes its previously generated SQL artifact. Project-reference consumers must
import the target explicitly, because NuGet build assets do not flow through
project references.

To leave a template for SQLCMD, do not supply replacement items. For example:

```sh
sqlcmd -b -S localhost -d Reference \
  -i "obj/Debug/net10.0/ArkCompliance.Sql/policy-<hash>.compliance.sql" \
  -v ComplianceSchema="sales" ComplianceLabel="Confidential - GDPR"
```

Alternatively, include the generated template from a database project's
post-deployment script and provide the corresponding SQLCMD variables to
SqlPackage. SQLCMD substitution is textual: deployment variable values must be
trusted and already escaped for their SQL context. Prefer build-time items for
values containing brackets or quotes:

```xml
<ItemGroup>
  <ArkComplianceSqlToken Include="ComplianceSchema" Value="sales" />
  <ArkComplianceSqlToken Include="ComplianceLabel" Value="Customer's confidential data" />
</ItemGroup>
```

Build-time replacement escapes `]` inside identifiers and `'` inside literals,
preserves unspecified tokens, and rejects duplicate token names, control
characters, and replacement values that introduce another SQLCMD variable.
The generated statements classify data and apply dynamic data masks; they do
not create tables or columns. `StoragePolicy.None` records an explicit unmasked
decision. `ApplicationEncrypted` records classification without emitting a mask
or pretending to provision encryption or keys; application encryption handlers
and Always Encrypted DDL are not implemented by this task.

`ARKPII007` is an error for an unpolicied classified member of an opted-in type,
including inherited members and nullable sensitive value objects. `ARKPII012`
is a warning at recognized JSON serialization, Rebus message, MVC HTTP-action,
and Ark `HttpEndpoint` contract boundaries. A nonempty
`[PersonalDataEgress(Purpose = "...")]` on the contract or boundary records the
purpose. Arbitrary custom transports and cross-method value provenance are not
inferred by this rule.

## Acceptance

- [x] SQL generation is opt-in per type and per column, with verbatim names.
- [x] `ARKPII007` is scoped to `[SqlDataPolicy]` types; `ARKPII012` covers other
  egresses.
- [x] The emitted script is a token template with a documented substitution
  path.
- [x] The [task board](../README.md) status for PII-IMP-08 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.

Focused validation: `dotnet build
tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
--configuration Debug --no-restore` succeeds with zero warnings; `dotnet test
--project tests/Ark.Tools.Compliance.Sql.Tests/Ark.Tools.Compliance.Sql.Tests.csproj
--no-build --configuration Debug --minimum-expected-tests 1` passes all 31 tests.
Generated `.g.cs` manifests and materialized escaped SQL were inspected.
The SQL package builds for both `net8.0` and `net10.0`; its packed NuGet archive
was inspected to confirm both assemblies and the build-transitive target.
Full-solution validation and executing substituted SQL against the reference SQL
Server database remain unverified here.
