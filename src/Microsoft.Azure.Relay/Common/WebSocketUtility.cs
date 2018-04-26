// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    struct WebSocketReadMessageResult
    {
        public int Length { get; set; }
        public WebSocketMessageType MessageType { get; set; }
        public WebSocketCloseStatus? CloseStatus { get; set; }
        public string CloseStatusDescription { get; set; }
    }

    static class WebSocketUtility
    {
        public static async Task SendStreamAsync(this WebSocket webSocket, Stream stream, WebSocketMessageType messageType, ArraySegment<byte> buffer, CancellationToken cancelToken)
        {
            bool endOfMessage = false;
            int totalBytesSent = 0;
            int bytesRead;
            do
            {
                bytesRead = await stream.ReadAsync(buffer.Array, buffer.Offset, buffer.Count, cancelToken).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    // Send an empty frame with EndOfMessage = true
                    endOfMessage = true;
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer.Array, 0, 0), messageType, endOfMessage, cancelToken).ConfigureAwait(false);
                }
                else
                {
                    totalBytesSent += bytesRead;
                    if (stream.CanSeek)
                    {
                        endOfMessage = totalBytesSent == stream.Length;
                    }

                    await webSocket.SendAsync(new ArraySegment<byte>(buffer.Array, buffer.Offset, bytesRead), messageType, endOfMessage, cancelToken).ConfigureAwait(false);
                }
            }
            while (!endOfMessage);
        }

        /// <summary>
        /// Reads fragments from a WebSocket until an entire message or close is received.
        /// </summary>
        public static async Task<WebSocketReadMessageResult> ReadMessageAsync(WebSocket webSocket, ArraySegment<byte> buffer, Stream destinationStream, CancellationToken cancelToken)
        {
            var readMessageResult = new WebSocketReadMessageResult();
            WebSocketReceiveResult readFragmentResult;
            do
            {
                readFragmentResult = await webSocket.ReceiveAsync(buffer, cancelToken).ConfigureAwait(false);
                readMessageResult.Length += readFragmentResult.Count;
                destinationStream.Write(buffer.Array, buffer.Offset, readFragmentResult.Count);
            }
            while (!readFragmentResult.EndOfMessage && readFragmentResult.MessageType != WebSocketMessageType.Close);

            readMessageResult.MessageType = readFragmentResult.MessageType;
            readMessageResult.CloseStatus = readFragmentResult.CloseStatus;
            readMessageResult.CloseStatusDescription = readFragmentResult.CloseStatusDescription;
            return readMessageResult;
        }
    }
}
