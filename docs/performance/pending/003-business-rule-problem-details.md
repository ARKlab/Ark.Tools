# Cache business-rule problem-details accessors

## Scope

`ExceptionProblemDetailsMapper.CreateBusinessRuleViolation` reflects all
derived violation properties and calls `PropertyInfo.GetValue` for each mapped
exception. The mediator sample registers and uses this mapper for request-time
RFC 7807 responses.

## Outcome

Avoid repeated property discovery and reflective getters while preserving the
current problem-details wire shape for every business-rule violation type.

## Implementation guidelines

1. Add exception-mapping benchmarks to
   `/home/runner/work/Ark.Tools/Ark.Tools/benchmarks/Ark.Tools.Benchmarks/`
   using representative violations with zero, one, and several derived
   properties.
2. First implement a thread-safe runtime metadata cache keyed by concrete
   violation `Type`. Cache property names and compiled or created getter
   delegates; exclude base `BusinessRuleViolation` properties as today.
3. Preserve `type`, `title`, and `status` extension entries, status code,
   title, detail, and duplicate-key behavior.
4. Keep metadata immutable after publication and do not cache the violation
   instance or resulting dictionary.
5. Evaluate a source-generated accessor registry as an opt-in alternative for
   trim/AOT-sensitive applications. It must fall back to the runtime cache for
   unregistered violation types.
6. Add tests comparing the complete `ProblemDetails` extension payload between
   baseline behavior and cached/generated behavior.

## Acceptance criteria

- Reflection-based property discovery occurs once per concrete violation type.
- The error response JSON and extension keys are backward compatible.
- Concurrent exceptions of a newly seen type are safe and produce complete
  payloads.
- BenchmarkDotNet shows reduced allocations and mean mapping time for repeated
  violations of the same type.

## Verification

1. Run focused ProblemDetails and mediator sample tests plus the full solution
   build/test commands.
2. Run Release BenchmarkDotNet exception-mapping benchmarks and retain the
   baseline/candidate artifacts.
3. Validate serialized RFC 7807 responses for at least two derived violation
   types and one violation with no derived properties.

