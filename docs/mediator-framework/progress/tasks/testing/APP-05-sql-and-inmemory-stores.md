# APP-05 — Run the application suite against SQL and in-memory stores

**Depends on:** APP-03, APP-04
**Scope:** Sample test hooks, Docker documentation, persistence behavior

Use the [execution rules](../../mediator-testing-plan.md#5-execution-rules-for-every-task)
for every implementation task.

## Implementation details

1. Make SQL the default profile in the direct test context and preserve the
   `ARK_SAMPLE_SQL_CONNECTION` override. Add
   `ARK_SAMPLE_INMEMORY_TESTS=1` as the explicit alternate-profile switch.
2. Deploy the existing DACPAC once before the default suite using
   `Microsoft.SqlServer.Dac`; reset the database before every SQL scenario with
   `[ops].[ResetFull_OnlyForTesting]`.
3. Keep the reset procedure's FK-safe order: use `DELETE FROM` for
   FK-constrained application tables and truncate only independent history
   tables.
4. Run the persistence-sensitive contract scenarios against both
   `InMemoryGreetingStore` and `SqlGreetingStore`: create/read/update,
   paging/search, audits, opaque row-version ETags, transactions, and SQL
   outbox effects.
5. Keep Rebus in-memory in both the SQL default and in-memory store profiles.
   If an application storage abstraction is added for Azure Blob, add a
   separate Azurite-tagged profile and run the same attachment contracts
   against it; otherwise document that the sample uses `DocumentStore` in
   memory and does not need Azurite.
6. Serialize the SQL profile if shared database state requires it; keep the
   scenario-owned in-memory demonstration parallel where the Rebus test
   utilities permit it.
7. Update
   `samples/Ark.MediatorFramework.Sample/README.md` and its Docker instructions
   with the exact profile-selection commands and cleanup behavior.

## Outcome

- The sample proves application persistence against the real local SQL
  implementation while retaining a fast explicit alternate test run.

## Acceptance

- [x] Default tests deploy and reset the DACPAC and pass when SQL Server is
  available.
- [x] The explicit in-memory profile passes without Docker or SQL Server.
- [ ] Dapper query paths, transaction/outbox paths, audit persistence, paging,
  and row-version-to-opaque-ETag conversion have direct assertions.
- [ ] SQL cleanup is FK-safe and leaves no scenario data.
- [x] Profile documentation never embeds credentials or tokens.

## Tests

- Start `samples/Ark.MediatorFramework.Sample/docker-compose.yml` and run the
  default sample test project against SQL Server.
- Set `ARK_SAMPLE_INMEMORY_TESTS=1` and run the same sample test project without
  Docker or SQL Server.
- Required scenarios/cases:
  - create/read/update, paging/search, audits, opaque ETags, transactions, and
    outbox behavior against SQL;
  - the same persistence-sensitive contract cases against the in-memory stores;
  - SQL reset after a failed scenario and no state leakage into the next case.
- Run the full-solution gates after stopping the container.

## Validation

- The complete sample suite passes with `ARK_SAMPLE_INMEMORY_TESTS=1`.
- The complete sample suite passes with SQL Server, using
  `ARK_SAMPLE_SQL_CONNECTION` supplied outside the repository.
