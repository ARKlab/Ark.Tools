// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework;
using Ark.MediatorFramework.Messaging;

using AwesomeAssertions;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies transport-neutral messaging network configuration.</summary>
[TestClass]
public sealed class MessagingNetworkTests
{
    [TestMethod]
    public void ResolvesProfileWithDeterministicIdentityAndDefaults()
    {
        var options = MessagingNetworkDescriptor.Resolve(typeof(TestNetwork));

        options.NetworkType.Should().Be(typeof(TestNetwork));
        options.Requires.Should().Be(MessagingCapabilities.Receive | MessagingCapabilities.PubSub);
        options.MaximumTransportPayloadBytes.Should().Be(240_000);
        options.Serializers.Should().ContainSingle().Which.Should().Be(MessagingSerializationProtocol.Json);
    }

    [TestMethod]
    public void AcceptsTransportSuperset()
    {
        var options = MessagingNetworkDescriptor.Resolve(typeof(TestNetwork));

        options.Validate(MessagingCapabilities.Receive | MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend);
    }

    [TestMethod]
    public void RejectsMissingTransportCapability()
    {
        var options = MessagingNetworkDescriptor.Resolve(typeof(TestNetwork));

        Action action = () => options.Validate(MessagingCapabilities.Receive);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*PubSub*");
    }

    [TestMethod]
    public void RejectsSecondLevelPolicyWithOneDelivery()
    {
        Action action = () => new MessagingNetworkOptions(
            typeof(TestNetwork),
            MessagingCapabilities.None,
            new[] { MessagingSerializationProtocol.Json },
            MessagingSerializationProtocol.Json,
            MessagingCompressionAlgorithm.None,
            0,
            240_000,
            1_000_000,
            240_000,
            1_000_000,
            new InvalidRetryPolicy(),
            TimeSpan.Zero,
            TimeSpan.Zero,
            MessagingResourceLifecycle.External,
            "connection",
            "identity");

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [MessagingNetwork(MessagingCapabilities.Receive | MessagingCapabilities.PubSub)]
    private sealed class TestNetwork
    {
    }

    private sealed class InvalidRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 1;
        public bool SecondLevelRetriesEnabled => true;
        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);
        public TimeSpan RetryDelay => TimeSpan.Zero;
    }
}
