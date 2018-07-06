using NodaTime;
using System;

namespace Ark.Tools.ResourceWatcher.WorkerHost
{
    public interface IHostConfig
    {
        /// <summary>
        /// Name of the worker. Also used as "tenant" for the State-Tracking 
        /// </summary>
        string WorkerName { get; }
        /// <summary>
        /// Number of Resources that can be processed ("writed") in parallel
        /// </summary>
        uint DegreeOfParallelism { get; }
        /// <summary>
        /// Ignore the state found in the State-Tracking but still update the state on success or error
        /// </summary>
        bool IgnoreState { get; }
        /// <summary>
        /// Sleep time between each POLL
        /// </summary>
        TimeSpan Sleep { get; }
        /// <summary>
        /// The number of times a resource is processed, for each "Version"(IResourceInfo.Modified)
        /// </summary>
        uint MaxRetries { get; }
        /// <summary>
        /// Skip any resource with IResourceInfo.Modified older than this number of days
        /// </summary>
        uint? SkipResourcesOlderThanDays { get; }
        /// <summary>
        /// The Duration of a BAN.
        /// </summary>
        /// <remarks>
        /// When a resource gets to MaxRetries it is considered BANned for this duration and new Modified are ignored.
        /// </remarks>
        Duration BanDuration { get; }
        /// <summary>
        /// The limit on the Run duration for the purpose of notification
        /// </summary>
        /// <remarks>
        /// If a Run takes more than the given time, a notification is triggered
        /// </remarks>
        TimeSpan RunDurationNotificationLimit { get; }
        /// <summary>
        /// The limit on the resource duration for the purpose of notification
        /// </summary>
        /// <remarks>
        /// If the processing of a resource takes more than the given time, a notification is triggered
        /// </remarks>
        TimeSpan ResourceDurationNotificationLimit { get; }
    }
}
