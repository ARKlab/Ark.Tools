# Runtime redaction

Compile-time analyzers are the first net; runtime redaction is the second, for what
they cannot see (third-party code, dynamic payloads, `Exception.ToString()` of
foreign exceptions).

## What redacts by itself

Every sensitive value object renders redacted on **all** formatting surfaces —
`ToString()`, interpolation, `IFormattable`, `ISpanFormattable`, debugger display,
`TypeConverter` display. No configuration involved: if a generated type reaches a
log line, the log line contains the mask.

| Redactor | Output |
| --- | --- |
| `ArkErasingRedactor` | `***` |
| `ArkMaskingRedactor` | `***` (never emits source characters) |
| `ArkHmacRedactor` | `hmac:<64 hex>` with a key; **`***` without one (fail-closed)** |
| `ArkNullRedactor` | pass-through (for `ArkRedaction.None` types) |

## NLog via Ark.Tools.NLog (automatic)

`Ark.Tools.NLog` references `Ark.Tools.Compliance.NLog` and wraps its own message,
text, and properties layouts (console, file, database `Properties`/`Message`/
`ExceptionMessage` columns, Slack) in a `ComplianceLayout`. **This wiring is always
in place** — you do not call anything to get it:

```csharp
// Redaction wiring is already active here.
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .Apply();
```

Two knobs exist, both on the configurer:

```csharp
// Override the defaults — e.g. enable the last-resort PII text scan
// (off by default; it has a measurable, small cost).
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .WithComplianceRedaction(o =>
    {
        o.PiiScan = PiiScanMode.MessageAndProperties;
    })
    .Apply();

// Explicit, greppable opt-out.
NLogConfigurer.For(appName)
    .WithArkDefaultTargetsAndRules(config)
    .WithoutComplianceRedaction()
    .Apply();
```

Unrelated layouts (e.g. the database stack-trace column) are not wrapped — the
scan touches only Ark's own affected layouts, and NLog events are neither cloned
nor cached.

## The PII scanner

`PiiScanner` is a last-resort single pass over *rendered* layout text using
source-generated, timeout-bounded regexes (emails, IBANs, digit runs) with
`SearchValues` prefilters. Matches are replaced with the marker `***ARKPII***`.

It is **off by default** (decision: enabling it silently would hide analyzer gaps).
When you enable it and the marker shows up in production logs, treat it as a bug
report: something classified reached a log without being declared. The marker is
designed to be alertable in your log platform.

## Stand-alone NLog (without Ark.Tools.NLog)

Reference `Ark.Tools.Compliance.NLog` and wrap the layouts you own:

```xml
<PackageReference Include="Ark.Tools.Compliance.NLog" />
```

```csharp
using Ark.Tools.Compliance;
using Ark.Tools.Compliance.NLog;

var scanner = new PiiScanner(PiiScanMode.MessageAndProperties);

var target = new FileTarget("app")
{
    Layout = new ComplianceLayout(Layout.FromString("${longdate} ${message}"), () => scanner),
};
```

`ComplianceLayout` takes the inner layout and a scanner provider; when the provider
returns `null` or a disabled scanner, it is a pass-through.

For correct message-template handling on the setup builder there is also:

```csharp
LogManager.Setup().SetupSerialization(s => s.UseComplianceRedaction());
```

## Microsoft.Extensions.Logging / Telemetry

If you log through `Microsoft.Extensions.Telemetry` (e.g. `[LoggerMessage]` with
classified parameters), register Ark's redactors so Microsoft's log redaction uses
them:

```csharp
services.AddArkRedaction();
```

This maps `Ark:PersonalData` to the masking redactor, the other classifications to
the erasing redactor (fail-closed fallback included), and calls
`EnableRedaction()` on the logging builder. `ARKPII013` warns when a project uses
Microsoft telemetry logging without this call.

## HMAC correlation

Types declared with `ArkRedaction.Hmac` render as a stable `hmac:` digest so
support can correlate the same value across log lines without learning it. The key
comes from the `ARK_TOOLS_COMPLIANCE_HMAC_KEY` environment variable; without it the
rendering falls back to `***`. Rotate the key per environment/tenant — the digest
is only stable within one key.
