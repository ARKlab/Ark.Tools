# API-surface snapshots

The API-surface generator records the public shape generated from mediator
contracts. It protects HTTP routes, gRPC methods and protobuf fields, messaging
names and ownership, Rebus queues, transport groups, and lifetime/version
metadata from unnoticed changes.
It is an approval gate: it tells the team that the generated surface changed; it
does not decide whether that change is compatible.

## Enable, disable, and locate the generated file

The package enables API-surface comparison by default, but does not emit
compiler-generated source files by default. Set the MSBuild properties in the
application project or pass them transiently on the command line:

| Setting | Default | Effect | Use it when |
| --- | --- | --- | --- |
| `ArkApiSurfaceEnabled` | `true` | Generates the current surface and compares it with a baseline when one exists. | Normal application builds. |
| `ArkApiSurfaceEnabled=false` | — | Does not generate or compare snapshots. | A temporary local experiment before a public API exists. Do not use it to accept a released API change. |
| `EmitCompilerGeneratedFiles=true` | `false` | Emits compiler-generated `.g.cs` files; the package target supplies `$(BaseIntermediateOutputPath)generated` when no path is provided and enables the `ArkApiSurface.current.txt` convenience copy target. | Transiently inspect or bootstrap a snapshot. |
| `ArkApiSurface.txt` | absent | The accepted, committed baseline in the application project directory. | The project is ready to track a public surface. |
| `obj/<Configuration>/<TargetFramework>/ArkApiSurface.current.txt` | generated only when `EmitCompilerGeneratedFiles=true` | The current surface copied during the build. Its path follows the SDK intermediate-output path. | Review and copy this file after accepting a deliberate change. |

Start tracking after the initial surface is ready:

```xml
<!-- MyApplication.csproj: optional; this is the default -->
<PropertyGroup>
  <ArkApiSurfaceEnabled>true</ArkApiSurfaceEnabled>
</PropertyGroup>
```
Source: [`Ark.MediatorFramework.Sample.API.csproj`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/Ark.MediatorFramework.Sample.API.csproj)

```bash
dotnet build src/MyApplication/MyApplication.csproj \
  -p:EmitCompilerGeneratedFiles=true
cp src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt \
   src/MyApplication/ArkApiSurface.txt
git add src/MyApplication/ArkApiSurface.txt
```
Source: [`ArkApiSurface.txt`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/ArkApiSurface.txt)

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
EVENT MyApp.Messages.GreetingCompleted
  name: greeting_completed
  former: -
END
MESSAGE MyApp.Messages.CompleteGreeting
  name: complete_greeting
  former:
    - old_complete_greeting
END
NETWORK MyApp.Messages.GreetingNetwork
  members:
    - MyApp.Messages.GreetingProcessorParticipant
    - MyApp.Messages.GreetingPublisherParticipant
  requires:
    - pubsub
    - receive
END
PARTICIPANT MyApp.Messages.GreetingProcessorParticipant
  network: MyApp.Messages.GreetingNetwork
  identity: greeting-processor
  processes:
    - complete_greeting
  publishes: -
  subscribes:
    - greeting_completed
  serializers:
    - json
  default: json
