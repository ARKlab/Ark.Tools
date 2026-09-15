# ARKPII013: Register Ark redaction for Microsoft telemetry

- **Severity:** Warning
- **Component:** Compliance
- **Diagnostic message:** `Project references Microsoft.Extensions.Telemetry but does not call AddArkRedaction(); classified logging can remain unredacted`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// In an executable project referencing Microsoft.Extensions.Telemetry:
// telemetry is configured, but Ark redaction is not registered.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry();

var app = builder.Build();
app.Run();
```

### Correct

```csharp
// In an executable project referencing Microsoft.Extensions.Telemetry:
// register Ark redaction when using Microsoft telemetry.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddArkRedaction();
builder.Services.AddOpenTelemetry();

var app = builder.Build();
app.Run();
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII013.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII013.md`.
