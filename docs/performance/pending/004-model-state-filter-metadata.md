# Cache model-state filter skip metadata

## Scope

`ModelStateValidationFilterAttribute.OnActionExecuting` calls
`MethodInfo.GetCustomAttributes` on every MVC action invocation to check
`SkipModelStateValidationFilterAttribute`. The filter is globally installed by
`ArkStartupWebApiCommon`, making it reachable from WebApplicationDemo and
Ark.Reference.

## Outcome

Remove repeated action-method attribute scans while retaining skip behavior and
invalid-model responses.

## Implementation guidelines

1. Add MVC filter microbenchmarks in
   `/home/runner/work/Ark.Tools/Ark.Tools/benchmarks/Ark.Tools.Benchmarks/`
   using controller action descriptors with and without the skip attribute.
2. Cache the Boolean decision by `MethodInfo` in a thread-safe process-wide
   cache, or use an equivalent static metadata path supplied by MVC.
3. Do not cache `ActionExecutingContext`, `ModelStateDictionary`, action
   results, or controller instances.
4. Preserve the current inherited-attribute lookup and only bypass validation
   when the marker is present.
5. Prefer this cache over a generator: the optimization is small, metadata is
   framework-owned, and dynamically discovered controllers must continue to
   work.
6. Add tests for marked and unmarked controller actions and invalid/valid model
   state outcomes.

## Acceptance criteria

- Each action method's skip decision is reflected at most once per process.
- Marked actions still bypass validation; unmarked invalid models still return
  `BadRequestObjectResult`.
- The cache is safe under concurrent requests and supports distinct actions.
- BenchmarkDotNet demonstrates lower mean filter invocation time and allocations
  for both marked and unmarked action cases.

## Verification

1. Run focused ASP.NET Core filter tests and the full solution build/test
   commands.
2. Execute Release BenchmarkDotNet filter benchmarks on the same SDK and host
   for baseline and candidate.
3. Inspect benchmark output and tests to confirm the cache does not retain
   request-scoped objects.

