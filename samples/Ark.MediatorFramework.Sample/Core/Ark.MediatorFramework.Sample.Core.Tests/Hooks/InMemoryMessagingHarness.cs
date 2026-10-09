// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Core.Application.JsonContext;
using Ark.Tools.MediatorFramework.Messaging;

using Microsoft.Extensions.DependencyInjection;

using System.Text.Json;

namespace Ark.MediatorFramework.Sample.Core.Tests.Hooks;

/// <summary>Shared in-memory topology and JSON options for the sample participants.</summary>
public static class InMemoryMessagingHarness
{
    /// <summary>Gets the participant queues in the sample network.</summary>
    public static IReadOnlyList<string> Queues { get; } =
    [
        SampleMessagingParticipant.Identity,
        SampleMessagingNotificationParticipant.Identity,
        SampleMessagingAuditParticipant.Identity,
    ];

    /// <summary>Creates queues, the completed-print topic, and both subscriptions.</summary>
    /// <remarks>
    /// Receivers also provision these resources when they start; the harness creates them up front so the
    /// topology exists before any participant starts.
    /// </remarks>
    /// <param name="transport">The shared in-memory transport.</param>
    /// <param name="ctk">The cancellation token.</param>
    public static async Task EnsureTopologyAsync(InMemoryMessagingTransport transport, CancellationToken ctk = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var maximumDeliveryCount = new SampleMessagingRetryPolicy().MaximumDeliveryCount * 2;
        foreach (var queue in Queues)
            await transport.EnsureQueueAsync(queue, maximumDeliveryCount, queue, ctk).ConfigureAwait(false);
        var topic = SampleMessagingNetwork.Registry.GetDestination<BookPrintCompleted>();
        await transport.EnsureTopicAsync(topic, SampleMessagingParticipant.Identity, ctk).ConfigureAwait(false);
        foreach (var subscriber in new[] { SampleMessagingNotificationParticipant.Identity, SampleMessagingAuditParticipant.Identity })
        {
            await transport.EnsureSubscriptionAsync(
                new MessagingSubscriptionResource(topic, subscriber, subscriber, maximumDeliveryCount, subscriber),
                ctk).ConfigureAwait(false);
        }
    }

    /// <summary>Configures the messaging JSON codec with the application source-generated context.</summary>
    /// <param name="services">The participant service collection.</param>
    public static void ConfigureJson(IServiceCollection services)
    {
        services.Configure<JsonSerializerOptions>(static options =>
        {
            // Same settings as the hosts: the Ark defaults (camelCase, EvolvableEnum and NodaTime converters).
            options.ConfigureArkDefaults();
            options.RespectNullableAnnotations = true;
            options.RespectRequiredConstructorParameters = true;
            options.TypeInfoResolver = ApplicationJsonSerializerContext.Default;
        });
    }
}
