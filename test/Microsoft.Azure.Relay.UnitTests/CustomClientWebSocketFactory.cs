using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Relay.UnitTests
{
    public class CustomClientWebSocketFactory : IClientWebSocketFactory
    {
        public bool IsCreatedCalled;

        public CustomClientWebSocketFactory()
        {
            this.IsCreatedCalled = false;
        }

        public IClientWebSocket Create()
        {
            IsCreatedCalled = true;
            return new CustomClientWebSocket(new ClientWebSocket());
        }
    }

    // Custom websocket implementation.
    class CustomClientWebSocket : IClientWebSocket
    {
        readonly ClientWebSocket client;

        public CustomClientWebSocket(System.Net.WebSockets.ClientWebSocket client)
        {
            this.client = client;
            this.Options = new CustomClientWebSocketOptions(this.client.Options);
        }

        public IClientWebSocketOptions Options { get; }

        public HttpResponseMessage Response { get { return null; } }

        public WebSocket WebSocket { get { return this.client; } }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            return this.client.ConnectAsync(uri, cancellationToken);
        }

        class CustomClientWebSocketOptions : IClientWebSocketOptions
        {
            readonly System.Net.WebSockets.ClientWebSocketOptions options;
            public CustomClientWebSocketOptions(ClientWebSocketOptions options)
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
