// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using NodaTime;

using System;

namespace Ark.Tools.ResourceWatcher.WorkerHost;

public class DefaultHostConfig : IHostConfig
{
    public virtual string WorkerName { get; set; } = "Worker";
    public virtual uint DegreeOfParallelism { get; set; } = (uint)Environment.ProcessorCount;
    public virtual bool IgnoreState { get; set; }
    public virtual TimeSpan Sleep { get; set; } = TimeSpan.FromMinutes(5);
    public virtual uint MaxRetries { get; set; } = 5;
    public virtual uint? SkipResourcesOlderThanDays { get; set; }
    public virtual Duration BanDuration { get; set; } = Duration.FromHours(24);
    public TimeSpan RunDurationNotificationLimit { get; set; } = TimeSpan.FromMinutes(60);
    public TimeSpan ResourceDurationNotificationLimit { get; set; } = TimeSpan.FromMinutes(10);
}