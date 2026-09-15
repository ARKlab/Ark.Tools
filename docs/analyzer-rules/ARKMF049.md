# ARKMF049: Invalid Azure Functions host contract selection

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `Type '{0}' in the host {1} list is not an [HttpEndpoint] contract declared by assembly '{2}'`

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

Use the standard `dotnet_diagnostic.ARKMF049.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF049.md`.
