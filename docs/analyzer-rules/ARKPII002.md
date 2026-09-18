# ARKPII002: Classified data reaches a log

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `Classified member '{0}' ([{1}]) reaches a log template, argument, or scope; log the key, not the person, or pass the value through a redactor. Analysis is intra-method only`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

The receiver of a logging sink is not analysed as data: instance receivers and the `this` argument of extension methods (for example the `ILogger<TCategory>` passed to `LogInformation`) never taint the call.

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

Use the standard `dotnet_diagnostic.ARKPII002.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII002.md`.
