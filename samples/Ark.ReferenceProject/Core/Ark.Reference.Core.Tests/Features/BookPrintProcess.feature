Feature: BookPrintProcess
    Background processing of book printing with Rebus messaging

@CleanDbBeforeScenario
Scenario: Create a book print process successfully
    Given I have created a book with title "Test Book" and author "Test Author"
    When I create a book print process for that book
    Then the print process should be created with status "Pending"
    And the print process progress should be 0

@CleanDbBeforeScenario
Scenario: Cannot create print process when one is already running
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book
    When I try to create another book print process for that book
    Then I should get a 400 Bad Request response
    And the error should indicate "PRINT_ALREADY_RUNNING"

@CleanDbBeforeScenario
Scenario: Print process completes successfully in background
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book with ShouldFail false
    When I wait for the print process to complete
    Then the print process status should be "Completed"
    And the print process progress should be 1.0

@CleanDbBeforeScenario
Scenario: Print process fails at 30% progress when ShouldFail is true
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book with ShouldFail true
    When I wait for the print process to fail
    Then the print process status should be "Error"
    And the print process progress should be 0.3
    And the error message should contain "Simulated print process failure"

@CleanDbBeforeScenario
Scenario: IFailed handler sets error status correctly
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book with ShouldFail true
    When I wait for the IFailed handler to process the error
    Then the print process status should be "Error"
    And the error message should not be empty
