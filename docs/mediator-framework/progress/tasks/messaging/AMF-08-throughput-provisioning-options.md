# AMF-08 — Throughput options on the transport declaration and provisioning

**Category**: messaging-throughput · **Priority**: pre-release
**Depends on**: AMF-07
**Scope**: FRAMEWORK + SAMPLE
**Design**: [Resource provisioning](../../../messaging-throughput-prd.md#8-resource-provisioning), [Configuration surface](../../../messaging-throughput-prd.md#9-configuration-surface)

## Problem

Service Bus partitioning must be chosen **at entity creation** and cannot be
changed afterwards, yet `ServiceBusTransportManagement.EnsureQueueAsync` sets
only `MaxDeliveryCount` and `UserMetadata`. A host that needs a partitioned queue
for throughput has no way to ask for one, and an existing non-partitioned queue
silently stays non-partitioned forever.

`LockDuration` has the same problem in reverse: it is mutable, matters for
prefetch safety, and is not declared anywhere.

These are deployment-shaping decisions, so they belong where the host declares
its transport, and must also be bindable from configuration so a deployment can
change them without a rebuild.

## Execution map

- **Declaration site**: options on the fluent transport declaration —
  `UseTransport(t => t.UseServiceBus(client, o => { o.EnablePartitioning = true;
  o.LockDuration = ...; }))` — and on the receiver's processing options.
- **Runtime setup**: the same options bind from configuration through
  `IOptions<>`, so partitioning intent and processing limits are deployable
  settings, not compile-time constants.
- **Create-time only**: `Partitioned` maps to `CreateQueueOptions.EnablePartitioning`
  and is applied only when the entity is created.
- **Loud mismatch**: the reconciler compares the declared value against the
  existing entity and, on mismatch, throws a diagnostic naming the entity, both
  values, and the fact that recreation is the only fix. Premium namespaces also
  warn that partitioning is a namespace-creation-time choice.
- **Mutable settings**: `LockDuration`, and size limits where the tier allows, are
  reconciled and updated.
- **Cost disclosure**: partitioning is opt-in because it forbids cross-partition
  transactions and send-batches, makes `SessionId` the partition key, and scopes
  ordering and dedup per partition.
- **Storage Queues**: no new provisioning knobs; visibility timeout stays a
  client-side receive parameter (AMF-06).

## Implementation steps

1. Add the throughput options to the transport builder and to
   `MessagingResourceManifest`, with the PRD's defaults.
2. Bind the options from configuration and validate them at composition time with
   named diagnostics.
3. Apply `EnablePartitioning` and `LockDuration` in `EnsureQueueAsync` at create
   time.
4. Implement the reconciler comparison and the mismatch diagnostic, including the
   Premium namespace warning.
5. Reconcile mutable settings on an existing entity without recreating it.
6. Validate `LockDuration` against `MaximumHandlerDuration`, the prefetch budget
   and the renewal capability.
7. Ensure the send path and Azure Functions receivers are unaffected by any of
   these options.
8. Update the API surface baseline.

## Core code shapes

Immutable-at-create settings are compared, never patched. The diagnostic must be
actionable on its own: entity name, declared value, actual value, required
remediation.

## Guide contribution

Document the option surface at both the transport declaration and the
configuration binding, which settings are create-time versus reconcilable, the
partitioning trade-offs, and the mismatch remediation procedure.

## Sample extension

Add a partitioned-queue profile to the sample with the trade-offs documented, and
show the mismatch diagnostic in the sample readme.

## Required test coverage

- A new queue is created partitioned when declared, and non-partitioned by
  default.
- An existing entity with a different partitioning value produces the diagnostic
  and does not silently continue.
- `LockDuration` is applied at create time and reconciled on an existing entity.
- Configuration binding produces the same result as the fluent declaration.
- Invalid combinations of lock duration, handler duration and prefetch fail
  composition.
- The send path and Functions receivers are unaffected.

## Outcomes

- Throughput-shaping infrastructure intent is declarable and deployable.
- Unfixable-in-place misconfiguration is reported loudly instead of silently
  degrading throughput.

## Acceptance

- [ ] Throughput options exist on the transport declaration and bind from configuration.
- [ ] Partitioning is applied at create time and never silently ignored.
- [ ] The mismatch diagnostic names entity, values and remediation.
- [ ] Lock duration is reconciled and validated against processing options.
- [ ] The [task board](../README.md) status for AMF-08 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
