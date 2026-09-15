# ARKPII202: Invalid sensitive value object declaration

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `Sensitive value object '{0}' must be declared as a non-generic readonly partial struct`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
// Violates the rule: generic and not readonly partial struct.
[SensitiveValueObject<string>]
public partial struct SensitiveValue<T>
{ }
```

### Correct

```csharp
// Sensitive value object must be a non-generic readonly partial struct.
[SensitiveValueObject<string>]
public readonly partial struct SensitiveValue { }
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII202.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII202.md`.
