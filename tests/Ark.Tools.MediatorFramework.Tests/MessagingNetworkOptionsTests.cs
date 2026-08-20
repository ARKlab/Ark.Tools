// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework;
using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using System.Reflection;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies shared messaging network configuration.</summary>
[TestClass]
public sealed class MessagingNetworkOptionsTests
{
    [TestMethod]
    public void ResolvesIdentityAndCopiesOpaqueMembers()
    {
        var members = new[] { typeof(MemberMarker) };
        var options = new MessagingNetworkOptions(
            typeof(BookNetwork),
            new MessagingNetworkAttribute
            {
                Members = members,
                Requires = MessagingCapabilities.Receive | MessagingCapabilities.PubSub
            });

        options.NetworkIdentity.Should().Be(typeof(BookNetwork).FullName);
        options.Members.Should().ContainSingle();
        (options.Members[0] == typeof(MemberMarker)).Should().BeTrue();
        options.MaximumTransportPayloadBytes.Should().Be(240_000);
        members[0] = typeof(BookNetwork);
        (options.Members[0] == typeof(MemberMarker)).Should().BeTrue();
    }

    [TestMethod]
    public void NetworkAttributeHasTheCompleteSharedApi()
    {
        typeof(MessagingNetworkAttribute).GetCustomAttribute<AttributeUsageAttribute>()
            .Should().BeEquivalentTo(new AttributeUsageAttribute(AttributeTargets.Class)
            {
                AllowMultiple = false,
                Inherited = false
            });

        typeof(MessagingNetworkAttribute).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.DeclaringType == typeof(MessagingNetworkAttribute))
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "Members",
                "Requires",
                "MaximumTransportPayloadBytes",
                "MaximumDecompressedPayloadBytes",
                "DataBusOffloadThresholdBytes",
                "DataBusMaximumAttachmentBytes",
                "MaximumSchedulingDelay",
                "ResourceLifecycle",
                "ConnectionConfigurationKey",
                "ManagedIdentityConfigurationKey");
    }

    [TestMethod]
    public void NetworkAttributeUsesTheDocumentedDefaults()
    {
        var declaration = new MessagingNetworkAttribute();

        declaration.Members.Should().BeEmpty();
        declaration.Requires.Should().Be(MessagingCapabilities.None);
        declaration.MaximumTransportPayloadBytes.Should().Be(240_000);
        declaration.MaximumDecompressedPayloadBytes.Should().Be(1_000_000);
        declaration.DataBusOffloadThresholdBytes.Should().Be(200_000);
        declaration.DataBusMaximumAttachmentBytes.Should().Be(50_000_000);
        declaration.MaximumSchedulingDelay.Should().Be(TimeSpan.FromDays(7));
        declaration.ResourceLifecycle.Should().Be(MessagingResourceLifecycle.CreateIfMissing);
        declaration.ConnectionConfigurationKey.Should().BeNull();
        declaration.ManagedIdentityConfigurationKey.Should().BeNull();
    }

    [TestMethod]
    public void MessagingCapabilitiesExposeOnlyOptionalFlags()
    {
        typeof(MessagingCapabilities).GetCustomAttribute<FlagsAttribute>().Should().NotBeNull();

        ((int)MessagingCapabilities.None).Should().Be(0);
        ((int)MessagingCapabilities.Receive).Should().Be(1);
        ((int)MessagingCapabilities.PubSub).Should().Be(2);
        ((int)MessagingCapabilities.ScheduledSend).Should().Be(4);
        Enum.GetNames<MessagingCapabilities>().Should().Equal("None", "Receive", "PubSub", "ScheduledSend");
    }

    [TestMethod]
    public void ResourceLifecycleExposesTheDocumentedValues()
    {
        Enum.GetValues<MessagingResourceLifecycle>()
            .Should().Equal(MessagingResourceLifecycle.CreateIfMissing, MessagingResourceLifecycle.External);
    }

    [TestMethod]
    public void OptionsExposeTheCompleteImmutableSharedApi()
    {
        typeof(MessagingNetworkOptions).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "NetworkType",
                "NetworkIdentity",
                "Members",
                "Requires",
                "MaximumTransportPayloadBytes",
                "MaximumDecompressedPayloadBytes",
                "DataBusOffloadThresholdBytes",
                "DataBusMaximumAttachmentBytes",
                "MaximumSchedulingDelay",
                "ResourceLifecycle",
                "ConnectionConfigurationKey",
                "ManagedIdentityConfigurationKey");

        typeof(MessagingNetworkOptions).GetConstructor([typeof(Type), typeof(MessagingNetworkAttribute)])
            .Should().NotBeNull();

        typeof(MessagingNetworkOptions).GetMethod(
                nameof(MessagingNetworkOptions.Validate),
                BindingFlags.Instance | BindingFlags.Public,
                [typeof(MessagingCapabilities)])
            .Should().NotBeNull();
    }

    [TestMethod]
    public void CopiesEverySharedSettingIntoImmutableOptions()
    {
        var declaration = new MessagingNetworkAttribute
        {
            Members = [typeof(MemberMarker)],
            Requires = MessagingCapabilities.Receive | MessagingCapabilities.PubSub | MessagingCapabilities.ScheduledSend,
            MaximumTransportPayloadBytes = 123,
            MaximumDecompressedPayloadBytes = 456,
            DataBusOffloadThresholdBytes = 789,
            DataBusMaximumAttachmentBytes = 987,
            MaximumSchedulingDelay = TimeSpan.FromMinutes(12),
            ResourceLifecycle = MessagingResourceLifecycle.External,
            ConnectionConfigurationKey = "Transport",
            ManagedIdentityConfigurationKey = "Identity"
        };

        var options = new MessagingNetworkOptions(typeof(BookNetwork), declaration);

        options.NetworkType.Should().Be<BookNetwork>();
        options.NetworkIdentity.Should().Be(typeof(BookNetwork).FullName);
        options.Members.Should().Equal(typeof(MemberMarker));
        options.Requires.Should().Be(declaration.Requires);
        options.MaximumTransportPayloadBytes.Should().Be(123);
        options.MaximumDecompressedPayloadBytes.Should().Be(456);
        options.DataBusOffloadThresholdBytes.Should().Be(789);
        options.DataBusMaximumAttachmentBytes.Should().Be(987);
        options.MaximumSchedulingDelay.Should().Be(TimeSpan.FromMinutes(12));
        options.ResourceLifecycle.Should().Be(MessagingResourceLifecycle.External);
        options.ConnectionConfigurationKey.Should().Be("Transport");
        options.ManagedIdentityConfigurationKey.Should().Be("Identity");
    }

    [TestMethod]
    public void ValidateRejectsMissingCapability()
    {
        var options = new MessagingNetworkOptions(
            typeof(BookNetwork),
            new MessagingNetworkAttribute { Requires = MessagingCapabilities.PubSub });

        var action = () => options.Validate(MessagingCapabilities.Receive);

        action.Should().Throw<InvalidOperationException>().Which.Message.Should()
            .Be($"Network '{typeof(BookNetwork).FullName}' requires unsupported capability 'PubSub'.");
    }

    [TestMethod]
    public void ValidateAcceptsACapabilitySuperset()
    {
        var options = new MessagingNetworkOptions(
            typeof(BookNetwork),
            new MessagingNetworkAttribute
            {
                Requires = MessagingCapabilities.Receive | MessagingCapabilities.PubSub
            });

        var action = () => options.Validate(
            MessagingCapabilities.Receive
                | MessagingCapabilities.PubSub
                | MessagingCapabilities.ScheduledSend);

        action.Should().NotThrow();
    }

    [TestMethod]
    public void ValidateNamesEveryMissingCapability()
    {
        foreach (var capability in new[]
        {
            MessagingCapabilities.Receive,
            MessagingCapabilities.PubSub,
            MessagingCapabilities.ScheduledSend
        })
        {
            var options = new MessagingNetworkOptions(
                typeof(BookNetwork),
                new MessagingNetworkAttribute { Requires = capability });

            var action = () => options.Validate(MessagingCapabilities.None);

            action.Should().Throw<InvalidOperationException>().Which.Message.Should()
                .Contain(capability.ToString());
        }
    }

    [TestMethod]
    public void NetworkHasNoParticipantOwnedSettings()
    {
        var properties = typeof(MessagingNetworkAttribute).GetProperties()
            .Select(property => property.Name)
            .ToArray();
        properties.Should().NotContain("Serialization");
        properties.Should().NotContain("Compression");
        properties.Should().NotContain("Retry");
        properties.Should().NotContain("IncomingSteps");
        properties.Should().NotContain("OutgoingSteps");
        properties.Should().NotContain("DataBusRetention");
        properties.Should().NotContain(property => property.Contains("AzureServiceBus", StringComparison.Ordinal));
        properties.Should().NotContain(property => property.Contains("StorageQueue", StringComparison.Ordinal));
    }

    private sealed class BookNetwork;

    private sealed class MemberMarker;
}
