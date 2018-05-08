// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a listener for accepting HybridConnections from remote clients.
    /// </summary>
    public class HybridConnectionListener : IConnectionStatus, ITraceSource
    {
        const int DefaultConnectionBufferSize = 64 * 1024;
        readonly InputQueue<HybridConnectionStream> connectionInputQueue;
        readonly ControlConnection controlConnection;
        IWebProxy proxy;
        string cachedToString;
        TrackingContext trackingContext;
        bool openCalled;
        volatile bool closeCalled;

        /// <summary>
        /// Create a new HybridConnectionListener instance for accepting HybridConnections.
        /// </summary>
        /// <param name="address">The address on which to listen for HybridConnections.  This address should 
        /// be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".</param>
        /// <param name="tokenProvider">The TokenProvider for connecting this listener to ServiceBus.</param>
        public HybridConnectionListener(Uri address, TokenProvider tokenProvider)
        {
            if (address == null || tokenProvider == null)
            {
                throw RelayEventSource.Log.ThrowingException(new ArgumentNullException(address == null ? nameof(address) : nameof(tokenProvider)), this);
            }
            else if (address.Scheme != RelayConstants.HybridConnectionScheme)
            {
                throw RelayEventSource.Log.ThrowingException(
                    new ArgumentException(SR.InvalidUriScheme.FormatInvariant(address.Scheme, RelayConstants.HybridConnectionScheme), nameof(address)), this);
            }

            this.Address = address;
            this.TokenProvider = tokenProvider;
            this.ConnectionBufferSize = DefaultConnectionBufferSize;
            this.OperationTimeout = RelayConstants.DefaultOperationTimeout;
            this.proxy = WebRequest.DefaultWebProxy;
            this.TrackingContext = TrackingContext.Create(this.Address);
            this.connectionInputQueue = new InputQueue<HybridConnectionStream>();
            this.controlConnection = new ControlConnection(this);
            this.UseBuiltInClientWebSocket = HybridConnectionConstants.DefaultUseBuiltInClientWebSocket;
        }

        /// <summary>Creates a new instance of <see cref="HybridConnectionListener" /> using the specified connection string.</summary>
        /// <param name="connectionString">The connection string to use.  This connection string must include the EntityPath property.</param>
        /// <returns>The newly created <see cref="HybridConnectionListener" /> instance.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the format of the <paramref name="connectionString" /> parameter is incorrect.</exception>
        public HybridConnectionListener(string connectionString)
            : this(connectionString, null, pathFromConnectionString: true)
        {
        }

        /// <summary>Creates a new instance of <see cref="HybridConnectionListener" /> from a connection string and
        /// the specified HybridConection path. Use this overload only when the connection string does not use the 
        /// <see cref="RelayConnectionStringBuilder.EntityPath" /> property.</summary> 
        /// <param name="connectionString">The connection string used. This connection string must not include the EntityPath property.</param>
        /// <param name="path">The path to the HybridConnection.</param>
        /// <returns>The created <see cref="HybridConnectionListener" />.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the format of the <paramref name="connectionString" /> parameter is incorrect.</exception>
        public HybridConnectionListener(string connectionString, string path)
            : this(connectionString, path, pathFromConnectionString: false)
        {
        }

        // This private .ctor handles both of the public overloads which take connectionString
        HybridConnectionListener(string connectionString, string path, bool pathFromConnectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(connectionString), this);
            }

            var builder = new RelayConnectionStringBuilder(connectionString);
            builder.Validate();

            if (pathFromConnectionString)
            {
                if (string.IsNullOrWhiteSpace(builder.EntityPath))
                {
                    // connectionString did not have required EntityPath
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringMustIncludeEntityPath, nameof(HybridConnectionClient)), this);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    // path parameter is required
                    throw RelayEventSource.Log.ArgumentNull(nameof(path), this);
                }
                else if (!string.IsNullOrWhiteSpace(builder.EntityPath))
                {
                    // EntityPath must not appear in connectionString
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringMustNotIncludeEntityPath, nameof(HybridConnectionListener)), this);
                }

                builder.EntityPath = path;
            }

            this.Address = new Uri(builder.Endpoint, builder.EntityPath);
            this.TokenProvider = builder.CreateTokenProvider();
            this.ConnectionBufferSize = DefaultConnectionBufferSize;
            this.OperationTimeout = builder.OperationTimeout;
            this.proxy = WebRequest.DefaultWebProxy;
            this.TrackingContext = TrackingContext.Create(this.Address);
            this.connectionInputQueue = new InputQueue<HybridConnectionStream>();
            this.controlConnection = new ControlConnection(this);
            this.UseBuiltInClientWebSocket = HybridConnectionConstants.DefaultUseBuiltInClientWebSocket;
        }

        /// <summary>
        /// Raised when the Listener is attempting to reconnect with ServiceBus after a connection loss.
        /// </summary>
        public event EventHandler Connecting;

        /// <summary>
        /// Raised when the Listener has successfully connected with ServiceBus
        /// </summary>
        public event EventHandler Online;
        
        /// <summary>
        /// Raised when the Listener will no longer be attempting to (re)connect with ServiceBus.
        /// </summary>
        public event EventHandler Offline;

        /// <summary>Gets a value that determines whether the connection is online.</summary>
        /// <value>true if the connection is alive and online; false if there 
        /// is no connectivity towards the Azure Service Bus from the current network location.</value> 
        public bool IsOnline { get; private set; }

        /// <summary>Retrieves the last error encountered when trying to reestablish the connection from the offline state.</summary>
        /// <value>Returns a <see cref="System.Exception" /> that contains the last error.</value>
        public Exception LastError { get; private set; }

        /// <summary>
        /// Allows installing a custom handler which can inspect request headers, control response headers,
        /// decide whether to accept or reject a web-socket upgrade request, and control the status code/description if rejecting.
        /// The AcceptHandler should return true to accept a client request or false to reject.
        /// </summary>
        public Func<RelayedHttpListenerContext, Task<bool>> AcceptHandler { get; set; }

        /// <summary>
        /// Installs a handler for Hybrid Http Requests.
        /// </summary>
        public Action<RelayedHttpListenerContext> RequestHandler { get; set; }

        /// <summary>
        /// Gets the address on which to listen for HybridConnections.  This address should be of the format
        /// "sb://contoso.servicebus.windows.net/yourhybridconnection".
        /// </summary>
        public Uri Address { get; }

        /// <summary>
        /// Gets or sets proxy information for connecting to ServiceBus.
        /// </summary>
        public IWebProxy Proxy
        {
            get
            {
                return this.proxy;
            }
            set
            {
                this.ThrowIfReadOnly();
                this.proxy = value;
            }
        }

        /// <summary>
        /// Gets the TokenProvider for authenticating this HybridConnection listener.
        /// </summary>
        public TokenProvider TokenProvider { get; }

        /// <summary>
        /// Gets the TrackingContext for this listener.
        /// </summary>
        public TrackingContext TrackingContext
        {
            get
            {
                return this.trackingContext;
            }
            private set
            {
                // We allow updating the TrackingContext in order to use the trackingId flown from the
                // service which has the SBSFE Role instance number suffix on it (i.e. "_GX").
                this.trackingContext = value;
            }
        }

        /// <summary>
        /// Controls whether the ClientWebSocket from .NET Core or a custom implementation is used.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool UseBuiltInClientWebSocket { get; set; }

        /// <summary>
        /// Gets or sets the connection buffer size.  Default value is 64K.
        /// </summary>
        internal int ConnectionBufferSize { get; }

        internal TimeSpan OperationTimeout { get; }

        object ThisLock { get { return this.connectionInputQueue; } }

        /// <summary>
        /// Opens the <see cref="HybridConnectionListener"/> and registers it as a listener in ServiceBus.
        /// Unless specified in the connection string the default is 1 minute.
        /// </summary>
        public Task OpenAsync()
        {
            return this.OpenAsync(this.OperationTimeout);
        }

        /// <summary>
        /// Opens the <see cref="HybridConnectionListener"/> and registers it as a listener in ServiceBus.
        /// </summary>
        /// <param name="timeout">A timeout to observe.</param>
        public async Task OpenAsync(TimeSpan timeout)
        {
            TimeoutHelper.ThrowIfNegativeArgument(timeout);
            using (var cts = new CancellationTokenSource(timeout))
            {
                await this.OpenAsync(cts.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Opens the <see cref="HybridConnectionListener"/> and registers it as a listener in ServiceBus.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public async Task OpenAsync(CancellationToken cancellationToken)
        {
            lock (this.ThisLock)
            {
                if (this.openCalled)
                {
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.GetString(SR.InstanceAlreadyRunning, this.GetType().Name)), this);
                }
                else if (this.closeCalled)
                {
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.EntityClosedOrAborted), this);
                }

                this.openCalled = true;
            }

            // Hookup IConnectionStatus events
            this.controlConnection.Connecting += (s, e) => this.OnConnectionStatus(this.Connecting, s, e);
            this.controlConnection.Online += (s, e) => this.OnConnectionStatus(this.Online, s, e);
            this.controlConnection.Offline += (s, e) => this.OnConnectionStatus(this.Offline, s, e);

            await this.controlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Closes the <see cref="HybridConnectionListener"/> using the default timeout.
        /// Unless specified in the connection string the default is 1 minute.
        /// </summary>
        public Task CloseAsync()
        {
            return this.CloseAsync(this.OperationTimeout);
        }

        /// <summary>
        /// Closes the <see cref="HybridConnectionListener"/> using the provided timeout.
        /// </summary>
        /// <param name="timeout">A timeout to observe.</param>
        public async Task CloseAsync(TimeSpan timeout)
        {
            TimeoutHelper.ThrowIfNegativeArgument(timeout);
            using (var cts = new CancellationTokenSource(timeout))
            {
                await this.CloseAsync(cts.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Closes the <see cref="HybridConnectionListener"/> using the provided CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            List<HybridConnectionStream> clients;
            lock (this.ThisLock)
            {
                if (this.closeCalled)
                {
                    return;
                }

                this.closeCalled = true;

                // If the input queue is empty this completes all pending waiters with null and prevents
                // any new items being added to the input queue.
                this.connectionInputQueue.Shutdown();

                // Close any unaccepted rendezvous.  DequeueAsync won't block since we've called connectionInputQueue.Shutdown().
                clients = new List<HybridConnectionStream>(this.connectionInputQueue.PendingCount);
                HybridConnectionStream stream;
                while ((stream = this.connectionInputQueue.DequeueAsync(cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult()) != null)
                {
                    clients.Add(stream);
                }
            }

            try
            {
                RelayEventSource.Log.ObjectClosing(this);
                await this.controlConnection.CloseAsync(cancellationToken).ConfigureAwait(false);

                clients.ForEach(client => ((WebSocketStream)client).Abort());
                RelayEventSource.Log.ObjectClosed(this);
            }
            catch (Exception e) when (!Fx.IsFatal(e))
            {
                RelayEventSource.Log.ThrowingException(e, this);
                throw;
            }
            finally
            {
                this.connectionInputQueue.Dispose();
            }
        }

        /// <summary>
        /// Accepts a new HybridConnection which was initiated by a remote client and returns the Stream.
        /// </summary>
        public Task<HybridConnectionStream> AcceptConnectionAsync()
        {
            lock (this.ThisLock)
            {
                if (!this.openCalled)
                {
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.ObjectNotOpened), this);
                }
            }

            return this.connectionInputQueue.DequeueAsync(CancellationToken.None);
        }

        /// <summary>
        /// Returns a string that represents the current object.  Includes a TrackingId for end to end correlation.
        /// </summary>
        public override string ToString()
        {
            return this.cachedToString ?? (this.cachedToString = nameof(HybridConnectionListener) + "(" + this.TrackingContext + ")");
        }

        /// <summary>
        /// Gets the <see cref="HybridConnectionRuntimeInformation"/> for this HybridConnection entity using the default timeout.
        /// Unless specified in the connection string the default is 1 minute.
        /// </summary>
        public async Task<HybridConnectionRuntimeInformation> GetRuntimeInformationAsync()
        {
            using (var cancelSource = new CancellationTokenSource(this.OperationTimeout))
            {
                return await this.GetRuntimeInformationAsync(cancelSource.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Gets the <see cref="HybridConnectionRuntimeInformation"/> for this HybridConnection entity using the provided CancellationToken.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to observe.</param>
        public Task<HybridConnectionRuntimeInformation> GetRuntimeInformationAsync(CancellationToken cancellationToken)
        {
            return ManagementOperations.GetAsync<HybridConnectionRuntimeInformation>(this.Address, this.TokenProvider, cancellationToken);
        }

        internal Task SendControlCommandAndStreamAsync(ListenerCommand command, Stream stream, CancellationToken cancelToken)
        {
            return this.controlConnection.SendCommandAndStreamAsync(command, stream, cancelToken);
        }

        void ThrowIfReadOnly()
        {
            lock (this.ThisLock)
            {
                if (this.openCalled)
                {
                    throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.ObjectIsReadOnly), this);
                }
            }
        }       

        async Task OnCommandAsync(ArraySegment<byte> commandBuffer, WebSocket webSocket)
        {
            try
            {
                var listenerCommand = ListenerCommand.ReadObject(commandBuffer);
                if (listenerCommand.Accept != null)
                {
                    await this.OnAcceptCommandAsync(listenerCommand.Accept).ConfigureAwait(false);
                }
                else if (listenerCommand.Request != null)
                {
                    await HybridHttpConnection.CreateAsync(this, listenerCommand.Request, webSocket).ConfigureAwait(false);
                }
                else
                {
                    string json = Encoding.UTF8.GetString(
                        commandBuffer.Array,
                        commandBuffer.Offset,
                        Math.Min(commandBuffer.Count, HybridConnectionConstants.MaxUnrecognizedJson));
                    RelayEventSource.Log.Warning(this, $"Received an unknown command: {json}.");
                }
            }
            catch (Exception exception) when (!Fx.IsFatal(exception))
            {
                RelayEventSource.Log.HandledExceptionAsWarning(this, exception);
            }
        }

        async Task OnAcceptCommandAsync(ListenerCommand.AcceptCommand acceptCommand)
        {
            Uri rendezvousUri = new Uri(acceptCommand.Address);
            Uri requestUri = this.GenerateAcceptRequestUri(rendezvousUri);

            var listenerContext = new RelayedHttpListenerContext(
                this, requestUri, acceptCommand.Id, "GET", acceptCommand.ConnectHeaders);
            listenerContext.Request.SetRemoteAddress(acceptCommand.RemoteEndpoint);

            RelayEventSource.Log.RelayListenerRendezvousStart(listenerContext.Listener, listenerContext.TrackingContext.TrackingId, acceptCommand.Address);
            try
            {
                var acceptHandler = this.AcceptHandler;
                bool shouldAccept = acceptHandler == null;
                if (acceptHandler != null)
                {
                    // Invoke and await the user's AcceptHandler method
                    try
                    {
                        shouldAccept = await acceptHandler(listenerContext).ConfigureAwait(false);
                    }
                    catch (Exception userException) when (!Fx.IsFatal(userException))
                    {
                        string description = SR.GetString(SR.AcceptHandlerException, listenerContext.TrackingContext.TrackingId);
                        RelayEventSource.Log.RelayListenerRendezvousFailed(this, listenerContext.TrackingContext.TrackingId, description + " " + userException);
                        listenerContext.Response.StatusCode = HttpStatusCode.BadGateway;
                        listenerContext.Response.StatusDescription = description;
                    }
                }

                // Don't block the pump waiting for the rendezvous
                this.CompleteAcceptAsync(listenerContext, rendezvousUri, shouldAccept).Fork(this);
            }
            catch (Exception exception) when (!Fx.IsFatal(exception))
            {
                RelayEventSource.Log.RelayListenerRendezvousFailed(this, listenerContext.TrackingContext.TrackingId, exception);
                RelayEventSource.Log.RelayListenerRendezvousStop();
            }
        }

        /// <summary>
        /// Form the logical request Uri using the scheme://host:port from the listener and the path from the acceptCommand (minus "/$hc")
        /// e.g. sb://contoso.servicebus.windows.net/hybrid1?foo=bar
        /// </summary>
        Uri GenerateAcceptRequestUri(Uri rendezvousUri)
        {
            var requestUri = new UriBuilder(this.Address);
            requestUri.Query = HybridConnectionUtility.FilterQueryString(rendezvousUri.Query);
            requestUri.Path = rendezvousUri.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            if (requestUri.Path.StartsWith("$hc/", StringComparison.Ordinal))
            {
                requestUri.Path = requestUri.Path.Substring(4);
            }

            return requestUri.Uri;
        }

        async Task CompleteAcceptAsync(RelayedHttpListenerContext listenerContext, Uri rendezvousUri, bool shouldAccept)
        {
            try
            {
                if (shouldAccept)
                {
                    var webSocketStream = await listenerContext.AcceptAsync(rendezvousUri).ConfigureAwait(false);
                    lock (this.ThisLock)
                    {
                        if (this.closeCalled)
                        {
                            RelayEventSource.Log.RelayListenerRendezvousFailed(this, listenerContext.TrackingContext.TrackingId, SR.ObjectClosedOrAborted);
                            return;
                        }

                        this.connectionInputQueue.EnqueueAndDispatch(webSocketStream, null, canDispatchOnThisThread: false);
                    }
                }
                else
                {
                    RelayEventSource.Log.RelayListenerRendezvousRejected(
                        listenerContext.TrackingContext, listenerContext.Response.StatusCode, listenerContext.Response.StatusDescription);
                    await listenerContext.RejectAsync(rendezvousUri).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (!Fx.IsFatal(exception))
            {
                RelayEventSource.Log.RelayListenerRendezvousFailed(this, listenerContext.TrackingContext.TrackingId, exception);
            }
            finally
            {
                RelayEventSource.Log.RelayListenerRendezvousStop();
            }
        }

        void OnConnectionStatus(EventHandler handler, object sender, EventArgs args)
        {
            // Propagate inner properties in case they've mutated.
            var innerStatus = (IConnectionStatus)sender;
            this.IsOnline = innerStatus.IsOnline;
            this.LastError = innerStatus.LastError;

            handler?.Invoke(this, args);
        }

        // Connects, maintains, and transparently reconnects this listener's control connection with the cloud service.
        sealed class ControlConnection : IConnectionStatus
        {
            // Retries after 0, 1, 2, 5, 10, 30 seconds
            static readonly TimeSpan[] ConnectDelayIntervals = {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            };

            readonly HybridConnectionListener listener;
            readonly Uri address;
            readonly int bufferSize;
            readonly string path;
            readonly TokenRenewer tokenRenewer;
            readonly CancellationTokenSource shutdownCancellationSource;
            readonly AsyncLock sendAsyncLock;
            readonly ArraySegment<byte> receiveBuffer;
            readonly ArraySegment<byte> sendBuffer;
            Task<WebSocket> connectAsyncTask;
            Task receivePumpTask;
            CancellationToken closeCancellationToken;
            int connectDelayIndex;
            volatile bool closeCalled;

            public ControlConnection(HybridConnectionListener listener)
            {
                this.listener = listener;
                this.address = listener.Address;
                this.bufferSize = listener.ConnectionBufferSize;
                this.path = address.AbsolutePath.TrimStart('/');
                this.shutdownCancellationSource = new CancellationTokenSource();
                this.receiveBuffer = new ArraySegment<byte>(new byte[this.bufferSize]);
                this.sendBuffer = new ArraySegment<byte>(new byte[this.bufferSize]);
                this.sendAsyncLock = new AsyncLock();
                this.tokenRenewer = new TokenRenewer(
                    this.listener, this.address.AbsoluteUri, TokenProvider.DefaultTokenTimeout);
            }

            public event EventHandler Connecting;
            public event EventHandler Offline;
            public event EventHandler Online;

            public bool IsOnline { get; private set; }

            public Exception LastError { get; private set; }

            object ThisLock { get { return this.tokenRenewer; } }

            public async Task OpenAsync(CancellationToken cancellationToken)
            {
                // Esstablish a WebSocket connection right now so we can detect any fatal errors
                var connectTask = this.EnsureConnectTask(cancellationToken);
                bool succeeded = false;
                try
                {
                    // Block so we surface any errors to the user right away.
                    await connectTask.ConfigureAwait(false);

                    this.tokenRenewer.TokenRenewed += this.OnTokenRenewed;
                    this.receivePumpTask = this.ReceivePumpAsync();
                    succeeded = true;
                }
                finally
                {
                    if (!succeeded)
                    {
                        await this.CloseOrAbortWebSocketAsync(connectTask, abort: true).ConfigureAwait(false);
                    }
                }
            }

            public async Task CloseAsync(CancellationToken cancellationToken)
            {
                Task<WebSocket> connectTask;
                lock (this.ThisLock)
                {
                    if (this.closeCalled)
                    {
                        return;
                    }

                    this.closeCancellationToken = cancellationToken;
                    connectTask = this.connectAsyncTask;
                    this.connectAsyncTask = null;
                    this.closeCalled = true;
                }

                this.tokenRenewer.Close();
                this.shutdownCancellationSource.Cancel();

                // Start a clean close by first calling CloseOutputAsync.  The finish (CloseAsync) happens when
                // the receive pump task finishes working.
                if (connectTask != null)
                {
                    var webSocket = await connectTask.ConfigureAwait(false);
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", cancellationToken).ConfigureAwait(false);
                }

                if (this.receivePumpTask != null)
                {
                    await this.receivePumpTask.ConfigureAwait(false);
                }
            }

            internal async Task SendCommandAndStreamAsync(ListenerCommand command, Stream stream, CancellationToken cancelToken)
            {
                Task<WebSocket> connectTask = this.EnsureConnectTask(cancelToken);
                var webSocket = await connectTask.ConfigureAwait(false);

                using (await this.sendAsyncLock.LockAsync(cancelToken).ConfigureAwait(false))
                {
                    // Since we're using the sendBuffer.Array this work needs to be inside the sendAsyncLock.
                    ArraySegment<byte> commandBuffer;
                    using (var commandStream = new MemoryStream(this.sendBuffer.Array, writable: true))
                    {
                        commandStream.SetLength(0);
                        command.WriteObject(commandStream);
                        commandBuffer = new ArraySegment<byte>(this.sendBuffer.Array, 0, (int)commandStream.Position);
                    }

                    await webSocket.SendAsync(commandBuffer, WebSocketMessageType.Text, true, cancelToken).ConfigureAwait(false);

                    if (stream != null)
                    {
                        await webSocket.SendStreamAsync(stream, WebSocketMessageType.Binary, this.sendBuffer, cancelToken).ConfigureAwait(false);
                    }
                }
            }

            /// <summary>
            /// Gets or potentially creates a viable Task&lt;WebSocket&gt;.  If the existing one is faulted or canceled a new one is created.
            /// </summary>
            Task<WebSocket> EnsureConnectTask(CancellationToken cancellationToken)
            {
                lock (this.ThisLock)
                {
                    if (this.connectAsyncTask == null || this.connectAsyncTask.IsFaulted || this.connectAsyncTask.IsCanceled)
                    {
                        this.connectAsyncTask = this.ConnectAsync(cancellationToken);
                    }

                    return this.connectAsyncTask;
                }
            }

            async Task<WebSocket> ConnectAsync(CancellationToken cancellationToken)
            {
                Fx.Assert(!this.closeCalled, "Shouldn't call ConnectWebSocketAsync if CloseAsync was called.");
                var webSocket = ClientWebSocketFactory.Create(this.listener.UseBuiltInClientWebSocket);
                try
                {
                    var connectDelay = ConnectDelayIntervals[this.connectDelayIndex];
                    if (connectDelay != TimeSpan.Zero)
                    {
                        await Task.Delay(connectDelay, cancellationToken).ConfigureAwait(false);
                    }

                    RelayEventSource.Log.ObjectConnecting(this.listener);
                    webSocket.Options.SetBuffer(this.bufferSize, this.bufferSize);
                    webSocket.Options.Proxy = this.listener.Proxy;
                    webSocket.Options.KeepAliveInterval = HybridConnectionConstants.KeepAliveInterval;
                    webSocket.Options.SetRequestHeader(HybridConnectionConstants.Headers.RelayUserAgent, HybridConnectionConstants.ClientAgent);

                    var token = await this.tokenRenewer.GetTokenAsync().ConfigureAwait(false);
                    webSocket.Options.SetRequestHeader(RelayConstants.ServiceBusAuthorizationHeaderName, token.TokenString);

                    // When we reconnect we need to remove the "_GXX" suffix otherwise trackingId gets longer after each reconnect
                    string trackingId = TrackingContext.RemoveSuffix(this.listener.TrackingContext.TrackingId);

                    // Build the websocket uri, e.g. "wss://contoso.servicebus.windows.net:443/$hc/endpoint1?sb-hc-action=listen&sb-hc-id=E2E_TRACKING_ID"
                    var webSocketUri = HybridConnectionUtility.BuildUri(
                        this.address.Host,
                        this.address.Port,
                        this.address.AbsolutePath,
                        this.address.Query,
                        HybridConnectionConstants.Actions.Listen,
                        trackingId);

                    await webSocket.ConnectAsync(webSocketUri, cancellationToken).ConfigureAwait(false);

                    this.OnOnline();
                    RelayEventSource.Log.ObjectConnected(this.listener);
                    return webSocket.WebSocket;
                }
                catch (WebSocketException wsException)
                {
                    throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToRelayContract(wsException, webSocket.Response), this.listener);
                }
            }

            async Task CloseOrAbortWebSocketAsync(
                Task<WebSocket> connectTask,
                bool abort,
                WebSocketCloseStatus closeStatus = WebSocketCloseStatus.Empty,
                string statusDescription = null,
                CancellationToken cancellationToken = default(CancellationToken))
            {
                Fx.Assert(connectTask != null, "CloseWebSocketAsync was called with null connectTask");
                Fx.Assert(connectTask.IsCompleted || !abort, "CloseOrAbortWebSocketAsync(abort=true) should only be called with a completed connectTask");
                lock (this.ThisLock)
                {
                    if (object.ReferenceEquals(connectTask, this.connectAsyncTask))
                    {
                        this.connectAsyncTask = null;
                    }
                }

                WebSocket webSocket = null;
                try
                {
                    webSocket = await connectTask.ConfigureAwait(false);
                    if (abort)
                    {
                        webSocket.Abort();
                    }
                    else
                    {
                        await webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
                        await webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (!Fx.IsFatal(e))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, e);
                    webSocket?.Abort();
                }
            }

            async Task ReceivePumpAsync()
            {
                Exception exception = null;
                try
                {
                    bool keepGoing;
                    do
                    {
                        keepGoing = await this.ReceivePumpCoreAsync().ConfigureAwait(false);
                    }
                    while (keepGoing && !this.shutdownCancellationSource.IsCancellationRequested);
                }
                catch (Exception e) when (!Fx.IsFatal(e))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, e);
                    exception = e;
                }
                finally
                {
                    this.OnOffline(exception);
                }
            }

            /// <summary>
            /// Ensure we have a connected webSocket, listens for command messages, and handles those messages.
            /// </summary>
            /// <returns>A bool indicating whether or not the receive pump should keep running.</returns>
            async Task<bool> ReceivePumpCoreAsync()
            {
                bool keepGoing = true;
                CancellationToken shutdownToken = this.shutdownCancellationSource.Token;
                Task<WebSocket> connectTask = this.EnsureConnectTask(shutdownToken);
                try
                {
                    WebSocket webSocket = await connectTask.ConfigureAwait(false);
                    int totalBytesRead = 0;
                    do
                    {
                        var currentReadBuffer = new ArraySegment<byte>(this.receiveBuffer.Array, this.receiveBuffer.Offset + totalBytesRead, this.receiveBuffer.Count - totalBytesRead);
                        WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(currentReadBuffer, CancellationToken.None).ConfigureAwait(false);
                        if (receiveResult.MessageType == WebSocketMessageType.Close)
                        {
                            await this.CloseOrAbortWebSocketAsync(
                                connectTask, false, receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, shutdownToken).ConfigureAwait(false);
                            if (this.closeCalled)
                            {
                                // This is the cloud service responding to our clean shutdown.
                                keepGoing = false;
                            }
                            else
                            {
                                keepGoing = this.OnDisconnect(new ConnectionLostException(receiveResult.CloseStatus.Value + ": " + receiveResult.CloseStatusDescription));
                            }

                            break;
                        }

                        totalBytesRead += receiveResult.Count;
                        if (receiveResult.EndOfMessage)
                        {
                            var commandBuffer = new ArraySegment<byte>(this.receiveBuffer.Array, this.receiveBuffer.Offset, totalBytesRead);
                            await this.listener.OnCommandAsync(commandBuffer, webSocket).ConfigureAwait(false);
                            totalBytesRead = 0;
                        }
                    }
                    while (!shutdownToken.IsCancellationRequested);
                }
                catch (Exception exception) when (!Fx.IsFatal(exception))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, exception);
                    await this.CloseOrAbortWebSocketAsync(connectTask, abort: true).ConfigureAwait(false);
                    keepGoing = this.OnDisconnect(WebSocketExceptionHelper.ConvertToRelayContract(exception));
                }

                return keepGoing;
            }

            void OnOnline()
            {
                lock (this.ThisLock)
                {
                    if (this.IsOnline)
                    {
                        return;
                    }

                    this.LastError = null;
                    this.IsOnline = true;
                    this.connectDelayIndex = -1;
                }

                RelayEventSource.Log.Info(this.listener, "Online");
                this.Online?.Invoke(this, EventArgs.Empty);
            }

            void OnOffline(Exception lastError)
            {
                lock (this.ThisLock)
                {
                    if (lastError != null)
                    {
                        this.LastError = lastError;
                    }

                    this.IsOnline = false;
                }

                // Stop attempting to connect
                RelayEventSource.Log.Info(this.listener, $"Offline. {this.listener.TrackingContext}");
                this.Offline?.Invoke(this, EventArgs.Empty);
            }

            // Returns true if this control connection should attempt to reconnect after this exception.
            bool OnDisconnect(Exception lastError)
            {
                lock (this.ThisLock)
                {
                    this.LastError = lastError;
                    this.IsOnline = false;

                    if (this.connectDelayIndex < ConnectDelayIntervals.Length - 1)
                    {
                        this.connectDelayIndex++;
                    }
                }

                // Inspect the close status/description to see if this is a terminal case
                // or we should attempt to reconnect.
                bool shouldReconnect = ShouldReconnect(lastError);
                if (shouldReconnect)
                {
                    this.Connecting?.Invoke(this, EventArgs.Empty);
                }

                return shouldReconnect;
            }

            bool ShouldReconnect(Exception exception)
            {
                if (exception is EndpointNotFoundException)
                {
                    return false;
                }

                return true;
            }

            async void OnTokenRenewed(object sender, TokenEventArgs eventArgs)
            {
                try
                {
                    var listenerCommand = new ListenerCommand { RenewToken = new ListenerCommand.RenewTokenCommand() };
                    listenerCommand.RenewToken.Token = eventArgs.Token.TokenString;

                    await this.SendCommandAndStreamAsync(listenerCommand, null, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception) when (!Fx.IsFatal(exception))
                {
                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, exception);
                }
            }
        }
    }
}
