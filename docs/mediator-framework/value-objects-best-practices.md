# Value objects best practices

Avoid primitives for values with a domain meaning, such as identifiers, titles
or ratings: wrap them in value objects. The
[Mediator Framework sample](../../samples/Ark.MediatorFramework.Sample/README.md)
uses [Vogen](https://github.com/SteveDunn/Vogen) for domain values and
Ark.Tools.Compliance sensitive value objects for personal data.

## Vogen value objects

A [Vogen](https://github.com/SteveDunn/Vogen) value object travels as the
primitive it wraps on every transport. Declare it in the contracts project:

```csharp
[ValueObject<Guid>]
public readonly partial struct BookId;

public sealed record GetBookQuery(BookId Id) : IQuery<GetBookQuery, Book>;
```
Source: [`BookId.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/BookId.cs)

Ark.Tools recognizes Vogen types by their `[ValueObject]` attribute and takes
no Vogen dependency. Source generators cannot see each other's output, so
Ark.Tools assumes the members Vogen generates by default: keep `Value`, `From`
and the JSON converter on a contract type. A route or query value needs
`TryParse` or the `TypeConverter`; with neither, the endpoint reports
`ARKMF059`.

- **JSON**: declare the source-generated `JsonSerializerContext` outside the
  contracts assembly, as the sample does in its application project. There it
  sees Vogen's `[JsonConverter]` and needs no converter setup; in the contracts
  assembly it cannot, because source generators cannot see each other's
  output. If a context has to live with the contracts, add
  `ValueObjectJsonConverterFactory` to its
  `[JsonSourceGenerationOptions(Converters = ...)]`; `ConfigureArkDefaults()`
  also registers it. A context needs `[JsonSerializable(typeof(int))]` (or
  `long`, `double`) for numeric value objects, because Vogen's converter reads
  the number through the options.
- **Routes and query strings**: zero setup. Minimal API and Azure Functions
  bind the value through Vogen's `TryParse`, or through its `TypeConverter`
  when `parsableForPrimitives` (`parsableForStrings` for a string) is
  `GenerateNothing`.
- **OpenAPI**: add `AddArkValueObjectSchemas()`; see [OpenAPI](openapi.md).
- **gRPC**: zero setup for a value object over `string`, `Guid`, `bool`,
  `int`, `long`, `float` or `double`. The exported `.proto` declares the
  primitive, and `MapArkGrpcServicesFromAssembly` registers the value object
  with protobuf-net. proto3 never writes `""` or `0`, so an absent field reads
  as the value object over that empty value, as JSON reads an explicit `""`;
  an optional (`?`) member stays `null`. See [gRPC](grpc.md) for `Guid`
  members.
- **Dapper** (`Ark.Tools.Dapper`): call
  `ValueObjectDapper.Register<BookId, Guid>(BookId.From, static id => id.Value)`
  once at startup. Vogen's own Dapper handler would require the contracts
  project to reference Dapper.
- **`ToDataTableArk()`**: zero setup. A value object member becomes a column
  of its primitive type, ready for table-valued parameters and `SqlBulkCopy`.
- **MessagePack**: list the value objects used in a `[MessagePackObject]` on a
  marker class with Vogen's `[MessagePack<T>]` attributes and compose its
  generated `MessagePackFormatters` into the resolver; see
  [`SampleMessagePackFormatters.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.API/SampleMessagePackFormatters.cs).

Value objects carry no validation in the sample: Vogen's `Validate` would throw
while the request is deserialized, before the validation pipeline can report
which field is wrong. Validate the wrapped value with FluentValidation instead,
keeping the property name in the error:

```csharp
RuleFor(static book => book.Title.IsInitialized() ? book.Title.Value : null)
    .NotEmpty()
    .MaximumLength(200)
    .OverridePropertyName(nameof(Book.V1.Create.Title));
```
Source: [`BookValidators.cs`](../../samples/Ark.MediatorFramework.Sample/Core/Ark.MediatorFramework.Sample.Core.Application/Handlers/Validators/BookValidators.cs)

## Personal data

Wrap personal data in an Ark.Tools.Compliance
[sensitive value object](../compliance/sensitive-value-objects.md), such as
`PersonName` or your own `[SensitiveValueObject<string>]`, rather than in a
Vogen one, so that logs and `ToString()` stay redacted. The Mediator Framework
treats it like a value object over `string`:

- **JSON**: zero setup through a context in another assembly; in the same
  assembly, through the same `ValueObjectJsonConverterFactory`.
- **Routes and query strings**: bound through its generated `TypeConverter`.
- **OpenAPI**: add `AddArkComplianceSchemas()` and
  `AddSensitiveValueSchema<T>()` for your own types.
- **gRPC**: zero setup. The exported `.proto` declares a `string`, and
  `MapArkGrpcServicesFromAssembly` registers it with protobuf-net through
  `SensitiveValueSerialization`. Do not also register it with
  `SensitiveValueProtobuf`, whose surrogate writes a nested message instead.
- **Dapper** and **MessagePack**: register it with the
  `Ark.Tools.Compliance.Dapper` and `Ark.Tools.Compliance.MessagePack` adapters.

## Native AOT

In a Native AOT app, `ConfigureArkDefaults()` is unavailable, so add
`ValueObjectJsonConverterFactory` (or Vogen's generated `VogenTypesFactory`) to
the serializer options yourself. `ToDataTableArk()` handles value objects without
reflection only when the element type is known at the call site; the reflection
fallback, used from generic code, throws `NotSupportedException` because Native
AOT removes the `Value` property metadata. Vogen's JSON converter for a numeric
primitive calls the reflection-based `JsonSerializer` and is reported by Native
AOT; `Customizations.TreatNumberAsStringInSystemTextJson` avoids it, at the cost of
writing the number as a JSON string.
