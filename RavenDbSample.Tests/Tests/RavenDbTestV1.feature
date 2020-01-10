Feature: RavenDbTestSteps
Check if the generic controller works

Scenario: Audit Test
    Given Role 'Admin'
     When I create a new Audit Test
     Then The request succeded
