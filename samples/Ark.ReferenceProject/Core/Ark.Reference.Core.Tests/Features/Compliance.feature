@CleanDbBeforeScenario
Feature: Compliance

  Scenario: Classified book data is protected across the application boundaries
    Given I have created a book with
      | Title             | Author   | Genre      | ISBN           |
      | Compliance sample | Jane Doe | Technology | 111-1111111111 |
    Then the API response contains the cleartext author
    And the compliance log and span contain only the masked author
    And the database masks the author for a low-privilege reader

  Scenario: Swagger keeps sensitive values as classified primitives
    When I get url /swagger/docs/v1.0
    Then the request succeded
    And the Swagger Book author schema is a classified primitive
