# Cache Minimal API type converters

## Scope

`ArkTypeConverterValue<T>.TryParse` calls
`TypeDescriptor.GetConverter(typeof(T))` on every route or query value
conversion. The converter and its string-conversion capability are candidates
for caching once per closed `T`.

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

## Verification

1. Run focused Minimal API binding tests and the full solution build/test
   commands.
2. Execute Release BenchmarkDotNet conversion benchmarks on the same SDK and
   host for baseline and candidate.
3. Validate a registered converter under both an explicit provider and the
   invariant-culture fallback.
