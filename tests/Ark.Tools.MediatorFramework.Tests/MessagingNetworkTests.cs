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

        options.NetworkType.Should().Be<TestNetwork>();
        options.Requires.Should().Be(MessagingCapabilities.Receive | MessagingCapabilities.PubSub);
        options.MaximumTransportPayloadBytes.Should().Be(240_000);
        options.Serializers.Should().ContainSingle().Which.Should().Be(SerializationProtocol.Json);
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
        Action action = () => _ = new MessagingNetworkOptions(
            typeof(TestNetwork),
            MessagingCapabilities.None,
            new[] { SerializationProtocol.Json },
            SerializationProtocol.Json,
            CompressionAlgorithm.None,
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

    [TestMethod]
    public void ResolvesRegisteredContractMetadata()
    {
        var options = MessagingNetworkDescriptor.Resolve(typeof(ContractNetwork));

        options.Contracts.Should().ContainSingle();
        options.Contracts[0].ContractType.Should().Be<ContractMessage>();
        options.Contracts[0].Name.Should().Be("contract_message");
        options.Contracts[0].FormerNames.Should().ContainSingle().Which.Should().Be("old_contract_message");
    }

    [TestMethod]
    public void NormalizesGenericContractNamesWithoutAritySuffix()
    {
        MessagingContractDescriptor.Normalize(typeof(GenericContract<>))
            .Should().Be("ark.tools.mediator_framework.tests.messaging_network_tests.generic_contract");
    }

    [TestMethod]
    public void RejectsNegativeSchedulingDelayWithCorrectParameterName()
    {
        Action action = () => _ = new MessagingNetworkOptions(
            typeof(TestNetwork),
            MessagingCapabilities.None,
            new[] { SerializationProtocol.Json },
            SerializationProtocol.Json,
            CompressionAlgorithm.None,
            0,
            240_000,
            1_000_000,
            240_000,
            1_000_000,
            new ValidRetryPolicy(),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(-1),
            MessagingResourceLifecycle.External,
            "connection",
            "identity");

        action.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("maximumSchedulingDelay");
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

    private sealed class ValidRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 2;
        public bool SecondLevelRetriesEnabled => true;
        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(1);
        public TimeSpan RetryDelay => TimeSpan.Zero;
    }

    [Message(
        OwnerQueue = "contracts",
        Name = "contract_message",
        FormerNames = new[] { "old_contract_message" })]
    private sealed record ContractMessage;

    private sealed class GenericContract<T>
    {
    }

    [MessagingNetwork(MessagingCapabilities.Receive,
        Contracts = new[] { typeof(ContractMessage) })]
    private sealed class ContractNetwork
    {
    }
}
