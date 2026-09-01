# SDK-IMP-08 - Compatibility matrix and paired-package release gate

**Category**: validation · **Priority**: release
**Status**: Cancelled

## Cancellation

SDK-IMP-08 is not needed as a separate task.

- The required compatibility and consumer-behavior coverage is already present
  in `tests/Ark.Tools.Sdk.Tests`.
- NuGet archive/content inspection is not part of the required test scope.
- SDK-IMP-09 will use the repository source-build arrangement and will not
  consume a published SDK preview pair.
- No preview package publication or release-gate workflow changes are required
  for this migration.

## Decision

Keep the existing SDK tests as the compatibility proof. Do not add a second
compatibility harness, package archive inspection, preview publication step, or
release workflow gate under this task.

## Acceptance

- [x] Task cancelled because existing tests provide the required coverage and
  SDK-IMP-09 does not require a published preview pair.
