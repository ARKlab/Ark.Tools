# ARKPII201: Unsupported sensitive value type

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `Sensitive value object '{0}' must use string as its underlying type`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule: sensitive value object uses a non-string underlying type.
[SensitiveValueObject<int>]
public readonly partial struct CustomerSsn
{
    public int Value { get; }
}
```

### Correct

```csharp
// Complies with the rule: sensitive value object uses string as its underlying type.
[SensitiveValueObject<string>]
public readonly partial struct CustomerSsn
{
    public string Value { get; }
}
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII201.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII201.md`.
