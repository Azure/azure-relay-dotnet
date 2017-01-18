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

    public class RunTimeTests
    {
        const string ConnectionStringEnvironmentVariable = "azure-relay-dotnet/connectionstring";
        const string AuthenticatedEntityPath = "authenticated";
        const string UnauthenticatedEntityPath = "unauthenticated";

        const string TestAcceptHandlerResultHeader = "X-TestAcceptHandlerResult";
        const string TestAcceptHandlerStatusCodeHeader = "X-TestAcceptHandlerStatusCode";
        const string TestAcceptHandlerStatusDescriptionHeader = "X-TestAcceptHandlerStatusDescription";
        const string TestAcceptHandlerSetResponseHeader = "X-TestAcceptHandlerSetResponseHeader";
        const string TestAcceptHandlerDelayHeader = "X-TestAcceptHandlerDelay";

        readonly string ConnectionString;

        enum EndpointTestType
        {
            Authenticated,
            Unauthenticated
        }

        public static IEnumerable<object> AuthenticationTestPermutations => new object[]
        {
            new object[] { EndpointTestType.Authenticated },
            new object[] { EndpointTestType.Unauthenticated }
        };

        public RunTimeTests()
        {
            var envConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(envConnectionString))
            {
                throw new InvalidOperationException($"'{ConnectionStringEnvironmentVariable}' environment variable was not found!");
            }

            // Validate the connection string
            ConnectionString = new RelayConnectionStringBuilder(envConnectionString).ToString();
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task HybridConnectionTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();
                TestUtility.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                TestUtility.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                TestUtility.Log("Calling clientStream.CloseAsync");
                var clientStreamCloseTask = clientStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                TestUtility.Log("Reading from listenerStream");
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling listenerStream.CloseAsync");
                var listenerStreamCloseTask = listenerStream.CloseAsync(new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);
                await listenerStreamCloseTask;
                TestUtility.Log("Calling listenerStream.CloseAsync completed");
                await clientStreamCloseTask;
                TestUtility.Log("Calling clientStream.CloseAsync completed");

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
                listener = null;
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ClientShutdownTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                TestUtility.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                TestUtility.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                TestUtility.Log("Calling clientStream.Shutdown");
                clientStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling listenerStream.Shutdown and Dispose");
                listenerStream.Shutdown();
                listenerStream.Dispose();
                bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"clientStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling clientStream.Dispose");
                clientStream.Dispose();

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
                listener = null;
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ConcurrentClientsTest(EndpointTestType endpointTestType)
        {
            const int ClientCount = 100;
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(10));

                TestUtility.Log($"Opening {ClientCount} connections quickly");

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

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
                listener = null;
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task WriteLargeDataSetTest(EndpointTestType endpointTestType, int kilobytesToSend = 1024)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                TestUtility.Log($"clientStream connected! {clientStream}");
                var listenerStream = await listener.AcceptConnectionAsync();

                TestUtility.Log("Sending 1MB from client->listener");
                byte[] sendBuffer = this.CreateBuffer(kilobytesToSend * 1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                await clientStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                TestUtility.Log($"clientStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                TestUtility.Log("Sending 1MB from listener->client");
                await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                TestUtility.Log($"listenerStream wrote {sendBuffer.Length} bytes");

                await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                TestUtility.Log("Calling clientStream.Shutdown");
                clientStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling listenerStream.Dispose");
                listenerStream.Dispose();

                TestUtility.Log("Calling clientStream.Dispose");
                clientStream.Dispose();
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ListenerShutdownTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                TestUtility.Log("Client and Listener HybridStreams are connected!");

                byte[] sendBuffer = this.CreateBuffer(2 * 1024, new byte[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 });
                await listenerStream.WriteAsync(sendBuffer, 0, sendBuffer.Length);
                TestUtility.Log($"listenerStream wrote {sendBuffer.Length} bytes");

                byte[] readBuffer = new byte[sendBuffer.Length];
                await this.ReadCountBytesAsync(clientStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                Assert.Equal(sendBuffer, readBuffer);

                TestUtility.Log("Calling listenerStream.Shutdown");
                listenerStream.Shutdown();
                int bytesRead = await this.SafeReadAsync(clientStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"clientStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling clientStream.Shutdown and Dispose");
                clientStream.Shutdown();
                clientStream.Dispose();
                bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                TestUtility.Log($"listenerStream.Read returned {bytesRead} bytes");
                Assert.Equal(0, bytesRead);

                TestUtility.Log("Calling listenerStream.Dispose");
                listenerStream.Dispose();
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ListenerAbortWhileClientReadingTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var clientStream = await client.CreateConnectionAsync();
                var listenerStream = await listener.AcceptConnectionAsync();

                TestUtility.Log("Client and Listener HybridStreams are connected!");

                using (var cancelSource = new CancellationTokenSource())
                {
                    TestUtility.Log("Aborting listener WebSocket");
                    cancelSource.Cancel();
                    await listenerStream.CloseAsync(cancelSource.Token);
                }

                byte[] readBuffer = new byte[1024];
                await Assert.ThrowsAsync<RelayException>(() => clientStream.ReadAsync(readBuffer, 0, readBuffer.Length));

                TestUtility.Log("Calling clientStream.Close");
                var clientCloseTask = clientStream.CloseAsync(CancellationToken.None);
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task NonExistantNamespaceTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                TestUtility.Log("Setting ConnectionStringBuilder.Endpoint to 'sb://fakeendpoint.com'");

                var fakeEndpointConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    Endpoint = new Uri("sb://fakeendpoint.com")
                };

                if (endpointTestType == EndpointTestType.Authenticated)
                {
                    fakeEndpointConnectionStringBuilder.EntityPath = AuthenticatedEntityPath;
                }
                else
                {
                    fakeEndpointConnectionStringBuilder.EntityPath = UnauthenticatedEntityPath;
                }

                var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

                listener = new HybridConnectionListener(fakeEndpointConnectionString);
                var client = new HybridConnectionClient(fakeEndpointConnectionString);

#if NET451
                await Assert.ThrowsAsync<EndpointNotFoundException>(() => listener.OpenAsync());
                await Assert.ThrowsAsync<EndpointNotFoundException>(() => client.CreateConnectionAsync());
#else
                await Assert.ThrowsAsync<RelayException>(() => listener.OpenAsync());
                await Assert.ThrowsAsync<RelayException>(() => client.CreateConnectionAsync());
#endif
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task NonExistantHCEntityTest()
        {
            HybridConnectionListener listener = null;
            try
            {
                TestUtility.Log("Setting ConnectionStringBuilder.EntityPath to a new GUID");
                var fakeEndpointConnectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = Guid.NewGuid().ToString()
                };
                var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

                listener = new HybridConnectionListener(fakeEndpointConnectionString);
                var client = new HybridConnectionClient(fakeEndpointConnectionString);
#if NET451
                await Assert.ThrowsAsync<EndpointNotFoundException>(() => listener.OpenAsync());
                await Assert.ThrowsAsync<EndpointNotFoundException>(() => client.CreateConnectionAsync());
#else
                await Assert.ThrowsAsync<RelayException>(() => listener.OpenAsync());
                await Assert.ThrowsAsync<RelayException>(() => client.CreateConnectionAsync());
#endif


            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task ListenerShutdownWithPendingAcceptsTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                var client = GetHybridConnectionClient(endpointTestType);

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(20));

                var acceptTasks = new List<Task<HybridConnectionStream>>(600);
                TestUtility.Log($"Calling listener.AcceptConnectionAsync() {acceptTasks.Capacity} times");
                for (int i = 0; i < acceptTasks.Capacity; i++)
                {
                    acceptTasks.Add(listener.AcceptConnectionAsync());
                    Assert.False(acceptTasks[i].IsCompleted);
                }

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(TimeSpan.FromSeconds(10));
                listener = null;
                for (int i = 0; i < acceptTasks.Count; i++)
                {
                    Assert.True(acceptTasks[i].Wait(TimeSpan.FromSeconds(5)));
                    Assert.Null(acceptTasks[i].Result);
                }
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task RawWebSocketSenderTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);

                var clientWebSocket = new ClientWebSocket();
                var wssUri = await GetWebSocketConnectionUri(endpointTestType);

                using (var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    TestUtility.Log($"Opening {listener}");
                    await listener.OpenAsync(cancelSource.Token);

                    await clientWebSocket.ConnectAsync(wssUri, cancelSource.Token);

                    var listenerStream = await listener.AcceptConnectionAsync();

                    TestUtility.Log("Client and Listener are connected!");
                    Assert.Null(clientWebSocket.SubProtocol);

                    byte[] sendBuffer = this.CreateBuffer(1024, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });
                    await clientWebSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Binary, true, cancelSource.Token);
                    TestUtility.Log($"clientWebSocket wrote {sendBuffer.Length} bytes");

                    byte[] readBuffer = new byte[sendBuffer.Length];
                    await this.ReadCountBytesAsync(listenerStream, readBuffer, 0, readBuffer.Length, TimeSpan.FromSeconds(30));
                    Assert.Equal(sendBuffer, readBuffer);

                    TestUtility.Log("Calling clientStream.CloseAsync");
                    var clientStreamCloseTask = clientWebSocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "From Test Code", cancelSource.Token);
                    TestUtility.Log("Reading from listenerStream");
                    int bytesRead = await this.SafeReadAsync(listenerStream, readBuffer, 0, readBuffer.Length);
                    TestUtility.Log($"listenerStream.Read returned {bytesRead} bytes");
                    Assert.Equal(0, bytesRead);

                    TestUtility.Log("Calling listenerStream.CloseAsync");
                    var listenerStreamCloseTask = listenerStream.CloseAsync(cancelSource.Token);
                    await listenerStreamCloseTask;
                    TestUtility.Log("Calling listenerStream.CloseAsync completed");
                    await clientStreamCloseTask;
                    TestUtility.Log("Calling clientStream.CloseAsync completed");

                    TestUtility.Log($"Closing {listener}");
                    await listener.CloseAsync(TimeSpan.FromSeconds(10));
                    listener = null;
                }
            }
            finally
            {
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task AcceptHandlerTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                // Install a Custom AcceptHandler which allows using Headers to control the StatusCode, StatusDescription,
                // and whether to accept or reject the client.
                listener.AcceptHandler = TestAcceptHandler;

                var clientConnectionString = GetConnectionString(endpointTestType);

                var connectionStringBuilder = new RelayConnectionStringBuilder(clientConnectionString);

                var wssUri = await GetWebSocketConnectionUri(endpointTestType);
                TestUtility.Log($"Using WebSocket address {wssUri}");
                using (var cancelSource = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler accepting with only returning true");
                    AcceptEchoListener(listener);
                    var clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerResultHeader, true.ToString());
                    await clientWebSocket.ConnectAsync(wssUri, cancelSource.Token);
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test closing web socket", cancelSource.Token);

                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler accepting and setting response header");
                    AcceptEchoListener(listener);
                    clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerResultHeader, true.ToString());
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerSetResponseHeader, "X-CustomHeader: Test value");
                    await clientWebSocket.ConnectAsync(wssUri, cancelSource.Token);
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test closing web socket", cancelSource.Token);

                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler rejecting with only returning false");
                    AcceptEchoListener(listener);
                    HttpStatusCode expectedStatusCode = HttpStatusCode.BadRequest;
                    string expectedStatusDescription = "Rejected by user code";
                    clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerResultHeader, false.ToString());
                    var webSocketException = await Assert.ThrowsAsync<WebSocketException>(() => clientWebSocket.ConnectAsync(wssUri, cancelSource.Token));
                    VerifyHttpStatusCodeAndDescription(webSocketException, expectedStatusCode, expectedStatusDescription);

                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler rejecting with setting status code and returning false");
                    AcceptEchoListener(listener);
                    expectedStatusCode = HttpStatusCode.Unauthorized;
                    expectedStatusDescription = "Unauthorized";
                    clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerResultHeader, false.ToString());
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerStatusCodeHeader, ((int)expectedStatusCode).ToString());
                    webSocketException = await Assert.ThrowsAsync<WebSocketException>(() => clientWebSocket.ConnectAsync(wssUri, cancelSource.Token));
                    VerifyHttpStatusCodeAndDescription(webSocketException, expectedStatusCode, expectedStatusDescription);

                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler rejecting with setting status code+description and returning false");
                    AcceptEchoListener(listener);
                    expectedStatusCode = HttpStatusCode.Unauthorized;
                    expectedStatusDescription = "Status Description from test";
                    clientWebSocket = new ClientWebSocket();
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerResultHeader, false.ToString());
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerStatusCodeHeader, ((int)expectedStatusCode).ToString());
                    clientWebSocket.Options.SetRequestHeader(TestAcceptHandlerStatusDescriptionHeader, expectedStatusDescription);
                    webSocketException = await Assert.ThrowsAsync<WebSocketException>(() => clientWebSocket.ConnectAsync(wssUri, cancelSource.Token));
                    VerifyHttpStatusCodeAndDescription(webSocketException, expectedStatusCode, expectedStatusDescription);

                    TestUtility.Log("Testing HybridConnectionListener.AcceptHandler with a custom handler that returns null instead of a valid Task<bool>");
                    listener.AcceptHandler = context => null;
                    AcceptEchoListener(listener);
                    expectedStatusCode = HttpStatusCode.BadGateway;
                    expectedStatusDescription = "The Listener's custom AcceptHandler threw an exception. See Listener logs for details.";
                    clientWebSocket = new ClientWebSocket();
                    webSocketException = await Assert.ThrowsAsync<WebSocketException>(() => clientWebSocket.ConnectAsync(wssUri, cancelSource.Token));
                    VerifyHttpStatusCodeAndDescription(webSocketException, expectedStatusCode, expectedStatusDescription, false);
                }
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        /// <summary>
        /// Since these tests all share a common connection string, this method will modify the 
        /// endpoint / shared access keys as needed based on the EndpointTestType.
        /// </summary>
        string GetConnectionString(EndpointTestType endpointTestType)
        {
            var connectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString);
            if (endpointTestType == EndpointTestType.Unauthenticated)
            {
                connectionStringBuilder.EntityPath = UnauthenticatedEntityPath;
                connectionStringBuilder.SharedAccessKey = string.Empty;
                connectionStringBuilder.SharedAccessKeyName = string.Empty;
                connectionStringBuilder.SharedAccessSignature = string.Empty;
            }
            else
            {
                connectionStringBuilder.EntityPath = AuthenticatedEntityPath;
            }

            return connectionStringBuilder.ToString();
        }

        /// <summary>
        /// Returns a HybridConnectionClient based on the EndpointTestType (authenticated/unauthenticated).
        /// </summary>
        HybridConnectionClient GetHybridConnectionClient(EndpointTestType endpointTestType)
        {
            var connectionString = GetConnectionString(endpointTestType);
            return new HybridConnectionClient(connectionString);
        }

        /// <summary>
        /// Returns a HybridConnectionListener based on the EndpointTestType (authenticated/unauthenticated).
        /// </summary>
        HybridConnectionListener GetHybridConnectionListener(EndpointTestType endpointTestType)
        {
            // Even if the endpoint is unauthenticated, the *listener* still needs to be authenticated
            if (endpointTestType == EndpointTestType.Unauthenticated)
            {
                var connectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = UnauthenticatedEntityPath
                };
                return new HybridConnectionListener(connectionStringBuilder.ToString());
            }

            return new HybridConnectionListener(GetConnectionString(endpointTestType));
        }

        /// <summary>
        /// Since these tests all share a common connection string, this method will modify the 
        /// endpoint / shared access keys as needed based on the EndpointTestType, and return a WebSocket URI.
        /// </summary>
        async Task<Uri> GetWebSocketConnectionUri(EndpointTestType endpointTestType)
        {
            var clientConnectionString = GetConnectionString(endpointTestType);
            var connectionStringBuilder = new RelayConnectionStringBuilder(clientConnectionString);
            var connectionUriString = $"wss://{connectionStringBuilder.Endpoint.Host}/$hc/{connectionStringBuilder.EntityPath}";

            if (endpointTestType == EndpointTestType.Authenticated)
            {
                var tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(
                    connectionStringBuilder.SharedAccessKeyName,
                    connectionStringBuilder.SharedAccessKey);

                var token = await tokenProvider.GetTokenAsync(connectionStringBuilder.Endpoint.ToString(), TimeSpan.FromMinutes(10));

                connectionUriString += $"?sb-hc-token={WebUtility.UrlEncode(token.TokenString)}";
            }

            return new Uri(connectionUriString);
        }

        /// <summary>
        /// Create an send-side HybridConnectionStream, send N bytes to it, receive N bytes response from it,
        /// then close the HybridConnectionStream.
        /// </summary>
        async Task RunEchoClientAsync(HybridConnectionStream clientStream, int byteCount)
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
                TestUtility.Log($"[byteCount={byteCount}] {e.GetType().Name}: {e.Message}");
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
        async void AcceptEchoListener(HybridConnectionListener listener)
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
                            TestUtility.Log($"AcceptEchoListener {readException.GetType().Name}: {readException.Message}");
                            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                            {
                                await listenerStream.CloseAsync(cts.Token);
                            }

                            return;
                        }

                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                        {
                            if (bytesRead == 0)
                            {
                                await listenerStream.CloseAsync(cts.Token);
                                return;
                            }
                            else
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
                TestUtility.Log($"AcceptEchoListener {e.GetType().Name}: {e.Message}");
            }
        }

        async Task ReadCountBytesAsync(Stream stream, byte[] buffer, int offset, int bytesToRead, TimeSpan timeout)
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
                    TestUtility.Log($"Stream read {bytesRead} bytes");
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
        async Task<int> SafeReadAsync(Stream stream, byte[] buffer, int offset, int bytesToRead)
        {
            int bytesRead = 0;
            try
            {
                bytesRead = await stream.ReadAsync(buffer, offset, bytesToRead);
                TestUtility.Log($"Stream read {bytesRead} bytes");
            }
            catch (IOException ex)
            {
                TestUtility.Log($"Stream.ReadAsync error {ex}");
            }

            return bytesRead;
        }

        byte[] CreateBuffer(int length, byte[] fillPattern)
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

        async Task SafeCloseAsync(HybridConnectionListener listener)
        {
            if (listener != null)
            {
                try
                {
                    TestUtility.Log($"Closing {listener}");
                    await listener.CloseAsync(TimeSpan.FromSeconds(10));
                }
                catch (Exception e)
                {
                    TestUtility.Log($"Error closing HybridConnectionListener {e.GetType()}: {e.Message}");
                }
            }
        }

        async Task<bool> TestAcceptHandler(RelayedHttpListenerContext listenerContext)
        {
            string delayString = listenerContext.Request.Headers[TestAcceptHandlerDelayHeader];
            TimeSpan delay;
            if (!string.IsNullOrEmpty(delayString) && TimeSpan.TryParse(delayString, out delay))
            {
                await Task.Delay(delay);
            }

            string statusCodeString = listenerContext.Request.Headers[TestAcceptHandlerStatusCodeHeader];
            int statusCode;
            if (!string.IsNullOrEmpty(statusCodeString) && int.TryParse(statusCodeString, out statusCode))
            {
                listenerContext.Response.StatusCode = (HttpStatusCode)statusCode;
            }

            string statusDescription = listenerContext.Request.Headers[TestAcceptHandlerStatusDescriptionHeader];
            if (!string.IsNullOrEmpty(statusDescription))
            {
                listenerContext.Response.StatusDescription = statusDescription;
            }

            string responseHeaders = listenerContext.Request.Headers[TestAcceptHandlerSetResponseHeader];
            if (!string.IsNullOrEmpty(responseHeaders))
            {
                foreach (var headerAndValue in responseHeaders.Split(','))
                {
                    string[] headerNameAndValue = headerAndValue.Split(':');
                    listenerContext.Response.Headers[headerNameAndValue[0]] = headerNameAndValue[1].Trim();
                }
            }

            bool result;
            if (!bool.TryParse(listenerContext.Request.Headers[TestAcceptHandlerResultHeader], out result))
            {
                result = true;
            }

            TestUtility.Log($"Test AcceptHandler: {listenerContext.Request.Url} {(int)listenerContext.Response.StatusCode}: {listenerContext.Response.StatusDescription} returning {result}");
            return result;
        }

        void VerifyHttpStatusCodeAndDescription(
            WebSocketException webSocketException,
            HttpStatusCode expectedStatusCode,
            string expectedStatusDescription,
            bool exactMatchDescription = true)
        {
            Assert.NotNull(webSocketException.InnerException);

            // TODO: Error details aren't available in .NET Core due to issue:
            // https://github.com/dotnet/corefx/issues/13773
            ////Assert.IsAssignableFrom<WebException>(webSocketException.InnerException);
            ////var webException = (WebException)webSocketException.InnerException;
            ////Assert.NotNull(webException.Response);
            ////Assert.IsAssignableFrom<HttpWebResponse>(webException.Response);
            ////var httpWebResponse = (HttpWebResponse)webException.Response;
            ////TestUtility.Log($"Actual HTTP Status: {(int)httpWebResponse.StatusCode}: {httpWebResponse.StatusDescription}");
            ////Assert.Equal(expectedStatusCode, httpWebResponse.StatusCode);
            ////if (exactMatchDescription)
            ////{
            ////    Assert.Equal(expectedStatusDescription, httpWebResponse.StatusDescription);
            ////}
            ////else
            ////{
            ////    Assert.Contains(expectedStatusDescription, httpWebResponse.StatusDescription);
            ////}
        }
    }
}