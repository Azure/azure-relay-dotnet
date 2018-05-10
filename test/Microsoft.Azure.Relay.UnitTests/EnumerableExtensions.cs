// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    static class EnumerableExtensions
    {
        public static async Task ParallelBatchAsync<TSource>(this IEnumerable<TSource> sources, int batchSize, int parallelTasksCount, Func<IEnumerable<TSource>, Task> asyncTask)
        {
            List<Task> tasks = new List<Task>();
            int remainingCount = sources.Count();
            IEnumerable<TSource> remainingEntities = sources;
            while (remainingCount > 0)
            {
                IEnumerable<TSource> batch;
                int currentBatchSize;
                if (remainingCount > batchSize)
                {
                    currentBatchSize = batchSize;
                    batch = remainingEntities.Take(currentBatchSize);
                    remainingEntities = remainingEntities.Skip(currentBatchSize);
                }
                else
                {
                    currentBatchSize = remainingCount;
                    batch = remainingEntities;
                }

                tasks.Add(asyncTask(batch));
                if (tasks.Count == parallelTasksCount)
                {
                    await Task.WhenAll(tasks.ToArray());
                    tasks.Clear();
                }

                remainingCount -= currentBatchSize;
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }
    }
}
