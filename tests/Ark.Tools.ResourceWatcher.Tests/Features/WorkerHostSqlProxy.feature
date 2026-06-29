@workerhost-sql-proxy @integration
Feature: WorkerHost SQL proxy integration
    As a developer using the non-generic WorkerHost proxy
    I want SqlStateProvider wiring to resolve correctly
    So that RunOnce does not fail during SQL state initialization

    Background:
        Given a SQL Server database is available

    Scenario: WorkerHost proxy runs with SqlStateProvider
        Given a WorkerHost proxy configured with SqlStateProvider
        When I run the WorkerHost proxy once
        Then the WorkerHost proxy run should complete without exception
