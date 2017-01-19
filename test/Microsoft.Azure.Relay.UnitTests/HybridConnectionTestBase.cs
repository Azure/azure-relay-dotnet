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

    public class HybridConnectionTestBase
    {
        protected readonly string ConnectionString;

        protected enum EndpointTestType
        {
            Authenticated,
            Unauthenticated
        }

        public static IEnumerable<object> AuthenticationTestPermutations => new object[]
        {
            new object[] { EndpointTestType.Authenticated },
            new object[] { EndpointTestType.Unauthenticated }
        };

        public HybridConnectionTestBase()
        {
            var envConnectionString = Environment.GetEnvironmentVariable(Constants.ConnectionStringEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(envConnectionString))
            {
                throw new InvalidOperationException($"'{Constants.ConnectionStringEnvironmentVariable}' environment variable was not found!");
            }

            // Validate the connection string
            ConnectionString = new RelayConnectionStringBuilder(envConnectionString).ToString();
        }

        /// <summary>
        /// Returns a HybridConnectionClient based on the EndpointTestType (authenticated/unauthenticated).
        /// </summary>
        protected HybridConnectionClient GetHybridConnectionClient(EndpointTestType endpointTestType)
        {
            var connectionString = GetConnectionString(endpointTestType);
            return new HybridConnectionClient(connectionString);
        }

        /// <summary>
        /// Returns a HybridConnectionListener based on the EndpointTestType (authenticated/unauthenticated).
        /// </summary>
        protected HybridConnectionListener GetHybridConnectionListener(EndpointTestType endpointTestType)
        {
            // Even if the endpoint is unauthenticated, the *listener* still needs to be authenticated
            if (endpointTestType == EndpointTestType.Unauthenticated)
            {
                var connectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString)
                {
                    EntityPath = Constants.UnauthenticatedEntityPath
                };
                return new HybridConnectionListener(connectionStringBuilder.ToString());
            }

            return new HybridConnectionListener(GetConnectionString(endpointTestType));
        }

        protected async Task SafeCloseAsync(HybridConnectionListener listener)
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

        /// <summary>
        /// Call HybridConnectionListener.AcceptConnectionAsync, once/if a listener is accepted
        /// read from its stream and echo the bytes until a 0-byte read occurs, then close.
        /// </summary>
        protected async void AcceptEchoListener(HybridConnectionListener listener)
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

        protected async Task ReadCountBytesAsync(Stream stream, byte[] buffer, int offset, int bytesToRead, TimeSpan timeout)
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

        protected byte[] CreateBuffer(int length, byte[] fillPattern)
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

        /// <summary>
        /// Calls Stream.ReadAsync with Exception handling. If an IOException occurs a zero byte read is returned.
        /// </summary>
        protected async Task<int> SafeReadAsync(Stream stream, byte[] buffer, int offset, int bytesToRead)
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

        /// <summary>
        /// Since these tests all share a common connection string, this method will modify the 
        /// endpoint / shared access keys as needed based on the EndpointTestType.
        /// </summary>
        protected string GetConnectionString(EndpointTestType endpointTestType)
        {
            var connectionStringBuilder = new RelayConnectionStringBuilder(this.ConnectionString);
            if (endpointTestType == EndpointTestType.Unauthenticated)
            {
                connectionStringBuilder.EntityPath = Constants.UnauthenticatedEntityPath;
                connectionStringBuilder.SharedAccessKey = string.Empty;
                connectionStringBuilder.SharedAccessKeyName = string.Empty;
                connectionStringBuilder.SharedAccessSignature = string.Empty;
            }
            else
            {
                connectionStringBuilder.EntityPath = Constants.AuthenticatedEntityPath;
            }

            return connectionStringBuilder.ToString();
        }

        protected void VerifyHttpStatusCodeAndDescription(
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
