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

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task EmptyRequestEmptyResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                await listener.OpenAsync(cts.Token);
                listener.RequestHandler = async (context) =>
                {
                    TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                    await context.Response.CloseAsync();
                };

                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;

                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    getRequest.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    LogHttpRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                    }

                    var postRequest = new HttpRequestMessage();
                    postRequest.Method = HttpMethod.Post;
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    LogHttpRequest(postRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                    }
                }

                await listener.CloseAsync(cts.Token);
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task LarseRequestEmptyResponse(EndpointTestType endpointTestType)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                await listener.OpenAsync(cts.Token);
                string requestBody = null;
                listener.RequestHandler = async (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    requestBody = StreamToString(context.Request.InputStream);
                    TestUtility.Log($"Body Length: {requestBody.Length}");
                    await context.Response.CloseAsync();
                };

                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;

                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;
                    var postRequest = new HttpRequestMessage();
                    await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                    postRequest.Method = HttpMethod.Post;
                    var body = new string('y', 65 * 1024);
                    postRequest.Content = new StringContent(body);
                    LogHttpRequest(postRequest, client, showBody: false);
                    using (HttpResponseMessage response = await client.SendAsync(postRequest, cts.Token))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal(0, response.Content.ReadAsStreamAsync().Result.Length);
                        Assert.Equal(body.Length, requestBody.Length);
                    }
                }

                await listener.CloseAsync(cts.Token);
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task LoadBalancedListeners(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener1 = null;
            HybridConnectionListener listener2 = null;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                listener1 = this.GetHybridConnectionListener(endpointTestType);
                listener2 = this.GetHybridConnectionListener(endpointTestType);
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Opening HybridConnectionListeners");
                await Task.WhenAll(
                    listener1.OpenAsync(cts.Token),
                    listener2.OpenAsync(cts.Token));

                const int TotalExpectedRequestCount = 100;
                long listenerRequestCount1 = 0;
                long listenerRequestCount2 = 0;

                listener1.RequestHandler = async (context) =>
                {
                    Interlocked.Increment(ref listenerRequestCount1);
                    context.Response.OutputStream.WriteByte(1);
                    await context.Response.CloseAsync();
                };

                listener2.RequestHandler = async (context) =>
                {
                    Interlocked.Increment(ref listenerRequestCount2);
                    context.Response.OutputStream.WriteByte(2);
                    await context.Response.CloseAsync();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                await Enumerable.Range(0, TotalExpectedRequestCount).ParallelBatchAsync(
                    batchSize: 10,
                    parallelTasksCount: 10,
                    asyncTask: async (indexes) =>
                    {
                        string messageIndexes = string.Join(",", indexes);
                        TestUtility.Log($"Messages {messageIndexes} starting");
                        try
                        {
                            foreach (var index in indexes)
                            {
                                var httpRequest = (HttpWebRequest)WebRequest.Create(hybridHttpUri);
                                using (var abortRegistration = cts.Token.Register(() => httpRequest.Abort()))
                                {
                                    httpRequest.Method = HttpMethod.Get.Method;
                                    await AddAuthorizationHeader(connectionString, httpRequest.Headers, hybridHttpUri);

                                    using (var httpResponse = (HttpWebResponse)(await httpRequest.GetResponseAsync()))
                                    {
                                        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
                                    }
                                }
                            }
                        }
                        catch (WebException webException)
                        {
                            TestUtility.Log($"Messages {messageIndexes} Error: {webException}");
                            throw;
                        }

                        TestUtility.Log($"Messages {messageIndexes} complete");
                    });

                TestUtility.Log($"Listener1 Request Count: {listenerRequestCount1}");
                TestUtility.Log($"Listener2 Request Count: {listenerRequestCount2}");

                Assert.Equal(TotalExpectedRequestCount, listenerRequestCount1 + listenerRequestCount2);
                Assert.True(listenerRequestCount1 > 0, "Listener 1 should have received some of the events.");
                Assert.True(listenerRequestCount2 > 0, "Listener 2 should have received some of the events.");

                await Task.WhenAll(
                    listener1.CloseAsync(cts.Token),
                    listener2.CloseAsync(cts.Token));
            }
            finally
            {
                TestUtility.Log("Shutting down");
                cts.Dispose();
                await Task.WhenAll(
                    SafeCloseAsync(listener1),
                    SafeCloseAsync(listener2));
            }
        }

        [Theory, DisplayTestMethodName]
        [MemberData(nameof(AuthenticationTestPermutations))]
        async Task MultiValueHeader(EndpointTestType endpointTestType)
        {
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                const string CustomHeaderName = "X-CustomHeader";
                string requestHeaderValue = string.Empty;
                listener.RequestHandler = (context) =>
                {
                    TestUtility.Log("HybridConnectionListener.RequestHandler invoked with Request:");
                    TestUtility.Log($"{context.Request.HttpMethod} {context.Request.Url}");
                    context.Request.Headers.AllKeys.ToList().ForEach((k) => TestUtility.Log($"{k}: {context.Request.Headers[k]}"));
                    TestUtility.Log(StreamToString(context.Request.InputStream));
                    requestHeaderValue = context.Request.Headers[CustomHeaderName];

                    context.Response.Headers.Add(CustomHeaderName, "responseValue1");
                    context.Response.Headers.Add(CustomHeaderName, "responseValue2");
                    context.Response.OutputStream.Close();
                };

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    var getRequest = new HttpRequestMessage();
                    getRequest.Method = HttpMethod.Get;
                    await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                    getRequest.Headers.Add(CustomHeaderName, "requestValue1");
                    getRequest.Headers.Add(CustomHeaderName, "requestValue2");

                    LogHttpRequest(getRequest, client);
                    using (HttpResponseMessage response = await client.SendAsync(getRequest))
                    {
                        LogHttpResponse(response);
                        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                        Assert.Equal("requestValue1, requestValue2", requestHeaderValue);
                        string[] responseHeaders = string.Join(",", response.Headers.GetValues(CustomHeaderName)).Split(new[] { ',' });
                        for (int i = 0; i < responseHeaders.Length; i++)
                        {
                            responseHeaders[i] = responseHeaders[i].Trim(' ', '\t');
                        }

                        Assert.Equal(new [] {"responseValue1", "responseValue2" }, responseHeaders);
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task StatusCodes()
        {
            var endpointTestType = EndpointTestType.Authenticated;
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                var httpHandler = new HttpClientHandler { AllowAutoRedirect = false };
                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient(httpHandler) { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    HttpStatusCode[] expectedStatusCodes = new HttpStatusCode[]
                    {
                        HttpStatusCode.Accepted, HttpStatusCode.Ambiguous, HttpStatusCode.BadGateway, HttpStatusCode.BadRequest, HttpStatusCode.Conflict,
                        /*HttpStatusCode.Continue,*/ HttpStatusCode.Created, HttpStatusCode.ExpectationFailed, HttpStatusCode.Forbidden,
                        HttpStatusCode.GatewayTimeout, HttpStatusCode.Gone, HttpStatusCode.HttpVersionNotSupported, HttpStatusCode.InternalServerError,
                        HttpStatusCode.LengthRequired, HttpStatusCode.MethodNotAllowed, HttpStatusCode.MovedPermanently, HttpStatusCode.MultipleChoices,
                        HttpStatusCode.NoContent, HttpStatusCode.NonAuthoritativeInformation, HttpStatusCode.NotAcceptable, HttpStatusCode.NotFound,
                        HttpStatusCode.NotImplemented, HttpStatusCode.NotModified, HttpStatusCode.PartialContent, HttpStatusCode.PaymentRequired,
                        HttpStatusCode.PreconditionFailed, HttpStatusCode.ProxyAuthenticationRequired, HttpStatusCode.Redirect, HttpStatusCode.TemporaryRedirect,
                        HttpStatusCode.RedirectMethod, HttpStatusCode.RequestedRangeNotSatisfiable, HttpStatusCode.RequestEntityTooLarge, HttpStatusCode.RequestTimeout,
                        HttpStatusCode.RequestUriTooLong, HttpStatusCode.ResetContent, HttpStatusCode.ServiceUnavailable,
                        /*HttpStatusCode.SwitchingProtocols,*/ HttpStatusCode.Unauthorized, HttpStatusCode.UnsupportedMediaType,
                        HttpStatusCode.Unused, HttpStatusCode.UpgradeRequired, HttpStatusCode.UseProxy, (HttpStatusCode)418, (HttpStatusCode)450
                    };

                    foreach (HttpStatusCode expectedStatusCode in expectedStatusCodes)
                    {
                        TestUtility.Log($"Testing HTTP Status Code: {(int)expectedStatusCode} {expectedStatusCode}");
                        listener.RequestHandler = (context) =>
                        {
                            TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                            context.Response.StatusCode = expectedStatusCode;
                            context.Response.Close();
                        };

                        var getRequest = new HttpRequestMessage();
                        getRequest.Method = HttpMethod.Get;
                        await AddAuthorizationHeader(connectionString, getRequest, hybridHttpUri);
                        TestUtility.Log($"Request: {getRequest.Method} {hybridHttpUri}");
                        using (HttpResponseMessage response = await client.SendAsync(getRequest))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(expectedStatusCode, response.StatusCode);
                        }

                        var postRequest = new HttpRequestMessage();
                        postRequest.Method = HttpMethod.Post;
                        await AddAuthorizationHeader(connectionString, postRequest, hybridHttpUri);
                        string body = "{  \"a\": 11,   \"b\" :22, \"c\":\"test\",    \"d\":true}";
                        postRequest.Content = new StringContent(body);
                        postRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        TestUtility.Log($"Request: {postRequest.Method} {hybridHttpUri}");
                        using (HttpResponseMessage response = await client.SendAsync(postRequest))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(expectedStatusCode, response.StatusCode);
                        }
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        [Fact, DisplayTestMethodName]
        async Task Verbs()
        {
            var endpointTestType = EndpointTestType.Authenticated;
            HybridConnectionListener listener = this.GetHybridConnectionListener(endpointTestType);
            try
            {
                RelayConnectionStringBuilder connectionString = GetConnectionStringBuilder(endpointTestType);
                Uri endpointUri = connectionString.Endpoint;

                TestUtility.Log("Calling HybridConnectionListener.Open");
                await listener.OpenAsync(TimeSpan.FromSeconds(30));

                Uri hybridHttpUri = new UriBuilder("https://", endpointUri.Host, endpointUri.Port, connectionString.EntityPath).Uri;
                using (var client = new HttpClient { BaseAddress = hybridHttpUri })
                {
                    client.DefaultRequestHeaders.ExpectContinue = false;

                    HttpMethod[] methods = new HttpMethod[]
                    {
                        HttpMethod.Delete, HttpMethod.Get, HttpMethod.Head, HttpMethod.Options, HttpMethod.Post, HttpMethod.Put, HttpMethod.Trace
                    };

                    foreach (HttpMethod method in methods)
                    {
                        TestUtility.Log($"Testing HTTP Verb: {method}");
                        string actualMethod = string.Empty;
                        listener.RequestHandler = (context) =>
                        {
                            TestUtility.Log($"RequestHandler: {context.Request.HttpMethod} {context.Request.Url}");
                            actualMethod = context.Request.HttpMethod;
                            context.Response.Close();
                        };

                        var request = new HttpRequestMessage();
                        await AddAuthorizationHeader(connectionString, request, hybridHttpUri);
                        request.Method = method;
                        TestUtility.Log($"Request: {request.Method} {hybridHttpUri}");
                        using (HttpResponseMessage response = await client.SendAsync(request))
                        {
                            TestUtility.Log($"Response: HTTP/{response.Version} {(int)response.StatusCode} {response.ReasonPhrase}");
                            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                            Assert.Equal(method.Method, actualMethod);
                        }
                    }
                }

                await listener.CloseAsync(TimeSpan.FromSeconds(10));
            }
            finally
            {
                await SafeCloseAsync(listener);
            }
        }

        static Task AddAuthorizationHeader(RelayConnectionStringBuilder connectionString, HttpRequestMessage request, Uri resource)
        {
            return AddAuthorizationHeader(connectionString, resource, (t) => request.Headers.Add("ServiceBusAuthorization", t));
        }

        static Task AddAuthorizationHeader(RelayConnectionStringBuilder connectionString, WebHeaderCollection headers, Uri resource)
        {
            return AddAuthorizationHeader(connectionString, resource, (t) => headers.Add("ServiceBusAuthorization", t));
        }

        static async Task AddAuthorizationHeader(RelayConnectionStringBuilder connectionString, Uri resource, Action<string> addHeader)
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
                addHeader(token.TokenString);
            }
        }
    }
}
