// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Ark.Tools.MediatorFramework.Messaging;

using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

using AwesomeAssertions;

using Microsoft.Extensions.Configuration;

namespace Ark.Tools.MediatorFramework.Tests;

/// <summary>Covers the Service Bus entity-shaping options and their reconciliation.</summary>
[TestClass]
public sealed class MessagingProvisioningOptionsTests
{
    [TestMethod]
    public async Task CreatedQueueCarriesTheDeclaredShape()
    {
        var administration = new RecordingAdministration();
        var management = new ServiceBusTransportManagement(
            administration,
            new ServiceBusMessagingOptions
            {
                EnablePartitioning = true,
                LockDuration = TimeSpan.FromMinutes(2),
                MaxSizeInMegabytes = 5120,
                MaxMessageSizeInKilobytes = 2048
            });

        await management.EnsureQueueAsync("consumer-a", 4, "consumer-a", default).ConfigureAwait(false);

        administration.Created.Should().NotBeNull();
        administration.Created!.EnablePartitioning.Should().BeTrue();
        administration.Created.LockDuration.Should().Be(TimeSpan.FromMinutes(2));
        administration.Created.MaxSizeInMegabytes.Should().Be(5120);
        administration.Created.MaxMessageSizeInKilobytes.Should().Be(2048);
    }

    [TestMethod]
    public async Task CreatedQueueIsNotPartitionedByDefault()
    {
        var administration = new RecordingAdministration();
        var management = new ServiceBusTransportManagement(administration);

        await management.EnsureQueueAsync("consumer-a", 4, "consumer-a", default).ConfigureAwait(false);

        administration.Created.Should().NotBeNull();
        administration.Created!.EnablePartitioning.Should().BeFalse();
        administration.Created.LockDuration.Should().Be(TimeSpan.FromSeconds(60));
    }

    [TestMethod]
    public async Task PartitioningMismatchNamesEntityValuesAndRemediation()
    {
        var administration = new ExistingAdministration(partitioned: false, lockDuration: TimeSpan.FromSeconds(60));
        var management = new ServiceBusTransportManagement(
            administration,
            new ServiceBusMessagingOptions { EnablePartitioning = true });

        var act = () => management.EnsureQueueAsync("consumer-a", 4, "consumer-a", default);

        var exception = (await act.Should().ThrowAsync<MessagingCompositionException>().ConfigureAwait(false)).Which;
        exception.Diagnostic.Should().Be(MessagingCompositionDiagnostic.ImmutableEntitySettingMismatch);
        exception.Message.Should().Contain("consumer-a")
            .And.Contain("EnablePartitioning=False")
            .And.Contain("EnablePartitioning=True")
            .And.Contain("delete and recreate");
        administration.Updated.Should().BeNull();
    }

    [TestMethod]
    public async Task LockDurationIsReconciledOnAnExistingQueue()
    {
        var administration = new ExistingAdministration(partitioned: false, lockDuration: TimeSpan.FromSeconds(30));
        var management = new ServiceBusTransportManagement(
            administration,
            new ServiceBusMessagingOptions { LockDuration = TimeSpan.FromMinutes(2) });

        await management.EnsureQueueAsync("consumer-a", 2, "consumer-a", default).ConfigureAwait(false);

        administration.Updated.Should().NotBeNull();
        administration.Updated!.LockDuration.Should().Be(TimeSpan.FromMinutes(2));
    }

    [TestMethod]
    public async Task AMatchingExistingQueueIsLeftAlone()
    {
        var administration = new ExistingAdministration(partitioned: false, lockDuration: TimeSpan.FromSeconds(60));
        var management = new ServiceBusTransportManagement(administration);

        await management.EnsureQueueAsync("consumer-a", 2, "consumer-a", default).ConfigureAwait(false);

        administration.Updated.Should().BeNull();
    }

