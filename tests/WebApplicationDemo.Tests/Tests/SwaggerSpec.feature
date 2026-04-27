Feature: SwaggerSpec
    OpenAPI/Swagger specification correctness including NodaTime type mapping,
    OData endpoint presence per version, model versioning in schema,
    deprecated flag, and FixODataMediaType filter behavior

Scenario: Swagger spec v0.0 - contains OData MarketRecord endpoint
    When I fetch the swagger spec for version v0.0
    Then the swagger spec contains path matching MarketRecord

Scenario: Swagger spec v1.0 - contains OData People and MarketRecordV1 endpoints
    When I fetch the swagger spec for version v1.0
    Then the swagger spec contains path matching People
    And the swagger spec contains path matching MarketRecordV1

Scenario: Swagger spec v2.0 - contains OData People v2 endpoint
    When I fetch the swagger spec for version v2.0
    Then the swagger spec contains path matching People

Scenario: Swagger spec v0.0 - does not contain People endpoint (no People controller for v0)
    When I fetch the swagger spec for version v0.0
    Then the swagger spec does not contain path matching People

Scenario: NodaTime LocalDate mapped as date format via LocalDateRange schema
    When I fetch the swagger spec for version v1.0
    Then the swagger spec LocalDateRange schema has start property with format date

Scenario: Entity V1 Output allOf has date property with LocalDate format
    When I fetch the swagger spec for version v1.0
    Then the swagger spec Entity.V1.Output allOf has date property with format date

Scenario: Person V1 schema does not have phone property directly
    When I fetch the swagger spec for version v1.0
    Then the swagger spec schema Person.V1 does not have property phone

Scenario: Person V2 schema has phone property in allOf extension
    When I fetch the swagger spec for version v2.0
    Then the swagger spec schema Person.V2 allOf has property phone

Scenario: Non-OData endpoint has no odata media type in responses
    When I fetch the swagger spec for version v1.0
    Then the swagger spec entity endpoint has no odata content type in responses

Scenario: FlaggedEnum parameter renders as array in swagger
    When I fetch the swagger spec for version v1.0
    Then the swagger spec GET entity endpoint has EntityResult as array parameter

Scenario: Default error responses (400, 401, 403, 500) are present on entity endpoint
    When I fetch the swagger spec for version v1.0
    Then the swagger spec entity GET endpoint has response 400
    And the swagger spec entity GET endpoint has response 401
    And the swagger spec entity GET endpoint has response 403
    And the swagger spec entity GET endpoint has response 500
