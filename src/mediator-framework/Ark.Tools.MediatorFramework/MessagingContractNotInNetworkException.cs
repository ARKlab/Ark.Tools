// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework;

/// <summary>Indicates that a contract lookup targeted a network that does not declare the contract.</summary>
public sealed class MessagingContractNotInNetworkException : InvalidOperationException
{
    /// <summary>Creates an empty contract lookup exception.</summary>
    public MessagingContractNotInNetworkException()
    {
        ContractType = null;
        NetworkIdentity = string.Empty;
    }

    /// <summary>Creates a contract lookup exception with a message.</summary>
    /// <param name="message">The exception message.</param>
    public MessagingContractNotInNetworkException(string message)
        : base(message)
    {
        ContractType = null;
        NetworkIdentity = string.Empty;
    }

    /// <summary>Creates a contract lookup exception with a message and inner exception.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public MessagingContractNotInNetworkException(string message, Exception innerException)
        : base(message, innerException)
    {
        ContractType = null;
        NetworkIdentity = string.Empty;
    }

    /// <summary>Creates an exception for a missing contract lookup.</summary>
    /// <param name="contractType">The contract type that was not declared.</param>
    /// <param name="networkIdentity">The network identity searched.</param>
    public MessagingContractNotInNetworkException(Type contractType, string networkIdentity)
        : base(
            string.Format(
                CultureInfo.InvariantCulture,
                "Contract '{0}' is not declared by network '{1}'.",
                contractType?.FullName ?? contractType?.Name ?? "<unknown>",
                networkIdentity))
    {
        ContractType = contractType ?? throw new ArgumentNullException(nameof(contractType));
        NetworkIdentity = networkIdentity ?? throw new ArgumentNullException(nameof(networkIdentity));
    }

    /// <summary>Gets the missing contract type.</summary>
    public Type? ContractType { get; }

    /// <summary>Gets the network identity searched.</summary>
    public string NetworkIdentity { get; }
}
