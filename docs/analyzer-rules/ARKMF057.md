# ARKMF057: Use a valid Rebus participant host binding

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `{0}. Configure a supported Rebus participant host binding`

## What it checks

This Ark.Tools diagnostic identifies the condition described above and reports it at the relevant declaration, contract, or generated-code input. The diagnostic message includes the contextual symbol and the required correction where applicable.

## How to fix it

Apply the correction stated by the diagnostic. Do not suppress the rule when changing the declaration, contract, host configuration, or data flow is possible.

### Incorrect

```csharp
[ArkRebusHost(typeof(OrphanParticipant))]
public sealed partial class OrphanRebusHost;
```

### Correct

```csharp
[MessagingNetwork(Members = new[] { typeof(Participant) })]
public sealed class OrdersNetwork;

[ArkRebusHost(typeof(Participant))]
public sealed partial class OrdersRebusHost;
```

## Suppression and configuration

Use the standard `dotnet_diagnostic.ARKMF057.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF057.md`.
