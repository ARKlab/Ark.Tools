// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.MediatorFramework;

/// <summary>Controls ownership of broker resources declared by a messaging network.</summary>
public enum MessagingResourceLifecycle
{
    /// <summary>Resources are created and removed by the host.</summary>
    Managed,

    /// <summary>Resources are expected to be provisioned outside the host.</summary>
    External,
}
