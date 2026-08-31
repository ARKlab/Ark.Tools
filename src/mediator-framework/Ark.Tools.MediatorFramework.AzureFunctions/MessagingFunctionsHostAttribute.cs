// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Identifies the trigger binding selected by an Azure Functions messaging host.</summary>
public enum MessagingFunctionsTriggerBinding
{
    /// <summary>Azure Service Bus PeekLock trigger.</summary>
    ServiceBus = 0,

    /// <summary>Azure Storage Queue trigger.</summary>
    StorageQueue = 1
}

/// <summary>Binds an Azure Functions app to one messaging participant.</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MessagingFunctionsHostAttribute : Attribute
{
    /// <summary>Creates the binding for one participant and trigger selection.</summary>
    /// <param name="participant">The participant declaration type.</param>
    /// <param name="binding">The compile-time trigger binding.</param>
    public MessagingFunctionsHostAttribute(
        Type participant,
        MessagingFunctionsTriggerBinding binding)
    {
        Participant = participant ?? throw new ArgumentNullException(nameof(participant));
        Binding = binding;
    }

    /// <summary>Gets the bound participant declaration type.</summary>
    public Type Participant { get; }

    /// <summary>Gets the compile-time trigger binding selection.</summary>
    public MessagingFunctionsTriggerBinding Binding { get; }

    /// <summary>Gets or sets the host configuration key containing the transport connection.</summary>
    public string? ConnectionConfigurationKey { get; set; }

    /// <summary>Gets or sets host-local incoming pipeline step types.</summary>
    public Type[] IncomingSteps { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets host-local outgoing pipeline step types.</summary>
    public Type[] OutgoingSteps { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets whether Storage Queue host-setting mismatches fail startup.</summary>
    public bool StrictStorageQueueHostSettings { get; set; }
}

/// <summary>Generic Azure Functions host binding for a participant declaration.</summary>
/// <typeparam name="TParticipant">The generated participant declaration type.</typeparam>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MessagingFunctionsHostAttribute<TParticipant> : Attribute
    where TParticipant : class, global::Ark.Tools.MediatorFramework.IMessagingParticipantDeclaration
{
    /// <summary>Gets the bound participant declaration type.</summary>
    public Type Participant => typeof(TParticipant);

    /// <summary>Gets the compile-time trigger binding selection.</summary>
    public MessagingFunctionsTriggerBinding Binding { get; }

    /// <summary>Gets or sets the host configuration key containing the transport connection.</summary>
    public string? ConnectionConfigurationKey { get; set; }

    /// <summary>Gets or sets host-local incoming pipeline step types.</summary>
    public Type[] IncomingSteps { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets host-local outgoing pipeline step types.</summary>
    public Type[] OutgoingSteps { get; set; } = Array.Empty<Type>();

    /// <summary>Gets or sets whether Storage Queue host-setting mismatches fail startup.</summary>
    public bool StrictStorageQueueHostSettings { get; set; }

    /// <summary>Creates the binding for a participant declaration and trigger selection.</summary>
    /// <param name="binding">The compile-time trigger binding.</param>
    public MessagingFunctionsHostAttribute(MessagingFunctionsTriggerBinding binding)
    {
        Binding = binding;
    }
}
