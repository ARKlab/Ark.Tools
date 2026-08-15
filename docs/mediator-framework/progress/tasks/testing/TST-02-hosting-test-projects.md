# TST-02 — Create framework-owned hosting test projects

**Depends on:** TST-01
**Scope:** `tests/`

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Add a dedicated
   `tests/Ark.Tools.MediatorFramework.Hosting.Tests/` project targeting
   `net10.0`; add it to `Ark.Tools.slnx`.
2. Mirror the existing MSTest testing-platform hook used by
   `tests/Ark.Tools.MediatorFramework.Tests/` so the project discovers at least
   one test with the repo test command.
3. Reference only framework/runtime packages and existing Ark.Tools building
   blocks. Do not reference any project under
   `samples/Ark.MediatorFramework.Sample/`.
4. Follow the existing generated-client pattern. If MSBuild ordering requires
   it, add a small
   `tests/Ark.Tools.MediatorFramework.Hosting.GrpcClient/` support project and a
   test-contracts project; export the test proto to a deterministic build
   directory and generate the client with the existing centrally managed
   `Grpc.Tools` package.
5. Add a synthetic contract set covering one request, query, command, Rebus
   message, route/query/body parameters, server-owned property, validation
   failure, business violation, streaming result, attachment, and version
   lifetime. Keep handlers deterministic and backed by test-only state.
6. Add a fixture that creates and disposes a test `Container`, configures the
   default scoped lifestyle, registers the synthetic handlers, and builds the
   Minimal API/gRPC/Rebus host layers independently.
7. Add a test-only authenticated principal provider; do not use real identity
   providers or network credentials.

## Outcome

- The framework has an independent host-boundary test home and a synthetic
  application that cannot hide generator or runtime defects.

## Acceptance

- [x] The new project is in the solution and passes the normal test command.
- [x] No hosting test project references the sample application.
- [x] A smoke test proves the synthetic handler can be resolved from its
  container and that the fixture disposes all host resources.
- [x] Generated proto/client artifacts are reproducible and are not committed
  as generated `bin/` or `obj/` output.
- [x] All public fixture helpers have XML documentation.

## Tests

- `dotnet test tests/Ark.Tools.MediatorFramework.Hosting.Tests/ -f net10.0`.
- Run the generated-client test once from a clean `obj/` directory.
- Required scenarios/cases:
  - the synthetic handler resolves and the fixture disposes its container and
    host resources;
  - a clean build exports the synthetic proto and generates the client without
    sample project references;
  - the project discovers the smoke test through the normal solution test
    command.
- Run the full-solution build and test gates.

## Validation

- `dotnet build tests/Ark.Tools.MediatorFramework.Hosting.Tests/Ark.Tools.MediatorFramework.Hosting.Tests.csproj --configuration Debug --no-restore`
- `dotnet test tests/Ark.Tools.MediatorFramework.Hosting.Tests/ -f net10.0 --configuration Debug --no-build`
- Clean `obj/` build of `Ark.Tools.MediatorFramework.Hosting.GrpcClient` exported
  `Hosting.proto` and generated the client successfully.
- `dotnet build Ark.Tools.slnx --configuration Debug`
- `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1`
