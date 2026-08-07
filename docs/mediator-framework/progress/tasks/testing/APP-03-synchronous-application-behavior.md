# APP-03 — Cover synchronous application behavior

**Depends on:** APP-02
**Scope:** Sample application Reqnroll features and focused tests

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

Create or rewrite features so every application handler registered by
`ApplicationComposition.Register` has a contract-level behavior or an explicit
documented reason for exclusion. The initial coverage matrix is:

| Application behavior | Contract(s) to dispatch | Assertions |
| --- | --- | --- |
| Create and read | `CreateGreetingRequest`, `GetGreetingQuery` | Returned identity/message and query result |
| Update and missing entity | `UpdateGreetingMessageRequest`, `UpdateGreetingRequest`, `GetGreetingQuery` | Updated state; typed not-found exception |
| Versioned application result | `GetGreetingV2Query` | V2 response-only fields, not a v2 URL |
| FluentValidation | All validator-backed requests/queries | `ValidationException` field failures |
| Business rule | Duplicate/create violation path | `BusinessRuleViolationException` and violation payload |
| Paging/search | `SearchGreetingsQuery`, `GetAuditsQuery` | Valid pages, total counts, stable ordering, invalid query exceptions |
| Auditing | Create/update/query through the decorated handler | User, operation, entity, identifier, deterministic timestamp |
| Authorization | Policy-decorated commands/requests | Allowed principal succeeds; missing claim throws the authorization exception |
| Polymorphic behavior | `DescribeShapeRequest` and shape contracts | Correct subtype/business result; no wire round-trip assertion |
| Attachments | Upload requests and `GetDocumentQuery` | Byte content, metadata, count/size validation, missing document exception |
| Streaming | `GetGreetingsStreamQuery` | Item order/count, empty result, producer observes cancellation |
| Inline command/notification | `RefreshGreetingCommand`, `GreetingCreatedNotification` | Command effect and notification side effect |
| Failure/dead-letter behavior | `FailingRebusRequest` where application-owned | Typed failure, second-level `IFailed<T>` handling, and error-queue outcome when the failed handler also fails |

Use the real application decorators and public contracts. Arrange state only
through earlier contract dispatches or the documented test adapter; do not call
`SampleDataContext` or `IGreetingStore` from a step.

## Outcome

- All application code paths, including exceptional paths, have readable
  contract-level scenarios.

## Acceptance

- [ ] The coverage table maps every current application handler to a scenario or
  an explicit follow-up task.
- [ ] Business violations, validation failures, not-found, authorization, and
  cancellation are tested by throws/observations rather than transport errors.
- [ ] Paging, SQL-independent business rules, attachment behavior, streaming,
  and auditing are covered without serialization.
- [ ] No scenario relies on `DateTime.UtcNow`, random sleeps, or shared mutable
  state.

## Tests

- Run each Reqnroll feature independently and as a complete suite.
- Run focused MSTest tests for cancellation and the concurrency-fault test
  decorator.
- Required scenarios/cases:
  - create/read, update/not-found, versioned result, validation, business
    violation, paging/search, auditing, and authorization;
  - polymorphic results, attachments, streaming/empty/cancellation, and
    command/notification side effects;
  - all assertions use contract results, typed exceptions, or observed state,
    never transport status or serialization.
- Run the full-solution gates.
