//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    class WebSocketStream : HybridConnectionStream
    {
        readonly WebSocket webSocket;
        string toString;

        public WebSocketStream(WebSocket webSocket, TrackingContext trackingContext)
        {
            this.webSocket = webSocket;
            this.TrackingContext = trackingContext;
            this.ReadTimeout = Timeout.Infinite;
            this.WriteTimeout = Timeout.Infinite;
        }

        public override bool CanRead
        {
            get
            {
                return this.webSocket.State != WebSocketState.CloseReceived && this.webSocket.State != WebSocketState.Closed;
            }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanTimeout
        {
            get { return this.webSocket.State != WebSocketState.Closed; }
        }

        public override bool CanWrite
        {
            get
            {
                return this.webSocket.State != WebSocketState.CloseSent && this.webSocket.State != WebSocketState.Closed;
            }
        }

        public override long Length
        {
            get { throw RelayEventSource.Log.ThrowingException(new NotSupportedException(), this); }
        }

        public override long Position
        {
            get { throw RelayEventSource.Log.ThrowingException(new NotSupportedException(), this); }

            set { throw RelayEventSource.Log.ThrowingException(new NotSupportedException(), this); }
        }

        public override int ReadTimeout
        {
            get; set;
        }

        public override int WriteTimeout
        {
            get; set;
        }

        TrackingContext TrackingContext { get; set; }

        public override string ToString()
        {
            if (this.toString == null)
            {
                this.toString = typeof(HybridConnectionStream).Name + "(" + this.TrackingContext + ")";
            }

            return this.toString;
        }

        protected override async Task OnCloseAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Combine the provided CancellationToken with one using the ReadTimeout value
                using (var timeoutCancelSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.ReadTimeout)))
                using (var linkedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancelSource.Token, cancellationToken))
                {
                    await this.webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "CloseAsync", linkedCancelSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                RelayEventSource.Log.HandledExceptionAsWarning(this, exception);
                this.webSocket.Abort();
            }
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return TaskEx.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            try
            {
                // Combine the provided CancellationToken with one using the ReadTimeout value
                using (var timeoutCancelSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.ReadTimeout)))
                using (var linkedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancelSource.Token, cancelToken))
                {
                    WebSocketReceiveResult result = await this.webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer, offset, count), linkedCancelSource.Token).ConfigureAwait(false);
                    return result.Count;
                }
            }
            catch (WebSocketException webSocketException)
            {
                throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToRelayContract(webSocketException), this);
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.ReadAsync(buffer, offset, count, CancellationToken.None).ToAsyncResult(callback, state, true);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskEx.EndAsyncResult<int>(asyncResult);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Combine the provided CancellationToken with one using the WriteTimeout value
                using (var timeoutCancelSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.WriteTimeout)))
                using (var linkedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancelSource.Token, cancellationToken))
                {
                    await this.webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", linkedCancelSource.Token).ConfigureAwait(false);
                }
            }
            catch (WebSocketException webSocketException)
            {
                throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToRelayContract(webSocketException), this);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAsync(buffer, offset, count, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            try
            {
                // Combine the provided CancellationToken with one using the WriteTimeout value
                using (var timeoutCancelSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(this.WriteTimeout)))
                using (var linkedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancelSource.Token, cancelToken))
                {
                    await this.webSocket.SendAsync(
                        new ArraySegment<byte>(buffer, offset, count), this.WriteMode == WriteMode.Binary ? WebSocketMessageType.Binary : WebSocketMessageType.Text, true, linkedCancelSource.Token).ConfigureAwait(false);
                }
            }
            catch (WebSocketException webSocketException)
            {
                throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToRelayContract(webSocketException), this);
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.WriteAsync(buffer, offset, count, CancellationToken.None).ToAsyncResult(callback, state, true);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            TaskEx.EndAsyncResult(asyncResult);
        }
    }
}
