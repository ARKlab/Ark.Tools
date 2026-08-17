# Cache Minimal API type converters

## Scope

`ArkTypeConverterValue<T>.TryParse` calls
`TypeDescriptor.GetConverter(typeof(T))` on every route or query value
conversion. The converter and its string-conversion capability are candidates
for caching once per closed `T`.

## Outcome

Each closed `ArkTypeConverterValue<T>` type now initializes a static generic
holder containing its `TypeConverter` and `CanConvertFrom(typeof(string))`
result. Conversion still supplies the request's culture when it is a
`CultureInfo`, and otherwise uses `CultureInfo.InvariantCulture`.

## Implementation guidelines

1. Add a focused benchmark comparing repeated conversion before and after the
   cache for representative registered converters.
2. Cache the converter and `CanConvertFrom(typeof(string))` result in a static
   generic holder or equivalent per-`T` metadata path.
3. Preserve the supplied `IFormatProvider`, the invariant-culture fallback,
   conversion exception handling, and the existing `TryParse` result behavior.
4. Confirm converter lifetime and `TypeDescriptor` provider behavior before
   retaining a converter instance.
5. Preserve the existing trimming suppressions and explicit host registration
   requirements.
6. Add tests for supported conversion, unsupported string conversion, null
   input, provider use, and conversion failures.

## Acceptance criteria

- `TypeDescriptor.GetConverter` is not called for every value conversion of the
  same closed `T`.
- Supported and unsupported converters produce the existing results.
- Culture-sensitive converters still receive the request's format provider.
- Concurrent first use is safe and does not cache request-scoped values.
- BenchmarkDotNet demonstrates reduced repeated conversion overhead.

## Decisions

- Retained the converter instance and capability result for the lifetime of the
  closed generic type. `TypeDescriptor.GetConverter` returns reusable converter
  instances, while conversion input and culture remain per call.
- Kept the existing trimming suppressions and startup registration requirement.
  Applications must register custom converters before the first conversion for a
  closed type; this matches the existing TypeDescriptor registration contract.
- Added tests for supported values, null input, unsupported conversion,
  explicit culture propagation, conversion failures, and concurrent first use.
- Added an in-process .NET 10 BenchmarkDotNet comparison. The observed mean was
  63.62 ns for the per-call TypeDescriptor lookup and 15.36 ns for the cached
  path. Both paths allocated 48 B for the returned wrapper; the cached path
  reduced mean time by 75.9%.

## Verification

1. `dotnet test tests/Ark.Tools.MediatorFramework.Tests/Ark.Tools.MediatorFramework.Tests.csproj --no-restore`
   passed with 112 tests.
2. `dotnet build benchmarks/Ark.Tools.Benchmarks/Ark.Tools.Benchmarks.csproj
   --configuration Release --no-restore` passed.
3. The Release in-process .NET 10 converter benchmark completed on the same
   SDK and host used for the recorded results.
