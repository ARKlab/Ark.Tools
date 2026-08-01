# API-surface snapshots

The API-surface generator records the public shape generated from mediator
contracts. It protects HTTP routes, gRPC methods and protobuf fields, Rebus
queues, transport groups, and lifetime/version metadata from unnoticed changes.
It is an approval gate: it tells the team that the generated surface changed; it
does not decide whether that change is compatible.

## Enable, disable, and locate the generated file

The package enables snapshot generation by default. Set the MSBuild property in
the application project to control it:

| Setting | Default | Effect | Use it when |
| --- | --- | --- | --- |
| `ArkApiSurfaceEnabled` | `true` | Generates the current surface and compares it with a baseline when one exists. | Normal application builds. |
| `ArkApiSurfaceEnabled=false` | — | Does not generate or compare snapshots. | A temporary local experiment before a public API exists. Do not use it to accept a released API change. |
| `ArkApiSurface.txt` | absent | The accepted, committed baseline in the application project directory. | The project is ready to track a public surface. |
| `obj/<tfm>/ArkApiSurface.current.txt` | generated | The current surface emitted during the build. Exact `obj` path depends on the target framework and configuration. | Review and copy this file after accepting a deliberate change. |

Start tracking after the initial surface is ready:

```xml
<!-- MyApplication.csproj: optional; this is the default -->
<PropertyGroup>
  <ArkApiSurfaceEnabled>true</ArkApiSurfaceEnabled>
</PropertyGroup>
```

```bash
dotnet build src/MyApplication/MyApplication.csproj
cp src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt \
   src/MyApplication/ArkApiSurface.txt
git add src/MyApplication/ArkApiSurface.txt
```

Use the generated file as the source to copy. Do not hand-edit a baseline:
hand edits can hide a generator change and will be replaced on the next review.
The sample's application project and its generated `obj` output are also a
working reference for the expected layout.

## Read the output

The exact ordering is deterministic. A small application can produce output
like this:

```text
CONTRACT CreateGreetingRequest -> GreetingResponse [group=Greetings] [http=POST /api/v{version}/greetings] [version=1+] [grpc=CreateGreeting] [grpc-version=1+]
CONTRACT CreateGreetingRequest.Name : string
CONTRACT GreetingResponse
CONTRACT GreetingResponse.Id : Guid
CONTRACT GreetingResponse.Message : string
CONTRACT GreetingResponse.Status : EvolvableEnum<GreetingStatus>
EVOLVABLE-ENUM GreetingStatus.NOT_SET=0
EVOLVABLE-ENUM GreetingStatus.Active=1
REBUS CreateGreetingRequest -> queue:greetings
```

The `CONTRACT` line identifies the request, result, route, gRPC method, group,
and version range. Following lines describe public members and protobuf tags.
`ENUM Type.Member=value` and `EVOLVABLE-ENUM Type.Member=value` lines list
every member and numeric value of a plain enum or an
`EvolvableEnum<TEnum>`-wrapped enum reached from a contract, so adding,
removing, or renumbering a member is a visible diff. The `REBUS` line
describes the generated queue route. The baseline covers:

| Change | Detected | Typical decision |
| --- | --- | --- |
| Add, remove, or rename an HTTP route/method | Yes | Add a version or retain the old route. |
| Change an HTTP or gRPC version range | Yes | Confirm the retirement and migration plan. |
| Add, remove, rename, or change the type of a public contract member | Yes | Preserve released protobuf tags; use a new optional tag for additive data. |
| Change only a protobuf member number | No | Review generated proto/schema changes separately; snapshots record the public contract shape, not protobuf tag numbers. |
| Add, remove, rename, or renumber an `enum`/`EvolvableEnum<TEnum>` member | Yes | For a strict enum this is a breaking wire change; for an evolvable enum, adding a member is safe for unknown-value-tolerant clients, but removing or renumbering one is not. |
| Change the gRPC service/method name or API group | Yes | Treat it as a consumer-visible wire change. |
| Change a `RebusMessage` owner queue | Yes | Plan routing and consumer deployment together. |
| Change handler implementation only | No | No public generated surface has changed. |

## Respond to diagnostics

| Diagnostic | Meaning | Resolution |
| --- | --- | --- |
| `ARKAPI001` | Tracking is enabled but `ArkApiSurface.txt` is missing. | Build, copy `obj/.../ArkApiSurface.current.txt` to the project directory, review it, then commit it. |
| `ARKAPI002` | The committed baseline differs from the generated surface. | Inspect the diff. Make a compatibility/versioning decision. Copy the generated file over the baseline only after approval. |

Example failure:

```text
error ARKAPI002: Contract 'CreateGreetingRequest' has changed since the last
accepted snapshot. Update ArkApiSurface.txt to accept this change.
```

An intentional addition is accepted only after review:

```bash
diff -u src/MyApplication/ArkApiSurface.txt \
  src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt
cp src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt \
  src/MyApplication/ArkApiSurface.txt
git add src/MyApplication/ArkApiSurface.txt
```

Never update the baseline simply to make `ARKAPI002` disappear. First classify
the change. A changed route, protobuf type/tag, removed member, queue, status
behavior, authorization boundary, or semantic meaning normally needs a new
version and consumer communication. Keep the snapshot enabled in CI so the
same review gate applies to every build.

Architecture rationale: [design.md](../design.md).
