# Serialization

The sample uses Ark JSON defaults (camel case, NodaTime, enum members), a
source-generated `System.Text.Json` context, MessagePack negotiation with a
composite resolver, and protobuf surrogates for NodaTime. Apply matching
attributes to contracts that cross each wire.

```csharp
var messagePackResolver = CompositeResolver.Create(
    MessagePack.NodaTime.NodatimeResolver.Instance,
    DynamicEnumAsStringResolver.Instance,
    StandardResolver.Instance);
services.AddMessagePackFormatter(messagePackResolver);
RuntimeTypeModel.Default.AddNodaTimeSurrogates();
```

Source: [`SampleStartup.cs`](../../../samples/Ark.MediatorFramework.Sample/src/Ark.MediatorFramework.Sample.WebInterface/SampleStartup.cs).

`[ProtoContract]`/`[ProtoMember]` define protobuf, and
`[MessagePackObject]`/`[Union]` define MessagePack. JSON polymorphism uses the
Ark converter; the sample's `Shape` hierarchy keeps a named `Kind`
discriminator while protobuf and MessagePack use numbered subtype envelopes.
Use a custom converter/resolver as the escape hatch. Rationale:
[`design.md`](../design.md).
