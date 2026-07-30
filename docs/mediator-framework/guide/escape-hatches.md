# Escape hatches

Generated wiring is the default, not a restriction. Use a handwritten adapter
when the transport has behavior that cannot be expressed by a contract
attribute, while keeping the core handler reusable.

| Need | Escape hatch |
| --- | --- |
| Custom HTTP binding | Hand-written Minimal API mapping |
| Custom gRPC behavior | Hand-written method in the generated `partial` or service |
| Legacy bus handler | Hand-written `IHandleMessages<T>` |
| Existing MVC surface | Keep controllers and migrate incrementally |
| Multipart shape outside generated binding | `MapArkAttachmentUpload` |

The sample demonstrates a handwritten gRPC service:
[`DocumentsGrpcService.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/DocumentsGrpcService.cs),
and a compatibility MessagePack controller:
[`MessagePackGreetingController.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/MessagePackGreetingController.cs).
For MVC coexistence, follow [`migration-from-mvc.md`](../migration-from-mvc.md).
Rationale: [`design.md`](../design.md).
