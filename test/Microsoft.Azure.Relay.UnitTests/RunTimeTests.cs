//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

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

    public class RunTimeTests
    {
        private Logger logger;
        private RelayConnectionStringBuilder connectionStringBuilder;

        public RunTimeTests(ITestOutputHelper output)
        {
            this.logger = new Logger(output);

            var connectionString = Environment.GetEnvironmentVariable("RELAYCONNECTIONSTRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("RELAYCONNECTIONSTRING environment variable was not found!");
            }

            this.connectionStringBuilder = new RelayConnectionStringBuilder(connectionString)
            {
                // Unless explicitly stated, run all tests against the authenticated Hybrid Connection
                EntityPath = "authenticated",
                OperationTimeout = TimeSpan.FromSeconds(15)
            };
        }

        [Fact]
        public async Task UnauthenticatedHybridConnection()
        {
            this.logger.Log("Creating a listener connection string for the unauthenticated Hybrid Connection");
            var listenerConnectionStringBuilder = this.connectionStringBuilder;
            listenerConnectionStringBuilder.EntityPath = "unauthenticated";
            var listenerConnectionString = listenerConnectionStringBuilder.ToString();

            this.logger.Log("Creating a client connection string for the unauthenticated Hybrid Connection. Setting the keys to string.Empty");
            var clientConnectionStringBuilder = this.connectionStringBuilder;
            clientConnectionStringBuilder.EntityPath = "unauthenticated";
            clientConnectionStringBuilder.SharedAccessKey = string.Empty;
            clientConnectionStringBuilder.SharedAccessKeyName = string.Empty;
            clientConnectionStringBuilder.SharedAccessSignature = string.Empty;
            var clientConnectionString = clientConnectionStringBuilder.ToString();

            var listener = new HybridConnectionListener(listenerConnectionString);
            var client = new HybridConnectionClient(clientConnectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();
            this.logger.Log("Client and Listener HybridStreams are connected!");

            byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

            byte[] readBuffer = new byte[sendBuffer.Length];
            await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Calling clientStream.CloseAsync");
            var clientStreamCloseTask = clientStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            this.logger.Log("Reading from listenerStream");
            int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling listenerStream.CloseAsync");
            var listenerStreamCloseTask = listenerStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            await listenerStreamCloseTask;
            this.logger.Log("Calling listenerStream.CloseAsync completed");
            await clientStreamCloseTask;
            this.logger.Log("Calling clientStream.CloseAsync completed");

            this.logger.Log("Closing " + listener.GetType().Name);
            await listener.CloseAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task AuthenticatedHybridConnection()
        {
            var connectionString = this.connectionStringBuilder.ToString();

            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();
            this.logger.Log("Client and Listener HybridStreams are connected!");

            byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

            byte[] readBuffer = new byte[sendBuffer.Length];
            await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Calling clientStream.CloseAsync");
            var clientStreamCloseTask = clientStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            this.logger.Log("Reading from listenerStream");
            int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling listenerStream.CloseAsync");
            var listenerStreamCloseTask = listenerStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
            await listenerStreamCloseTask;
            this.logger.Log("Calling listenerStream.CloseAsync completed");
            await clientStreamCloseTask;
            this.logger.Log("Calling clientStream.CloseAsync completed");

            this.logger.Log("Closing " + listener.GetType().Name);
            await listener.CloseAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task ClientShutdown()
        {
            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();

            this.logger.Log("Client and Listener HybridStreams are connected!");

            byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

            byte[] readBuffer = new byte[sendBuffer.Length];
            await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Calling clientStream.Shutdown");
            clientStream.Shutdown();
            int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling listenerStream.Shutdown and Close");
            listenerStream.Shutdown();
            listenerStream.Close();
            bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"clientStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling clientStream.Close");
            clientStream.Close();

            this.logger.Log("Closing " + listener.GetType().Name);
            await listener.CloseAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task ConcurrentClients()
        {
            const int ClientCount = 100;

            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log($"Opening {listener}");
            await listener.OpenAsync(TimeSpan.FromSeconds(60));

            this.logger.Log($"Opening {ClientCount} connections quickly");

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

            this.logger.Log($"Closing {listener}");
            await listener.CloseAsync(TimeSpan.FromSeconds(10));
        }

        [Fact]
        public async Task Write1Mb()
        {
            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();

            this.logger.Log("Sending 1MB from client->listener");
            byte[] sendBuffer = this.CreateBuffer(1024 * 1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log($"clientStream wrote {sendBuffer.Length} bytes");

            byte[] readBuffer = new byte[sendBuffer.Length];
            await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Sending 1MB from listener->client");
            await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log("listenerStream wrote {sendBuffer.Length} bytes");

            await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Calling clientStream.Shutdown");
            clientStream.Shutdown();
            int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling listenerStream.Close");
            listenerStream.Close();

            this.logger.Log("Calling clientStream.Close");
            clientStream.Close();
        }

        [Fact]
        public async Task ListenerShutdown()
        {
            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();

            this.logger.Log("Client and Listener HybridStreams are connected!");

            byte[] sendBuffer = this.CreateBuffer(2 * 1024, new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 });
            await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
            this.logger.Log("listenerStream wrote {sendBuffer.Length} bytes");

            byte[] readBuffer = new byte[sendBuffer.Length];
            await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
            Assert.Equal(sendBuffer, readBuffer);

            this.logger.Log("Calling listenerStream.Shutdown");
            listenerStream.Shutdown();
            int bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"clientStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling clientStream.Shutdown and Close");
            clientStream.Shutdown();
            clientStream.Close();
            bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
            this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
            Assert.Equal(0, bytesRead);

            this.logger.Log("Calling listenerStream.Close");
            listenerStream.Close();
        }

        [Fact]
        public async Task ListenerAbortWhileClientReading()
        {
            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            this.logger.Log("Calling HybridConnectionListener.Open");
            await listener.OpenAsync(TimeSpan.FromSeconds(30));

            var clientStream = await client.CreateConnectionAsync();
            var listenerStream = await listener.AcceptConnectionAsync();

            this.logger.Log("Client and Listener HybridStreams are connected!");

            using (var cancelSource = new CancellationTokenSource())
            {
                this.logger.Log("Aborting listener WebSocket");
                cancelSource.Cancel();
                await listenerStream.CloseAsync(cancelSource.Token);
            }

            byte[] readBuffer = new byte[1024];
            await Assert.ThrowsAsync<RelayException>(() => clientStream.ReadAsync(readBuffer, 0, readBuffer.Length));

            this.logger.Log("Calling clientStream.Close");
            var clientCloseTask = clientStream.CloseAsync(CancellationToken.None);
        }

        [Fact]
        public async Task NonExistantNamespace()
        {
            this.logger.Log("Setting ConnectionStringBuilder.Endpoint to 'sb://fakeendpoint.com'");
            var fakeEndpointConnectionStringBuilder = this.connectionStringBuilder;
            fakeEndpointConnectionStringBuilder.Endpoint = new Uri("sb://fakeendpoint.com");
            var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

            var listener = new HybridConnectionListener(fakeEndpointConnectionString);
            var client = new HybridConnectionClient(fakeEndpointConnectionString);

            await Assert.ThrowsAsync<RelayException>(() => listener.OpenAsync(TimeSpan.FromSeconds(30)));
            await Assert.ThrowsAsync<RelayException>(() => client.CreateConnectionAsync());
        }

        [Fact]
        public async Task NonExistantHybridConnection()
        {
            this.logger.Log("Setting ConnectionStringBuilder.EntityPath to a new GUID");
            var fakeEndpointConnectionStringBuilder = this.connectionStringBuilder;
            fakeEndpointConnectionStringBuilder.EntityPath = Guid.NewGuid().ToString();
            var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

            var listener = new HybridConnectionListener(fakeEndpointConnectionString);
            var client = new HybridConnectionClient(fakeEndpointConnectionString);

            await Assert.ThrowsAsync<EndpointNotFoundException>(() => listener.OpenAsync());
            await Assert.ThrowsAsync<EndpointNotFoundException>(() => client.CreateConnectionAsync());
        }

        [Fact]
        public void ClientParameterValidation()
        {
            string connectionString = "Endpoint=sb://whatever.servicebus.windows.net/";
            string connectionStringWithEntityPath = "Endpoint=sb://whatever.servicebus.windows.net/;EntityPath=" + this.connectionStringBuilder.EntityPath;
            string connectionStringWithSASKeyValueOnly = "Endpoint=sb://whatever.servicebus.windows.net/;SharedAccessKey=" + this.connectionStringBuilder.SharedAccessKey;

            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient((string)null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionString));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(connectionString, null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionClient(connectionString, string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionStringWithEntityPath, this.connectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionClient(connectionStringWithSASKeyValueOnly, this.connectionStringBuilder.EntityPath));
        }

        [Fact]
        public void ListenerParameterValidation()
        {
            string connectionString = "Endpoint=sb://whatever.servicebus.windows.net/";
            string connectionStringWithEntityPath = "Endpoint=sb://whatever.servicebus.windows.net/;EntityPath=" + this.connectionStringBuilder.EntityPath;
            string connectionStringWithSASKeyValueOnly = "Endpoint=sb://whatever.servicebus.windows.net/;SharedAccessKey=" + this.connectionStringBuilder.SharedAccessKey;

            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener((string)null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionString));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(connectionString, null));
            Assert.Throws<ArgumentNullException>(() => new HybridConnectionListener(connectionString, string.Empty));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionString, this.connectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionStringWithEntityPath, this.connectionStringBuilder.EntityPath));
            Assert.Throws<ArgumentException>(() => new HybridConnectionListener(connectionStringWithSASKeyValueOnly, this.connectionStringBuilder.EntityPath));
        }

        [Fact]
        public async Task ListenerShutdownWithPendingAccepts()
        {
            var connectionString = this.connectionStringBuilder.ToString();
            var listener = new HybridConnectionListener(connectionString);
            var client = new HybridConnectionClient(connectionString);

            await listener.OpenAsync(TimeSpan.FromSeconds(20));
            this.logger.Log("Calling HybridConnectionListener.Open");

            var acceptTasks = new List<Task<HybridConnectionStream>>(600);
            this.logger.Log($"Calling listener.AcceptConnectionAsync() {acceptTasks.Capacity} times");
            for (int i = 0; i < acceptTasks.Capacity; i++)
            {
                acceptTasks.Add(listener.AcceptConnectionAsync());
                Assert.False(acceptTasks[i].IsCompleted);
            }

            this.logger.Log("Calling HybridConnectionListener.Close");
            await listener.CloseAsync(TimeSpan.FromSeconds(10));
            for (int i = 0; i < acceptTasks.Count; i++)
            {
                Assert.True(acceptTasks[i].Wait(TimeSpan.FromSeconds(5)));
                Assert.Null(acceptTasks[i].Result);
            }
        }

        [Fact]
        public async Task SubProtocol()
        {
            var listener = new HybridConnectionListener(this.connectionStringBuilder.ToString());

            var clientWebSocket = new ClientWebSocket();
            string subProtocol1 = "wshybridconnection";
            string subProtocol2 = "somethingelsehere";
            clientWebSocket.Options.AddSubProtocol(subProtocol1);
            clientWebSocket.Options.AddSubProtocol(subProtocol2);

            var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                this.connectionStringBuilder.SharedAccessKeyName,
                this.connectionStringBuilder.SharedAccessKey);

            var token = await tokenProvider.GetTokenAsync(this.connectionStringBuilder.Endpoint.ToString(), TimeSpan.FromMinutes(10));

            var wssUri = new Uri(string.Format(
                "wss://{0}/$hc/{1}?sb-hc-action={2}&sb-hc-token={3}",
                this.connectionStringBuilder.Endpoint.Host,
                this.connectionStringBuilder.EntityPath,
                "connect",
                WebUtility.UrlEncode(token.TokenString)));

            using (var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
            {
                this.logger.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(cancelSource.Token);
                
                await clientWebSocket.ConnectAsync(wssUri, cancelSource.Token);

                var listenerStream = await listener.AcceptConnectionAsync();

                this.logger.Log("Client and Listener are connected!");
                Assert.Null(clientWebSocket.SubProtocol);

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientWebSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Binary, true, cancelSource.Token);
                this.logger.Log($"clientWebSocket wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                this.logger.Log("Calling clientStream.CloseAsync");
                var clientStreamCloseTask = clientWebSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "From Test Code", cancelSource.Token);
                this.logger.Log("Reading from listenerStream");
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                this.logger.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                this.logger.Log("Calling listenerStream.CloseAsync");
                var listenerStreamCloseTask = listenerStream.CloseAsync(cancelSource.Token);
                await listenerStreamCloseTask;
                this.logger.Log("Calling listenerStream.CloseAsync completed");
                await clientStreamCloseTask;
                this.logger.Log("Calling clientStream.CloseAsync completed");

                this.logger.Log("Closing " + listener.GetType().Name);
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
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
                this.logger.Log($"[byteCount={byteCount}] {e.GetType().Name}: {e.Message}");
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
                            this.logger.Log($"AcceptEchoListener {readException.GetType().Name}: {readException.Message}");
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
                this.logger.Log($"AcceptEchoListener {e.GetType().Name}: {e.Message}");
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
                    this.logger.Log($"Stream read {bytesRead} bytes");
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
                this.logger.Log($"Stream read {bytesRead} bytes");
            }
            catch (IOException ex)
            {
                this.logger.Log($"Stream.ReadAsync error {ex}");
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
    }
}