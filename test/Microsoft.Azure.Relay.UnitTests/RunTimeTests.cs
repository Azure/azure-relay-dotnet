// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class RunTimeTests : HybridConnectionTestBase
    {
        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task HybridConnectionTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = this.GetHybridConnectionListener(endpointTestType);
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
                    fakeEndpointConnectionStringBuilder.EntityPath = Constants.AuthenticatedEntityPath;
                }
                else
                {
                    fakeEndpointConnectionStringBuilder.EntityPath = Constants.UnauthenticatedEntityPath;
                }

                var fakeEndpointConnectionString = fakeEndpointConnectionStringBuilder.ToString();

                listener = new HybridConnectionListener(fakeEndpointConnectionString);
                var client = new HybridConnectionClient(fakeEndpointConnectionString);

// TODO: Remove this once .NET Core supports inspecting the StatusCode/Description. https://github.com/dotnet/corefx/issues/13773
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
    }
}