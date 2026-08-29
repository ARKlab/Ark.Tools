# AZM-23 — Multiline messaging API surface

**Category**: azure-functions-messaging · **Priority**: pre-release
**Depends on**: AZM-17, AZM-18, AZM-19, AZM-21, AZM-22
**Scope**: API-SURFACE GENERATOR + BASELINES + DOCUMENTATION
**Design**: [API surface snapshots](../../../design.md#api-surface-snapshots), [API-surface guide](../../../guide/api-surface-snapshots.md)

## Problem

Messaging `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` snapshots currently
place every value on one dense line. Small metadata changes replace the whole
line, hiding the exact contract decision developers must review. This makes the
API-surface safety feature unnecessarily difficult to use.

Before release, messaging records must use a deterministic multiline grammar.
Existing one-line baselines intentionally fail once so developers review and
accept the clearer generated representation.

## Execution map

- **Generator**: emit one deterministic block per message, event, participant,
  and network with one field per line.
- **Parser**: strictly validate block kinds, required fields, ordering,
  duplicates, values, and terminators.
- **Diff ownership**: map every changed field back to its owning CLR declaration
  for `ARKAPI002`.
- **Ordering**: sort blocks by kind and fully qualified CLR owner; sort all set
  values ordinally within their field.
- **Migration**: reject old one-line messaging records with an actionable
  diagnostic pointing to baseline regeneration. No mixed grammar.
- **Compatibility**: leave non-messaging API-surface record formats unchanged.

## Implementation steps

1. Define a simple block grammar with an explicit kind/owner header, indented
   `key: value` fields, and an unambiguous terminator.
2. Give `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` fixed required field
   lists and ordering. Emit optional/empty sets as `-`; never omit a field.
3. Place each scalar field on its own line. Keep each already-sorted set on one
   field line unless a value-per-line form produces a smaller and clearer diff;
   select one form and lock it with snapshots.
4. Parse messaging blocks as records rather than independent lines. Reject
   unknown kinds/fields, missing or duplicate fields, invalid order, malformed
   values, and unterminated blocks with `ARKAPI004`.
5. Preserve logical names and generic declaration syntax established by prior
   pre-release tasks.
6. Compute API drift by canonical block/field so diagnostics identify the
   changed field and owning contract, participant, or network.
7. Reject legacy one-line messaging entries with a message explaining the
   one-time soft break and exact regeneration workflow.
8. Keep `CONTRACT`, `REBUS`, `ENUM`, and `EVOLVABLE-ENUM` parsing and output
   byte-for-byte unchanged.
9. Regenerate every affected baseline only from emitted current snapshots after
   reviewing the multiline diff.
10. Update guides and fixtures with generated examples; do not hand-author
    baseline values.

## Core code shapes

Conceptual grammar; exact delimiter is selected and snapshot-locked by this
task:

```text
PARTICIPANT MyApp.PrintingParticipant
  network: MyApp.BookNetwork
  identity: printing
  processes: books.print
  publishes: -
  subscribes: books.printed
  serializers: json|msgpack
  default: json
END
```

Every field is deterministic. A change to `subscribes` changes one line rather
than replacing the complete participant record.

## Guide contribution

Update analyzer and API-surface guides with the block grammar, deterministic
ordering, field ownership, one-time migration failure, baseline regeneration,
and review workflow.

## Sample extension

Regenerate the Book application baseline after AZM-17 through AZM-22. Include
review examples showing a one-field contract change and its focused diff.

## Required test coverage

- All four messaging kinds emit the exact multiline grammar.
- Empty and populated fields are deterministic and ordinally sorted.
- One metadata change affects only its field line.
- Missing, duplicate, unknown, reordered, and malformed fields produce
  `ARKAPI004`.
- Unterminated and nested blocks are rejected.
- Drift diagnostics map each field to the correct CLR declaration.
- Legacy one-line messaging records fail with migration guidance.
- Non-messaging snapshot formats remain unchanged.
- Repeated and cross-target generation is byte-for-byte deterministic.
- All repository baselines are generated output and accepted after review.

## Outcomes

- Messaging API drift is easy to identify and review.
- Snapshot parsing remains strict and deterministic.
- The one-time soft break occurs before the first package release.

## Acceptance

- [ ] Messaging snapshots use one reviewed multiline block grammar.
- [ ] Parser validation and declaration-local drift diagnostics cover every
  field.
- [ ] Legacy one-line messaging records fail with actionable migration guidance.
- [ ] Non-messaging formats are unchanged.
- [ ] All baselines, guides, and generator snapshots are updated.
- [ ] The [task board](../README.md) status for AZM-23 is updated to this task's acceptance state.
- [ ] `dotnet build Ark.Tools.slnx --configuration Debug` succeeds with zero warnings.
- [ ] `dotnet test Ark.Tools.slnx --no-build --configuration Debug --minimum-expected-tests 1` passes.
