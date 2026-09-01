// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.AzureFunctions;

/// <summary>Provides the generated metadata of an Azure Functions messaging host.</summary>
/// <typeparam name="TSelf">The implementing host declaration.</typeparam>
public interface IMessagingFunctionsHost<TSelf>
    where TSelf : IMessagingFunctionsHost<TSelf>
{
    /// <summary>Gets the generated Functions host manifest.</summary>
    static abstract MessagingFunctionsManifest Manifest { get; }
}
