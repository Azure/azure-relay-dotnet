// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    sealed class HybridHttpConnection : ITraceSource
    {
        const int MaxControlConnectionBodySize = 64 * 1024;
        const int BufferSize = 8 * 1024;
        readonly HybridConnectionListener listener;
        readonly WebSocket controlWebSocket;
        readonly Uri rendezvousAddress;
        WebSocket rendezvousWebSocket;

        HybridHttpConnection(HybridConnectionListener listener, WebSocket controlWebSocket, string rendezvousAddress)
        {
            this.listener = listener;
            this.controlWebSocket = controlWebSocket;
            this.rendezvousAddress = new Uri(rendezvousAddress);
            this.TrackingContext = this.GetTrackingContext();
            RelayEventSource.Log.HybridHttpRequestStarting(this.TrackingContext);
        }

        public TrackingContext TrackingContext { get; }

        TimeSpan OperationTimeout { get { return this.listener.OperationTimeout; } }

        public static async Task CreateAsync(HybridConnectionListener listener, ListenerCommand.RequestCommand requestCommand, WebSocket controlWebSocket)
        {
            var hybridHttpConnection = new HybridHttpConnection(listener, controlWebSocket, requestCommand.Address);

            // In this method we're holding up the listener's control connection.
            // Do only what we need to do (receive any request body from control channel) and then let this Task complete.
            bool requestOverControlConnection = requestCommand.Body.HasValue;
            var requestAndStream = new RequestCommandAndStream(requestCommand, null);
            if (requestOverControlConnection)
            {
                requestAndStream = await hybridHttpConnection.ReceiveRequestBodyOverControlAsync(requestCommand).ConfigureAwait(false);
            }

            // ProcessFirstRequestAsync runs without blocking the listener control connection:
            Task.Run(() => hybridHttpConnection.ProcessFirstRequestAsync(requestAndStream)).Fork(hybridHttpConnection);
        }

        public override string ToString()
        {
            return nameof(HybridHttpConnection);
        }

        TrackingContext GetTrackingContext()
        {
            var queryParameters = HybridConnectionUtility.ParseQueryString(this.rendezvousAddress.Query);
            string trackingId = queryParameters[HybridConnectionConstants.Id];

            string path = this.rendezvousAddress.LocalPath;
            if (path.StartsWith(HybridConnectionConstants.HybridConnectionRequestUri, StringComparison.OrdinalIgnoreCase))
            {
                path = path.Substring(HybridConnectionConstants.HybridConnectionRequestUri.Length);
            }

            Uri logicalAddress = new UriBuilder()
            {
                Scheme = Uri.UriSchemeHttps,
                Host = this.listener.Address.Host,
                Path = path,
            }.Uri;

            return TrackingContext.Create(trackingId, logicalAddress);
        }

        async Task ProcessFirstRequestAsync(RequestCommandAndStream requestAndStream)
        {
            try
            {
                var requestCommand = requestAndStream.RequestCommand;
                if (!requestCommand.Body.HasValue)
                {
                    // Need to rendezvous to get the real RequestCommand
                    requestAndStream = await this.ReceiveRequestOverRendezvousAsync().ConfigureAwait(false);
                }

                this.InvokeRequestHandler(requestAndStream);
            }
            catch (Exception e) when (!Fx.IsFatal(e))
            {
                RelayEventSource.Log.HandledExceptionAsWarning(this.listener, e);
                await this.CloseAsync().ConfigureAwait(false);
            }
        }

        async Task<RequestCommandAndStream> ReceiveRequestBodyOverControlAsync(ListenerCommand.RequestCommand requestCommand)
        {
            Stream requestStream = null;
            if (requestCommand.Body.Value)
            {
                // We need to buffer this body stream so we can let go of the listener control connection
                requestStream = new MemoryStream(BufferSize);
                using (var cts = new CancellationTokenSource(this.OperationTimeout))
                {
                    var buffer = new ArraySegment<byte>(new byte[BufferSize]);
                    var readResult = await WebSocketUtility.ReadMessageAsync(this.controlWebSocket, buffer, requestStream, cts.Token).ConfigureAwait(false);
                    if (readResult.MessageType == WebSocketMessageType.Close)
                    {
                        throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.EntityClosedOrAborted), this);
                    }
                    else if (readResult.MessageType != WebSocketMessageType.Binary)
                    {
                        throw RelayEventSource.Log.ThrowingException(
                            new ProtocolViolationException(SR.GetString(SR.InvalidType, WebSocketMessageType.Binary, readResult.MessageType)), this);
                    }

                    requestStream.Position = 0;
                }
            }

            return new RequestCommandAndStream(requestCommand, requestStream);
        }

        async Task<RequestCommandAndStream> ReceiveRequestOverRendezvousAsync()
        {
            using (var cancelSource = new CancellationTokenSource(this.OperationTimeout))
            {
                // A Rendezvous is required to get full request
                await this.EnsureRendezvousAsync(cancelSource.Token).ConfigureAwait(false);
            }

            RelayEventSource.Log.HybridHttpReadRendezvousValue(this, "request command");
            ListenerCommand.RequestCommand requestCommand;
            using (var rendezvousCommandStream = new WebSocketMessageStream(this.rendezvousWebSocket, this.OperationTimeout))
            {
                // Deserializing the object from stream makes a sync-over-async call which can deadlock
                // if performed on the websocket transport's callback thread.
                requestCommand = await Task.Run(() => ListenerCommand.ReadObject(rendezvousCommandStream).Request).ConfigureAwait(false);
                if (rendezvousCommandStream.MessageType == WebSocketMessageType.Close)
                {
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.EntityClosedOrAborted), this);
                }
                else if (rendezvousCommandStream.MessageType != WebSocketMessageType.Text)
                {
                    throw RelayEventSource.Log.ThrowingException(
                        new ProtocolViolationException(SR.GetString(SR.InvalidType, WebSocketMessageType.Text, rendezvousCommandStream.MessageType)), this);
                }
                else if (requestCommand == null)
                {
                    throw RelayEventSource.Log.ThrowingException(new ProtocolViolationException(SR.GetString(SR.InvalidType, "request", "{unknown}")), this);
                }
            }

            Stream requestStream = null;
            if (requestCommand.Body.HasValue && requestCommand.Body.Value)
            {
                RelayEventSource.Log.HybridHttpReadRendezvousValue(this, "request body");
                requestStream = new WebSocketMessageStream(this.rendezvousWebSocket, this.OperationTimeout);
            }

            return new RequestCommandAndStream(requestCommand, requestStream);
        }

        void InvokeRequestHandler(RequestCommandAndStream requestAndStream)
        {
            ListenerCommand.RequestCommand requestCommand = requestAndStream.RequestCommand;
            Uri requestUri = new Uri(this.listener.Address, requestCommand.RequestTarget);
            var listenerContext = new RelayedHttpListenerContext(
                this.listener,
                requestUri,
                requestCommand.Id,
                requestCommand.Method,
                requestCommand.RequestHeaders);
            listenerContext.Request.SetRemoteAddress(requestCommand.RemoteEndpoint);
            listenerContext.Response.StatusCode = HttpStatusCode.OK;
            listenerContext.Response.OutputStream = new ResponseStream(this, listenerContext);

            RelayEventSource.Log.HybridHttpRequestReceived(listenerContext.TrackingContext, requestCommand.Method);

            Stream requestStream = requestAndStream.Stream;
            if (requestStream != null)
            {
                listenerContext.Request.HasEntityBody = true;
                listenerContext.Request.InputStream = requestStream;
            }

            var requestHandler = this.listener.RequestHandler;
            if (requestHandler != null)
            {
                try
                {
                    RelayEventSource.Log.HybridHttpInvokingUserRequestHandler();
                    requestHandler(listenerContext);
                }
                catch (Exception userException) when (!Fx.IsFatal(userException))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this, userException);
                    listenerContext.Response.StatusCode = HttpStatusCode.InternalServerError;
                    listenerContext.Response.StatusDescription = this.TrackingContext.EnsureTrackableMessage(SR.RequestHandlerException);
                    listenerContext.Response.CloseAsync().Fork(this);
                    return;
                }
            }
            else
            {
                RelayEventSource.Log.HybridHttpConnectionMissingRequestHandler();
                listenerContext.Response.StatusCode = HttpStatusCode.NotImplemented;
                listenerContext.Response.StatusDescription = this.TrackingContext.EnsureTrackableMessage(SR.RequestHandlerMissing);
                listenerContext.Response.CloseAsync().Fork(this);
            }
        }

        async Task SendResponseAsync(ListenerCommand.ResponseCommand responseCommand, Stream responseBodyStream, CancellationToken cancelToken)
        {
            if (this.rendezvousWebSocket == null)
            {
                RelayEventSource.Log.HybridHttpConnectionSendResponse(this.TrackingContext, "control", responseCommand.StatusCode);
                var listenerCommand = new ListenerCommand { Response = responseCommand };
                await this.listener.SendControlCommandAndStreamAsync(listenerCommand, responseBodyStream, cancelToken).ConfigureAwait(false);
            }
            else
            {
                RelayEventSource.Log.HybridHttpConnectionSendResponse(this.TrackingContext, "rendezvous", responseCommand.StatusCode);
                await this.EnsureRendezvousAsync(cancelToken).ConfigureAwait(false);

                using (var memoryStream = new MemoryStream(this.listener.ConnectionBufferSize))
                {
                    new ListenerCommand { Response = responseCommand }.WriteObject(memoryStream);
                    memoryStream.Position = 0;
                    ArraySegment<byte> buffer = memoryStream.GetArraySegment();

                    // We need to respond over the rendezvous connection
                    await this.rendezvousWebSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancelToken).ConfigureAwait(false);

                    if (responseCommand.Body && responseBodyStream != null)
                    {
                        buffer = new ArraySegment<byte>(buffer.Array, 0, buffer.Array.Length);
                        await this.rendezvousWebSocket.SendStreamAsync(responseBodyStream, WebSocketMessageType.Binary, buffer, cancelToken).ConfigureAwait(false);
                    }
                }
            }
        }

        Task SendBytesOverRendezvousAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancelToken)
        {
            RelayEventSource.Log.HybridHttpConnectionSendBytes(this.TrackingContext, buffer.Count);
            return this.rendezvousWebSocket.SendAsync(buffer, messageType, endOfMessage, cancelToken);
        }

        async Task EnsureRendezvousAsync(CancellationToken cancelToken)
        {
            if (this.rendezvousWebSocket == null)
            {                
                RelayEventSource.Log.HybridHttpCreatingRendezvousConnection(this.TrackingContext);
                var clientWebSocket = ClientWebSocketFactory.Create(this.listener.UseBuiltInClientWebSocket);
                DefaultWebProxy.ConfigureProxy(clientWebSocket.Options, this.listener.Proxy);
                this.rendezvousWebSocket = clientWebSocket.WebSocket;
                await clientWebSocket.ConnectAsync(this.rendezvousAddress, cancelToken).ConfigureAwait(false);
            }
        }

        async Task CloseAsync()
        {
            if (this.rendezvousWebSocket != null)
            {
                using (var cts = new CancellationTokenSource(this.OperationTimeout))
                {
                    RelayEventSource.Log.ObjectClosing(this);
                    await this.rendezvousWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "NormalClosure", cts.Token).ConfigureAwait(false);
                    RelayEventSource.Log.ObjectClosed(this);
                }
            }
        }

        static ListenerCommand.ResponseCommand CreateResponseCommand(RelayedHttpListenerContext listenerContext)
        {
            var responseCommand = new ListenerCommand.ResponseCommand();
            responseCommand.StatusCode = (int)listenerContext.Response.StatusCode;
            responseCommand.StatusDescription = listenerContext.Response.StatusDescription;
            responseCommand.RequestId = listenerContext.TrackingContext.TrackingId;
            foreach (string headerName in listenerContext.Response.Headers.AllKeys)
            {
                responseCommand.ResponseHeaders[headerName] = listenerContext.Response.Headers[headerName];
            }

            return responseCommand;
        }

        sealed class ResponseStream : Stream, ICloseAsync, ITraceSource
        {
            static readonly TimeSpan WriteBufferFlushTimeout = TimeSpan.FromSeconds(2);
            readonly HybridHttpConnection connection;
            readonly RelayedHttpListenerContext context;
            readonly AsyncLock asyncLock;
            bool closed;
            MemoryStream writeBufferStream;
            Timer writeBufferFlushTimer;
            bool responseCommandSent;

            public ResponseStream(HybridHttpConnection connection, RelayedHttpListenerContext context)
            {
                this.connection = connection;
                this.context = context;
                this.WriteTimeout = TimeoutHelper.ToMilliseconds(this.connection.OperationTimeout);
                this.asyncLock = new AsyncLock();
            }

            enum FlushReason { BufferFull, RendezvousExists, Timer }

            public override bool CanRead { get { return false; } }

            public override bool CanSeek { get { return false; } }

            public override bool CanWrite { get { return true; } }

            public override bool CanTimeout { get { return true; } }

            public override long Length
            {
                get
                {
                    throw new NotSupportedException();
                }
            }

            public override long Position
            {
                get
                {
                    throw new NotSupportedException();
                }

                set
                {
                    throw new NotSupportedException();
                }
            }

            public override int WriteTimeout { get; set; }

            public TrackingContext TrackingContext
            {
                get { return this.context.TrackingContext; }
            }

            public override void Flush()
            {
                // Nothing to do here. Either:
                // 1. We're still buffering data to see if it will all fit into a single response on the control connection
                // - Or -
                // 2. We've got a rendezvous and each Write[Async] call flushes to the websocket immediately
            }

            public override Task FlushAsync(CancellationToken cancelToken)
            {
                // Nothing to do here. Either:
                // 1. We're still buffering data to see if it will all fit into a single response on the control connection
                // - Or -
                // 2. We've got a rendezvous and each Write[Async] call flushes to the websocket immediately
                return TaskEx.CompletedTask;
            }

            // The caller of this method must have acquired this.asyncLock
            async Task FlushCoreAsync(FlushReason reason, CancellationToken cancelToken)
            {
                RelayEventSource.Log.HybridHttpResponseStreamFlush(this.TrackingContext, reason.ToString());
                if (!this.responseCommandSent)
                {
                    var responseCommand = CreateResponseCommand(this.context);
                    responseCommand.Body = true;

                    // At this point we have no choice but to rendezvous
                    await this.connection.EnsureRendezvousAsync(cancelToken).ConfigureAwait(false);

                    // Send the response command over the rendezvous connection
                    await this.connection.SendResponseAsync(responseCommand, null, cancelToken).ConfigureAwait(false);
                    this.responseCommandSent = true;

                    if (this.writeBufferStream != null && this.writeBufferStream.Length > 0)
                    {
                        var writeBuffer = this.writeBufferStream.GetArraySegment();
                        await this.connection.SendBytesOverRendezvousAsync(writeBuffer, WebSocketMessageType.Binary, false, cancelToken).ConfigureAwait(false);

                        this.writeBufferStream.Dispose();
                        this.writeBufferStream = null;
                        this.CancelWriteBufferFlushTimer();
                    }
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new InvalidOperationException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                this.WriteAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            public override async Task WriteAsync(byte[] array, int offset, int count, CancellationToken cancelToken)
            {
                RelayEventSource.Log.HybridHttpResponseStreamWrite(this.TrackingContext, count);
                this.context.Response.SetReadOnly();
                using (var timeoutCancelSource = new CancellationTokenSource(this.WriteTimeout))
                using (var linkedCancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCancelSource.Token))
                using (await this.asyncLock.LockAsync(linkedCancelSource.Token).ConfigureAwait(false))
                {
                    if (!this.responseCommandSent)
                    {
                        FlushReason flushReason;
                        if (this.connection.rendezvousWebSocket != null)
                        {
                            flushReason = FlushReason.RendezvousExists;
                        }
                        else
                        {
                            int bufferedCount = this.writeBufferStream != null ? (int)this.writeBufferStream.Length : 0;
                            if (count + bufferedCount <= MaxControlConnectionBodySize)
                            {
                                // There's still a chance we might be able to respond over the control connection, accumulate bytes
                                if (this.writeBufferStream == null)
                                {
                                    int initialStreamSize = Math.Min(count * 2, MaxControlConnectionBodySize);
                                    this.writeBufferStream = new MemoryStream(initialStreamSize);
                                    this.writeBufferFlushTimer = new Timer((s) => ((ResponseStream)s).OnWriteBufferFlushTimer(), this, WriteBufferFlushTimeout, Timeout.InfiniteTimeSpan);
                                }

                                this.writeBufferStream.Write(array, offset, count);
                                return;
                            }

                            flushReason = FlushReason.BufferFull;
                        }

                        // FlushCoreAsync will rendezvous, send the responseCommand, and any writeBufferStream bytes
                        await this.FlushCoreAsync(flushReason, linkedCancelSource.Token).ConfigureAwait(false);
                    }

                    var buffer = new ArraySegment<byte>(array, offset, count);
                    await this.connection.SendBytesOverRendezvousAsync(buffer, WebSocketMessageType.Binary, false, linkedCancelSource.Token).ConfigureAwait(false);
                }
            }

#if NET45
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                return this.WriteAsync(buffer, offset, count).ToAsyncResult(callback, state);
            }

            public override void EndWrite(IAsyncResult asyncResult)
            {
                TaskEx.EndAsyncResult(asyncResult);
            }
#endif // NET45

            public override string ToString()
            {
                return this.connection.ToString() + "+" + nameof(ResponseStream);
            }

            protected override void Dispose(bool disposing)
            {
                try
                {
                    if (disposing && !this.closed)
                    {
                        this.CloseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                finally
                {
                    base.Dispose(disposing);
                }
            }

            public async Task CloseAsync()
            {
                if (this.closed)
                {
                    return;
                }

                try
                {
                    RelayEventSource.Log.ObjectClosing(this);
                    using (var cancelSource = new CancellationTokenSource(this.WriteTimeout))
                    using (await this.asyncLock.LockAsync(cancelSource.Token).ConfigureAwait(false))
                    {
                        if (!this.responseCommandSent)
                        {
                            var responseCommand = CreateResponseCommand(this.context);
                            if (this.writeBufferStream != null)
                            {
                                responseCommand.Body = true;
                                this.writeBufferStream.Position = 0;
                            }

                            // Don't force any rendezvous now
                            await this.connection.SendResponseAsync(responseCommand, this.writeBufferStream, cancelSource.Token).ConfigureAwait(false);
                            this.responseCommandSent = true;
                            this.CancelWriteBufferFlushTimer();
                        }
                        else
                        {
                            var buffer = new ArraySegment<byte>(new byte[0], 0, 0);
                            await this.connection.SendBytesOverRendezvousAsync(buffer, WebSocketMessageType.Binary, true, cancelSource.Token).ConfigureAwait(false);
                        }
                    }

                    RelayEventSource.Log.ObjectClosed(this);
                }
                catch (Exception e) when (!Fx.IsFatal(e))
                {
                    RelayEventSource.Log.ThrowingException(e, this);
                    throw;
                }
                finally
                {
                    try
                    {
                        await this.connection.CloseAsync().ConfigureAwait(false);
                    }
                    catch (Exception closeException) when (!Fx.IsFatal(closeException))
                    {
                        RelayEventSource.Log.HandledExceptionAsWarning(this, closeException);
                    }

                    this.closed = true;
                }
            }

            async void OnWriteBufferFlushTimer()
            {
                try
                {
                    using (var cancelSource = new CancellationTokenSource(this.WriteTimeout))
                    using (await this.asyncLock.LockAsync(cancelSource.Token).ConfigureAwait(false))
                    {
                        await this.FlushCoreAsync(FlushReason.Timer, cancelSource.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (!Fx.IsFatal(e))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this, e);
                }
            }

            void CancelWriteBufferFlushTimer()
            {
                if (this.writeBufferFlushTimer != null)
                {
                    this.writeBufferFlushTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    this.writeBufferFlushTimer.Dispose();
                    this.writeBufferFlushTimer = null;
                }
            }
        }

        struct RequestCommandAndStream
        {
            public RequestCommandAndStream(ListenerCommand.RequestCommand requestCommand, Stream stream)
            {
                this.RequestCommand = requestCommand;
                this.Stream = stream;
            }

            public ListenerCommand.RequestCommand RequestCommand;
            public Stream Stream;
        }
    }
}
