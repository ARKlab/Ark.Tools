Feature: Serialization

  Scenario: Controllers and Minimal APIs bind JSON strictly
    Then the HTTP JSON options use the Ark defaults and respect nullable annotations and required constructor parameters
