// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.Solid;

using Rebus.Config;
using Rebus.Handlers;
using Rebus.Routing;
using Rebus.Routing.TypeBased;

using SimpleInjector;

namespace Ark.MediatorFramework.Sample.RebusProcessor;

internal static class SampleRebusEndpoints
{
    public static void RegisterHandlers(Container container)
    {
        container.Collection.Append<IHandleMessages<ProcessBookPrintProcessRequest>, ProcessHandler>();
        container.Collection.Append<IHandleMessages<FailingRebusRequest>, FailingHandler>();
        container.Collection.Append<
            IHandleMessages<Rebus.Retry.Simple.IFailed<ProcessBookPrintProcessRequest>>,
            Ark.Tools.MediatorFramework.Rebus.RebusMessagingFailedHandler<ProcessBookPrintProcessRequest>>();
    }

    public static void ConfigureRouting(StandardConfigurer<IRouter> routing)
    {
        routing.TypeBased()
            .Map<ProcessBookPrintProcessRequest>("ark-mediator-sample")
            .Map<FailingRebusRequest>("ark-mediator-sample");
    }

    private sealed class ProcessHandler(IRequestProcessor processor) :
        IHandleMessages<ProcessBookPrintProcessRequest>
    {
        public async Task Handle(ProcessBookPrintProcessRequest message)
        {
            await processor.ExecuteAsync<ProcessBookPrintProcessRequest, BookPrintProcessResponse>(
                message,
                Rebus.Extensions.MessageContextExtensions.GetCancellationToken(
                    Rebus.Pipeline.MessageContext.Current)).ConfigureAwait(false);
        }
    }

    private sealed class FailingHandler(IRequestProcessor processor) :
        IHandleMessages<FailingRebusRequest>
    {
        public async Task Handle(FailingRebusRequest message)
        {
            await processor.ExecuteAsync<FailingRebusRequest, DeadLetterAck>(
                message,
                Rebus.Extensions.MessageContextExtensions.GetCancellationToken(
                    Rebus.Pipeline.MessageContext.Current)).ConfigureAwait(false);
        }
    }
}
