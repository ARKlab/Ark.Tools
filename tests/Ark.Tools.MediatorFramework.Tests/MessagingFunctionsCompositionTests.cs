// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Ark.Tools.MediatorFramework.AzureFunctions;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;

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
    /// <summary>Verifies the producer path registers only the restricted outbound runtime.</summary>
    [TestMethod]
    public async Task SenderOnlyCompositionResolvesWorkingBusWithoutDispatcher()
    {
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        var transport = new InMemoryMessagingTransport();
        services.AddArkMessagingParticipant(
            _descriptor(receives: false),
            transport,
            _dataBus());
        await using var provider = services.BuildServiceProvider();

        var bus = provider.GetRequiredService<IBus>();
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
        MessagingFunctionsTriggerBinding binding)
    {
        var descriptor = _descriptor(receives: true);
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
            resources: new MessagingResourceManifest(
                "composition",
                "composition",
                1,
                Array.Empty<MessagingTopicResource>(),
                Array.Empty<MessagingSubscriptionResource>(),
                Array.Empty<string>(),
                MessagingResourceLifecycle.External));
    }

    private static MessagingParticipantDescriptor _descriptor(bool receives)
    {
        var network = new MessagingNetworkOptions(
            typeof(CompositionNetwork),
            new MessagingNetworkAttribute
            {
                Members = new[] { typeof(CompositionParticipant) },
                MaximumSchedulingDelay = TimeSpan.Zero
            });
        return new MessagingParticipantDescriptor(
            typeof(CompositionParticipant),
            network,
            new CompositionRegistry(network.NetworkIdentity),
            "composition",
            new[] { SerializationProtocol.Json },
            MessagingDefaultRetryPolicy.Instance,
            CompressionAlgorithm.None,
            0,
            receives,
            receives ? _dispatchAsync : null,
            dispatchFailed: null);
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
