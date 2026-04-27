Feature: OData
    OData endpoint functionality: querying, filtering, key lookup, and model versioning

Scenario: OData MarketRecord v0 - list returns data
    When I get OData url /v0.0/MarketRecord
    Then The OData request succeded
    And the OData response has value array

Scenario: OData MarketRecord v0 - $select returns only selected field
    When I get OData url /v0.0/MarketRecord?$select=Market
    Then The OData request succeded
    And the OData response has value array
    And the OData response first item has field market
    And the OData response first item does not have field dateTimeOffset

Scenario: OData MarketRecord v1 - list returns data
    When I get OData url /v1.0/MarketRecordV1
    Then The OData request succeded
    And the OData response has value array

Scenario: OData People v1 - list returns Person V1 schema (no phone field)
    When I get OData url /v1.0/People
    Then The OData request succeded
    And the OData response has value array
    And the OData response first item does not have field phone

Scenario: OData People v2 - list returns Person V2 schema (with phone field)
    When I get OData url /v2.0/People
    Then The OData request succeded
    And the OData response has value array
    And the OData response first item has field phone

Scenario: OData People v1 - key lookup returns entity with id 1
    When I get OData url /v1.0/People(1)
    Then The OData request succeded
    And the OData single result has id equal to 1

Scenario: OData People v2 - key lookup returns entity with id 2
    When I get OData url /v2.0/People(2)
    Then The OData request succeded
    And the OData single result has id equal to 2

Scenario: OData People v1 - $filter by FirstName returns one result
    When I get OData url /v1.0/People?$filter=FirstName eq 'John'
    Then The OData request succeded
    And the OData response has value array
    And the OData response value count is 1

Scenario: OData People v2 - $orderby by FirstName returns ordered results
    When I get OData url /v2.0/People?$orderby=FirstName asc
    Then The OData request succeded
    And the OData response has value array

Scenario: OData People v1 - $select returns only selected fields
    When I get OData url /v1.0/People?$select=Id,FirstName
    Then The OData request succeded
    And the OData response has value array
    And the OData response first item has field id
    And the OData response first item has field firstName
    And the OData response first item does not have field email
