# BOOK-01 — Book catalog create and retrieve vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + TESTS

## Problem

The first migration increment must establish a working Book vocabulary. A
contracts-only change is not verifiable under contract-level BDD testing.

## Decision

Start with minimal create and retrieve operations using the existing in-memory
persistence seam. Do not add future child entities in this increment.

## Steps

1. Add documented `Book_CreateRequest` and `Book_GetQuery` contracts.
2. Add the Book model and handlers in the existing Application structure.
3. Wire contracts and handlers through Application composition.
4. Add a scenario-owned Reqnroll scenario that creates and retrieves a Book,
   ending with a `Then` assertion.
5. Replace only the catalog smoke scenario used for Greeting.
6. Run the affected BDD tests and sample build.

## Outcomes

- The sample has its first independently working Book vertical slice.
- Contract-level BDD proves public contracts are wired to handlers.

## Acceptance

- [ ] At least one new public Book contract is implemented and documented.
- [ ] Each new contract is wired to an Application handler.
- [ ] Reqnroll dispatches the contract and asserts the result.
- [ ] Sample build and affected tests pass.
- [ ] No unimplemented public contract is added.
