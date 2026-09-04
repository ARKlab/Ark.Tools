# PII-IMP-02 — Sensitive value-object generator

**Category**: compliance-generator · **Priority**: high
**Depends on**: PII-IMP-01
**Scope**: SOURCE GENERATOR + TESTS
**Design**: [Sensitive value objects](../../../privacy-by-default-prd.md#62-sensitive-value-objects),
[Why not build on Vogen](../../../privacy-by-default-prd.md#14-value-objects-why-not-build-on-vogen)

## Problem

A classified `string` is still a `string`: it interpolates, concatenates, and
prints itself in every debugger, log, and exception. The value object is what
makes the *default* rendering safe, so that a missed analyzer rule degrades to a
mask rather than a leak.

## Execution map

- **Attribute**: `[SensitiveValueObject<T>(ArkRedaction redaction, …)]` on a
  `readonly partial struct`; `T` limited to `string` in this task.
- **Generated members**: `From`/`TryFrom` with optional `_validate`/`_normalize`
  hooks, equality and hash over the normalised value, `IFormattable` where the
  default and `"R"` formats are redacted, `ISpanFormattable`/`TryFormat` with the
  same redaction, and `Reveal(CompliancePurpose)` as the only cleartext accessor
  (decision PII‑03). No implicit or explicit conversion to `T` is generated.
- **Leak surfaces closed by construction**: `DebuggerDisplay` renders the
  redacted form; no `DebuggerTypeProxy`; the generated `TypeConverter`
  round-trips for model binding but `ConvertTo(string)` returns the redacted
  form unless the destination is the converter's own round-trip contract.
- **Default targets in this task**: `System.Text.Json` converter (closed
  generic, `JsonSerializerContext`-friendly) and the Dapper
  `SqlMapper.TypeHandler<T>`. Other targets are PII-IMP-03.
- **Built-in types** shipped with the package: `EmailAddress`, `PhoneNumber`,
  `PersonName`, `PostalAddressLine`, `NationalIdentifier`, `ApiKey`.
- **Diagnostics owned here**: generator-side errors for an unsupported
  underlying type, a non-partial or non-`readonly struct` declaration, and a
  user-declared cleartext `ToString`.

## Implementation steps

1. Add `Ark.Tools.Compliance.Generators` as an analyzer-asset project packaged
   inside `Ark.Tools.Compliance`.
2. Implement the incremental generator over the attribute, emitting one file per
   type with `#nullable enable` and full XML documentation.
3. Emit STJ and Dapper converters behind the default `SerializationTargets`.
4. Add the six built-in types with validation and normalisation.
5. Add snapshot tests for the emitted source, following the existing
   `GeneratorSnapshotTests` pattern.

## Required test coverage

- Snapshot of the generated source for each redaction mode.
- `ToString()`, string interpolation, `$"{value}"`, `string.Format`,
  `TryFormat`, and `DebuggerDisplay` all yield the redacted form.
- `Reveal(purpose)` is the only member returning cleartext; no implicit
  conversion to `string` compiles.
- STJ round-trip writes cleartext and rehydrates a value whose `ToString()` is
  still redacted.
- Dapper handler parameterises cleartext and reads back a redacted-rendering
  value.
- Built-in type validation rejects malformed input without echoing the input in
  the exception message.

## Outcomes

- Sensitive strings have a safe default rendering everywhere, including places
  no analyzer inspects.
- Cleartext access is explicit, greppable, and attributable to a purpose.

## Acceptance

- [x] The generator emits the full redacted surface for `string`-shaped types.
- [x] No generated member returns cleartext except `Reveal(CompliancePurpose)`.
- [x] STJ and Dapper round-trips are proven by tests.
- [x] Six built-in sensitive types ship with the package.
- [x] The [task board](../README.md) status for PII-IMP-02 matches this task.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero
  warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
  passes.
