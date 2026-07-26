# SMP-04 — Optimistic concurrency + opaque ETag in the sample (G6, part 3)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE
**Depends on**: SMP-02 (SQL/Dapper — shipped), FW-08 (`[ETag]` + `If-Match` binding), FW-09
(`ETag` response header + 304 + gRPC error parity).

## Problem

The mediator sample has no concurrency story. FW-08/FW-09 give the framework an opaque, generator-
driven ETag; nothing exercises it end-to-end. This task makes the sample the executable
demonstration: **SQL `ROWVERSION` server-side, opaque `string` on the wire**, plus the Ark
optimistic-concurrency retry pattern.

Reference implementations to mirror (read them before starting):

- `samples/Ark.ReferenceProject/Ark.Reference.Common/Services/Decorators/OptimisticConcurrencyRetrierDecorator.cs`
  and its registration in
  `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/Host/ApiHost.cs` (search
  `OptimisticConcurrency`) — the retry-on-optimistic-failure pattern. Note it uses Polly and
  `Ex.IsOptimistic()` from `samples/Ark.ReferenceProject/Ark.Reference.Common/Ex.cs`.
- `samples/WebApplicationDemo/Dto/Entity.cs` + `samples/ProblemDetailsSample/Common/Dto/Entity.cs` —
  the MVC ETag shape (`IEntityWithETag._ETag` + `ETagHeaderBasicSupportFilterAttribute`). The
  mediator sample uses the `[ETag]` attribute from FW-08 instead, because its contracts are
  immutable `record`s and `IEntityWithETag` requires a settable member — implementing the interface
  is allowed, just not needed here. Carrying the token in the model is intended in both shapes.

## Guardrails

- **No new package dependency.** In particular the sample has no Polly reference: implement the
  retrier as a small bounded loop, not by adding Polly. Any dependency change would also require
  regenerated `packages.lock.json` files (CI runs `RestoreLockedMode=true`) — avoid it.
- **The ETag is opaque on the contract.** The contract property is `string?`. Never expose `byte[]`,
  never document the encoding in the contract XML docs, never let a client-visible type depend on
  `ROWVERSION`. The base64 encoding lives only in the DAL.
- **The ETag property stays in every payload and schema** (request and response, HTTP/gRPC/Rebus).
  The `If-Match` header is an HTTP-only override of the request field, per D9.
- **Do not change the framework.** All generator/runtime behavior comes from FW-08/FW-09. If
  something is missing, stop and record it — do not patch the generator from this task.
- **The default test run uses the in-memory store**: SQL tests are opt-in via `ARK_SAMPLE_SQL_TESTS=1`
  (see `samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/Hooks/SampleTestContext.cs`).
  Every behavior below must therefore work identically on `InMemoryGreetingStore` and
  `SqlGreetingStore`, and the tests must not require SQL.
- **Do not rename or renumber existing `ProtoMember` numbers or MessagePack keys.** New fields get
  new numbers (`GreetingResponse` currently uses 1–7 → the ETag is 8).
- **Do not weaken existing endpoints**: `GetGreetingQuery`, `CreateGreetingRequest`,
  `RefreshGreetingCommand`, `UpdateGreetingRequest` (envelope-binding demo) keep their current
  routes, verbs, status codes and authorization.
- **Do not use `TRUNCATE TABLE`** in test cleanup for FK-referenced tables; `ops.ResetFull_OnlyForTesting`
  already handles the reset and needs no change for a `ROWVERSION` column.

## Implementation details

### 1. Database

`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Database/dbo/Tables/Greeting.sql`:
add `[RowVersion] ROWVERSION NOT NULL`. It is maintained by SQL Server — never inserted or updated
explicitly. `ops/ResetFull_OnlyForTesting.sql` needs no change.

### 2. Contracts (`GreetingContracts.cs`)

- `GreetingResponse`: add
  `[ProtoMember(8)] [ETag] public string? ETag { get; init; }` with XML docs describing it as an
  opaque concurrency token that must be echoed back in `If-Match` (HTTP) or in the request field
  (gRPC).
