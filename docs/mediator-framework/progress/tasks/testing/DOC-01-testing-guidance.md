# DOC-01 — Publish the revised testing guidance

**Depends on:** TST-03, TST-04, TST-05, APP-06
**Scope:** Mediator Framework reference and sample documentation

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Rewrite `docs/mediator-framework/guide/testing.md` around two workflows:
   - framework maintainers use `tests/Ark.Tools.MediatorFramework.Tests` and the
     hosting test project for generated host/wire behavior;
   - application teams use their composition root, a scenario-owned
     SimpleInjector scope, direct contract dispatch, the SQL default or
     explicit in-memory store profile, and Rebus idle/outbox waiting.
2. Add a “what not to assert” section explicitly excluding URLs, status codes,
   ProblemDetails format, serialization, and OpenAPI from application tests.
3. Add a complete direct-dispatch example using the sample's
   `ApplicationComposition.Register` pattern, with no transport types.
4. Document the default SQL profile, Docker startup, DACPAC reset, explicit
   `ARK_SAMPLE_INMEMORY_TESTS=1` profile, deterministic clock/user setup, and
   bounded background-bus wait.
5. Update the testing section of
   `docs/mediator-framework/design.md` so it no longer says sample scenarios
   exercise HTTP/gRPC public interfaces.
6. Update `docs/mediator-framework/guide/README.md`,
   `samples/Ark.MediatorFramework.Sample/README.md`, and the progress task
   board with the new ownership and source map.
7. Replace references to the old `SampleTestContext` boundary workflow with the
   direct context and the framework hosting test project.

## Outcome

- A new contributor can reproduce both testing layers from repository
  documentation without inferring hidden host setup.

## Acceptance

- [ ] All documentation uses the same ownership terms as this plan.
- [ ] Every code path in the direct-dispatch example exists in compiled sample
  code or is explicitly marked pseudocode-free guidance.
- [ ] The guide documents failure assertions, SQL and in-memory profile setup,
  Rebus waits, cleanup, and cancellation.
- [ ] Broken links and stale references to HTTP/gRPC sample BDD steps are
  removed.

## Tests

- Repository-wide search for `SampleTestContext`, transport wording in the
  application testing section, and stale T9.8 claims.
- Validate Markdown links by checking each target path.
- Run `git diff --check`.
- Required scenarios/cases:
  - a contributor can follow the direct-dispatch example with the application
    composition root and one scope per top-level contract call;
  - the guide distinguishes framework host/wire tests from sample application
    tests and documents SQL default plus in-memory alternate profiles;
  - failure assertions, bounded Rebus waits, cancellation, cleanup, and all
    referenced paths are documented without credentials.
- Run the full-solution build/test gates; documentation itself needs no
  separate compiler.
