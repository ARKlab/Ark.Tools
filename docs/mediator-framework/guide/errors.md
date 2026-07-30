# Errors

Handlers report expected domain failures with domain exceptions. The host
converts them into RFC 7807 Problem Details for HTTP and `Google.Rpc.Status`
for gRPC.

## Return a business failure

```csharp
if (await _store.ExistsAsync(request.Name, cancellationToken).ConfigureAwait(false))
{
    throw new BusinessRuleViolationException(
        new GreetingAlreadyExistsViolation(request.Name));
}
```

Register `AddArkProblemDetailsExceptionHandler()` and place
`UseArkProblemDetailsExceptionHandler()` at the outer edge of the HTTP
pipeline.

**Outcome:** callers receive a stable, machine-readable business error rather
than a successful response, an unstructured exception string, or a server stack
trace.

## Know the public mappings

| Failure | HTTP | gRPC |
| --- | --- | --- |
| FluentValidation input error | 400 | `InvalidArgument` |
| Business rule violation | 400 | `FailedPrecondition` |
| Policy authorization failure | 403 | `PermissionDenied` |
| Optimistic concurrency conflict | 409 | `Aborted` |
| ETag precondition mismatch | 412 | `FailedPrecondition` |
| Unhandled exception | 500 | `Internal` |

Configure `ArkGrpcErrorOptions.IncludeExceptionDetails` only for trusted
diagnostic environments. Development includes details automatically; production
responses must not expose exception messages or stack traces.

## Design public failures deliberately

Use validation failures for malformed input, authorization failures for denied
access, and business-rule violations for valid requests that the domain cannot
accept. Include a safe error code and client-actionable details; never include
connection strings, credentials, access tokens, stack traces, or unreviewed
exception messages.

Unhandled exceptions become generic server errors. Log their full details on the
server with structured logging and correlate them through the host's normal
telemetry. Add a dedicated mapper only when a domain failure needs a documented,
different public representation.

Architecture rationale: [design.md](../design.md).
