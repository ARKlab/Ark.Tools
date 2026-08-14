# BOOK-08 — Complete Book migration and Greeting removal

**Category**: sample-book · **Priority**: Release scope · **Scope**: SAMPLE + HOSTS + TESTS + DOCUMENTATION  
**Depends on**: BOOK-03, BOOK-04, BOOK-05, BOOK-06, BOOK-07

## Problem

The final migration must remove Greeting without leaving stale routes,
generated artifacts, tests, or documentation. A cleanup-only task is not
acceptable because every task must remain testable.

## Steps

1. Add and wire a final `GetBookSummaryQuery` contract if the completed
   capabilities need a final aggregate smoke path.
2. Complete HTTP, gRPC, Azure Functions, OpenAPI, JSON, protobuf, and API
   snapshot parity for the implemented Book surface.
3. Add the final contract-level BDD smoke scenario and host-boundary checks.
4. Update the README and guide with the Book architecture and incremental
   workflows.
5. Remove Greeting contracts, handlers, messages, routes, tables, fixtures,
   tests, and active documentation after replacement coverage is green.
6. Search for stale Greeting references and classify historical references.
7. Run the full sample build and test suite.

## Outcomes

- Book is the sole active sample domain.
- Documentation, generated artifacts, hosts, and tests describe the same API.

## Acceptance

- [x] Existing Book catalog, streaming, edition, review, activity, cover, and
      print-process contracts provide the final smoke coverage; no additional
      summary contract is required.
- [x] Contract-level BDD and host-boundary parity tests pass.
- [x] No stale Greeting reference remains in active sample code or docs.
- [x] README and guide explain the Book workflows end to end.
- [x] Full sample build and tests pass.
