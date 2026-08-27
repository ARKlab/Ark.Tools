// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.Storage.Queues;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SimpleInjector;

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Identifies the Azure transport composed by a Functions messaging host.</summary>
public enum MessagingFunctionsRuntimeTransport
{
    /// <summary>Azure Service Bus.</summary>
    AzureServiceBus,

    /// <summary>Azure Storage Queue.</summary>
    AzureStorageQueue
}

/// <summary>Composes generated messaging participants in Azure Functions hosts.</summary>
public static class MessagingFunctionsServiceCollectionExtensions
{
    /// <summary>
    /// Composes the generated participant with an Azure transport selected from configuration.
    /// </summary>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The existing application Simple Injector container.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="manifest">The generated Functions messaging manifest.</param>
    /// <param name="dataBus">The shared network DataBus.</param>
    /// <param name="transport">The runtime Azure transport selection.</param>
    /// <param name="storageQueueHostSettings">Effective Queue host settings, when applicable.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessagingFunctionsHost(
        this IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        IMessagingDataBus dataBus,
        MessagingFunctionsRuntimeTransport transport,
        StorageQueueFunctionsHostSettings? storageQueueHostSettings = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(dataBus);

        var connection = configuration[manifest.ConnectionConfigurationKey];
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Messaging transport configuration '{0}' is required.",
                    manifest.ConnectionConfigurationKey));
        }

        return transport switch
        {
            MessagingFunctionsRuntimeTransport.AzureServiceBus =>
                _addServiceBus(services, container, manifest, dataBus, connection),
            MessagingFunctionsRuntimeTransport.AzureStorageQueue =>
                _addStorageQueue(
                    services,
                    container,
                    manifest,
                    dataBus,
                    connection,
                    storageQueueHostSettings),
            _ => throw new ArgumentOutOfRangeException(nameof(transport))
        };
    }

    /// <summary>
    /// Composes a generated participant over application-created transport services.
    /// </summary>
    /// <param name="services">The Functions service collection.</param>
    /// <param name="container">The existing application Simple Injector container.</param>
    /// <param name="manifest">The generated Functions messaging manifest.</param>
    /// <param name="transport">The application-created transport.</param>
    /// <param name="dataBus">The shared network DataBus.</param>
    /// <param name="management">Optional resource-management seam.</param>
    /// <param name="queueServiceClient">The Queue service client used by Storage Queue settlement.</param>
    /// <param name="storageQueueHostSettings">Effective Queue host settings, when applicable.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessagingFunctionsHost(
        this IServiceCollection services,
        Container container,
        MessagingFunctionsManifest manifest,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IMessagingTransportManagement? management = null,
        QueueServiceClient? queueServiceClient = null,
        StorageQueueFunctionsHostSettings? storageQueueHostSettings = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(dataBus);

        _validateManifest(manifest, transport);
        var descriptor = manifest.Descriptor!;
        _registerSteps(container, manifest.IncomingSteps);
        _registerSteps(container, manifest.OutgoingSteps);
        services.AddArkMessagingParticipant(
            descriptor,
            transport,
            dataBus,
            manifest.OutgoingSteps,
            container.GetInstance);
        services.AddSingleton(manifest);

        if (!descriptor.Receives)
            return services;

        if (transport is StorageQueueMessagingTransport)
        {
            if (queueServiceClient is null)
                throw new InvalidOperationException(
                    "Storage Queue Functions composition requires the QueueServiceClient used for settlement.");
            services.AddSingleton(queueServiceClient);
            if (storageQueueHostSettings is not null)
            {
                services.AddSingleton(storageQueueHostSettings);
                services.AddSingleton<IHostedService, StorageQueueFunctionsHostSettingsValidator>();
            }
            else if (manifest.StrictStorageQueueHostSettings)
            {
                throw new InvalidOperationException(
                    "Strict Storage Queue composition requires effective host settings.");
            }
        }

        services.AddSingleton(serviceProvider => new MessagingHeaderProcessor(
            serviceProvider.GetRequiredService<IMessagingCodecRegistry>(),
            descriptor.Network.NetworkIdentity));
        services.AddSingleton(serviceProvider => new MessagingPayloadReceiver(
            serviceProvider.GetRequiredService<IMessagingDataBus>(),
            descriptor.Network));
        services.AddSingleton(serviceProvider => new MessagingDispatcher(
            container,
            serviceProvider.GetRequiredService<MessagingHeaderProcessor>(),
            serviceProvider.GetRequiredService<MessagingPayloadReceiver>(),
            descriptor.RetryPolicy,
            (logicalName, payload, processor, ctk) =>
                descriptor.Dispatch!(logicalName, payload, processor, ctk),
            descriptor.DispatchFailed is null
                ? null
                : (logicalName, payload, deliveryCount, error, processor, ctk) =>
                    descriptor.DispatchFailed(
                        logicalName,
                        payload,
                        deliveryCount,
                        error,
                        processor,
                        ctk),
            manifest.IncomingSteps,
            container.GetInstance));

        if (manifest.Resources.Lifecycle == MessagingResourceLifecycle.CreateIfMissing)
        {
            management ??= transport as IMessagingTransportManagement;
            if (management is null)
                throw new InvalidOperationException(
                    "The selected messaging transport does not provide resource lifecycle management.");
            services.AddSingleton(management);
            services.AddArkMessagingResourceLifecycle(manifest.Resources);
        }

        return services;
    }

    private static IServiceCollection _addServiceBus(
        IServiceCollection services,
        Container container,
        MessagingFunctionsManifest manifest,
        IMessagingDataBus dataBus,
        string connection)
    {
#pragma warning disable CA2000 // The registered transport owns the client and the service provider owns the transport.
        ServiceBusClient client;
        ServiceBusAdministrationClient administration;
        if (_isConnectionString(connection))
        {
            client = new ServiceBusClient(connection);
            administration = new ServiceBusAdministrationClient(connection);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            client = new ServiceBusClient(connection, credential);
            administration = new ServiceBusAdministrationClient(connection, credential);
        }

        var transport = new ServiceBusMessagingTransport(client);
        return services.AddArkMessagingFunctionsHost(
            container,
            manifest,
            transport,
            dataBus,
            new ServiceBusTransportManagement(administration));
#pragma warning restore CA2000
    }

    private static IServiceCollection _addStorageQueue(
        IServiceCollection services,
        Container container,
        MessagingFunctionsManifest manifest,
        IMessagingDataBus dataBus,
        string connection,
        StorageQueueFunctionsHostSettings? hostSettings)
    {
        QueueServiceClient client;
        StorageQueueMessagingTransport transport;
        if (Uri.TryCreate(connection, UriKind.Absolute, out var serviceUri))
        {
            TokenCredential credential = new DefaultAzureCredential();
            client = new QueueServiceClient(serviceUri, credential);
            transport = new StorageQueueMessagingTransport(
                serviceUri,
                credential,
                manifest.Descriptor!.RetryPolicy.MaximumHandlerDuration,
                manifest.Descriptor.RetryPolicy.RetryDelay);
        }
        else
        {
            client = new QueueServiceClient(connection);
            transport = new StorageQueueMessagingTransport(
                connection,
                manifest.Descriptor!.RetryPolicy.MaximumHandlerDuration,
                manifest.Descriptor.RetryPolicy.RetryDelay);
        }

        return services.AddArkMessagingFunctionsHost(
            container,
            manifest,
            transport,
            dataBus,
            transport,
            client,
            hostSettings);
    }

    private static void _validateManifest(
        MessagingFunctionsManifest manifest,
        IMessagingTransport transport)
    {
        var descriptor = manifest.Descriptor
            ?? throw new InvalidOperationException(
                "The Functions messaging manifest does not contain generated participant composition metadata.");
        if (manifest.Participant != descriptor.ParticipantType)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Composed participant '{0}' does not match generated binding '{1}'.",
                    descriptor.ParticipantType,
                    manifest.Participant));
        }
        if (manifest.Network != descriptor.Network.NetworkType)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Composed network '{0}' does not match generated binding '{1}'.",
                    descriptor.Network.NetworkType,
                    manifest.Network));
        }

        descriptor.Network.Validate(transport.Capabilities);
        if (!descriptor.Receives)
            return;
        if (transport is InMemoryMessagingTransport)
            throw new InvalidOperationException(
                "Azure Functions cannot host the InMemory receive transport.");

        var composedBinding = transport switch
        {
            ServiceBusMessagingTransport => MessagingFunctionsTriggerBinding.ServiceBus,
            StorageQueueMessagingTransport => MessagingFunctionsTriggerBinding.StorageQueue,
            _ => throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Transport '{0}' is not supported by Azure Functions messaging composition.",
                    transport.GetType()))
        };
        if (manifest.TriggerBinding != composedBinding)
        {
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Composed transport '{0}' does not match generated trigger binding '{1}'.",
                    composedBinding,
                    manifest.TriggerBinding));
        }
    }

    private static void _registerSteps(Container container, IEnumerable<Type> stepTypes)
    {
        foreach (var stepType in stepTypes)
        {
            ArgumentNullException.ThrowIfNull(stepType);
            if (container.GetRegistration(stepType, throwOnFailure: false) is null)
                container.Register(stepType, stepType, Lifestyle.Scoped);
        }
    }

    private static bool _isConnectionString(string value)
    {
        return value.Contains('=', StringComparison.Ordinal);
    }
}
