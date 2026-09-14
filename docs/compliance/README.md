# Ark.Tools.Compliance — Consumer Guide

The Compliance kit protects personal data (PII) and secrets in .NET applications with
five cooperating layers. A leak has to pass all of them:

1. **Declare** — classification attributes and sensitive value objects mark what is
   personal data, sensitive data, a user credential, an infrastructure secret, or pseudonymous.
2. **Refuse** — Roslyn analyzers turn "classified value reaches a log/exception/telemetry
   sink" into a compile error (`ARKPII*` diagnostics).
3. **Inventory** — a generated, committed `ArkComplianceSurface.txt` baseline: new or
   changed personal data cannot enter the codebase without a visible diff. This supports
   the records of processing activities required by [GDPR Art. 30](https://gdpr-info.eu/art-30-gdpr/).
4. **Enforce downstream** — generated SQL Server sensitivity classification and dynamic
   data masking DDL; serializer adapters for every transport Ark uses.
5. **Redact at runtime** — sensitive value objects render redacted by `ToString()`;
   an optional NLog PII scan catches what the analyzers cannot see.

## Packages

| Package | What it gives you |
| --- | --- |
| `Ark.Tools.Compliance` | Classification attributes, `Ark` taxonomy, redactors, `CompliancePurpose`, built-in sensitive value objects, the `[SensitiveValueObject<T>]` source generator, `System.Text.Json`/`TypeConverter` support. No serialization dependencies. |
| `Ark.Tools.Compliance.Analyzers` | The `ARKPII*` analyzers and code fixes plus the default lexicon/sinks configuration. Development dependency only (`PrivateAssets="all"`), nothing ships at runtime. |
| `Ark.Tools.Compliance.Sql` | `[SqlDataPolicy]`/`[SqlColumnPolicy]` attributes and the MSBuild targets that turn them into SQL Server sensitivity-classification and masking scripts. |
| `Ark.Tools.Compliance.NLog` | `ComplianceLayout` wrapper and PII scanning for NLog output. Referenced automatically by `Ark.Tools.NLog`. |
| `Ark.Tools.Compliance.Dapper` | Dapper `TypeHandler` for sensitive value objects. |
| `Ark.Tools.Compliance.NewtonsoftJson` | Newtonsoft.Json converter for sensitive value objects. |
| `Ark.Tools.Compliance.MessagePack` | MessagePack formatter + resolver for sensitive value objects. |
| `Ark.Tools.Compliance.Protobuf` | protobuf-net surrogate registration for sensitive value objects. |
| `Ark.Tools.Compliance.Reqnroll` | Value retriever/comparer so feature-file tables bind sensitive value objects. |

## Guide

- [Getting started](getting-started.md) — project setup, stand-alone and with `Ark.Tools.Sdk`.
- [Classifying data](classifying-data.md) — attributes vs sensitive value objects, and when to use which.
- [Sensitive value objects](sensitive-value-objects.md) — declaring your own, built-in types, `Reveal`, serializer adapters.
- [Analyzers](analyzers.md) — diagnostic reference, extending the lexicon and sinks, escape hatches, the compliance surface baseline.
- [SQL storage policies](sql-policies.md) — generating sensitivity classification and masking DDL, including the `.sqlproj` setup.
- [Runtime redaction](runtime-redaction.md) — redactors, NLog integration, the PII scanner, DI registration.
- [Configuration reference](configuration.md) — every MSBuild property, item, and environment variable.

## The 60-second picture

```csharp
using Ark.Tools.Compliance;

public sealed record Customer
{
    public Guid Id { get; init; }

    // A built-in sensitive value object: validated, normalized, redacted ToString().
    [PersonalData]
    public EmailAddress Email { get; init; }

    // A plain classified property: the analyzers guard every sink it flows into.
    [SensitivePersonalData]
    public string? DietaryNotes { get; init; }

    // Reviewed statement that a PII-looking member is fine.
    [NotPersonalData("Internal segment label, never contains customer input.")]
    public string SegmentName { get; init; } = default!;
}
```

```csharp
_logger.Info(CultureInfo.InvariantCulture, "Processing {CustomerId}", customer.Id); // OK
_logger.Info(CultureInfo.InvariantCulture, "Processing {Email}", customer.Email);  // OK: renders masked
_logger.Info(CultureInfo.InvariantCulture, "Notes {Notes}", customer.DietaryNotes);
// error ARKPII002: classified data in logging
```

Cleartext is always available — but only through an explicit, greppable call that
states *why*:

```csharp
var to = customer.Email.Reveal(CompliancePurpose.SendTransactionalEmail);
```