END
```
Source: [`ArkApiSurface.txt`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/ArkApiSurface.txt)

The `CONTRACT` line identifies the request, result, route, gRPC method, group,
and version range. Following lines describe public members and protobuf tags.
`ENUM Type.Member=value` and `EVOLVABLE-ENUM Type.Member=value` lines list
every member and numeric value of a plain enum or an
`EvolvableEnum<TEnum>`-wrapped enum reached from a contract, so adding,
removing, or renumbering a member is a visible diff. The `REBUS` line
describes generated Rebus queue routing. `MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` are deterministic multiline
blocks. Each block has a fixed field order and ends with `END`; set values are
ordinal-sorted with one list item per line, and empty sets are `-`. They record logical names and aliases,
never transport-mapped entity names, plus participant membership, identity,
ownership, subscriptions, serializers, and network capabilities. These
declaration entries may feed either Rebus or native generation, but do not imply
wire compatibility.
The baseline covers:

| Change | Detected | Typical decision |
| --- | --- | --- |
| Add, remove, or rename an HTTP route/method | Yes | Add a version or retain the old route. |
| Change an HTTP or gRPC version range | Yes | Confirm the retirement and migration plan. |
| Add, remove, rename, or change the type of a public contract member | Yes | Preserve released protobuf tags; use a new optional tag for additive data. |
| Change only a protobuf member number | No | Review generated proto/schema changes separately; snapshots record the public contract shape, not protobuf tag numbers. |
| Add, remove, rename, or renumber an `enum`/`EvolvableEnum<TEnum>` member | Yes | For a strict enum this is a breaking wire change; for an evolvable enum, adding a member is safe for unknown-value-tolerant clients, but removing or renumbering one is not. |
| Change the gRPC service/method name or API group | Yes | Treat it as a consumer-visible wire change. |
| Change a `RebusMessage` owner queue | Yes | Plan routing and consumer deployment together. |
| Change a logical message/event name or alias | Yes | Drain old native messages; changing an event name also requires an explicit topic/subscription migration. |
| Change a participant identity, owner, subscriber, serializer, or network member | Yes | Review the selected topology mode, resources, wire compatibility, and deployment order. |
| Change handler implementation only | No | No public generated surface has changed. |

## Respond to diagnostics

| Diagnostic | Meaning | Resolution |
| --- | --- | --- |
| `ARKAPI001` | Tracking is enabled but `ArkApiSurface.txt` is missing. | Run `dotnet build -p:EmitCompilerGeneratedFiles=true`, copy `obj/.../ArkApiSurface.current.txt` to the project directory, review it, then commit it. |
| `ARKAPI002` | The committed baseline differs from the generated surface. | Run `dotnet build -p:EmitCompilerGeneratedFiles=true`, inspect the generated file, and copy it over the baseline only after approval. |
| `ARKAPI004` | The baseline contains an unknown, malformed, reordered, or legacy messaging entry. | Regenerate `ArkApiSurface.current.txt` and replace the baseline after reviewing the multiline block diff. |

Example failure:

```text
error ARKAPI002: Contract 'CreateGreetingRequest' has changed since the last
accepted snapshot. Run 'dotnet build -p:EmitCompilerGeneratedFiles=true' to
inspect ArkApiSurface.current.txt, then update ArkApiSurface.txt to accept this
change.
```
Source: [`ArkApiSurface.txt`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/ArkApiSurface.txt)

An intentional addition is accepted only after review:

```bash
dotnet build src/MyApplication/MyApplication.csproj \
  -p:EmitCompilerGeneratedFiles=true
diff -u src/MyApplication/ArkApiSurface.txt \
  src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt
cp src/MyApplication/obj/Debug/net10.0/ArkApiSurface.current.txt \
  src/MyApplication/ArkApiSurface.txt
git add src/MyApplication/ArkApiSurface.txt
```
Source: [`ArkApiSurface.txt`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.API/ArkApiSurface.txt)

Never update the baseline simply to make `ARKAPI002` disappear. First classify
the change. A changed route, protobuf type/tag, removed member, queue, status
behavior, authorization boundary, or semantic meaning normally needs a new
version and consumer communication. Keep the snapshot enabled in CI so the
same review gate applies to every build.

## Review generated host code and package APIs

The contract snapshot intentionally represents generated trigger behavior
through its contract and topology declarations rather than trigger
implementation details. Build the consuming Functions project with
`EmitCompilerGeneratedFiles=true`, then inspect the emitted `.g.cs` for the
native trigger type, participant identity queue, connection setting, dispatcher
call, and manual settlement. Copy generated signatures into documentation only
from that output. Generated registry members marked with
`MessagingGeneratedSurfaceAttribute` are omitted because the dedicated
`MESSAGE`, `EVENT`, `PARTICIPANT`, and `NETWORK` lines already represent them.

.NET package validation separately protects public attributes, enums, options,
`IBus`, `MessagingFailed<T>`, pipeline contracts, DataBus abstractions, outbox
APIs, processor hosting, and message-context members. Both gates must pass: the
snapshot protects application wire/topology declarations, while package
validation protects the library CLR surface.

Architecture rationale: [design.md](../design.md).
