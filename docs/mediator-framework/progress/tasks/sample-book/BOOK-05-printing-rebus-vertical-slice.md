# BOOK-05 — Book printing process completion vertical slice

**Category**: sample-book · **Priority**: Release scope · **Scope**: API + APPLICATION + REBUS HOST + TESTS  
**Depends on**: BOOK-03

## Problem

The sample already contains a Book printing process:
`BookPrintProcessResponse`, `CreateBookPrintProcessRequest`,
`GetBookPrintProcessQuery`, `ProcessBookPrintProcessRequest`, handlers,
persistence, and Reqnroll scenarios. This task must not create a duplicate
printing-job model or parallel Rebus workflow.

## Steps

1. Treat the existing `BookPrintProcess*` contracts, handlers, persistence,
   notification service, and BDD scenarios as the baseline implementation.
2. Add one missing public operation, `CancelBookPrintProcessRequest`, only if
   cancellation can use the existing process state and handler composition
   without introducing a second job model.
3. Wire cancellation to an Application handler and define valid terminal and
   invalid concurrent states.
4. Extend contract-level BDD with cancellation success and invalid-state
   scenarios; retain completion, failure, concurrency, and resume scenarios.
5. Verify sender/processor ownership, bounded retry, outbox behavior, and
   dead-letter diagnostics against the existing implementation.
6. Run Rebus integration tests and sample build.

## Outcomes

- The existing Book printing process is completed and hardened rather than
  duplicated.
- Rebus is demonstrated through one coherent Book workflow.
- Public contracts do not expose internal bus messages.

## Acceptance

- [ ] Existing BookPrintProcess contracts and handlers remain the sole printing
  workflow.
- [ ] A new cancellation contract is implemented and handler-wired, or the
  task records why the existing state model makes cancellation inappropriate.
- [ ] BDD covers completion, failure, concurrency, resume, and cancellation
  when implemented.
- [ ] Sender/processor, retry, outbox, dead-letter, and cleanup behavior is
  verified.
- [ ] Sample build and affected tests pass.
