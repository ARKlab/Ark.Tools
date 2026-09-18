# BOOK-06 — Book reviews and reading activity vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + DATABASE + TESTS  
**Depends on**: BOOK-02

## Problem

The Book sample needs natural child-resource behavior beyond catalog metadata,
without becoming a full library product.

## Steps

1. Add `CreateBookReviewRequest` and `ListBookReviewsQuery`.
2. Wire contracts to handlers and persist review data.
3. Add rating/text validation and `books.reviews.*` authorization.
4. Add `RecordReadingActivityRequest` and `GetReadingActivityQuery`.
5. Wire and persist activity using repository time abstractions.
6. Add contract-level BDD for review and activity success/failure paths.
7. Run affected SQL/in-memory tests and sample build.

## Outcomes

- Child-resource commands and queries are demonstrated through Book behavior.
- Validation, authorization, NodaTime, and bounded activity retrieval are
  covered by BDD.

## Acceptance

- [x] New review/activity contracts are implemented, documented, and wired.
- [x] BDD covers valid and invalid review/activity operations.
- [x] Persistence cleanup isolates scenarios.
- [x] Time values follow repository conventions.
- [x] Sample build and affected tests pass.
