using Rebus.Bus;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Pipeline.Send;
using Rebus.Retry.Simple;
using Rebus.Transport;
using Rebus.Transport.InMem;
using SimpleInjector;
using System;
using System.Collections.Generic;

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
            public ITransactionContext TransactionContext { get; }
            public IncomingStepContext IncomingStepContext { get; }
            public TransportMessage TransportMessage { get; }
            public Message Message { get; }
            public Dictionary<string, string> Headers { get; }
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
                return new PipelineStepInjector(pipeline)
                    .OnReceive(step, PipelineRelativePosition.Before, typeof(SimpleRetryStrategyStep))
                    ;
            });
        }

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

        public static void UseTestsInMemoryTransportAsOneWayClient(this StandardConfigurer<ITransport> configurer, InMemNetwork network)
        {
            if (configurer == null) throw new ArgumentNullException(nameof(configurer));
            if (network == null) throw new ArgumentNullException(nameof(network));

            configurer.Register(c => new TestsInMemTransport(network, null));

            OneWayClientBackdoor.ConfigureOneWayClient(configurer);
        }
    }
}
