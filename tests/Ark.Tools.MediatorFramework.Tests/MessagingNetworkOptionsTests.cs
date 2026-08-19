// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Messaging;

using AwesomeAssertions;

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
        options.Members.Should().ContainSingle().Which.Should().Be(typeof(MemberMarker));
        options.MaximumTransportPayloadBytes.Should().Be(240_000);
        members[0] = typeof(BookNetwork);
        options.Members[0].Should().Be(typeof(MemberMarker));
    }

    [TestMethod]
    public void ValidateRejectsMissingCapability()
    {
        var options = new MessagingNetworkOptions(
            typeof(BookNetwork),
            new MessagingNetworkAttribute { Requires = MessagingCapabilities.PubSub });

        var action = () => options.Validate(MessagingCapabilities.Receive);

        action.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("PubSub");
    }

    [TestMethod]
    public void NetworkHasNoParticipantOwnedSettings()
    {
        typeof(MessagingNetworkAttribute).GetProperties()
            .Select(property => property.Name)
            .Should()
            .NotContain(new[] { "Serialization", "Compression", "Retry", "IncomingSteps", "OutgoingSteps", "DataBusRetention" });
    }

    private sealed class BookNetwork;

    private sealed class MemberMarker;
}
