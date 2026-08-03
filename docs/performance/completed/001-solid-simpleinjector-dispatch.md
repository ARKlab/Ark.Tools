# Replace Solid runtime dispatch

## Scope

`SimpleInjectorRequestProcessor`, `SimpleInjectorQueryProcessor`, and
`SimpleInjectorCommandProcessor` construct closed handler interfaces with
`MakeGenericType` and invoke handlers through `dynamic` for every dispatch.
Ark.Reference controllers reach these processors on normal HTTP requests.

## Outcome

Eliminate per-dispatch runtime reflection and dynamic binder work while
preserving handler decoration, scoping, cancellation, exceptions, and public
`IRequestProcessor`, `IQueryProcessor`, and `ICommandProcessor` APIs.

## Implementation guidelines

1. Establish the benchmark project at
   `/home/runner/work/Ark.Tools/Ark.Tools/benchmarks/Ark.Tools.Benchmarks/`.
   Use BenchmarkDotNet and project-reference the production projects under
   test. Do not add benchmark code to samples or test projects.
2. Benchmark the current request, query, and command paths independently,
   using a verified SimpleInjector container with representative decorated
   handlers.
3. Prefer an opt-in source generator that emits strongly typed processor
   dispatch for discovered contracts. It must call the same closed handler
   services through SimpleInjector so registered decorators retain their
   ordering and lifetimes.
4. Keep the runtime processor as the compatibility fallback for contracts not
   covered by generated code, including dynamically loaded contracts.
5. If an incremental cache is delivered before generation, cache only immutable
   closed handler type/invoker metadata. Never cache a handler instance, because
   handlers and decorators can be scoped or transient.
6. Add behavioral tests covering decorated request/query/command handlers,
   cancellation propagation, exceptions, and fallback dispatch.

## Acceptance criteria

- Generated dispatch contains no `MakeGenericType`, `dynamic`, or reflection
  invocation on the covered request path.
- Existing public processor interfaces and existing registration APIs remain
  source and binary compatible.
- Decorators execute in the same order before and after the change.
- The fallback path remains functional and documented.
- BenchmarkDotNet results show an improvement in mean execution time and
  allocated bytes for every covered dispatch kind versus the committed
  baseline; publish the result markdown in the benchmark artifact/output, not
  as a source-controlled performance claim.

## Decision record

- Added a transitive incremental source generator and module initializer, so
  existing processor registrations and constructor calls automatically use the
  generated dispatcher without application changes.
- Generated dispatch resolves the same closed handler service from the
  SimpleInjector container on every call, preserving decorators and lifetimes.
- Processors retain the existing reflection/dynamic path when no generated
  dispatcher handles a contract, including dynamically loaded contracts.
- C# 14 call-site interception was not used: processor calls commonly target
  public interfaces, and replacing those calls would not preserve custom
  processor implementations or a safe original-call fallback. The generated
  module initializer provides the same seamless behavior at processor creation.
- Added warning `SOLID001` with an IDE code fix for the interim
  `SimpleInjector*Processor.Create(container, dispatcher)` API, so consumers
  that adopted the incremental shape can return to the unchanged constructor
  without manual edits.
- Benchmarks compare fallback and generated request, query, and command paths;
  benchmark output remains an artifact rather than a source-controlled claim.
- Smoke results on .NET 10.0.10: request 363.04 ns/464 B versus 54.85 ns/192 B,
  query 367.27 ns/464 B versus 56.56 ns/192 B, and command 274.68 ns/200 B
  versus 38.65 ns/48 B. Raw output is in `BenchmarkDotNet.Artifacts/results/`.

## Verification

1. Run the focused processor and decorator tests, then
   `dotnet build /home/runner/work/Ark.Tools/Ark.Tools/Ark.Tools.slnx --configuration Debug`.
2. Run
   `dotnet test /home/runner/work/Ark.Tools/Ark.Tools/Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`.
3. Run the BenchmarkDotNet dispatch benchmarks in Release outside a debugger,
   with the baseline and candidate on the same machine and SDK. Retain raw
   BenchmarkDotNet artifacts with the change review.
