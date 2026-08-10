# APP-09 — Keep transactional outbox parity in test profiles

**Depends on:** APP-04, APP-05  
**Scope:** Outbox library, sample composition, application tests, and guidance

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Add a composable in-memory outbox context factory to
   `Ark.Tools.Outbox`, with commit, peek-lock, count, clear, and rollback-on-
   dispose behavior matching the SQL outbox seam.
2. Use that factory from the sample in-memory data context so
   `ISampleDataContext.OutboxContext` is always available.
3. Configure Rebus with the outbox in every sample profile and remove handler
   branches that send directly when SQL is disabled.
4. Keep outbox inspection and cleanup available in both test profiles.

## Acceptance

- [x] The library exposes an in-memory implementation of
  `IOutboxAsyncContextFactory` and `IOutboxContextFactory`.
- [x] In-memory contexts stage messages until commit, release peek locks on
  dispose without commit, and remove processed messages on commit.
- [x] SQL and in-memory sample handlers use the same outbox enlistment path.
- [x] Rebus outbox configuration is unconditional wherever the application
  expects transactional delivery.

## Tests

- Verify in-memory outbox commit, rollback-on-dispose, peek-lock, count, and
  clear behavior.
- Run the direct application and Rebus workflow scenarios with
  `ARK_SAMPLE_INMEMORY_TESTS=1`.
- Run the same scenarios with the SQL profile when SQL Server is available.
- Run the full-solution build and test gates.
