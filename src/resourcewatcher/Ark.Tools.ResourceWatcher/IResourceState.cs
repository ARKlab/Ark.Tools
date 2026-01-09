// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

namespace Ark.Tools.ResourceWatcher;

public interface IResourceState
{
    Instant RetrievedAt { get; }
    /// <summary>
    /// Checksum provided back to the data retriver to avoid processing of the resource (i.e. parsing). 
    /// It's checked even in case of new IResourceInfo.Modified in case of spurious modifications.
    /// </summary>
    string? CheckSum { get; }
}