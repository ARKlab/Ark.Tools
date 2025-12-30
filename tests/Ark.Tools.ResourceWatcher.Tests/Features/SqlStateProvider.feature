@sqlstateprovider @integration
Feature: SqlStateProvider Integration Tests
    As a developer using ResourceWatcher with SQL Server
    I want to verify the SqlStateProvider works correctly with a real database
    So that I can trust state persistence across worker runs

    Background:
        Given a SQL Server database is available
        And the SqlStateProvider is configured

    # ===== TABLE CREATION =====
    @setup
    Scenario: EnsureTableAreCreated creates required database objects
        When I call EnsureTableAreCreated
        Then the State table should exist
        And the udt_State_v2 type should exist
        And the udt_ResourceIdList type should exist

    # ===== SAVE AND LOAD STATE =====
    @crud
    Scenario: Save and load a single resource state
        Given a new resource state for tenant "test-tenant" and resource "res-001"
            | Field        | Value                  |
            | Modified     | 2024-06-15T10:30:00    |
            | RetryCount   | 0                      |
            | CheckSum     | abc123                 |
        When I save the resource state
        And I load state for tenant "test-tenant"
        Then the loaded state should contain resource "res-001"
        And resource "res-001" should have Modified "2024-06-15T10:30:00"
        And resource "res-001" should have CheckSum "abc123"
        And resource "res-001" should have RetryCount 0

    @crud
    Scenario: Save and load multiple resource states
        Given a new resource state for tenant "test-tenant" and resource "res-A"
            | Field        | Value                  |
            | Modified     | 2024-06-15T10:00:00    |
            | CheckSum     | checksum-A             |
        And a new resource state for tenant "test-tenant" and resource "res-B"
            | Field        | Value                  |
            | Modified     | 2024-06-15T11:00:00    |
            | CheckSum     | checksum-B             |
        And a new resource state for tenant "test-tenant" and resource "res-C"
            | Field        | Value                  |
            | Modified     | 2024-06-15T12:00:00    |
            | CheckSum     | checksum-C             |
        When I save all resource states
        And I load state for tenant "test-tenant"
        Then the loaded state should contain 3 resources
        And resource "res-A" should have CheckSum "checksum-A"
        And resource "res-B" should have CheckSum "checksum-B"
        And resource "res-C" should have CheckSum "checksum-C"

    @crud
    Scenario: Load specific resources by ID
        Given a new resource state for tenant "test-tenant" and resource "res-1"
            | Field    | Value               |
            | Modified | 2024-06-15T10:00:00 |
        And a new resource state for tenant "test-tenant" and resource "res-2"
            | Field    | Value               |
            | Modified | 2024-06-15T11:00:00 |
        And a new resource state for tenant "test-tenant" and resource "res-3"
            | Field    | Value               |
            | Modified | 2024-06-15T12:00:00 |
        When I save all resource states
        And I load state for tenant "test-tenant" with resource IDs "res-1,res-3"
        Then the loaded state should contain 2 resources
        And the loaded state should contain resource "res-1"
        And the loaded state should contain resource "res-3"
        And the loaded state should not contain resource "res-2"

    # ===== UPDATE STATE =====
    @update
    Scenario: Update existing resource state
        Given a new resource state for tenant "test-tenant" and resource "res-upd"
            | Field        | Value               |
            | Modified     | 2024-06-15T10:00:00 |
            | RetryCount   | 0                   |
            | CheckSum     | original-checksum   |
        When I save the resource state
        And I update resource "res-upd" with
            | Field        | Value               |
            | Modified     | 2024-06-15T14:00:00 |
            | RetryCount   | 2                   |
            | CheckSum     | updated-checksum    |
        And I save the resource state
        And I load state for tenant "test-tenant"
        Then resource "res-upd" should have Modified "2024-06-15T14:00:00"
        And resource "res-upd" should have RetryCount 2
        And resource "res-upd" should have CheckSum "updated-checksum"

    # ===== TENANT ISOLATION =====
    @isolation
    Scenario: States are isolated by tenant
        Given a new resource state for tenant "tenant-A" and resource "shared-res"
            | Field    | Value               |
            | Modified | 2024-06-15T10:00:00 |
            | CheckSum | tenant-a-checksum   |
        And a new resource state for tenant "tenant-B" and resource "shared-res"
            | Field    | Value               |
            | Modified | 2024-06-15T11:00:00 |
            | CheckSum | tenant-b-checksum   |
        When I save all resource states
        And I load state for tenant "tenant-A"
        Then the loaded state should contain 1 resources
        And resource "shared-res" should have CheckSum "tenant-a-checksum"
        When I load state for tenant "tenant-B"
        Then the loaded state should contain 1 resources
        And resource "shared-res" should have CheckSum "tenant-b-checksum"

    # ===== MODIFIED SOURCES =====
    @modified-sources
    Scenario: Save and load state with ModifiedSources
        Given a new resource state for tenant "test-tenant" and resource "multi-source"
            | Field    | Value               |
            | Modified |                     |
        And the resource has ModifiedSource "source-api" at "2024-06-15T09:00:00"
        And the resource has ModifiedSource "source-file" at "2024-06-15T10:00:00"
        When I save the resource state
        And I load state for tenant "test-tenant"
        Then resource "multi-source" should have ModifiedSource "source-api" at "2024-06-15T09:00:00"
        And resource "multi-source" should have ModifiedSource "source-file" at "2024-06-15T10:00:00"

    # ===== EXTENSIONS =====
    @extensions
    Scenario: Save and load state with Extensions
        Given a new resource state for tenant "test-tenant" and resource "res-ext"
            | Field        | Value               |
            | Modified     | 2024-06-15T10:00:00 |
        And the resource has extension "lastOffset" with value "12345"
        And the resource has extension "cursor" with value "abc-cursor"
        When I save the resource state
        And I load state for tenant "test-tenant"
        Then resource "res-ext" should have extension "lastOffset" with value "12345"
        And resource "res-ext" should have extension "cursor" with value "abc-cursor"

    # ===== RETRY AND BAN STATE =====
    @retry
    Scenario: Save and load retry state with exception
        Given a new resource state for tenant "test-tenant" and resource "res-fail"
            | Field        | Value                  |
            | Modified     | 2024-06-15T10:00:00    |
            | RetryCount   | 3                      |
        And the resource has last exception "Test error message"
        When I save the resource state
        And I load state for tenant "test-tenant"
        Then resource "res-fail" should have RetryCount 3

    # ===== EMPTY RESULTS =====
    @edge-case
    Scenario: Load state for non-existent tenant returns empty
        When I load state for tenant "non-existent-tenant"
        Then the loaded state should be empty

    @edge-case
    Scenario: Load state with empty resource IDs returns empty
        Given a new resource state for tenant "test-tenant" and resource "res-001"
            | Field    | Value               |
            | Modified | 2024-06-15T10:00:00 |
        When I save the resource state
        And I load state for tenant "test-tenant" with empty resource IDs
        Then the loaded state should be empty

    # ===== LARGE BATCH (2000+ resources) =====
    @performance
    Scenario: Load state with more than 2000 resource IDs
        Given 2500 resource states for tenant "batch-tenant"
        When I save all resource states
        And I load state for tenant "batch-tenant" with all 2500 resource IDs
        Then the loaded state should contain 2500 resources
