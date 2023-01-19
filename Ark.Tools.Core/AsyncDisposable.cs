// Copyright (c) 2023 Ark Energy S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    public static class AsyncDisposable
    {

        private class AnonymousDisposable : IAsyncDisposable
        {
            private volatile Func<ValueTask>? _cleanup;

            public AnonymousDisposable(Func<ValueTask> cleanup)
            {
                _cleanup = cleanup;
            }

            /// <summary>
            /// Gets a value that indicates whether the object is disposed.
            /// </summary>
            public bool IsDisposed => _cleanup == null;

            public ValueTask DisposeAsync()
            {
                return Interlocked.Exchange(ref _cleanup, null)?.Invoke() ?? default;
            }
        }

        public static IAsyncDisposable Create(Func<ValueTask> cleanup)
        {
            return new AnonymousDisposable(cleanup);
        }
    }
}
