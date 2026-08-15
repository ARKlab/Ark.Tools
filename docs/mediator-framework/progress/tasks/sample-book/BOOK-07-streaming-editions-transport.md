# BOOK-07 — Book streaming, editions, and transport parity vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + HOSTS + TESTS  
**Depends on**: BOOK-01

## Problem

The framework's streaming and polymorphic serialization capabilities need
coherent Book examples and must be verified across the supported host
boundaries.

## Steps

1. Add `StreamBooksQuery` and a Book stream item contract.
2. Wire the query to a cancellation-aware Application handler.
3. Add Book edition polymorphic contracts and a describing handler.
4. Add JSON, protobuf, MessagePack, OpenAPI, HTTP, and gRPC metadata.
5. Add contract-level BDD for stream behavior and edition dispatch.
6. Add focused HTTP/gRPC boundary tests and generated artifact checks.
7. Run affected tests and sample build.

## Outcomes

- Streaming and polymorphism are explained and tested with Book concepts.
- Transport-specific behavior remains covered at the host boundary.

## Acceptance

- [x] New streaming and edition contracts are implemented and handler-wired.
- [x] BDD covers stream results, cancellation/bounds, and edition variants.
- [x] Applicable HTTP/gRPC/serialization tests pass.
- [x] Generated API/protobuf artifacts remain consistent.
- [x] Sample build and affected tests pass.
