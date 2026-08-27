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
    private const string _rebusBusServiceType = "Rebus.Bus.IBus";

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

        return transport switch
        {
            MessagingFunctionsRuntimeTransport.AzureServiceBus =>
                _addServiceBus(services, container, configuration, manifest, dataBus),
            MessagingFunctionsRuntimeTransport.AzureStorageQueue =>
                _addStorageQueue(
                    services,
                    container,
                    configuration,
                    manifest,
                    dataBus,
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

        _validateExclusiveBus(container);
        _validateManifest(manifest, transport);
        var descriptor = manifest.Descriptor!;
        _validateHandlers(container, descriptor);
        _registerSteps(container, manifest.IncomingSteps);
        _registerSteps(container, manifest.OutgoingSteps);
        management ??= transport as IMessagingTransportManagement;
        services.AddArkMessagingParticipant(
            descriptor,
            transport,
            dataBus,
            manifest.Resources,
            management,
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

        return services;
    }

    private static IServiceCollection _addServiceBus(
        IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        IMessagingDataBus dataBus)
    {
        var connection = configuration[manifest.ConnectionConfigurationKey];
        var fullyQualifiedNamespace = configuration[
            string.Concat(manifest.ConnectionConfigurationKey, ":fullyQualifiedNamespace")];
#pragma warning disable CA2000 // The registered transport owns the client and the service provider owns the transport.
        ServiceBusClient client;
        ServiceBusAdministrationClient administration;
        if (!string.IsNullOrWhiteSpace(connection) && _isConnectionString(connection))
        {
            client = new ServiceBusClient(connection);
            administration = new ServiceBusAdministrationClient(connection);
        }
        else
        {
            var serviceNamespace = !string.IsNullOrWhiteSpace(fullyQualifiedNamespace)
                ? fullyQualifiedNamespace
                : connection;
            if (string.IsNullOrWhiteSpace(serviceNamespace))
                throw _missingConfiguration(
                    manifest.ConnectionConfigurationKey,
                    "fullyQualifiedNamespace");
            var credential = _createCredential(configuration, manifest.ConnectionConfigurationKey);
            client = new ServiceBusClient(serviceNamespace, credential);
            administration = new ServiceBusAdministrationClient(serviceNamespace, credential);
        }

        var transport = new ServiceBusMessagingTransport(client);
        services.AddArkMessagingFunctionsHost(
            container,
            manifest,
            transport,
            dataBus,
            new ServiceBusTransportManagement(administration));
        services.AddSingleton<IHostedService>(_ => new ServiceBusTransportLifetime(transport));
        return services;
#pragma warning restore CA2000
    }

    private static IServiceCollection _addStorageQueue(
        IServiceCollection services,
        Container container,
        IConfiguration configuration,
        MessagingFunctionsManifest manifest,
        IMessagingDataBus dataBus,
        StorageQueueFunctionsHostSettings? hostSettings)
    {
        var connection = configuration[manifest.ConnectionConfigurationKey];
        var queueServiceUri = configuration[
            string.Concat(manifest.ConnectionConfigurationKey, ":queueServiceUri")];
        QueueServiceClient client;
        StorageQueueMessagingTransport transport;
        if (!string.IsNullOrWhiteSpace(queueServiceUri))
        {
            if (!Uri.TryCreate(queueServiceUri, UriKind.Absolute, out var serviceUri))
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Messaging transport configuration '{0}:queueServiceUri' must be an absolute URI.",
                        manifest.ConnectionConfigurationKey));
            TokenCredential credential = _createCredential(
                configuration,
                manifest.ConnectionConfigurationKey);
            client = new QueueServiceClient(serviceUri, credential);
            transport = new StorageQueueMessagingTransport(
                serviceUri,
                credential,
                manifest.Descriptor!.RetryPolicy.MaximumHandlerDuration,
                manifest.Descriptor.RetryPolicy.RetryDelay);
        }
        else if (Uri.TryCreate(connection, UriKind.Absolute, out var serviceUri))
        {
            TokenCredential credential = _createCredential(
                configuration,
                manifest.ConnectionConfigurationKey);
            client = new QueueServiceClient(serviceUri, credential);
            transport = new StorageQueueMessagingTransport(
                serviceUri,
                credential,
                manifest.Descriptor!.RetryPolicy.MaximumHandlerDuration,
                manifest.Descriptor.RetryPolicy.RetryDelay);
        }
        else if (string.IsNullOrWhiteSpace(connection))
        {
            throw _missingConfiguration(
                manifest.ConnectionConfigurationKey,
                "queueServiceUri");
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

    private static void _validateHandlers(
        Container container,
        MessagingParticipantDescriptor descriptor)
    {
        foreach (var handlerServiceType in descriptor.HandlerServiceTypes)
        {
            if (container.GetRegistration(handlerServiceType, throwOnFailure: false) is null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Messaging handler service '{0}' is not registered in the application container.",
                        handlerServiceType));
            }
        }
    }

    private static void _validateExclusiveBus(Container container)
    {
        // ponytail: Avoid a Rebus package dependency; replace this with shared topology markers when adapters expose them.
        if (container.GetCurrentRegistrations().Any(registration =>
                string.Equals(
                    registration.ServiceType.FullName,
                    _rebusBusServiceType,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Rebus and Mediator Framework messaging buses cannot be composed for the same Functions topology.");
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

    private static TokenCredential _createCredential(
        IConfiguration configuration,
        string connectionConfigurationKey)
    {
        var clientId = configuration[
            string.Concat(connectionConfigurationKey, ":clientId")];
        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId
        });
    }

    private static InvalidOperationException _missingConfiguration(
        string connectionConfigurationKey,
        string identitySetting)
    {
        return new InvalidOperationException(
            string.Format(
                CultureInfo.InvariantCulture,
                "Messaging transport configuration '{0}' or '{0}:{1}' is required.",
                connectionConfigurationKey,
                identitySetting));
    }

    private sealed class ServiceBusTransportLifetime : IHostedService, IAsyncDisposable
    {
        private readonly ServiceBusMessagingTransport _transport;
        private int _disposed;

        public ServiceBusTransportLifetime(ServiceBusMessagingTransport transport)
        {
            _transport = transport;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            await _transport.DisposeAsync().ConfigureAwait(false);
        }
    }
}
