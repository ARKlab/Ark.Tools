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

Create a violation by deriving from `BusinessRuleViolation`. Its public
properties are safe, structured data returned to clients, so make them
client-actionable and never include secrets or exception text:

```csharp
using Ark.Tools.Core.BusinessRuleViolation;

public sealed class GreetingAlreadyExistsViolation : BusinessRuleViolation
{
    public GreetingAlreadyExistsViolation(string name)
        : base("GREETING_ALREADY_EXISTS")
    {
        Name = name;
        Detail = "A greeting with this name already exists.";
    }

    public string Name { get; }
}
```

Throw `BusinessRuleViolationException` from the handler as shown above. The
HTTP problem-details mapper and gRPC interceptor expose the base `Status`,
`Title`, `Detail`, and safe public derived properties. Use FluentValidation for
malformed input; use a violation for valid input that cannot be accepted by a
business rule.

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

## Inspect HTTP error responses

A business-rule failure is an RFC 7807 response. For the violation above:

```http
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json
```

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "GREETING_ALREADY_EXISTS",
  "status": 400,
  "detail": "A greeting with this name already exists.",
  "name": "hello"
}
```

A FluentValidation failure is HTTP 400 and identifies invalid input fields.
For example, a missing name can produce:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

Exact validation text depends on the application's validators. Clients should
branch on stable error codes/statuses rather than parsing human-readable
`detail` text.

## Inspect gRPC error responses

gRPC returns a non-OK status and rich `google.rpc.Status` details. A business
violation maps to `FailedPrecondition` and has an
`ark.mediator.ArkBusinessRuleViolation` detail:

```text
code: FAILED_PRECONDITION
message: "GREETING_ALREADY_EXISTS"
details {
  type_url: "type.googleapis.com/ark.mediator.ArkBusinessRuleViolation"
  value: { type: "GreetingAlreadyExistsViolation", title: "GREETING_ALREADY_EXISTS",
           status: 400, detail: "A greeting with this name already exists.",
           extensions: { "Name": "\"hello\"" } }
}
```

FluentValidation maps to `InvalidArgument` with a `google.rpc.BadRequest`
detail. Each invalid member is a `field_violations` entry:

```text
code: INVALID_ARGUMENT
message: "Validation failed"
details {
  type_url: "type.googleapis.com/google.rpc.BadRequest"
  value: { field_violations: [{ field: "Name", description: "'Name' must not be empty." }] }
}
```

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
