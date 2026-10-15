# ARKCORE004: Evolvable enum cannot evolve

- **Severity:** Warning
- **Component:** Core analyzers
- **Diagnostic message:** `Leave at least one unused value in enum '{0}' backing type '{1}' for future members`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

The diagnostic is reported once for each `EvolvableEnum<T>` or `EvolvableEnum<T, TBacking>` usage, on the enum type argument of that usage, with the enum declaration as an additional location. An invalid enum used in several places therefore gets one diagnostic per usage, each in the file that contains the usage.

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

Use the standard `dotnet_diagnostic.ARKCORE004.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKCORE004.md`.
