# Cache permission authorization requirement types

## Scope

`PermissionAuthorizationHandler<TPermissionEnum>.HandleAsync` constructs the
resource-specific closed
`PermissionAuthorizationRequirement<TPermissionEnum, TResource>` type with
`MakeGenericType` for every authorization check. Cache the closed requirement
type by runtime resource type while retaining the existing non-resource
requirement path.

## Implementation guidelines

1. Add a focused benchmark comparing repeated authorization checks with and
   without the closed-generic-type cache across resource types.
2. Use a thread-safe cache keyed by the runtime resource `Type`, scoped to the
   closed `PermissionAuthorizationHandler<TPermissionEnum>` type.
3. Keep the non-null resource behavior, null-resource behavior, assignability
   filtering, provider calls, and requirement success semantics unchanged.
4. Do not cache the authorization context, policy, resource instance,
   requirements, permissions, or provider.
5. Add tests for null resources, one or more resource types, concurrent first
   use, unrelated requirements, and permission success/failure.

## Acceptance criteria

- `MakeGenericType` is not called for every authorization check of the same
  permission and resource type.
- Requirements for different runtime resource types remain distinct and are
  matched correctly.
- Null resources continue to match only the non-resource requirement type.
- Authorization results and provider invocation behavior remain backward
  compatible.
- BenchmarkDotNet demonstrates reduced repeated authorization overhead.

## Verification

1. Run focused authorization handler tests and the full solution build/test
   commands.
2. Execute Release BenchmarkDotNet authorization benchmarks on the same SDK and
   host for baseline and candidate.
3. Validate concurrent first use with multiple runtime resource types.
