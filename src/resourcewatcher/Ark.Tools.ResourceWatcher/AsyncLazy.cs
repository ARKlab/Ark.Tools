// Copyright (C) 2024 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.ResourceWatcher
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
    /// This is a lightweight implementation replacing Nito.AsyncEx.Coordination.AsyncLazy.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
    public sealed class AsyncLazy<T>
    {
        private readonly Lock _mutex = new();
        private readonly Func<Task<T>> _factory;
        private Lazy<Task<T>> _instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy{T}"/> class.
        /// </summary>
        /// <param name="factory">The asynchronous delegate that is invoked to produce the value when it is needed.</param>
        public AsyncLazy(Func<Task<T>> factory)
        {
            ArgumentNullException.ThrowIfNull(factory);
            _factory = () => System.Threading.Tasks.Task.Run(factory);
            _instance = new Lazy<Task<T>>(_factory);
        }

        /// <summary>
        /// Whether the asynchronous factory method has started. 
        /// This is initially <c>false</c> and becomes <c>true</c> when this instance is awaited.
        /// </summary>
        public bool IsStarted
        {
            get
            {
                lock (_mutex)
                    return _instance.IsValueCreated;
            }
        }

        /// <summary>
        /// Gets the task representing the asynchronous initialization.
        /// </summary>
        public Task<T> Task
        {
            get
            {
                lock (_mutex)
                    return _instance.Value;
            }
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy{T}"/> to be awaited.
        /// </summary>
        public TaskAwaiter<T> GetAwaiter()
        {
            return Task.GetAwaiter();
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy{T}"/> to be awaited.
        /// </summary>
        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext)
        {
            return Task.ConfigureAwait(continueOnCapturedContext);
        }
    }
}
