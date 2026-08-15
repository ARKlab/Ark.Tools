# Cache policy authorization metadata

## Scope

The three `PolicyAuthorizeOrLogic*Decorator` classes in Ark.Reference scan
`PolicyAuthorizeAttribute` values in each decorator constructor. Handlers are
registered transiently, so sample HTTP requests repeat that reflection.

## Outcome

Authorization metadata is now resolved once per closed decorator contract type
using a nested static generic holder. The cached value is only the inherited,
ordered `PolicyAuthorizeAttribute[]`; policy-provider lookup, resource
construction, authorization, and failure accumulation remain per request.

## Decisions and caveats

- Kept `GetCustomAttributes(typeof(PolicyAuthorizeAttribute), true)` unchanged
  to preserve inheritance and ordering.
- Kept the existing OR behavior, failure messages, and transient decorator
  lifetime unchanged.
- Added focused tests for pass-through, first-policy success, all-policy
  failure, inherited metadata, and concurrent first use.
- Added BenchmarkDotNet coverage for request, query, and command decorators with
  zero, one, and multiple policies. The benchmark measures construction; the
  existing integration tests continue to cover request execution.
- Generated metadata was not introduced because the bounded generic cache is
  sufficient and does not require application changes.

## Verification

1. Run focused authorization decorator tests and the full solution build/test
   commands from `AGENTS.md`.
2. Run Release BenchmarkDotNet benchmarks with equal warmup/iteration settings
   for baseline and candidate on the same machine.
3. Review generated benchmark artifacts and confirm the optimized path has no
   repeated `GetCustomAttributes` call.
