@CleanDbBeforeScenario
Feature: Book
    CRUD operations for Book management

Rule: Book CRUD operations must succeed with valid data

Scenario: Endpoint_ Book_ Create and Get
When I create a single Book with
| Title                    | Author          | Genre      | ISBN          |
| The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |
Then the request succeded
Then the stored Book response should be 
| Title                    | Author          | Genre      | ISBN          |
| The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |

When I request the Book 'The Pragmatic Programmer' by id
Then the request succeded
Then the Book response should match
| Title                    | Author          | Genre      | ISBN          |
| The Pragmatic Programmer | Hunt and Thomas | Technology | 978-0135957059 |


Scenario: Endpoint_ Book_ Get by filters  
When I create multiple Book with
| Title          | Author      | Genre      | ISBN          |
| Clean Code     | Martin      | Technology | 978-0132350884 |
| Design Patterns| GoF         | Technology | 978-0201633610 |
| The Hobbit     | Tolkien     | Fiction    | 978-0345339683 |

And I request the Book by
| Author  |
| Martin  |
Then the request succeded
    Then the Book response count should be 1

When I request the Book by
| Genre      |
| Technology |
Then the request succeded
    Then the Book response count should be 2


Scenario: Endpoint_ Book_ Update
When I create multiple Book with
| Title      | Author  | Genre      | ISBN          |
| Book A     | Author1 | Technology | 111-1111111111 |
| Book B     | Author2 | Fiction    | 222-2222222222 |
| Book C     | Author3 | Science    | 333-3333333333 |
Then the request succeded

When I update the Book 'Book A' with
| Title      | Author     | Genre   | ISBN          |
| Updated A  | NewAuthor1 | Science | 444-4444444444 |
Then the request succeded

When I request the Book 'Updated A' by id
Then the Book response should match
| Title     | Author     | Genre   | ISBN          |
| Updated A | NewAuthor1 | Science | 444-4444444444 |

Scenario: Endpoint_ Book_ Update_ 404
When I try to update a Book with
| Id | Title    | Author   | Genre   | ISBN          |
| 0  | NewTitle | NewAuthor| Fiction | 555-5555555555 |
Then the request fails with 404


Scenario: Endpoint_ Book_ Delete
    When I create multiple Book with
| Title  | Author  | Genre      | ISBN          |
| BookA  | Author1 | Technology | 111-1111111111 |
| BookB  | Author2 | Fiction    | 222-2222222222 |
    Then the request succeded

    When I delete the Book 'BookA' by id
    Then the request succeded

    When I request the Book 'BookA' by id
    Then the request fails with 404

Rule: Book creation must fail with invalid data

Scenario: Validator_ Book_ Create fails for no title provided
When I create a single Book with
| Title | Author  | Genre      | ISBN |
|       | Author1 | Technology | 1234 |
Then the request fails with 400

Scenario: Validator_ Book_ Create fails for no author provided
When I create a single Book with
| Title  | Author | Genre      | ISBN |
| Title1 |        | Technology | 1234 |
Then the request fails with 400

Scenario: Validator_ Book_ Create fails for no genre provided  
When I create a single Book with
| Title  | Author  | Genre  | ISBN |
| Title1 | Author1 | NotSet | 1234 |
Then the request fails with 400

Scenario: Validator_ Book_ Create fails for title too long
When I create a single Book with
| Title                                                                                                                                                                                                                                            | Author  | Genre      | ISBN |
| AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA | Author1 | Technology | 1234 |
Then the request fails with 400

Scenario: Validator_ Book_ Create fails for author too long
When I create a single Book with
| Title  | Author                                                                                              | Genre      | ISBN |
| Title1 | AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA | Technology | 1234 |
Then the request fails with 400
