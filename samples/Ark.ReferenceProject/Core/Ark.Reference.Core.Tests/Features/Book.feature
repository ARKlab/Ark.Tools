@CleanDbBeforeScenario
Feature: Book
    CRUD operations for Book management

  Rule: Book CRUD operations must succeed with valid data

    Scenario: Endpoint Book Create and Get
      When I create a single Book with
        | Title                    | Author          | Genre      | ISBN           |
        | The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |
      Then the request succeded
      Then the stored Book response should be
        | Title                    | Author          | Genre      | ISBN           |
        | The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |
      When I request the Book 'The Pragmatic Programmer' by id
      Then the request succeded
      Then the Book response should match
        | Title                    | Author          | Genre      | ISBN           |
        | The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |

    Scenario Outline: Endpoint Book Get by filters - <FilterName>
      Given I have created the test books for filter testing
      When I request the Book by
        | <FilterName> |
        | <Value>      |
      Then the request succeded
      Then the Book response count should be <ExpectedCount>

    Examples:
        | FilterName | Value        | ExpectedCount |
        | Author     | Martin       | 1             |
        | Author     | Tolkien      | 1             |
        | Genre      | Technology   | 2             |
        | Genre      | Fiction      | 1             |
        | Title      | Clean Code   | 1             |
        | Title      | The Hobbit   | 1             |

    Scenario: Endpoint Book Update
      When I create multiple Book with
        | Title  | Author  | Genre      | ISBN           |
        | Book A | Author1 | Technology | 111-1111111111 |
        | Book B | Author2 | Fiction    | 222-2222222222 |
        | Book C | Author3 | Science    | 333-3333333333 |
      Then the request succeded
      When I update the Book 'Book A' with
        | Title     | Author     | Genre   | ISBN           |
        | Updated A | NewAuthor1 | Science | 444-4444444444 |
      Then the request succeded
      When I request the Book 'Updated A' by id
      Then the Book response should match
        | Title     | Author     | Genre   | ISBN           |
        | Updated A | NewAuthor1 | Science | 444-4444444444 |

    Scenario: Endpoint Book Update 404
      When I try to update a Book with
        | Id | Title    | Author    | Genre   | ISBN           |
        |  0 | NewTitle | NewAuthor | Fiction | 555-5555555555 |
      Then the request fails with 404

    Scenario: Endpoint Book Delete
      When I create multiple Book with
        | Title | Author  | Genre      | ISBN           |
        | BookA | Author1 | Technology | 111-1111111111 |
        | BookB | Author2 | Fiction    | 222-2222222222 |
      Then the request succeded
      When I delete the Book 'BookA' by id
      Then the request succeded
      When I request the Book 'BookA' by id
      Then the request fails with 404

  Rule: Book creation must fail with invalid data

    Scenario: Validator Book Create fails for no title provided
      When I create a single Book with
        | Title | Author  | Genre      | ISBN |
        |       | Author1 | Technology | 1234 |
      Then the request fails with 400

    Scenario: Validator Book Create fails for no author provided
      When I create a single Book with
        | Title  | Author | Genre      | ISBN |
        | Title1 |        | Technology | 1234 |
      Then the request fails with 400

    Scenario: Validator Book Create fails for no genre provided
      When I create a single Book with
        | Title  | Author  | Genre  | ISBN |
        | Title1 | Author1 | NotSet | 1234 |
      Then the request fails with 400

    Scenario: Validator Book Create fails for title too long
      When I create a single Book with
        | Title                                                                                                                                                                                                                                   | Author  | Genre      | ISBN |
        | AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA | Author1 | Technology | 1234 |
      Then the request fails with 400

    Scenario: Validator Book Create fails for author too long
      When I create a single Book with
        | Title  | Author                                                                                                | Genre      | ISBN |
        | Title1 | AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA | Technology | 1234 |
      Then the request fails with 400
