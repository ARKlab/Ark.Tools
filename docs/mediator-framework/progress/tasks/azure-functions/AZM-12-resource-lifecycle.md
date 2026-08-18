# AZM-12 — Concurrency-safe Service Bus resource lifecycle

**Category**: azure-functions-messaging · **Priority**: core
**Depends on**: AZM-01, AZM-02, AZM-10
**Scope**: RUNTIME + HOSTING
**Design**: [Resource lifecycle](../../azure-functions-messaging-design.md#8-resource-lifecycle)

## Problem

Participants may start concurrently and must dynamically manage their event
subscriptions without deleting resources owned by another participant.

## Execution map

- **Runtime project**: implement management reconciliation in
  `Ark.Tools.MediatorFramework.Messaging`; consume only the generated
  manifest from AZM-10 through the AZM-05 transport management seam.
- **Ownership**: named consumer participants own their identity queue and
  forwarding
  subscriptions. Topics declared by the network may be `Ensure`d by the
  owning publisher **or** by any subscriber (create if missing only).
  Producer-role participants own no queue and no subscription. Sender-only
  participants
  own no entity. Queues and topics are never auto-deleted.
- **Reconciliation order**: validate configuration → ensure identity queue →
  ensure declared topics (publisher or subscriber) → ensure/update
  forwarding subscriptions → delete obsolete **subscriptions** proven to be
  participant-owned, including in production. No rolling dual-subscription
  grace:
  an obsolete subscription would keep delivering unhandled events into the
  identity queue and spike the DLQ.
- **Accepted rollout risk**: both removal and addition can be incompatible
  with old processors. Removing a subscription can race with an old host that
  still expects it; adding one can deliver an event that an old host cannot
  process. Deployment must stop/drain incompatible versions or use versioned
  identities/contracts.
- **Failure policy**: authorization, naming, incompatible existing entity
  settings, and partial reconciliation fail startup with structured
  diagnostics.
- **Stop condition**: never delete queues/topics automatically and never
  mutate Rebus-managed resources.

## Implementation steps

1. Define the generated desired-resource manifest for participant identity
   queues,
   owned topics, and subscriptions forwarding to identity queues.
2. Add startup registration that can ensure declared queues/topics/subscriptions
   exist while allowing IaC-managed deployments. Topic `Ensure` is allowed
   from publisher and subscriber participants; it must not delete or mutate a
   pre-existing foreign/IaC topic.
3. Derive deterministic subscription names from participant identity and
   topic, using
   only Azure-supported names.
4. Mark subscriptions with ownership metadata where Azure supports it and
   restrict deletion to subscriptions demonstrably owned by the current
   participant
   identity. Never delete queues or topics.
5. Remove obsolete participant-owned subscriptions after the desired set is
   known,
   including in production. Never remove an arbitrary or foreign
   subscription.
6. Make create/update/delete operations safe under concurrent startup from
   multiple instances and participants.
7. Expose diagnostics for management failures and avoid silently treating
   authorization or naming failures as success.
8. Apply the network retry policy to queue/subscription maximum delivery count
   and validate PeekLock-compatible entity settings.
9. Keep queues/topics optionally IaC-precreated; startup ensure must be
   idempotent.
10. Treat event logical-name changes as explicit topology migrations.
    `FormerNames` affects contract deserialization only; never auto-rename,
    merge, or delete an old topic.
11. Document that subscription reconciliation is not a deployment
    orchestrator and provide stop/drain or versioned-identity rollout guidance.

## Guide contribution

Update [`guide/azure-functions.md`](../../../guide/azure-functions.md) with
network-scoped resource provisioning, participant-owned subscription cleanup,
concurrent startup, and IaC coexistence.

## Sample extension

Extend the Book sample deployment/test composition with the shared network
resource manifest and prove that multiple Mediator Framework participants
preserve
foreign/IaC-managed resources. Rebus runs in a separate topology and does not
participate in this lifecycle.

## Required test coverage

- Concurrent ensure from multiple instances of one participant.
- Two participants subscribing to one topic without deletion races.
- Every subscription forwards to its participant identity queue.
- Multiple topics for one participant.
- Obsolete owned subscription removal.
- Foreign subscription preservation.
- Missing permissions, invalid names, and transient management failures.
- Restart after partial resource creation.
- Event logical-name migration leaves old topics untouched and requires an
  explicit operator plan.
- A subscriber participant starting before the publisher can `Ensure` the
  topic
  and then create its forwarding subscription.
- Obsolete owned subscriptions are deleted on reconcile in production.
- Addition/removal rollout guidance covers old-processor incompatibility in
  both directions.

## Caveats

- Confirm Azure Service Bus management API limitations and eventual consistency
  before finalizing the naming/ownership implementation.
- Production deletion is allowed only for participant-owned resources.

## Outcomes

- Event subscriptions follow participant configuration dynamically.
- IaC and runtime startup provisioning can coexist safely.
- Scale-out does not create duplicate unmanaged subscriptions or destructive
  cross-participant races.

## Acceptance

- [ ] Desired resources are generated and consumed at startup.
- [ ] Concurrent lifecycle operations are idempotent and tested.
- [ ] Only participant-owned obsolete subscriptions are removed.
- [ ] Management failures are explicit and diagnosable.
- [ ] IaC-precreated resources remain supported.
- [ ] The [task board](../README.md) status for AZM-12 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
