// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

namespace Ark.Tools.MediatorFramework.Grpc;

/// <summary>Controls exception details included in unexpected gRPC errors.</summary>
public sealed class ArkGrpcErrorOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether exception details are included outside development.
    /// Development environments always include details.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