- New update contract, the actual ETag demo:

  ```
  [HttpEndpoint("PUT", "/api/v{version}/greetings/{id}")]
  [GrpcMethod("UpdateGreetingMessage")] [GrpcService("Greetings")]
  [RequireScopePolicy(ApplicationScopes.GreetingWrite)]
  [ProtoContract]
  public sealed record UpdateGreetingMessageRequest : IRequest<GreetingResponse>
  ```
  with `Guid Id` (route), `string Message`, `[ETag] string? ETag` (`[ProtoMember]`-numbered and
  bindable from the body — it is a normal field), and `[ServerSet] string? UserId` (mirror
  `CreateGreetingRequest`). Follow the existing file's XML-doc and attribute style.
- Add a FluentValidation validator for the new request in `GreetingValidators.cs` (non-empty
  `Message`, non-empty `Id`), mirroring the existing validators.

### 3. Store and DAL

`IGreetingStore` gains
`Task<GreetingResponse> UpdateAsync(Guid id, string message, string? expectedETag, AuditEntry? audit, CancellationToken ctk)`.

`SqlGreetingStore` / `SampleDataContext`:

- Token encoding, DAL-private: `Convert.ToBase64String(rowVersionBytes)` on read;
  `Convert.FromBase64String(token)` on write, wrapped so that a malformed token becomes
  `EntityTagMismatchException` (never an unhandled `FormatException` → never a 500).
- `SELECT` statements add `[RowVersion]`; `GreetingRow` gains `public byte[] RowVersion { get; set; }`
  and `ToResponse()` sets `ETag`.
- Conditional update:

  ```
  UPDATE [dbo].[Greeting] SET [Message] = @Message, [AuditId] = @AuditId
  OUTPUT inserted.[RowVersion]
  WHERE [Id] = @Id AND [RowVersion] = @RowVersion;
  ```
  Zero rows affected → re-read the row: if it no longer exists, throw `EntityNotFoundException`;
  if it exists, the client token was stale → throw
  `Ark.Tools.Core.EntityTag.EntityTagMismatchException` (→ 412). Return the response built from the
  `OUTPUT`ed new `RowVersion`, so the caller immediately gets the new ETag.
- A `null`/absent `expectedETag` on the update path (neither header nor body field) throws
  `EntityTagMismatchException` — the sample requires the precondition. (`428 Precondition Required` is deliberately out of scope; say so in the
  handler XML docs.)
- `InMemoryGreetingStore` must expose the same semantics with an in-memory monotonic version per id
  (for example `Convert.ToBase64String(BitConverter.GetBytes(version))`), compared under the same
  lock/`ConcurrentDictionary` update that stores the new value, so a stale token loses.

### 4. Optimistic-concurrency retrier

Add `OptimisticConcurrencyRetrierDecorator<TRequest, TResult> : IRequestHandler<TRequest, TResult>`
to the sample Application project (new file), and register it in `ApplicationComposition.Register`
with `container.RegisterDecorator(typeof(IRequestHandler<,>), typeof(OptimisticConcurrencyRetrierDecorator<,>));`.

- Retries at most **2** times, only when the exception (or any inner exception) is
  `Ark.Tools.Core.OptimisticConcurrencyException` — mirror the `IsOptimistic()` walk from
  `Ark.Reference.Common/Ex.cs`, but keep it private and dependency-free (a `while (ex != null)` loop,
  no Polly).
- `EntityTagMismatchException` is **never** retried: it is a client precondition failure (412), not a
  server-detected race.
- Log each retry at **Warn** with NLog structured logging and `CultureInfo.InvariantCulture`
  (request type name + attempt number); the exhausted attempt is not logged here — it surfaces as the
  409 ProblemDetails.
- Decorator ordering: register it **outermost** relative to validation/audit decorators so a retry
  re-runs the whole handler pipeline; verify the registration order in `ApplicationComposition.cs`
  and state the chosen order in a code comment.

