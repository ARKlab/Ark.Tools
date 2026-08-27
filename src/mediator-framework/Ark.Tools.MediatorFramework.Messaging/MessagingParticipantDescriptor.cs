// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System.Collections.ObjectModel;

using Ark.Tools.Solid;

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Dispatches one generated participant contract.</summary>
/// <param name="logicalName">The logical contract name.</param>
/// <param name="payload">The prepared payload reader.</param>
/// <param name="processor">The scoped command processor.</param>
/// <param name="ctk">The cancellation token.</param>
/// <returns>A task that completes after dispatch.</returns>
public delegate Task MessagingDispatch(
    string logicalName,
    IMessagingPayloadReader payload,
    ICommandProcessor processor,
    CancellationToken ctk);

/// <summary>Dispatches one generated participant second-level failure.</summary>
/// <param name="logicalName">The logical contract name.</param>
/// <param name="payload">The prepared payload reader.</param>
/// <param name="deliveryCount">The native delivery count.</param>
/// <param name="error">The bounded exception snapshot.</param>
/// <param name="processor">The scoped command processor.</param>
/// <param name="ctk">The cancellation token.</param>
/// <returns>A task that completes after dispatch.</returns>
public delegate Task MessagingFailedDispatch(
    string logicalName,
    IMessagingPayloadReader payload,
    int deliveryCount,
    MessagingExceptionInfo error,
    ICommandProcessor processor,
    CancellationToken ctk);

/// <summary>Provides the generated runtime metadata for one network participant.</summary>
public sealed class MessagingParticipantDescriptor
{
    /// <summary>Creates a generated participant descriptor.</summary>
    /// <param name="participantType">The participant declaration type.</param>
    /// <param name="network">The resolved network options.</param>
    /// <param name="registry">The generated network contract registry.</param>
    /// <param name="identity">The resolved participant identity.</param>
    /// <param name="serializers">The serializers supported by the participant.</param>
    /// <param name="retryPolicy">The participant retry policy.</param>
    /// <param name="compression">The outgoing compression algorithm.</param>
    /// <param name="compressionMinimumSizeBytes">The minimum payload size eligible for compression.</param>
    /// <param name="receives">Whether the participant receives contracts.</param>
    /// <param name="dispatch">The generated normal dispatch binder.</param>
    /// <param name="dispatchFailed">The generated second-level failure binder.</param>
    public MessagingParticipantDescriptor(
        Type participantType,
        MessagingNetworkOptions network,
        IMessagingContractRegistry registry,
        string identity,
        IEnumerable<SerializationProtocol> serializers,
        IMessagingRetryPolicy retryPolicy,
        CompressionAlgorithm compression,
        int compressionMinimumSizeBytes,
        bool receives,
        MessagingDispatch? dispatch,
        MessagingFailedDispatch? dispatchFailed)
    {
        ParticipantType = participantType ?? throw new ArgumentNullException(nameof(participantType));
        Network = network ?? throw new ArgumentNullException(nameof(network));
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        ArgumentException.ThrowIfNullOrEmpty(identity);
        ArgumentNullException.ThrowIfNull(serializers);
        ArgumentNullException.ThrowIfNull(retryPolicy);
        ArgumentOutOfRangeException.ThrowIfNegative(compressionMinimumSizeBytes);
        if (!string.Equals(network.NetworkIdentity, registry.NetworkIdentity, StringComparison.Ordinal))
            throw new ArgumentException("The registry and network identities must match.", nameof(registry));
        if (receives && dispatch is null)
            throw new ArgumentNullException(nameof(dispatch), "A receive participant requires a generated dispatch binder.");

        MessagingRetryPolicyValidation.Validate(retryPolicy);
        Identity = identity;
        Serializers = new ReadOnlyCollection<SerializationProtocol>(serializers.Distinct().ToArray());
        RetryPolicy = retryPolicy;
        Compression = compression;
        CompressionMinimumSizeBytes = compressionMinimumSizeBytes;
        Receives = receives;
        Dispatch = dispatch;
        DispatchFailed = dispatchFailed;
    }

    /// <summary>Gets the participant declaration type.</summary>
    public Type ParticipantType { get; }

    /// <summary>Gets the resolved network options.</summary>
    public MessagingNetworkOptions Network { get; }

    /// <summary>Gets the generated network contract registry.</summary>
    public IMessagingContractRegistry Registry { get; }

    /// <summary>Gets the resolved participant identity.</summary>
    public string Identity { get; }

    /// <summary>Gets the serializers supported by the participant.</summary>
    public IReadOnlyList<SerializationProtocol> Serializers { get; }

    /// <summary>Gets the participant retry policy.</summary>
    public IMessagingRetryPolicy RetryPolicy { get; }

    /// <summary>Gets the outgoing compression algorithm.</summary>
    public CompressionAlgorithm Compression { get; }

    /// <summary>Gets the minimum payload size eligible for compression.</summary>
    public int CompressionMinimumSizeBytes { get; }

    /// <summary>Gets whether the participant receives contracts.</summary>
    public bool Receives { get; }

    /// <summary>Gets the generated normal dispatch binder.</summary>
    public MessagingDispatch? Dispatch { get; }

    /// <summary>Gets the generated second-level failure binder.</summary>
    public MessagingFailedDispatch? DispatchFailed { get; }

    /// <summary>Creates the participant payload sender over the shared DataBus.</summary>
    /// <param name="dataBus">The shared network DataBus.</param>
    /// <returns>The configured payload sender.</returns>
    public MessagingPayloadSender CreatePayloadSender(IMessagingDataBus dataBus)
    {
        return new MessagingPayloadSender(
            dataBus,
            Network,
            Compression,
            CompressionMinimumSizeBytes);
    }
}

/// <summary>Provides the framework retry defaults for participants without an explicit policy.</summary>
public sealed class MessagingDefaultRetryPolicy : IMessagingRetryPolicy
{
    private MessagingDefaultRetryPolicy()
    {
    }

    /// <summary>Gets the shared default policy.</summary>
    public static MessagingDefaultRetryPolicy Instance { get; } = new();

    /// <inheritdoc />
    public int MaximumDeliveryCount => 1;

    /// <inheritdoc />
    public bool SecondLevelRetriesEnabled => false;

    /// <inheritdoc />
    public TimeSpan MaximumHandlerDuration => TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public TimeSpan RetryDelay => TimeSpan.Zero;
}
