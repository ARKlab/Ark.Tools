# AZF-10 — Core Tools boundary tests, parity matrix and user guide

**Category**: azure-functions · **Priority**: release gate · **Scope**: TESTS + DOCS + CI

## Problem

Helper tests cannot prove Function discovery, host routing, authentication
integration, response streaming or deployment-shaped startup. The workstream needs
a real-host gate and an endpoint-by-endpoint parity record suitable for release.

## Prerequisites

- AZF-01 through AZF-08 merged.
- AZD-06, AZD-07, AZD-09 and AZD-10 decided.
- Verify the pinned Azure Functions Core Tools version against Microsoft local
  development guidance.

## Implementation steps

1. Add a dedicated framework E2E boundary-test project under `tests/` that launches
   Core Tools from the built Function sample directory on an allocated loopback
   port. The generated sample Function is the test subject, not the location of the
   framework tests.
2. Capture process output, wait for a generated anonymous health endpoint with a
   bounded timeout, fail with useful logs on early exit, and terminate the complete
   process tree in cleanup.
3. Keep credentials/configuration test-only and inject them through process
   environment. Never write secrets to committed local settings.
4. Run the existing Minimal API TestServer beside the Function host. Create shared
   test cases that send equivalent HTTP requests and normalize nondeterministic
   fields only when documented.
5. Cover every in-scope sample endpoint and capability: route versions, binding,
   JSON, 401/403, validation, domain/unhandled ProblemDetails, statuses, ETags,
   paging, polymorphism, uploads, downloads, streaming decision and Rebus send.
6. Assert content types, headers and body shape, not only status codes. For safe
   deterministic outputs, compare normalized responses across hosts.
7. Commit a parity matrix listing every sample `[HttpEndpoint]`, its active
   versions, capabilities and exact test method(s) proving Minimal API and
   Functions behavior.
8. Update `.github/workflows/ci.yml` to install an exact Core Tools version and run
   the complete boundary suite in a dedicated job or explicit step on every pull
   request. Cache only the pinned tool installation, archive sanitized Function host
   logs on failure, and fail when the tool or host is unavailable; do not mark tests
   passed through an internal skip.
9. Add an Azure Functions guide covering package installation, host marker,
   startup, JSON configuration, auth profiles, one-way Rebus, local execution,
   tests, platform caveats and explicit MessagePack and OpenAPI exclusions.
10. Update the mediator guide index, design, package table, task tracker and sample
    README. Check all relative links and code references.

## Caveats

- Do not use arbitrary sleeps for readiness.
- Ensure tests can run concurrently without fixed ports or shared process names.
- Redact environment values and authorization headers from captured logs.
- Core Tools is the local runtime, not proof of every Azure front-end behavior;
  record any deployment-only smoke test separately.
- Keep framework-library and Core Tools E2E tests under `tests/`. Keep `samples/`
  focused on showcasing application capabilities and how an application can test
  its own composition.
- A parity matrix row without a runnable test reference is incomplete.

## Required test coverage

- Process start/readiness/failure/cleanup paths.
- Endpoint inventory guard detects new Application endpoints absent from the matrix.
- Cross-host cases listed above, including first-item streaming evidence when
  approved.
- Boundary tests prove generated routes are discovered from Function metadata.
- CI test verifies zero silent skips and archives sanitized host logs on failure.
- Workflow review proves `.github/workflows/ci.yml` invokes the boundary project on
  pull requests rather than relying on its presence in the solution.

## Outcomes

- Feature parity is a runnable release gate, not a design assertion.
- Users have complete isolated-worker setup, security, messaging and test guidance.

## Acceptance

- [ ] AZD-06, AZD-07, AZD-09 and AZD-10 are recorded as decided.
- [ ] The complete Core Tools suite runs on every pull request and fails loudly when
  the host cannot start.
- [ ] `.github/workflows/ci.yml` pins Core Tools, invokes the boundary suite and
  uploads sanitized host logs on failure.
- [ ] Every supported sample endpoint has a parity-matrix row and runnable tests.
- [ ] Auth, errors, files, ETags, streaming decision and Rebus send are boundary-tested.
- [ ] Documentation states all platform limitations and MessagePack and OpenAPI
  exclusions.
- [ ] Relative links and cited file/test names resolve.
- [ ] Changed files pass secret scanning.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
