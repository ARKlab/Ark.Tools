# ARKMSG027: Processed contract cannot be dispatched

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `Participant '{0}' processes '{1}', which implements neither ICommand<TSelf> nor IRequest<TSelf, TResponse>`

## What it checks

A contract listed in a participant's `Processes` is a message. The generated
receiver dispatches a message through `ICommandProcessor` when it implements
`ICommand<TSelf>`, or through `IRequestProcessor` when it implements
`IRequest<TSelf, TResponse>` (the response is discarded). Any other shape, such
as a query, has no handler the receiver could run, so every delivery would be
dead-lettered as an unknown contract.

## How to fix it

Make the contract a command or a request, or remove it from `Processes`.

### Incorrect

```csharp
[Message(Name = "books.lookup_book")]
public sealed record LookupBook : IQuery<LookupBook, Book>;

[MessagingParticipant(Processes = new[] { typeof(LookupBook) })]
public sealed partial class LookupParticipant;
```

### Correct

```csharp
[Message(Name = "books.print_book")]
public sealed record PrintBook : ICommand<PrintBook>;

[MessagingParticipant(Processes = new[] { typeof(PrintBook) })]
public sealed partial class PrintingParticipant;
```

## Suppression and configuration

Do not suppress this rule: the receiver cannot dispatch the contract.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMSG027.md`.
