// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Client WebSocket Interface.
    /// </summary>
    public interface IClientWebSocket
    {
        /// <summary>
        /// Client Websocket Options.
        /// </summary>
        IClientWebSocketOptions Options { get; }

        /// <summary>
        /// Http Response Message.
        /// </summary>
        HttpResponseMessage Response { get; }

        /// <summary>
        /// Websocket object.
        /// </summary>
        WebSocket WebSocket { get; }

        /// <summary>
        /// Connect to a WebSocket server as an asynchronous operation.
        /// </summary>
        /// <param name="uri">The URI of the WebSocket server to connect to.</param>
        /// <param name="cancellationToken">A cancellation token used to propagate notification that the operation should be canceled.</param>
        /// <returns></returns>
        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Client Websocket Options interface.
    /// </summary>
    public interface IClientWebSocketOptions
    {
        /// <summary>
        /// Gets or sets the proxy for WebSocket requests.
        /// </summary>
        IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket protocol keep-alive interval.
        /// </summary>
        TimeSpan KeepAliveInterval { get; set; }

        /// <summary>
        /// Adds a sub-protocol to be negotiated during the WebSocket connection handshake.
        /// </summary>
        /// <param name="subProtocol">The WebSocket sub-protocol to add.</param>
        void AddSubProtocol(string subProtocol);

        /// <summary>
        /// Sets the client buffer parameters.
        /// </summary>
        /// <param name="receiveBufferSize">The size, in bytes, of the client receive buffer.</param>
        /// <param name="sendBufferSize">The size, in bytes, of the client send buffer.</param>
        void SetBuffer(int receiveBufferSize, int sendBufferSize);

        /// <summary>
        /// Creates a HTTP request header and its value.
        /// </summary>
        /// <param name="name">The name of the HTTP header.</param>
        /// <param name="value">The value of the HTTP header.</param>
        void SetRequestHeader(string name, string value);
    }

    static class ClientWebSocketFactory
    {
        public static IClientWebSocket Create(bool useBuiltInWebSocket)
        {
#if NETSTANDARD
            if (!useBuiltInWebSocket)
            {
                if (Microsoft.Azure.Relay.WebSockets.NetCore21.ClientWebSocket.IsSupported())
                {
                    return new Microsoft.Azure.Relay.WebSockets.NetCore21.ClientWebSocket();
                }

                return new Microsoft.Azure.Relay.WebSockets.NetStandard20.ClientWebSocket();
            }
#endif // NETSTANDARD

            return new FrameworkClientWebSocket(new System.Net.WebSockets.ClientWebSocket());
        }

        class FrameworkClientWebSocket : IClientWebSocket
        {
            readonly System.Net.WebSockets.ClientWebSocket client;

            public FrameworkClientWebSocket(System.Net.WebSockets.ClientWebSocket client)
            {
                this.client = client;
                this.Options = new FrameworkClientWebSocketOptions(this.client.Options);
            }

            public IClientWebSocketOptions Options { get; }

            public HttpResponseMessage Response { get { return null; } }
            
            public WebSocket WebSocket { get { return this.client; } }

            public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
            {
                return this.client.ConnectAsync(uri, cancellationToken);
            }

            class FrameworkClientWebSocketOptions : IClientWebSocketOptions
            {
                readonly System.Net.WebSockets.ClientWebSocketOptions options;
                public FrameworkClientWebSocketOptions(System.Net.WebSockets.ClientWebSocketOptions options)
                {
                    this.options = options;
                }

                public IWebProxy Proxy
                {
                    get { return this.options.Proxy; }
                    set { this.options.Proxy = value; }
                }

                public TimeSpan KeepAliveInterval
                {
                    get { return this.options.KeepAliveInterval; }
                    set { this.options.KeepAliveInterval = value; }
                }

                public void AddSubProtocol(string subProtocol)
                {
                    this.options.AddSubProtocol(subProtocol);
                }
               
                public void SetBuffer(int receiveBufferSize, int sendBufferSize)
                {
                    this.options.SetBuffer(receiveBufferSize, sendBufferSize);
                }

                public void SetRequestHeader(string name, string value)
                {
                    this.options.SetRequestHeader(name, value);
                }
            }
        }
    }
}
