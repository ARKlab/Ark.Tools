# ARKPII203: Cleartext ToString is not allowed

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `Sensitive value object '{0}' cannot declare a ToString method`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule: sensitive value object declares cleartext ToString.
[SensitiveValueObject<string>]
public readonly partial struct ClassifiedValue
{
    public override string ToString() => "cleartext";
}
```

### Correct

```csharp
// Compliant: the generator provides a redacted ToString.
[SensitiveValueObject<string>]
public readonly partial struct ClassifiedValue { }
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII203.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII203.md`.
