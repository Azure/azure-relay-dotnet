//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Relay.WebSockets;

    /// <summary>
    /// Provides a client for initiating new send-side HybridConnections.
    /// </summary>
    public class HybridConnectionClient
    {
        // Currently 64K
        const int DefaultConnectionBufferSize = 64 * 1024;
        readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(70);

        /// <summary>
        /// Create a new HybridConnectionClient instance for initiating HybridConnections where no client authentication is required.
        /// </summary>
        /// <param name="address">The address on which to listen for HybridConnections.  This address should 
        /// be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".</param>
        public HybridConnectionClient(Uri address)
        {
            this.Initialize(address, null, tokenProviderRequired: false);
        }

        /// <summary>
        /// Create a new HybridConnectionClient instance for initiating HybridConnections with client authentication.
        /// </summary>
        /// <param name="address">The address on which to listen for HybridConnections.  This address should 
        /// be of the format "sb://contoso.servicebus.windows.net/yourhybridconnection".</param>
        /// <param name="tokenProvider">The TokenProvider for connecting to ServiceBus.</param>
        public HybridConnectionClient(Uri address, TokenProvider tokenProvider)
        {
            this.Initialize(address, tokenProvider, tokenProviderRequired: true);
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
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// Gets or sets the connection buffer size.  Default value is 64K.
        /// </summary>
        int ConnectionBufferSize { get; set; }

        /// <summary>
        /// Establishes a new send-side HybridConnection and returns the Stream.
        /// </summary>
        public async Task<HybridConnectionStream> CreateConnectionAsync()
        {
            TrackingContext trackingContext = CreateTrackingContext(this.Address);
            string traceSource = this.GetType().Name + "(" + trackingContext + ")";
            var timeoutHelper = new TimeoutHelper(this.ConnectTimeout);

            RelayEventSource.Log.RelayClientConnectStart(traceSource);
            try
            {
                var webSocket = new ClientWebSocket45();
                webSocket.Options.Proxy = this.Proxy;
                webSocket.Options.SetBuffer(this.ConnectionBufferSize, this.ConnectionBufferSize);

                if (this.TokenProvider != null)
                {
                    RelayEventSource.Log.GetTokenStart(traceSource);
                    var token = await this.TokenProvider.GetTokenAsync(this.Address.GetLeftPart(UriPartial.Path), RelayConstants.Claims.Send, timeoutHelper.RemainingTime());
                    RelayEventSource.Log.GetTokenStop(token.ExpiresAtUtc);

                    webSocket.Options.SetRequestHeader(RelayConstants.ServiceBusAuthorizationHeaderName, token.TokenValue.ToString());
                }

                // Build the websocket uri, e.g. "wss://contoso.servicebus.windows.net:443/$servicebus/hybridconnection/endpoint1?sb-hc-action=connect&sb-hc-id=E2E_TRACKING_ID"
                Uri webSocketUri = HybridConnectionUtility.BuildUri(
                    this.Address.Host,
                    this.Address.Port,
                    this.Address.AbsolutePath,
                    this.Address.Query,
                    HybridConnectionConstants.Actions.Connect,
                    trackingContext.TrackingId);

                using (var cancelSource = new CancellationTokenSource(timeoutHelper.RemainingTime()))
                {
                    await webSocket.ConnectAsync(webSocketUri, cancelSource.Token);
                }

                var trackingId = webSocket.ResponseHeaders[TrackingContext.TrackingIdName];
                if (!string.IsNullOrEmpty(trackingId))
                {
                    // Update to the flown trackingId (which has _GX suffix)
                    trackingContext = TrackingContext.Create(trackingId, trackingContext.SubsystemId);
                    traceSource = this.GetType().Name + "(" + trackingContext + ")";
                }

                return new WebSocketStream(webSocket, trackingContext);
            }
            catch (WebSocketException wsException)
            {
                throw RelayEventSource.Log.ThrowingException(WebSocketExceptionHelper.ConvertToIoContract(wsException), traceSource);
            }
            finally
            {
                RelayEventSource.Log.RelayClientConnectStop(traceSource);
            }
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

        void Initialize(Uri address, TokenProvider tokenProvider, bool tokenProviderRequired = false)
        {
            if (address == null)
            {
                throw RelayEventSource.Log.ThrowingException(new ArgumentNullException("address"), this);
            }
            else if (tokenProviderRequired && tokenProvider == null)
            {
                throw RelayEventSource.Log.ThrowingException(new ArgumentNullException("tokenProvider"), this);
            }
            else if (address.Scheme != RelayConstants.HybridConnectionScheme)
            {
                throw RelayEventSource.Log.ThrowingException(
                    new ArgumentException(SR.InvalidUriScheme.FormatInvariant(address.Scheme, RelayConstants.HybridConnectionScheme), "address"));
            }

            this.Address = address;
            this.TokenProvider = tokenProvider;
            this.ConnectionBufferSize = DefaultConnectionBufferSize;
            this.ConnectTimeout = DefaultConnectTimeout;
            this.Proxy = WebRequest.DefaultWebProxy;
        }
    }
}
