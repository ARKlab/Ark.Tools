Feature: Greetings
    The sample application exposes transport-independent greeting contracts.

    Scenario: Create and query a greeting
        Given I am an authenticated user
        When I create the greeting "Application greeting"
        And I query the greeting
        Then the greeting can be queried

    Scenario: Anonymous requests are rejected
        Given I am an anonymous user
        When I create the greeting "Anonymous greeting"
        Then the request fails with an authorization exception

    Scenario: Duplicate greetings violate the business rule
        Given I am an authenticated user
        And I create the greeting "Duplicate greeting"
        When I create the greeting "Duplicate greeting"
        Then the request fails with a greeting already exists violation for "Duplicate greeting"

    Scenario: Invalid greetings are rejected
        Given I am an authenticated user
        When I create the greeting ""
        Then the request fails validation

    Scenario: Version two exposes the evolved greeting contract
        Given I am an authenticated user
        And I create the greeting "Versioned greeting"
        When I query the greeting through version two
        Then the version two greeting includes its message length

    Scenario: A stream observes cancellation without a transport
        Given I am an authenticated user
        When I consume a greeting stream and cancel after two items
        Then the stream yields two items before cancellation

    Scenario: Creating a greeting writes a queryable audit record
        Given I am an authenticated user
        When I create the greeting "Audited greeting"
        Then the audit query contains a CreateGreetingRequest operation for "test-user"
