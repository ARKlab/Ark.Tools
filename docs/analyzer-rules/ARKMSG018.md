# ARKMSG018: Invalid event contract

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `Event contract '{0}' must implement ICommand<TSelf> (requests and queries cannot be events)`

## What it checks

An event is a contract marked `[Event]`, listed in a participant's `Publishes`, or listed in a participant's `Subscribes`. It must implement `ICommand<TSelf>`. A request (`IRequest<TSelf, TResponse>`) is a valid message for `Processes`, but never an event: nobody receives its response.

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

Use the standard `dotnet_diagnostic.ARKMSG018.severity` EditorConfig setting only for an intentional exception. Prefer a documented, reviewed suppression over disabling the rule globally.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMSG018.md`.
