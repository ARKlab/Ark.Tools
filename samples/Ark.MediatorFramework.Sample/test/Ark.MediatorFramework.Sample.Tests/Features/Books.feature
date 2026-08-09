Feature: Books
    The application manages books through reusable table-driven contracts.

    Rule: Valid books can be created, updated, queried, and deleted

        Scenario: Manage a book
            Given I create a book with
                | Title                    | Author          | Genre      | ISBN           |
                | The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |
            Then the current book is
                | Title                    | Author          | Genre      | ISBN           |
                | The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |
            When I update the current book with
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            Then the current book is
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            When I retrieve the current book
            Then the current book is
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            When I delete the current book

    Rule: Book searches use table-defined entities and filters

        Scenario: Filter books by author
            Given I create books with
                | Title           | Author  | Genre      | ISBN           |
                | Clean Code      | Martin  | Technology | 978-0132350884 |
                | Design Patterns | GoF     | Technology | 978-0201633610 |
                | The Hobbit      | Tolkien | Fiction    | 978-0345339683 |
            When I search books by
                | Author | Skip | Limit |
                | Martin | 0    | 25    |
            Then the book search has 1 results
            And the book search contains
                | Title      | Author | Genre      | ISBN           |
                | Clean Code | Martin | Technology | 978-0132350884 |

    Rule: Book changes are audited

        Scenario: Create a book audit
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            Then the current book audit is
                | UserId    | EntityType   | Operation         |
                | application-test-user | Output | Book_CreateRequest.V1 |

    Rule: Book printing runs asynchronously

        Scenario: Complete a book print process
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I wait for the background bus to be idle and the outbox to be empty
            And I retrieve the current book print process
            Then the current book print process is
                | Status    | Progress |
                | Completed | 1        |

        Scenario: Report a failed book print process
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | true       |
            And I wait for the background bus to be idle and the outbox to be empty
            And I retrieve the current book print process
            Then the current book print process has error details

        Scenario: Prevent concurrent book print processes
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I start a book print process for the current book with
                | ShouldFail |
                | false      |
            Then the request fails because the current book is already printing

        Scenario: Surface a failed external print-completion notification
            Given the print-completion notification service fails
            And I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            Then the error queue contains the failed message