### 5. Conflict test seam

To exercise 409 deterministically on both stores, add a small singleton to the Application project,
e.g. `ConcurrencyFaultInjector` with `int PendingFailures { get; set; }`, consulted at the start of
`UpdateAsync` in both stores: when positive, decrement and throw
`new OptimisticConcurrencyException(...)`. Register it as a singleton next to `AuditCounter` (which
is the existing precedent for a test-observable singleton). Document in XML docs that it exists to
demonstrate the retry/409 path.

### 6. Tests

`samples/Ark.MediatorFramework.Sample/test/Ark.MediatorFramework.Sample.Tests/` — Reqnroll feature +
steps (follow `Features/Greetings.feature` and `Steps/GreetingSteps.cs`) or a dedicated MSTest class
(follow `AuthorizationTests.cs`); use AwesomeAssertions.

Required scenarios:

1. `GET` a greeting → response carries a non-empty quoted `ETag` header and the same value in the
   body property.
2. `PUT` with the current `If-Match` → `200`, body contains a **different** ETag than before, and the
   response `ETag` header equals it.
3. `PUT` with a stale `If-Match` (the pre-update token) → `412` ProblemDetails.
4. `PUT` with no `If-Match` but the current token in the body `etag` field → `200` (the field is a
   first-class source); `PUT` with a stale token in the body and no header → `412`.
5. `PUT` with neither `If-Match` nor a body token → `412` ProblemDetails.
6. `PUT` with a syntactically invalid token (not base64) → `412`, not `500`.
7. `GET` with `If-None-Match` equal to the current ETag → `304` with an empty body; with a stale
   value → `200`.
8. `ConcurrencyFaultInjector.PendingFailures = 2` → `PUT` still succeeds (retrier); `= 3` → `409`
   ProblemDetails (retries exhausted).
9. gRPC parity: `GetGreeting` returns the ETag in the message; `UpdateGreetingMessage` with a stale
   token fails with `StatusCode.FailedPrecondition`, and with the fault injector exhausted fails with
   `StatusCode.Aborted` (use `Ark.MediatorFramework.Sample.GrpcClient`, as in the existing transport
   parity tests).

### 7. Documentation

- `samples/Ark.MediatorFramework.Sample/README.md`: short "Optimistic concurrency" section showing
  the `curl` round trip (read ETag → `If-Match` → 412 on stale).
- `docs/mediator-framework/design.md`: extend the D9 section written by FW-08/FW-09 with the sample's
  `ROWVERSION`-backed encoding, stating that the encoding is a DAL detail and the contract stays
  opaque.

## Outcomes

- The sample demonstrates the full Ark optimistic-concurrency pattern on the mediator stack: SQL
  `ROWVERSION` server-side, opaque token on the contract, `If-Match`/`ETag` over HTTP, message field
  over gRPC, retry decorator, and 412/409 ProblemDetails.
- Both the in-memory and SQL stores behave identically, so the default (SQL-less) test run covers it.

## Acceptance

- [ ] `Greeting` table has `ROWVERSION`; no explicit writes to it anywhere.
- [ ] `GreetingResponse.ETag` and `UpdateGreetingMessageRequest.ETag` are `string?` marked `[ETag]`,
      serialized in JSON/MessagePack/protobuf and present in the OpenAPI schemas; no `byte[]` on any
      contract.
- [ ] Retrier decorator registered outermost on `IRequestHandler<,>`; retries only
      `OptimisticConcurrencyException`, never `EntityTagMismatchException`.
- [ ] All nine scenarios above pass **without** `ARK_SAMPLE_SQL_TESTS=1`, and the SQL-backed run
      (`ARK_SAMPLE_SQL_TESTS=1`) passes the same suite.
- [ ] No new package references; no `packages.lock.json` churn.
- [ ] Sample README and `design.md` updated.
- [ ] Full solution build with zero warnings + `dotnet test Ark.Tools.slnx` green.
