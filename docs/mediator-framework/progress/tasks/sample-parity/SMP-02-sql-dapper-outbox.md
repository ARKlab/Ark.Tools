# SMP-02 — SQL/Dapper persistence + transactional Outbox (G4)

**Category**: sample-parity · **Priority**: Release blocker · **Scope**: SAMPLE

## Problem

The mediator sample persists in memory
(`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.Application/SampleDataContext.cs`)
and publishes bus messages non-transactionally. The ReferenceProject demonstrates the recommended
production pattern: Dapper over SQL Server with a transactional **Outbox**.

## Reference pattern (copy this)

- Data context: `samples/Ark.ReferenceProject/Core/Ark.Reference.Core.Application/DAL/CoreDataContext_Sql.cs` — extends `AbstractSqlAsyncContextWithOutbox` (from `src/common/Ark.Tools.Outbox.SqlServer` / `Ark.Tools.Sql`).
- Database project + migrations: ReferenceProject `Database` folder/projects.
- Docker deps: `samples/Ark.ReferenceProject/docker-compose.yml` (SQL Server + Azurite).
- Outbox-published message: `CompleteGreetingCompositionRequest`-style flow — bus publish enlisted in the same SQL transaction.

## Steps

1. Add a database project for the sample (mirror the ReferenceProject database project layout) with a `Greeting` table schema and the Outbox tables (`Ark.Tools.Outbox.SqlServer` schema helpers).
2. Add/extend `docker-compose.yml` under `samples/Ark.MediatorFramework.Sample/` for SQL Server (mirror ReferenceProject's compose file, including the test-reset stored procedure pattern `[ops].[ResetFull_OnlyForTesting]` — note: use `DELETE FROM` not `TRUNCATE` for FK-referenced tables).
3. Implement `SampleDataContext : AbstractSqlAsyncContextWithOutbox` and an
   `InMemorySampleDataContextFactory` behind the same `ISampleDataContext` contract; handlers
   use the context factory registered in `ApplicationComposition.cs`/`SampleComposition.cs`.
4. Make the greeting-created bus notification go through the Outbox within the SQL transaction.
5. Test infra: wire the docker dependency in the Reqnroll test hooks (`Hooks/SampleTestContext.cs`), with per-scenario DB reset (ReferenceProject pattern).
6. Keep the in-memory implementation available behind configuration **only if trivial**; otherwise delete it — SQL becomes the sample default.

## Outcomes

- Sample demonstrates transactional persistence + outbox publishing, matching Ark production guidance, exercised by integration tests against dockerized SQL Server.

## Acceptance

- [x] CRUD scenarios pass against SQL Server via docker-compose (opt-in SQL Reqnroll path) (domain variation: sample uses `Book` entities rather than `Greeting`; `ApplicationTestContext.cs:72-79` gates SQL-vs-in-memory via `ARK_SAMPLE_INMEMORY_TESTS`).
- [x] Bus message is enlisted in the same SQL transaction as the greeting write (`NativeOutboxIntegrationTests.cs:25-51`, wiring `ApplicationComposition.cs:47-58,153`).
- [x] Test DB reset between scenarios (no cross-scenario leakage) (`Hooks/DatabaseHooks.cs:62-78` + `ops/ResetFull_OnlyForTesting.sql`).
- [x] README of the sample documents `docker compose up -d` prerequisite (`README.md:165-185`).
- [x] Lockfiles updated; full solution build + tests green.
