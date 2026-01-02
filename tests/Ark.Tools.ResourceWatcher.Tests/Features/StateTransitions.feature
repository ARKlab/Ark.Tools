@resourcewatcher
Feature: Resource Watcher State Transitions
    As a developer using ResourceWatcher
    I want to understand the state machine transitions
    So that I can correctly implement and test my resource processors

    Background:
        Given a ResourceWatcher with in-memory state provider
        And the worker is configured with MaxRetries of 3
        And the worker is configured with a BanDuration of 24 hours

    # ===== NEW STATE =====
    @new-state
    Scenario: New resource is processed and stored
        Given a resource "resource-001" with Modified "2024-01-15T10:00:00"
        And the resource has not been seen before
        When the ResourceWatcher runs
        Then the resource "resource-001" should be processed as "New"
        And the result type should be "Normal"
        And the state for "resource-001" should have RetryCount of 0

    @new-state
    Scenario: New resource with ModifiedSources triggers processing
        Given a resource "resource-002" with Modified "2024-01-15T10:00:00"
        And the resource has ModifiedSource "source-a" at "2024-01-15T09:00:00"
        And the resource has ModifiedSource "source-b" at "2024-01-15T09:30:00"
        And the resource has not been seen before
        When the ResourceWatcher runs
        Then the resource "resource-002" should be processed as "New"
        And the state should track both modified sources

    # ===== UPDATED STATE =====
    @updated-state
    Scenario: Resource with newer Modified timestamp is processed as Updated
        Given a resource "resource-003" with Modified "2024-01-15T10:00:00"
        And the resource was previously processed with Modified "2024-01-14T10:00:00"
        When the ResourceWatcher runs
        Then the resource "resource-003" should be processed as "Updated"
        And the result type should be "Normal"

    # ===== NOTHING TO DO STATE =====
    @nothing-to-do
    Scenario: Resource with same Modified timestamp is skipped
        Given a resource "resource-005" with Modified "2024-01-15T10:00:00"
        And the resource was previously processed with Modified "2024-01-15T10:00:00"
        When the ResourceWatcher runs
        Then the diagnostic should show "NothingToDo" for "resource-005"

    # ===== RETRY STATE =====
    @retry-state
    Scenario: Failed resource processing triggers retry
        Given a resource "resource-007" with Modified "2024-01-15T10:00:00"
        And the resource has not been seen before
        And the processor is configured to fail for "resource-007"
        When the ResourceWatcher runs
        Then the resource "resource-007" should be processed as "New"
        And the result type should be "Error"
        And the state for "resource-007" should have RetryCount of 1

    # ===== BAN STATE =====
    @ban-state
    Scenario: Resource exceeding max retries is banned
        Given a resource "resource-010" with Modified "2024-01-15T10:00:00"
        And the resource was previously processed with Modified "2024-01-15T10:00:00"
        And the previous state has RetryCount of 3
        And the processor is configured to fail for "resource-010"
        When the ResourceWatcher runs
        Then the resource "resource-010" should be processed as "Retry"
        And the result type should be "Error"
        And the state for "resource-010" should be banned

    @ban-state
    Scenario: Banned resource is skipped during ban period
        Given a resource "resource-011" with Modified "2024-01-15T12:00:00"
        And the resource was previously processed with Modified "2024-01-15T10:00:00"
        And the previous state has RetryCount of 4 and LastEvent now
        When the ResourceWatcher runs
        Then the diagnostic should show "Banned" for "resource-011"

    # ===== RETRY SUCCESS RESETS STATE =====
    @retry-state
    Scenario: Successful retry resets RetryCount to zero
        Given a resource "resource-012" with Modified "2024-01-15T10:00:00"
        And the resource was previously processed with Modified "2024-01-15T10:00:00"
        And the previous state has RetryCount of 2
        When the ResourceWatcher runs
        Then the resource "resource-012" should be processed as "Retry"
        And the result type should be "Normal"
        And the state for "resource-012" should have RetryCount of 0

    # ===== PARALLEL PROCESSING =====
    @parallel-processing
    Scenario: Multiple new resources are processed in parallel
        Given the worker is configured with DegreeOfParallelism of 4
        And a resource "resource-A" with Modified "2024-01-15T10:00:00"
        And "resource-A" has not been seen before
        And a resource "resource-B" with Modified "2024-01-15T10:00:00"
        And "resource-B" has not been seen before
        And a resource "resource-C" with Modified "2024-01-15T10:00:00"
        And "resource-C" has not been seen before
        When the ResourceWatcher runs
        Then all 3 resources should be processed as "New"
        And all results should be "Normal"

    # ===== CHECKSUM STORAGE =====
    @checksum-tracking
    Scenario: Resource checksum is stored in state after processing
        Given a resource "resource-013" with Modified "2024-01-15T10:00:00" and checksum "unique-checksum-123"
        And the resource has not been seen before
        When the ResourceWatcher runs
        Then the resource "resource-013" should be processed as "New"
        And the state for "resource-013" should have checksum "unique-checksum-123"
