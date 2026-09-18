# Application architecture best practices

The Mediator Framework does not define the internal layering of an application.
The sample uses the following boundaries so handlers remain explicit and
composable.

## Handler

Handlers own the transaction lifecycle. They decide whether a workflow needs a
lock, optimistic concurrency, idempotency checks, or an external call between
the read and the commit. A handler composes fine-grained context operations and
commits or rolls back the complete workflow.

## Context and DAL

Contexts own ORM and persistence operations. Their methods are small and
composable: read, evaluate, write, and commit are separate operations. A
context does not start a hidden transaction per method and does not decide
business policy. SQL and in-memory profiles implement the same context-factory
contract, including the transactional outbox.

## Domain service

Domain services are singletons that contain reusable business rules and
side-effects. Handlers call them with the contexts and clients they need.
Compose a domain service from multiple handlers when a request and a message
perform the same domain action. A domain service is not required when a handler
contains no duplicated business logic.

Business rules belong in domain services. Persistence side-effects belong in
contexts/DALs. Events and messages are published or sent by domain services.
External services are called by domain services through adapters.

## External adapter

An adapter owns calls to systems outside the current service. This includes
third-party services, shared cross-application services, and another service's
endpoint. It does not include persistence owned by the current service: SQL,
the bus, blob storage, and the local outbox remain contexts/DALs.

Every external adapter has a mock or stub implementation and a binding driver.
Scenario-scoped mocks must not outlive their scenario. A singleton application
service may depend on a proxy that resolves the current binding; calling that
proxy outside a scenario fails immediately.

## Explicitly rejected

Do not add a `Store` abstraction. A Store method commonly opens and commits one
transaction, hiding transaction boundaries, lock strategy, idempotency, and
external-call interleaving inside the Store. That makes handlers unable to own
their responsibilities and is explicitly rejected.

## Example

```csharp
public async Task<Response> ExecuteAsync(Request request, CancellationToken ctk)
{
    await using var context = await _contexts.CreateAsync(ctk);
    var entity = await context.ReadAsync(request.Id, ctk)
        ?? throw new EntityNotFoundException();
    var result = await _domain.ApplyAsync(entity, context, _external, ctk);
    await context.CommitAsync(ctk);
    return result;
}
```
