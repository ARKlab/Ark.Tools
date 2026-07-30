# Escape hatches

Generated wiring is the default for ordinary request/response operations. Keep
the application contract and handler transport-neutral, then add a hand-written
adapter only for behavior the declarative surface cannot express.

| Requirement | Use |
| --- | --- |
| Custom HTTP parsing or response | Hand-written Minimal API mapping |
| One-file multipart conversion | `MapArkAttachmentUpload` |
| Existing or custom gRPC protocol | Hand-written service or generated partial |
| Legacy message routing | Hand-written `IHandleMessages<T>` |
| Existing controllers | MVC coexistence during migration |

## Preserve the handler boundary

```csharp
app.MapPost("/legacy/greeting", async (
    LegacyGreetingDto body,
    IRequestHandler<CreateGreetingRequest, GreetingResponse> handler,
    CancellationToken cancellationToken) =>
{
    var response = await handler.ExecuteAsync(
        new CreateGreetingRequest { Name = body.Text },
        cancellationToken).ConfigureAwait(false);
    return Results.Ok(response);
});
```

**Outcome:** the legacy endpoint owns its unusual binding and response, while
the application operation retains its normal validation, authorization, and
business implementation.

## Decide before escaping

First check whether a route template, query binding, status property, attachment
option, version attribute, or serializer configuration already expresses the
need. Use the smallest adapter when it does not. Document its public behavior,
apply the same authorization and error mapping as generated endpoints, and add
boundary tests because the generator no longer protects that surface.

For incremental controller migration, see
[migration from MVC](../migration-from-mvc.md). Architecture rationale:
[design.md](../design.md).
