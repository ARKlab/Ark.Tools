# ARKPII009: Explain why a declaration contains no personal data

- **Severity:** Warning
- **Component:** Compliance
- **Diagnostic message:** `NotPersonalData on '{0}' needs a specific justification, not a placeholder; incorrectly excluded data escapes redaction`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule
logger.LogInformation("{Value}", classifiedValue);
```

### Correct

```csharp
// Use the approved classification, purpose, policy, or redactor described by the diagnostic.
logger.LogInformation("Value available");
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII009.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII009.md`.
