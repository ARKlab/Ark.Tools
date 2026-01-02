using Rebus.Bus;
using Rebus.Config;
using Rebus.Exceptions;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Pipeline.Send;
using Rebus.Time;
using Rebus.Transport;

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

        sealed class FakeMessageContext : IMessageContext
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
            if (!currentContext.Headers.TryGetValue(Headers.DeferCount, out var count)
                || !int.TryParse(count, NumberStyles.Integer, CultureInfo.InvariantCulture, out cnt)
                || cnt < maxRetries)
                return bus.Advanced.TransportMessage.Defer(delay);
            else
            {
                onRetryFailure?.Invoke();
                return bus.Advanced.TransportMessage.Deadletter("RetryDeferred reached maxRetries=" + maxRetries);
            }
        }

    }
}