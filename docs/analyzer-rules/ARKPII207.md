# ARKPII207: Invalid SQL policy mapping

- **Severity:** Error
- **Component:** Compliance
- **Diagnostic message:** `SQL policy for '{0}' is invalid: {1}`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
[SqlDataPolicy(Table = "Customers")]
public sealed class Customer
{
    [SqlColumnPolicy("", StoragePolicy.None)]
    public string Email { get; set; } = string.Empty;
}
```

### Correct

```csharp
[SqlDataPolicy(Schema = "sales", Table = "Customers")]
public sealed class Customer
{
    [SqlColumnPolicy("Email", StoragePolicy.Masked)]
    public string Email { get; set; } = string.Empty;
}
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKPII207.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKPII207.md`.
