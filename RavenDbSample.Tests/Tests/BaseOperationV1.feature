Feature: BaseOperation
Check if the BaseOperation controller works

Scenario: Base Operation Create and Read

	When I create a new BaseOperation
	Then The request succeded
	When I get the BaseOperation with Id 'Pippo'
	Then The request succeded
	When I read the BaseOperation
	Then The request succeded
	Then The BaseOperation has B Id equal to 'SpecB-1'

Scenario: Base Operation Fails on Empty Database

	When I get the BaseOperation with Id 'Pippo'
	Then The request fails with 404
