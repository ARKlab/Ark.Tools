# Errors

Handlers report expected domain failures with domain exceptions. The host
converts them into the appropriate public error representation: RFC 7807
Problem Details for HTTP and `Google.Rpc.Status` for gRPC.

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
trace. Generated OpenAPI describes standard 400, 403, and 500 responses.

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
