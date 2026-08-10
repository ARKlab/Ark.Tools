Feature: Greeting cards
    Greeting-card attachments are tested through transport-agnostic application contracts.

    Rule: A greeting card retains its metadata and bytes

        Scenario: Upload and retrieve a greeting card
            When I upload a greeting card with
                | Name     | ContentType | Content       |
                | card.txt | text/plain  | Hello, cards! |
            Then the greeting card upload is
                | Name     | ContentType | Length |
                | card.txt | text/plain  | 13     |
            When I retrieve the current greeting card
            Then the current greeting card is
                | Name     | ContentType | Content       |
                | card.txt | text/plain  | Hello, cards! |

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
