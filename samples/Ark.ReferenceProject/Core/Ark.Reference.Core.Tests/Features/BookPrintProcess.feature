@CleanDbBeforeScenario
Feature: BookPrintProcess
    Background processing of book printing with Rebus messaging

Rule: Book print process creation must follow business rules

Scenario: Create a book print process successfully
    Given I have created a book with title "Test Book" and author "Test Author"
    When I create a book print process for that book
    Then the print process should be created with status "Pending"
    And the print process progress should be 0

Scenario: Cannot create print process when one is already running
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book
    When I try to create another book print process for that book
    Then the request fails with 400
    And the business rule violation code is "BookPrintingProcessAlreadyRunningViolation"

Rule: Background processing must update print process status correctly

Scenario: Print process completes successfully in background
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book with ShouldFail false
    When I wait background bus to idle and outbox to be empty
    And I retrieve the print process status
    Then the print process is
    | Status    | Progress |
    | Completed | 1.0      |

Scenario: IFailed handler sets error status correctly
    Given I have created a book with title "Test Book" and author "Test Author"
    And I have created a book print process for that book with ShouldFail true
    When I wait background bus to idle and outbox to be empty
    And I retrieve the print process status
    Then the print process is
    | Status |
    | Error  |
    And the print process has error details
