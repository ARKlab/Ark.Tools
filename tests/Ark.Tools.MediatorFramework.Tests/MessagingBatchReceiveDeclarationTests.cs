// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using AwesomeAssertions;

using Azure.Messaging.ServiceBus;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Verifies the AMF-06 and AMF-07 batch-receive declarations that need no broker.</summary>
[TestClass]
public sealed class MessagingBatchReceiveDeclarationTests
{
    private const string _serviceBusConnection =
        "Endpoint=sb://localhost/;SharedAccessKeyName=test;SharedAccessKey=dGVzdA==";

    [TestMethod]
    public void StorageQueueReportsBatchOfThirtyTwoAndTheConfiguredVisibilityAsItsLock()
    {
        var transport = new StorageQueueMessagingTransport(
            "UseDevelopmentStorage=true",
            receiveVisibilityTimeout: TimeSpan.FromMinutes(2));

        var capabilities = transport.ReceiverCapabilities;

        capabilities.MaximumBatchSize.Should().Be(32);
        StorageQueueMessagingTransport.MaximumReceiveBatchSize.Should().Be(32);
        capabilities.SupportsServerSideWait.Should().BeFalse();
        capabilities.SupportsLockRenewal.Should().BeTrue();
        capabilities.NativeLockDuration.Should().Be(TimeSpan.FromMinutes(2));
    }

    [TestMethod]
    public void StorageQueueVisibilityTimeoutCoversHandlerBufferWaitAndRenewalMargin()
    {
        var options = new MessagingProcessingOptions
        {
            InitialConcurrency = 4,
            MaximumConcurrency = 64,
            PrefetchMultiplier = 2,
            ExpectedHandlerDuration = TimeSpan.FromSeconds(2),
            RenewalSafetyMargin = TimeSpan.FromSeconds(10)
        };

        var derived = StorageQueueMessagingTransport.DeriveReceiveVisibilityTimeout(
            TimeSpan.FromSeconds(30),
            options);

        // Handler (30 s) + one buffered delivery per worker at 2 s + the 10 s renewal margin.
        derived.Should().Be(TimeSpan.FromSeconds(42));
    }

    [TestMethod]
    public void StorageQueueVisibilityDerivationFailsBeyondTheServiceMaximum()
    {
        var derive = static () => StorageQueueMessagingTransport.DeriveReceiveVisibilityTimeout(
            TimeSpan.FromDays(7));

        derive.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    [TestMethod]
    public async Task ServiceBusDefaultsToABatchOfOneHundredAndSurfacesTheEntityLockDuration()
    {
#pragma warning disable CA2000 // The transport owns and disposes the client.
        await using var defaults = new ServiceBusMessagingTransport(new ServiceBusClient(_serviceBusConnection));
        await using var tuned = new ServiceBusMessagingTransport(
            new ServiceBusClient(_serviceBusConnection),
            maximumReceiveBatchSize: 10,
            receiveChannels: 3,
            lockDuration: TimeSpan.FromSeconds(45));
#pragma warning restore CA2000

        defaults.ReceiverCapabilities.MaximumBatchSize.Should().Be(100);
        defaults.ReceiverCapabilities.SupportsServerSideWait.Should().BeTrue();
        defaults.ReceiverCapabilities.SupportsLockRenewal.Should().BeTrue();
        defaults.ReceiverCapabilities.NativeLockDuration.Should().BeNull();

        tuned.ReceiverCapabilities.MaximumBatchSize.Should().Be(10);
        tuned.ReceiverCapabilities.NativeLockDuration.Should().Be(TimeSpan.FromSeconds(45));
    }

    [TestMethod]
    public void ProcessorHostRejectsALockShorterThanTheRenewalCadence()
    {
        var options = new MessagingProcessingOptions
        {
            RenewalSafetyMargin = TimeSpan.FromSeconds(10),
            RenewalScanInterval = TimeSpan.FromSeconds(1)
        };
        var source = new StubSource(new MessagingReceiverCapabilities(32, false, true, TimeSpan.FromSeconds(11)));

        var compose = () => new MessagingProcessorHost(
            source,
            "queue",
            static (_, _) => Task.CompletedTask,
            options);

        compose.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ProcessingOptionsInvalid);
    }

    [TestMethod]
    public void ProcessorHostAcceptsALockLongerThanTheRenewalCadence()
    {
        var options = new MessagingProcessingOptions
        {
            RenewalSafetyMargin = TimeSpan.FromSeconds(10),
            RenewalScanInterval = TimeSpan.FromSeconds(1)
        };
        var source = new StubSource(new MessagingReceiverCapabilities(32, false, true, TimeSpan.FromMinutes(1)));

        var compose = () => new MessagingProcessorHost(
            source,
            "queue",
            static (_, _) => Task.CompletedTask,
            options);

        compose.Should().NotThrow();
    }

    private sealed class StubSource : IMessagingMessageSource
    {
        internal StubSource(MessagingReceiverCapabilities capabilities)
        {
            ReceiverCapabilities = capabilities;
        }

        public MessagingReceiverCapabilities ReceiverCapabilities { get; }

        public async ValueTask<IReadOnlyList<IMessagingLockedDelivery>> ReceiveBatchAsync(
            string queue,
            int maxMessages,
            TimeSpan maxWait,
            CancellationToken ctk)
        {
            await Task.Delay(maxWait, ctk).ConfigureAwait(false);
            return Array.Empty<IMessagingLockedDelivery>();
        }
    }
}
