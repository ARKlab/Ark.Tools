// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;
using Azure.Storage.Queues;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NodaTime;

using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies generated participant and Azure Functions host composition.</summary>
[TestClass]
public sealed class MessagingFunctionsCompositionTests
{
    /// <summary>Verifies generated Functions metadata composes through the fluent entry point.</summary>
    [TestMethod]
    public async Task FluentFunctionsCompositionUsesManifestAndRegistersOutbox()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["CompositionConnection:fullyQualifiedNamespace"] = "composition.servicebus.windows.net",
                ["CompositionConnection:clientId"] = Guid.NewGuid().ToString()
            })
            .Build();

        services.ConfigureArkMessagingFunctions(
            container,
            configuration,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            messaging => messaging
                .UseAzureServiceBus()
                .UseDataBus(_dataBus())
                .UseOutbox());
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMessagingTransport>()
            .Should().BeOfType<ServiceBusMessagingTransport>();
        provider.GetRequiredService<IBusOutboxEnlistment>().Should().NotBeNull();
    }

    /// <summary>Verifies the fluent Functions entry point rejects incomplete composition.</summary>
    [TestMethod]
    public void FluentFunctionsCompositionRequiresTransport()
    {
        var services = new ServiceCollection();
        using var container = _container();

        var action = () => services.ConfigureArkMessagingFunctions(
            container,
            new ConfigurationBuilder().Build(),
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            messaging => messaging.UseDataBus(_dataBus()));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requires a transport*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies duplicate fluent Functions transport choices fail immediately.</summary>
    [TestMethod]
    public void FluentFunctionsCompositionRejectsDuplicateTransport()
    {
        var services = new ServiceCollection();
        using var container = _container();

        var action = () => services.ConfigureArkMessagingFunctions(
            container,
            new ConfigurationBuilder().Build(),
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            messaging => messaging
                .UseAzureServiceBus()
                .UseAzureServiceBus());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*transport is already selected*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies Functions fluent composition rejects a hosted native outbox processor.</summary>
    [TestMethod]
    public void FluentFunctionsCompositionRejectsNativeOutboxProcessor()
    {
        var services = new ServiceCollection();
        using var container = _container();

        var action = () => services.ConfigureArkMessagingFunctions(
            container,
            new ConfigurationBuilder().Build(),
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            messaging => messaging.UseOutbox(new Ark.Tools.Outbox.InMemoryOutboxContextFactory()));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot host the native messaging outbox processor*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies the producer path registers only the restricted outbound runtime.</summary>
    [TestMethod]
    public async Task SenderOnlyCompositionResolvesWorkingBusWithoutDispatcher()
    {
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        var transport = new InMemoryMessagingTransport();
        services._AddArkMessagingParticipant(
            _descriptor(receives: false),
            transport,
            _dataBus());
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<IBus>();
        provider.GetRequiredService<IBusOutboxEnlistment>().Should().BeSameAs(bus);
        await bus.Send(new CompositionMessage(), default, default).ConfigureAwait(false);

        provider.GetService<MessagingDispatcher>().Should().BeNull();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
#pragma warning disable MA0004 // The enumerator is disposed by the test.
        await using var enumerator = transport.ReceiveAsync("composition", cts.Token)
            .GetAsyncEnumerator(cts.Token);
#pragma warning restore MA0004
        (await enumerator.MoveNextAsync().ConfigureAwait(false)).Should().BeTrue();
        await enumerator.Current.CompleteAsync(default).ConfigureAwait(false);
    }

    /// <summary>Verifies Functions cross-wires the native bus into the application container.</summary>
    [TestMethod]
    public async Task SenderOnlyCompositionCrossWiresBusAndOutboxEnlistment()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        services.AddArkMessagingFunctionsHost(
            container,
            _manifest(
                MessagingFunctionsTriggerBinding.ServiceBus,
                _descriptor(receives: false)),
            new InMemoryMessagingTransport(),
            _dataBus());
        await using var provider = services.BuildServiceProvider();
        var bridge = provider.GetServices<IHostedService>()
            .Single(service => service.GetType().Name == "MessagingFunctionsBusBridge");
        await bridge.StartAsync(default).ConfigureAwait(false);

        container.GetInstance<IBus>().Should().BeSameAs(provider.GetRequiredService<IBus>());
        container.GetInstance<IBusOutboxEnlistment>().Should()
            .BeSameAs(provider.GetRequiredService<IBusOutboxEnlistment>());
    }

    /// <summary>Verifies missing host transport configuration fails before registration.</summary>
    [TestMethod]
    public void MissingConfigurationNamesRequiredKey()
    {
        var services = new ServiceCollection();
        using var container = _container();
        var configuration = new ConfigurationBuilder().Build();

        var action = () => services.AddArkMessagingFunctionsHost(
            container,
            configuration,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            _dataBus(),
            MessagingFunctionsRuntimeTransport.AzureServiceBus);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*CompositionConnection*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies standard Functions Service Bus identity settings compose without a secret.</summary>
    [TestMethod]
    public async Task ServiceBusIdentityConfigurationComposesOwnedTransport()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["CompositionConnection:fullyQualifiedNamespace"] = "composition.servicebus.windows.net",
                ["CompositionConnection:clientId"] = Guid.NewGuid().ToString()
            })
            .Build();

        services.AddArkMessagingFunctionsHost(
            container,
            configuration,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            _dataBus(),
            MessagingFunctionsRuntimeTransport.AzureServiceBus);
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IMessagingTransport>()
            .Should().BeOfType<ServiceBusMessagingTransport>();
        provider.GetServices<IHostedService>()
            .Should().Contain(service => service.GetType().Name == "ServiceBusTransportLifetime");
    }

    /// <summary>Verifies standard Functions Storage Queue identity settings compose without a secret.</summary>
    [TestMethod]
    public async Task StorageQueueIdentityConfigurationComposesQueueServiceClient()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["CompositionConnection:queueServiceUri"] = "https://composition.queue.core.windows.net",
                ["CompositionConnection:clientId"] = Guid.NewGuid().ToString()
            })
            .Build();

        services.AddArkMessagingFunctionsHost(
            container,
            configuration,
            _manifest(
                MessagingFunctionsTriggerBinding.StorageQueue,
                _descriptor(receives: true, retryPolicy: new CompositionRetryPolicy())),
            _dataBus(),
            MessagingFunctionsRuntimeTransport.AzureStorageQueue);
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<QueueServiceClient>().Uri.Should().Be(
            new Uri("https://composition.queue.core.windows.net"));
    }

    /// <summary>Verifies generated and composed transport bindings cannot drift.</summary>
    [TestMethod]
    public async Task TriggerBindingMismatchNamesGeneratedAndComposedBindings()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        await using var transport = _serviceBus();

        var action = () => services.AddArkMessagingFunctionsHost(
            container,
            _manifest(MessagingFunctionsTriggerBinding.StorageQueue),
            transport,
            _dataBus());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ServiceBus*StorageQueue*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies Functions rejects the long-running InMemory receive transport.</summary>
    [TestMethod]
    public void InMemoryReceiveTransportIsRejected()
    {
        var services = new ServiceCollection();
        using var container = _container();

        var action = () => services.AddArkMessagingFunctionsHost(
            container,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            new InMemoryMessagingTransport(),
            _dataBus());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot host the InMemory receive transport*");
        services.Should().BeEmpty();
    }

    /// <summary>Verifies Functions composition cannot be mixed with the native polling processor.</summary>
    [TestMethod]
    public void NativeOutboxProcessorIsRejectedBeforeOrAfterFunctionsComposition()
    {
        var factory = new Ark.Tools.Outbox.InMemoryOutboxContextFactory();
        var processorFirst = new ServiceCollection();
        processorFirst.AddSingleton<IMessagingTransport>(new InMemoryMessagingTransport());
        processorFirst.AddArkMessagingOutboxProcessor(factory);
        using var firstContainer = _container();

        var addFunctions = () => processorFirst.AddArkMessagingFunctionsHost(
            firstContainer,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            new InMemoryMessagingTransport(),
            _dataBus());
        addFunctions.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot host*native messaging outbox processor*");

        var functionsFirst = new ServiceCollection();
        using var secondContainer = _container();
        functionsFirst.AddArkMessagingFunctionsHost(
            secondContainer,
            _manifest(
                MessagingFunctionsTriggerBinding.ServiceBus,
                _descriptor(receives: false)),
            new InMemoryMessagingTransport(),
            _dataBus());

        var addProcessor = () => functionsFirst.AddArkMessagingOutboxProcessor(factory);
        addProcessor.Should().Throw<InvalidOperationException>()
            .WithMessage("*Azure Functions composition is active*");
    }

    /// <summary>Verifies a matching Functions host resolves native bus and dispatch services.</summary>
    [TestMethod]
    public async Task MatchingServiceBusHostResolvesBusAndDispatcher()
    {
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        await using var container = _container();
#pragma warning disable CA2000 // The service provider owns the registered transport.
        var transport = _serviceBus();
#pragma warning restore CA2000
        services.AddArkMessagingFunctionsHost(
            container,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus),
            transport,
            _dataBus());
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IBus>().Should().BeOfType<MessagingBus>();
        provider.GetRequiredService<MessagingDispatcher>().Should().NotBeNull();
        provider.GetServices<IHostedService>()
            .Should().NotContain(service => service.GetType().Name.Contains("Rebus", StringComparison.Ordinal)
                || service.GetType().Name.Contains("Outbox", StringComparison.Ordinal));
    }

    /// <summary>Verifies sender-only participants still reconcile publisher-owned topics.</summary>
    [TestMethod]
    public async Task SenderOnlyFunctionsCompositionRegistersResourceLifecycle()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        var descriptor = _descriptor(receives: false);
        var topics = new[] { new MessagingTopicResource("composition-topic", descriptor.Identity) };
        var knownTopics = new[] { "composition-topic" };
        var resources = new MessagingResourceManifest(
            descriptor.Identity,
            identityQueue: null,
            1,
            topics,
            Array.Empty<MessagingSubscriptionResource>(),
            knownTopics,
            MessagingResourceLifecycle.CreateIfMissing);

        services.AddArkMessagingFunctionsHost(
            container,
            _manifest(
                MessagingFunctionsTriggerBinding.ServiceBus,
                descriptor,
                resources),
            new InMemoryMessagingTransport(),
            _dataBus());
        await using var provider = services.BuildServiceProvider();

        provider.GetServices<IHostedService>()
            .Should().Contain(service => service.GetType().Name == "MessagingResourceStartupService");
        provider.GetService<MessagingDispatcher>().Should().BeNull();
    }

    /// <summary>Verifies consumed contracts require handlers in the application container.</summary>
    [TestMethod]
    public async Task MissingConsumedContractHandlerFailsComposition()
    {
        var services = new ServiceCollection();
        await using var container = _container();
        await using var transport = _serviceBus();
        var descriptor = _descriptor(
            receives: true,
            new[] { typeof(ICommandHandler<CompositionConsumedMessage>) });

        var action = () => services.AddArkMessagingFunctionsHost(
            container,
            _manifest(MessagingFunctionsTriggerBinding.ServiceBus, descriptor),
            transport,
            _dataBus());

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*ICommandHandler*CompositionConsumedMessage*not registered*");
        services.Should().BeEmpty();
    }

    private static Container _container()
    {
        var container = new Container();
        container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
        return container;
    }

    private static InMemoryMessagingDataBus _dataBus()
    {
        return new InMemoryMessagingDataBus(
            SystemClock.Instance,
            Duration.FromDays(8));
    }

    private static ServiceBusMessagingTransport _serviceBus()
    {
#pragma warning disable CA2000 // The returned transport owns the client.
        return new ServiceBusMessagingTransport(new ServiceBusClient(
            "Endpoint=sb://localhost/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA=="));
#pragma warning restore CA2000
    }

    private static MessagingFunctionsManifest _manifest(
        MessagingFunctionsTriggerBinding binding,
        MessagingParticipantDescriptor? descriptor = null,
        MessagingResourceManifest? resources = null)
    {
        descriptor ??= _descriptor(receives: true);
        return new MessagingFunctionsManifest(
            typeof(CompositionParticipant),
            typeof(CompositionNetwork),
            descriptor,
            binding,
            "composition",
            "CompositionConnection",
            1,
            TimeSpan.FromMinutes(5),
            Array.Empty<MessagingFunctionsSubscription>(),
            Array.Empty<Type>(),
            Array.Empty<Type>(),
            resources: resources ?? new MessagingResourceManifest(
                "composition",
                "composition",
                1,
                Array.Empty<MessagingTopicResource>(),
                Array.Empty<MessagingSubscriptionResource>(),
                Array.Empty<string>(),
                MessagingResourceLifecycle.External));
    }

    private static MessagingParticipantDescriptor _descriptor(
        bool receives,
        IEnumerable<Type>? handlerServiceTypes = null,
        IMessagingRetryPolicy? retryPolicy = null)
    {
        var network = new MessagingNetworkOptions(
            typeof(CompositionNetwork),
            new MessagingNetworkAttribute
            {
                Members = new[] { typeof(CompositionParticipant) },
                Requires = MessagingCapabilities.SendReceive,
                MaximumSchedulingDelay = TimeSpan.Zero
            });
        return new MessagingParticipantDescriptor(
            typeof(CompositionParticipant),
            network,
            new CompositionRegistry(network.NetworkIdentity),
            "composition",
            new[] { SerializationProtocol.Json },
            retryPolicy ?? MessagingDefaultRetryPolicy.Instance,
            CompressionAlgorithm.None,
            0,
            receives,
            receives ? _dispatchAsync : null,
            dispatchFailed: null,
            handlerServiceTypes);
    }

    private static async Task _dispatchAsync(
        string logicalName,
        IMessagingPayloadReader payload,
        ICommandProcessor processor,
        CancellationToken ctk)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private sealed class CompositionNetwork;

    private sealed class CompositionParticipant;

    private sealed record CompositionMessage;

    private sealed class CompositionConsumedMessage : ICommand<CompositionConsumedMessage>;

    private sealed class CompositionRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 3;

        public bool SecondLevelRetriesEnabled => false;

        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(5);

        public TimeSpan RetryDelay => TimeSpan.FromSeconds(10);
    }

    private sealed class CompositionRegistry : IMessagingContractRegistry
    {
        public CompositionRegistry(string networkIdentity)
        {
            NetworkIdentity = networkIdentity;
        }

        public string NetworkIdentity { get; }

        public string GetDestination<T>() where T : class
        {
            return "composition";
        }

        public string GetProcessorIdentity<T>() where T : class
        {
            return "composition";
        }

        public string GetPublisherIdentity<T>() where T : class
        {
            return "composition";
        }

        public SerializationProtocol GetWireProtocol<T>() where T : class
        {
            return SerializationProtocol.Json;
        }

        public string GetLogicalName<T>() where T : class
        {
            return "composition_message";
        }
    }
}
