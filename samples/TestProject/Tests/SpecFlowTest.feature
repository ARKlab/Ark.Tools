Feature: SpecFlowTest
	Test to see if specflow still working


Scenario: SpecFlow stil working
	When I get a wrong url
	Then The request fails with 404
	
Scenario: Swagger Spec V0.0
    When I get url /swagger/docs/v0.0
    Then The request succeded

Scenario: Swagger Spec V1.0
    When I get url /swagger/docs/v1.0
    Then The request succeded

Scenario: Swagger Spec V2.0
    When I get url /swagger/docs/v2.0
    Then The request succeded

Scenario: Swagger Spec V3.0
    When I get url /swagger/docs/v3.0
    Then The request succeded

Scenario: Swagger Page
    When I get url /swagger/index.html
    Then The request succeded
