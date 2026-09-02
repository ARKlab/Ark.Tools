# DOC-01 — User documentation: getting started + feature guide

**Category**: docs · **Priority**: **Release blocker** · **Scope**: DOCS (+ sample references)

## Problem

`docs/mediator-framework/` contains design, research and delivery tracking — documents written for the
people building the framework. An adopting team has no getting-started path and no per-feature
reference; today the only usable entry point is reading `design.md` (~700 lines) and the sample source.

## Design

A task-oriented guide under `docs/mediator-framework/guide/`, separate from `design.md` (which stays
the architecture rationale) and from `progress/` (delivery tracking). Every page:

- states what the feature is for, in one paragraph;
- shows the **smallest** working example (contract + attribute + what the developer gets);
- links to the exact file in `samples/Ark.MediatorFramework.Sample` that demonstrates it;
- lists the escape hatch when the feature does not fit.

Snippets must be copied from compiled sample code, never invented, so they cannot rot silently.

## Pages to write

| File | Content |
| --- | --- |
| `guide/README.md` | Index + when to use this framework vs `Ark.Tools.AspNetCore` MVC. |
| `guide/getting-started.md` | New project: reference the packages, write one contract + handler, host it on Minimal API, call it; then add gRPC and Rebus by adding attributes only. Includes the SimpleInjector wiring and the run/test commands. |
| `guide/contracts-and-handlers.md` | `IRequest`/`IQuery`/`ICommand`, pure handler rules, records, `[ProtoContract]`/`[ProtoMember]` numbering rules. |
| `guide/http-endpoints.md` | `[HttpEndpoint]`: verb/template, envelope binding (route/query/body), `[HttpQuery]`, `[ServerSet]`, status semantics (`SuccessStatusCode`, `NullResultStatusCode`), route groups. |
| `guide/grpc.md` | `[GrpcMethod]`, `[ServiceGroup]`, proto export on build, consuming the exported protos, gRPCui. |
| `guide/rebus.md` | `[RebusMessage]`, owner queues + generated routing, per-message scope, cancellation, HTTP→bus composition. |
| `guide/versioning.md` | `Versioning(Introduced, Retired)`, `{version}` expansion, per-version documents and gRPC services, superseding a contract. |
| `guide/errors.md` | Domain exceptions → ProblemDetails / `Google.Rpc.Status`, `BusinessRuleViolation`, standard 400/403/500 responses, what is never leaked. |
| `guide/validation-and-authorization.md` | FluentValidation decorators, the transport-agnostic policy decorator, secure-by-default endpoints and `AllowAnonymous`. |
| `guide/serialization.md` | JSON (Ark defaults, STJ source generation), MessagePack negotiation + required resolver, protobuf/NodaTime surrogates, polymorphism across the three wires. |
| `guide/attachments.md` | Upload (single + multiple files), download, limits and sanitization, gRPC streaming upload, `MapArkAttachmentUpload` escape hatch. |
| `guide/streaming.md` | `IAsyncEnumerable<T>` responses on HTTP/gRPC, MessagePack buffering ceiling, cancellation. |
| `guide/openapi.md` | Documents per version, tags/operation names, XML documentation flow, NodaTime/polymorphism/`[ServerSet]` transformers, OAuth2 configuration, Scalar. |
| `guide/api-surface-snapshots.md` | Why the build fails on an API change, how to regenerate, how to review the diff, shipped vs unshipped. |
| `guide/testing.md` | Reqnroll behavioral tests through public interfaces, the gRPC client generated from exported protos, test auth. |
| `guide/escape-hatches.md` | Hand-written Minimal API mapping, hand-written gRPC method in the generated `partial`, hand-written `IHandleMessages<>`, MVC coexistence (links to `../migration-from-mvc.md`). |

## Steps

1. Write the pages above; keep each under ~150 lines and prefer a table + one snippet over prose.
2. Cross-link: `docs/mediator-framework/README.md` points to `guide/README.md` as the entry point for
   users; every guide page links back to the relevant `design.md` section for rationale.
3. Add a short "Documentation" section to `samples/Ark.MediatorFramework.Sample/README.md` mapping each
   sample file to the guide page it illustrates.
4. Verify every referenced sample path exists (a wrong link is a defect).
5. Write the pages **after** NET-06, GEN-09, FW-05, FW-06, FW-07 and GEN-10 are merged, so the documented
   behavior is the shipped behavior. If a feature is still pending when the page is written, the page
   states it explicitly rather than describing an unimplemented API.

## Test coverage (required)

Documentation has no unit tests; the enforced checks are:

- A link check over `docs/mediator-framework/**/*.md` — every relative link resolves to an existing
  file (run it and record the command and its output in the PR description).
- Every code snippet is copied from a file in `samples/Ark.MediatorFramework.Sample` that is compiled by
  `dotnet build Ark.Tools.slnx`; the snippet's source path is cited immediately under it.
- Reviewer check: following `guide/getting-started.md` verbatim produces a running endpoint (the PR
  author states they performed this walkthrough).

## Outcomes

- An adopting team can go from zero to a working three-transport service by following the guide, and can
  look up every supported feature without reading the design or the generator source.

## Acceptance

- [x] All pages in the table exist with the described content.
- [ ] Every snippet cites the compiled sample file it comes from.
- [x] All relative links in `docs/mediator-framework/` resolve (link check performed, result in PR).
- [x] `docs/mediator-framework/README.md` and the sample README point at the guide.
- [x] No page documents an unimplemented behavior without flagging it as pending.
- [x] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [x] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.

> **Review 2026-09-02**: All 16 guide pages exist and links resolve; still open: several pages (http-endpoints, contracts-and-handlers, serialization, streaming, openapi, errors, validation-and-authorization, attachments, versioning, escape-hatches, api-surface-snapshots) do not yet cite the compiled sample files their snippets come from.
