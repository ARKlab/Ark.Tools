# ARKMSG023: Messaging declaring type must be partial

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `Type '{0}' is marked with [{1}] but is not a non-nested, non-generic partial class, so its routing members cannot be generated`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule
// Generated contract or host declaration does not satisfy the diagnostic.
```

### Correct

```csharp
// Correct the contract or host declaration as requested by the diagnostic.
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKMSG023.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMSG023.md`.
