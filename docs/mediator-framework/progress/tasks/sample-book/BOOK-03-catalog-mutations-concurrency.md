# BOOK-03 — Book catalog mutations, concurrency, and audit vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + TESTS  
**Depends on**: BOOK-02

## Problem

Validation, authorization, ETags, and auditing are currently demonstrated with
Greeting names. Book mutations must demonstrate those capabilities through a
complete, testable slice.

## Steps

1. Add `Book_UpdateRequest` and `Book_DeleteRequest`.
2. Wire both contracts to handlers and existing decorators.
3. Add validation, `books.*` authorization scopes, ETag behavior, and audit
   events.
4. Add BDD scenarios for valid update, invalid update, stale ETag, and delete.
5. Run the affected tests and sample build.

## Outcomes

- Book mutation behavior demonstrates validation, authorization, concurrency,
  and auditing through contract-level BDD.

## Acceptance

- [ ] New mutation contracts are implemented, documented, and handler-wired.
- [ ] BDD covers success and failure paths with final assertions.
- [ ] ETag, validation, authorization, and audit behavior is verified.
- [ ] Sample build and affected tests pass.
