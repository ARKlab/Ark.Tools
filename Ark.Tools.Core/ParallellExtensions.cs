// Copyright (c) 2018 Ark S.r.l. All rights reserved.
// Licensed under the MIT License. See LICENSE file for license information. 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Ark.Tools.Core
{
    public static class ParallellExtensions
    {
        //Parallell by Partitioner
        public static Task Parallel<T>(this IList<T> list, int degree, Func<T, Task> action)
        {
            return list.Parallel(degree, (i, x) => action?.Invoke(x));
        }

        public static Task Parallel<T>(this IList<T> list, int degree, Func<long, T, Task> action)
        {
            var tasks = System.Collections.Concurrent.Partitioner.Create(list, true)
                                .GetOrderablePartitions(degree)
                                .Select(async partition =>
                                {
                                    using (partition)
                                    {
                                        while (partition.MoveNext())
                                        {
                                            await action?.Invoke(partition.Current.Key, partition.Current.Value);
                                        }
                                    }
                                });

            return Task.WhenAll(tasks);
        }
    }
}
