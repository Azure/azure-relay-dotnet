// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class RunTimeTests : HybridConnectionTestBase
    {
        public RunTimeTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task UnauthenticatedHybridConnection()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("UnauthenticatedHybridConnection test start");

                this.Logger.Log("Creating a listener connection string for the unauthenticated Hybrid Connection");
                var listenerConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = UnauthenticatedEntityPath
                };

                this.Logger.Log("Creating a client connection string for the unauthenticated Hybrid Connection. Setting the keys to string.Empty");

                var clientConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = UnauthenticatedEntityPath,
                    SharedAccessKey = string.Empty,
                    SharedAccessKeyName = string.Empty,
                    SharedAccessSignature = string.Empty,
                };

                Assert.Equal(string.Empty, clientConnectionStringBuilder.SharedAccessKey);
                Assert.Equal(string.Empty, clientConnectionStringBuilder.SharedAccessKeyName);
                Assert.Equal(string.Empty, clientConnectionStringBuilder.SharedAccessSignature);

                listener = new HybridConnectionListener(listenerConnectionStringBuilder.ToString());
                var client = new HybridConnectionClient(clientConnectionStringBuilder.ToString());

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();
                this.Logger.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Calling clientStream.CloseAsync");
                var clientStreamCloseTask = clientStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                this.Logger.Log("Reading from listenerStream");
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling listenerStream.CloseAsync");
                var listenerStreamCloseTask = listenerStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                await listenerStreamCloseTask;
                this.Logger.Log("Calling listenerStream.CloseAsync completed");
                await clientStreamCloseTask;
                this.Logger.Log("Calling clientStream.CloseAsync completed");

                this.Logger.Log("Closing " + listener.GetType().Name);
                await listener.CloseAsync(TimeSpan.FromSeconds(10));

                this.Logger.Log("UnauthenticatedHybridConnection test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task AuthenticatedHybridConnection()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("AuthenticatedHybridConnection test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();
                this.Logger.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Calling clientStream.CloseAsync");
                var clientStreamCloseTask = clientStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                this.Logger.Log("Reading from listenerStream");
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling listenerStream.CloseAsync");
                var listenerStreamCloseTask = listenerStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                await listenerStreamCloseTask;
                this.Logger.Log("Calling listenerStream.CloseAsync completed");
                await clientStreamCloseTask;
                this.Logger.Log("Calling clientStream.CloseAsync completed");

                this.Logger.Log("Closing " + listener.GetType().Name);
                await listener.CloseAsync(TimeSpan.FromSeconds(10));

                this.Logger.Log("AuthenticatedHybridConnection test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task ClientShutdown()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("ClientShutdown test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                this.Logger.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Calling clientStream.Shutdown");
                clientStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling listenerStream.Shutdown and Dispose");
                listenerStream.Shutdown();
                listenerStream.Dispose();
                bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"clientStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling clientStream.Dispose");
                clientStream.Dispose();

                this.Logger.Log("Closing " + listener.GetType().Name);
                await listener.CloseAsync(TimeSpan.FromSeconds(10));

                this.Logger.Log("ClientShutdown test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task ConcurrentClients()
        {
            HybridConnectionListener listener = null;
            try
            {
                const int ClientCount = 100;

                this.Logger.Log("ConcurrentClients test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(10));

                this.Logger.Log($"Opening {ClientCount} connections quickly");

                var createConnectionTasks = new List<Task<HybridConnectionStream>>();
                for (var i = 0; i < ClientCount; i++)
                {
                    createConnectionTasks.Add(client.CreateConnectionAsync());
                }

                var senderTasks = new List<Task>();
                for (var i = 0; i < ClientCount; i++)
                {
                    this.AcceptEchoListener(listener);
                    senderTasks.Add(this.RunEchoClientAsync(await createConnectionTasks[i], i + 1));
                }

                await Task.WhenAll(senderTasks);

                this.Logger.Log("Closing " + listener.GetType().Name);
                await listener.CloseAsync(TimeSpan.FromSeconds(10));

                this.Logger.Log("ConcurrentClients test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task Write1Mb()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("Write1Mb test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                this.Logger.Log("Sending 1MB from client->listener");
                byte[] sendBuffer = this.CreateBuffer(1024 * 1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Sending 1MB from listener->client");
                await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log("listenerStream wrote {sendBuffer.Length} bytes");

                await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Calling clientStream.Shutdown");
                clientStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling listenerStream.Dispose");
                listenerStream.Dispose();

                this.Logger.Log("Calling clientStream.Dispose");
                clientStream.Dispose();

                this.Logger.Log("Write1Mb test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task ListenerShutdown()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("ListenerShutdown test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                this.Logger.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(2 * 1024, new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 });
                await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                this.Logger.Log("listenerStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.Logger.Log("Calling listenerStream.Shutdown");
                listenerStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"clientStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling clientStream.Shutdown and Dispose");
                clientStream.Shutdown();
                clientStream.Dispose();
                bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.Logger.Log("Calling listenerStream.Dispose");
                listenerStream.Dispose();

                this.Logger.Log("ListenerShutdown test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task ListenerAbortWhileClientReading()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("ListenerAbortWhileClientReading test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                this.Logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                this.Logger.Log("Client and Listener HybridStreams are connected!");

                using (var cancelSource = new CancellationTokenSource())
                {
                    this.Logger.Log("Aborting listener WebSocket");
                    cancelSource.Cancel();
                    await listenerStream.CloseAsync(cancelSource.Token);
                }

                byte[] readBuffer = new byte[1024];
                await Assert.ThrowsAsync<RelayException>(() => clientStream.ReadAsync(readBuffer, 0, readBuffer.Length));

                this.Logger.Log("Calling clientStream.Close");
                var clientCloseTask = clientStream.CloseAsync(CancellationToken.None);

                this.Logger.Log("ListenerAbortWhileClientReading test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task NonExistantNamespace()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("NonExistantNamespace test start");

                this.Logger.Log("Setting ConnectionStringBuilder.Endpoint to 'sb://fakeendpoint.com'");

                var fakeEndpointConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    Endpoint = new Uri("sb://fakeendpoint.com")
                };
                var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

                listener = new HybridConnectionListener(fakeEndpointConnectionString);
                var client = new HybridConnectionClient(fakeEndpointConnectionString);

                await Assert.ThrowsAsync<RelayException>(() => listener.OpenAsync(TimeSpan.FromSeconds(30)));
                await Assert.ThrowsAsync<RelayException>(() => client.CreateConnectionAsync());

                this.Logger.Log("NonExistantNamespace test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task NonExistantHCEntity()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("NonExistantHybridConnection test start");

                this.Logger.Log("Setting ConnectionStringBuilder.EntityPath to a new GUID");
                var fakeEndpointConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = Guid.NewGuid().ToString()
                };
                var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

                listener = new HybridConnectionListener(fakeEndpointConnectionString);
                var client = new HybridConnectionClient(fakeEndpointConnectionString);

                await Assert.ThrowsAsync<EndpointNotFoundException>(() => listener.OpenAsync());
                await Assert.ThrowsAsync<EndpointNotFoundException>(() => client.CreateConnectionAsync());

                this.Logger.Log("NonExistantHybridConnection test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task ListenerShutdownWithPendingAccepts()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("ListenerShutdownWithPendingAccepts test start");

                listener = new HybridConnectionListener(this.ConnectionString);
                var client = new HybridConnectionClient(this.ConnectionString);

                await listener.OpenAsync(TimeSpan.FromSeconds(20));
                this.Logger.Log("Calling HybridConnectionListener.Open");

                var acceptTasks = new List<Task<HybridConnectionStream>>(600);
                this.Logger.Log($"Calling listener.AcceptConnectionAsync() {acceptTasks.Capacity} times");
                for (int i = 0; i < acceptTasks.Capacity; i++)
                {
                    acceptTasks.Add(listener.AcceptConnectionAsync());
                    Assert.False(acceptTasks[i].IsCompleted);
                }

                this.Logger.Log("Calling HybridConnectionListener.Close");
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
                for (int i = 0; i < acceptTasks.Count; i++)
                {
                    Assert.True(acceptTasks[i].Wait(TimeSpan.FromSeconds(5)));
                    Assert.Null(acceptTasks[i].Result);
                }

                this.Logger.Log("ListenerShutdownWithPendingAccepts test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        [Fact]
        public async Task RawWebSocketSender()
        {
            HybridConnectionListener listener = null;
            try
            {
                this.Logger.Log("SubProtocol test start");

                listener = new HybridConnectionListener(this.ConnectionString);

                var connectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString);

                var clientWebSocket = new ClientWebSocket();

                var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                    connectionStringBuilder.SharedAccessKeyName,
                    connectionStringBuilder.SharedAccessKey);

                var token = await tokenProvider.GetTokenAsync(connectionStringBuilder.Endpoint.ToString(), TimeSpan.FromMinutes(10));

                var wssUri = new Uri(string.Format(
                    "wss://{0}/$hc/{1}?sb-hc-action={2}&sb-hc-token={3}",
                    connectionStringBuilder.Endpoint.Host,
                    connectionStringBuilder.EntityPath,
                    "connect",
                    WebUtility.UrlEncode(token.TokenString)));

                using (var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    this.Logger.Log("Calling HybridConnectionListener.Open");
                    await listener.OpenAsync(cancelSource.Token);

                    await clientWebSocket.ConnectAsync(wssUri, cancelSource.Token);

                    var listenerStream = await listener.AcceptConnectionAsync();

                    this.Logger.Log("Client and Listener are connected!");
                    Assert.Null(clientWebSocket.SubProtocol);

                    byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    await clientWebSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Binary, true, cancelSource.Token);
                    this.Logger.Log($"clientWebSocket wrote {sendBuffer.Length} bytes");

                    byte[] readBuffer = new byte[sendBuffer.Length];
                    await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                    Assert.Equal(sendBuffer, readBuffer);

                    this.Logger.Log("Calling clientStream.CloseAsync");
                    var clientStreamCloseTask = clientWebSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "From Test Code", cancelSource.Token);
                    this.Logger.Log("Reading from listenerStream");
                    int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                    this.Logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                    Assert.Equal(0, bytesRead);

                    this.Logger.Log("Calling listenerStream.CloseAsync");
                    var listenerStreamCloseTask = listenerStream.CloseAsync(cancelSource.Token);
                    await listenerStreamCloseTask;
                    this.Logger.Log("Calling listenerStream.CloseAsync completed");
                    await clientStreamCloseTask;
                    this.Logger.Log("Calling clientStream.CloseAsync completed");

                    this.Logger.Log("Closing " + listener.GetType().Name);
                    await listener.CloseAsync(TimeSpan.FromSeconds(10));
                }

                this.Logger.Log("SubProtocol test end");
            }
            finally
            {
                await this.SafeCloseHybridConnectionListenerAsync(listener);
            }
        }

        /// <summary>
        /// Create an send-side HybridConnectionStream, send N bytes to it, receive N bytes response from it,
        /// then close the HybridConnectionStream.
        /// </summary>
        private async Task RunEchoClientAsync(HybridConnectionStream clientStream, int byteCount)
        {
            var cancelSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                byte[] sendBuffer = this.CreateBuffer(byteCount, new[] { (byte)(byteCount % byte.MaxValue) });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length, cancelSource.Token);

                byte[] readBuffer = new byte[sendBuffer.Length + 10];
                int bytesRead = await clientStream.ReadAsync(readBuffer, 0, readBuffer.Length, cancelSource.Token);
                Assert.Equal(sendBuffer.Length, bytesRead);

                await clientStream.CloseAsync(cancelSource.Token);
            }
            catch (Exception e)
            {
                this.Logger.Log($"[byteCount={byteCount}] {e.GetType().Name}: {e.Message}");
                await clientStream.CloseAsync(cancelSource.Token);
                throw;
            }
            finally
            {
                cancelSource.Dispose();
            }
        }

        /// <summary>
        /// Call HybridConnectionListener.AcceptConnectionAsync, once/if a listener is accepted
        /// read from its stream and echo the bytes until a 0-byte read occurs, then close.
        /// </summary>
        private async void AcceptEchoListener(HybridConnectionListener listener)
        {
            try
            {
                var listenerStream = await listener.AcceptConnectionAsync();
                if (listenerStream != null)
                {
                    byte[] buffer = new byte[4 * 1024];
                    do
                    {
                        int bytesRead;
                        try
                        {
                            bytesRead = await listenerStream.ReadAsync(buffer, 0, buffer.Length);
                        }
                        catch (Exception readException)
                        {
                            this.Logger.Log($"AcceptEchoListener {readException.GetType().Name}: {readException.Message}");
                            await listener.CloseAsync(new TimeSpan(0, 0, 10));
                            return;
                        }

                        if (bytesRead == 0)
                        {
                            await listener.CloseAsync(new TimeSpan(0, 0, 10));
                            return;
                        }
                        else
                        {
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                            {
                                await listenerStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                            }
                        }
                    }
                    while (true);
                }
            }
            catch (Exception e)
            {
                this.Logger.Log($"AcceptEchoListener {e.GetType().Name}: {e.Message}");
            }
        }

        private async Task ReadCountBytesAsync(Stream stream, byte[] buffer, int offset, int bytesToRead, TimeSpan timeout)
        {
            DateTime timeoutInstant = DateTime.Now.Add(timeout);
            int? originalReadTimeout = stream.CanTimeout ? stream.ReadTimeout : (int?)null;
            try
            {
                int totalBytesRead = 0;
                do
                {
                    TimeSpan remainingTimeout = timeoutInstant.Subtract(DateTime.Now);
                    if (remainingTimeout <= TimeSpan.Zero)
                    {
                        break;
                    }

                    stream.ReadTimeout = (int)remainingTimeout.TotalMilliseconds;
                    int bytesRead = await stream.ReadAsync(buffer, offset + totalBytesRead, bytesToRead - totalBytesRead);
                    this.Logger.Log($"Stream read {bytesRead} bytes");
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    totalBytesRead += bytesRead;
                }
                while (totalBytesRead < bytesToRead);

                if (totalBytesRead < bytesToRead)
                {
                    throw new TimeoutException("The requested number of bytes could not be received.  ReadAsync returned 0 bytes");
                }
            }
            finally
            {
                if (originalReadTimeout.HasValue)
                {
                    stream.ReadTimeout = originalReadTimeout.Value;
                }
            }
        }

        /// <summary>
        /// Calls Stream.ReadAsync with Exception handling. If an IOException occurs a zero byte read is returned.
        /// </summary>
        private async Task<int> SafeReadAsync(Stream stream, byte[] buffer, int offset, int bytesToRead)
        {
            int bytesRead = 0;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, offset, bytesToRead);
                this.Logger.Log($"Stream read {bytesRead} bytes");
            }
            catch (IOException ex)
            {
                this.Logger.Log($"Stream.ReadAsync error {ex}");
            }

            return bytesRead;
        }

        private byte[] CreateBuffer(int length, byte[] fillPattern)
        {
            byte[] buffer = new byte[length];

            int offset = 0;
            do
            {
                int bytesToCopy = Math.Min(length - offset, fillPattern.Length);
                Array.Copy(fillPattern, 0, buffer, offset, bytesToCopy);
                offset += bytesToCopy;
            }
            while (offset < length);

            return buffer;
        }

        async Task SafeCloseHybridConnectionListenerAsync(HybridConnectionListener listener)
        {
            if (listener != null)
            {
                try
                {
                    Logger.Log("Closing HybridConnectionListener");
                    await listener.CloseAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception e)
                {
                    Logger.Log($"Error closing HybridConnectionListener {e.GetType()}: {e.Message}");
                }
            }
        }
    }
}