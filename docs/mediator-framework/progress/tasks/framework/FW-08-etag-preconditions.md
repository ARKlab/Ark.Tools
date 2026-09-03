# FW-08 — `[ETag]` contract attribute + `If-Match` precondition binding (G6, part 1)

**Category**: framework · **Priority**: Release blocker · **Scope**: FRAMEWORK
**Depends on**: FW-03 (shared ProblemDetails package — already shipped).
**Blocks**: FW-09, SMP-04.

## Problem

The mediator framework has no concurrency-token story. Existing Ark hosts solve it with the MVC
filter `ETagHeaderBasicSupportFilterAttribute`
(`src/aspnetcore/Ark.Tools.AspNetCore/ETagHeaderBasicSupportFilterAttribute.cs`), which reflects over
`Ark.Tools.Core.EntityTag.IEntityWithETag` (a mutable `string? _ETag` property) at runtime. That
approach cannot be reused as-is:

- It is MVC-only (`ActionFilterAttribute`) — the mediator host is MVC-free.
- `IEntityWithETag` forces a **mutable** member on contracts that are immutable `record`s.
- It is HTTP-only: the mediator exposes the *same* contract over gRPC and Rebus, where the token must
  travel as a normal message field, not as a header.

**Decision (D9, revised)**: the framework introduces a declarative marker attribute, `[ETag]`, on a
single `string?` contract property, because a *transport-agnostic* marker is needed and
`IEntityWithETag` forces a mutable member. The attribute is a marker only: a contract that does
implement `IEntityWithETag` (`string? _ETag { get; set; }`) is a perfectly valid carrier — just put
`[ETag]` on that member.

**The concurrency field is part of the model on every protocol.** It stays serialized in JSON,
MessagePack and protobuf, on requests as well as responses, and it stays visible in the OpenAPI
schemas: that is the only way optimistic concurrency works over transports without headers. The
HTTP `If-Match` header is an *additional*, higher-priority source, not a replacement — nothing is
hidden or stripped from a payload because of `[ETag]`.

The token is **opaque** — the framework never parses, derives, or interprets it; only the application
knows that it encodes (for example) a SQL `ROWVERSION`. `Ark.Tools.Core.EntityTag.IEntityWithETag`
stays untouched and keeps serving MVC hosts. The exception types (`EntityTagMismatchException` →
412, `OptimisticConcurrencyException` → 409) **are** reused from `Ark.Tools.Core`; they are already
mapped by `Ark.Tools.AspNetCore.ProblemDetails`
(`src/aspnetcore/Ark.Tools.AspNetCore.ProblemDetails/ExceptionProblemDetailsMapper.cs`) — do not add
new exception types and do not touch the mapper.

This task covers the **request/precondition** direction only. FW-09 covers response emission.

## Guardrails

- **Do not modify** `Ark.Tools.Core`, `Ark.Tools.AspNetCore`, or
  `Ark.Tools.AspNetCore.ProblemDetails`. No changes to `IEntityWithETag` or to the MVC filter.
- **Do not add any package dependency.** No new NuGet references anywhere.
- **Do not modify the gRPC or Rebus generators.** An `[ETag]` property is an ordinary contract member
  for them (it must keep its `ProtoMember` field and stay in the request message). Explicitly:
  `[ETag]` is **not** `[ServerSet]` — do not add `ETagAttribute` to any `ServerSet` filtering list in
  `src/mediator-framework/Ark.Tools.MediatorFramework.Grpc.Generators/GrpcEndpointGenerator.cs`.
- **Do not remove the `[ETag]` property from any payload or schema.** It stays bindable from the
  request body and documented in the request schema; the header only overrides it.
- **Do not touch the sample** (`samples/Ark.MediatorFramework.Sample`). SMP-04 consumes this feature.
- **Do not implement** `428 Precondition Required`, weak validators (`W/"..."`),
  `If-Unmodified-Since`, or `If-Range`. Out of scope; record nothing, they are already deferred here.
