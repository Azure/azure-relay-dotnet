// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class WebSocketStreamTests
    {
        /// <summary>
        /// Verifies that calling CloseAsync on a WebSocketStream whose underlying
        /// WebSocket has been disposed does not throw ObjectDisposedException.
        /// </summary>
        [Fact]
        [DisplayTestMethodName]
        public async Task CloseAsync_DisposedWebSocket_DoesNotThrow()
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Dispose();

            var trackingContext = TrackingContext.Create();
            var stream = new WebSocketStream(clientWebSocket, trackingContext);

            // This should not throw ObjectDisposedException
            await stream.CloseAsync(CancellationToken.None);
        }

        /// <summary>
        /// Verifies that calling ShutdownAsync on a WebSocketStream whose underlying
        /// WebSocket has been disposed does not throw ObjectDisposedException.
        /// </summary>
        [Fact]
        [DisplayTestMethodName]
        public async Task ShutdownAsync_DisposedWebSocket_DoesNotThrow()
        {
            var clientWebSocket = new ClientWebSocket();
            clientWebSocket.Dispose();

            var trackingContext = TrackingContext.Create();
            var stream = new WebSocketStream(clientWebSocket, trackingContext);

            // This should not throw ObjectDisposedException
            await stream.ShutdownAsync(CancellationToken.None);
        }
    }
}
