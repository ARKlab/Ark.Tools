# GEN-04 — Remove sample `Documents.proto` from the framework generator (A6)

**Category**: generator-dx · **Priority**: **Release blocker** · **Scope**: FRAMEWORK + SAMPLE

## Problem

The gRPC streaming upload service is hand-written in the sample
(`samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/DocumentsGrpcService.cs`)
and its **sample-specific `Documents.proto` content is hardcoded inside the framework generator**
(`src/mediator-framework/Ark.Tools.MediatorFramework.Grpc.Generators/GrpcEndpointGenerator.cs`,
proto-export emission around the streaming section). Every consumer of the framework gets the
sample's Documents proto leaked into their proto export.

## Steps

1. Remove the hardcoded `Documents.proto` block from `GrpcEndpointGenerator.cs`.
2. Provide a general mechanism for hosts to contribute hand-written protos to the export:
   the proto-export infrastructure (`src/mediator-framework/Ark.Tools.MediatorFramework.Grpc/ArkProtoExport.cs`
   and `buildTransitive/Ark.Tools.MediatorFramework.Grpc.targets`) should pick up additional `.proto`
   files declared by the host project (e.g. an `ArkAdditionalProto` MSBuild item or copying protos
   from a known content folder into `ArkExportProtoDir`). Follow the existing MSBuild pattern in the
   `.targets` file.
3. Move `Documents.proto` content into the sample project as a real `.proto` file registered via the
   new mechanism, keeping `DocumentsGrpcService.cs` working and the exported artifact set identical
   for the sample.
4. Verify a consumer **without** the sample gets no Documents proto: add/extend a generator test that
   runs the gRPC generator on a minimal compilation and asserts the export contains only its own
   contracts.
5. If FW-04 (generated download/upload streaming) lands first, coordinate: the hand-written service
   may shrink or disappear; this task is still required for whatever remains hand-written.

## Outcomes

- Framework generator emits only consumer-derived protos; hosts have a supported way to add hand-written protos to the export.

## Acceptance

- [ ] No sample-specific string content remains in any framework generator.
- [ ] Sample proto export unchanged (same files/services available to grpcui, per `samples/Ark.MediatorFramework.Sample/README.md` workflow).
- [ ] Generator test proves a clean consumer exports no Documents proto.
- [ ] Full solution build + tests green.
