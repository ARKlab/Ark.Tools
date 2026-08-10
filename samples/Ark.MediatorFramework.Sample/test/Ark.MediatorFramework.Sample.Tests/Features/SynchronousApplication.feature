Feature: Synchronous application contracts
    The application behavior suite dispatches contracts without a transport host.

    Scenario: A missing greeting returns a typed not-found exception
        Given I am an authenticated user
        When I query a missing greeting
        Then the request fails with a missing entity exception

    Scenario: Envelope binding remains an application contract
        Given I am an authenticated user
        When I dispatch an envelope update contract
        Then the envelope response contains the composed values

    Scenario: Invalid paging reports every field failure
        Given I am an authenticated user
        When I search greetings with invalid paging
        Then the greeting search fails validation for skip and limit

    Scenario: Polymorphic application behavior returns the concrete shape
        Given I am an authenticated user
        When I describe a circle with radius 2
        Then the shape description is a circle with area 12.566370614359172

    Scenario: The synchronous refresh command is dispatchable
        Given I am an authenticated user
        When I dispatch the refresh greeting command
        Then the refresh greeting command completes
