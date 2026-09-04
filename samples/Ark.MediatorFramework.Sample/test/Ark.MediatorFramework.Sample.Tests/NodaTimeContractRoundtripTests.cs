// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.API;
using Ark.MediatorFramework.Sample.API.JsonContext;

using Ark.Tools.Nodatime.Protobuf;

using AwesomeAssertions;

using MessagePack;
using MessagePack.Resolvers;

using NodaTime;

using ProtoBuf.Meta;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Tests;

/// <summary>Verifies that the sample's NodaTime contract preserves values on every wire format.</summary>
[TestClass]
public sealed class NodaTimeContractRoundtripTests
{
    /// <summary>Round-trips an audit record through JSON, MessagePack, and protobuf.</summary>
    [TestMethod]
    public void AuditRecord_roundtrips_across_supported_wire_formats()
    {
        var original = new AuditRecord
        {
            Id = Guid.Parse("4f8f3b0a-7c5a-4d3e-9a1b-2c6d8e0f1234"),
            UserId = "roundtrip-user",
            EntityType = "Book",
            Identifier = "book-42",
            Operation = "Updated",
            Timestamp = Instant.FromUtc(2026, 7, 27, 12, 34, 56).PlusNanoseconds(789),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(
            original,
            SampleApiJsonSerializerContext.Default.AuditRecord);
        var jsonClone = JsonSerializer.Deserialize(
            json,
            SampleApiJsonSerializerContext.Default.AuditRecord);

        var messagePackOptions = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(
                MessagePack.NodaTime.NodatimeResolver.Instance,
                DynamicEnumAsStringResolver.Instance,
                StandardResolver.Instance));
        var messagePack = MessagePackSerializer.Serialize(original, messagePackOptions);
        var messagePackClone = MessagePackSerializer.Deserialize<AuditRecord>(
            messagePack,
            messagePackOptions);

        var protobufModel = RuntimeTypeModel.Create();
        protobufModel.AddNodaTimeSurrogates();
        protobufModel.Add(typeof(AuditRecord), true);
        using var protobufStream = new MemoryStream();
        protobufModel.Serialize(protobufStream, original);
        protobufStream.Position = 0;
        var protobufClone = (AuditRecord)protobufModel.Deserialize(
            protobufStream,
            null,
            typeof(AuditRecord));

        jsonClone.Should().Be(original);
        messagePackClone.Should().Be(original);
        protobufClone.Should().Be(original);
    }
}
