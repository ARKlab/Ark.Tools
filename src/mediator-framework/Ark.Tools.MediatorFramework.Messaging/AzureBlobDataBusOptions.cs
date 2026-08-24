// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Messaging;

/// <summary>Composition options for the Azure Blob DataBus provider.</summary>
public sealed record AzureBlobDataBusOptions
{
    /// <summary>Gets the dedicated container holding Mediator Framework attachments.</summary>
    public required string ContainerName { get; init; }

    /// <summary>
    /// Gets the blob-name prefix isolating this network's attachments for lifecycle cleanup.
    /// </summary>
    public string Prefix { get; init; } = "amf1/";

    /// <summary>
    /// Gets the minimum attachment lifetime required by the lifecycle policy.
    /// </summary>
    public required TimeSpan MinimumAttachmentLifetime { get; init; }

    /// <summary>
    /// Gets the Azure Blob Storage connection string or service URI. A service URI
    /// uses DefaultAzureCredential.
    /// </summary>
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Gets whether startup ensures the container exists instead of requiring IaC creation.
    /// </summary>
    public bool EnsureContainer { get; init; }
}
