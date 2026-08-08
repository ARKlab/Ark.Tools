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
            When I update the book "The Pragmatic Programmer" with
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            Then the current book is
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            When I retrieve the book "The Pragmatic Coder 2"
            Then the current book is
                | Title                 | Author      | Genre   | ISBN           |
                | The Pragmatic Coder 2 | Hunt Thomas | Science | 978-0135957059 |
            When I delete the book "The Pragmatic Coder 2"

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
