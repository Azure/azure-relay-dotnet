﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a client for initiating new send-side HybridConnections.
    /// </summary>
    public class HybridConnectionClient
    {
        const int DefaultConnectionBufferSize = 64 * 1024;
        static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(70);
        bool useBuiltInClientWebSocket;

        /// <summary>
        /// Create a new HybridConnectionClient instance for initiating HybridConnections where no client authentication is required.
        /// </summary>
        /// <param name="address">The address on which to listen for HybridConnections.  This address should 
        /// be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".</param>
        public HybridConnectionClient(Uri address)
        {
            this.Initialize(address, DefaultConnectTimeout, null, tokenProviderRequired: false);
        }

        /// <summary>
        /// Create a new HybridConnectionClient instance for initiating HybridConnections with client authentication.
        /// </summary>
        /// <param name="address">The address on which to listen for HybridConnections.  This address should 
        /// be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".</param>
        /// <param name="tokenProvider">The TokenProvider for connecting to ServiceBus.</param>
        public HybridConnectionClient(Uri address, TokenProvider tokenProvider)
        {
            this.Initialize(address, DefaultConnectTimeout, tokenProvider, tokenProviderRequired: true);
        }

        /// <summary>Creates a new instance of <see cref="HybridConnectionClient" /> using the specified connection string.</summary>
        /// <param name="connectionString">The connection string to use.  This connection string must include the EntityPath property.</param>
        /// <returns>The newly created <see cref="HybridConnectionClient" /> instance.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the format of the <paramref name="connectionString" /> parameter is incorrect.</exception>
        public HybridConnectionClient(string connectionString)
            : this(connectionString, null, pathFromConnectionString: true)
        {
        }

        /// <summary>Creates a new instance of <see cref="HybridConnectionClient" /> from a connection string and
        /// the specified HybridConection path. Use this overload only when the connection string does not use the 
        /// <see cref="RelayConnectionStringBuilder.EntityPath" /> property.</summary> 
        /// <param name="connectionString">The connection string used. This connection string must not include the EntityPath property.</param>
        /// <param name="path">The path to the HybridConnection.</param>
        /// <returns>The created <see cref="HybridConnectionClient" />.</returns>
        /// <exception cref="System.ArgumentException">Thrown when the format of the <paramref name="connectionString" /> parameter is incorrect.</exception>
        public HybridConnectionClient(string connectionString, string path)
            : this(connectionString, path, pathFromConnectionString: false)
        {
        }

        // This private .ctor handles both of the public overloads which take connectionString
        HybridConnectionClient(string connectionString, string path, bool pathFromConnectionString)
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
                    // EntityPath is required in connectionString.
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringMustIncludeEntityPath, nameof(HybridConnectionClient)), this);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    // path is required outside of connectionString
                    throw RelayEventSource.Log.ArgumentNull(nameof(path), this);
                }
                else if (!string.IsNullOrWhiteSpace(builder.EntityPath))
                {
                    // connectionString is not allowed to include EntityPath
                    throw RelayEventSource.Log.Argument(nameof(connectionString), SR.GetString(SR.ConnectionStringMustNotIncludeEntityPath, nameof(HybridConnectionClient)), this);
                }

                builder.EntityPath = path;
            }

            TokenProvider tokenProvider = builder.CreateTokenProvider();

            TimeSpan connectTimeout = DefaultConnectTimeout;
            if (builder.OperationTimeout != RelayConstants.DefaultOperationTimeout)
            {
                // Only change from our default (70 seconds) if it appears user has changed the OperationTimeout in the connectionString.
                connectTimeout = builder.OperationTimeout;
            }

            this.Initialize(new Uri(builder.Endpoint, builder.EntityPath), connectTimeout, tokenProvider, tokenProvider != null);
        }

        /// <summary>
        /// Gets the address of this HybridConnection to connect through. The address on which to listen for HybridConnections.
        /// This address should be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".
        /// </summary>
        public Uri Address { get; private set; }

        /// <summary>
        /// Gets or sets proxy information for connecting to ServiceBus.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets the TokenProvider for authenticating HybridConnections.
        /// </summary>
        public TokenProvider TokenProvider { get; private set; }

        /// <summary>
        /// Gets or sets the timeout used when connecting a HybridConnection.  Default value is 70 seconds.
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }

        /// <summary>
        /// Controls whether the ClientWebSocket from .NET Core or a custom implementation is used.
        /// If a custom <see cref="ClientWebSocketFactory"/> is configured then this property is ignored.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public bool UseBuiltInClientWebSocket
        {
            get
            {
                return this.useBuiltInClientWebSocket;
            }
            set
            {
                this.useBuiltInClientWebSocket = value;

                // If a custom ClientWebSocketFactory has not been configured then switch to our proper implementation
                if (object.ReferenceEquals(this.ClientWebSocketFactory, Microsoft.Azure.Relay.ClientWebSocketFactory.Default) ||
                    object.ReferenceEquals(this.ClientWebSocketFactory, Microsoft.Azure.Relay.ClientWebSocketFactory.DefaultBuiltIn))
                {
                    this.ClientWebSocketFactory = value ? 
                        Microsoft.Azure.Relay.ClientWebSocketFactory.DefaultBuiltIn : 
                        Microsoft.Azure.Relay.ClientWebSocketFactory.Default;
                }
            }
        }

        /// <summary>
        /// Websocket's keep-alive interval.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Custom ClientWebSocketFactory Implementation.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public IClientWebSocketFactory ClientWebSocketFactory { get; set; }

        /// <summary>
        /// Gets or sets the connection buffer size.  Default value is 64K.
        /// </summary>
        int ConnectionBufferSize { get; set; }

        /// <summary>
        /// Establishes a new send-side HybridConnection and returns the Stream.
        /// </summary>
        public Task<HybridConnectionStream> CreateConnectionAsync()
        {
            return CreateConnectionAsync(null);
        }

        /// <summary>
        /// Establishes a new send-side HybridConnection and returns the Stream.
        /// </summary>
        public async Task<HybridConnectionStream> CreateConnectionAsync(IDictionary<string, string> requestHeaders)
        {
            TrackingContext trackingContext = CreateTrackingContext(this.Address);
            string traceSource = nameof(HybridConnectionClient) + "(" + trackingContext + ")";
            // todo - Check if timeout helper needs to be started here.
            var timeoutHelper = TimeoutHelper.CreateOnly(this.OperationTimeout);

            RelayEventSource.Log.ObjectConnecting(traceSource, trackingContext);
            var webSocket = this.ClientWebSocketFactory.Create();
            try
            {
                DefaultWebProxy.ConfigureProxy(webSocket.Options, this.Proxy);
                webSocket.Options.KeepAliveInterval = this.KeepAliveInterval;
                webSocket.Options.SetBuffer(this.ConnectionBufferSize, this.ConnectionBufferSize);
                webSocket.Options.SetRequestHeader(HybridConnectionConstants.Headers.RelayUserAgent, HybridConnectionConstants.ClientAgent);

                if (this.TokenProvider != null)
                {
                    RelayEventSource.Log.GetTokenStart(traceSource);
                    string audience = this.Address.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
                    var token = await this.TokenProvider.GetTokenAsync(audience, TokenProvider.DefaultTokenTimeout).ConfigureAwait(false);
                    RelayEventSource.Log.GetTokenStop(traceSource, token.ExpiresAtUtc);
                    webSocket.Options.SetRequestHeader(RelayConstants.ServiceBusAuthorizationHeaderName, token.TokenString);
                }

                if (requestHeaders != null)
                {
                    foreach (KeyValuePair<string, string> header in requestHeaders)
                    {
                        webSocket.Options.SetRequestHeader(header.Key, header.Value);
                    }
                }

                // Build the websocket uri, e.g. "wss://contoso.servicebus.windows.net:443/$hc/endpoint1?sb-hc-action=connect&sb-hc-id=E2E_TRACKING_ID"
                Uri webSocketUri = HybridConnectionUtility.BuildUri(
                    this.Address.Host,
                    this.Address.Port,
                    this.Address.AbsolutePath,
                    this.Address.Query,
                    HybridConnectionConstants.Actions.Connect,
                    trackingContext.TrackingId);

                using (var cancelSource = new CancellationTokenSource(timeoutHelper.RemainingTime()))
                {
                    await webSocket.ConnectAsync(webSocketUri, cancelSource.Token).ConfigureAwait(false);
                }

                RelayEventSource.Log.ObjectConnected(traceSource, trackingContext);
                return new WebSocketStream(webSocket.WebSocket, trackingContext);
            }
            catch (Exception exception) when (!WebSocketExceptionHelper.IsRelayContract(exception))
            {
                throw RelayEventSource.Log.ThrowingException(
                    WebSocketExceptionHelper.ConvertToRelayContract(exception, trackingContext, webSocket.Response, isListener: false),
                    traceSource);
            }
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
            if (this.TokenProvider == null)
            {
                throw RelayEventSource.Log.ThrowingException(new InvalidOperationException(SR.TokenProviderRequired), this);
            }

            return ManagementOperations.GetAsync<HybridConnectionRuntimeInformation>(this.Address, this.TokenProvider, cancellationToken);
        }

        static TrackingContext CreateTrackingContext(Uri address)
        {
#if DEBUG
            // In DEBUG builds allow setting the trackingId via query string: "?id=00000000-0000-0000-0000-000000000000"
            if (!string.IsNullOrEmpty(address.Query))
            {
                string[] kvps = address.Query.TrimStart('?').Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string kvp in kvps)
                {
                    if (kvp.StartsWith("id=", StringComparison.Ordinal))
                    {
                        return TrackingContext.Create(kvp.Substring(3), address);
                    }
                }
            }
#endif // DEBUG

            return TrackingContext.Create(address);
        }

        void Initialize(Uri address, TimeSpan operationTimeout, TokenProvider tokenProvider, bool tokenProviderRequired)
        {
            if (address == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(address), this);
            }
            else if (address.Scheme != RelayConstants.HybridConnectionScheme)
            {
                throw RelayEventSource.Log.Argument(nameof(address), SR.GetString(SR.InvalidUriScheme, address.Scheme, RelayConstants.HybridConnectionScheme), this);
            }
            else if (tokenProviderRequired && tokenProvider == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(tokenProvider), this);
            }

            this.Address = address;
            this.TokenProvider = tokenProvider;
            this.ConnectionBufferSize = DefaultConnectionBufferSize;
            this.OperationTimeout = operationTimeout;
            this.Proxy = DefaultWebProxy.Instance;
            this.useBuiltInClientWebSocket = HybridConnectionConstants.DefaultUseBuiltInClientWebSocket;
            this.ClientWebSocketFactory = Microsoft.Azure.Relay.ClientWebSocketFactory.Default;
            this.KeepAliveInterval = HybridConnectionConstants.KeepAliveInterval;
        }
    }
}
