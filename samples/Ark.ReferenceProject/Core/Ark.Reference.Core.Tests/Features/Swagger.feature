Feature: Swagger

  Scenario Outline: Swagger Spec <version>
    When I get url /swagger/docs/<version>
    Then the request succeded

    Examples:
      | version |
      | v1.0    |
