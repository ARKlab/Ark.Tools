// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using Microsoft.Extensions.Hosting;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Reports a failed resource-management operation with structured context.</summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "The operation and resource context are required for every instance.")]
public sealed class MessagingResourceManagementException : InvalidOperationException
{
    /// <summary>Creates a resource-management failure.</summary>
    /// <param name="operation">The failed management operation.</param>
    /// <param name="resource">The affected resource.</param>
    /// <param name="innerException">The provider failure.</param>
    public MessagingResourceManagementException(
        string operation,
        string resource,
        Exception innerException)
        : base(
            string.Format(
                CultureInfo.InvariantCulture,
                "Messaging resource operation '{0}' failed for '{1}'.",
                operation,
                resource),
            innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(operation);
        ArgumentException.ThrowIfNullOrEmpty(resource);
        Operation = operation;
        Resource = resource;
    }

    /// <summary>Gets the failed management operation.</summary>
    public string Operation { get; }

    /// <summary>Gets the affected resource.</summary>
    public string Resource { get; }
}

/// <summary>Reconciles a generated participant resource manifest.</summary>
public sealed class MessagingResourceReconciler
{
    private readonly IMessagingTransportManagement _management;

    /// <summary>Creates a reconciler over the selected transport management seam.</summary>
    /// <param name="management">The transport management implementation.</param>
    public MessagingResourceReconciler(IMessagingTransportManagement management)
    {
        _management = management ?? throw new ArgumentNullException(nameof(management));
    }

    /// <summary>Reconciles the desired resources in dependency order.</summary>
    /// <param name="manifest">The generated desired resources.</param>
    /// <param name="ctk">The cancellation token.</param>
    /// <returns>A task that completes after reconciliation.</returns>
    public async Task ReconcileAsync(
        MessagingResourceManifest manifest,
        CancellationToken ctk)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.Lifecycle == MessagingResourceLifecycle.External)
            return;

        if (manifest.IdentityQueue is { } queue)
        {
            await _runAsync(
                "ensure-queue",
                queue,
                token => _management.EnsureQueueAsync(
                    queue,
                    manifest.MaximumDeliveryCount,
                    manifest.ParticipantIdentity,
                    token),
                ctk).ConfigureAwait(false);
        }

        foreach (var topic in manifest.Topics)
        {
            await _runAsync(
                "ensure-topic",
                topic.Name,
                token => _management.EnsureTopicAsync(topic.Name, topic.OwnerIdentity, token),
                ctk).ConfigureAwait(false);
        }

        foreach (var subscription in manifest.Subscriptions)
        {
            await _runAsync(
                "ensure-subscription",
                subscription.Topic + "/" + subscription.Name,
                token => _management.EnsureSubscriptionAsync(subscription, token),
                ctk).ConfigureAwait(false);
        }

        var desired = manifest.Subscriptions.ToDictionary(
            static subscription => (subscription.Topic, subscription.Name));
        foreach (var topic in manifest.KnownNetworkTopics)
        {
            var existing = await _runAsync(
                "list-subscriptions",
                topic,
                token => _management.GetSubscriptionsAsync(topic, token),
                ctk).ConfigureAwait(false);
            foreach (var subscription in existing)
            {
                if (string.Equals(
                        subscription.OwnerIdentity,
                        manifest.ParticipantIdentity,
                        StringComparison.Ordinal)
                    && !desired.ContainsKey((topic, subscription.Name)))
                {
                    await _runAsync(
                        "delete-subscription",
                        topic + "/" + subscription.Name,
                        token => _management.DeleteSubscriptionAsync(topic, subscription.Name, token),
                        ctk).ConfigureAwait(false);
                }
            }
        }
    }

    private static async Task _runAsync(
        string operation,
        string resource,
        Func<CancellationToken, Task> action,
        CancellationToken ctk)
    {
        try
        {
            await action(ctk).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ctk.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MessagingResourceManagementException(operation, resource, ex);
        }
    }

    private static async Task<T> _runAsync<T>(
        string operation,
        string resource,
        Func<CancellationToken, Task<T>> action,
        CancellationToken ctk)
    {
        try
        {
            return await action(ctk).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ctk.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new MessagingResourceManagementException(operation, resource, ex);
        }
    }
}

internal sealed class MessagingResourceStartupService : IHostedService
{
    private readonly MessagingResourceManifest _manifest;
    private readonly MessagingResourceReconciler _reconciler;

    public MessagingResourceStartupService(
        MessagingResourceManifest manifest,
        MessagingResourceReconciler reconciler)
    {
        _manifest = manifest;
        _reconciler = reconciler;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _reconciler.ReconcileAsync(_manifest, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
