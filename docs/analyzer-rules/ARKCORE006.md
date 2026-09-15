# ARKCORE006: Capture the caught exception

- **Severity:** Error
- **Component:** Core analyzers
- **Diagnostic message:** `Name the caught exception before throwing a replacement so it can be used as the inner exception`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule
// See the diagnostic message for the required enum or exception change.
```

### Correct

```csharp
// Apply the declaration or exception-handling change requested by the diagnostic.
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKCORE006.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE006.md`.
