# Sensitive value objects

A sensitive value object is a `readonly struct` wrapping a validated string whose
`ToString()` is **always redacted**. The `Ark.Tools.Compliance` package ships an
incremental source generator that produces the full implementation from a small
declaration — no allocation, no reflection, AoT- and trim-clean.

## Built-in types

Most projects never write their own. `Ark.Tools.Compliance` ships:

| Type | Classification | Redaction | Validation |
| --- | --- | --- | --- |
| `EmailAddress` | `[PersonalData]` | Mask | RFC-shaped address, ≤ 320 chars, normalized to lower-case trimmed |
| `PhoneNumber` | `[PersonalData]` | Mask | 7–15 digits, optional leading `+`, separators stripped |
| `PersonName` | `[PersonalData]` | Mask | 1–200 chars, trimmed |
| `PostalAddressLine` | `[PersonalData]` | Erase | 1–500 chars |
| `NationalIdentifier` | `[SensitivePersonalData]` | Erase | 1–100 chars |
| `ApiKey` | `[UserCredentials]` | Erase | 1–4096 chars |

```csharp
var email = EmailAddress.From("Jane.Doe@Example.com");   // normalized, validated
EmailAddress.TryFrom(userInput, out var parsed);          // non-throwing

email.ToString();       // "***" — redacted, always
$"to: {email}";         // "to: ***" — interpolation is redacted too
email.Reveal(CompliancePurpose.SendTransactionalEmail);   // "jane.doe@example.com"
```

## Declaring your own

Apply `[SensitiveValueObject<string>]` to a **non-generic, non-nested,
`readonly partial struct`** (only `string` is supported as the underlying type),
together with the classification attribute:

```csharp
using Ark.Tools.Compliance;

[PersonalData]
[SensitiveValueObject<string>(ArkRedaction.Mask)]
public readonly partial struct CustomerReference
{
    // Optional hooks — omit either and a default is generated.
    private static ValidationResult _validate(string value)
        => value.StartsWith("CUST-", StringComparison.Ordinal)
            ? ValidationResult.Ok
            : ValidationResult.Invalid("Not a customer reference.");

    private static string _normalize(string value) => value.Trim().ToUpperInvariant();
}
```

The generator emits (in `{Type}.SensitiveValueObject.g.cs`):

- `From(string)` — normalize → validate → construct, `ArgumentException` on invalid;
- `TryFrom(string?, out T)` — non-throwing variant;
- `Reveal(CompliancePurpose purpose)` — the **only** cleartext access; throws if the
  purpose has an empty reason or `Unspecified` category;
- redacted `ToString()`, `IFormattable`, `ISpanFormattable`, and
  `DebuggerDisplay` — every formatting surface renders the redaction, never the value;
- `IEquatable<T>`, `==`/`!=`, `GetHashCode` over the normalized cleartext;
- `[JsonConverter]` (System.Text.Json) and `[TypeConverter]` applied automatically —
  JSON and model binding round-trip the **cleartext** (a serializer is a declared
  egress), while every display surface stays redacted;
- `ISensitiveValue<T>` — the contract all serializer adapters build on.

Wrong declarations are compile errors: `ARKPII201` (unsupported underlying type),
`ARKPII202` (not a non-generic `readonly partial struct`), `ARKPII203` (you declared
a cleartext `ToString()`), `ARKPII204` (a `_validate`/`_normalize` hook has the
wrong signature).

## Redaction modes

Chosen per type via the attribute argument:

| `ArkRedaction` | `ToString()` renders |
| --- | --- |
| `Erase` (default) | `***` |
| `Mask` | `***` (masking redactor; never emits source characters) |
| `Hmac` | `hmac:<64 hex chars>` — stable per key, so support can correlate values across log lines without learning them. Reads the key from the `ARK_TOOLS_COMPLIANCE_HMAC_KEY` environment variable; **without a key it fails closed to `***`**. |
| `None` | cleartext — only for types that are classified but deliberately loggable |

## Revealing cleartext

`Reveal` requires a `CompliancePurpose` carrying a GDPR-style category:

```csharp
// Well-known purpose:
email.Reveal(CompliancePurpose.SendTransactionalEmail);

// Custom purpose — category is mandatory and cannot be Unspecified:
author.Reveal(CompliancePurpose.Custom(
    "Compose the Book description", CompliancePurposeCategory.TechnicalFunctional));
```

Categories: `TechnicalFunctional`, `TechnicalTelemetry`, `Marketing`,
`LegalObligation`, `Security`, `CustomerSupport`, `Analytics`.

`Reveal` calls are deliberately greppable — auditing cleartext access across a
codebase is one search.

## Serializer adapters

The core package has **no serialization dependencies**. Each transport gets a small
adapter package which you opt into and register at startup; all of them write the
**cleartext** value on the wire (an explicit, inventoried egress) and re-validate on
read.

| Package | Registration |
| --- | --- |
| `Ark.Tools.Compliance.Dapper` | `SensitiveValueDapper.RegisterBuiltIn();` and `SensitiveValueDapper.Register<CustomerReference>();` |
| `Ark.Tools.Compliance.NewtonsoftJson` | `SensitiveValueNewtonsoftJson.RegisterBuiltIn(settings);` / `Register<T>(settings)` |
| `Ark.Tools.Compliance.MessagePack` | add `SensitiveValueFormatterResolver.Instance` to the resolver chain; `SensitiveValueFormatterResolver.RegisterBuiltIn();` / `Register<T>()` |
| `Ark.Tools.Compliance.Protobuf` | `RuntimeTypeModel.Default.RegisterBuiltIn();` / `.Register<T>()` |
| `Ark.Tools.Compliance.Reqnroll` | `SensitiveValueReqnroll.Register();` in test setup — feature tables then bind columns to sensitive value objects |

System.Text.Json and `TypeConverter` need no registration — the generator applies
the converters directly on the struct.

Group the registrations in one startup class per serializer, implementing
`ISensitiveValueSerializerRegistration` so they are easy to find:

```csharp
public sealed partial class AppDapperCompliance : ISensitiveValueSerializerRegistration
{
    public static void Register()
    {
        SensitiveValueDapper.RegisterBuiltIn();
        SensitiveValueDapper.Register<CustomerReference>();
    }
}
```

## Test data

Never use real-looking values in fixtures — `ARKPII006` flags plausible-real
literals (corporate email domains, checksum-valid IBANs/tax codes). Use the
deterministic fakes, which stay inside RFC 2606 reserved domains and reserved
number ranges:

```csharp
var email = ComplianceFakes.Email(seed: 1);        // "john.doe@example.org" — reserved domain
var phone = ComplianceFakes.PhoneNumber(seed: 1);  // reserved +1 555 01xx range
var name  = ComplianceFakes.PersonName(seed: 1);
```
