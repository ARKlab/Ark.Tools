// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

namespace Ark.Tools.ResourceWatcher
{
    public interface IResourceTrackedState : IResourceMetadata
    {
        int RetryCount { get; }
        Instant LastEvent { get; }
        string? CheckSum { get; }
        Instant? RetrievedAt { get; }
    }
}
