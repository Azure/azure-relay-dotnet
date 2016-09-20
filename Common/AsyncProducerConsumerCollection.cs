//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Provides an asynchronous producer/consumer collection.</summary>
    [DebuggerDisplay("Count={CurrentCount}")]
    sealed class AsyncProducerConsumerCollection<T> : IDisposable
    {
        /// <summary>Asynchronous semaphore used to keep track of asynchronous work.</summary>
        private SemaphoreSlim _semaphore = new SemaphoreSlim(0, int.MaxValue);
        /// <summary>The data stored in the collection.</summary>
        private IProducerConsumerCollection<T> _collection;

        /// <summary>Initializes the asynchronous producer/consumer collection to store data in a first-in-first-out (FIFO) order.</summary>
        public AsyncProducerConsumerCollection() : this(new ConcurrentQueue<T>()) { }

        /// <summary>Initializes the asynchronous producer/consumer collection.</summary>
        /// <param name="collection">The underlying collection to use to store data.</param>
        public AsyncProducerConsumerCollection(IProducerConsumerCollection<T> collection)
        {
            if (collection == null) throw new ArgumentNullException("collection");
            _collection = collection;
        }

        /// <summary>Adds an element to the collection.</summary>
        /// <param name="item">The item to be added.</param>
        public void Add(T item)
        {
            if (_collection.TryAdd(item)) _semaphore.Release();
            else throw new InvalidOperationException("Invalid collection");
        }

        /// <summary>Takes an element from the collection asynchronously.</summary>
        /// <returns>A Task that represents the element removed from the collection.</returns>
        public Task<T> TakeAsync()
        {
            return _semaphore.WaitAsync().ContinueWith(_ =>
            {
                T result;
                if (!_collection.TryTake(out result)) throw new InvalidOperationException("Invalid collection");
                return result;
            }, TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /// <summary>Gets the number of elements in the collection.</summary>
        public int Count { get { return _collection.Count; } }

        /// <summary>Disposes of the collection.</summary>
        public void Dispose()
        {
            if (_semaphore != null)
            {
                _semaphore.Dispose();
                _semaphore = null;
            }
        }
    }
}