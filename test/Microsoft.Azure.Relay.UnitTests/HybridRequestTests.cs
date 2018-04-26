// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class HybridRequestTests : HybridConnectionTestBase
    {
        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task SmallRequestSmallResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = null;
            try
            {
                listener = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                string expectedResponse = "{ \"a\" : true }";
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));

                    context.Response.StatusCode = expectedStatusCode;
                    byte[] responseBytes = Encoding.UTF8.GetBytes(expectedResponse);
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                };

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(cts.Token);

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Method = HttpMethod.Get;
                    LogHttpRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest, cts.Token))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("OK", response.ReasonPhrase);
                        Assert.Equal(expectedResponse, await response.Content.ReadAsStringAsync());
                    }

                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                    postRequest.Content = new StringContent(body);
                    postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    LogHttpRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("OK", response.ReasonPhrase);
                        Assert.Equal(expectedResponse, await response.Content.ReadAsStringAsync());
                    }
                }

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(cts.Token);
                listener = null;
            }
            finally
            {
                cts.Dispose();
                await this.SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task SmallRequestLargeResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = null;
            try
            {
                listener = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log($"Opening {listener}");
                await listener.OpenAsync(cts.Token);

                string expectedResponse = new string('y', 65 * 1024);
                byte[] responseBytes = Encoding.UTF8.GetBytes(expectedResponse);
                HttpStatusCode expectedStatusCode = HttpStatusCode.OK;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));

                    context.Response.StatusCode = expectedStatusCode;
                    context.Response.StatusDescription = "TestStatusDescription";
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                    context.Response.Close();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Method = HttpMethod.Get;
                    LogHttpRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest, cts.Token))
                    {
                        LogHttpResponse(response, showBody: false);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("TestStatusDescription", response.ReasonPhrase);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Assert.Equal(expectedResponse.Length, responseContent.Length);
                        Assert.Equal(expectedResponse, responseContent);
                    }

                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                    postRequest.Content = new StringContent(body);
                    postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    LogHttpRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogHttpResponse(response, showBody: false);
                        Assert.Equal(expectedStatusCode, response.StatusCode);
                        Assert.Equal("TestStatusDescription", response.ReasonPhrase);
                        string responseContent = await response.Content.ReadAsStringAsync();
                        Assert.Equal(expectedResponse.Length, responseContent.Length);
                        Assert.Equal(expectedResponse, responseContent);
                    }
                }                

                TestUtility.Log($"Closing {listener}");
                await listener.CloseAsync(cts.Token);
                listener = null;
            }
            finally
            {
                cts.Dispose();
                await this.SafeCloseAsync(listener);
            }
        }

        static async Task AddAuthorizationHeader(RelayConnectionStringBuilder connectionString, HttpRequestMessage request, Uri resource)
        {
            TokenProvider tokenProvider = null;
            if (!string.IsNullOrEmpty(connectionString.SharedAccessKeyName) && !string.IsNullOrEmpty(connectionString.SharedAccessKey))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessKeyName, connectionString.SharedAccessKey);
            }
            else if (!string.IsNullOrEmpty(connectionString.SharedAccessSignature))
            {
                tokenProvider = TokenProvider.CreateSharedAccessSignatureTokenProvider(connectionString.SharedAccessSignature);
            }

            if (tokenProvider != null)
            {
                var token = await tokenProvider.GetTokenAsync(resource.AbsoluteUri, TimeSpan.FromMinutes(20));
                request.Headers.Add("ServiceBusAuthorization", token.TokenString);
            }
        }
    }
}
