# BOOK-04 — Book cover upload and download vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + HOSTS + TESTS  
**Depends on**: BOOK-03

## Problem

Greeting-card attachments do not belong in a Book API. Cover upload/download
must be delivered as a working vertical slice, not as contract preparation.

## Steps

1. Add `UploadBookCoverRequest` and `DownloadBookCoverQuery`.
2. Wire both contracts to handlers and existing attachment storage.
3. Add file validation, authorization, and cover metadata persistence.
4. Add contract-level BDD for upload and download.
5. Add HTTP/gRPC boundary tests for the implemented routes.
6. Remove the corresponding greeting-card scenario only after Book coverage is
   green.
7. Run the affected tests and sample build.

## Outcomes

- Attachments are demonstrated through a working Book cover workflow.

## Acceptance

- [ ] New cover contracts are implemented, documented, and handler-wired.
- [ ] BDD and applicable HTTP/gRPC tests pass.
- [ ] File validation, authorization, and missing-cover behavior is verified.
- [ ] Sample build and affected tests pass.
