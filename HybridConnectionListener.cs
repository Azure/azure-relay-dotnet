//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay.WebSockets;

    /// <summary>
    /// Provides a listener for accepting HybridConnections from remote clients.
    /// </summary>
    public class HybridConnectionListener : IConnectionStatus
    {
        static readonly string ClientAgentFileVersion = LookupFileVersion();

        const int DefaultConnectionBufferSize = 64 * 1024;
        readonly Dictionary<string, DataConnection> clientConnections;
        readonly AsyncProducerConsumerCollection<HybridConnectionStream> connectionInputQueue;
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
                throw RelayEventSource.Log.ThrowingException(new ArgumentNullException(address == null ? "address" : "tokenProvider"), this);
            }
            else if (address.Scheme != RelayConstants.HybridConnectionScheme)
            {
                throw RelayEventSource.Log.ThrowingException(
                    new ArgumentException(SR.InvalidUriScheme.FormatInvariant(address.Scheme, RelayConstants.HybridConnectionScheme), "address"), this);
            }

            this.Address = address;
            this.TokenProvider = tokenProvider;
            this.ConnectionBufferSize = DefaultConnectionBufferSize;
            this.proxy = WebRequest.DefaultWebProxy;
            this.TrackingContext = TrackingContext.Create(this.Address);

            this.clientConnections = new Dictionary<string, DataConnection>();
            this.connectionInputQueue = new AsyncProducerConsumerCollection<HybridConnectionStream>();
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
        /// </summary>
        public async Task OpenAsync(TimeSpan timeout)
        {
            TimeoutHelper.ThrowIfNegativeArgument(timeout);
            using (var cts = new CancellationTokenSource(timeout))
            {
                await this.OpenAsync(cts.Token);
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

            await this.controlConnection.OpenAsync(cancellationToken);
        }

        /// <summary>
        /// Closes the <see cref="HybridConnectionListener"/> using the provided timeout.
        /// </summary>
        public async Task CloseAsync(TimeSpan timeout)
        {
            TimeoutHelper.ThrowIfNegativeArgument(timeout);
            using (var cts = new CancellationTokenSource(timeout))
            {
                await this.CloseAsync(cts.Token);
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
                await this.controlConnection.CloseAsync(cancellationToken);

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

            return this.connectionInputQueue.TakeAsync();
        }

        public override string ToString()
        {
            if (this.cachedToString == null)
            {
                this.cachedToString = this.GetType().Name + "(" + this.TrackingContext + ")";
            }

            return this.cachedToString;
        }

        static string LookupFileVersion()
        {
            var a = Assembly.GetExecutingAssembly();
            var attribute = (AssemblyFileVersionAttribute)a.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true)[0];
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

            this.connectionInputQueue.Add(connection);
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
                    await this.OnAcceptClientCommandAsync(listenerCommand.Accept);
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
            // This results in retries after 0.9375, 1.875, 3.75, 7.5, 15, 30, and 60 seconds.
            static readonly TimeSpan MinumumReconnectInterval = TimeSpan.FromTicks(9375000);
            static readonly TimeSpan MaximumReconnectInterval = TimeSpan.FromSeconds(60);

            readonly HybridConnectionListener listener;
            readonly Uri address;
            readonly int bufferSize;
            readonly string path;
            readonly TokenRenewer tokenRenewer;
            readonly CancellationTokenSource shutdownCancellationSource;
            readonly ClientWebSocket45 clientWebSocket;
            Task receivePumpTask;
            ArraySegment<byte> receiveBuffer;
            bool closeCalled;
            CancellationToken closeCancellationToken;

            public ControlConnection(HybridConnectionListener listener)
            {
                this.listener = listener;
                this.address = listener.Address;
                this.bufferSize = listener.ConnectionBufferSize;
                this.path = address.AbsolutePath.TrimStart('/');
                this.shutdownCancellationSource = new CancellationTokenSource();
                this.receiveBuffer = new ArraySegment<byte>(new byte[this.bufferSize]);
                this.clientWebSocket = new ClientWebSocket45();
                this.tokenRenewer = new TokenRenewer(this.listener.TokenProvider, this.address.AbsoluteUri, RelayConstants.Claims.Listen, this.listener);
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
                await this.ConnectWebSocketAsync(cancellationToken);
                this.tokenRenewer.TokenRenewed += this.OnTokenRenewed;
                this.receivePumpTask = this.ReceivePumpAsync(this.shutdownCancellationSource.Token);
            }

            public async Task CloseAsync(CancellationToken cancellationToken)
            {
                lock (this.ThisLock)
                {
                    if (this.closeCalled)
                    {
                        return;
                    }

                    this.closeCancellationToken = cancellationToken;
                    this.closeCalled = true;
                }

                this.tokenRenewer.Close();
                this.shutdownCancellationSource.Cancel();

                // Start a clean close by first calling CloseOutputAsync.  The finish (CloseAsync) happens when
                // the receive pump task finishes working.
                await this.clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Normal Closure", cancellationToken);
                if (this.receivePumpTask != null)
                {
                    await this.receivePumpTask;
                }
            }

            async Task ConnectWebSocketAsync(CancellationToken cancellationToken)
            {
                try
                {
                    RelayEventSource.Log.RelayClientConnectStart(this.listener);

                    this.clientWebSocket.Options.SetBuffer(this.bufferSize, this.bufferSize);
                    this.clientWebSocket.Options.Proxy = this.listener.Proxy;
                    this.clientWebSocket.Options.UserAgent = "ServiceBus/" + ClientAgentFileVersion;

                    var token = await this.tokenRenewer.GetTokenAsync(TimeSpan.FromMinutes(1));
                    this.clientWebSocket.Options.SetRequestHeader(RelayConstants.ServiceBusAuthorizationHeaderName, token.TokenValue.ToString());

                    // Build the websocket uri, e.g. "wss://contoso.servicebus.windows.net:443/$servicebus/hybridconnection/endpoint1?sb-hc-action=listen&sb-hc-id=E2E_TRACKING_ID"
                    var webSocketUri = HybridConnectionUtility.BuildUri(
                        this.address.Host,
                        this.address.Port,
                        this.address.AbsolutePath,
                        this.address.Query,
                        HybridConnectionConstants.Actions.Listen,
                        this.listener.TrackingContext.TrackingId);

                    await this.clientWebSocket.ConnectAsync(webSocketUri, cancellationToken);

                    var trackingId = this.clientWebSocket.ResponseHeaders[TrackingContext.TrackingIdName];
                    if (!string.IsNullOrEmpty(trackingId))
                    {
                        // Update to the flown trackingId (which has _GX suffix)
                        this.listener.TrackingContext = TrackingContext.Create(trackingId, this.listener.TrackingContext.SubsystemId);
                    }

                    this.OnOnline();
                }
                catch (WebSocketException wsException)
                {
                    throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToIoContract(wsException), this.listener);
                }
                finally
                {
                    RelayEventSource.Log.RelayClientConnectStop(this.listener);
                }
            }

            async Task CloseWebSocketAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                try
                {
                    await this.clientWebSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
                    await this.clientWebSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, e);
                    this.clientWebSocket.Abort();
                }
            }

            async Task ReceivePumpAsync(CancellationToken shutdownToken)
            {
                try
                {
                    while (!shutdownToken.IsCancellationRequested)
                    {
                        await this.ReceivePumpCoreAsync(shutdownToken);
                    }

                    // Since we're out of the above while loop we must be shutting down (closing or faulted)
                    await this.CloseWebSocketAsync(WebSocketCloseStatus.NormalClosure, "Closing", this.closeCancellationToken);
                    this.OnOffline(null);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    RelayEventSource.Log.HandledExceptionAsWarning(this.listener, e);
                }
            }

            /// <summary>
            /// Ensure we have a connected webSocket, listen for a command message, and handle that message.
            /// </summary>
            async Task ReceivePumpCoreAsync(CancellationToken shutdownToken)
            {
                var receiveResult = await this.clientWebSocket.ReceiveAsync(this.receiveBuffer, CancellationToken.None);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    await this.CloseWebSocketAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, shutdownToken);
                    this.OnDisconnect(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription);
                    return;
                }

                Fx.Assert(receiveResult.Count > 0, "Expected a non-zero count of bytes received.");
                this.listener.OnCommandAsync(new ArraySegment<byte>(this.receiveBuffer.Array, this.receiveBuffer.Offset, receiveResult.Count)).Fork(this.listener);
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
                }

                RelayEventSource.Log.RelayClientGoingOnline(this.listener.ToString());
                var handler = this.Online;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
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
                var handler = this.Offline;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            void OnDisconnect(WebSocketCloseStatus closeStatus, string closeDescription)
            {
                lock (this.ThisLock)
                {
                    this.LastError = new ConnectionLostException(closeStatus + ": " + closeDescription);
                    this.IsOnline = false;
                }

                // TODO: Inspect the close status/description to see if this is a terminal case
                // or we should attempt to reconnect.
                var handler = this.Connecting;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }

            async void OnTokenRenewed(object sender, TokenEventArgs eventArgs)
            {
                try
                {
                    var listenerCommand = new ListenerCommand();
                    listenerCommand.RenewToken = new ListenerCommand.RenewTokenCommand();
                    listenerCommand.RenewToken.Token = eventArgs.Token.TokenValue.ToString();

                    byte[] buffer;
                    using (var stream = new MemoryStream())
                    {
                        listenerCommand.WriteObject(stream);
                        buffer = stream.GetBuffer();
                        await this.clientWebSocket.SendAsync(new ArraySegment<byte>(buffer, 0, (int)stream.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
                    }
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
                        connection.Close();
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
                    await Task.Delay(TimeSpan.FromMilliseconds(2));

                    var timeoutHelper = new TimeoutHelper(AcceptTimeout);

                    var clientWebSocket = new ClientWebSocket45();
                    clientWebSocket.Options.SetBuffer(this.bufferSize, this.bufferSize);
                    clientWebSocket.Options.Host = this.Address.Host;
                    clientWebSocket.Options.Proxy = this.listener.Proxy;

                    using (var cancelSource = new CancellationTokenSource(timeoutHelper.RemainingTime()))
                    {
                        await clientWebSocket.ConnectAsync(this.rendezvousAddress, cancelSource.Token);
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
