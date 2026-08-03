# Generate DataTable row writers

## Scope

`SqlStateProvider` reaches `ToDataTableArk()` when saving state and when loading
2,000 or more resource IDs. It already caches fields and properties per generic
type, but each row still uses `FieldInfo.GetValue` and `PropertyInfo.GetValue`.
The current call sites use anonymous row types.

## Outcome

Eliminate per-row reflective member access for ResourceWatcher SQL
table-valued-parameter payloads without changing SQL schemas, NodaTime
conversions, or public DataTable extension behavior.

## Implementation guidelines

1. Create DataTable benchmarks in
   `/home/runner/work/Ark.Tools/Ark.Tools/benchmarks/Ark.Tools.Benchmarks/`
   for the resource-ID and state payload shapes at realistic sizes, including
   2,000 IDs and representative state batches.
2. Replace anonymous SQL row shapes in `SqlStateProvider` with named,
   internal row types whose schema is explicit and testable.
3. Prefer a source generator that emits a typed DataTable schema initializer
   and row-value writer for annotated/named row types. Generated code must
   perform the existing NodaTime and enum conversions.
4. Keep `ToDataTableArk()` as a reflection-based generic fallback for public
   compatibility and unannotated consumers. Do not claim it has no metadata
   cache; its remaining issue is per-row getter invocation.
5. Avoid generated `DataTable` schema changes: preserve column names, order,
   CLR column types, null handling, and SQL user-defined table type names.
6. Add parity tests that compare schemas and rows from the generated writer and
   existing extension for nulls, NodaTime values, enums, and empty batches.

## Acceptance criteria

- ResourceWatcher's covered SQL paths perform no reflective field/property
  getter invocation per row.
- Existing SQL table-valued parameters remain accepted by their user-defined
  table types.
- The public `ToDataTableArk()` API remains unchanged and retains its fallback.
- BenchmarkDotNet shows lower mean time and allocated bytes for both covered
  payloads, with the 2,000-ID case reported explicitly.

## Verification

1. Run ResourceWatcher focused tests, including SQL integration tests where
   Docker dependencies are available, then run the full solution build/test
   commands.
2. Execute Release BenchmarkDotNet DataTable benchmarks with identical input
   data for baseline and candidate; retain exported result artifacts.
3. Compare generated and fallback DataTable column metadata and row values
   before exercising the SQL TVP integration path.

