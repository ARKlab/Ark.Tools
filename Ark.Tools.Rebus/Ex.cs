using Rebus.Bus;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Pipeline.Send;
using Rebus.Retry.FailFast;
using Rebus.Time;
using Rebus.Transport;
using Rebus.Transport.InMem;

using SimpleInjector;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Ark.Tools.Rebus
{
    public static class Ex
    {
        public static void ConfigureRebus(this Container container, Action<RebusConfigurer> configurationCallback)
        {
            container.RegisterSingleton<IMessageContextProvider, MessageContextProvider>();
            container.Register(() =>
            {
                return MessageContext.Current ?? new FakeMessageContext();
            });

            container.RegisterSingleton(() =>
            {
                var containerAdapter = new SimpleInjectorHandlerActivator(container);
                var rebusConfigurer = Configure.With(containerAdapter);
                configurationCallback(rebusConfigurer);
                return rebusConfigurer.Start();
            });
        }

        class FakeMessageContext : IMessageContext
        {
            public ITransactionContext? TransactionContext { get; }
            public IncomingStepContext? IncomingStepContext { get; }
            public TransportMessage? TransportMessage { get; }
            public Message? Message { get; }
            public Dictionary<string, string>? Headers { get; }
        }

        public static void StartBus(this Container container)
        {
            container.GetInstance<IBus>();
        }

        public static void AutomaticallyFlowUserContext(this OptionsConfigurer configurer, Container container)
        {
            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = new UserFlowStep(container);
                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.After, typeof(DeserializeIncomingMessageStep))
                    .OnSend(step, PipelineRelativePosition.Before, typeof(SerializeOutgoingMessageStep));
            });
        }

        public static void UseApplicationInsight(this OptionsConfigurer configurer, Container container)
        {
            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var step = new ApplicationInsightsStep(container);
                return new PipelineStepConcatenator(
                    new PipelineStepInjector(pipeline)
                        .OnSend(step, PipelineRelativePosition.Before, typeof(SerializeOutgoingMessageStep)))
                    .OnReceive(step, PipelineAbsolutePosition.Front)
                    ;
                ;
            });
        }

        public static void UseApplicationInsightMetrics(this OptionsConfigurer configurer, Container container)
        {
            configurer.Decorate<IPipeline>(c =>
            {
                var pipeline = c.Get<IPipeline>();
                var time = c.Get<IRebusTime>();
                var step = new ApplicationInsightsProcessingMetricsStep(container, time);

                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.Before, typeof(DispatchIncomingMessageStep))
                    ;
            });
        }

        public static Task RetryDeferred(this IBus bus, int maxRetries, TimeSpan delay, Action? onRetryFailure = null)
        {
            int cnt = 0;
            var currentContext = MessageContext.Current;
            if (!currentContext.Headers.TryGetValue("defer-retry", out var count)
                || !int.TryParse(count, out cnt)
                || cnt < maxRetries)
                return bus.Defer(delay, new Dictionary<string, string>
                {
                    { "defer-retry", (++cnt).ToString(CultureInfo.InvariantCulture) }
                });

            onRetryFailure?.Invoke();

            return Task.CompletedTask;
        }

        [Obsolete("Use UseDrainableInMemoryTransport", true)]
        public static void UseTestsInMemoryTransport(this StandardConfigurer<ITransport> configurer, InMemNetwork network, string inputQueueName)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (network == null) throw new ArgumentNullException(nameof(network));
            if (inputQueueName == null) throw new ArgumentNullException(nameof(inputQueueName));

            configurer.OtherService<TestsInMemTransport>()
                .Register(context => new TestsInMemTransport(network, inputQueueName));

            configurer.OtherService<ITransportInspector>()
                .Register(context => context.Get<TestsInMemTransport>());

            configurer.Register(context => context.Get<TestsInMemTransport>());
        }

        [Obsolete("Use UseDrainableInMemoryTransportAsOneWay", true)]
        public static void UseTestsInMemoryTransportAsOneWayClient(this StandardConfigurer<ITransport> configurer, InMemNetwork network)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (network == null) throw new ArgumentNullException(nameof(network));

            configurer.Register(c => new TestsInMemTransport(network, null));

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }

    }
}
