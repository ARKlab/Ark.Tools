# BOOK-02 — Book catalog search and persistence vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + DATABASE + TESTS  
**Depends on**: BOOK-01

## Problem

The Book slice must work against supported persistence profiles and provide
search. A schema-only task would leave the solution without a verifiable
feature.

## Steps

1. Add `Book_SearchQuery` with bounded filters, paging, and sorting.
2. Wire the query to an Application handler.
3. Add Book SQL/Dapper mapping, indexes, and the in-memory equivalent.
4. Add BDD scenarios for persisted create, retrieve, and search.
5. Run SQL and in-memory profiles where infrastructure is available.
6. Run the affected tests and sample build.

## Outcomes

- Book catalog behavior works against supported persistence profiles.
- Search is proven through a public contract, handler, and BDD scenario.

## Acceptance

- [x] Search contract is implemented, documented, and wired to a handler.
- [x] BDD covers search success and bounded paging behavior.
- [x] SQL and in-memory persistence tests pass.
- [x] Reset/cleanup leaves subsequent scenarios isolated.
- [x] Sample build and affected tests pass.
