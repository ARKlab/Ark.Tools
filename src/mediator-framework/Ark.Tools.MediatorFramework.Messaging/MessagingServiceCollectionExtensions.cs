// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;

using MessagePack;
using MessagePack.Resolvers;

using Ark.Tools.Outbox;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Registers the transport-neutral messaging runtime.</summary>
public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the host-options-driven JSON codec and codec registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<JsonSerializerOptions>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, JsonMessagingCodec>());
        services.AddSingleton<MessagingCodecRegistry>();
        services.AddSingleton<IMessagingCodecRegistry>(
            static serviceProvider => serviceProvider.GetRequiredService<MessagingCodecRegistry>());
        return services;
    }

    /// <summary>Registers a messaging transport and validates network capabilities.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="transport">The transport to register.</param>
    /// <param name="networks">The resolved networks using the transport.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessaging(
        this IServiceCollection services,
        IMessagingTransport transport,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(networks);

        foreach (var network in networks)
        {
            ArgumentNullException.ThrowIfNull(network);
            network.Validate(transport.Capabilities);
        }

        services._addArkMessaging();
        services.AddSingleton<IMessagingTransport>(transport);
        if (transport is IMessagingReceiveTransport receiveTransport)
            services.AddSingleton(receiveTransport);
        if (transport is IMessagingTransportManagement management)
            services.AddSingleton(management);
        return services;
    }

    /// <summary>Registers native transactional outbox enqueue support.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingOutboxEnqueue(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<MessagingOutboxEnqueueRegistration>();
        return services;
    }

    /// <summary>Registers the always-running native messaging outbox processor.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="contextFactory">The durable outbox context factory.</param>
    /// <param name="batchSize">The maximum number of messages processed per poll.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddArkMessagingOutboxProcessor(
        this IServiceCollection services,
        IOutboxAsyncContextFactory contextFactory,
        int batchSize = 10)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        if (services.Any(static service =>
                service.ServiceType == typeof(MessagingOutboxProcessor)
                || service.ServiceType.FullName
                    == "Ark.Tools.MediatorFramework.AzureFunctions.MessagingFunctionsManifest"))
        {
            throw new InvalidOperationException(
                "A native messaging outbox processor is already registered or Azure Functions composition is active.");
        }

        services._addArkMessagingOutboxEnqueue();
        services.AddSingleton(contextFactory);
        services.AddSingleton(serviceProvider => new MessagingOutboxProcessor(
            serviceProvider.GetRequiredService<IOutboxAsyncContextFactory>(),
            serviceProvider.GetRequiredService<IMessagingTransport>(),
            batchSize));
        services.AddSingleton<IHostedService>(
            static serviceProvider => serviceProvider.GetRequiredService<MessagingOutboxProcessor>());
        return services;
    }

    /// <summary>Registers the shared DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="dataBus">The provider shared by all network participants.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingDataBus(
        this IServiceCollection services,
        IMessagingDataBus dataBus,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataBus);
        ArgumentNullException.ThrowIfNull(networks);
        _validateDataBusLifetime(dataBus, networks);
        services.AddSingleton<IMessagingDataBus>(dataBus);
        return services;
    }

    /// <summary>
    /// Registers the Azure Blob DataBus provider and validates its data-plane
    /// configuration when the host starts.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The Azure Blob provider options.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkAzureBlobMessagingDataBus(
        this IServiceCollection services,
        AzureBlobDataBusOptions options,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(networks);

        var dataBus = new AzureBlobMessagingDataBus(options);
        _validateDataBusLifetime(dataBus, networks);
        services.AddSingleton(dataBus);
        services.AddSingleton<IMessagingDataBus>(dataBus);
        services.AddSingleton<IHostedService, AzureBlobMessagingDataBusStartupValidator>();
        return services;
    }

    /// <summary>Registers the in-memory DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="clock">The clock used for attachment expiry.</param>
    /// <param name="lifetime">The attachment lifetime.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkInMemoryMessagingDataBus(
        this IServiceCollection services,
        NodaTime.IClock? clock = null,
        NodaTime.Duration? lifetime = null,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services._addArkMessagingDataBus(
            new InMemoryMessagingDataBus(
                clock ?? NodaTime.SystemClock.Instance,
                lifetime ?? NodaTime.Duration.FromHours(1)),
            networks);
    }

    /// <summary>Registers the default-lifetime in-memory DataBus provider.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="networks">The networks that use the provider.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkInMemoryMessagingDataBus(
        this IServiceCollection services,
        params MessagingNetworkOptions[] networks)
    {
        return services._addArkInMemoryMessagingDataBus(
            clock: null,
            lifetime: null,
            networks);
    }

    /// <summary>Registers the first-class in-memory messaging transport.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="networks">The resolved networks using the transport.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkInMemoryMessaging(
        this IServiceCollection services,
        params MessagingNetworkOptions[] networks)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services._addArkMessaging(new InMemoryMessagingTransport(), networks);
    }

    /// <summary>Registers startup reconciliation for a generated resource manifest.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="manifest">The generated desired resources.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingResourceLifecycle(
        this IServiceCollection services,
        MessagingResourceManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(manifest);
        services.AddSingleton(manifest);
        services.AddSingleton<MessagingResourceReconciler>();
        services.AddSingleton<IHostedService, MessagingResourceStartupService>();
        return services;
    }

    /// <summary>Registers the native restricted bus for one messaging participant.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="network">The resolved network options.</param>
    /// <param name="registry">The generated contract registry.</param>
    /// <param name="payloadSender">The participant-configured payload sender.</param>
    /// <param name="participantIdentity">The sending participant identity.</param>
    /// <param name="outgoingStepTypes">Optional outgoing pipeline step types.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingBus(
        this IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        MessagingPayloadSender payloadSender,
        string participantIdentity,
        IReadOnlyList<Type>? outgoingStepTypes = null)
    {
        return services._addArkMessagingBus(
            network,
            registry,
            payloadSender,
            participantIdentity,
            outgoingStepTypes,
            null);
    }

    /// <summary>Registers the native restricted bus for one messaging participant.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="network">The resolved network options.</param>
    /// <param name="registry">The generated contract registry.</param>
    /// <param name="payloadSender">The participant-configured payload sender.</param>
    /// <param name="participantIdentity">The sending participant identity.</param>
    /// <param name="outgoingStepTypes">Optional outgoing pipeline step types.</param>
    /// <param name="resolveStep">Optional host pipeline-step resolver.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingBus(
        this IServiceCollection services,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        MessagingPayloadSender payloadSender,
        string participantIdentity,
        IReadOnlyList<Type>? outgoingStepTypes,
        Func<Type, object>? resolveStep)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(payloadSender);
        ArgumentException.ThrowIfNullOrEmpty(participantIdentity);

        services.AddSingleton<MessagingBus>(serviceProvider => new MessagingBus(
                serviceProvider.GetRequiredService<IMessagingTransport>(),
                network,
                registry,
                serviceProvider.GetRequiredService<IMessagingCodecRegistry>(),
                payloadSender,
                participantIdentity,
                outgoingStepTypes,
                resolveStep ?? serviceProvider.GetRequiredService));
        services.AddSingleton<IBus>(
            static serviceProvider => serviceProvider.GetRequiredService<MessagingBus>());
        services.AddSingleton<IBusOutboxEnlistment>(
            static serviceProvider => serviceProvider.GetRequiredService<MessagingBus>());
        return services;
    }

    /// <summary>
    /// Registers the transport-neutral producer runtime for one generated participant.
    /// This path does not register receive dispatch or start a receive worker.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="participant">The generated participant descriptor.</param>
    /// <param name="transport">The selected runtime transport.</param>
    /// <param name="dataBus">The shared network DataBus.</param>
    /// <param name="outgoingStepTypes">Optional host-local outgoing pipeline steps.</param>
    /// <param name="resolveStep">Optional host pipeline-step resolver.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingParticipant(
        this IServiceCollection services,
        MessagingParticipantDescriptor participant,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        IReadOnlyList<Type>? outgoingStepTypes = null,
        Func<Type, object>? resolveStep = null)
    {
        var resources = participant.Network.ResourceLifecycle == MessagingResourceLifecycle.CreateIfMissing
            && participant.PublishedTopics.Count > 0
                ? new MessagingResourceManifest(
                    participant.Identity,
                    identityQueue: null,
                    participant.RetryPolicy.MaximumDeliveryCount,
                    participant.PublishedTopics,
                    Array.Empty<MessagingSubscriptionResource>(),
                    participant.PublishedTopics.Select(static topic => topic.Name),
                    participant.Network.ResourceLifecycle)
                : null;
        return services._addArkMessagingParticipant(
            participant,
            transport,
            dataBus,
            resources,
            transport as IMessagingTransportManagement,
            outgoingStepTypes,
            resolveStep);
    }

    /// <summary>Registers one generated participant with explicit resource lifecycle services.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="participant">The generated participant descriptor.</param>
    /// <param name="transport">The selected runtime transport.</param>
    /// <param name="dataBus">The shared network DataBus.</param>
    /// <param name="resources">Optional generated desired-resource manifest.</param>
    /// <param name="management">Optional resource-management seam.</param>
    /// <param name="outgoingStepTypes">Optional host-local outgoing pipeline steps.</param>
    /// <param name="resolveStep">Optional host pipeline-step resolver.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addArkMessagingParticipant(
        this IServiceCollection services,
        MessagingParticipantDescriptor participant,
        IMessagingTransport transport,
        IMessagingDataBus dataBus,
        MessagingResourceManifest? resources,
        IMessagingTransportManagement? management,
        IReadOnlyList<Type>? outgoingStepTypes,
        Func<Type, object>? resolveStep)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(dataBus);
        if (string.Equals(participant.Identity, MessagingOutboxProcessor.Identity, StringComparison.Ordinal))
            throw new InvalidOperationException("The participant identity 'outbox-processor' is reserved.");
        if (resources?.Lifecycle == MessagingResourceLifecycle.CreateIfMissing
            && management is null)
        {
            throw new InvalidOperationException(
                "The selected messaging transport does not provide resource lifecycle management.");
        }

        if (services.Any(static service => service.ServiceType == typeof(MessagingParticipantDescriptor)))
            throw new InvalidOperationException("A messaging participant is already registered in this host.");

        services._addArkMessaging(transport, participant.Network);
        services._addArkMessagingDataBus(dataBus, participant.Network);
        services.AddSingleton(participant);
        services.AddSingleton(participant.Network);
        services.AddSingleton(participant.Registry);
        services.AddSingleton(participant.RetryPolicy);
        var payloadSender = participant.CreatePayloadSender(dataBus);
        services.AddSingleton(payloadSender);
        services._addArkMessagingBus(
            participant.Network,
            participant.Registry,
            payloadSender,
            participant.Identity,
            outgoingStepTypes,
            resolveStep);
        services.AddSingleton<IHostedService, MessagingParticipantStartupValidator>();
        if (resources?.Lifecycle == MessagingResourceLifecycle.CreateIfMissing)
        {
            services.AddSingleton(management!);
            services._addArkMessagingResourceLifecycle(resources);
        }
        return services;
    }

    private static void _validateDataBusLifetime(
        IMessagingDataBus dataBus,
        IEnumerable<MessagingNetworkOptions> networks)
    {
        var lifetime = dataBus switch
        {
            InMemoryMessagingDataBus inMemory =>
                inMemory.MinimumAttachmentLifetime.ToTimeSpan(),
            AzureBlobMessagingDataBus azureBlob => azureBlob.MinimumAttachmentLifetime,
            _ => TimeSpan.Zero
        };
        if (lifetime <= TimeSpan.Zero)
            return;

        foreach (var network in networks)
        {
            ArgumentNullException.ThrowIfNull(network);
            if (network.MaximumSchedulingDelay <= TimeSpan.Zero)
                continue;

            if (lifetime <= network.MaximumSchedulingDelay)
                throw new ArgumentOutOfRangeException(
                    nameof(dataBus),
                    $"The DataBus attachment lifetime must cover network '{network.NetworkIdentity}' maximum scheduling delay.");
        }

    }

    /// <summary>Registers the MessagePack and protobuf messaging codecs.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addMessagePackAndProtobufMessagingCodecs(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services._addMessagePackMessagingCodec();
        services._addProtobufMessagingCodec();
        return services;
    }

    /// <summary>Registers the MessagePack and protobuf codecs with a host resolver.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resolver">The host MessagePack formatter resolver.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addMessagePackAndProtobufMessagingCodecs(
        this IServiceCollection services,
        IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services._addMessagePackMessagingCodec(resolver);
        services._addProtobufMessagingCodec();
        return services;
    }

    /// <summary>Registers the MessagePack messaging codec.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addMessagePackMessagingCodec(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IFormatterResolver>(StandardResolver.Instance);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        return services;
    }

    /// <summary>Registers the MessagePack messaging codec with a host resolver.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="resolver">The host MessagePack formatter resolver.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addMessagePackMessagingCodec(
        this IServiceCollection services,
        IFormatterResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(resolver);

        services.Replace(ServiceDescriptor.Singleton<IFormatterResolver>(resolver));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, MessagePackMessagingCodec>());
        return services;
    }

    /// <summary>Registers the protobuf messaging codec.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    internal static IServiceCollection _addProtobufMessagingCodec(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMessagingCodec, ProtobufMessagingCodec>());
        return services;
    }
}

internal sealed class MessagingParticipantStartupValidator : IHostedService
{
    private readonly MessagingParticipantDescriptor _participant;
    private readonly IMessagingCodecRegistry _codecs;

    public MessagingParticipantStartupValidator(
        MessagingParticipantDescriptor participant,
        IMessagingCodecRegistry codecs)
    {
        _participant = participant;
        _codecs = codecs;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        MessagingJsonStartupValidation.ValidateDeclaredSerializers(
            _codecs,
            _participant.Serializers,
            _participant.Identity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

internal sealed class MessagingOutboxEnqueueRegistration;
