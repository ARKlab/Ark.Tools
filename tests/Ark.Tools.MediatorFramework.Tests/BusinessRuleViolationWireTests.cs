// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text;

using Ark.MediatorFramework;

using AwesomeAssertions;

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
            PayloadJson = "{\"Greeting\":\"hello\"}",
        };
        using var stream = new MemoryStream();

        Serializer.Serialize(stream, expected);
        stream.Position = 0;
        var actual = Serializer.Deserialize<ArkBusinessRuleViolation>(stream);

        actual.Type.Should().Be(expected.Type);
        actual.Title.Should().Be(expected.Title);
        actual.Status.Should().Be(expected.Status);
        Encoding.UTF8.GetBytes(actual.PayloadJson).Should().Equal(Encoding.UTF8.GetBytes(expected.PayloadJson));
    }
}
