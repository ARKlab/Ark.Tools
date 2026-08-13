Feature: Synchronous application contracts
    The application behavior suite dispatches contracts without a transport host.

    Scenario: Polymorphic application behavior returns the concrete shape
        Given I am an authenticated user
        When I describe a circle with radius 2
        Then the shape description is a circle with area 12.566370614359172
