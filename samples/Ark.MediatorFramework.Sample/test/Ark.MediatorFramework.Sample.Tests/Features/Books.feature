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
                | Title                 |
                | The Pragmatic Coder 2 |
            Then the current book is
                | Title                 | Author          | Genre      | ISBN           |
                | The Pragmatic Coder 2 | Hunt and Thomas | Technology | 978-0135957059 |
            And the current book has a refreshed opaque ETag
            And the current book has a deterministic audit for "Book_UpdateRequest.V1"
            When I retrieve the current book
            Then the current book is
                | Title                 | Author          | Genre      | ISBN           |
                | The Pragmatic Coder 2 | Hunt and Thomas | Technology | 978-0135957059 |
            When I delete the current book
            Then the current book was deleted

        Scenario: Reject an invalid book update
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I update the current book with
                | Title |
                |       |
            Then the book request fails validation

        Scenario: Reject a book mutation without its write scope
            Given I am an authenticated user without the book write scope
            When I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            Then the book request fails with an authorization exception

        Scenario: Reject a stale book ETag
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I update the current book with
                | Title |
                | Dune  |
            And I update the current book with a stale ETag and
                | Title |
                | Dune 2 |
            Then the book request fails because the book ETag is stale

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

        Scenario: Page books in a stable sort order
            Given I create books with
                | Title           | Author  | Genre      |
                | Clean Code      | Martin  | Technology |
                | Design Patterns | GoF     | Technology |
                | The Hobbit      | Tolkien | Fiction    |
            When I search books by title ascending with
                | Skip | Limit |
                | 1    | 1     |
            Then the book search has 3 results
            And the book search page has skip 1, limit 1, and 1 results

    Rule: Book changes are audited

        Scenario: Create a book audit
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            Then the current book audit is
                | UserId    | EntityType   | Operation         |
                | application-test-user | Output | Book_CreateRequest.V1 |

    Rule: Book covers retain metadata and bytes

        Scenario: Upload and download a book cover
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user with the book cover scope
            When I upload a cover for the current book with
                | Name      | ContentType | Content       |
                | cover.png | image/png   | Cover bytes!  |
            Then the book cover upload is
                | Name      | ContentType | Length |
                | cover.png | image/png   | 12     |
            When I download the cover for the current book
            Then the current book cover is
                | Name      | ContentType | Content      |
                | cover.png | image/png   | Cover bytes! |

        Scenario: Reject a book cover without its scope
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user without the book cover scope
            When I upload a cover for the current book with
                | Name      | ContentType | Content |
                | cover.png | image/png   | bytes   |
            Then the book cover request fails with an authorization exception

        Scenario: Reject an invalid book cover
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user with the book cover scope
            When I upload a cover for the current book with
                | Name      | ContentType | Content |
                | cover.txt | text/plain  | bytes   |
            Then the book cover request fails validation

        Scenario: Report a missing book cover
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user with the book cover scope
            When I download the cover for the current book
            Then the book cover request fails because the cover is missing

    Rule: Book reviews demonstrate child-resource behavior

        Scenario: Create and list book reviews
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I create a book review with
                | Rating | Text             |
                | 5      | Excellent book!  |
            Then the book review was created
            When I list book reviews with
                | Skip | Limit |
                | 0    | 10    |
            Then the book review list has 1 results

        Scenario: Reject an invalid book review
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I create a book review with
                | Rating | Text |
                | 6      | Bad  |
            Then the book request fails validation

        Scenario: Reject a book review without its write scope
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user without the book review write scope
            When I create a book review with
                | Rating | Text |
                | 5      | Good |
            Then the book request fails with an authorization exception

    Rule: Reading activity uses repository time and bounded retrieval

        Scenario: Record and retrieve reading activity
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I record reading activity with
                | Kind    | Progress |
                | Started | 0        |
            Then the reading activity was recorded at the repository time
            When I list reading activity with
                | Limit |
                | 1     |
            Then the reading activity list has at most 1 results

        Scenario: Reject invalid reading activity
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            When I record reading activity with
                | Kind     | Progress |
                | Finished | 50       |
            Then the book request fails validation

        Scenario: Reject reading activity without its write scope
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            And I am an authenticated user without the book activity write scope
            When I record reading activity with
                | Kind    | Progress |
                | Started | 0        |
            Then the book request fails with an authorization exception

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
            When I concurrently start two book print processes for the current book with
                | ShouldFail |
                | false      |
            Then the request fails because the current book is already printing

        Scenario: Resume an interrupted book print process
            Given I create a book with
                | Title | Author  | Genre   |
                | Dune  | Herbert | Fiction |
            And I have a running book print process for the current book
            When I resume the current book print process
            Then the current book print process is
                | Status    | Progress |
                | Completed | 1        |

        Scenario: Cancel a running book print process
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            And I have a running book print process for the current book
            When I cancel the current book print process
            Then the current book print process was cancelled

        Scenario: Reject cancellation of a completed book print process
            Given I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I wait for the background bus to be idle and the outbox to be empty
            And I retrieve the current book print process
            When I cancel the current book print process
            Then cancellation fails because the current book print process is terminal

        Scenario: Surface a failed external print-completion notification
            Given the print-completion notification service fails
            And I create a book with
                | Title | Author | Genre   |
                | Dune  | Herbert | Fiction |
            When I start a book print process for the current book with
                | ShouldFail |
                | false      |
            And I wait for the background bus to be idle and the outbox to be empty
            And I retrieve the current book print process
            Then the current book print process has error details
            And the print-completion notification service was called

    Rule: Book streaming and editions use transport-neutral contracts

        Scenario: Stream bounded Book items with cancellation
            Given I am an authenticated user with the book read scope
            When I consume a Book stream and cancel after two items
            Then the Book stream contains 2 items
            And the Book stream was cancelled

        Scenario: Reject a Book stream above its bound
            Given I am an authenticated user with the book read scope
            When I request a Book stream above the bound
            Then the Book stream request fails because its count is out of range

        Scenario: Describe a printed Book edition
            Given I am an authenticated user with the book read scope
            When I describe a printed Book edition
            Then the Book edition description is "Paperback print edition with 320 pages"

        Scenario: Describe a digital Book edition
            Given I am an authenticated user with the book read scope
            When I describe a digital Book edition
            Then the Book edition description is "EPUB digital edition with 1048576 bytes"
