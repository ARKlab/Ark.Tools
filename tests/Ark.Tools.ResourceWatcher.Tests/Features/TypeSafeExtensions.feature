@typesafe-extensions @integration
Feature: Type-Safe Extensions Support
    As a developer using ResourceWatcher
    I want to use strongly-typed extensions with compile-time safety
    So that I can avoid runtime type errors and get better IntelliSense support

    Background:
        Given a SQL Server database is available for type-safe extensions
        And the database schema is prepared

    # ===== VOIDEXTENSIONS TESTS =====
    @voidextensions
    Scenario: VoidExtensions serializes as null in database
        Given a SqlStateProvider configured for VoidExtensions
        And a resource state with VoidExtensions for tenant "void-tenant" and resource "void-res-001"
            | Modified            | RetryCount | CheckSum |
            | 2024-06-15T10:30:00 | 0          | abc123   |
        When I save the VoidExtensions resource state
        And I load VoidExtensions state for tenant "void-tenant"
        Then the VoidExtensions loaded state should contain resource "void-res-001"
        And the Extensions column in database should be null for "void-res-001"

    @voidextensions
    Scenario: Multiple VoidExtensions states can be saved and loaded
        Given a SqlStateProvider configured for VoidExtensions
        And a resource state with VoidExtensions for tenant "void-tenant" and resource "void-A"
            | Modified            | CheckSum   |
            | 2024-06-15T10:00:00 | checksum-A |
        And a resource state with VoidExtensions for tenant "void-tenant" and resource "void-B"
            | Modified            | CheckSum   |
            | 2024-06-15T11:00:00 | checksum-B |
        When I save all VoidExtensions resource states
        And I load VoidExtensions state for tenant "void-tenant"
        Then the VoidExtensions loaded state should contain 2 resources
        And all VoidExtensions resources should have null Extensions

    # ===== TYPED EXTENSIONS TESTS =====
    @typed-extensions
    Scenario: Typed extensions serialize and deserialize correctly
        Given a SqlStateProvider configured for typed extensions
        And a resource state with typed extensions for tenant "typed-tenant" and resource "typed-res-001"
            | Modified            | RetryCount | CheckSum |
            | 2024-06-15T10:30:00 | 0          | abc123   |
        And the resource has typed extension with LastOffset 12345 and ETag "etag-v1"
        When I save the typed extensions resource state
        And I load typed extensions state for tenant "typed-tenant"
        Then the typed extensions loaded state should contain resource "typed-res-001"
        And resource "typed-res-001" should have typed extension LastOffset 12345
        And resource "typed-res-001" should have typed extension ETag "etag-v1"

    @typed-extensions
    Scenario: Typed extensions with complex nested data
        Given a SqlStateProvider configured for typed extensions
        And a resource state with typed extensions for tenant "typed-tenant" and resource "nested-001"
            | Modified            |
            | 2024-06-15T10:00:00 |
        And the resource has typed extension with LastOffset 999 and Counter 42
        And the resource has typed extension metadata "key1" with value "value1"
        And the resource has typed extension metadata "key2" with value "value2"
        When I save the typed extensions resource state
        And I load typed extensions state for tenant "typed-tenant"
        Then resource "nested-001" should have typed extension LastOffset 999
        And resource "nested-001" should have typed extension Counter 42
        And resource "nested-001" should have typed extension metadata "key1" with value "value1"
        And resource "nested-001" should have typed extension metadata "key2" with value "value2"

    @typed-extensions
    Scenario: Update typed extensions preserves type information
        Given a SqlStateProvider configured for typed extensions
        And a resource state with typed extensions for tenant "typed-tenant" and resource "update-001"
            | Modified            |
            | 2024-06-15T10:00:00 |
        And the resource has typed extension with LastOffset 100 and ETag "v1"
        When I save the typed extensions resource state
        And I update typed extensions for resource "update-001" with LastOffset 200 and ETag "v2"
        And I save the typed extensions resource state
        And I load typed extensions state for tenant "typed-tenant"
        Then resource "update-001" should have typed extension LastOffset 200
        And resource "update-001" should have typed extension ETag "v2"

    # ===== TYPE SAFETY TESTS =====
    @type-safety
    Scenario: SqlStateProvider with different extension types are type-safe
        Given a SqlStateProvider configured for VoidExtensions as "voidProvider"
        And a SqlStateProvider configured for typed extensions as "typedProvider"
        And a resource state with VoidExtensions for tenant "type-test" and resource "void-res"
            | Modified            |
            | 2024-06-15T10:00:00 |
        And a resource state with typed extensions for tenant "type-test" and resource "typed-res"
            | Modified            |
            | 2024-06-15T10:00:00 |
        And the resource "typed-res" has typed extension with LastOffset 777
        When I save the VoidExtensions resource state with provider "voidProvider"
        And I save the typed extensions resource state with provider "typedProvider"
        Then loading with "voidProvider" for tenant "type-test" and resource "void-res" returns VoidExtensions
        And loading with "typedProvider" for tenant "type-test" and resource "typed-res" returns typed extensions
        And the typed resource should have LastOffset 777

    # ===== JSON SERIALIZATION ROUND-TRIP =====
    @serialization
    Scenario: Typed extensions survive serialization round-trip
        Given a SqlStateProvider configured for typed extensions
        And a resource state with typed extensions for tenant "serial-tenant" and resource "serial-001"
            | Modified            | RetryCount | CheckSum |
            | 2024-06-15T10:30:00 | 2          | check999 |
        And the resource has typed extension with LastOffset 54321
        And the resource has typed extension with ETag "complex-etag-@#$%"
        And the resource has typed extension with Counter 99
        And the resource has typed extension metadata "special" with value "chars: @#$%^&*()"
        When I save the typed extensions resource state
        And I load typed extensions state for tenant "serial-tenant"
        Then resource "serial-001" should have Modified "2024-06-15T10:30:00"
        And resource "serial-001" should have RetryCount 2
        And resource "serial-001" should have CheckSum "check999"
        And resource "serial-001" should have typed extension LastOffset 54321
        And resource "serial-001" should have typed extension ETag "complex-etag-@#$%"
        And resource "serial-001" should have typed extension Counter 99
        And resource "serial-001" should have typed extension metadata "special" with value "chars: @#$%^&*()"

    # ===== NULL EXTENSIONS HANDLING =====
    @null-handling
    Scenario: Typed extensions can be null
        Given a SqlStateProvider configured for typed extensions
        And a resource state with typed extensions for tenant "null-tenant" and resource "null-ext-001"
            | Modified            |
            | 2024-06-15T10:00:00 |
        And the resource has null typed extensions
        When I save the typed extensions resource state
        And I load typed extensions state for tenant "null-tenant"
        Then resource "null-ext-001" should have null typed extensions

    @null-handling
    Scenario: VoidExtensions is always treated as null
        Given a SqlStateProvider configured for VoidExtensions
        And a resource state with VoidExtensions for tenant "void-tenant" and resource "always-null"
            | Modified            |
            | 2024-06-15T10:00:00 |
        When I save the VoidExtensions resource state
        And I load VoidExtensions state for tenant "void-tenant"
        Then the Extensions column in database should be null for "always-null"
        And resource "always-null" VoidExtensions should be default value
