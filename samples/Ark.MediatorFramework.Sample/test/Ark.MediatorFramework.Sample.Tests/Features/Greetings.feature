Feature: Greetings
    The sample application exposes greeting workflows through its public transports.

    Scenario: Create and query a greeting over HTTP
        When I create the greeting "HTTP greeting" over HTTP
        Then the greeting is available over HTTP

    Scenario: Create a greeting over gRPC and query it over HTTP
        When I create the greeting "gRPC greeting" over gRPC
        Then the greeting is available over HTTP

    Scenario: Duplicate greetings violate the business rule
        Given I create the greeting "Duplicate greeting" over HTTP
        When I create the greeting "Duplicate greeting" over HTTP
        Then the request returns a business rule violation

    Scenario: Version two exposes the evolved greeting contract
        Given I create the greeting "Versioned greeting" over HTTP
        When I query the greeting through version two
        Then the version two greeting includes its message length

    Scenario: HTTP composition completes asynchronously through Rebus
        When I compose the greeting "Asynchronous greeting" over HTTP
        Then the composed greeting is eventually available over HTTP
