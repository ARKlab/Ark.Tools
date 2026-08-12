Feature: Greeting cards
    Greeting-card attachments are tested through transport-agnostic application contracts.

        Scenario: Unknown greeting cards report a typed missing entity
            When I retrieve an unknown greeting card
            Then the document query fails because the greeting card is missing

    Rule: Batch upload preserves the supplied attachment order

        Scenario: Upload greeting cards as a collection
            When I upload greeting cards with
                | Name      | ContentType | Content |
                | first.txt | text/plain  | one     |
                | second.txt | text/plain | two     |
            Then the greeting card batch contains
                | Name       | ContentType | Content |
                | first.txt  |             |         |
                | second.txt |             |         |
