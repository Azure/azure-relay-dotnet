//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using ClientWebSocket = Microsoft.Azure.Relay.WebSockets.ClientWebSocket;

    /// <summary>
    /// Provides a listener for accepting HybridConnections from remote clients.
    /// </summary>
    public class HybridConnectionListener : IConnectionStatus
    {
        static readonly string ClientAgentFileVersion = LookupFileVersion();

        const int DefaultConnectionBufferSize = 64 * 1024;
        readonly Dictionary<string, DataConnection> clientConnections;
        readonly InputQueue<HybridConnectionStream> connectionInputQueue;
        readonly ControlConnection controlConnection;
        IWebProxy proxy;
        string cachedToString;
        TrackingContext trackingContext;
        bool openCalled;
        bool closeCalled;

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

            this.clientConnections = new Dictionary<string, DataConnection>();
            this.connectionInputQueue = new InputQueue<HybridConnectionStream>();
            this.controlConnection = new ControlConnection(this);
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

            this.clientConnections = new Dictionary<string, DataConnection>();
            this.connectionInputQueue = new InputQueue<HybridConnectionStream>();
            this.controlConnection = new ControlConnection(this);
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
        /// Gets the address on which to listen for HybridConnections.  This address should be of the format
        /// "sb://contoso.servicebus.windows.net/yourhybridconnection".
        /// </summary>
        public Uri Address { get; private set; }

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
        public TokenProvider TokenProvider { get; private set; }

        /// <summary>
        /// Gets or sets the connection buffer size.  Default value is 64K.
        /// </summary>
        int ConnectionBufferSize { get; set; }

        TimeSpan OperationTimeout { get; set; }

        TrackingContext TrackingContext
        {
            get
            {
                return this.trackingContext;
            }
            set
            {
                // We allow updating the TrackingContext in order to use the trackingId flown from the
                // service which has the SBSFE Role instance number suffix on it (i.e. "_GX").
                this.trackingContext = value;
                this.cachedToString = null;
            }
        }

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
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            List<DataConnection> clients;
            lock (this.ThisLock)
            {
                if (this.closeCalled)
                {
                    return;
                }

                this.closeCalled = true;

                clients = new List<DataConnection>(this.clientConnections.Values);
                this.clientConnections.Clear();
            }

            try
            {
                RelayEventSource.Log.RelayClientCloseStart(this);
                await this.controlConnection.CloseAsync(cancellationToken).ConfigureAwait(false);

                for (int index = 0; index < clients.Count; index++)
                {
                    clients[index].Close();
                }
            }
            catch (Exception e)
            {
                RelayEventSource.Log.RelayClientCloseException(this, e);
                throw;
            }
            finally
            {
                this.connectionInputQueue.Dispose();
                RelayEventSource.Log.RelayClientCloseStop();
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
        /// Returns a string that represents the current object.
        /// </summary>
        public override string ToString()
        {
            if (this.cachedToString == null)
            {
                this.cachedToString = this.GetType().Name + "(" + this.TrackingContext + ")";
            }

            return this.cachedToString;
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
        public Task<HybridConnectionRuntimeInformation> GetRuntimeInformationAsync(CancellationToken cancellationToken)
        {
            return ManagementOperations.GetAsync<HybridConnectionRuntimeInformation>(this.Address, this.TokenProvider, cancellationToken);
        }

        [Conditional("DEBUG")]
        internal void SendControlCommand(ListenerCommand listenerCommand, CancellationToken cancellationToken)
        {
            this.controlConnection.SendCommandAsync(listenerCommand, cancellationToken).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static string LookupFileVersion()
        {
            var a = typeof(HybridConnectionListener).GetTypeInfo().Assembly;
            var attribute = a.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return attribute.Version;
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

        void AcceptSucceeded(DataConnection session, HybridConnectionStream connection)
        {
            RelayEventSource.Log.RelayListenerRendezvousStop(this.ToString(), session.TrackingContext.TrackingId);

            lock (this.ThisLock)
            {
                this.clientConnections.Remove(session.Id);
            }

            this.connectionInputQueue.EnqueueAndDispatch(connection, null, false);
            session.Close();
        }

        void AcceptFailed(DataConnection session, Exception exception)
        {
            RelayEventSource.Log.RelayListenerRendezvousFailed(this, session.TrackingContext.TrackingId, exception?.ToString() ?? "Unknown error");
            lock (this.ThisLock)
            {
                this.clientConnections.Remove(session.Id);
            }

            session.Close();
        }

        async Task OnCommandAsync(ArraySegment<byte> buffer)
        {
            try
            {
                ListenerCommand listenerCommand;
                using (var stream = new MemoryStream(buffer.Array, buffer.Offset, buffer.Count))
                {
                    listenerCommand = ListenerCommand.ReadObject(stream);
                }

                if (listenerCommand.Accept != null)
                {
                    await this.OnAcceptClientCommandAsync(listenerCommand.Accept).ConfigureAwait(false);
                }
                else
                {
                    string json = Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count);
                    RelayEventSource.Log.RelayListenerUnknownCommand(this.ToString(), json);
                }
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                RelayEventSource.Log.HandledExceptionAsWarning(this, exception);
            }
        }

        Task OnAcceptClientCommandAsync(ListenerCommand.AcceptCommand acceptCommand)
        {
            string id = acceptCommand.Id;
            var trackingContext = TrackingContext.Create(id, this.Address);

#if DEBUG
            // TestHook: In DEBUG builds if the trackingId is Guid.Empty don't accept the rendezvous
            if (trackingContext.TrackingId.StartsWith(Guid.Empty.ToString(), StringComparison.Ordinal))
            {
                return TaskEx.CompletedTask;
            }
#endif

            RelayEventSource.Log.RelayListenerRendezvousStart(this.ToString(), trackingContext.TrackingId, acceptCommand.Address);

            DataConnection clientConnection;
            lock (this.ThisLock)
            {
                if (this.closeCalled)
                {
                    RelayEventSource.Log.RelayListenerRendezvousFailed(this.ToString(), trackingContext.TrackingId, SR.ObjectClosedOrAborted);
                    return TaskEx.CompletedTask;
                }
                else if (this.clientConnections.ContainsKey(id))
                {
                    RelayEventSource.Log.RelayListenerRendezvousFailed(this.ToString(), trackingContext.TrackingId, SR.DuplicateConnectionId);
                    return TaskEx.CompletedTask;
                }

                clientConnection = new DataConnection(this, acceptCommand, trackingContext);
                this.clientConnections.Add(id, clientConnection);
            }

            return clientConnection.AcceptConnectionAsync();
        }

        void OnConnectionStatus(EventHandler handler, object sender, EventArgs args)
        {
            // Propagate inner properties in case they've mutated.
            var innerStatus = (IConnectionStatus)sender;
            this.IsOnline = innerStatus.IsOnline;
            this.LastError = innerStatus.LastError;
            
            if (handler != null)
            {
                handler(this, args);
            }
        }

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
            readonly AsyncLock sendLock;
            Task<WebSocket> connectAsyncTask;
            Task receivePumpTask;
            ArraySegment<byte> receiveBuffer;
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
                this.sendLock = new AsyncLock();
                this.tokenRenewer = new TokenRenewer(
                    this.listener.TokenProvider, this.address.AbsoluteUri, TokenProvider.DefaultTokenTimeout, this.listener);
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

            internal async Task SendCommandAsync(ListenerCommand listenerCommand, CancellationToken cancellationToken)
            {
                Task<WebSocket> connectTask = this.EnsureConnectTask(cancellationToken);
                var webSocket = await connectTask.ConfigureAwait(false);
                using (var stream = new MemoryStream())
                {
                    listenerCommand.WriteObject(stream);
                    ArraySegment<byte> buffer = stream.GetArraySegment();

                    using (await this.sendLock.LockAsync(cancellationToken).ConfigureAwait(false))
                    {
                        await webSocket.SendAsync(
                            buffer, WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
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
                try
                {
                    var connectDelay = ConnectDelayIntervals[this.connectDelayIndex];
                    if (connectDelay != TimeSpan.Zero)
                    {
                        await Task.Delay(connectDelay, cancellationToken).ConfigureAwait(false);
                    }

                    RelayEventSource.Log.RelayClientConnectStart(this.listener);
                    var webSocket = new ClientWebSocket();
                    webSocket.Options.SetBuffer(this.bufferSize, this.bufferSize);
                    webSocket.Options.Proxy = this.listener.Proxy;
                    webSocket.Options.KeepAliveInterval = HybridConnectionConstants.KeepAliveInterval;
                    webSocket.Options.SetRequestHeader("User-Agent", "ServiceBus/" + ClientAgentFileVersion);

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

#if NET45 // TODO: Flow Response Headers in NETSTANDARD ClientWebSocket
                    trackingId = webSocket.ResponseHeaders[TrackingContext.TrackingIdName];
                    if (!string.IsNullOrEmpty(trackingId))
                    {
                        // Update to the flown trackingId (which has _GX suffix)
                        this.listener.TrackingContext = TrackingContext.Create(trackingId, this.listener.TrackingContext.SubsystemId);
                    }
#endif

                    this.OnOnline();
                    return webSocket;
                }
                catch (WebSocketException wsException)
                {
                    throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToRelayContract(wsException), this.listener);
                }
                finally
                {
                    RelayEventSource.Log.RelayClientConnectStop(this.listener);
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
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

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
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

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
                    do
                    {
                        WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(this.receiveBuffer, CancellationToken.None).ConfigureAwait(false);
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

                        Fx.Assert(receiveResult.Count > 0, "Expected a non-zero count of bytes received.");
                        this.listener.OnCommandAsync(new ArraySegment<byte>(this.receiveBuffer.Array, this.receiveBuffer.Offset, receiveResult.Count)).Fork(this.listener);
                    }
                    while (!shutdownToken.IsCancellationRequested);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }

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

                RelayEventSource.Log.RelayClientGoingOnline(this.listener.ToString());
                this.Online?.Invoke(this, EventArgs.Empty);
            }

            void OnOffline(Exception lastError)
            {
                lock (this.ThisLock)
                {
                    this.LastError = lastError;
                    this.IsOnline = false;
                }

                // Stop attempting to connect
                RelayEventSource.Log.RelayClientStopConnecting(this.listener.ToString(), "HybridConnection");
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
                // TODO: Figure out exceptions where we shouldn't reconnect.
                // Examples would be expired token or the HybridConnection management object was deleted.
                return true;
            }

            async void OnTokenRenewed(object sender, TokenEventArgs eventArgs)
            {
                try
                {
                    var listenerCommand = new ListenerCommand();
                    listenerCommand.RenewToken = new ListenerCommand.RenewTokenCommand();
                    listenerCommand.RenewToken.Token = eventArgs.Token.TokenString;

                    await this.SendCommandAsync(listenerCommand, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }

                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, exception);
                }
            }
        }

        class DataConnection
        {
            static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(20);
            readonly int bufferSize;
            readonly HybridConnectionListener listener;
            readonly TokenProvider tokenProvider;
            readonly Uri rendezvousAddress;
            bool complete;
            bool disposed;

            public DataConnection(HybridConnectionListener listener, ListenerCommand.AcceptCommand acceptCommand, TrackingContext trackingContext)
            {
                this.listener = listener;
                this.bufferSize = listener.ConnectionBufferSize;
                this.Address = listener.Address;
                this.tokenProvider = listener.TokenProvider;
                this.Id = acceptCommand.Id;
                this.rendezvousAddress = new Uri(acceptCommand.Address);
                this.TrackingContext = trackingContext;
            }

            public string Id
            {
                get; private set;
            }

            internal Uri Address
            {
                get; private set;
            }

            public TrackingContext TrackingContext
            {
                get; private set;
            }

            object ThisLock
            {
                get { return this.rendezvousAddress; }
            }

            public void Close()
            {
                lock (this.ThisLock)
                {
                    if (this.disposed)
                        return;

                    this.disposed = true;
                    this.complete = true;
                }
            }

            public void AcceptFailed(Exception exception)
            {
                lock (this.ThisLock)
                {
                    if (this.complete)
                        return;

                    this.complete = true;
                }

                this.listener.AcceptFailed(this, exception);
            }

            public void AcceptSucceeded(HybridConnectionStream connection)
            {
                lock (this.ThisLock)
                {
                    if (this.complete)
                    {
                        connection.Dispose();
                        return;
                    }

                    this.complete = true;
                }

                this.listener.AcceptSucceeded(this, connection);
            }

            public async Task AcceptConnectionAsync()
            {
                try
                {
                    // Performance: Address Resolution (ARP) work-around: When we receive the control message from a TCP connection which hasn't had any
                    // outbound traffic for 2 minutes the ARP cache no longer has the MAC address required to ACK the control message.  If we also begin
                    // connecting a new socket at exactly the same time there's a known race condition (insert link here) where ARP can only resolve one
                    // address at a time, which causes the loser of the race to have to retry after 3000ms.  To avoid the 3000ms delay we just pause for
                    // a few ms here instead.
                    await Task.Delay(TimeSpan.FromMilliseconds(2)).ConfigureAwait(false);

                    var timeoutHelper = new TimeoutHelper(AcceptTimeout);

                    var clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetBuffer(this.bufferSize, this.bufferSize);
                    clientWebSocket.Options.SetRequestHeader("Host", this.Address.Host);
                    clientWebSocket.Options.Proxy = this.listener.Proxy;
                    clientWebSocket.Options.KeepAliveInterval = HybridConnectionConstants.KeepAliveInterval;

                    using (var cancelSource = new CancellationTokenSource(timeoutHelper.RemainingTime()))
                    {
                        await clientWebSocket.ConnectAsync(this.rendezvousAddress, cancelSource.Token).ConfigureAwait(false);
                    }

                    var webSocketStream = new WebSocketStream(clientWebSocket, this.TrackingContext);
                    this.AcceptSucceeded(webSocketStream);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }

                    this.AcceptFailed(exception);
                }
            }
        }
    }
}
