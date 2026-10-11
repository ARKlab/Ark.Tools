# Value object libraries: Vogen vs Meziantou.Framework.StronglyTypedId

Research record for the Vogen adoption in the Mediator Framework sample
(October 2026). It compares
[Vogen](https://github.com/SteveDunn/Vogen) 8.0.7 with
[Meziantou.Framework.StronglyTypedId](https://github.com/meziantou/Meziantou.Framework)
3.0.7 on the points where Vogen needed Ark.Tools work. Findings marked
*verified* come from a throwaway two-project build (contracts and app, .NET SDK
10, Native AOT publish on linux-x64); the rest come from the libraries' source.

## Decision

Keep Vogen as the one library Ark.Tools supports first-class; do not add
Meziantou support without consumer demand.

- Meziantou's System.Text.Json converter is a private nested class: a
  `JsonSerializerContext` in another assembly reports SYSLIB1220, generates no
  metadata and throws `NotSupportedException` (*verified*). Ark's pattern is
  contracts in one assembly, contexts in another, which works with Vogen.
- Meziantou's marker attribute is `[Conditional("StronglyTypedId_Attributes")]`,
  so it is absent from compiled metadata, and Ark's attribute-name detection
  cannot see Meziantou types from a referenced assembly.
- Meziantou has no Dapper, MessagePack, OpenAPI, EF Core or validation support.

## What each library generates

| | Vogen 8.0.7 | Meziantou 3.0.7 |
| --- | --- | --- |
| Marker | `[ValueObject<T>]`, kept in metadata | `[StronglyTypedId<T>]`, `[Conditional]` |
| Scope | any value object; validation and normalization hooks | identifiers first; any supported primitive (no `DateOnly`/`TimeOnly`) |
| Conversions | `Conversions` flags: TypeConverter and System.Text.Json by default; Newtonsoft, EF Core, Dapper, MessagePack, BSON, Orleans and more opt-in | System.Text.Json, TypeConverter by default; Newtonsoft and MongoDB when referenced |
| Factory | `From(T)`, `TryFrom` | `From{Int32,String,Guid,...}`, `Parse`/`TryParse` |
| Analyzers | 37 rules, including no `default`/`new` | 4 rules |
| `ToString()` | the value | record format (`Title { Value = Dune }`) unless `GenerateToStringAsRecord = false` |
| License, downloads | Apache-2.0, about 7.4M | MIT, about 196K |

## Transport by transport

### System.Text.Json

- **Same assembly**: both fail the same way. A source-generated context cannot
  see a converter another generator produced, and writes `{}` (*verified* for
  Meziantou). Ark's `ValueObjectJsonConverterFactory` fixes it for Vogen.
- **Another assembly**: Vogen works with no setup (verified in the sample's
  Application contexts). Meziantou fails unless each type declares
  `public partial class XJsonConverter;` (*verified*).
- **Numbers**: Vogen reads through `options.GetTypeInfo(typeof(int))`, so a
  context needs `[JsonSerializable(typeof(int))]`, and writes through the
  reflection-based `JsonSerializer`, which Native AOT reports. Meziantou uses
  `Utf8JsonWriter` directly: no extra metadata, no AOT warning (*verified*),
  but it ignores `JsonNumberHandling`.

### Route and query binding

Vogen generates `TryParse(string, IFormatProvider?, out T)` and a
`TypeConverter`, each of which can be turned off; Ark reads those options
(`ValueObjectSymbols.GeneratesTryParse`/`GeneratesTypeConverter`) and reports
`ARKMF059` when neither exists. Meziantou always generates `TryParse` and
explicit `IParsable`/`ISpanParsable`; Ark's generic `IParsable` path would bind a
referenced type without a value-object special case.

### gRPC (protobuf-net)

Neither library targets protobuf-net. Ark registers either kind through
`ArkProtobufValueObjects.Register`, which writes the wrapped scalar. proto3
never writes `""` or `0`, so an absent field left a Vogen struct uninitialized,
and its `Value` throws `ValueObjectValidationException`.
`ArkProtobufValueObjects.InitializeAbsentValueObjects`, called by the generated
`MapArkGrpcServicesFromAssembly`, now sets those members to the value object
over the empty value, as JSON reads an explicit `""`.

Meziantou's `Value` never throws: `default` reads `0` or `Guid.Empty`, but
**`null`** for a string, so Ark would still need the absent-member handling for
strings and a null-safe write.

### FluentValidation 12

FluentValidation 12 removed `Transform`. With Vogen, `RuleFor(x => (string)x.Title)`
names the rule `Title` (FluentValidation strips the cast; *verified*, also
`Data.Title` when nested) but throws on an uninitialized value object, which
still reaches the validator for a member that is not `required` when JSON omits
it or code does not set it.

Defining `VOGEN_NO_VALIDATION` in the contracts project makes `IsInitialized()`
always true and `Value` return the primitive's default instead of throwing,
while `Validate` still runs in `From` (*verified*): the cast form then reports
`'Title' must not be empty.` for an unset value. The cost is losing Vogen's
detection of uninitialized use everywhere else. The sample defines it and uses
the cast form.

Meziantou has no conversion operators: `RuleFor(x => x.Title.Value)` is safe but
named `Title.Value`, so it still needs `OverridePropertyName`.

### Other integrations

| Aspect | Vogen | Meziantou | Ark.Tools |
| --- | --- | --- | --- |
| Validation | `Validate`, `NormalizeInput` in `From`, JSON and parsing | hand-written constructor only; `default` bypasses it | sample validates with FluentValidation (#1089) |
| Dapper | optional handler, forces a Dapper reference on contracts | none | `ValueObjectDapper.Register` works for both |
| MessagePack | `[MessagePack<T>]` marker formatters | none | a generic formatter would be needed for Meziantou |
| OpenAPI | `MapVogenTypesIn...`, `OpenApiMarker<T>` | none | `AddArkValueObjectSchemas` detects Vogen |
| EF Core | converter and markers | none | — |
| Native AOT | numeric JSON converter reported | clean (*verified*) | — |

## Supporting Meziantou as well

It would touch about eight places in three packages:

1. `ValueObjectSymbols`: describe the library, primitive, factory name and
   parse/converter availability; detect Meziantou in the same compilation by
   attribute and in references by `IStronglyTypedId<T>`.
2. `GrpcEndpointGenerator`: use `From{Type}` names.
3. `HttpStringBinding`: treat same-compilation Meziantou types as `TryParse`.
4. Runtime detectors (`ValueObjectJsonConverterFactory`, `ArkOpenApiEx`,
   `ToDataTableArk`): add the interface check.
5. `ToDataTableArk` interceptor generator and `ComplianceSymbolFacts`.
6. `ArkProtobufValueObjects`: a null string primitive on write.
7. An analyzer requiring the public converter declaration on contract types.
8. A MessagePack formatter, documentation and tests.

Not checked: Ark.Tools built against Meziantou, and protobuf-net with Meziantou
types.
