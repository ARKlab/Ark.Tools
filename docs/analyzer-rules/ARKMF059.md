# ARKMF059: Property cannot be bound from the request

- **Severity:** Error
- **Component:** Mediator Framework
- **Diagnostic messages:**
  - `HTTP endpoint '{0}' uses verb '{1}', which has no request body, but property '{2}' is not bound from the route or query string; mark it [HttpRoute] or [HttpQuery] with a type that converts from a string, or [ServerSet]`
  - `HTTP endpoint '{0}' binds property '{1}' from the route or query string, but its type '{2}' cannot be converted from a string`

## What it checks

The rule reports a contract property that the generated endpoint cannot bind from
the request. The second message is used for a route or `[HttpQuery]` property,
the first for any other property.

A `GET` or `DELETE` endpoint, and a `HEAD` Azure Functions function, never
reads the request body, including when `AcceptsMessagePack = true` (MessagePack
then applies to the response only). Every property of the contract must therefore be bound from the route
or the query string. Properties marked `[ServerSet]` are filled by the server,
are never bound from the request, and are not checked.

### Minimal API generator

The rule checks `GET` and `DELETE` endpoints. The Minimal API generator does
not map `HEAD` and reports it as an unsupported verb (`ARKMF010`). The rule
reports a property when:

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
  property, route properties included, from the route or query string only
  when its type is:
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
  inferred body parameters` at startup.

  A contract with `[ServerSet]` properties is not bound with `[AsParameters]`,
  which would expose them to ASP.NET Core: its other properties are bound
  explicitly from the route or query string, and the rules above still apply
  to them.

For every verb, the endpoint binds a route or `[HttpQuery]` property
explicitly, unless it binds the contract with `[AsParameters]` as described
above. An explicit route or query value is read as follows:

- a type from the list above is bound by ASP.NET Core;
- a string collection, such as `string[]`, `List<string>`, `IEnumerable<string>`
  or `IReadOnlyList<string>`, receives every value of the query parameter;
- any other single value is converted with its `TypeConverter` at runtime, so a
  converter registered with `TypeDescriptor`, such as the Ark.Tools NodaTime
  converters, is used.

The rule reports the property when its type is:

- an array of a type without `TryParse`, which ASP.NET Core rejects at startup
  (`must have a valid TryParse method to support converting from a string`);
- a collection, such as `List<int>`, `HashSet<Guid>` or a dictionary, without a
  `[TypeConverter]` attribute, or a complex object. No `TypeConverter` converts a
  string to them, so every request that carries the value fails with `400`.

A single value whose type has neither `TryParse` nor a `TypeConverter` registered
at runtime also fails every request that carries it. The generator cannot see
runtime registrations, so it does not report this case.

The diagnostic is reported at the property. When the contract is in a
referenced assembly, it is reported at every `MapArkEndpoints` or
`MapArkEndpointsFromAssembly` call that discovers it, so a `#pragma` around one
call does not hide it at the others.

### Azure Functions generator

A generated `GET`, `HEAD` or `DELETE` function binds only route properties and
`[HttpQuery]` properties, for requests, queries and commands alike. The rule
reports every other settable property, other than an `[ETag]` property, which
would otherwise be silently ignored.

For every verb, a route or `[HttpQuery]` value is converted from a single
string with `ArkTypeConverter` at runtime, and an `[HttpQuery]` string
collection, such as `string[]`, `List<string>` or `IEnumerable<string>`,
receives every value of the query parameter. The rule reports a route or
`[HttpQuery]` property whose type is any other array or collection, or a
complex object: no type converter converts a single string to it. Unlike the
Minimal API generator, the Azure Functions generator does not bind arrays such
as `int[]`.

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

A route or query type that cannot be converted from a string:

```csharp
[HttpEndpoint("POST", "/api/v{version}/books/{bookId}/copies")]
public sealed record AddCopies : IRequest<AddCopies, int>
{
    public Guid BookId { get; init; }

    [HttpQuery]
    public List<int> Shelves { get; init; } = []; // ARKMF059: use int[]
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

Do not suppress this rule: the endpoint would fail at startup, fail every
request that carries the property, or silently ignore it.

## Source

This rule is implemented in Ark.Tools and its descriptor links here: `https://github.com/ARKlab/Ark.Tools/blob/master/docs/analyzer-rules/ARKMF059.md`.
