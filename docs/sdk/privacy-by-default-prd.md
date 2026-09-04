# PRD — Privacy by Default for Ark.Tools

Status: **approved**; research complete, review decisions applied (see
[Decisions](#17-decisions)); implementation tracked as the `PII-IMP` series on
the [SDK task board](progress/tasks/README.md#compliance-privacy-by-default).

Owner: Ark.Tools SDK. Scope: analyzers, source generators, value objects, NLog/OTel
pipelines, SQL policy generation, test data.

## 1. Problem

Personal data and secrets leak out of line-of-business code through paths that
compile cleanly and pass review:

| Mistake | Typical shape | Where it ends up |
| --- | --- | --- |
| Logging an email/phone | `_logger.Info(CultureInfo.InvariantCulture, "Login {User}", user.Email)` | Console, file, `Ark.Database` SQL target, Slack, mail |
| Logging a resolved secret | `_logger.Debug(CultureInfo.InvariantCulture, "Calling {Url}", urlWithSasToken)` | same, plus OTel spans |
| Logging the environment | `_logger.Info(CultureInfo.InvariantCulture, "Config {@Config}", config)` | whole connection strings |
| PII stored unmasked | `nvarchar` column, no masking, no classification | DB, backups, DBA access, exports |
| PII in exception text | `throw new EntityNotFoundException($"No user for {email}")` | logs, `ProblemDetails`, Slack |
| Real PII in test data | `.feature` tables and JSON fixtures with real names/IBANs | Git history, forever |

The common root cause is that **nothing in the toolchain knows which values are
personal**. Every safeguard available today is opt-in at the point of use, and
the point of use is exactly where the developer is not thinking about privacy.

Regulatory hooks that make this a product requirement, not a preference:
GDPR Art. 25 (data protection *by design and by default*), Art. 5(1)(c) (data
minimisation), Art. 32 (pseudonymisation), Art. 30 (records of processing);
OWASP Top 10 A02:2021 and A09:2021; CWE‑532 (sensitive info in log file),
CWE‑359, CWE‑312, CWE‑209; NIST SP 800‑122; ISO/IEC 27701.

## 2. Goals

1. A developer must **declare** what is personal. Undeclared, PII-looking members
   are a build diagnostic, not silence.
2. Once declared, **the compiler refuses** to let the value reach a log template,
   an exception message, a telemetry tag, or a plain `ToString()`.
3. Escape hatches exist, are explicit, are greppable, and carry a justification.
4. Runtime redaction is a **second net**, never the primary control, and fails
   closed (unknown classification ⇒ erased, not passed through).
5. Everything on the hot path is **compile-time**: analyzers, code fixes, source
   generators, interceptors. No reflection, no runtime type scanning, AoT- and
   trim-clean.
6. Serialization of personal data stays legal and easy (it is the point of the
   application) but becomes **declared and inventoried**.

## 3. Non-goals

- Detecting PII inside opaque payloads at runtime (ML/NER à la Presidio).
- Encrypting everything. Column encryption is opt-in per classified member.
- Replacing DB-side controls (Always Encrypted, Dynamic Data Masking); we
  *generate* and *verify* them, we do not reimplement them.
- Access control, retention, subject-access-request tooling, consent.
- Full inter-procedural taint analysis (see [§13 Rejected](#13-rejected-approaches)).

## 4. Research summary

Exhaustive survey of NuGet, `dotnet/extensions`, `dotnet/roslyn`, `github/codeql`,
SonarSource, Serilog/NLog/OpenTelemetry, value-object generators, and SQL Server
data-protection features. Full verdicts below; the actionable conclusions are:

**The space is essentially unoccupied.** A NuGet search for PII/redaction
analyzers returns 4 packages total; the only purpose-built compile-time one
(`LeakGuard` 0.4.0) has no public repository and ~92 downloads. There is no
maintained NLog masking library at all.

**Microsoft already shipped the reference model — for `[LoggerMessage]` only.**
`Microsoft.Extensions.Compliance.Abstractions` (10.9.0, MIT) defines
`DataClassification` (`readonly struct` of taxonomy + value) and the
`DataClassificationAttribute` base class; `Microsoft.Extensions.Compliance.Redaction`
provides `Redactor`, `ErasingRedactor`, `HmacRedactor` (the latter gated behind
`EXTEXP0002`) and `IRedactorProvider`. The compile-time enforcement lives in the
`LoggerMessage` generator inside `Microsoft.Extensions.Telemetry.Abstractions`:

- `LOGGEN035` *"The logging method parameter leaks sensitive data"* — fires when
  a logging-method parameter's type transitively contains a classified member,
  and the generator then **refuses to emit the partial method**, turning a
  warning into a build break.
- `LOGGEN017` (error) — `[LogProperties]`/`[TagProvider]` cannot be combined with
  classification attributes.
- `LOGGEN026` (warning) — a custom tag provider opts a parameter *out* of redaction.
- `LOGGEN036` (warning) — the value has no meaningful `ToString`/`IFormattable`.

The emission mechanism is the interesting part: classified values are written to
a separate `ClassifiedTagArray` and the message template reads them back from
`RedactedTagArray`, so the generator *statically proves* that classified values
render only through the redaction pipeline. There is **no public `[PersonalData]`
attribute** — Microsoft's own taxonomy is internal; consumers are expected to
define their own (`Microsoft.AspNetCore.Identity`'s `[PersonalData]` is an
unrelated GDPR export feature).

Also relevant: `Microsoft.Gen.ComplianceReports` emits a `ComplianceReport.json`
build artifact listing every classified member when
`<GenerateComplianceReport>true</GenerateComplianceReport>` is set — a
machine-readable compliance inventory, i.e. GDPR Art. 30 evidence produced by the build.

**Why Ark.Tools does not get this for free.** Ark logs through NLog's `Logger`
(`LogManager.GetCurrentClassLogger()`, `_logger.Info(CultureInfo.InvariantCulture,
"…{Tag}…", value)`), not through `[LoggerMessage]` partial methods. `LOGGEN*`
never runs on Ark code, and the redaction pipeline (`AddRedaction()` +
`EnableRedaction()`) is `Microsoft.Extensions.Telemetry`-only. Adopting the
Microsoft classification *vocabulary* is valuable; adopting its *enforcement* is
not possible without rewriting every call site (see [§13.3](#133-rejected-migrating-ark-logging-to-loggermessage--loggen-wholesale)).

| Item | Latest (2026‑09) | License | Compile-time | AoT | Maintained | Verdict for Ark |
| --- | --- | --- | --- | --- | --- | --- |
| `Microsoft.Extensions.Compliance.Abstractions` | 10.9.0 | MIT | attributes only | yes | yes | **Adopt as a dependency** (decision PII‑01) |
| `Microsoft.Extensions.Compliance.Redaction` | 10.9.0 | MIT | no | yes | yes | Adopt `Redactor` shape |
| `Microsoft.Extensions.Telemetry.Abstractions` (LOGGEN) | 10.9.0 | MIT | **yes** | yes | yes | Reference design; not usable directly |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | 5.6.0 | MIT | yes | n/a | yes | **Already in SDK** — extend `BannedSymbols.Ark.Tools.txt` |
| `Microsoft.CodeAnalysis.AnalyzerUtilities` | 5.6.0 | MIT | yes | n/a | yes | Optional; taint types are `internal` |
| CodeQL C# security queries | rolling | MIT (queries) | CI-time | n/a | yes | Complementary; heuristic-only |
| `SonarAnalyzer.CSharp` | 10.33.0.1635 | file (LGPL-ish) | taint rules **do not run** from NuGet | n/a | yes | Rejected as gate |
| `SecurityCodeScan.VS2019` | 5.6.7 | LGPL‑3.0 | yes | n/a | **abandoned 2022** | Rejected |
| `Puma.Security.Rules.2019` | 2.4.23 | MPL‑2.0 | yes | n/a | package stale | Rejected |
| `LeakGuard` | 0.4.0 | MIT | claimed | ? | ~92 downloads, no repo | Rejected (unverifiable) |
| `Vogen` | 8.0.7 | Apache‑2.0 | **yes** (`VOG008/009/010/025`) | struct | yes | Design model, interop target, not a dependency — [§14](#14-value-objects-why-not-build-on-vogen) |
| `StronglyTypedId` | 1.0.0-beta08 (no stable) | MIT | yes | yes | slow | Rejected |
| `ValueOf` | 2.0.31 | MIT | no analyzer, class-based | allocates | deprecated by author | Rejected |
| `SecureString` | BCL | — | no | yes | **DE0001: do not use** | Rejected |
| `Serilog.Enrichers.Sensitive` / `Destructurama.Attributed` | 2.1.0 / 5.3.0 | MIT | no | reflection | yes | Wrong stack; runtime-only |
| NLog masking library | — | — | — | — | **none exists** | Build ours |
| OpenTelemetry .NET redaction | DIY `BaseProcessor` | Apache‑2.0 | no | yes | yes | Build ours (repo already has processors) |
| `Bogus` | 35.6.5 | MIT | no | test-only | yes | Adopt for test data |

**Verified negatives** (things widely assumed to exist that do not):

- No `cs/cleartext-logging` CodeQL query for C# (it exists for Java/Python/JS/Go);
  no CWE‑532 pack for C#. `cs/cleartext-storage-of-sensitive-information` and
  `cs/information-exposure-through-exception` do exist.
- CodeQL's notion of "sensitive" is identifier-name matching
  (`%password%`, `%passwd%`, `%account%id%`, …) — `Email`, `Ssn`, `Iban`,
  `DateOfBirth`, `PhoneNumber` are **not** matched. Extension points
  (`AdditionalSensitiveStrings`) exist and are worth using, but this is not a gate.
- Roslyn's taint engine (`TaintedDataConfig`, `SourceInfo`, `SinkInfo`) is
  `internal`, and `SinkKind` is a closed enum — a PII sink kind cannot be added.
  `dotnet/roslyn-analyzers` is archived; the code now lives in
  `dotnet/roslyn/src/RoslynAnalyzers`.
- NLog has **no** `ILogEventInterceptor`. The real extension points are
  `SetupSerialization(s => s.RegisterObjectTransformation<T>(…))`,
  `RegisterValueFormatter(IValueFormatter)`, `WrapperTargetBase`, layout
  renderers, and filters. The design below uses exactly those.
- No analyzer anywhere (Microsoft, CodeQL, Sonar, SCS, Puma) checks personal data
  flowing into **exception messages**.
- Two source generators cannot observe each other's output in one compilation.
  This is what makes a "sensitive value object as a Vogen add-on" impossible
  ([§14.2](#142-why-that-does-not-solve-this-prds-problem)); it also explains why
  Vogen ships an STJ *converter factory* instead of relying on `[JsonConverter]`.
- No tool bridges C# classification attributes to SQL
  `ADD SENSITIVITY CLASSIFICATION` / `MASKED WITH` DDL.
- C# interceptors are stable since the **.NET 9.0.2xx SDK** (per
  `dotnet/roslyn/docs/features/interceptors.md`), not "new in C# 14" as several
  blogs claim. They intercept ordinary methods only, opt-in via
  `<InterceptorsNamespaces>`, and have weak IDE traceability.

## 5. Solution shape

Five layers, in order of authority. A leak must pass all five.

1. **Declare** — classification attributes and sensitive value objects
   (`Ark.Tools.Compliance`).
2. **Refuse** — `ARKPII*` analyzers turn use-at-a-sink into a compile error
   (`Ark.Tools.Compliance.Analyzers`, shipped inside `Ark.Tools.Compliance`, wired by
   `Ark.Tools.Sdk`).
3. **Inventory** — a generated, committed `ArkComplianceSurface.txt` snapshot; new or
   changed personal data cannot enter the codebase without an explicit diff, the
   same gate style already used for `ArkApiSurface.txt` (`ARKAPI001..004`).
4. **Enforce downstream** — generated SQL classification/masking policy, generated
   JSON/Dapper/Reqnroll converters, generated `ComplianceReport`-style artifact.
5. **Redact at runtime** — NLog wrapper target + value formatter, OTel processor;
   fail-closed. This layer exists for what the analyzers cannot see (third-party
   code, dynamic payloads, `Exception.ToString()` of foreign exceptions).

## 6. Developer experience

### 6.1 Declaring personal data

Ark ships a taxonomy plus one attribute per class of data. The attributes derive
from `Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute`
so Microsoft's own tooling and any `[LoggerMessage]` code in a consumer solution
see the same classification. The dependency on
`Microsoft.Extensions.Compliance.Abstractions` is decided (PII‑01): interop with
the Microsoft stack — in particular the `LOGGEN` guards described in
[§13.3](#133-rejected-migrating-ark-logging-to-loggermessage--loggen-wholesale)
— is a requirement, not an option.

```csharp
namespace Ark.Tools.Compliance;

/// <summary>Ark-owned data classification taxonomy.</summary>
public static class ArkDataClassifications
{
    public const string TaxonomyName = "Ark";

    /// <summary>Directly identifies a natural person (GDPR Art. 4(1)).</summary>
    public static DataClassification PersonalData => new(TaxonomyName, nameof(PersonalData));

    /// <summary>Special categories of personal data (GDPR Art. 9).</summary>
    public static DataClassification SensitivePersonalData => new(TaxonomyName, nameof(SensitivePersonalData));

    /// <summary>Credentials, keys, tokens, connection strings.</summary>
    public static DataClassification Secret => new(TaxonomyName, nameof(Secret));

    /// <summary>Re-identifiable only with additional data held separately.</summary>
    public static DataClassification Pseudonymous => new(TaxonomyName, nameof(Pseudonymous));
}
```

Developer-facing attributes:

```csharp
public sealed class PersonalDataAttribute : DataClassificationAttribute { … }
public sealed class SensitivePersonalDataAttribute : DataClassificationAttribute { … }
public sealed class SecretAttribute : DataClassificationAttribute { … }
public sealed class PseudonymousAttribute : DataClassificationAttribute { … }

/// <summary>Explicit, reviewed statement that a PII-looking member is not personal data.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotPersonalDataAttribute(string justification) : Attribute
{
    public string Justification { get; } = justification;
}
```

Usage:

```csharp
public sealed record Customer
{
    public CustomerId Id { get; init; }

    [PersonalData(Notes = "Contact address; needed for order confirmation.")]
    public string Email { get; init; } = default!;

    [PersonalData]
    public string? PhoneNumber { get; init; }

    [SensitivePersonalData(Notes = "Dietary requirements imply health data.")]
    public string? DietaryNotes { get; init; }

    [NotPersonalData("Free-form internal category name, never contains customer input.")]
    public string SegmentName { get; init; } = default!;
}
```

What the developer sees if they forget:

```
error ARKPII001: Property 'Customer.Email' looks like personal data but is not classified.
                 Apply [PersonalData], use an Ark.Tools.Compliance value object, or apply
                 [NotPersonalData("<why>")]. Unclassified personal data is not redacted
                 in logs, not masked in SQL, and not listed in the compliance inventory.
```

with code fixes: *Add `[PersonalData]`* · *Change type to `EmailAddress`* ·
*Add `[NotPersonalData]`…*.

### 6.2 Sensitive value objects

For string-shaped PII, a value object is stronger than an attribute: it travels
with the value through locals, method parameters, and returns, so the analyzer
does not need inter-procedural flow analysis to keep protecting it.

```csharp
namespace Ark.Reference.Core.Common.Dto;

using Ark.Tools.Compliance;

[PersonalData(Notes = "Customer contact address.")]
[SensitiveValueObject<string>(ArkRedaction.Mask)]
public readonly partial struct EmailAddress
{
    private static ValidationResult _validate(string value)
        => EmailValidator.IsValid(value) ? ValidationResult.Ok : ValidationResult.Invalid("Not an email address.");

    private static string _normalize(string value) => value.Trim().ToLowerInvariant();
}
```

The generator emits a `readonly struct` (no allocation, no reflection, AoT-clean)
with:

- `From(string)` / `TryFrom` with the validation and normalisation hooks;
- `override string ToString()` returning the **redacted** rendering
  (`ArkRedaction.Mask` ⇒ `j***@e***.com`, `ArkRedaction.Hmac` ⇒ `hmac:9f2c…`,
  `ArkRedaction.Erase` ⇒ `***`);
- `IFormattable.ToString(format, provider)` where `"R"` is redacted (default) and
  the **only** way to obtain cleartext is the explicit, greppable
  `Reveal(CompliancePurpose purpose)` method;
- equality/hash over the normalised value;
- the full serialisation surface Ark actually uses — because serialisation is
  expected, and a type people cannot serialise is a type people will not adopt.

Generated serialisation support, one file per opted-in target, all closed-generic
and reflection-free:

| Target | Generated artifact | Enabled by |
| --- | --- | --- |
| `System.Text.Json` | `JsonConverter<EmailAddress>` + `JsonSerializerContext`-friendly registration | default |
| Newtonsoft.Json | `JsonConverter` | `SerializationTargets.NewtonsoftJson` |
| **protobuf-net** | surrogate `struct` + `RuntimeTypeModel`/`[ProtoContract]` registration, in the shape of `Ark.Tools.Protobuf`'s `EvolvableEnumSurrogate<T>` | `SerializationTargets.Protobuf` |
| **MessagePack** | `IMessagePackFormatter<EmailAddress>` + a generated resolver entry, in the shape of `Ark.Tools.MessagePack`'s `EvolvableEnumFormatter<T>` | `SerializationTargets.MessagePack` |
| Dapper | `SqlMapper.TypeHandler<EmailAddress>` | default |
| `TypeConverter` | redaction-aware converter (see below) | default |
| **OpenAPI / Swashbuckle** | a `MapType` registration extension + `x-ark-classification` vendor extension, in the shape of `Ark.Tools.AspNetCore.Swashbuckle`'s `MapNodaTimeTypes` | `SerializationTargets.OpenApi` |
| Reqnroll | value retriever + comparer registration | test projects |

```csharp
[PersonalData(Notes = "Customer contact address.")]
[SensitiveValueObject<string>(ArkRedaction.Mask,
    Serialization = SerializationTargets.SystemTextJson
                  | SerializationTargets.Protobuf
                  | SerializationTargets.MessagePack
                  | SerializationTargets.OpenApi)]
public readonly partial struct EmailAddress { … }
```

Protobuf and MessagePack are first-class rather than an afterthought because the
MediatorFramework transports and `Ark.Tools.MessagePack`/`Ark.Tools.Protobuf`
are how these values cross process boundaries; a value object that only speaks
JSON would push developers straight back to raw `string`. Both formatters write
the **cleartext** value (an explicit, inventoried egress, exactly like the JSON
converter) and both are generated per closed type so nothing is discovered by
reflection at runtime — `MessagePack`'s generated resolver and protobuf-net's
surrogate registration are emitted by the same incremental generator, keeping the
AoT/trim guarantee.

**OpenAPI is not optional either.** These types appear on HTTP contracts
(§6.5), so without a schema mapping Swashbuckle reflects over the struct and
documents `{ "value": "string" }` — a wrong schema that silently breaks clients
and, worse, invites developers back to `string`. The generator emits a
partial-class registration extension in the shape of the existing
`SupportNodaTimeExtensions.MapNodaTimeTypes`:

```csharp
// generated: Ark.Tools.Compliance.OpenApi
public static SwaggerGenOptions MapArkComplianceTypes(this SwaggerGenOptions c)
{
    c.MapType<EmailAddress>(() => new OpenApiSchema
    {
        Type = JsonSchemaType.String,
        Format = "email",
        Examples = [JsonValue.Create("jane.doe@example.com")],   // RFC 2606, never real PII
        Extensions = { ["x-ark-classification"] = new JsonNodeExtension("Ark:PersonalData") },
    });
    return c;
}
```

Two deliberate properties: it is a **`MapType` mapping, not an `ISchemaFilter`**,
so nothing reflects over the type at startup and the AoT/trim guarantee survives;
and the schema carries `x-ark-classification`, which makes the published OpenAPI
document itself an egress record — the same fact that `ARKPII012` and
`ArkComplianceSurface.txt` track, now visible to API consumers and gateway
policy. `ArkStartupWebApiCommon` calls the generated extension by default, so a
classified type is documented correctly without the developer wiring anything.
Examples come from the RFC 2606 reserved-domain generator used by `ARKPII006`,
so a schema example can never be a real address.

**Not in scope for v1, deliberately:** EF Core value converters and Orleans
surrogates. Both are plausible future targets, and both need more than a
converter — an EF Core mapping has to carry the storage policy of §6.6
(`Masked` / `ApplicationEncrypted`, and Always Encrypted's equality-only
comparison semantics), and an Orleans surrogate has to carry classification
across grain-state versioning. They are tracked as follow-ups so that when they
land they land *with* the compliance bits rather than as bare converters that
quietly become a new cleartext egress.

One trap the generator has to close: `DebuggerDisplay`, `TypeConverter`, and any
`IParsable`/`ISpanFormattable` implementation are all cleartext-leaking surfaces
if generated naively. Ark's generator emits redacted forms for all of them, with
cleartext reachable only via `Reveal(CompliancePurpose)` and the serialisation
converters above.

Ark ships ready-made ones so most projects never write their own:
`EmailAddress`, `PhoneNumber`, `NationalIdentificationNumber`, `Iban`,
`PostalAddress`, `PersonName`, `IpAddressValue`, `ApiKey`, `BearerToken`.

Reading cleartext is deliberate and visible:

```csharp
// Compiles. Explicit, greppable, and recorded in the compliance inventory.
var body = new MailMessage(from, customer.Email.Reveal(CompliancePurpose.SendTransactionalEmail));

// error ARKPII005: 'EmailAddress.Reveal' requires a CompliancePurpose; implicit conversion to string is banned.
string raw = customer.Email;
```

### 6.3 Logging

This is the primary sink and the strictest rule. The analyzer understands NLog's
`Logger`/`ILogger` methods (`Trace`/`Debug`/`Info`/`Warn`/`Error`/`Fatal`/`Log`,
including the `IFormatProvider` overloads Ark mandates), `Microsoft.Extensions.Logging`
`ILogger` extensions, `BeginScope`, and `Activity.SetTag`/`AddTag`/`AddEvent`.

```csharp
private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

public async Task ProcessAsync(Customer customer, CancellationToken ctk)
{
    // error ARKPII002: Personal data 'Customer.Email' ([PersonalData]) is used as a
    //                  structured log argument. Log a non-identifying key instead, or
    //                  pass the classified value through a redactor.
    _logger.Info(CultureInfo.InvariantCulture, "Processing {Email}", customer.Email);

    // error ARKPII002: Personal data reaches the log through string interpolation.
    _logger.Info(CultureInfo.InvariantCulture, $"Processing {customer.Email}");

    // error ARKPII002: Type 'Customer' has classified members and is logged as a
    //                  structured object ({@Customer}); redaction is not applied to
    //                  NLog object destructuring.
    _logger.Info(CultureInfo.InvariantCulture, "Processing {@Customer}", customer);

    // OK — identifier is Pseudonymous and the classification allows logging.
    _logger.Info(CultureInfo.InvariantCulture, "Processing {CustomerId}", customer.Id);

    // OK — explicit, redacted rendering; the runtime value is the mask, not the address.
    _logger.Info(CultureInfo.InvariantCulture, "Processing {Email}", customer.Email.Redacted());
}
```

Rule of thumb printed in the diagnostic help: *log the key, not the person*.

For the rare case where a redacted-but-correlatable value is genuinely needed,
`Redacted()` returns a `RedactedValue` struct whose `ToString()` is the
HMAC/mask produced by the configured `Redactor` — stable across a process/tenant
so support can correlate, useless as an identifier outside it.

### 6.4 Exceptions and error contracts

No existing tool checks this path; in Ark it is the highest-risk one, because
exception text flows into the `Ark.Database`/Slack/mail targets and, through
`BusinessRuleViolation`, into HTTP `ProblemDetails` and gRPC status details.

```csharp
// error ARKPII003: Personal data 'Customer.Email' is used in an exception message.
//                  Exception text is logged and returned to clients.
throw new EntityNotFoundException($"Customer {customer.Email} not found");

// error ARKPII003: Personal data is stored in Exception.Data.
ex.Data["email"] = customer.Email;

// OK
throw new EntityNotFoundException($"Customer {customer.Id} not found");
```

The existing repository guidance that business-rule violation properties "must not
contain PII, secrets, or unexpected exception details" becomes machine-checked:
`ARKPII003` also fires on classified members declared on a
`BusinessRuleViolation`-derived type.

### 6.5 Serialisation and transport

Serialising personal data is normal and stays legal. What changes is that the
*shape* is declared, so the inventory and the downstream generators know about it.

```csharp
// OK, and recorded in ArkComplianceSurface.txt as an egress of PersonalData.
[HttpEndpoint(HttpVerb.Get, "/customers/{id}")]
public sealed record GetCustomer : IQuery<CustomerDto> { … }

public sealed record CustomerDto
{
    public CustomerId Id { get; init; }

    [PersonalData]
    public EmailAddress Email { get; init; }
}
```

```
warning ARKPII012: Contract 'CustomerDto' exposes personal data over HTTP but declares no
                   handling policy. Apply [PersonalDataEgress(Purpose = …)] to record the
                   lawful purpose in the compliance inventory.
```

Value-object converters make this transparent: `EmailAddress` serialises as the
cleartext string on the wire (a JSON converter is an explicit egress and is
therefore exempt from `ARKPII005`), while `ToString()` everywhere else stays redacted.

### 6.6 Persistence policy

Classified members must declare how they are stored, and **generating DDL is
opt-in**. C# type/member names are not the SQL schema: `CustomerEntity.Email`
may be `[sales].[Customers].[email_address]`, one entity may map to several
tables, and Dapper poco names are frequently plural/prefixed. There is no
convention to infer `schema.table.column` from, so nothing is emitted unless the
developer says so explicitly with a second, dedicated attribute layered *on top
of* the classification.

```csharp
[SqlDataPolicy(Schema = "sales", Table = "Customers")]   // opt-in: generate DDL for this type
public sealed record CustomerEntity
{
    [PersonalData]
    [SqlColumnPolicy("email_address", StoragePolicy.Masked, MaskFunction = SqlMask.Email)]
    public EmailAddress Email { get; init; }

    [SensitivePersonalData]
    [SqlColumnPolicy("dietary_notes", StoragePolicy.ApplicationEncrypted, KeyName = "cmk-customer")]
    public string? DietaryNotes { get; init; }

    // Classified, but this type is DDL-generating, so an undeclared column is an error:
    // error ARKPII007: Classified member 'CustomerEntity.PhoneNumber' has no [SqlColumnPolicy]
    //                  on a type marked [SqlDataPolicy]. Unmasked by omission is the exact
    //                  failure this rule exists to prevent.
    [PersonalData]
    public PhoneNumber? PhoneNumber { get; init; }
}
```

- `[SqlDataPolicy]` is what turns generation on. Without it a classified type is
  still inventoried and still protected everywhere else — it simply produces no
  SQL, because the design refuses to guess a table name.
- `[SqlColumnPolicy]` carries the **column name verbatim**; the generator never
  derives it from the property name. Schema/table may be overridden per member
  for split-table mappings.
- `ARKPII007` fires only inside a `[SqlDataPolicy]` type. Types with no SQL
  mapping at all (DTOs, messages) are governed by `ARKPII012` instead.

Build output — a `.sql` **template**, not a finished script
(`obj/…/generated/…/ArkCompliance.Sql/CustomerEntity.compliance.sql`), consumed
by the database project as an opt-in post-deployment script:

```sql
:setvar ComplianceLabel "Confidential - GDPR"

ADD SENSITIVITY CLASSIFICATION TO [$(ComplianceSchema)].[Customers].[email_address]
    WITH (LABEL = '$(ComplianceLabel)', INFORMATION_TYPE = 'Contact Info', RANK = HIGH);

ALTER TABLE [$(ComplianceSchema)].[Customers]
    ALTER COLUMN [email_address] ADD MASKED WITH (FUNCTION = 'email()');
```

The template tokens exist because SQL-side naming legitimately differs from the
C# side and per-environment (multi-tenant schemas, `[dbo]` vs `[sales]`,
label taxonomies that differ between customers). Substitution is by SQLCMD
variables when the script runs through SqlPackage/`sqlcmd`, and by an MSBuild
item (`ArkComplianceSqlToken`) for build-time replacement:

```xml
<ItemGroup>
  <ArkComplianceSqlToken Include="ComplianceSchema" Value="sales" />
  <ArkComplianceSqlToken Include="ComplianceLabel" Value="Confidential - GDPR" />
</ItemGroup>
```

`ApplicationEncrypted` instead generates the Dapper type handler that encrypts on
write/decrypts on read (`Always Encrypted` remains available and is preferred when
the deployment supports it; the generator emits the `ENCRYPTED WITH` column
definition in that mode).

### 6.7 Test data

```gherkin
Given the following Customers
  | Id | Email                    |
  | 1  | mario.rossi@ark-energy.eu |
```

```
warning ARKPII006: Literal looks like real personal data (corporate email domain) in test
                   data. Use Ark.Tools.Reqnroll fake generators (deterministic Bogus seed)
                   or an example.invalid / example.com address.
```

`ARKPII006` recognises RFC 2606 reserved domains, `Bogus`-generated shapes, and
checksum-valid IBAN/credit-card/tax-code literals (mod‑97, Luhn), so obviously
fake data is silent and plausible-real data is not. `Ark.Tools.Reqnroll` gains
`ArkFakes` helpers with `Randomizer.Seed` pinned for determinism.

### 6.8 Escape hatches

Three levels, all explicit and all greppable:

```csharp
// 1. Reviewed exception at the call site, with reason. Recorded in the inventory.
[ComplianceReviewed("ARKPII002", "Ticket ARK-1234: support runbook requires the masked local part.", Expires = "2027-01-01")]
private void _logSupportContext(Customer c) { … }

// 2. Standard pragma, for one line.
#pragma warning disable ARKPII002 // support runbook, ARK-1234
    _logger.Debug(CultureInfo.InvariantCulture, "ctx {Email}", c.Email.Redacted());
#pragma warning restore ARKPII002

// 3. Project-wide severity override in .editorconfig (discouraged, visible in review).
//    Note: 'suggestion' is not a useful setting here — a diagnostic that does not
//    fail 'dotnet build' is a diagnostic nobody reads. Turn it off or leave it on.
// dotnet_diagnostic.ARKPII001.severity = none
```

`ARKPII008` warns when `[ComplianceReviewed]` is missing a reason or is past
`Expires`, so exceptions rot loudly instead of silently.

### 6.9 Runtime redaction (second net)

`Ark.Tools.Compliance.NLog` adds one wrapper target and one value formatter to the
existing `NLogConfigurer` chain. NLog has no log-event interceptor, so the wrapper
target is the correct place: it sees the `LogEventInfo` once, before fan-out to
console/file/database/Slack/mail.

**It is on by default.** `NLogConfigurer.WithArkDefaultTargetsAndRules(...)` —
and therefore `WithDefaultTargetsAndRulesFromConfiguration` and
`IHostBuilder.ConfigureNLog(...)` — wires redaction with the fail-closed policy
below whenever `Ark.Tools.Compliance.NLog` is referenced. A configuration a
developer forgets to write is a configuration that leaks, so there is no
"remember to call `.WithComplianceRedaction()`" step:

```csharp
// Redaction is already active here: classified values render redacted on every
// Ark default target, with no extra call.
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .Apply();
```

`WithComplianceRedaction` exists only to **override** the defaults, and
`WithoutComplianceRedaction()` is the explicit, greppable opt-out:

```csharp
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .WithComplianceRedaction(o =>
    {
        o.Default = ArkRedaction.Erase;                        // fail closed (default)
        o.For(ArkDataClassifications.PersonalData, ArkRedaction.Hmac);
        o.For(ArkDataClassifications.Secret, ArkRedaction.Erase);
        o.PatternScan = PatternScanMode.MessageAndProperties;  // last-resort text scan, off by default
    })
    .Apply();
```

Defaults applied without any call: `Default = Erase`, `PersonalData = Hmac`,
`SensitivePersonalData = Erase`, `Secret = Erase`, `Pseudonymous = None`,
`PatternScan = Off` (decision PII‑06 — the scan is the only part with a
measurable cost, and enabling it silently would hide analyzer gaps).

Three mechanisms, all AoT-safe:

1. **Typed transformation** — for every generated sensitive value object the
   generator also emits a registration
   (`SetupSerialization(s => s.RegisterObjectTransformation<EmailAddress>(…))`),
   so structured properties are redacted even when they arrive as `object`.
2. **Value formatter** — an `IValueFormatter` decorator that intercepts message
   template parameter rendering for classified types not covered above.
3. **Pattern scan** — a `RedactingTargetWrapper : WrapperTargetBase` running a
   single pass over the rendered message with `[GeneratedRegex]`-compiled
   patterns pre-filtered by `SearchValues<char>` prefilters (email `@`, IBAN
   country prefixes, digit runs). Off by default; measured budget: ≤ 2 µs per
   event for a 200-char message. Regexes are source-generated with a timeout,
   never `RegexOptions.Compiled` at runtime.

`Ark.Tools.OTel` gains `ArkComplianceRedactionProcessor : BaseProcessor<Activity>`
following the existing `ArkPreFilterProcessor`/`ArkTelemetryEnrichmentProcessor`
pattern, applying the same `Redactor` to tag values, likewise registered by the
default OTel setup rather than by an opt-in call.

> The runtime layer is deliberately dumb. If it ever fires in production, that is
> a bug report against the analyzers, and the mask string (`***ARKPII***`) is
> designed to be alertable in the log platform.

### 6.10 Compliance inventory

`ArkComplianceSurface.txt` is generated next to the existing `ArkApiSurface.txt`
— a **separate** file (decision PII‑04: different audience, different cadence, and
a privacy diff must not be buried inside an API diff) — committed, and diffed by
the build.

```
COMPLIANCE-SURFACE 1
CLASSIFIED Ark.Reference.Core.Common.Dto.CustomerDto.Email
  CLASSIFICATION Ark:PersonalData
  TYPE Ark.Reference.Core.Common.Dto.EmailAddress
  STORAGE sales.Customers.email_address Masked(email)
  EGRESS Http:GetCustomer
  NOTES Contact address; needed for order confirmation.
END
REVIEWED CustomerSupportService._logSupportContext
  RULE ARKPII002
  REASON ARK-1234
  EXPIRES 2027-01-01
END
```

Drift is `ARKPII020` (error) with the same "accept the generated diff" workflow as
`ARKAPI002`. The file doubles as GDPR Art. 30 evidence and as the input for the
SQL policy generator and DPIA reviews.

## 7. Diagnostics

`Category = "Compliance"`. Shipped through `AnalyzerReleases.*.md` like the existing
`ARKCORE*`/`ARKSOLID*` rules, documented in `docs/analyzers.md`, severities set in
a packaged `Ark.Tools.Compliance.globalconfig`.

| ID | Severity | Rule |
| --- | --- | --- |
| ARKPII001 | Warning | PII-suggestive member/parameter/local is not classified |
| ARKPII002 | **Error** | Classified value reaches a log template, argument, or scope |
| ARKPII003 | **Error** | Classified value reaches an exception message or `Exception.Data` |
| ARKPII004 | **Error** | Classified value reaches an `Activity` tag, metric dimension, or baggage |
| ARKPII005 | **Error** | Implicit `ToString`/interpolation/concat/`Reveal` without purpose on a classified value |
| ARKPII006 | Warning | Test data literal looks like real personal data |
| ARKPII007 | **Error** | Classified member in a `[SqlDataPolicy]` type without `[SqlColumnPolicy]` |
| ARKPII008 | Warning | `[ComplianceReviewed]` lacks a reason or is expired |
| ARKPII009 | Warning | `[NotPersonalData]` justification is missing or boilerplate |
| ARKPII010 | **Error** | Classification attribute on a member the pipeline cannot redact (open `object`, `dynamic`, delegate, or a `[ValueObject]` type with cleartext-leaking `Conversions`/debugger attributes — [§14.5](#145-interop-not-exclusion)) |
| ARKPII011 | **Error** | Classified value passed to a banned formatting sink (`Console.*`, `Debug.*`, `Trace.*`, `StringBuilder.Append`) |
| ARKPII012 | Warning | Contract exposes personal data with no declared egress purpose |
| ARKPII013 | Warning | Project uses `Microsoft.Extensions.Telemetry` logging without `AddArkRedaction()` (see [§13.3](#133-rejected-migrating-ark-logging-to-loggermessage--loggen-wholesale)) |
| ARKPII020 | **Error** | `ArkComplianceSurface.txt` drift |
| ARKPII021 | **Error** | `ArkComplianceSurface.txt` missing or malformed |

Errors are errors *by default and deliberately over-zealous*: suppression is one
pragma away and is visible in review, whereas a leak is invisible.

## 8. Analyzer implementation strategy

Deliberately **not** a taint engine (see [§13.1](#131-rejected-full-inter-procedural-taint-analysis)).
Three tiers, in order of cost:

1. **Symbol tier (cheap, exact).** `RegisterSymbolAction` over properties, fields,
   parameters, and records; reads classification attributes, including the
   type-level and positional-parameter forms. Recursion into member types is
   **not limited to records** — the `LOGGEN035` implementation only recurses into
   `IsRecord` types, which is its most likely false-negative source; ours walks any
   non-framework type with a cycle guard and a configurable depth (default 5).
2. **Operation tier (local flow).** `RegisterOperationAction` on `IInvocationOperation`,
   `IObjectCreationOperation`, `IThrowOperation`, `IInterpolatedStringOperation`.
   Argument expressions are resolved through a **local, intra-method** backward walk
   over `IOperation` (assignments, ternaries, `?.`, casts) so that
   `var e = c.Email; _logger.Info(…, e);` is caught without a full data-flow
   analysis. Cross-method flow is intentionally out of scope: that is what value
   objects are for.
3. **Type tier (free).** Any value whose *type* is a generated sensitive value
   object is classified regardless of flow — no analysis needed, which is why
   §6.2 is the recommended path for string PII.

Sinks are declared in data, not code: a packaged `ComplianceSinks.Ark.txt`
`AdditionalFiles` document in Documentation-Comment-ID format, identical in shape
to `BannedSymbols.Ark.Tools.txt`, so a consumer can add their own sinks
(a custom audit logger, a legacy `Trace` wrapper) without forking the analyzer.

```
M:NLog.Logger.Info(System.IFormatProvider,System.String,System.Object[]);log
M:System.Diagnostics.Activity.SetTag(System.String,System.Object);telemetry
M:MyCompany.Legacy.AuditWriter.Write(System.String);log
```

The lexicon for `ARKPII001` is likewise data: a packaged, overridable
`ComplianceLexicon.Ark.txt` (`email`, `mail`, `phone`, `mobile`, `ssn`, `taxcode`,
`codicefiscale`, `iban`, `vat`, `birth`, `address`, `firstname`, `surname`,
`latitude`, `passport`, `licenseplate`, …) with negative terms
(`hashed`, `masked`, `redacted`, `template`, `count`) — the CodeQL heuristic model,
but extended to actual PII and consumer-extensible.

Performance/authoring rules: `EnforceExtendedAnalyzerRules`, no file I/O in
analyzers, `netstandard2.0`, generated code excluded, all state per-compilation
via `RegisterCompilationStartAction`, and generators are incremental
(`IIncrementalGenerator`) with deterministic, snapshot-tested output — matching the
existing mediator-framework generator discipline.

## 9. Packaging

| Package | Contents | TFMs |
| --- | --- | --- |
| `Ark.Tools.Compliance` | attributes, taxonomy, `Redactor`s, value objects, `Reveal`/`CompliancePurpose`; ships the analyzer + generator + code-fix DLLs as `analyzers/dotnet/cs` (same pattern as `Ark.Tools.Core`) | `net8.0;net10.0` |
| `Ark.Tools.Compliance.Analyzers` (+ `.CodeFixes`) | `IsPackable=false`, packed into the above | `netstandard2.0` |
| `Ark.Tools.Compliance.NLog` | `RedactingTargetWrapper`, `IValueFormatter`, redaction wired **by default** into `WithArkDefaultTargetsAndRules`; `WithComplianceRedaction`/`WithoutComplianceRedaction` for override/opt-out | `net8.0;net10.0` |
| `Ark.Tools.Compliance.Sql` | Dapper handlers for encrypted columns, opt-in DDL template generation (`[SqlDataPolicy]`) | `net8.0;net10.0` |
| `Ark.Tools.Compliance.Protobuf` / `.MessagePack` | generator targets emitting surrogates/formatters next to `Ark.Tools.Protobuf` / `Ark.Tools.MessagePack` | `net8.0;net10.0` |
| `Ark.Tools.Compliance.OpenApi` | generated `MapType` registrations and the `x-ark-classification` extension; wired into `ArkStartupWebApiCommon` next to `MapNodaTimeTypes` | `net8.0;net10.0` |
| `Ark.Tools.OTel` (existing) | `ArkComplianceRedactionProcessor`, registered by the default setup | unchanged |
| `Ark.Tools.Sdk` / `Ark.Tools.Build` (existing) | implicit `PackageReference` (`EnableArkToolsCompliance`), packaged `Ark.Tools.Compliance.globalconfig` (`ARKPII*` **and** the `LOGGEN*` escalations of §13.3), `ComplianceSinks`/`ComplianceLexicon` `AdditionalFiles`, `ArkComplianceSurface.txt` gate | unchanged |

Opt-out follows the SDK convention already established
(`EnableArkToolsCompliance=false`, per-rule severity overrides), and the analyzer
package is `PrivateAssets="all"` / `developmentDependency`.

The package family is named **`Ark.Tools.Compliance`**, not `Ark.Tools.Privacy`,
to line up with `Microsoft.Extensions.Compliance.*` — same vocabulary, same
`DataClassification` types, one concept to learn. The diagnostic prefix stays
`ARKPII` because it names the risk being reported, not the package that reports
it (as `ARKAPI*` names API surface, not `Ark.Tools.Api`).

## 10. Rollout

**No "observe" stage.** An earlier draft proposed shipping every rule at
`suggestion` first. That is worthless here: most code in these solutions is
written by agents outside an IDE, and `suggestion`-severity diagnostics are
invisible to `dotnet build` and therefore ignored. The rules ship at the
severities in §7 from day one — the risk mitigation is the opt-out, not a
softer default.

Two modes only:

1. `ArkComplianceMode=Enforce` (**default**) — the severities in §7.
2. `ArkComplianceMode=Off` (`EnableArkToolsCompliance=false`) — for a solution
   that cannot absorb the change yet. Opting out is a single, greppable,
   review-visible MSBuild property; per-rule severity overrides in
   `.editorconfig` remain available for finer control.

Existing solutions therefore adopt this the same way they adopt any other
breaking SDK change: bump the SDK on a branch, fix or suppress what the build
reports, commit `ArkComplianceSurface.txt` as the baseline. `ARKPII020` (drift)
then guarantees no *new* undeclared personal data can be added afterwards.

## 11. Testing

- `Microsoft.CodeAnalysis.Testing` verifier tests per rule: positive, negative,
  suppression, and code-fix cases, including the shapes `LOGGEN035` misses
  (non-record nested classes, tuples, collection element types).
- Generator snapshot tests (existing `GeneratorSnapshotTests` pattern).
- Reqnroll integration test in the reference project: an end-to-end scenario that
  logs a `Customer` through the real `NLogConfigurer` chain **with no redaction
  call in the setup** — proving the default wiring of §6.9 — and asserts the
  database target row contains the mask, never the address.
- Round-trip tests for every generated serialisation target (STJ, Newtonsoft,
  protobuf-net, MessagePack, Dapper, Reqnroll), each asserting that the wire form
  is cleartext and that `ToString()`/`DebuggerDisplay` on the deserialised value
  is still redacted.
- OpenAPI document test in the reference API: a classified property is documented
  as its primitive schema (not `{ "value": … }`), carries `x-ark-classification`,
  and its example is an RFC 2606 reserved value.
- A clean-consumer SDK test (existing `tests/Ark.Tools.Sdk.Tests` fixture) proving
  the rules and configuration flow through the packages.
- AoT/trim smoke test: publish the reference API with
  `PublishAot`/`PublishTrimmed` and assert no new warnings.

## 12. Success criteria

- Zero unclassified PII-suggestive members in the reference project.
- `ArkComplianceSurface.txt` reviewed on every PR that changes it.
- Runtime redaction never fires in the reference project's integration tests
  (i.e. the compile-time layer is doing the work).
- No measurable logging throughput regression with `PatternScan` disabled;
  < 5 % with it enabled.

## 13. Rejected approaches

### 13.1 Rejected: full inter-procedural taint analysis

Roslyn's `TaintedDataAnalysis` (`TaintedDataConfig`, `SourceInfo`, `SinkInfo`) is
`internal` to `Microsoft.CodeAnalysis.AnalyzerUtilities` and `SinkKind` is a closed
enum, so a `Pii` sink kind cannot be registered. The code is MIT and could be
vendored, and `PointsToAnalysis`/`ValueContentAnalysis` are public, but the cost is
a per-compilation whole-program analysis with a non-stable API surface that has
churned across 3.3.x → 4.14 → 5.x. Rejected for v1: the value-object tier removes
most of the need (the *type* carries the classification across method boundaries),
and IDE-speed diagnostics matter more than completeness. Revisit only if
measurements show the local-flow tier misses real leaks.

### 13.2 Rejected: rely on CodeQL / Sonar / Security Code Scan / Puma Scan

- **CodeQL**: no C# `cleartext-logging` query exists, "sensitive" is
  `%password%`-style name matching that ignores `Email`/`Ssn`/`Iban`, and its
  "sanitizer" heuristic accepts any method whose name contains `encode`. It runs in
  CI minutes after the mistake, not in the editor. **Kept as a complementary
  layer**: we will extend `AdditionalSensitiveStrings` with the Ark lexicon.
- **SonarAnalyzer.CSharp**: its taint rules are computed server-side and do not run
  from the NuGet package, so it cannot be a `dotnet build` gate; and it has no
  classification model. (Separately evaluated in `SDK-IMP-11`.)
- **Security Code Scan**: LGPL‑3.0 and unmaintained since 2022‑11.
- **Puma Scan**: MPL‑2.0, package pinned to `Microsoft.CodeAnalysis` 3.0.0, no PII
  model, licensing unverifiable.
- **LeakGuard**: the only purpose-built package, but no public repository, ~92
  downloads, and unverifiable rule semantics. Taking a dependency on it would
  violate the repository's "no unnecessary third-party dependencies" rule.

### 13.3 Rejected: migrating Ark logging to `[LoggerMessage]` + LOGGEN wholesale

Rejected **as a migration**, adopted **as the mandatory path wherever Microsoft
logging is already the right answer**.

Rejected as a migration because it means rewriting every
`_logger.Info(CultureInfo.InvariantCulture, …)` call site in Ark.Tools, its
samples, and every consumer solution into partial logging methods; taking
`Microsoft.Extensions.Telemetry` (and its `ILogger` pipeline) as a hard
dependency alongside NLog; and accepting that redaction is active only when
`AddRedaction()` **and** `EnableRedaction()` are both wired — a
silent-degradation failure mode.

But there is a real, non-negotiable overlap. Projects regularly have to extend
Microsoft stack extension points — ASP.NET Core middleware, MVC/minimal-API
filters, `IHostedService`, EF Core interceptors, Azure Functions middleware —
and there the injected `ILogger<T>` is the correct logger. **The rule is: inside
Microsoft extension points, use Microsoft logging, not NLog**
(`NLogConfigurer` already bridges it via `logging.AddNLog()`, so the events land
on the same Ark targets). That path must be as safe as the NLog one, which
requires two things from this design:

1. **Vocabulary compatibility (decision PII‑01).** Ark classification attributes
   derive from `DataClassificationAttribute`, so `LOGGEN035` recognises an
   `[PersonalData]`-marked member in an Ark type with no bridge, no duplicate
   attribute, and no adapter. This is the concrete reason the dependency is
   taken rather than reimplemented.
2. **LOGGEN guards enabled by default in the SDK.** `Ark.Tools.Build` ships them
   escalated in the packaged global config, so a `[LoggerMessage]` leak is a
   build break, not a warning someone scrolls past:

   ```ini
   # Ark.Tools.Compliance.globalconfig
   dotnet_diagnostic.LOGGEN035.severity = error   # parameter leaks sensitive data
   dotnet_diagnostic.LOGGEN017.severity = error   # [LogProperties] + classification
   dotnet_diagnostic.LOGGEN026.severity = error   # tag provider opts out of redaction
   dotnet_diagnostic.LOGGEN036.severity = warning # no meaningful ToString/IFormattable
   ```

   Escalation only bites once `Microsoft.Extensions.Telemetry.Abstractions` is
   referenced (the generator is not present otherwise), so this is free for
   projects that never touch the Microsoft logging stack. `ARKPII002` covers the
   same call sites through `ILogger`/`BeginScope` extension methods, so a project
   that uses `ILogger` *without* `[LoggerMessage]` is still guarded — the two
   mechanisms overlap on purpose.

Additionally, `Ark.Tools.Compliance` provides the DI one-liner that makes the
Microsoft pipeline fail-closed (`services.AddArkRedaction()` performing both
`AddRedaction()` and `logging.EnableRedaction()`), removing the
"half-configured, silently unredacted" failure mode, and `ARKPII013` reports a
project that references `Microsoft.Extensions.Telemetry` without it.

The rest of LOGGEN is **adopted as prior art**: the classification vocabulary,
the fail-closed default (`ErasingRedactor`), the compliance-report artifact idea,
and the "abort generation" trick — and we fix its record-only recursion
limitation.

### 13.4 Rejected: runtime-only masking (Serilog/NLog/OTel/Presidio-style)

`Serilog.Enrichers.Sensitive` and `Destructurama.Attributed` are mature, but they
are the wrong stack (Ark is NLog) and, more importantly, `[NotLogged]` only applies
when the object is destructured with `{@Obj}` — `_log.Information("User {Email}",
user.Email)` bypasses it silently, with no compile-time signal. Both are
reflection-based and AoT-hostile. ML/checksum runtime detectors
(`TasmanianDevil`, Presidio) are useful for opaque payloads but cost per-event CPU,
produce false negatives on the exact fields we already know about, and cannot be a
gate. Runtime masking is therefore layer 5 of 5, never layer 1.

### 13.5 Rejected: name-based detection as the enforcement mechanism

Naming heuristics are how CodeQL and every "PII scanner" work, and they are both
noisy (`emailTemplateId`) and blind (`Recipient`, `Payload`, `Ref1`). In this design
the lexicon only powers `ARKPII001`, whose remedy is to *declare* — after which all
enforcement is attribute/type driven and exact.

### 13.6 Rejected: `SecureString`, and "just encrypt everything"

`SecureString` is explicitly deprecated guidance (`DE0001`); it is not encrypted at
all on Linux/macOS, requires marshalling back to plaintext to be useful, and
carries no classification. Blanket column encryption breaks indexing, joins, and
reporting (Always Encrypted deterministic mode supports only equality), so
encryption stays an opt-in `StoragePolicy` per member, with Dynamic Data Masking as
the default *display* control — noting explicitly that DDM is presentation-only and
never a substitute for encryption or access control.

### 13.7 Rejected: interceptors as the detection mechanism

C# interceptors (stable since the .NET 9.0.2xx SDK; the repo already ships an
interceptor generator for `ToDataTableArk`) can rewrite `Logger.Info(...)` call
sites to a redacting overload. Rejected for detection because interception is
silent — the developer never learns they wrote a leak, the IDE has "limited
traceability" for intercepted calls, `[InterceptsLocation]` data is invalidated by
every file edit, and interception cannot cover constructors (i.e. exceptions),
properties, or delegates. Kept as a **candidate code-fix/remediation** mechanism for
a later phase, behind an explicit opt-in, never as the primary control.

### 13.8 Rejected: taking `Vogen` (or `StronglyTypedId`, `ValueOf`) as a dependency

`Vogen` 8.0.7 is the best-in-class value-object generator and much of its design
is adopted. Building on it, adding an add-on, or contributing
`Microsoft.Extensions.Compliance` support upstream were all evaluated in detail —
see **[§14 Value objects: why not build on Vogen](#14-value-objects-why-not-build-on-vogen)**,
which is the full analysis this rejection rests on. Summary: two source
generators cannot see each other's output in the same compilation, so an Ark
generator cannot emit the redacted `ToString`/`TryFormat` that Vogen would
otherwise let a *human* hand-write; several cleartext surfaces
(`DebuggerDisplay`, `TypeConverter`, PolyType marshaler) have no off switch; there
is no plugin model and no protobuf-net support; and Vogen's own contribution
guide steers minority-audience features to add-on libraries, which is exactly
what Roslyn makes impossible here.

`StronglyTypedId` has no stable release; `ValueOf` is class-based (allocating),
analyzer-less, and deprecated by its own author.

### 13.9 Rejected: `BannedApiAnalyzers` alone

Banning `object.ToString()` on sensitive types is useful and **is part of the
design** (the SDK already ships `BannedSymbols.Ark.Tools.txt`), but banning is
symbol-identity based: it cannot know that a `string` local holds an email, cannot
inspect message templates, and cannot produce the inventory. It is a complement,
not the mechanism.

### 13.10 Rejected: a separate compliance repository/package family

Considered shipping this as a standalone product. Rejected: the value comes from
being *on by default* in `Ark.Tools.Sdk`, from reusing the existing analyzer
configuration, snapshot-gate, and NLog/OTel/Dapper integration points, and from
one version line. It follows the accepted SDK architecture (`SDK-01`, `SDK-24`).

## 14. Value objects: why not build on Vogen

Review raised the obvious objection: §6.2 is largely a value-object generator, and
`Vogen` is a mature, well-maintained one. This chapter is the precise analysis of
whether the sensitive value object can be **built on top of Vogen** (as a
consumer, as an add-on, or by contributing `Microsoft.Extensions.Compliance`
support upstream) instead of generating our own type. Findings are from Vogen
`8.0.7` (`main` @ `e5bbd98`, 2026‑07‑21) and its issue tracker.

### 14.1 What Vogen already offers that we would want

Vogen is genuinely close, and the parts it does well are exactly the parts we do
not want to write:

| Capability | Vogen | Evidence |
| --- | --- | --- |
| Construction lockdown | `VOG008` (no user ctors), `VOG009` (no `default`), `VOG010` (no `new`), `VOG025` (no `Activator.CreateInstance`), `VOG027`, `VOG040` | `src/Vogen/Rules/*.cs` |
| `Validate` / `NormalizeInput` hooks | private static, analyzer-enforced (`VOG004/005/014/015/016/028`) | `docs/…/ValidationTutorial.md` |
| **User-supplied members win** | `ToString`, `Equals`, `GetHashCode`, `IFormattable`/`ISpanFormattable.TryFormat`, `IConvertible`, `Parse`/`TryParse` are all skipped if the user declares them | `GenerateCodeForToString.cs`, `GenerateCodeForTryFormat.cs`, `MethodDiscovery.cs` |
| **Partial members** (C# 13 / Roslyn 4.12+) | `Value`, `From`, `TryFrom` can be re-declared `partial` to change accessibility **and carry attributes** | `DiscoverUserProvidedPartials.cs:8-31`, `Util.cs:32-61` |
| Surface reduction | `Conversions.None`, `CastOperator.None` both ways (explicit is already the default), `PrimitiveEqualityGeneration.Omit`, `DebuggerAttributeGeneration.Basic` | `Vogen.SharedTypes/*.cs` |
| Serialisation | `TypeConverter`, STJ (+ AoT-clean `VogenTypesFactory`), Newtonsoft, Dapper, EF Core, LinqToDb, Bson, Orleans, ServiceStack, Xml, **MessagePack** (since 5.0.5) | `Conversions.cs`, `samples/AotTrimmedSample` |
| OpenAPI | `OpenApiSchemaCustomizations`: a Swashbuckle `ISchemaFilter` (**reflection-based**) or a mapping extension method | `OpenApiSchemaCustomizations.cs`, `docs/…/Use-in-Swagger.md` |

So the answer to "can a *developer* hand-build a sensitive value object on
Vogen?" is **yes** — `Conversions.None`, `CastOperator.None`, a hand-written
redacting `ToString`, `TryFormat`, `IConvertible.ToString`, and a `private
partial string Value { get; }` gets most of the way there.

### 14.2 Why that does not solve *this* PRD's problem

The PRD's requirement is not "a value object exists". It is **"the safe thing is
what you get when you write nothing"**. Every item in the previous paragraph is a
thing the developer must remember, on every type, forever — which is precisely
the failure mode §1 describes. To make it automatic, Ark would have to
*generate* those members. That is where it breaks:

**Blocker 1 — two source generators cannot see each other's output in the same
compilation.** This is a Roslyn fundamental, confirmed twice in Vogen's own
tracker:

> "I started working on this, but hit a brick wall: MemoryPack uses source
> generators, but so does Vogen. There is no way to order which source generator
> runs first… **This won't be a problem if the value objects are in a separate
> assembly.**" — Vogen maintainer, [`#696`](https://github.com/SteveDunn/Vogen/issues/696)

> "…it still would only help users that define their data types in a separate
> assembly/project from the code that serializes them." — Andrew Arnott
> (PolyType/Nerdbank.MessagePack), [`#834`](https://github.com/SteveDunn/Vogen/issues/834)

Consequences, in both directions:

- Vogen's "user-supplied members win" detection (`MethodDiscovery`) queries the
  semantic model of **user source only**. A `ToString`/`TryFormat`/`Equals`
  emitted by an Ark generator is invisible to it, so Vogen emits its own →
  `CS0111`/`CS0102` duplicate members. The one mechanism that makes Vogen
  customisable is unusable from a generator.
- Conversely, an Ark generator cannot read `Value`, `_value`, `From`, or Vogen's
  `ToString` to build a redacted rendering, an NLog transformation registration,
  or an inventory entry.
- Adding fields to the partial triggers `CS0282`; adding a constructor triggers
  `VOG008`.

The only safe co-existence is "emit member names Vogen never emits" (`Reveal`,
`Redacted`) — which is exactly the small part we would *not* need help with.

The maintainer's workaround (put value objects in a separate assembly) is real
and works for Ark's own built-in types, but as a **rule imposed on consumers** it
means "every project must move its sensitive DTO primitives into a separate
assembly from the code that uses them". That is a large, permanent architectural
tax to pay for a generator dependency, and it fails for exactly the code that
matters most: an entity and its repository in the same project.

**Blocker 2 — cleartext escapes Vogen cannot close.** Verified in the generated
output:

| Path | Status |
| --- | --- |
| `[DebuggerDisplay("Underlying type: {type}, Value = { _value }")]` + `[DebuggerTypeProxy]` exposing `Value` | Emitted **by default**; `DebuggerAttributeGeneration` has `Default`/`Basic`/`Full` — **no `None`**, and `Basic` is documented as a Rider workaround, not a security control |
| `TypeConverter.ConvertTo` returning `idValue.Value` | On by default (`Conversions.Default` includes `TypeConverter`); anything using `TypeDescriptor` (ASP.NET model binding, some layout renderers) gets cleartext. Removable only by giving up ASP.NET route/query binding |
| `[PolyType.TypeShape(Marshaler = …)]` with `Marshal(v) => v.Value` | Emitted **unconditionally** when PolyType is in the compilation — no flag suppresses it |
| `IFormattable.ToString` / `ISpanFormattable.TryFormat` | String interpolation `$"{vo}"` goes through `TryFormat`, **not** `ToString` — overriding `ToString` alone is insufficient, a trap we must not leave to developers |
| `IConvertible.ToString(IFormatProvider)` | Hoisted whenever the primitive implements it |
| `GetHashCode` | Returns the unsalted primitive hash |
| `VOG009/010/025` | These live in standalone analyzers **without** `NotConfigurable`, so `dotnet_diagnostic.VOG010.severity = none` disables them — acceptable for IDs, not for a compliance gate |

**Blocker 3 — no extension model, by design.** The conversion generators are a
fixed 7-entry array (`src/Vogen/Util.cs:66-75`) and `ConversionMarkerKind` has 4
values (`EFCore`, `MessagePack`, `Bson`, `OpenApi`). `[ValueObject]` has no
parameter for "attach these attributes to the generated type", and its source
carries the rule *"DO NOT ADD PARAMETERS HERE"*. **protobuf-net is not
supported at all** — the documented answer is a hand-written
`VogenSurrogate<TW,TP>` per type ([FAQ](https://github.com/SteveDunn/Vogen/blob/main/docs/site/Writerside/topics/reference/FAQ.md)) —
and protobuf is a required target (§6.2). No third-party package building on
Vogen was found to exist.

### 14.3 Contributing to Vogen instead

Evaluated seriously: the maintainer is highly responsive (external PRs merged
within days; `Conversions.MessagePack` and `XmlSerializable` both went from
community request to shipped in ~2–3 weeks). But `CONTRIBUTING.md` sets the
scope test explicitly:

> "…we want features that will be useful to the majority of our users and not
> just a small subset. **If you're just targeting a minority of users, consider
> writing an add-on/plugin library.**"

A `Microsoft.Extensions.Compliance` classification model, a purpose-gated
`Reveal`, redacted `ToString`/`TryFormat`/debugger surfaces, and an NLog
transformation registry are the definition of "a small subset" for a
general-purpose value-object library — so the honest expected outcome is "please
write an add-on", and §14.2 Blocker 1 is precisely why an add-on cannot exist
in-assembly. Vogen would also have to own an opinion about *which* redaction
applies, which is Ark policy, not Vogen's business.

Two contributions are nevertheless worth making, because they are generic,
benefit every Vogen user, and reduce our own divergence if we ever revisit:

1. `DebuggerAttributeGeneration.None` — a value object holding a secret should be
   able to opt out of the debugger display entirely. Small, uncontroversial.
2. `Conversions.Protobuf` (protobuf-net surrogate generation) — a serializer flag
   in the same category as the accepted `MessagePack`/`XmlSerializable` ones.

These are filed as good-citizen upstream work, not as prerequisites for this PRD.

### 14.4 What Ark actually implements (and does not)

"Reimplementing Vogen" overstates the work, because the sensitive value object is
deliberately a **narrow** type:

| Vogen feature | Ark generator |
| --- | --- |
| Any underlying primitive, `INumber<T>` hoisting, comparison generation, `[Instance]`, EF Core/LinqToDb/Bson/Orleans/ServiceStack/Xml/OpenAPI conversions, `StaticAbstracts`, `ParsableForPrimitives`, LinqPad dump, Swashbuckle filters | **Not implemented.** Out of scope. |
| `string` (and a small set of validated primitives) with `_validate`/`_normalize`, `From`/`TryFrom`, equality, STJ/Newtonsoft/Dapper/**protobuf-net**/**MessagePack**/**OpenAPI**/Reqnroll converters | Implemented, closed-generic and AoT-clean; OpenAPI as a `MapType` mapping, never a reflection-based `ISchemaFilter`. |
| — | **New:** classification attribute flow, redacted `ToString`/`TryFormat`/`IConvertible`/debugger surfaces, `Reveal(CompliancePurpose)`, NLog `RegisterObjectTransformation` registration, `ArkComplianceSurface.txt` entry, `ARKPII*` integration. |

The estimate is a single-primitive-shape generator plus converter templates —
materially smaller than Vogen, and every line of it exists because it is the
integration point with the analyzers, the inventory, and the redaction pipeline.

### 14.5 Interop, not exclusion

Ark does not ban Vogen, and a solution already using it loses nothing:

- The `ARKPII*` analyzers are attribute-driven and type-driven; they do not care
  which generator produced a type. A `[ValueObject<string>]` type that also
  carries `[PersonalData]` is classified, is inventoried, and is refused at every
  sink exactly like an Ark-generated one.
- `ARKPII010` covers the Vogen-specific traps above: a classified `[ValueObject]`
  type with `Conversions` including `TypeConverter`, with default debugger
  attributes, or without a user-supplied `TryFormat`, is reported with the
  concrete fix, so "Vogen + classification" is a supported, checked combination
  rather than an unguarded one.

**Decision:** own generator for the sensitive value object; no Vogen dependency;
Vogen supported as an interop target; two generic improvements offered upstream.
Revisit only if Roslyn gains generator ordering/visibility, which would remove
Blocker 1 entirely.

## 15. Risks

| Risk | Mitigation |
| --- | --- |
| False positives make teams disable the rules | `ARKPII001` is the only heuristic rule and stays a warning (PII‑02); lexicon and sinks are consumer-editable data files; per-rule severity and `EnableArkToolsCompliance=false` are the ramp |
| Analyzer cost in large solutions | Symbol/type tiers dominate; local flow is intra-method only; benchmarks in CI |
| Developers use `Reveal()` reflexively | `Reveal` requires a `CompliancePurpose`, is greppable, and appears in the inventory diff |
| New MS dependency conflicts with CPM/lock files | `Microsoft.Extensions.Compliance.Abstractions` is attributes-only, MIT, and already in the transitive graph of `Microsoft.Extensions.Telemetry`; `Directory.Packages.props` and every `packages.lock.json` are updated in the same commit (CI runs `RestoreLockedMode=true`) |
| Inventory churn noise | Snapshot format is deterministic and ordinal-sorted, like `ArkApiSurface.txt` |

## 16. Delivery

The design is approved. Implementation is tracked on the SDK task board as the
**PII-IMP** series, under
[`progress/tasks/compliance/`](progress/tasks/compliance/), following the same
conventions as the `SDK-IMP` tasks: one task per branch/PR, each self-contained,
each leaving `dotnet build Ark.Tools.slnx --configuration Debug` and
`dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
green.

| Task | Scope | PRD sections |
| --- | --- | --- |
| [PII-IMP-01](progress/tasks/compliance/PII-IMP-01-compliance-foundation.md) | `Ark.Tools.Compliance` attributes, taxonomy, redactors, `CompliancePurpose` | §6.1, §17 PII‑01 |
| [PII-IMP-02](progress/tasks/compliance/PII-IMP-02-sensitive-value-object-generator.md) | Value-object generator, redacted surfaces, STJ/Dapper/`TypeConverter` | §6.2, §14 |
| [PII-IMP-03](progress/tasks/compliance/PII-IMP-03-serialization-targets.md) | Newtonsoft, protobuf-net, MessagePack, OpenAPI/Swashbuckle, Reqnroll targets | §6.2, §6.5 |
| [PII-IMP-04](progress/tasks/compliance/PII-IMP-04-declaration-tier-analyzers.md) | `ARKPII001/005/009/010` + code fixes + `ComplianceLexicon` | §6.1, §7, §8 |
| [PII-IMP-05](progress/tasks/compliance/PII-IMP-05-sink-tier-analyzers.md) | `ARKPII002/003/004/011` + `ComplianceSinks` | §6.3, §6.4, §7, §8 |
| [PII-IMP-06](progress/tasks/compliance/PII-IMP-06-compliance-surface-gate.md) | `ArkComplianceSurface.txt` generator + `ARKPII020/021` | §6.10, §17 PII‑04 |
| [PII-IMP-07](progress/tasks/compliance/PII-IMP-07-runtime-redaction.md) | NLog pipeline on by default + OTel processor | §6.9, §17 PII‑06 |
| [PII-IMP-08](progress/tasks/compliance/PII-IMP-08-sql-policy-generation.md) | `[SqlDataPolicy]`/`[SqlColumnPolicy]`, `ARKPII007/012`, template script | §6.6, §17 PII‑05 |
| [PII-IMP-09](progress/tasks/compliance/PII-IMP-09-test-data-rules.md) | `ARKPII006` + `Ark.Tools.Reqnroll` fakes | §6.7 |
| [PII-IMP-10](progress/tasks/compliance/PII-IMP-10-sdk-wiring-and-loggen-guards.md) | SDK wiring, `ArkComplianceMode`, `LOGGEN*` escalation, `AddArkRedaction()`, `ARKPII013`, docs | §9, §10, §13.3 |
| [PII-IMP-11](progress/tasks/compliance/PII-IMP-11-reference-project-adoption.md) | Reference-project adoption, end-to-end and AoT tests | §11, §12 |
| [PII-IMP-12](progress/tasks/compliance/PII-IMP-12-vogen-upstream-contributions.md) | Upstream `DebuggerAttributeGeneration.None` and `Conversions.Protobuf` | §14.3 |

Deferred follow-ups, deliberately not tasks yet: EF Core value converters and
Orleans surrogates for sensitive value objects (§6.2) — they must land carrying
the storage and versioning compliance semantics, not as bare converters.

## 17. Decisions

All open questions from the first draft were resolved in review; there are no
open decisions blocking implementation.

| ID | Question | Decision |
| --- | --- | --- |
| PII‑01 | Depend on `Microsoft.Extensions.Compliance.Abstractions` (MIT, 10.9.0)? | **Depend on it.** Ark attributes derive from `DataClassificationAttribute`, so classification is one vocabulary across NLog and the Microsoft stack, and the `LOGGEN` guards (§13.3) work on Ark-classified types with no bridge. |
| PII‑02 | Default severity of `ARKPII001` | **Warning.** It is the only name-heuristic rule; a false positive must not break the build. Every attribute/type-driven rule stays an error. |
| PII‑03 | `Reveal(CompliancePurpose)` gate vs. plain `Value` property | **Purpose-gated.** Cleartext is reachable only through `Reveal(CompliancePurpose)`; the purpose argument is what makes the access greppable, reviewable, and inventoriable. No plain `Value` accessor is generated. |
| PII‑04 | Is `ArkComplianceSurface.txt` separate from `ArkApiSurface.txt`? | **Separate file.** Different audience (DPO/GDPR Art. 30 vs. API compatibility), different change cadence, and a privacy diff must not be lost in an API diff. |
| PII‑05 | SQL policy output | **Opt-in template script.** A post-deployment `.sql` emitted only for `[SqlDataPolicy]` types, with SQLCMD/MSBuild token replacement because SQL naming and label taxonomies differ from the C# side (§6.6). No DACPAC refactor integration. |
| PII‑06 | Ship the runtime pattern scan | **Yes, default off.** It is the only net for third-party and dynamic payloads; keeping it off by default preserves the "analyzers do the work" property and the throughput budget. |

## 18. References

**Microsoft**
- Data classification in .NET — <https://learn.microsoft.com/dotnet/core/extensions/data-classification>
- Data redaction in .NET — <https://learn.microsoft.com/dotnet/core/extensions/data-redaction>
- Compile-time logging source generation — <https://learn.microsoft.com/dotnet/core/extensions/logging/source-generation>
- `dotnet/extensions` diagnostics list (LOGGEN, EXTEXP, AUDREPGEN) — <https://github.com/dotnet/extensions/blob/main/docs/list-of-diagnostics.md>
- LoggerMessage generator parser (`LOGGEN035`, `RecordHasSensitivePublicMembers`) — <https://github.com/dotnet/extensions/tree/main/src/Generators/Microsoft.Gen.Logging/Parsing>
- Compliance report generator — <https://github.com/dotnet/extensions/tree/main/src/Generators/Microsoft.Gen.ComplianceReports>
- Interceptors — <https://github.com/dotnet/roslyn/blob/main/docs/features/interceptors.md>
- Banned API analyzers — <https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md>
- Dynamic Data Masking — <https://learn.microsoft.com/sql/relational-databases/security/dynamic-data-masking>
- Always Encrypted (and secure enclaves) — <https://learn.microsoft.com/sql/relational-databases/security/encryption/always-encrypted-database-engine>
- `ADD SENSITIVITY CLASSIFICATION` — <https://learn.microsoft.com/sql/t-sql/statements/add-sensitivity-classification-transact-sql>
- `DE0001: SecureString shouldn't be used` — <https://github.com/dotnet/platform-compat/blob/master/docs/DE0001.md>

**Ecosystem**
- NLog serialization setup (`RegisterObjectTransformation`, `RegisterValueFormatter`) — <https://github.com/NLog/NLog/blob/dev/src/NLog/SetupSerializationBuilderExtensions.cs>
- NLog `WrapperTargetBase` — <https://github.com/NLog/NLog/blob/dev/src/NLog/Targets/Wrappers/WrapperTargetBase.cs>
- CodeQL C# sensitive-data heuristics — <https://github.com/github/codeql/blob/main/csharp/ql/lib/semmle/code/csharp/security/SensitiveActions.qll>
- CodeQL C# query help — <https://codeql.github.com/codeql-query-help/csharp/>
- Sonar taint analysis is server-side — <https://docs.sonarsource.com/sonarqube-for-visual-studio/using/taint-vulnerabilities>
- Vogen — <https://github.com/SteveDunn/Vogen>
- Vogen `#696` (generator ordering brick wall, maintainer) — <https://github.com/SteveDunn/Vogen/issues/696>
- Vogen `#834` (same limitation, PolyType author) — <https://github.com/SteveDunn/Vogen/issues/834>
- Vogen contribution scope ("consider writing an add-on/plugin library") — <https://github.com/SteveDunn/Vogen/blob/main/CONTRIBUTING.md>
- Vogen protobuf-net guidance (hand-written surrogate) — <https://github.com/SteveDunn/Vogen/blob/main/docs/site/Writerside/topics/reference/FAQ.md>
- `Ark.Tools.Protobuf` / `Ark.Tools.MessagePack` (surrogate and formatter shapes reused) — `src/common/`
- `Serilog.Enrichers.Sensitive` — <https://github.com/serilog-contrib/Serilog.Enrichers.Sensitive>
- `Destructurama.Attributed` — <https://github.com/destructurama/attributed>
- OpenTelemetry .NET log redaction — <https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/logs/redaction/README.md>
- OpenTelemetry handling sensitive data — <https://opentelemetry.io/docs/security/handling-sensitive-data/>
- `filipw/tasmaniandevil` (runtime PII recognisers, Presidio-style) — <https://github.com/filipw/tasmaniandevil>
- Bogus — <https://github.com/bchavez/Bogus>

**Standards**
- GDPR Art. 25 — <https://gdpr-info.eu/art-25-gdpr/>; Art. 5 — <https://gdpr-info.eu/art-5-gdpr/>; Art. 32 — <https://gdpr-info.eu/art-32-gdpr/>
- OWASP Top 10 A02:2021 — <https://owasp.org/Top10/2021/A02_2021-Cryptographic_Failures/>; A09:2021 — <https://owasp.org/Top10/2021/A09_2021-Security_Logging_and_Monitoring_Failures/>
- OWASP ASVS (data protection chapter; verify numbering for the targeted release) — <https://owasp.org/www-project-application-security-verification-standard/>
- NIST SP 800‑122 — <https://csrc.nist.gov/pubs/sp/800/122/final>
- CWE‑532 — <https://cwe.mitre.org/data/definitions/532.html>; CWE‑359 — <https://cwe.mitre.org/data/definitions/359.html>; CWE‑209 — <https://cwe.mitre.org/data/definitions/209.html>

**Related Ark.Tools documents**
- [`design.md`](design.md) — SDK architecture that this PRD plugs into.
- [`progress/decisions.md`](progress/decisions.md) — `SDK-20`/`SDK-21`/`SDK-24` analyzer and banned-API policy.
- [`../analyzers.md`](../analyzers.md) — current analyzer inventory and diagnostic IDs.
