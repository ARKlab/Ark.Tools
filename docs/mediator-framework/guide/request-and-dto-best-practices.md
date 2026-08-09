# Request and DTO best practices

Keep the application model separate from the operation that carries it. Use a
static class as the namespace for each model and version its transport-neutral
types under that namespace:

```csharp
public static class Book
{
    public static class V1
    {
        public record Input
        {
            public required string Title { get; init; }
            public required string Author { get; init; }
        }

        public record Create : Input;

        public record Update : Input
        {
            [ServerSet]
            public Guid Id { get; init; }
        }

        public record Output : Input
        {
            public Guid Id { get; init; }
            public required string Description { get; init; }
        }
    }
}
```

Requests, queries, and commands get their own static namespace. Their `V1`
payload composes the model rather than inheriting from it:

```csharp
public static class Book_CreateRequest
{
    [HttpEndpoint("POST", "/books")]
    public record V1([property: HttpBody] Book.V1.Create Data)
        : IRequest<V1, Book.V1.Output>;
}

public static class Book_UpdateRequest
{
    [HttpEndpoint("PUT", "/books/{id}")]
    public record V1(
        [property: HttpBody] Book.V1.Input Data,
        [property: HttpRoute] Guid Id)
        : IRequest<V1, Book.V1.Output>;
}
```

This shape gives tests a stable current model while drivers compose it into the
operation sent to the application. It also makes the distinction explicit:

- `Input`, `Create`, and `Update` describe the model accepted by the domain.
- `Output` describes the model returned by the domain.
- `Request`, `Query`, and `Command` describe an operation and its transport
  bindings.
- Route, query, and server-owned values belong to the operation envelope, not
  to a reusable model.

Use inheritance only for compatible model evolution. Use composition for
operation payloads, especially when a request combines a body with route or
query values. Do not duplicate model fields in every operation contract.

The generators treat `[HttpBody]` as the body member of a composed request.
The body type is deserialized as the payload and the generated endpoint builds
the outer request with the bound route and query members. The same outer
contract remains available to direct application tests and other transports.