- **Never emit or accept an unvalidated token into a header** — see the header-injection rule in
  step 5. This is a security requirement, not an optimization.
- Keep the framework transport-agnostic: `Ark.Tools.MediatorFramework` must not reference ASP.NET
  Core.

## Implementation details

### 1. `ETagAttribute` (new file)

Create `src/mediator-framework/Ark.Tools.MediatorFramework/ETagAttribute.cs`, namespace
`Ark.Tools.MediatorFramework` (this is the transport-agnostic core package — `ApiGroupAttribute.cs` in the
same folder is the pattern to copy for file header, XML docs and style):

- `[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]`
- `public sealed class ETagAttribute : Attribute`
- XML docs must state: the property carries an **opaque** concurrency token; it is a normal, fully
  serialized member of the contract on every transport (JSON, MessagePack, protobuf, Rebus); on an
  HTTP request contract the `If-Match` request header, when present, overrides the value bound from
  the body; on a response contract it is also emitted as the `ETag` response header (FW-09).

### 2. Generator model (`MinimalApiEndpointGenerator.cs`)

In `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`:

- Add `private const string ETagAttribute = "Ark.Tools.MediatorFramework.ETagAttribute";` next to the
  existing `ServerSetAttribute` constant, resolve it with `compilation.GetTypeByMetadataName(...)`
  and thread the (nullable) symbol through `Extract(...)` exactly like `serverSetAttr`.
- Add `bool IsETag` to `PropertyModel` and populate it from the attribute presence.
- Add two diagnostics to
  `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/DiagnosticDescriptors.cs`
  (ids `ARKMF017` and `ARKMF018` are free; category `Ark.Tools.MediatorFramework`, severity `Error`):
  - `ARKMF017` "Invalid ETag property" — `[ETag]` applied to a property whose type is not `string`
    (nullable or not).
  - `ARKMF018` "Duplicate ETag property" — more than one `[ETag]` property on one contract.
  Report them through the existing `DiagnosticInfo` list plumbing used by `MissingRouteProperty`, and
  return `EndpointModel.Invalid(...)` for that endpoint so no broken code is emitted.

### 3. Binding rules

An `[ETag]` property on the **request contract** binds normally (body, and route/query if the
property is a route/query parameter — no exclusions anywhere: do **not** touch the `IsServerSet`
exclusion sites). On HTTP the precondition headers additionally override it:

- For every emitted endpoint variant (plain, explicit-binding, multipart, download), after the
  request instance is constructed and after `EmitServerSetAssignments`, assign the resolved header
  value **only when a header precondition was supplied**:
  `if (etag is not null) request = request with { <Prop> = etag };` for records,
  `request.<Prop> = etag;` otherwise (mirror `EmitServerSetAssignments` for the non-record path).
- Resolution order (implement as a **public static helper in the runtime package**, not as inline
  emitted logic — see step 5):
  1. If `If-Match` is present and non-empty → split the header on `,`, trim whitespace, take the
     **first** entry, then unquote it (strip one leading and one trailing `"`); `*` stays `*`.
     Order is: split → take first → unquote. Only the first entry is honored; document that ceiling
     in the helper XML docs (upgrade path: return all entries and let the application match any).
  2. Else, if `If-None-Match` is present and its value is `*` → `"*"` (the "create only if absent"
     assertion, matching the MVC filter behavior).
  3. Else → `null` — **no header precondition; the value bound from the payload is kept as-is** (it
     may itself be `null`, in which case the handler decides whether that is allowed).
- Precedence is header-over-payload, and it is one-directional: an inbound header value is never
  written back into a response header (FW-09 emits only handler-produced values).

### 4. OpenAPI

- **Do not filter the property out of any schema.** `AddArkServerSetProperties` in
  `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkOpenApiEx.cs` stays as it is —
  `[ETag]` is not `[ServerSet]`; the property is documented in both request and response schemas
  because non-HTTP clients supply and read it there.
