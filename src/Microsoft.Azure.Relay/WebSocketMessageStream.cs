// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    class WebSocketMessageStream : Stream
    {
        readonly WebSocket webSocket;
        long position;

        public WebSocketMessageStream(WebSocket webSocket, TimeSpan readTimeout)
        {
            this.webSocket = webSocket;
            this.ReadTimeout = TimeoutHelper.ToMilliseconds(readTimeout);
        }

        public override bool CanRead { get { return true; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanTimeout { get { return true; } }

        public override bool CanWrite { get { return false; } }

        public override long Length { get { throw new NotSupportedException(); } }

        public override long Position
        {
            get
            {
                return this.position;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public bool EndOfMessage { get; private set; }

        public WebSocketMessageType MessageType { get; private set; }

        public override int ReadTimeout { get; set; }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.EndOfMessage)
            {
                // avoid extra allocations
                return 0;
            }

            return this.ReadAsync(buffer, offset, count).ConfigureAwait(false).GetAwaiter().GetResult();
        }

#if NETFRAMEWORK
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.ReadAsync(buffer, offset, count).ToAsyncResult(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskEx.EndAsyncResult<int>(asyncResult);
        }
#endif // NETFRAMEWORK

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancelToken)
        {
            if (this.EndOfMessage)
            {
                return 0;
            }

            using (var timeoutCts = new CancellationTokenSource(this.ReadTimeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, timeoutCts.Token))
            {
                var receiveResult = await this.webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, count), linkedCts.Token).ConfigureAwait(false);
                this.MessageType = receiveResult.MessageType;
                if (receiveResult.EndOfMessage || receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    this.EndOfMessage = true;
                }

                this.position += receiveResult.Count;
                return receiveResult.Count;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
