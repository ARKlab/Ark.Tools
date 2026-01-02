// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.
using Ark.ResourceWatcher.Sample.Processor;
using Ark.ResourceWatcher.Sample.Provider;
using Ark.Tools.ResourceWatcher.WorkerHost;

using NodaTime;

namespace Ark.ResourceWatcher.Sample.Config
{
    /// <summary>
    /// Configuration for the Blob Worker Host.
    /// </summary>
    public sealed class MyWorkerHostConfig : IHostConfig, IMyResourceProcessorConfig, IMyStorageResourceProviderConfig
    {
        /// <summary>
        /// Gets the worker name used to identify this worker in state tracking.
        /// </summary>
        public string WorkerName { get; init; } = "BlobWorker";

        /// <summary>
        /// Gets the polling interval between runs.
        /// </summary>
        public TimeSpan Sleep { get; init; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets the maximum number of retries before banning a resource.
        /// </summary>
        public uint MaxRetries { get; init; } = 3;

        /// <summary>
        /// Gets the duration for which a resource is banned after exceeding max retries.
        /// </summary>
        public Duration BanDuration { get; init; } = Duration.FromHours(24);

        /// <summary>
        /// Gets the degree of parallelism for processing resources.
        /// </summary>
        public uint DegreeOfParallelism { get; init; } = 4;

        /// <summary>
        /// Gets whether to ignore state tracking (reprocess all resources).
        /// </summary>
        public bool IgnoreState { get; init; }

        /// <summary>
        /// Gets the number of days after which old resources are skipped.
        /// </summary>
        public uint? SkipResourcesOlderThanDays { get; init; }

        /// <summary>
        /// Gets the notification limit for run duration.
        /// </summary>
        public TimeSpan RunDurationNotificationLimit { get; init; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets the notification limit for resource processing duration.
        /// </summary>
        public TimeSpan ResourceDurationNotificationLimit { get; init; } = TimeSpan.FromMinutes(5);
        public required Uri ProviderUrl { get; init; }
        public required Uri SinkUrl { get; init; }
    }
}