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

## Decisions

- Added a `ConcurrentDictionary<Type, Accessor[]>` runtime cache. Each entry is
  immutable after publication and contains expression-compiled getters for the
  derived public properties.
- Kept the existing payload construction order and overwrite behavior for
  `type`, `title`, and `status`; the exception and resulting dictionary are not
  cached.
- Added compatibility tests for zero, one, and several derived properties, plus
  concurrent first-use mapping tests.
- Added Release BenchmarkDotNet baseline/candidate pairs for the same three
  violation shapes. A source-generated registry was evaluated but not added:
  the runtime cache preserves the existing public API and supplies the required
  fallback for arbitrary application-defined violation types.
- BenchmarkDotNet now runs the exception benchmarks with an explicit
  in-process .NET 10 job, avoiding the repository's incompatible generated
  net8 benchmark project. The completed run produced these baseline/candidate
  results (mean, allocated bytes):

  | Shape | Reflection | Cached |
  | --- | ---: | ---: |
  | Empty | 372.6 ns, 784 B | 222.7 ns, 688 B |
  | Single property | 387.3 ns, 1040 B | 319.4 ns, 936 B |
  | Several properties | 512.7 ns, 1104 B | 354.3 ns, 984 B |
