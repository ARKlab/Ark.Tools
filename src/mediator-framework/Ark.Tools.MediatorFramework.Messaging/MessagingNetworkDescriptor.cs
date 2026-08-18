// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Reflection;

namespace Ark.MediatorFramework.Messaging;

/// <summary>Resolves a declared network profile into immutable runtime options.</summary>
public static class MessagingNetworkDescriptor
{
    /// <summary>Resolves the <see cref="MessagingNetworkAttribute"/> on a profile type.</summary>
    /// <param name="networkType">The profile type.</param>
    /// <returns>The resolved options.</returns>
    public static MessagingNetworkOptions Resolve(Type networkType)
    {
        ArgumentNullException.ThrowIfNull(networkType);
        var attribute = networkType.GetCustomAttribute<MessagingNetworkAttribute>()
            ?? throw new InvalidOperationException($"Type '{networkType.FullName}' is not a messaging network profile.");
        var retryPolicy = _createRetryPolicy(attribute.RetryPolicy);
        var contracts = attribute.Contracts
            .Select(MessagingContractDescriptor.Resolve)
            .OrderBy(contract => contract.Name, StringComparer.Ordinal)
            .ToArray();
        return new MessagingNetworkOptions(
            networkType,
            attribute.Requires,
            attribute.Serializers,
            attribute.DefaultSerializer,
            attribute.Compression,
            attribute.CompressionMinimumSizeBytes,
            attribute.MaximumTransportPayloadBytes,
            attribute.MaximumDecompressedPayloadBytes,
            attribute.DataBusOffloadThresholdBytes,
            attribute.MaximumDataBusAttachmentBytes,
            retryPolicy,
            TimeSpan.FromSeconds(attribute.LockRenewalBufferSeconds),
            TimeSpan.FromSeconds(attribute.MaximumSchedulingDelaySeconds),
            attribute.ResourceLifecycle,
            attribute.ConnectionConfigurationKey,
            attribute.ManagedIdentityConfigurationKey,
            contracts);
    }

    private static IMessagingRetryPolicy _createRetryPolicy(Type? retryPolicyType)
    {
        if (retryPolicyType is null)
            return new DefaultMessagingRetryPolicy();
        if (!typeof(IMessagingRetryPolicy).IsAssignableFrom(retryPolicyType))
            throw new InvalidOperationException($"Retry policy '{retryPolicyType.FullName}' must implement {nameof(IMessagingRetryPolicy)}.");
        return (IMessagingRetryPolicy?)Activator.CreateInstance(retryPolicyType)
            ?? throw new InvalidOperationException($"Retry policy '{retryPolicyType.FullName}' must have a public parameterless constructor.");
    }

    private sealed class DefaultMessagingRetryPolicy : IMessagingRetryPolicy
    {
        public int MaximumDeliveryCount => 1;
        public bool SecondLevelRetriesEnabled => false;
        public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(5);
        public TimeSpan RetryDelay => TimeSpan.Zero;
    }
}
