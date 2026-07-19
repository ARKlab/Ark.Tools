Feature: Greetings
    The sample application exposes greeting workflows through its public transports.

    Scenario: Create and query a greeting over HTTP
        Given I am an authenticated user
        When I create the greeting "HTTP greeting" over HTTP
        Then the greeting is available over HTTP

    Scenario: Anonymous HTTP requests are rejected
        Given I am an anonymous user
        When I create the greeting "Anonymous greeting" over HTTP
        Then the request is unauthorized

    Scenario: Create and query a greeting over gRPC
        Given I am an authenticated user
        When I create the greeting "gRPC greeting" over gRPC
        Then the greeting is available over gRPC

    Scenario: Duplicate greetings violate the business rule
        Given I am an authenticated user
        And I create the greeting "Duplicate greeting" over HTTP
        When I create the greeting "Duplicate greeting" over HTTP
        Then the request returns a business rule violation

    Scenario: Invalid greeting is rejected over HTTP
        Given I am an authenticated user
        When I create the greeting "" over HTTP
        Then the request returns validation errors

    Scenario: Invalid greeting is rejected over gRPC
        Given I am an authenticated user
        When I create the greeting "" over gRPC
        Then the gRPC request is invalid

    Scenario: Version two exposes the evolved greeting contract
        Given I am an authenticated user
        And I create the greeting "Versioned greeting" over HTTP
        When I query the greeting through version two
        Then the version two greeting includes its message length

    Scenario: HTTP composition completes asynchronously through Rebus
        Given I am an authenticated user
        When I compose the greeting "Asynchronous greeting" over HTTP
        Then the composed greeting is eventually available over HTTP

    Scenario: Creating a greeting writes a queryable audit record
        Given I am an authenticated user
        When I create the greeting "Audited greeting" over HTTP
        Then the audit query contains a CreateGreetingRequest record for "test-user"
