// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Provides Azure transport and DataBus fluent composition helpers.</summary>
public static class AzureFluentMessagingCompositionExtensions
{
    /// <summary>Uses Azure Service Bus.</summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="client">The configured Service Bus client.</param>
    /// <param name="configure">
    /// Optional entity-shaping options. The declared lock duration is what the renewer plans
    /// against, so declaring it here keeps provisioning and processing reading the same number.
    /// </param>
    /// <returns>This builder.</returns>
    public static MessagingTransportBuilder UseServiceBus(
        this MessagingTransportBuilder builder,
        ServiceBusClient client,
        Action<ServiceBusMessagingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);
        var options = new ServiceBusMessagingOptions();
        configure?.Invoke(options);
        options.Validate();
#pragma warning disable CA2000 // Ownership is transferred to the composition service provider.
        builder.Use(new ServiceBusMessagingTransport(client, lockDuration: options.LockDuration));
#pragma warning restore CA2000
        return builder;
    }

    /// <summary>Uses Azure Storage Queues.</summary>
    /// <param name="builder">The transport builder.</param>
    /// <param name="client">The configured Queue Storage service client.</param>
    /// <returns>This builder.</returns>
    public static MessagingTransportBuilder UseStorageQueue(
        this MessagingTransportBuilder builder,
        QueueServiceClient client)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(client);
        builder.Use(new StorageQueueMessagingTransport(client));
        return builder;
    }

    /// <summary>Uses Azure Blob Storage.</summary>
    /// <param name="builder">The DataBus builder.</param>
    /// <param name="options">The Azure Blob DataBus options.</param>
    /// <returns>This builder.</returns>
    public static MessagingDataBusBuilder UseAzureBlob(
        this MessagingDataBusBuilder builder,
        AzureBlobDataBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        builder.Use(new AzureBlobMessagingDataBus(options));
        return builder;
    }

    /// <summary>Uses an Azure Blob DataBus.</summary>
    /// <typeparam name="TNetwork">The generated messaging network declaration.</typeparam>
    /// <typeparam name="TParticipant">The generated participant declaration.</typeparam>
    /// <param name="builder">The messaging mode builder.</param>
    /// <param name="options">The Azure Blob DataBus options.</param>
    /// <returns>This builder.</returns>
    public static MessagingModeBuilder<TNetwork, TParticipant> UseAzureBlobDataBus<TNetwork, TParticipant>(
        this MessagingModeBuilder<TNetwork, TParticipant> builder,
        AzureBlobDataBusOptions options)
        where TNetwork : class, IMessagingNetwork<TNetwork>
        where TParticipant : class, IMessagingParticipant<TParticipant>
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UseDataBus(new AzureBlobMessagingDataBus(options));
    }
}
