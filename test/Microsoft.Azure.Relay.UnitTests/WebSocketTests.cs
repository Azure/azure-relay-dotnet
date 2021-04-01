// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class WebSocketTests : HybridConnectionTestBase
    {
        const string TestAcceptHandlerResultHeader = "X-TestAcceptHandlerResult";
        const string TestAcceptHandlerStatusCodeHeader = "X-TestAcceptHandlerStatusCode";
        const string TestAcceptHandlerStatusDescriptionHeader = "X-TestAcceptHandlerStatusDescription";
        const string TestAcceptHandlerSetResponseHeader = "X-TestAcceptHandlerSetResponseHeader";
        const string TestAcceptHandlerDelayHeader = "X-TestAcceptHandlerDelay";

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task RawWebSocketSenderTest(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = null;
            try
            {
                listener = GetHybridConnectionListener(endpointTestType);
                await TestRawWebSocket(listener, endpointTestType);
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

        [Fact]
        [DisplayTestMethodName]
        async Task CustomWebSocketTest()
        {
            HybridConnectionListener listener = null;
            try
            {
                EndpointTestType endpointTestType = EndpointTestType.Authenticated;
                listener = GetHybridConnectionListener(endpointTestType);
                CustomClientWebSocketFactory factory = new CustomClientWebSocketFactory();
                listener.CustomClientWebSocketFactory = factory;
                await TestRawWebSocket(listener, endpointTestType);
                Assert.True(factory.WasCreateCalled);
            }
            finally
            {
                await this.SafeCloseAsync(listener);
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

        async Task TestRawWebSocket(HybridConnectionListener listener, EndpointTestType endpointTestType)
        {
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
    }
}
