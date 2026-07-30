# Errors

Throw domain exceptions from handlers. The host maps validation and domain
failures to RFC 7807 ProblemDetails on HTTP and `Google.Rpc.Status` on gRPC.
Standard 400, 403, and 500 responses are described on every generated endpoint;
authentication failures are not exposed as domain details.

```csharp
if ((await _store.AllAsync(ctk).ConfigureAwait(false)).Any())
    throw new BusinessRuleViolationException(new GreetingAlreadyExistsViolation(Request.Name));
```

Source: [`GreetingHandlers.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/GreetingHandlers.cs).

Register `AddArkProblemDetailsExceptionHandler()` and put
`UseArkProblemDetailsExceptionHandler()` outermost in the pipeline. Do not leak
stack traces, connection strings, tokens, or internal exception messages.
Implement a handwritten exception mapper only for a domain error with a
deliberate different representation. Rationale: [`design.md`](../design.md).
