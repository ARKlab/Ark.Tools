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

## Acceptance

- [ ] SQL generation is opt-in per type and per column, with verbatim names.
- [ ] `ARKPII007` is scoped to `[SqlDataPolicy]` types; `ARKPII012` covers other
  egresses.
- [ ] The emitted script is a token template with a documented substitution
  path.
- [ ] The [task board](../README.md) status for PII-IMP-08 matches this task.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
