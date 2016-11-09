//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    class AsyncLock : IDisposable
    {
        private readonly SemaphoreSlim asyncSemaphore;

        private readonly Task<LockRelease> lockRelease;

        private bool disposed = false;

        public AsyncLock()
        {
            asyncSemaphore = new SemaphoreSlim(1);
            lockRelease = Task.FromResult(new LockRelease(this));
        }

        public Task<LockRelease> LockAsync()
        {
            var wait = asyncSemaphore.WaitAsync();
            if (wait.IsCompleted)
            {
                return lockRelease;
            }

            return wait.ContinueWith(
                (_, state) => new LockRelease((AsyncLock)state),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public Task<LockRelease> LockAsync(CancellationToken cancellationToken)
        {
            var wait = asyncSemaphore.WaitAsync(cancellationToken);

            // Note this check is on RanToCompletion; not IsCompleted which could be RanToCompletion, Faulted, or Canceled.
            if (wait.Status == TaskStatus.RanToCompletion)
            {
                return lockRelease;
            }

            // Since we pass the cancellationToken here if it gets cancelled the task returned from 
            // ContinueWith will itself be Cancelled.
            return wait.ContinueWith(
                (t, state) =>
                {
                    if (t.IsFaulted)
                    {
                        // AggregateException.GetBaseException gets the first AggregateException with more than one inner exception
                        // OR the first exception that's not an AggregateException.
                        throw t.Exception.GetBaseException().Rethrow();
                    }

                    return new LockRelease((AsyncLock)state);
                },
                this,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    this.asyncSemaphore.Dispose();
#if NET45
                    // This is only disposing the Task...
                    this.lockRelease.Dispose();
#endif
                }

                this.disposed = true;
            }
        }

        public struct LockRelease : IDisposable
        {
            private readonly AsyncLock asyncLockRelease;
            private bool disposed;

            internal LockRelease(AsyncLock release)
            {
                this.asyncLockRelease = release;
                this.disposed = false;
            }

            public void Dispose()
            {
                Dispose(true);
            }

            void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        if (asyncLockRelease != null)
                        {
                            asyncLockRelease.asyncSemaphore.Release();
                        }
                    }

                    this.disposed = true;
                }
            }
        }
    }
}
