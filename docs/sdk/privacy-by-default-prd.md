# PRD — Privacy by Default for Ark.Tools

Status: **proposed; research complete; decisions open (see [Open decisions](#16-open-decisions))**.

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
machine-readable privacy inventory, i.e. GDPR Art. 30 evidence produced by the build.

**Why Ark.Tools does not get this for free.** Ark logs through NLog's `Logger`
(`LogManager.GetCurrentClassLogger()`, `_logger.Info(CultureInfo.InvariantCulture,
"…{Tag}…", value)`), not through `[LoggerMessage]` partial methods. `LOGGEN*`
never runs on Ark code, and the redaction pipeline (`AddRedaction()` +
`EnableRedaction()`) is `Microsoft.Extensions.Telemetry`-only. Adopting the
Microsoft classification *vocabulary* is valuable; adopting its *enforcement* is
not possible without rewriting every call site (see [§13.3](#133-rejected-migrate-ark-logging-to-loggermessage--loggen)).

| Item | Latest (2026‑09) | License | Compile-time | AoT | Maintained | Verdict for Ark |
| --- | --- | --- | --- | --- | --- | --- |
| `Microsoft.Extensions.Compliance.Abstractions` | 10.9.0 | MIT | attributes only | yes | yes | **Adopt vocabulary** (dep decision PII‑01) |
| `Microsoft.Extensions.Compliance.Redaction` | 10.9.0 | MIT | no | yes | yes | Adopt `Redactor` shape |
| `Microsoft.Extensions.Telemetry.Abstractions` (LOGGEN) | 10.9.0 | MIT | **yes** | yes | yes | Reference design; not usable directly |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | 5.6.0 | MIT | yes | n/a | yes | **Already in SDK** — extend `BannedSymbols.Ark.Tools.txt` |
| `Microsoft.CodeAnalysis.AnalyzerUtilities` | 5.6.0 | MIT | yes | n/a | yes | Optional; taint types are `internal` |
| CodeQL C# security queries | rolling | MIT (queries) | CI-time | n/a | yes | Complementary; heuristic-only |
| `SonarAnalyzer.CSharp` | 10.33.0.1635 | file (LGPL-ish) | taint rules **do not run** from NuGet | n/a | yes | Rejected as gate |
| `SecurityCodeScan.VS2019` | 5.6.7 | LGPL‑3.0 | yes | n/a | **abandoned 2022** | Rejected |
| `Puma.Security.Rules.2019` | 2.4.23 | MPL‑2.0 | yes | n/a | package stale | Rejected |
| `LeakGuard` | 0.4.0 | MIT | claimed | ? | ~92 downloads, no repo | Rejected (unverifiable) |
| `Vogen` | 8.0.7 | Apache‑2.0 | **yes** (`VOG008/009/010/025`) | struct | yes | Design model for our value objects |
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
- No tool bridges C# classification attributes to SQL
  `ADD SENSITIVITY CLASSIFICATION` / `MASKED WITH` DDL.
- C# interceptors are stable since the **.NET 9.0.2xx SDK** (per
  `dotnet/roslyn/docs/features/interceptors.md`), not "new in C# 14" as several
  blogs claim. They intercept ordinary methods only, opt-in via
  `<InterceptorsNamespaces>`, and have weak IDE traceability.

## 5. Solution shape

Five layers, in order of authority. A leak must pass all five.

1. **Declare** — classification attributes and sensitive value objects
   (`Ark.Tools.Privacy`).
2. **Refuse** — `ARKPII*` analyzers turn use-at-a-sink into a compile error
   (`Ark.Tools.Privacy.Analyzers`, shipped inside `Ark.Tools.Privacy`, wired by
   `Ark.Tools.Sdk`).
3. **Inventory** — a generated, committed `ArkPrivacySurface.txt` snapshot; new or
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
see the same classification (subject to decision PII‑01).

```csharp
namespace Ark.Tools.Privacy;

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
                 Apply [PersonalData], use an Ark.Tools.Privacy value object, or apply
                 [NotPersonalData("<why>")]. Unclassified personal data is not redacted
                 in logs, not masked in SQL, and not listed in the privacy inventory.
```

with code fixes: *Add `[PersonalData]`* · *Change type to `EmailAddress`* ·
*Add `[NotPersonalData]`…*.

### 6.2 Sensitive value objects

For string-shaped PII, a value object is stronger than an attribute: it travels
with the value through locals, method parameters, and returns, so the analyzer
does not need inter-procedural flow analysis to keep protecting it.

```csharp
namespace Ark.Reference.Core.Common.Dto;

using Ark.Tools.Privacy;

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
  `Reveal(PrivacyPurpose purpose)` method;
- `System.Text.Json` converter (source-generated, `JsonSerializerContext`-friendly),
  Dapper `SqlMapper.TypeHandler`, `TypeConverter`, and a Reqnroll
  retriever/comparer registration — because serialisation is expected and must not
  be the reason people avoid the type;
- equality/hash over the normalised value.

Ark ships ready-made ones so most projects never write their own:
`EmailAddress`, `PhoneNumber`, `NationalIdentificationNumber`, `Iban`,
`PostalAddress`, `PersonName`, `IpAddressValue`, `ApiKey`, `BearerToken`.

Reading cleartext is deliberate and visible:

```csharp
// Compiles. Explicit, greppable, and recorded in the privacy inventory.
var body = new MailMessage(from, customer.Email.Reveal(PrivacyPurpose.SendTransactionalEmail));

// error ARKPII005: 'EmailAddress.Reveal' requires a PrivacyPurpose; implicit conversion to string is banned.
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
// OK, and recorded in ArkPrivacySurface.txt as an egress of PersonalData.
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
                   lawful purpose in the privacy inventory.
```

Value-object converters make this transparent: `EmailAddress` serialises as the
cleartext string on the wire (a JSON converter is an explicit egress and is
therefore exempt from `ARKPII005`), while `ToString()` everywhere else stays redacted.

### 6.6 Persistence policy

Classified members must declare how they are stored. The generator turns the
declaration into DDL, closing the "PII not policy-masked in DB" gap.

```csharp
public sealed record CustomerEntity
{
    [PersonalData]
    [PersonalDataStorage(StoragePolicy.Masked, MaskFunction = SqlMask.Email)]
    public EmailAddress Email { get; init; }

    [SensitivePersonalData]
    [PersonalDataStorage(StoragePolicy.ApplicationEncrypted, KeyName = "cmk-customer")]
    public string? DietaryNotes { get; init; }
}
```

Build output (`obj/…/generated/…/ArkPrivacy.Sql/CustomerEntity.privacy.sql`),
picked up by the database project as a post-deployment script:

```sql
ADD SENSITIVITY CLASSIFICATION TO [dbo].[Customer].[Email]
    WITH (LABEL = 'Confidential - GDPR', INFORMATION_TYPE = 'Contact Info', RANK = HIGH);

ALTER TABLE [dbo].[Customer]
    ALTER COLUMN [Email] ADD MASKED WITH (FUNCTION = 'email()');
```

`ApplicationEncrypted` instead generates the Dapper type handler that encrypts on
write/decrypts on read (`Always Encrypted` remains available and is preferred when
the deployment supports it; the generator emits the `ENCRYPTED WITH` column
definition in that mode). A missing `[PersonalDataStorage]` on a classified member
of a type used by `Ark.Tools.Sql`/Dapper is `ARKPII007` (error), because
"unmasked by omission" is the exact failure we are eliminating.

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
[PrivacyReviewed("ARKPII002", "Ticket ARK-1234: support runbook requires the masked local part.", Expires = "2027-01-01")]
private void _logSupportContext(Customer c) { … }

// 2. Standard pragma, for one line.
#pragma warning disable ARKPII002 // support runbook, ARK-1234
    _logger.Debug(CultureInfo.InvariantCulture, "ctx {Email}", c.Email.Redacted());
#pragma warning restore ARKPII002

// 3. Project-wide severity, via the packaged global config (discouraged, visible in review).
// dotnet_diagnostic.ARKPII001.severity = suggestion
```

`ARKPII008` warns when `[PrivacyReviewed]` is missing a reason or is past
`Expires`, so exceptions rot loudly instead of silently.

### 6.9 Runtime redaction (second net)

`Ark.Tools.Privacy.NLog` adds one wrapper target and one value formatter to the
existing `NLogConfigurer` chain. NLog has no log-event interceptor, so the wrapper
target is the correct place: it sees the `LogEventInfo` once, before fan-out to
console/file/database/Slack/mail.

```csharp
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .WithPrivacyRedaction(o =>
    {
        o.Default = ArkRedaction.Erase;                        // fail closed
        o.For(ArkDataClassifications.PersonalData, ArkRedaction.Hmac);
        o.For(ArkDataClassifications.Secret, ArkRedaction.Erase);
        o.PatternScan = PatternScanMode.MessageAndProperties;  // last-resort text scan
    })
    .Apply();
```

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
   country prefixes, digit runs). Off by default in `Trace`/`Debug`-heavy
   pipelines; measured budget: ≤ 2 µs per event for a 200-char message. Regexes
   are source-generated with a timeout, never `RegexOptions.Compiled` at runtime.

`Ark.Tools.OTel` gains `ArkPrivacyRedactionProcessor : BaseProcessor<Activity>`
following the existing `ArkPreFilterProcessor`/`ArkTelemetryEnrichmentProcessor`
pattern, applying the same `Redactor` to tag values.

> The runtime layer is deliberately dumb. If it ever fires in production, that is
> a bug report against the analyzers, and the mask string (`***ARKPII***`) is
> designed to be alertable in the log platform.

### 6.10 Privacy inventory

`ArkPrivacySurface.txt` is generated next to the existing `ArkApiSurface.txt`,
committed, and diffed by the build.

```
PRIVACY-SURFACE 1
CLASSIFIED Ark.Reference.Core.Common.Dto.CustomerDto.Email
  CLASSIFICATION Ark:PersonalData
  TYPE Ark.Reference.Core.Common.Dto.EmailAddress
  STORAGE Masked(email)
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

`Category = "Privacy"`. Shipped through `AnalyzerReleases.*.md` like the existing
`ARKCORE*`/`ARKSOLID*` rules, documented in `docs/analyzers.md`, severities set in
a packaged `Ark.Tools.Privacy.globalconfig`.

| ID | Severity | Rule |
| --- | --- | --- |
| ARKPII001 | Warning | PII-suggestive member/parameter/local is not classified |
| ARKPII002 | **Error** | Classified value reaches a log template, argument, or scope |
| ARKPII003 | **Error** | Classified value reaches an exception message or `Exception.Data` |
| ARKPII004 | **Error** | Classified value reaches an `Activity` tag, metric dimension, or baggage |
| ARKPII005 | **Error** | Implicit `ToString`/interpolation/concat/`Reveal` without purpose on a classified value |
| ARKPII006 | Warning | Test data literal looks like real personal data |
| ARKPII007 | **Error** | Classified member persisted without `[PersonalDataStorage]` |
| ARKPII008 | Warning | `[PrivacyReviewed]` lacks a reason or is expired |
| ARKPII009 | Warning | `[NotPersonalData]` justification is missing or boilerplate |
| ARKPII010 | **Error** | Classification attribute on a member the pipeline cannot redact (open `object`, `dynamic`, delegate) |
| ARKPII011 | **Error** | Classified value passed to a banned formatting sink (`Console.*`, `Debug.*`, `Trace.*`, `StringBuilder.Append`) |
| ARKPII012 | Warning | Contract exposes personal data with no declared egress purpose |
| ARKPII020 | **Error** | `ArkPrivacySurface.txt` drift |
| ARKPII021 | **Error** | `ArkPrivacySurface.txt` missing or malformed |

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

Sinks are declared in data, not code: a packaged `PrivacySinks.Ark.txt`
`AdditionalFiles` document in Documentation-Comment-ID format, identical in shape
to `BannedSymbols.Ark.Tools.txt`, so a consumer can add their own sinks
(a custom audit logger, a legacy `Trace` wrapper) without forking the analyzer.

```
M:NLog.Logger.Info(System.IFormatProvider,System.String,System.Object[]);log
M:System.Diagnostics.Activity.SetTag(System.String,System.Object);telemetry
M:MyCompany.Legacy.AuditWriter.Write(System.String);log
```

The lexicon for `ARKPII001` is likewise data: a packaged, overridable
`PrivacyLexicon.Ark.txt` (`email`, `mail`, `phone`, `mobile`, `ssn`, `taxcode`,
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
| `Ark.Tools.Privacy` | attributes, taxonomy, `Redactor`s, value objects, `Reveal`/`PrivacyPurpose`; ships the analyzer + generator + code-fix DLLs as `analyzers/dotnet/cs` (same pattern as `Ark.Tools.Core`) | `net8.0;net10.0` |
| `Ark.Tools.Privacy.Analyzers` (+ `.CodeFixes`) | `IsPackable=false`, packed into the above | `netstandard2.0` |
| `Ark.Tools.Privacy.NLog` | `RedactingTargetWrapper`, `IValueFormatter`, `NLogConfigurer.WithPrivacyRedaction` | `net8.0;net10.0` |
| `Ark.Tools.Privacy.Sql` | Dapper handlers for encrypted columns, DDL generation targets | `net8.0;net10.0` |
| `Ark.Tools.OTel` (existing) | `ArkPrivacyRedactionProcessor` | unchanged |
| `Ark.Tools.Sdk` / `Ark.Tools.Build` (existing) | implicit `PackageReference` (`EnableArkToolsPrivacy`), packaged `Ark.Tools.Privacy.globalconfig`, `PrivacySinks`/`PrivacyLexicon` `AdditionalFiles`, `ArkPrivacySurface.txt` gate | unchanged |

Opt-out follows the SDK convention already established
(`EnableArkToolsPrivacy=false`, per-rule severity overrides), and the analyzer
package is `PrivateAssets="all"` / `developmentDependency`.

## 10. Rollout

Adoption of an error-by-default rule set in existing solutions needs a ramp:

1. `ArkPrivacyMode=Observe` — every rule at `suggestion`; the generator still emits
   `ArkPrivacySurface.txt`, which becomes the backlog.
2. `ArkPrivacyMode=Warn` — default for the first minor release; `ARKPII020` drift
   already an error, so no *new* undeclared PII can be added.
3. `ArkPrivacyMode=Enforce` — the table in §7; default from the next major.

## 11. Testing

- `Microsoft.CodeAnalysis.Testing` verifier tests per rule: positive, negative,
  suppression, and code-fix cases, including the shapes `LOGGEN035` misses
  (non-record nested classes, tuples, collection element types).
- Generator snapshot tests (existing `GeneratorSnapshotTests` pattern).
- Reqnroll integration test in the reference project: an end-to-end scenario that
  logs a `Customer` through the real `NLogConfigurer` chain and asserts the
  database target row contains the mask, never the address.
- A clean-consumer SDK test (existing `tests/Ark.Tools.Sdk.Tests` fixture) proving
  the rules and configuration flow through the packages.
- AoT/trim smoke test: publish the reference API with
  `PublishAot`/`PublishTrimmed` and assert no new warnings.

## 12. Success criteria

- Zero unclassified PII-suggestive members in the reference project.
- `ArkPrivacySurface.txt` reviewed on every PR that changes it.
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

### 13.3 Rejected: migrate Ark logging to `[LoggerMessage]` + LOGGEN

Technically the strongest existing enforcement (`LOGGEN035` + the
`ClassifiedTagArray`/`RedactedTagArray` split is exactly the design we want), but
adopting it means: rewriting every `_logger.Info(CultureInfo.InvariantCulture, …)`
call site in Ark.Tools, its samples, and every consumer solution into partial
logging methods; taking `Microsoft.Extensions.Telemetry` (and its `ILogger`
pipeline) as a hard dependency alongside NLog; and accepting that redaction is
active only when `AddRedaction()` **and** `EnableRedaction()` are both wired — a
silent-degradation failure mode. Rejected as a migration; **adopted as prior art**:
we copy the classification vocabulary, the fail-closed default (`ErasingRedactor`),
the compliance-report artifact idea, and the "abort generation" trick, and we fix
its record-only recursion limitation.

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

`Vogen` 8.0.7 is the best-in-class value-object generator and its analyzer model
(`VOG008/009/010/025`: no user constructors, no `default`, no `new`, no reflection
construction) is exactly what a sensitive type needs, so its design is adopted.
A dependency is rejected because Ark needs the generated type to *also* carry
classification metadata, a redacting `ToString`, a `Reveal(PrivacyPurpose)` gate,
the NLog transformation registration, the Dapper/Reqnroll/JSON plumbing, and an
entry in `ArkPrivacySurface.txt` — i.e. the generator output is the integration
point, not an add-on. `StronglyTypedId` has no stable release; `ValueOf` is
class-based (allocating), analyzer-less, and deprecated by its own author.

### 13.9 Rejected: `BannedApiAnalyzers` alone

Banning `object.ToString()` on sensitive types is useful and **is part of the
design** (the SDK already ships `BannedSymbols.Ark.Tools.txt`), but banning is
symbol-identity based: it cannot know that a `string` local holds an email, cannot
inspect message templates, and cannot produce the inventory. It is a complement,
not the mechanism.

### 13.10 Rejected: a separate "privacy" repository/package family

Considered shipping this as a standalone product. Rejected: the value comes from
being *on by default* in `Ark.Tools.Sdk`, from reusing the existing analyzer
configuration, snapshot-gate, and NLog/OTel/Dapper integration points, and from
one version line. It follows the accepted SDK architecture (`SDK-01`, `SDK-24`).

## 14. Risks

| Risk | Mitigation |
| --- | --- |
| False positives make teams disable the rules | `ARKPII001` is the only heuristic rule and starts as a warning; lexicon and sinks are consumer-editable data files; three-stage rollout |
| Analyzer cost in large solutions | Symbol/type tiers dominate; local flow is intra-method only; benchmarks in CI |
| Developers use `Reveal()` reflexively | `Reveal` requires a `PrivacyPurpose`, is greppable, and appears in the inventory diff |
| New MS dependency conflicts with CPM/lock files | Decision PII‑01 keeps a zero-dependency fallback; lock files updated per repo policy |
| Inventory churn noise | Snapshot format is deterministic and ordinal-sorted, like `ArkApiSurface.txt` |

## 15. Delivery outline

Task documents will follow the `docs/sdk/progress/tasks` convention
(`PII-IMP-nn`), each self-contained, each leaving
`dotnet build Ark.Tools.slnx` and `dotnet test Ark.Tools.slnx` green.

1. `Ark.Tools.Privacy` attributes, taxonomy, redactors.
2. Sensitive value-object generator + built-in types.
3. `ARKPII001/005/009/010` (declaration tier) + code fixes.
4. `ARKPII002/003/004/011` (sink tier) + `PrivacySinks` additional file.
5. `ArkPrivacySurface.txt` generator and `ARKPII020/021` gate.
6. `Ark.Tools.Privacy.NLog` redaction pipeline + OTel processor.
7. `ARKPII007/012` + SQL policy generation (`Ark.Tools.Privacy.Sql`).
8. `ARKPII006` + `Ark.Tools.Reqnroll` fakes.
9. SDK wiring, `ArkPrivacyMode`, packaged configuration, docs
   (`docs/analyzers.md`, migration notes).
10. Reference-project migration and end-to-end tests.

## 16. Open decisions

| ID | Question | Options |
| --- | --- | --- |
| PII‑01 | Depend on `Microsoft.Extensions.Compliance.Abstractions` (MIT, 10.9.0)? | **A** derive from `DataClassificationAttribute` for interop (new dependency, needs approval) · **B** Ark-owned attributes, zero dependency, optional bridge package |
| PII‑02 | Default severity of `ARKPII001` at GA | Warning (proposed) · Error |
| PII‑03 | `Reveal(PrivacyPurpose)` gate vs. plain `Value` property | Purpose gate (proposed) · plain accessor + banned-symbol entry |
| PII‑04 | Is `ArkPrivacySurface.txt` a separate file or a section of `ArkApiSurface.txt`? | Separate (proposed) · merged |
| PII‑05 | SQL policy output: post-deployment script vs. DACPAC pre/post refactor | Script (proposed) · DACPAC integration |
| PII‑06 | Ship the runtime pattern scan at all, given it can only fire on analyzer misses | Yes, default off (proposed) · No |

## 17. References

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
