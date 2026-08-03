# Cache policy authorization metadata

## Scope

The three `PolicyAuthorizeOrLogic*Decorator` classes in Ark.Reference scan
`PolicyAuthorizeAttribute` values in each decorator constructor. Handlers are
registered transiently, so sample HTTP requests repeat that reflection.

## Outcome

Resolve authorization attributes once per closed contract type without changing
OR-policy behavior, policy evaluation, failure messages, or decorator lifetime.

## Implementation guidelines

1. Add BenchmarkDotNet benchmarks in
   `/home/runner/work/Ark.Tools/Ark.Tools/benchmarks/Ark.Tools.Benchmarks/`
   for request, query, and command decorators with zero, one, and multiple
   policy attributes.
2. Use a nested static generic metadata holder per `TRequest`, `TQuery`, and
   `TCommand`, or an equivalent bounded runtime type cache. Cache the immutable
   attribute array only.
3. Preserve attribute inheritance and ordering exactly by retaining the current
   `GetCustomAttributes(..., true)` semantics.
4. Keep policy-provider lookup, resource construction, authorization calls, and
   per-request failure accumulation uncached.
5. Consider generated metadata only after the generic static cache is measured;
   do not require applications to adopt a generator for this optimization.
6. Add tests for no-policy pass-through, first-policy success, all-policy
   failure, inherited attributes, and concurrent first use.

## Acceptance criteria

- Attribute reflection occurs at most once for each closed decorator contract
  type per process.
- No policy instance, authorization result, ClaimsPrincipal, resource, or
  scoped service is cached.
- OR semantics and existing failure output remain unchanged.
- BenchmarkDotNet reports lower allocations and lower mean construction plus
  execution cost for one- and multi-policy cases against baseline.

## Verification

1. Run focused authorization decorator tests and the full solution build/test
   commands from `AGENTS.md`.
2. Run Release BenchmarkDotNet benchmarks with equal warmup/iteration settings
   for baseline and candidate on the same machine.
3. Review generated benchmark artifacts and confirm the optimized path has no
   repeated `GetCustomAttributes` call.

