# ARKPII204: Invalid sensitive value object hook

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `Sensitive value object '{0}' declares '{1}' with an unsupported signature; it must be 'private static {2} {1}(string value)'`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule: unsupported hook signature (wrong parameter type)
[SensitiveValueObject<string>]
public readonly partial struct ClassifiedValue
{
    private static string _normalize(int value)
    {
        return value.ToString();
    }
}
```

### Correct

```csharp
// Correct: hook matches required signature `private static {type} {hook}(string value)`
[SensitiveValueObject<string>]
public readonly partial struct ClassifiedValue
{
    private static string _normalize(string value)
    {
        return value.Trim();
    }
}
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII204.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII204.md`.
