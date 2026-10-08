# ARKMF059: Property cannot be bound without a request body

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic message:** `HTTP endpoint '{0}' uses verb '{1}', which has no request body, but property '{2}' is not bound from the route or query string; mark it [HttpRoute] or [HttpQuery] with a type that converts from a string, or [ServerSet]`

## What it checks

A `GET`, `HEAD` or `DELETE` endpoint never reads the request body, including
when `AcceptsMessagePack = true` (MessagePack then applies to the response
only). Every property of the contract must therefore be bound from the route
or the query string. Properties marked `[ServerSet]` are filled by the server
and are not checked.

### Minimal API generator

The rule reports a property when:

- it is marked `[HttpBody]`;
- it is an `IArkAttachment` or a collection of attachments;
- its type is a complex object, or a collection of complex objects. A complex
  object has public settable or `init` properties and neither a static
  `TryParse(string, out T)` method nor a `[TypeConverter]` attribute;
- it would be silently dropped. When a request or query has at least one
  route or `[HttpQuery]` property, the endpoint binds only those properties
  (an explicit route or `[HttpQuery]` property is read through its
  `TypeConverter`), so any other settable property is never set. An `[ETag]`
  property is read from the `If-Match` header and is accepted;
- ASP.NET Core would infer it as a body. When a request or query has no route
  or `[HttpQuery]` property, and always for commands, the endpoint binds the
  contract with `[AsParameters]`. ASP.NET Core Minimal API then binds a
  property from the route or query string only when its type is:
  - a primitive, `string`, an enum, `Guid`, `DateTime`, `DateTimeOffset`,
    `TimeSpan`, `DateOnly`, `TimeOnly` or `Uri`, or `Nullable<T>` of one of
    these;
  - a type with a public static `TryParse(string, out T)` or
    `TryParse(string, IFormatProvider, out T)`, or that implements
    `IParsable<T>`;
  - an array `T[]` of the above, or `StringValues`.

  Any other type, including `List<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`,
  dictionaries, structs without `TryParse` and NodaTime types such as
  `LocalDate` and `Instant`, is inferred as a body and the endpoint throws
  `InvalidOperationException: Body was inferred but the method does not allow
  inferred body parameters` at startup. A route-bound property is accepted.

The diagnostic is reported at the property. When the contract is in a
referenced assembly, it is reported at the `MapArkEndpoints` or
`MapArkEndpointsFromAssembly` call that discovers it.

### Azure Functions generator

A generated `GET`, `HEAD` or `DELETE` function binds only route properties and
`[HttpQuery]` properties, for requests, queries and commands alike. The rule
reports every other settable property, other than an `[ETag]` property, which
would otherwise be silently ignored. Route and `[HttpQuery]` values are
converted with `ArkTypeConverter` at runtime.

The diagnostic is reported at the property. When the contract is in a
referenced assembly, it is reported at the `[assembly: HttpHost]` attribute
that selects it.

## How to fix it

Mark every input property `[HttpRoute]` or `[HttpQuery]`, or `[ServerSet]`
when the server fills it. Use an array instead of a list, flatten a complex
value into scalar query properties, use `POST` for a request that needs a body,
or give the type a static `TryParse` method so it can be read from a single
string.

### Incorrect

```csharp
public sealed class BookFilter
{
    public string? Author { get; set; }
}

[HttpEndpoint("GET", "/api/v{version}/books")]
public sealed record ListBooks : IQuery<ListBooks, IReadOnlyList<Book>>
{
    public BookFilter? Filter { get; init; }
}
```

### Correct

```csharp
[HttpEndpoint("GET", "/api/v{version}/books")]
public sealed record ListBooks : IQuery<ListBooks, IReadOnlyList<Book>>
{
    [HttpQuery]
    public string? Author { get; init; }
}
```

A dropped property:

```csharp
[HttpEndpoint("GET", "/api/v{version}/books/{bookId}/reviews")]
public sealed record ListReviews : IQuery<ListReviews, IReadOnlyList<Review>>
{
    public Guid BookId { get; init; }

    [HttpQuery]
    public int Skip { get; init; }

    public int Limit { get; init; } // ARKMF059: add [HttpQuery]
}
```

A type ASP.NET Core infers as a body:

```csharp
[HttpEndpoint("GET", "/api/v{version}/books")]
public sealed record ListBooks : IQuery<ListBooks, IReadOnlyList<Book>>
{
    public IReadOnlyList<string> Tags { get; init; } = []; // ARKMF059: use string[]

    public LocalDate Since { get; init; } // ARKMF059: mark it [HttpQuery]
}
```

## Suppression and configuration

Do not suppress this rule: the endpoint would fail at startup or silently
ignore the property.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF059.md`.