- The generator must add an `If-Match` header parameter to such endpoints, documented as an optional
  override of the payload value. Emit
  `.WithMetadata(new global::Ark.Tools.MediatorFramework.MinimalApi.ArkETagParameterMetadata())` and
  add an operation transformer, `AddArkETagParameters()`, in `ArkOpenApiEx.cs` that appends an
  optional `If-Match` header parameter (`string`, description: opaque concurrency token) to every
  operation carrying that metadata. Follow `AddArkTypeConverterValueSchemas` /
  `ArkTypeConverterParameterMetadata` as the existing pattern for metadata-driven operation
  transformers, including where the host opts in.

### 5. Runtime helper (security boundary)

Add `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkETag.cs` (namespace
`Ark.Tools.MediatorFramework.MinimalApi`), public and XML-documented:

- `public static string? ReadPrecondition(HttpContext context)` — implements the resolution order of
  step 3. The generated code calls only this.
- `public static bool IsValidToken(string value)` — rejects tokens containing `"`, `\`, or any
  control character (`< 0x20` or `0x7F`); used by FW-09 on emission. Add it here now so FW-09 has no
  framework-shape decision left to make.
- Validate the **inbound** value too: if the resolved precondition fails `IsValidToken` and is not
  `*`, return it unchanged — it is application data and will simply not match — but **never** write
  an inbound value into any response header (FW-09 emits only handler-produced values).

## Outcomes

- `[ETag]` exists in `Ark.Tools.MediatorFramework` and is documented as an opaque, transport-agnostic
  concurrency token that is a normal, serialized member of the contract on every protocol.
- Minimal API endpoints whose contract declares `[ETag]` receive the `If-Match` (or `If-None-Match: *`)
  value in that property when a header is supplied, and the payload value otherwise.
- OpenAPI documents the `If-Match` parameter; schemas keep the property.
- gRPC and Rebus behavior is unchanged: the property remains a normal message field.
- `docs/mediator-framework/design.md` gains an "Optimistic concurrency: opaque ETag tokens" section
  recording decision D9 (marker attribute over `IEntityWithETag`, token in the model on every
  protocol, header as an override on HTTP).

## Acceptance

- [x] `ETagAttribute` added to the core package with XML docs; no new dependencies anywhere.
- [x] Generator tests in `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`
      (follow the existing `CSharpGeneratorDriver` harness):
      a contract with an `[ETag]` property emits the header-binding assignment; a non-`string`
      `[ETag]` property reports `ARKMF017`; two `[ETag]` properties report `ARKMF018`.
- [x] Test proving the `If-Match` header wins over a token supplied in the request body, and that the
      body token is used when no precondition header is present.
- [x] Test proving the `[ETag]` property is present in the generated request schema (not filtered).
- [x] Test for `ArkETag.ReadPrecondition`: `If-Match: "abc"` → `abc`; `If-None-Match: *` → `*`;
      neither header → `null`.
- [x] Test for `ArkETag.IsValidToken` rejecting a quote, a backslash and a control character.
- [x] `.proto` export for a contract with an `[ETag]` property still contains the field (gRPC parity
      unchanged) — assert in an existing gRPC generator test.
- [x] `design.md` updated with the D9 section.
- [x] Full solution build with zero warnings + `dotnet test Ark.Tools.slnx` green.

> **Review 2026-09-02**: Still open: an end-to-end If-Match-wins-over-body test, an OpenAPI schema test for the `[ETag]` property, the neither-header→null `ReadPrecondition` case, and an explicit `.proto` export assertion (gRPC parity is exercised at runtime via `GrpcErrorsTests.cs`).

> **Review 2026-09-03**: Closed. `MinimalApiETagTests` proves If-Match wins over the body token and the body fallback via the HTTP-exposed `HostingETagUpdateRequest`; `MinimalApiOpenApiTests.ETagPropertiesRemainInDocumentedSchemas` proves the `[ETag]` property stays in the request schema; `GeneratorSnapshotTests.ArkETagReadsAndValidatesPreconditions` covers the neither-header→null case; `GrpcProtoExportTests.ExportsETagFieldsInProtoMessages` asserts the exported `e_tag` field.
