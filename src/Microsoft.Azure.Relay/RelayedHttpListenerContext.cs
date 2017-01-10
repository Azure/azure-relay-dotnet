// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using ClientWebSocket = Microsoft.Azure.Relay.WebSockets.ClientWebSocket;

    /// <summary>
    /// Provides access to the request and response objects representing a client request to a <see cref="HybridConnectionListener"/>.
    /// This is modeled after System.Net.HttpListenerContext.
    /// </summary>
    public class RelayedHttpListenerContext
    {
        static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(20);
        readonly Uri wssRendezvousAddress;
        string cachedToString;

        internal RelayedHttpListenerContext(HybridConnectionListener listener, ListenerCommand.AcceptCommand acceptCommand)
        {
            this.Listener = listener;
            this.wssRendezvousAddress = new Uri(acceptCommand.Address);
            Uri requestUri = this.GenerateRequestUri();
            this.TrackingContext = TrackingContext.Create(acceptCommand.Id, requestUri);
            this.Request = new RelayedHttpListenerRequest(requestUri, acceptCommand);
            this.Response = new RelayedHttpListenerResponse(this);
            
            this.FlowSubProtocol();
        }

        /// <summary>
        /// Gets the <see cref="RelayedHttpListenerRequest"/> that represents a client's request for a resource.
        /// </summary>
        public RelayedHttpListenerRequest Request { get; }

        /// <summary>
        /// Gets the <see cref="RelayedHttpListenerResponse"/> object to control the response to the client's request.
        /// </summary>
        public RelayedHttpListenerResponse Response { get; }

        internal HybridConnectionListener Listener { get; }

        internal TrackingContext TrackingContext { get; }

        /// <summary>
        /// Returns a string that represents the current object.  Includes a TrackingId for end to end correlation.
        /// </summary>
        public override string ToString()
        {
            return this.cachedToString ?? (this.cachedToString = nameof(RelayedHttpListenerContext) + "(" + this.TrackingContext + ")");
        }

        internal async Task<WebSocketStream> AcceptAsync()
        {
            // Performance: Address Resolution (ARP) work-around: When we receive the control message from a TCP connection which hasn't had any
            // outbound traffic for 2 minutes the ARP cache no longer has the MAC address required to ACK the control message.  If we also begin
            // connecting a new socket at exactly the same time there's a known race condition (insert link here) where ARP can only resolve one
            // address at a time, which causes the loser of the race to have to retry after 3000ms.  To avoid the 3000ms delay we just pause for
            // a few ms here instead.
            await Task.Delay(TimeSpan.FromMilliseconds(2)).ConfigureAwait(false);

            var clientWebSocket = this.CreateWebSocket();

            // If we are accepting a sub-protocol handle that here
            var subProtocol = this.Response.Headers[HybridConnectionConstants.Headers.SecWebSocketProtocol];
            if (!string.IsNullOrEmpty(subProtocol))
            {
                clientWebSocket.Options.AddSubProtocol(subProtocol);
            }

            using (var cancelSource = new CancellationTokenSource(AcceptTimeout))
            {
                await clientWebSocket.ConnectAsync(this.wssRendezvousAddress, cancelSource.Token).ConfigureAwait(false);
            }

            var webSocketStream = new WebSocketStream(clientWebSocket, this.TrackingContext);
            return webSocketStream;
        }

        internal async Task RejectAsync()
        {
            ClientWebSocket clientWebSocket = null;
            try
            {
                if (this.Response.StatusCode == HttpStatusCode.Continue)
                {
                    this.Response.StatusCode = HttpStatusCode.BadRequest;
                    this.Response.StatusDescription = "Rejected by user code";
                }

                // Add the status code/description to the URI query string
                int requiredCapacity = this.wssRendezvousAddress.OriginalString.Length + 50 + this.Response.StatusDescription.Length;
                var stringBuilder = new StringBuilder(this.wssRendezvousAddress.OriginalString, requiredCapacity);
                stringBuilder.AppendFormat("&{0}={1}", HybridConnectionConstants.StatusCode, (int)this.Response.StatusCode);
                stringBuilder.AppendFormat("&{0}={1}", HybridConnectionConstants.StatusDescription, WebUtility.UrlEncode(this.Response.StatusDescription));
                Uri rejectUri = new Uri(stringBuilder.ToString());

                clientWebSocket = this.CreateWebSocket();
                using (var cancelSource = new CancellationTokenSource(AcceptTimeout))
                {
                    await clientWebSocket.ConnectAsync(rejectUri, cancelSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e) when (!Fx.IsFatal(e))
            {
                WebException webException;
                HttpWebResponse httpWebResponse;
                if (e is WebSocketException &&
                    (webException = e.InnerException as WebException) != null &&
                    (httpWebResponse = webException.Response as HttpWebResponse) != null && 
                    httpWebResponse.StatusCode == HttpStatusCode.Gone)
                {
                    // status code of "Gone" is expected when rejecting a client request
                    return;
                }

                RelayEventSource.Log.HandledExceptionAsWarning(this, e);
            }
            finally
            {
                clientWebSocket?.Abort();
            }
        }

        ClientWebSocket CreateWebSocket()
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.SetBuffer(this.Listener.ConnectionBufferSize, this.Listener.ConnectionBufferSize);
            clientWebSocket.Options.SetRequestHeader("Host", this.Listener.Address.Host);
            clientWebSocket.Options.Proxy = this.Listener.Proxy;
            clientWebSocket.Options.KeepAliveInterval = HybridConnectionConstants.KeepAliveInterval;
            return clientWebSocket;
        }

        /// <summary>
        /// Form the logical request Uri using the scheme://host:port from the listener and the path from the acceptCommand (minus "/$hc")
        /// e.g. sb://contoso.servicebus.windows.net/hybrid1?foo=bar
        /// </summary>
        Uri GenerateRequestUri()
        {
            var requestUri = new UriBuilder(this.Listener.Address);
            requestUri.Query = HybridConnectionUtility.FilterQueryString(this.wssRendezvousAddress.Query);
            requestUri.Path = this.wssRendezvousAddress.GetComponents(UriComponents.Path, UriFormat.Unescaped);
            if (requestUri.Path.StartsWith("$hc/", StringComparison.Ordinal))
            {
                requestUri.Path = requestUri.Path.Substring(4);
            }

            return requestUri.Uri;
        }

        void FlowSubProtocol()
        {
            // By default use the first sub-protocol (if present)
            string subProtocol = this.Request.Headers[HybridConnectionConstants.Headers.SecWebSocketProtocol];
            if (!string.IsNullOrEmpty(subProtocol))
            {
                int separatorIndex = subProtocol.IndexOf(',');
                if (separatorIndex >= 0)
                {
                    // more than one sub-protocol in headers, only use the first.
                    subProtocol = subProtocol.Substring(0, separatorIndex);
                }

                this.Response.Headers[HybridConnectionConstants.Headers.SecWebSocketProtocol] = subProtocol;
            }
        }
    }
}
