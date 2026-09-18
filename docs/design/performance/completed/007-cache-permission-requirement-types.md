# Cache permission authorization requirement types

## Scope

`PermissionAuthorizationHandler<TPermissionEnum>.HandleAsync` previously
constructed the resource-specific closed
`PermissionAuthorizationRequirement<TPermissionEnum, TResource>` type with
`MakeGenericType` for every authorization check.

## Outcome

Resource-specific requirement types are now cached in a thread-safe
`ConcurrentDictionary<Type, Lazy<Type>>` nested in each closed
`PermissionAuthorizationHandler<TPermissionEnum>` type. The cache stores only
closed requirement types keyed by runtime resource type; authorization
contexts, policies, requirements, resources, permissions, and providers remain
per-check or per-handler as before.

Null resources match only the non-resource requirement type. Resource-specific
requirements remain distinct by runtime type, unrelated requirements are
filtered out, provider invocation is skipped when no permission requirement is
present, and successful or failed permission evaluation semantics are
unchanged.

## Decisions and caveats

- `Lazy<Type>` uses `ExecutionAndPublication` so concurrent first use publishes
  one closed type per runtime resource type.
- The non-resource path uses exact-type matching to prevent a resource-specific
  derived requirement from being selected when the resource is null.
- Added focused tests for null resources, multiple resource types, concurrent
  first use, unrelated requirements, provider invocation, and permission
  success/failure.
- Added a Release BenchmarkDotNet baseline that constructs the closed generic
  type for every check and a cached candidate covering two resource types.

## Verification

1. Focused permission tests passed: 5 tests.
2. Release benchmark passed on .NET SDK 10.0.400 / .NET 10.0.11:
   uncached 3.380 us and 6.49 KB versus cached 2.066 us and 5.87 KB per
   operation (0.61 ratio, 10% fewer allocations).
3. The benchmark uses five warmups and fifteen iterations for both methods.
4. Solution restore completed successfully; focused project test and benchmark
   builds completed without warnings or errors.
