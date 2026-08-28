// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.MediatorFramework.Sample.Application.DAL;
using Ark.Tools.MediatorFramework.Messaging;
using Ark.Tools.Outbox;
using Ark.Tools.Sql.SqlServer;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.DependencyInjection;

using NLog;

namespace Ark.MediatorFramework.Sample.OutboxProcessor;

/// <summary>Runs the dedicated native messaging outbox processor host.</summary>
public static class Program
{
    /// <summary>Starts the processor until the process is cancelled.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task that completes when the processor stops.</returns>
    public static async Task Main(string[] args)
    {
        try
        {
            var serviceBusConnection = Environment.GetEnvironmentVariable(
                "ARK_SAMPLE_SERVICEBUS_CONNECTION");
            if (string.IsNullOrWhiteSpace(serviceBusConnection))
            {
                throw new InvalidOperationException(
                    "ARK_SAMPLE_SERVICEBUS_CONNECTION is required.");
            }

            var sqlConnection = Environment.GetEnvironmentVariable(
                "ARK_SAMPLE_SQL_CONNECTION");
            if (string.IsNullOrWhiteSpace(sqlConnection))
                throw new InvalidOperationException("ARK_SAMPLE_SQL_CONNECTION is required.");
            IOutboxAsyncContextFactory outboxFactory = new SampleDataContextFactory(
                new SqlConnectionManager(),
                new SampleDataContextConfig(sqlConnection));

#pragma warning disable CA2000 // The transport owns and disposes the Service Bus client.
            var transport = new ServiceBusMessagingTransport(
                new ServiceBusClient(serviceBusConnection));
#pragma warning restore CA2000
            await using var __transport = transport.ConfigureAwait(false);
            var services = OutboxProcessorComposition.BuildServices(
                transport,
                outboxFactory);
            await using var __services = services.ConfigureAwait(false);
            var processor = services.GetRequiredService<MessagingOutboxProcessor>();
            using var stopping = new CancellationTokenSource();
            Console.CancelKeyPress += (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
#pragma warning disable MA0045 // Console cancellation callbacks are synchronous.
                stopping.Cancel();
#pragma warning restore MA0045
            };

            await processor.StartAsync(stopping.Token).ConfigureAwait(false);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                // Process shutdown.
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            LogManager.GetLogger("Main").Fatal(
                exception,
                CultureInfo.InvariantCulture,
                "Unhandled native outbox processor failure: {Message}",
                exception.Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            LogManager.Flush(TimeSpan.FromSeconds(5));
            LogManager.Shutdown();
        }
    }
}
