// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;


namespace Ark.Tools.ResourceWatcher;


public class ResourceState : IResourceTrackedState
{
    public virtual string? Tenant { get; set; }
    public virtual string ResourceId { get; set; } = string.Empty;
    public virtual string? CheckSum { get; set; }
    public virtual LocalDateTime Modified { get; set; }
    public virtual Dictionary<string, LocalDateTime>? ModifiedSources { get; set; }
    public virtual Instant LastEvent { get; set; }
    public virtual Instant? RetrievedAt { get; set; }
    public virtual int RetryCount { get; set; }
    public virtual object? Extensions { get; set; }
    public virtual Exception? LastException { get; set; }
}