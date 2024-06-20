@CleanDbBeforeScenario
Feature: Ping

## Endpoint ##################################################################################################
Scenario: Endpoint_ Ping_ Test
	Given I make a request to ping
	Then the request succeded
	Then the response is 'pong'

Scenario: Endpoint_ Ping_ Test by Name
    Given I make a request to 'SUCCESSFUL' ping
	Then the request succeded
	Then the ping response is
		| Name       | Code                 |
		| SUCCESSFUL | PING_CODE_SUCCESSFUL |


Scenario: Endpoint_ Ping_ Create and Get
	When I create a single Ping with
		| Name      | Type  |
		| PingName1 | Ping1 |
	Then the request succeded
	Then the stored Ping response should be 
		| Name      | Type  | Code                |
		| PingName1 | Ping1 | PING_CODE_PingName1 |

	When I request the Ping 'PingName1' by id
	Then the request succeded
	Then the Ping response should match
		| Name      | Type  | Code                |
		| PingName1 | Ping1 | PING_CODE_PingName1 |


Scenario: Endpoint_ Ping_ Get by filters  
	When I create multiple Ping with
		| Name      | Type  |
		| PingNameA | Ping1 |
		| PingNameB | Ping2 |
		| PingNameC | Ping2 |

	And  I request the Ping by
		| Name      |
		| PingNameB |
	Then the request succeded
    Then the Ping response count should be 1

	When  I request the Ping by
		| Type  |
		| Ping2 |
	Then the request succeded
    Then the Ping response count should be 2


Scenario: Endpoint_ Ping_ Put
	When I create multiple Ping with
		| Name  | Type  |
		| PingA | Ping1 |
		| PingB | Ping2 |
		| PingC | Ping2 |
	Then the request succeded

	When I update using 'PUT' the Ping 'PingA' with
		| Name        | Type  |
		| PingNewName | Ping2 |
	Then the request succeded

	When I request the Ping 'PingNewName' by id
	Then the Ping response should match
		| Name        | Type  | Code                  |
		| PingNewName | Ping2 | PING_CODE_PingNewName |

Scenario: Endpoint_ Ping_ Put_ 404
	When  I try to update using 'PUT' a Ping with
		| Id | Name        | Type  |
		| 0  | PingNewName | Ping2 |
	Then the request fails with 404


Scenario: Endpoint_ Ping_ Patch
	When I create multiple Ping with
		| Name  | Type  |
		| PingA | Ping1 |
		| PingB | Ping2 |
		| PingC | Ping2 |
	Then the request succeded

	When I update using 'PATCH' the Ping 'PingA' with
		| Name      |
		| PatchName |
	Then the request succeded

	When I request the Ping 'PatchName' by id
	Then the Ping response should match
		| Name      | Type  | Code                |
		| PatchName | Ping1 | PING_CODE_PatchName |

	When I update using 'PATCH' the Ping 'PatchName' with
		| Type  |
		| Ping2 |
	Then the request succeded

	When I request the Ping 'PatchName' by id
	Then the Ping response should match
		| Name      | Type  | Code                |
		| PatchName | Ping2 | PING_CODE_PatchName |

Scenario: Endpoint_ Ping_ Patch_ 404
	When  I try to update using 'PATCH' a Ping with
	| Id | Name        | Type  |
	| 0  | PingNewName | Ping2 |
	Then the request fails with 404
	

Scenario: Endpoint_ Ping_ Delete
    When I create multiple Ping with
		| Name  | Type  |
		| PingA | Ping1 |
		| PingB | Ping2 |
    Then the request succeded

    When I delete the Ping 'PingA' by id
    Then the request succeded

    When I request the Ping 'PingA' by id
    Then the request fails with 404


Scenario: Endpoint_ Ping_ Create and SendMsg
	When I create a single Ping And SendMsg with
		| Name      | Type  |
		| PingName1 | Ping1 |
	Then the request succeded
	Then the stored Ping response should be 
		| Name      | Type  | Code                |
		| PingName1 | Ping1 | PING_CODE_PingName1 |

	When I wait background bus to idle and outbox to be empty
	Then the request succeded

	When I request the Ping 'PingName1' by id
	Then the request succeded
	Then the Ping response should match
		| Name      | Type  | Code     |
		| PingName1 | Ping1 | HandleOk |

### Audit ##################################################################################################
Scenario: Audit_ Check Ping_ Create
	When I create a single Ping with
		| Name      | Type  |
		| AuditPing | Ping1 |
    Then the request succeded
       
    When I get the last audit for 'Ping'
    Then the audit record has
		| Key    | Value                   |
		| UserId | testUser1@ark-energy.eu |

    When I get the list of changes for this audit
    Then the list of changes contains 1 records

	Then the current Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping1 | PING_CODE_AuditPing |

Scenario: Audit_ Check Ping_ UpdatePut
	When I create a single Ping with
		| Name      | Type  |
		| AuditPing | Ping1 |
    Then the request succeded
       
    When I get the last audit for 'Ping'
    Then the audit record has
		| Key    | Value                   |
		| UserId | testUser1@ark-energy.eu |

    When I get the list of changes for this audit
    Then the list of changes contains 1 records

	Then the current Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping1 | PING_CODE_AuditPing |

	When I update using 'PUT' the Ping 'AuditPing' with
		| Name             | Type  |
		| AuditPingUpdated | Ping2 |
	Then the request succeded

    When I get the last audit for 'Ping'
	And I get the list of changes for this audit
	
	Then the previous Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping1 | PING_CODE_AuditPing |
		
	Then the current Ping audit is
		| Name             | Type  | Code                       |
		| AuditPingUpdated | Ping2 | PING_CODE_AuditPingUpdated |

Scenario: Audit_ Check Ping_ UpdatePatch
	When I create a single Ping with
		| Name      | Type  |
		| AuditPing | Ping1 |
    Then the request succeded
       
    When I get the last audit for 'Ping'
    Then the audit record has
		| Key    | Value                   |
		| UserId | testUser1@ark-energy.eu |

    When I get the list of changes for this audit
    Then the list of changes contains 1 records

	Then the current Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping1 | PING_CODE_AuditPing |

	When I update using 'PATCH' the Ping 'AuditPing' with
		| Type  |
		| Ping2 |
	Then the request succeded

    When I get the last audit for 'Ping'
	And I get the list of changes for this audit
	
	Then the previous Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping1 | PING_CODE_AuditPing |
		
	Then the current Ping audit is
		| Name      | Type  | Code                |
		| AuditPing | Ping2 | PING_CODE_AuditPing |

## Validator ##################################################################################################
Scenario: Validator_ Ping_ Create fails for no type provided
	When I create a single Ping with
	| Name      | Type |
	| PingName1 |      |
	Then the request fails with 400

Scenario: Validator_ Ping_ Create fails for no name provided
	When I create a single Ping with
	| Name | Type  |
	|      | Ping1 |
	Then the request fails with 400
	
Scenario: Validator_ Ping_ Create fails for name too long 
	When I create a single Ping with
	| Name                                                        | Type  |
	| AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA | Ping1 |
	Then the request fails with 400

Scenario: Validator_ Ping_ by Name fails
    Given I make a request to 'FAIL' ping
	Then the request fails with 400

Scenario: Validator_ Ping_ by Name fails because the min length 
	Given I make a request to '123' ping
	Then the request fails with 400
