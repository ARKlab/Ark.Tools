# ARKMF041: Invalid Storage Queue message encoding

- **Severity:** Warning
- **Component:** Mediator Framework
- **Diagnostic message:** `host.json extensions.queues.messageEncoding must be the literal 'none'`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
{
  "version": "2.0",
  "extensions": {
    "queues": { "messageEncoding": "base64" }
  }
}
```

### Correct

```csharp
{
  "version": "2.0",
  "extensions": {
    "queues": { "messageEncoding": "none" }
  }
}
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKMF041.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF041.md`.
