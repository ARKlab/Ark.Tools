// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Threading;
using System.Threading.Tasks;
<<<<<<< TODO: Unmerged change from project 'Ark.Tools.Core(net10.0)', Before:
namespace Ark.Tools.Core
{
#pragma warning disable MA0134 // Observe result of async calls
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
    public static class TaskExtensions
    {
        public static Task IgnoreExceptions(this Task task)
        {
            task.ContinueWith(c => { var ignored = c.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }

        public static Task FailFastOnException(this Task task)
        {
            task.ContinueWith(c => Environment.FailFast("Task faulted", c.Exception),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        }

        /// <summary>
        /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
        /// </summary>
        /// <remarks>
        /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
        /// </remarks>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
        {
            return task.IsCompleted // fast-path optimization
                ? task
                : task.ContinueWith(
                    completedTask => completedTask.GetAwaiter().GetResult(),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }

        /// <summary>
        /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
        /// </summary>
        /// <remarks>
        /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
        /// </remarks>
        /// <param name="task"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
        {
            return task.IsCompleted // fast-path optimization
                ? task
                : task.ContinueWith(
                    completedTask => completedTask.GetAwaiter().GetResult(),
                    cancellationToken,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
        }
    }
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
#pragma warning restore MA0134 // Observe result of async calls
}
=======
namespace Ark.Tools.Core;

#pragma warning disable MA0134 // Observe result of async calls
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
public static class TaskExtensions
{
    public static Task IgnoreExceptions(this Task task)
    {
        task.ContinueWith(c => { var ignored = c.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    public static Task FailFastOnException(this Task task)
    {
        task.ContinueWith(c => Environment.FailFast("Task faulted", c.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    /// <summary>
    /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
    /// </summary>
    /// <remarks>
    /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
    /// </remarks>
    /// <param name="task"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        return task.IsCompleted // fast-path optimization
            ? task
            : task.ContinueWith(
                completedTask => completedTask.GetAwaiter().GetResult(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    /// <summary>
    /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
    /// </summary>
    /// <remarks>
    /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
    /// </remarks>
    /// <param name="task"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        return task.IsCompleted // fast-path optimization
            ? task
            : task.ContinueWith(
                completedTask => completedTask.GetAwaiter().GetResult(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
#pragma warning restore MA0134 // Observe result of async calls
>>>>>>> After


namespace Ark.Tools.Core;

#pragma warning disable MA0134 // Observe result of async calls
#pragma warning disable MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
public static class TaskExtensions
{
    public static Task IgnoreExceptions(this Task task)
    {
        task.ContinueWith(c => { var ignored = c.Exception; },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    public static Task FailFastOnException(this Task task)
    {
        task.ContinueWith(c => Environment.FailFast("Task faulted", c.Exception),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted |
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    /// <summary>
    /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
    /// </summary>
    /// <remarks>
    /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
    /// </remarks>
    /// <param name="task"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        return task.IsCompleted // fast-path optimization
            ? task
            : task.ContinueWith(
                completedTask => completedTask.GetAwaiter().GetResult(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    /// <summary>
    /// Adopt a cancellationToken, abondoning the original Task in case of cancellation.
    /// </summary>
    /// <remarks>
    /// The original Task is left running, not awaited, in case of cancellation. To be used on Task that doesn't accept a Cancellation.
    /// </remarks>
    /// <param name="task"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        return task.IsCompleted // fast-path optimization
            ? task
            : task.ContinueWith(
                completedTask => completedTask.GetAwaiter().GetResult(),
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
#pragma warning restore MA0045 // Do not use blocking calls in a sync method (need to make calling method async)
#pragma warning restore MA0134 // Observe result of async calls