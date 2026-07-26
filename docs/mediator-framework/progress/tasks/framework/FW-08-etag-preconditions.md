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
- `IEntityWithETag` forces a **mutable** member on contracts that are immutable `record`s, and the
  member name `_ETag` leaks verbatim into JSON, MessagePack and `.proto` payloads.
- It is HTTP-only: the mediator exposes the *same* contract over gRPC and Rebus, where the token must
  travel as a normal message field, not as a header.

**Decision (D9)**: the framework does **not** adopt `IEntityWithETag` for mediator contracts. It
introduces a declarative marker attribute, `[ETag]`, on a single `string?` contract property. The
token is **opaque** — the framework never parses, derives, or interprets it; only the application
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
`Ark.MediatorFramework` (this is the transport-agnostic core package — `ApiGroupAttribute.cs` in the
same folder is the pattern to copy for file header, XML docs and style):

- `[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]`
- `public sealed class ETagAttribute : Attribute`
- XML docs must state: the property carries an **opaque** concurrency token; on an HTTP request
  contract it is bound from the `If-Match` request header (never from body, route or query); on a
  response contract it is emitted as the `ETag` response header (FW-09); on gRPC and Rebus it travels
  as a normal message field.

### 2. Generator model (`MinimalApiEndpointGenerator.cs`)

In `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/MinimalApiEndpointGenerator.cs`:

- Add `private const string ETagAttribute = "Ark.MediatorFramework.ETagAttribute";` next to the
  existing `ServerSetAttribute` constant, resolve it with `compilation.GetTypeByMetadataName(...)`
  and thread the (nullable) symbol through `Extract(...)` exactly like `serverSetAttr`.
- Add `bool IsETag` to `PropertyModel` and populate it from the attribute presence.
- Add two diagnostics to
  `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi.Generators/DiagnosticDescriptors.cs`
  (ids `ARKMF017` and `ARKMF018` are free; category `Ark.MediatorFramework`, severity `Error`):
  - `ARKMF017` "Invalid ETag property" — `[ETag]` applied to a property whose type is not `string`
    (nullable or not).
  - `ARKMF018` "Duplicate ETag property" — more than one `[ETag]` property on one contract.
  Report them through the existing `DiagnosticInfo` list plumbing used by `MissingRouteProperty`, and
  return `EndpointModel.Invalid(...)` for that endpoint so no broken code is emitted.

### 3. Binding rules

An `[ETag]` property on the **request contract** is bound from headers, never from client body/route/
query:

- It is excluded from the body-shape mass-assignment scan and from route/query binding: treat it in
  the same *exclusion* positions where `IsServerSet` is tested today (search `IsServerSet` in the
  generator), but keep it out of `ServerSetProperties` (that list is also consumed by the gRPC
  generator and by the OpenAPI schema filter for a different purpose).
- For every emitted endpoint variant (plain, explicit-binding, multipart, download), after the
  request instance is constructed and after `EmitServerSetAssignments`, assign the header value:
  `request = request with { <Prop> = <resolved> };` for records, `request.<Prop> = <resolved>;`
  otherwise (mirror `EmitServerSetAssignments` for the non-record path).
- Resolution order (implement as a **public static helper in the runtime package**, not as inline
  emitted logic — see step 5):
  1. If `If-Match` is present and non-empty → the raw header value, unquoted (strip one leading and
     one trailing `"`), preserving `*` as `*`. Multiple comma-separated values: pass the raw header
     through as-is only when there is exactly one value; when there are several, pass the **first**
     and let the application compare — document this ceiling in the helper XML docs.
  2. Else, if `If-None-Match` is present and its value is `*` → `"*"` (the "create only if absent"
     assertion, matching the MVC filter behavior).
  3. Else → `null` (no precondition supplied; the handler decides whether that is allowed).
- The property value that arrived in the request **body** must be discarded before the header
  resolution assigns the final value, so a client cannot bypass the precondition by putting a token
  in the body.

### 4. OpenAPI

- For endpoints whose request contract has an `[ETag]` property, the property must not appear in the
  request schema. Extend `AddArkServerSetProperties` in
  `src/mediator-framework/Ark.Tools.MediatorFramework.MinimalApi/ArkOpenApiEx.cs` — rename nothing;
  add the `ETagAttribute` check alongside the `ServerSetAttribute` check inside the same schema
  transformer and update its XML doc. (The response direction is FW-09; a response schema keeping
  the property is correct and intended, because gRPC clients read it there. Filtering here applies to
  request bodies only — if the same type is used for both, keep the property and rely on the header;
  document that in the XML docs.)
- The generator must add an `If-Match` header parameter to such endpoints. Emit
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
  concurrency token.
- Minimal API endpoints whose contract declares `[ETag]` receive the `If-Match` (or `If-None-Match: *`)
  value in that property, and clients cannot set it through body/route/query.
- OpenAPI documents the `If-Match` parameter and hides the property from request schemas.
- gRPC and Rebus behavior is unchanged: the property remains a normal message field.
- `docs/mediator-framework/design.md` gains an "Optimistic concurrency: opaque ETag tokens" section
  recording decision D9 (attribute over `IEntityWithETag`, and why).

## Acceptance

- [ ] `ETagAttribute` added to the core package with XML docs; no new dependencies anywhere.
- [ ] Generator tests in `tests/Ark.Tools.MediatorFramework.Tests/GeneratorSnapshotTests.cs`
      (follow the existing `CSharpGeneratorDriver` harness):
      a contract with an `[ETag]` property emits the header-binding assignment; a non-`string`
      `[ETag]` property reports `ARKMF017`; two `[ETag]` properties report `ARKMF018`.
- [ ] Test proving a token supplied in the **request body** is discarded and replaced by the
      `If-Match` header value (no mass-assignment bypass).
- [ ] Test for `ArkETag.ReadPrecondition`: `If-Match: "abc"` → `abc`; `If-None-Match: *` → `*`;
      neither header → `null`.
- [ ] Test for `ArkETag.IsValidToken` rejecting a quote, a backslash and a control character.
- [ ] `.proto` export for a contract with an `[ETag]` property still contains the field (gRPC parity
      unchanged) — assert in an existing gRPC generator test.
- [ ] `design.md` updated with the D9 section.
- [ ] Full solution build with zero warnings + `dotnet test Ark.Tools.slnx` green.
