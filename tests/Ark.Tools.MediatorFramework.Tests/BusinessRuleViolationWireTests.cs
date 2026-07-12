// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using AwesomeAssertions;

using Ark.Tools.MediatorFramework.Grpc;

using ProtoBuf;

namespace Ark.Tools.MediatorFramework.Tests;

[TestClass]
public sealed class BusinessRuleViolationWireTests
{
    [TestMethod]
    public void ArkBusinessRuleViolationRoundTripsThroughProtoBuf()
    {
        var expected = new ArkBusinessRuleViolation
        {
            Type = "GreetingAlreadyExistsViolation",
            Title = "Greeting already exists",
            Status = 400,
            Detail = "A greeting already exists.",
            Instance = string.Empty,
            Extensions = new Dictionary<string, string>(StringComparer.Ordinal) { ["Name"] = "\"hello\"" },
        };
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, expected);
        stream.Position = 0;
        var actual = Serializer.Deserialize<ArkBusinessRuleViolation>(stream);

        actual.Type.Should().Be(expected.Type);
        actual.Title.Should().Be(expected.Title);
        actual.Status.Should().Be(expected.Status);
        actual.Detail.Should().Be(expected.Detail);
        actual.Instance.Should().BeEmpty();
        actual.Extensions.Should().ContainKey("Name");
        actual.Extensions["Name"].Should().Be(expected.Extensions["Name"]);
    }
}
