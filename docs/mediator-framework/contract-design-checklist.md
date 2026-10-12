# Contract design checklist

Answer this checklist before you add a Mediator Framework contract or change a
released one. Each section asks the questions a reviewer will otherwise ask after
the code is written. Where Ark has a default, the checklist states it; a
deviation is allowed only with a recorded reason.

The [contract design workflow](contract-design-workflow.md) explains the decision
tree and the boundaries between your code and the generated code. This page is
the list of decisions.

## 0. How to use this checklist

1. **Draft.** Fill in every applicable answer you can derive from the request, the
   existing contracts, the code, and these guides. Check the facts the request
   states (types, existing fields, classification, available data) against the
   code, and record any correction at the top of the brief. Tag each answer
   `derived (<source>)`, naming the file, contract, or guide it came from.
2. **Tag the rest `ASK`.** Anything you cannot derive, or that is a business,
   legal, or product decision, is `ASK`. The legal items in
   [§15](#15-privacy-gdpr) are always `ASK`.
3. **Ask one question at a time** and record the answer. Every question lists
   the valid alternatives and a recommendation with its reason, so the answer
   is a choice rather than an essay:

   ```markdown
   **Q3 (§12 Authorization): A support agent may not see Customer B. 403 or 404?**
   - A) 403: simpler to diagnose; confirms the customer exists.
   - B) 404: hides existence; the identifier is an unguessable Guid.
   - Recommendation: B, because a 403 on a Guid discloses that the customer exists.
   ```

   When the right choice depends on input you do not have yet (a business rule,
   an owner, a retention period), still list the alternatives and give a
   conditional recommendation ("if the rule is X, A; otherwise B"). A question
   without alternatives and a recommendation is not ready to ask.

   For the legal items in [§15](#15-privacy-gdpr), list the alternatives (for
   example the lawful bases that could apply) and label the recommendation as a
   suggestion that the DPO must confirm.
4. **Confirm.** Present the completed brief and wait for explicit confirmation.
   Do not write contract, handler, validator, or test code before it.
5. **Record.** Paste the brief into the pull request description. Put every
   decision that code cannot show (why a field is create-only, which rule raises
   which violation, which failures an operation returns) in XML documentation
   (`<summary>`, `<remarks>`). Do not repeat what an attribute already states
   (route, verb, version, scope).

"N/A" is an answer only with its reason. When nobody is available to confirm the
brief, stop after drafting it.

**Scope.** A new contract needs every section. A change to an existing contract
needs the sections whose answers it changes, plus [§14](#14-versioning-and-compatibility)
and [§15](#15-privacy-gdpr), which every change must answer.

## 1. Operation

| Question | Default / rule |
| --- | --- |
| What business operation or fact does it represent? | One sentence in business language; it becomes the contract `<summary>`. |
| Which kind is it? | Read-only → `IQuery<T>`; changes state and returns a value → `IRequest<T>`; changes state, no value → `ICommand`; a fact others react to → event; deferred or retried work → message. |
| Who owns it? | The application whose team owns compatibility for its consumers. |
| Synchronous or background? | Work that must happen after the caller returns, be retried, or run elsewhere is a separate internal message, not part of the request. |

## 2. Fields and updatability

Write one table per model the contract touches, with a row for every field,
including the fields the change does not modify:

| Field | Type | Updatability | Required | Null meaning | Default | Concurrency | Validation | Personal-data class |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Title` | `string` | Updatable | yes | not allowed | — | — | 1–200 chars | not personal |

Updatability labels and where each field lives (see
[Request and DTO best practices](request-and-dto-best-practices.md)):

| Label | Create | Update | Output | Model |
| --- | --- | --- | --- | --- |
| Updatable | ✓ | ✓ | ✓ | `Input` |
| Create-only (write-once) | ✓ | ✗ | ✓ | `Create : Input` |
| Output-only (server computed or assigned, e.g. `Id`, `CreatedAt`) | ✗ | ✗ | ✓ | `Output` |
| Context-supplied (current user, tenant, clock) | ✗ | ✗ | optional | handler or server context; never in the body |
| Identity (route key) | route | route | ✓ | operation envelope |

Rules:

- `PUT` is always a full replace, nulls included. An absent field means its
  default, never "leave it untouched".
- A partial update is its own intent-specific operation (for example
  `Book_RenameRequest`), not a `PUT` that skips fields.
- Say what `null` means for every optional field: "unknown", "not applicable",
  "cleared", or not allowed.

## 3. Types and representation

| Question | Default / rule |
| --- | --- |
| Is the field a domain primitive (identifier, code, email, quantity)? | Use a value object; see [Value objects best practices](value-objects-best-practices.md). Personal data uses a sensitive value object. |
| Which time type? | NodaTime: `Instant` for a point in time, `LocalDate` for a calendar date, `ZonedDateTime`/`OffsetDateTime` when the zone or offset is part of the meaning. Never `DateTime`. |
| Decimal precision, units, currency? | State scale and precision; carry the unit or currency explicitly, not by convention. |
| Normalisation? | State casing, trimming and canonical form (for example, upper-case codes). |
| Is it an enum? | See [§14](#14-versioning-and-compatibility). |

## 4. Validation

Validation is stateless and runs in a `FluentValidation` validator before the
handler; see [Validation and authorization](validation-and-authorization.md).
A failure returns HTTP 400 / gRPC `InvalidArgument`.

| Question | Default / rule |
| --- | --- |
| Presence, length, range, format, pattern of each field? | One row per field in the §2 table's Validation column. |
| Collection sizes? | Every collection has a maximum. |
| Cross-field rules? | Listed and implemented in the validator. |
| Unknown `EvolvableEnum` values on input? | Rejected (`Value == null` fails validation). |
| Paging? | `Limit` has a maximum; `Skip` ≥ 0. |

## 5. Business rules

Rules that need state (persistence, other entities, the current status) belong in
the handler, not the validator.

| Rule | State needed | Named violation |
| --- | --- | --- |
| A print process cannot be cancelled once completed | print process status | `BookPrintProcessCannotBeCancelledViolation` |

- Each rule raises its own `BusinessRuleViolation` subclass with a stable type, as
  in [Errors](errors.md).
- List each violation in the contract's XML documentation as a possible failure.
  It is part of the versioned contract.
- Typical rules: uniqueness, a referenced entity exists, allowed state
  transitions, quotas, ownership.

## 6. Failure contract

List every failure a caller can observe. Defaults from [Errors](errors.md):

| Failure | HTTP | gRPC |
| --- | --- | --- |
| Validation | 400 | `InvalidArgument` |
| Business rule violation (named) | 400 | `FailedPrecondition` |
| Authorization | 403 (or 404, see [§12](#12-authorization)) | `PermissionDenied` |
| Not found | 404 | `NotFound` |
| Optimistic concurrency conflict | 409 | `Aborted` |
| ETag precondition mismatch | 412 | `FailedPrecondition` |

For each failure, record the Problem Details `type` (and any extension members)
that a client can branch on.

## 7. Concurrency and idempotency

| Question | Default / rule |
| --- | --- |
| Can two callers update the same resource concurrently? | Use an ETag (`If-Match`, 412) or a version field (409). State which. |
| Is a retried `POST` safe? | State how a duplicate create is detected (client key, natural key) or why duplicates are acceptable. |
| Can a message be delivered twice? | Handlers are idempotent; name the deduplication key. |

## 8. API design

Defaults, from the [sample](../../samples/Ark.MediatorFramework.Sample/README.md).
A deviation needs a reason.

| Concern | Default |
| --- | --- |
| Route | `/api/v{version}/<plural-kebab-resource>/{id}/<sub-resource>`, for example `/api/v{version}/books/{bookId}/reviews` |
| Non-CRUD action | a sub-path verb, for example `POST /api/v{version}/books/bulk` |
| Status per kind | read `200` (`404` on `null`); update returning a value `200`; create `201` (`SuccessStatusCode = 201`); accepted background work `202`; command without value `204`; see [HTTP endpoints](http-endpoints.md) |
| Binding | route keys `[HttpRoute]`, filters `[HttpQuery]`, payload `[HttpBody]` composing a model |
| gRPC | `[GrpcService("Books")]` (plural resource) and `[GrpcMethod("DownloadBookCover")]` (`VerbNoun`) |
| Contract names | `<Resource>_<Verb>Request`, `<Resource>_<Verb>Query`; events in the past tense (`BookArchived`) |
| Anonymous access | explicit `AllowAnonymous`, with the reason |

## 9. Lists, streaming and files

| Question | Default / rule |
| --- | --- |
| Paged list or stream? | Paged list for UI and APIs; an `IAsyncEnumerable<T>` stream for large exports; see [Streaming](streaming.md). |
| Paging, sorting and filtering? | State the parameters, the default sort, and the maximum page size. |
| File upload? | State maximum size, file count, and accepted content types; see [Attachments](attachments.md). |
| File download? | State the content type and `Content-Disposition` (inline or attachment). |

## 10. Limits and abuse

| Question | Default / rule |
| --- | --- |
| Body, page, attachment and stream limits? | Every one has a maximum (`MaxRequestBodySizeBytes`, `MaxFileCount`, page size). |
| Rate limiting? | Decided per endpoint group in the host; see [Validation and authorization](validation-and-authorization.md). |
| Expensive queries? | State the bound: an index, a mandatory filter, a timeout, or a maximum range. |

## 11. Transport exposure and serialization

| Question | Default / rule |
| --- | --- |
| Which transports: HTTP, gRPC, MCP, Azure Functions, MessagePack, messaging? | Only those with a named consumer. Add metadata only for them. |
| JSON | Source-generated `JsonSerializerContext` registration; see [Serialization](serialization.md). |
| gRPC | Stable `[ProtoMember]` numbers, never reused; see [gRPC](grpc.md). |
| MCP | Tool description written for an agent; state whether the operation is destructive; see [MCP](mcp.md). |
| Azure Functions | Supported shapes only; see [Azure Functions](azure-functions.md). |

## 12. Authorization

| Question | Default / rule |
| --- | --- |
| Operation permission? | An `Ark.Tools.Authorization` policy or scope on the contract, shared by every transport. |
| Can this caller act on this instance (owner, tenant, assignment)? | State where it is enforced (policy or handler). |
| Denied on a specific instance: 403 or 404? | Decide per contract. Prefer 403, unless 403 discloses that the resource exists: unguessable identifiers (GUIDs) return 404; guessable sequential identifiers (integers) can return 403. Record the identifier type and the reason. |
| Does a list return only rows the caller may see? | State the filter. |
| Are some fields visible only to some callers? | Use a separate output model rather than nulling fields. |
| Background messages and events | State the identity they run under and whether permission is checked again on consume. |

## 13. Messaging

| Question | Default / rule |
| --- | --- |
| Event: who publishes, who subscribes? | Name every subscriber and what happens when one is unavailable. |
| Message: who sends, who consumes, which queue or participant? | Named participant; see [Rebus](rebus.md) and [Messaging processing](messaging-processing.md). |
| Logical names | Stable; keep `FormerNames` when renaming. |
| Retry and dead-letter | State the retry policy and who handles dead-lettered messages. |
| Outbox | A handler that changes state and sends or publishes enlists the outbox. |
| Time to live | Every queue, dead-letter queue and outbox has an intrinsic TTL; state it. |
| Audience | Cross-application or internal to the application? This decides what personal data it may carry ([§15](#15-privacy-gdpr)). |

## 14. Versioning and compatibility

See [Versioning](versioning.md).

| Question | Default / rule |
| --- | --- |
| Is the change additive? | Prefer an additive path: an optional field with a default, or a new operation. |
| Breaking? | A new `[Versioning]` contract only with a written reason the additive path is impossible; keep the old version during migration and test both. |
| Enums | `EvolvableEnum<T>` with `NOT_SET = 0` on every public contract. Exceptions, named in the brief: an enum controlled externally (for example a third-party interface adapter) and `[Flags]` enums. |
| Wire identifiers | Released protobuf numbers and logical names stay reserved. |
| Review | API-surface snapshot, OpenAPI, and `.proto` diffs; see [API-surface snapshots](api-surface-snapshots.md). |

## 15. Privacy (GDPR)

The legal items below are decisions for the product owner and the data
protection officer, not for the developer or an agent. Record who decided, and
verify the conclusions with your DPO or legal team before relying on them.

| Question | Default / rule |
| --- | --- |
| Which fields are personal data, and of which class? | Fill the §2 Personal-data class column: personal, sensitive (special category, GDPR Art. 9 and 10), pseudonymous, credentials, or not personal. Free-text fields that may contain personal data are personal. |
| Purpose | `ASK` |
| Lawful basis | `ASK` |
| Retention period | `ASK` |
| Is a DPIA needed? | `ASK` |
| Minimisation | Each personal field is needed for the stated purpose. |
| Exposure | List the outputs, transports, logs, telemetry and third parties it reaches. Logs and telemetry carry no personal data. |
| Messages and events | Cross-application events and messages carry identifiers only, no personal data. Internal messages (same application, owned queue) may carry classified personal data. Sensitive and special-category data never travel in a message. |
| Storage | Platform encryption at rest is the baseline. State whether per-message or per-column encryption is needed on top. Every queue, dead-letter queue and outbox has a TTL ([§13](#13-messaging)). |
| Erasure | How the data is erased or anonymised, including temporal history tables, audit records, and message stores. |
| Access and export | How the data is found and exported for a data subject. |

### With Ark.Tools.Compliance

See the [Compliance guide](../compliance/README.md).

| Answer | Implementation |
| --- | --- |
| Personal | `[PersonalData]` or a sensitive value object such as `EmailAddress` |
| Sensitive / special category | `[SensitivePersonalData]` |
| Pseudonymous | `[Pseudonymous]` |
| Credentials | `[UserCredentials]` |
| Not personal despite the name | `[NotPersonalData("reason")]` |
| Kept out of logs, exceptions, telemetry | `ARKPII*` analyzers clean, no suppressions without review |
| Inventory | review the `ArkComplianceSurface.txt` diff |
| Storage masking | `[SqlDataPolicy]` / `[SqlColumnPolicy]` |
| OpenAPI | `AddArkComplianceSchemas()` |

### Without Ark.Tools.Compliance

Record the same answers in the brief, and check by hand that the classified
fields do not reach log templates, exception messages, telemetry tags, or
`ToString()` output, and that storage masking is configured in the database.

## 16. Audit and observability

| Question | Default / rule |
| --- | --- |
| Must changes be audited (who, when, what)? | State it; audited operations persist an audit record. |
| Telemetry and log tags | Identifiers and outcomes only, never personal data. |
| Metrics | State any business metric the operation must emit. |

## 17. Documentation

| Question | Default / rule |
| --- | --- |
| Contract and every property | XML documentation; it feeds OpenAPI, MCP tool descriptions and gRPC comments; see [OpenAPI](openapi.md). |
| Decisions code cannot show | `<remarks>`: why a field is create-only, what `null` means, which named violations the operation raises. |

## 18. Tests

See [Testing](testing.md).

| Question | Default / rule |
| --- | --- |
| Application behaviour | Direct dispatch tests for the handler, validation, authorization, business rules and effects. |
| Transport boundary | HTTP route, status, binding and authorization; gRPC status and schema; OpenAPI. |
| Messaging | Routing, retries, outbox delivery, subscribers and eventual effects. |
| Compatibility | Old and new clients when a version is added. |

## Brief template

Paste this into the pull request description and fill it in. Tag each answer
`derived (<source>)` or `ASK` until it is confirmed.

```markdown
## Contract brief: <ContractName>

### 1. Operation
### 2. Fields and updatability
| Field | Type | Updatability | Required | Null meaning | Default | Concurrency | Validation | Personal-data class |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
### 3. Types and representation
### 4. Validation
### 5. Business rules
| Rule | State needed | Named violation |
| --- | --- | --- |
### 6. Failure contract
### 7. Concurrency and idempotency
### 8. API design
### 9. Lists, streaming and files
### 10. Limits and abuse
### 11. Transport exposure and serialization
### 12. Authorization
### 13. Messaging
### 14. Versioning and compatibility
### 15. Privacy (GDPR)
- Purpose / lawful basis / retention / DPIA: decided by <name>, verified with DPO: <yes/no>
### 16. Audit and observability
### 17. Documentation
### 18. Tests

## Open questions
**Q1 (§n <section>): <question>**
- A) <alternative>: <consequence>
- B) <alternative>: <consequence>
- Recommendation: <A/B>, because <reason>
```
