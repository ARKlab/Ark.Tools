// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework.Messaging;

/// <summary>Immutable resolved settings for one messaging network.</summary>
public sealed record MessagingNetworkOptions
{
    /// <summary>Creates resolved network settings.</summary>
    public MessagingNetworkOptions(
        Type networkType,
        MessagingCapabilities requires,
        IReadOnlyList<SerializationProtocol> serializers,
        SerializationProtocol defaultSerializer,
        CompressionAlgorithm compression,
        int compressionMinimumSizeBytes,
        int maximumTransportPayloadBytes,
        int maximumDecompressedPayloadBytes,
        int dataBusOffloadThresholdBytes,
        int maximumDataBusAttachmentBytes,
        IMessagingRetryPolicy retryPolicy,
        TimeSpan lockRenewalBuffer,
        TimeSpan maximumSchedulingDelay,
        MessagingResourceLifecycle resourceLifecycle,
        string connectionConfigurationKey,
        string managedIdentityConfigurationKey)
        : this(
            networkType,
            requires,
            serializers,
            defaultSerializer,
            compression,
            compressionMinimumSizeBytes,
            maximumTransportPayloadBytes,
            maximumDecompressedPayloadBytes,
            dataBusOffloadThresholdBytes,
            maximumDataBusAttachmentBytes,
            retryPolicy,
            lockRenewalBuffer,
            maximumSchedulingDelay,
            resourceLifecycle,
            connectionConfigurationKey,
            managedIdentityConfigurationKey,
            Array.Empty<MessagingContractDescriptor>())
    {
    }

    /// <summary>Creates resolved network settings with its immutable contract registry.</summary>
    public MessagingNetworkOptions(
        Type networkType,
        MessagingCapabilities requires,
        IReadOnlyList<SerializationProtocol> serializers,
        SerializationProtocol defaultSerializer,
        CompressionAlgorithm compression,
        int compressionMinimumSizeBytes,
        int maximumTransportPayloadBytes,
        int maximumDecompressedPayloadBytes,
        int dataBusOffloadThresholdBytes,
        int maximumDataBusAttachmentBytes,
        IMessagingRetryPolicy retryPolicy,
        TimeSpan lockRenewalBuffer,
        TimeSpan maximumSchedulingDelay,
        MessagingResourceLifecycle resourceLifecycle,
        string connectionConfigurationKey,
        string managedIdentityConfigurationKey,
        IReadOnlyList<MessagingContractDescriptor> contracts)
    {
        NetworkType = networkType ?? throw new ArgumentNullException(nameof(networkType));
        Requires = requires;
        Serializers = (serializers ?? throw new ArgumentNullException(nameof(serializers))).ToArray();
        DefaultSerializer = defaultSerializer;
        Compression = compression;
        CompressionMinimumSizeBytes = compressionMinimumSizeBytes;
        MaximumTransportPayloadBytes = maximumTransportPayloadBytes;
        MaximumDecompressedPayloadBytes = maximumDecompressedPayloadBytes;
        DataBusOffloadThresholdBytes = dataBusOffloadThresholdBytes;
        MaximumDataBusAttachmentBytes = maximumDataBusAttachmentBytes;
        RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        LockRenewalBuffer = lockRenewalBuffer;
        MaximumSchedulingDelay = maximumSchedulingDelay;
        ResourceLifecycle = resourceLifecycle;
        ConnectionConfigurationKey = connectionConfigurationKey ?? throw new ArgumentNullException(nameof(connectionConfigurationKey));
        ManagedIdentityConfigurationKey = managedIdentityConfigurationKey ?? throw new ArgumentNullException(nameof(managedIdentityConfigurationKey));
        Contracts = (contracts ?? throw new ArgumentNullException(nameof(contracts))).ToArray();
        _validateSettings();
    }

    /// <summary>Profile type that identifies the network.</summary>
    public Type NetworkType { get; }
    /// <summary>Required capabilities.</summary>
    public MessagingCapabilities Requires { get; }
    /// <summary>Accepted receive protocols.</summary>
    public IReadOnlyList<SerializationProtocol> Serializers { get; }
    /// <summary>Default send protocol.</summary>
    public SerializationProtocol DefaultSerializer { get; }
    /// <summary>Compression algorithm.</summary>
    public CompressionAlgorithm Compression { get; }
    /// <summary>Minimum size for compression.</summary>
    public int CompressionMinimumSizeBytes { get; }
    /// <summary>Maximum transport payload size.</summary>
    public int MaximumTransportPayloadBytes { get; }
    /// <summary>Maximum decompressed payload size.</summary>
    public int MaximumDecompressedPayloadBytes { get; }
    /// <summary>DataBus offload threshold.</summary>
    public int DataBusOffloadThresholdBytes { get; }
    /// <summary>Maximum DataBus attachment size.</summary>
    public int MaximumDataBusAttachmentBytes { get; }
    /// <summary>Shared retry policy.</summary>
    public IMessagingRetryPolicy RetryPolicy { get; }
    /// <summary>Lock renewal buffer.</summary>
    public TimeSpan LockRenewalBuffer { get; }
    /// <summary>Maximum scheduling delay.</summary>
    public TimeSpan MaximumSchedulingDelay { get; }
    /// <summary>Resource ownership policy.</summary>
    public MessagingResourceLifecycle ResourceLifecycle { get; }
    /// <summary>Transport connection configuration key.</summary>
    public string ConnectionConfigurationKey { get; }
    /// <summary>Managed identity configuration key.</summary>
    public string ManagedIdentityConfigurationKey { get; }

    /// <summary>Registered message and event contracts.</summary>
    public IReadOnlyList<MessagingContractDescriptor> Contracts { get; }

    /// <summary>Validates the composed transport capabilities.</summary>
    public void Validate(MessagingCapabilities transportCapabilities)
    {
        var missing = Requires & ~transportCapabilities;
        if (missing != MessagingCapabilities.None)
            throw new InvalidOperationException($"Transport is missing messaging capability '{missing}'.");
    }

    private void _validateSettings()
    {
        if (Serializers.Count == 0 || !Serializers.Contains(DefaultSerializer))
            throw new ArgumentException("The default serializer must be one of the accepted serializers.", nameof(Serializers));
        if (CompressionMinimumSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(CompressionMinimumSizeBytes));
        if (MaximumTransportPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumTransportPayloadBytes));
        if (MaximumDecompressedPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumDecompressedPayloadBytes));
        if (DataBusOffloadThresholdBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(DataBusOffloadThresholdBytes));
        if (MaximumDataBusAttachmentBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaximumDataBusAttachmentBytes));
        if (LockRenewalBuffer < TimeSpan.Zero || MaximumSchedulingDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(LockRenewalBuffer));
        if (RetryPolicy.MaximumDeliveryCount < 1
            || (RetryPolicy.SecondLevelRetriesEnabled && RetryPolicy.MaximumDeliveryCount < 2))
            throw new ArgumentOutOfRangeException(nameof(RetryPolicy), "MaximumDeliveryCount must be at least 1, or at least 2 with second-level retries.");
        if (RetryPolicy.MaximumHandlerDuration < TimeSpan.Zero || RetryPolicy.RetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RetryPolicy), "Retry durations cannot be negative.");
    }
}
