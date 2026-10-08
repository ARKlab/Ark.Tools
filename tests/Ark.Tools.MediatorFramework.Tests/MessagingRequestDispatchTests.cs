// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Solid;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies that a request sent as a message reaches its request handler.</summary>
[TestClass]
public sealed class MessagingRequestDispatchTests
{
    private const string _queue = "request-receiver";

    /// <summary>A receiver processing a request runs its handler once and completes the delivery.</summary>
    [TestMethod]
    public async Task ReceiverRunsRequestHandlerOnceAndCompletesTheMessage()
    {
        var transport = new InMemoryMessagingTransport();
        var handler = new RecordingReviewHandler();
        var network = new MessagingNetworkOptions(
            typeof(RequestNetwork),
            new MessagingNetworkAttribute
            {
                Members = new[] { typeof(RequestReceiverParticipant) },
                Requires = MessagingCapabilities.SendReceive,
                MaximumSchedulingDelay = TimeSpan.Zero,
            });
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            static options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        services.AddSingleton<IRequestHandler<ReviewRequest, string>>(handler);
        services.AddScoped<ICommandProcessor, UnusedCommandProcessor>();
        services.AddScoped<IRequestProcessor, ServiceProviderRequestProcessor>();
        services.ConfigureArkMessaging(
            network,
            new RequestRegistry(network.NetworkIdentity),
            builder => builder.Receiver<RequestReceiverParticipant>(receiver => receiver
                .UseTransport(transport)
                .UseInMemoryDataBus()));
        await using var provider = services.BuildServiceProvider();
        var hosted = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hosted)
            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);

        try
        {
            await provider.GetRequiredService<IBus>()
                .Send(new ReviewRequest { Text = "Good" })
                .ConfigureAwait(false);

            (await handler._handled.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false))
                .Text.Should().Be("Good");
            using var settled = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (transport.GetPendingCount(_queue) > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(10), settled.Token).ConfigureAwait(false);
        }
        finally
        {
            foreach (var service in hosted.Reverse())
                await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }

        handler._count.Should().Be(1);
        transport.GetDeadLetters(_queue).Should().BeEmpty();
    }

    /// <summary>A receiving host without <see cref="IRequestProcessor"/> fails startup instead of failing every delivery.</summary>
    [TestMethod]
    public async Task ReceiverWithoutRequestProcessorFailsStartup()
    {
        var network = new MessagingNetworkOptions(
            typeof(RequestNetwork),
            new MessagingNetworkAttribute
            {
                Members = new[] { typeof(RequestReceiverParticipant) },
                Requires = MessagingCapabilities.SendReceive,
                MaximumSchedulingDelay = TimeSpan.Zero,
            });
        var services = new ServiceCollection();
        services.Configure<JsonSerializerOptions>(
            static options => options.TypeInfoResolver = new DefaultJsonTypeInfoResolver());
        services.AddScoped<ICommandProcessor, UnusedCommandProcessor>();
        services.ConfigureArkMessaging(
            network,
            new RequestRegistry(network.NetworkIdentity),
            static builder => builder.Receiver<RequestReceiverParticipant>(static receiver => receiver
                .UseTransport(new InMemoryMessagingTransport())
                .UseInMemoryDataBus()));
        await using var provider = services.BuildServiceProvider();
        var validator = provider.GetServices<IHostedService>()
            .Single(static s => s.GetType().Name == "MessagingParticipantStartupValidator");

        var act = async () => await validator.StartAsync(CancellationToken.None).ConfigureAwait(false);

        (await act.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(false))
            .WithMessage("*IRequestProcessor must be registered*");
    }

    /// <summary>A request contract sent as a message.</summary>
    public sealed record ReviewRequest : IRequest<ReviewRequest, string>
    {
        /// <summary>Gets the review text.</summary>
        public string Text { get; init; } = string.Empty;
    }

    private sealed class RequestNetwork;

    /// <summary>Hand-written equivalent of a generated participant that processes <see cref="ReviewRequest"/>.</summary>
    private sealed class RequestReceiverParticipant : IMessagingParticipant<RequestReceiverParticipant>
    {
        public static MessagingParticipantDescriptor CreateDescriptor(
            MessagingNetworkOptions network,
            IMessagingContractRegistry registry)
        {
            return new MessagingParticipantDescriptor(
                typeof(RequestReceiverParticipant),
                network,
                registry,
                _queue,
                new[] { SerializationProtocol.Json },
                MessagingDefaultRetryPolicy.Instance,
                CompressionAlgorithm.None,
                0,
                receives: true,
                _dispatchAsync,
                dispatchFailed: null,
                new[] { typeof(IRequestHandler<ReviewRequest, string>) });
        }

        // Same shape the generator emits for a request listed in Processes.
        private static async Task _dispatchAsync(
            string logicalName,
            IMessagingPayloadReader payload,
            ICommandProcessor processor,
            IRequestProcessor requestProcessor,
            CancellationToken ctk)
        {
            var message = await payload.DeserializeAsync<ReviewRequest>(ctk).ConfigureAwait(false);
            await requestProcessor.ExecuteAsync<ReviewRequest, string>(message, ctk).ConfigureAwait(false);
        }
    }

    private sealed class RequestRegistry(string networkIdentity) : IMessagingContractRegistry
    {
        public string NetworkIdentity { get; } = networkIdentity;

        public string GetDestination<T>() where T : class => _queue;

        public string GetProcessorIdentity<T>() where T : class => _queue;

        public string GetPublisherIdentity<T>() where T : class => _queue;

        public SerializationProtocol GetWireProtocol<T>() where T : class => SerializationProtocol.Json;

        public string GetLogicalName<T>() where T : class => "tests.review";
    }

    private sealed class RecordingReviewHandler : IRequestHandler<ReviewRequest, string>
    {
        private int _executions;

        internal TaskCompletionSource<ReviewRequest> _handled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int _count => Volatile.Read(ref _executions);

        public async Task<string> ExecuteAsync(ReviewRequest request, CancellationToken ctk = default)
        {
            Interlocked.Increment(ref _executions);
            _handled.TrySetResult(request);
            await Task.CompletedTask.ConfigureAwait(false);
            return "reviewed";
        }
    }

    private sealed class ServiceProviderRequestProcessor(IServiceProvider services) : IRequestProcessor
    {
        [Obsolete("Test seam.", error: true)]
        public TResponse Execute<TResponse>(IRequest<TResponse> request)
        {
            throw new NotSupportedException();
        }

        public Task<TResponse> ExecuteAsync<TResponse>(IRequest<TResponse> request, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public async Task<TResponse> ExecuteAsync<TRequest, TResponse>(
            IRequest<TRequest, TResponse> request,
            CancellationToken ctk = default)
            where TRequest : class, IRequest<TRequest, TResponse>
        {
            return await services.GetRequiredService<IRequestHandler<TRequest, TResponse>>()
                .ExecuteAsync((TRequest)request, ctk)
                .ConfigureAwait(false);
        }
    }

    private sealed class UnusedCommandProcessor : ICommandProcessor
    {
        [Obsolete("Test seam.", error: true)]
        public void Execute(ICommand command)
        {
            throw new NotSupportedException();
        }

        public Task ExecuteAsync(ICommand command, CancellationToken ctk = default)
        {
            throw new NotSupportedException();
        }

        public Task ExecuteAsync<TCommand>(ICommand<TCommand> command, CancellationToken ctk = default)
            where TCommand : class, ICommand<TCommand>
        {
            throw new NotSupportedException();
        }
    }
}
