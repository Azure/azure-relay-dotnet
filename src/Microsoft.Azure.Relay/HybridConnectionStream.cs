// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A Stream representing a connected HybridConnection.  Use it just like any other Stream with the addition of a
    /// Shutdown method for notifying the other side of this connection that shutdown is occurring.
    /// </summary>
    public abstract class HybridConnectionStream : Stream
    {
        string cachedToString;

        internal HybridConnectionStream(TrackingContext trackingContext)
        {
            this.TrackingContext = trackingContext;
        }
        /// <summary>
        /// Sets or gets the WriteMode for this stream. Default is WriteMode.Binary
        /// </summary>
        public WriteMode WriteMode { get; set; } = WriteMode.Binary;

        internal TrackingContext TrackingContext { get; }

        /// <summary>
        /// Initiates a graceful close process by shutting down sending through this 
        /// <see cref="HybridConnectionStream"/>. To disconnect cleanly and asynchronously, call Shutdown,
        /// wait for Read/ReadAsync to complete with a 0 byte read, then finally call Stream.Close();
        /// </summary>
        public virtual void Shutdown()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.WriteTimeout)))
            {
                this.ShutdownAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Initiates a graceful close process by shutting down sending through this 
        /// <see cref="HybridConnectionStream"/>. To disconnect cleanly and asynchronously, call ShutdownAsync,
        /// wait for Read/ReadAsync to complete with a 0 byte read, then finally call Stream.CloseAsync();
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                RelayEventSource.Log.RelayClientShutdownStart(this);
                await this.OnShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                RelayEventSource.Log.RelayClientShutdownStop();
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.  Includes a TrackingId for end to end correlation.
        /// </summary>
        public override string ToString()
        {
            return this.cachedToString ?? (this.cachedToString = nameof(HybridConnectionStream) + "(" + this.TrackingContext + ")");
        }

        /// <summary>
        /// Closes this <see cref="HybridConnectionStream"/> instance.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.ReadTimeout)))
                {
                    this.CloseAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Closes this <see cref="HybridConnectionStream"/> instance asynchronously using a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            try
            {
                RelayEventSource.Log.RelayClientCloseStart(this);
                await this.OnCloseAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                RelayEventSource.Log.RelayClientCloseException(this, e);
                throw;
            }
            finally
            {
                RelayEventSource.Log.RelayClientCloseStop();
            }
        }

        /// <summary>
        /// Derived classes implement shutdown logic in this method.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        protected abstract Task OnShutdownAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Derived classes implement close logic in this method.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        protected abstract Task OnCloseAsync(CancellationToken cancellationToken);
    }
}