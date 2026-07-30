# API-surface snapshots

The API-surface generator prevents a contract, route, or gRPC signature from
changing unnoticed. It compares the current generated surface with the accepted
`ArkApiSurface.txt` baseline in the application project directory.

## Establish and review the baseline

1. Build once and copy the generated surface to `ArkApiSurface.txt` in the
   project directory.
2. Commit the file; the package's build assets supply it as an additional file.
3. On later builds, review every `ARKAPI002` change before updating the baseline.

**Outcome:** adding, removing, or changing a route, version range, gRPC method,
message member, or Rebus route fails the build until the approved baseline
records the intentional change.

## Decide before accepting a diff

Classify the change first. An additive change may be acceptable in the current
version. A changed HTTP route, changed protobuf number/type, removed message
member, or altered public behavior requires a compatibility decision and often
a new version. Update `ArkApiSurface.txt` only after that decision and consumer
impact are documented in review.

The snapshot is an approval gate, not a compatibility tool. Do not disable it
to bypass a change. For an unshipped service, establish the first baseline after
the initial public surface is ready; for a shipped service, treat it as part of
the release process.

Architecture rationale: [design.md](../design.md).
