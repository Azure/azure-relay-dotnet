//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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
        /// Closes this <see cref="HybridConnectionStream"/> instance.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.ReadTimeout)))
            {
                this.CloseAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            base.Dispose(disposing);
        }

#if NET45
        /// <summary>
        /// Closes this <see cref="HybridConnectionStream"/> instance.
        /// </summary>
        public override void Close()
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.ReadTimeout)))
            {
                this.CloseAsync(cts.Token).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            base.Close();
        }
#endif

        /// <summary>
        /// Closes this <see cref="HybridConnectionStream"/> instance asynchronously using a <see cref="CancellationToken"/>.
        /// </summary>
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
        protected abstract Task OnShutdownAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Derived classes implement close logic in this method.
        /// </summary>
        protected abstract Task OnCloseAsync(CancellationToken cancellationToken);
    }
}