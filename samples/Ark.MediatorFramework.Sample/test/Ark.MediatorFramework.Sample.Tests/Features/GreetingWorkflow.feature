Feature: Greeting composition workflow
    The application completes queued greeting work through an isolated in-memory Rebus processor.

    Rule: Queued work is observed through application contracts

        Scenario: Compose a greeting and preserve the authenticated user
            When I compose a greeting with
                | Name               |
                | Background greeting |
            When I wait for the background bus to be idle and the outbox to be empty
            Then the background greeting is eventually visible through the query contract
            And the background greeting audit is attributed to "application-test-user"

        Scenario: Retry a transient composition failure
            When I compose a greeting with
                | Name          | FailuresBeforeSuccess |
                | Retried hello | 1                     |
            Then the background greeting is eventually visible through the query contract

    Rule: Failed messages are handled deterministically

        Scenario: Exhausted delivery reaches the error queue
            When I dispatch a failing background message with reason "retry exhaustion"
            Then the error queue contains the failed message

        Scenario: A second exhausted delivery remains in the error queue
            When I dispatch a failing background message with reason "failed second-level handler"
            Then the error queue contains the failed message
