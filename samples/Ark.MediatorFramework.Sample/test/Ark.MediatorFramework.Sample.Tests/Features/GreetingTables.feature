Feature: Greeting contracts
    The application uses table-driven verbs to prepare, mutate, and compare active entities.

    Rule: Greeting state is prepared and verified through contracts

        Scenario: Create, retrieve, and audit a greeting
            Given I am an authenticated user
            And I create a greeting with
                | Name        |
                | Table hello |
            Then the current greeting is
                | Message                            |
                | Hello, Table hello! (by test-user) |
            When I retrieve the current greeting
            Then the current greeting is
                | Message                            |
                | Hello, Table hello! (by test-user) |
            And the current greeting audit is
                | UserId    | Operation            | EntityType       |
                | test-user | CreateGreetingRequest | GreetingResponse |

        Scenario: Update an active greeting
            Given I am an authenticated user
            And I create a greeting with
                | Name           |
                | Original table |
            When I update the current greeting with
                | Message               |
                | Updated table greeting |
            Then the current greeting is
                | Message               |
                | Updated table greeting |

    Rule: Search results are prepared and compared through tables

        Scenario: Search greeting pages
            Given I am an authenticated user
            And I create greetings with
                | Name          |
                | Search alpha  |
                | Search beta   |
                | Other greeting |
            When I search greetings by
                | MessageContains | Skip | Limit |
                | Search          | 0    | 25    |
            Then the greeting search has 2 results
            And the greeting search contains
                | Message                               |
                | Hello, Search alpha! (by test-user)   |
                | Hello, Search beta! (by test-user)    |