    [TestMethod]
    public void ConfigurationBindingProducesTheFluentDeclaration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ServiceBus:EnablePartitioning"] = "true",
                ["ServiceBus:LockDuration"] = "00:02:00",
                ["ServiceBus:MaxSizeInMegabytes"] = "5120"
            })
            .Build();

        var bound = new ServiceBusMessagingOptions();
        configuration.GetSection("ServiceBus").Bind(bound);

        var declared = new ServiceBusMessagingOptions
        {
            EnablePartitioning = true,
            LockDuration = TimeSpan.FromMinutes(2),
            MaxSizeInMegabytes = 5120
        };

        bound.EnablePartitioning.Should().Be(declared.EnablePartitioning);
        bound.LockDuration.Should().Be(declared.LockDuration);
        bound.MaxSizeInMegabytes.Should().Be(declared.MaxSizeInMegabytes);
        bound.MaxMessageSizeInKilobytes.Should().Be(declared.MaxMessageSizeInKilobytes);
    }

    [TestMethod]
    public void OutOfRangeLockDurationFailsComposition()
    {
        var act = static () => new ServiceBusMessagingOptions { LockDuration = TimeSpan.FromMinutes(10) }.Validate();

        act.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.TransportOptionsInvalid);
    }

    [TestMethod]
    public void ALockShorterThanTheRenewalWindowFailsComposition()
    {
        var processing = new MessagingProcessingOptions
        {
            RenewalSafetyMargin = TimeSpan.FromSeconds(10),
            RenewalScanInterval = TimeSpan.FromSeconds(1)
        };
        var options = new ServiceBusMessagingOptions { LockDuration = TimeSpan.FromSeconds(10) };

        var act = () => options.Validate(processing);

        act.Should().Throw<MessagingCompositionException>()
            .Which.Diagnostic.Should().Be(MessagingCompositionDiagnostic.TransportOptionsInvalid);

        options.LockDuration = TimeSpan.FromSeconds(30);
        options.Validate(processing);
    }

    [TestMethod]
    public async Task NegativeSizeLimitsAreRejectedBeforeAnyCall()
    {
        var administration = new RecordingAdministration();

        var act = () => new ServiceBusTransportManagement(
            administration,
            new ServiceBusMessagingOptions { MaxSizeInMegabytes = -1 });

        act.Should().Throw<MessagingCompositionException>();
        administration.Created.Should().BeNull();
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private sealed class RecordingAdministration : ServiceBusAdministrationClient
    {
        public CreateQueueOptions? Created { get; private set; }

        public override async Task<Response<QueueProperties>> CreateQueueAsync(
            CreateQueueOptions options,
            CancellationToken cancellationToken = default)
        {
            Created = options;
            return await Task.FromResult<Response<QueueProperties>>(null!).ConfigureAwait(false);
        }
    }

    private sealed class ExistingAdministration : ServiceBusAdministrationClient
    {
        private readonly QueueProperties _queue;

        public ExistingAdministration(bool partitioned, TimeSpan lockDuration)
        {
            _queue = ServiceBusModelFactory.QueueProperties(
                "consumer-a",
                lockDuration: lockDuration,
                maxSizeInMegabytes: 1024,
                requiresDuplicateDetection: false,
                requiresSession: false,
                defaultMessageTimeToLive: TimeSpan.FromDays(14),
                autoDeleteOnIdle: TimeSpan.MaxValue,
                duplicateDetectionHistoryTimeWindow: TimeSpan.FromMinutes(10),
                maxDeliveryCount: 2,
                enableBatchedOperations: true,
                status: EntityStatus.Active,
                enablePartitioning: partitioned,
                userMetadata: "iac");
        }

        public QueueProperties? Updated { get; private set; }

        public override async Task<Response<QueueProperties>> CreateQueueAsync(
            CreateQueueOptions options,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            throw new ServiceBusException(
                "Queue exists.",
                ServiceBusFailureReason.MessagingEntityAlreadyExists);
        }

        public override async Task<Response<QueueProperties>> GetQueueAsync(
            string queueName,
            CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Response.FromValue(_queue, null!)).ConfigureAwait(false);
        }

        public override async Task<Response<QueueProperties>> UpdateQueueAsync(
            QueueProperties queue,
            CancellationToken cancellationToken = default)
        {
            Updated = queue;
            return await Task.FromResult(Response.FromValue(queue, null!)).ConfigureAwait(false);
        }
    }
}
