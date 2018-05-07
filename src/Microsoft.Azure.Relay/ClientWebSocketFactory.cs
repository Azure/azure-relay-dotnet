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

    interface IClientWebSocket
    {
        IClientWebSocketOptions Options { get; }

        HttpResponseMessage Response { get; }

        WebSocket WebSocket { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    }

    interface IClientWebSocketOptions
    {
        IWebProxy Proxy { get; set; }

        TimeSpan KeepAliveInterval { get; set; }

        void AddSubProtocol(string subProtocol);

        void SetBuffer(int receiveBufferSize, int sendBufferSize);

        void SetRequestHeader(string name, string value);
    }

    static class ClientWebSocketFactory
    {
        public static IClientWebSocket Create(bool useBuiltInWebSocket)
        {
#if NETSTANDARD
            if (!useBuiltInWebSocket)
            {
                return new Microsoft.Azure.Relay.WebSockets.ClientWebSocket();
            }
#endif // NETSTANDARD

            return new FrameworkClientWebSocketProxy(new System.Net.WebSockets.ClientWebSocket());
        }

        class FrameworkClientWebSocketProxy : IClientWebSocket
        {
            readonly System.Net.WebSockets.ClientWebSocket client;

            public FrameworkClientWebSocketProxy(System.Net.WebSockets.ClientWebSocket client)
            {
                this.client = client;
                this.Options = new FrameworkClientWebSocketOptions(this.client.Options);
            }

            public IClientWebSocketOptions Options { get; }

            public HttpResponseMessage Response { get => null; }
            
            public WebSocket WebSocket { get => this.client; }

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
                    get => this.options.Proxy;
                    set => this.options.Proxy = value;
                }

                public TimeSpan KeepAliveInterval
                {
                    get => this.options.KeepAliveInterval;
                    set => this.options.KeepAliveInterval = value;
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
