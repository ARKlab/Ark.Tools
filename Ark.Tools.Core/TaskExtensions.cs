// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    public static class TaskExtensions
    {
        public static Task IgnoreExceptions(this Task task)
        {
            task.ContinueWith(c => { var ignored = c.Exception; },
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously);
            return task;
        }

        public static Task FailFastOnException(this Task task)
        {
            task.ContinueWith(c => Environment.FailFast("Task faulted", c.Exception),
                TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously);
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
}
