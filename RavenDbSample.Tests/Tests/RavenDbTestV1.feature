Feature: RavenDbTestSteps
Check if the generic controller works

  Scenario: Test Controller
     When I try to ping
     Then The request succeded

Scenario: Audit Test
     When I create a new Audit Test
     Then The request succeded
