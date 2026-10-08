# ARKMSG028: Participant lists a contract more than once

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `Participant '{0}' lists '{1}' more than once in {2}`

## What it checks

A participant's `Processes`, `Publishes` and `Subscribes` lists each name a
contract at most once. A duplicate entry would otherwise pass the build and
fail at startup, when the resource manifest is validated. The diagnostic is
reported at the participant, once for each duplicated contract in each list.

## How to fix it

Remove the repeated entry from the list.

### Incorrect

```csharp
[MessagingParticipant(
    Subscribes = new[] { typeof(BookPrintCompleted), typeof(BookPrintCompleted) })]
public sealed partial class AuditParticipant;
```

### Correct

```csharp
[MessagingParticipant(Subscribes = new[] { typeof(BookPrintCompleted) })]
public sealed partial class AuditParticipant;
```

## Suppression and configuration

Do not suppress this rule: the host fails at startup.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMSG028.md`.
