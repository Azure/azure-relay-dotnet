// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Tracing;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class InputQueue<T> : IDisposable where T : class
    {
        readonly ItemQueue itemQueue;
        readonly Queue<IQueueReader> readerQueue;
        readonly List<IQueueWaiter> waiterList;
        QueueState queueState;

        public InputQueue()
        {
            this.itemQueue = new ItemQueue();
            this.readerQueue = new Queue<IQueueReader>();
            this.waiterList = new List<IQueueWaiter>();
            this.queueState = QueueState.Open;
        }

        public int PendingCount
        {
            get
            {
                lock (ThisLock)
                {
                    return this.itemQueue.ItemCount;
                }
            }
        }

        public int ReadersQueueCount
        {
            get
            {
                lock (ThisLock)
                {
                    return this.readerQueue.Count;
                }
            }
        }

        // Users like ServiceModel can hook this abort ICommunicationObject or handle other non-IDisposable objects
        public Action<T> DisposeItemCallback { get; set; }

        object ThisLock => this.itemQueue;

        public Task<T> DequeueAsync(CancellationToken cancellationToken)
        {
            return this.DequeueAsync(cancellationToken, null);
        }

        public Task<T> DequeueAsync(CancellationToken cancellationToken, object state)
        {
            Item item = default(Item);
            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else
                    {
                        var reader = new AsyncQueueReader(this, cancellationToken, state);
                        readerQueue.Enqueue(reader);
                        return reader.Task;
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        AsyncQueueReader reader = new AsyncQueueReader(this, cancellationToken, state);
                        readerQueue.Enqueue(reader);
                        return reader.Task;
                    }
                }
            }

            InvokeDequeuedCallback(item.DequeuedCallback);
            return Task.FromResult(item.GetValue());
        }

        public Task<bool> WaitForItemAsync(CancellationToken cancellationToken)
        {
            return this.WaitForItemAsync(cancellationToken, null);
        }

        public Task<bool> WaitForItemAsync(CancellationToken cancellationToken, object state)
        {
            lock (ThisLock)
            {
                if (queueState == QueueState.Open)
                {
                    if (!itemQueue.HasAvailableItem)
                    {
                        AsyncQueueWaiter waiter = new AsyncQueueWaiter(cancellationToken, state);
                        waiterList.Add(waiter);
                        return waiter.Task;
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (!itemQueue.HasAvailableItem && itemQueue.HasAnyItem)
                    {
                        AsyncQueueWaiter waiter = new AsyncQueueWaiter(cancellationToken, state);
                        waiterList.Add(waiter);
                        return waiter.Task;
                    }
                }
            }

            return Task.FromResult(true);
        }

        public void Close()
        {
            Dispose();
        }

        public void Dispatch()
        {
            IQueueReader reader = null;
            Item item = new Item();
            IQueueReader[] outstandingReaders = null;
            IQueueWaiter[] waiters;
            bool itemAvailable;

            lock (ThisLock)
            {
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                this.GetWaiters(out waiters);

                if (queueState != QueueState.Closed)
                {
                    itemQueue.MakePendingItemAvailable();

                    if (readerQueue.Count > 0)
                    {
                        item = itemQueue.DequeueAvailableItem();
                        reader = readerQueue.Dequeue();

                        if (queueState == QueueState.Shutdown && readerQueue.Count > 0 && itemQueue.ItemCount == 0)
                        {
                            outstandingReaders = new IQueueReader[readerQueue.Count];
                            readerQueue.CopyTo(outstandingReaders, 0);
                            readerQueue.Clear();

                            itemAvailable = false;
                        }
                    }
                }
            }

            if (outstandingReaders != null)
            {
                ActionItem.Schedule(s => CompleteOutstandingReadersCallback(s), outstandingReaders);
            }

            if (waiters != null)
            {
                CompleteWaitersLater(itemAvailable, waiters);
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }
        }

        public void EnqueueAndDispatch(T item)
        {
            EnqueueAndDispatch(item, null);
        }

        // dequeuedCallback is called as an item is dequeued from the InputQueue.  The 
        // InputQueue lock is not held during the callback.  However, the user code will
        // not be notified of the item being available until the callback returns.  If you
        // are not sure if the callback will block for a long time, then first call 
        // ActionItem.Schedule to get to a "safe" thread.
        public void EnqueueAndDispatch(T item, Action dequeuedCallback)
        {
            EnqueueAndDispatch(item, dequeuedCallback, true);
        }

        public void EnqueueAndDispatch(Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Fx.Assert(exception != null, "EnqueueAndDispatch: exception parameter should not be null");
            EnqueueAndDispatch(new Item(exception, dequeuedCallback), canDispatchOnThisThread);
        }

        public void EnqueueAndDispatch(T item, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Fx.Assert(item != null, "EnqueueAndDispatch: item parameter should not be null");
            EnqueueAndDispatch(new Item(item, dequeuedCallback), canDispatchOnThisThread);
        }

        public bool EnqueueWithoutDispatch(T item, Action dequeuedCallback)
        {
            Fx.Assert(item != null, "EnqueueWithoutDispatch: item parameter should not be null");
            return EnqueueWithoutDispatch(new Item(item, dequeuedCallback));
        }

        public bool EnqueueWithoutDispatch(Exception exception, Action dequeuedCallback)
        {
            Fx.Assert(exception != null, "EnqueueWithoutDispatch: exception parameter should not be null");
            return EnqueueWithoutDispatch(new Item(exception, dequeuedCallback));
        }

        public void Shutdown()
        {
            this.Shutdown(null);
        }

        // Don't let any more items in. Differs from Close in that we keep around
        // existing items in our itemQueue for possible future calls to Dequeue
        public void Shutdown(Func<Exception> pendingExceptionGenerator)
        {
            IQueueReader[] outstandingReaders = null;
            lock (ThisLock)
            {
                if (queueState == QueueState.Shutdown)
                {
                    return;
                }

                if (queueState == QueueState.Closed)
                {
                    return;
                }

                this.queueState = QueueState.Shutdown;

                if (readerQueue.Count > 0 && this.itemQueue.ItemCount == 0)
                {
                    outstandingReaders = new IQueueReader[readerQueue.Count];
                    readerQueue.CopyTo(outstandingReaders, 0);
                    readerQueue.Clear();
                }
            }

            if (outstandingReaders != null)
            {
                for (int i = 0; i < outstandingReaders.Length; i++)
                {
                    Exception exception = (pendingExceptionGenerator != null) ? pendingExceptionGenerator() : null;
                    outstandingReaders[i].Set(new Item(exception, null));
                }
            }
        }

        public void Dispose()
        {
            bool dispose = false;

            lock (ThisLock)
            {
                if (queueState != QueueState.Closed)
                {
                    queueState = QueueState.Closed;
                    dispose = true;
                }
            }

            if (dispose)
            {
                while (readerQueue.Count > 0)
                {
                    IQueueReader reader = readerQueue.Dequeue();
                    reader.Set(default(Item));
                }

                while (itemQueue.HasAnyItem)
                {
                    Item item = itemQueue.DequeueAnyItem();
                    DisposeItem(item);
                    InvokeDequeuedCallback(item.DequeuedCallback);
                }
            }
        }

        void DisposeItem(Item item)
        {
            T value = item.Value;
            if (value != null)
            {
                IDisposable disposableValue = value as IDisposable;
                if (disposableValue != null)
                {
                    disposableValue.Dispose();
                }
                else
                {
                    this.DisposeItemCallback?.Invoke(value);
                }
            }
        }

        static void CompleteOutstandingReadersCallback(object state)
        {
            IQueueReader[] outstandingReaders = (IQueueReader[])state;

            for (int i = 0; i < outstandingReaders.Length; i++)
            {
                outstandingReaders[i].Set(default(Item));
            }
        }

        static void CompleteWaiters(bool itemAvailable, IQueueWaiter[] waiters)
        {
            for (int i = 0; i < waiters.Length; i++)
            {
                waiters[i].Set(itemAvailable);
            }
        }

        static void CompleteWaitersFalseCallback(object state)
        {
            CompleteWaiters(false, (IQueueWaiter[])state);
        }

        static void CompleteWaitersLater(bool itemAvailable, IQueueWaiter[] waiters)
        {
            if (itemAvailable)
            {
                ActionItem.Schedule(s => CompleteWaitersTrueCallback(s), waiters);
            }
            else
            {
                ActionItem.Schedule(s => CompleteWaitersFalseCallback(s), waiters);
            }
        }

        static void CompleteWaitersTrueCallback(object state)
        {
            CompleteWaiters(true, (IQueueWaiter[])state);
        }

        static void InvokeDequeuedCallback(Action dequeuedCallback)
        {
            dequeuedCallback?.Invoke();
        }

        static void InvokeDequeuedCallbackLater(Action dequeuedCallback)
        {
            if (dequeuedCallback != null)
            {
                ActionItem.Schedule(s => OnInvokeDequeuedCallback(s), dequeuedCallback);
            }
        }

        static void OnDispatchCallback(object state)
        {
            ((InputQueue<T>)state).Dispatch();
        }

        static void OnInvokeDequeuedCallback(object state)
        {
            Fx.Assert(state != null, "InputQueue.OnInvokeDequeuedCallback: (state != null)");

            Action dequeuedCallback = (Action)state;
            dequeuedCallback();
        }

        void EnqueueAndDispatch(Item item, bool canDispatchOnThisThread)
        {
            bool disposeItem = false;
            IQueueReader reader = null;
            bool dispatchLater = false;
            IQueueWaiter[] waiters;
            bool itemAvailable;

            lock (ThisLock)
            {
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                this.GetWaiters(out waiters);

                if (queueState == QueueState.Open)
                {
                    if (canDispatchOnThisThread)
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            reader = readerQueue.Dequeue();
                        }
                    }
                    else
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            itemQueue.EnqueuePendingItem(item);
                            dispatchLater = true;
                        }
                    }
                }
                else // queueState == QueueState.Closed || queueState == QueueState.Shutdown
                {
                    disposeItem = true;
                }
            }

            if (waiters != null)
            {
                if (canDispatchOnThisThread)
                {
                    CompleteWaiters(itemAvailable, waiters);
                }
                else
                {
                    CompleteWaitersLater(itemAvailable, waiters);
                }
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }

            if (dispatchLater)
            {
                ActionItem.Schedule(s => OnDispatchCallback(s), this);
            }
            else if (disposeItem)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                DisposeItem(item);
            }
        }

        // This will not block, however, Dispatch() must be called later if this function
        // returns true.
        bool EnqueueWithoutDispatch(Item item)
        {
            lock (ThisLock)
            {
                // Open
                if (queueState != QueueState.Closed && queueState != QueueState.Shutdown)
                {
                    if (readerQueue.Count == 0 && waiterList.Count == 0)
                    {
                        itemQueue.EnqueueAvailableItem(item);
                        return false;
                    }

                    itemQueue.EnqueuePendingItem(item);
                    return true;
                }
            }

            DisposeItem(item);
            InvokeDequeuedCallbackLater(item.DequeuedCallback);
            return false;
        }

        void GetWaiters(out IQueueWaiter[] waiters)
        {
            if (waiterList.Count > 0)
            {
                waiters = waiterList.ToArray();
                waiterList.Clear();
            }
            else
            {
                waiters = null;
            }
        }

        // Used for timeouts. The InputQueue must remove readers from its reader queue to prevent
        // dispatching items to timed out readers.
        bool RemoveReader(IQueueReader reader)
        {
            Fx.Assert(reader != null, "InputQueue.RemoveReader: (reader != null)");

            lock (ThisLock)
            {
                if (queueState == QueueState.Open || queueState == QueueState.Shutdown)
                {
                    bool removed = false;

                    for (int i = readerQueue.Count; i > 0; i--)
                    {
                        IQueueReader temp = readerQueue.Dequeue();
                        if (object.ReferenceEquals(temp, reader))
                        {
                            removed = true;
                        }
                        else
                        {
                            readerQueue.Enqueue(temp);
                        }
                    }

                    return removed;
                }
            }

            return false;
        }

        enum QueueState
        {
            Open,
            Shutdown,
            Closed
        }

        interface IQueueReader
        {
            void Set(Item item);
        }

        interface IQueueWaiter
        {
            void Set(bool itemAvailable);
        }

        struct Item
        {
            public Item(T value, Action dequeuedCallback)
                : this(value, null, dequeuedCallback)
            {
            }

            public Item(Exception exception, Action dequeuedCallback)
                : this(null, exception, dequeuedCallback)
            {
            }

            Item(T value, Exception exception, Action dequeuedCallback)
            {
                this.Value = value;
                this.Exception = exception;
                this.DequeuedCallback = dequeuedCallback;
            }

            public Action DequeuedCallback { get; }

            public Exception Exception { get; }

            public T Value { get; }

            public T GetValue()
            {
                if (this.Exception != null)
                {
                    throw RelayEventSource.Log.ThrowingException(this.Exception, this, EventLevel.Informational);
                }

                return this.Value;
            }
        }

        class AsyncQueueReader : TaskCompletionSource<T>, IQueueReader
        {
            readonly InputQueue<T> inputQueue;
            readonly CancellationTokenRegistration cancelRegistration;

            public AsyncQueueReader(InputQueue<T> inputQueue, CancellationToken cancellationToken, object state)
                : base(state)
            {
                this.inputQueue = inputQueue;
                this.cancelRegistration = cancellationToken.Register(s => CancelCallback(s), this);
            }

            public void Set(Item inputItem)
            {
                this.cancelRegistration.Dispose();

                if (inputItem.Exception != null)
                {
                    this.TrySetException(inputItem.Exception);
                }
                else
                {
                    this.TrySetResult(inputItem.Value);
                }
            }

            static void CancelCallback(object state)
            {
                AsyncQueueReader thisPtr = (AsyncQueueReader)state;
                thisPtr.cancelRegistration.Dispose();
                if (thisPtr.inputQueue.RemoveReader(thisPtr))
                {
                    thisPtr.TrySetCanceled();
                }
            }
        }

        class AsyncQueueWaiter : TaskCompletionSource<bool>, IQueueWaiter
        {
            readonly CancellationTokenRegistration cancelRegistration;

            public AsyncQueueWaiter(CancellationToken cancellationToken, object state)
                : base(state)
            {
                this.cancelRegistration = cancellationToken.Register(s => CancelCallback(s), this);
            }

            public void Set(bool currentItemAvailable)
            {
                this.cancelRegistration.Dispose();
                this.TrySetResult(currentItemAvailable);
            }

            static void CancelCallback(object state)
            {
                var thisPtr = (AsyncQueueWaiter)state;
                thisPtr.cancelRegistration.Dispose();
                thisPtr.TrySetCanceled();
            }
        }

        class ItemQueue
        {
            int head;
            Item[] items;
            int pendingCount;
            int totalCount;

            public ItemQueue()
            {
                this.items = new Item[1];
            }

            public bool HasAnyItem => this.totalCount > 0;

            public bool HasAvailableItem => this.totalCount > this.pendingCount;

            public int ItemCount => this.totalCount;

            public Item DequeueAnyItem()
            {
                if (this.pendingCount == this.totalCount)
                {
                    this.pendingCount--;
                }
                return DequeueItemCore();
            }

            public Item DequeueAvailableItem()
            {
                if (this.totalCount == this.pendingCount)
                {
                    Fx.Assert(this.totalCount != this.pendingCount, "ItemQueue does not contain any available items");
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException("ItemQueue does not contain any available items"), this);
                }

                return DequeueItemCore();
            }

            public void EnqueueAvailableItem(Item item)
            {
                EnqueueItemCore(item);
            }

            public void EnqueuePendingItem(Item item)
            {
                EnqueueItemCore(item);
                this.pendingCount++;
            }

            public void MakePendingItemAvailable()
            {
                if (pendingCount == 0)
                {
                    Fx.Assert(this.pendingCount != 0, "ItemQueue does not contain any pending items");
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException("ItemQueue does not contain any pending items"), this);
                }

                this.pendingCount--;
            }

            Item DequeueItemCore()
            {
                if (totalCount == 0)
                {
                    Fx.Assert(totalCount != 0, "ItemQueue does not contain any items");
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException("ItemQueue does not contain any items"), this);
                }

                Item item = this.items[this.head];
                this.items[this.head] = new Item();
                this.totalCount--;
                this.head = (this.head + 1) % this.items.Length;
                return item;
            }

            void EnqueueItemCore(Item item)
            {
                if (this.totalCount == this.items.Length)
                {
                    Item[] newItems = new Item[this.items.Length * 2];
                    for (int i = 0; i < this.totalCount; i++)
                    {
                        newItems[i] = this.items[(head + i) % this.items.Length];
                    }
                    this.head = 0;
                    this.items = newItems;
                }
                int tail = (this.head + this.totalCount) % this.items.Length;
                this.items[tail] = item;
                this.totalCount++;
            }
        }
    }
}
