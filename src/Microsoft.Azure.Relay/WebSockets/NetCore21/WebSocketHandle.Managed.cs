﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets.NetCore21
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    // From: https://github.com/dotnet/corefx/blob/master/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/WebSocketHandle.Managed.cs
    sealed class WebSocketHandle
    {
        /// <summary>GUID appended by the server as part of the security key response.  Defined in the RFC.</summary>
        private const string WSServerGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>Shared, lazily-initialized handler for when using default options.</summary>
        private static SocketsHttpHandler s_defaultHandler;

        private readonly CancellationTokenSource _abortSource = new CancellationTokenSource();
        private WebSocketState _state = WebSocketState.Connecting;
        private WebSocket _webSocket;

        public static WebSocketHandle Create() => new WebSocketHandle();

        public static bool IsValid(WebSocketHandle handle) => handle != null;

        public WebSocketCloseStatus? CloseStatus => _webSocket?.CloseStatus;

        public string CloseStatusDescription => _webSocket?.CloseStatusDescription;

        public WebSocketState State => _webSocket?.State ?? _state;

        public string SubProtocol => _webSocket?.SubProtocol;

        public static void CheckPlatformSupport() { /* nop */ }

        // <RELAY_CUSTOM Comment="WebSocket.CreateFromStream we need is in netcoreapp2.1 but not part of netstandard1.0.">
        static MethodInfo s_createFromStreamMethod;

        public static bool IsSupported()
        {
            return GetCreateFromStreamMethod(throwOnError: false) != null;
        }

        static MethodInfo GetCreateFromStreamMethod(bool throwOnError)
        {
            if (s_createFromStreamMethod == null)
            {
                s_createFromStreamMethod = typeof(WebSocket).GetMethod(
                    "CreateFromStream",
                    new Type[] { typeof(Stream), typeof(bool), typeof(string), typeof(TimeSpan) });
                if (s_createFromStreamMethod == null && throwOnError)
                {
                    throw new MissingMethodException(typeof(WebSocket).FullName, "CreateFromStream(Stream, bool, string, TimeSpan)");
                }
            }

            return s_createFromStreamMethod;
        }
        // </RELAY_CUSTOM>

        public void Dispose()
        {
            _state = WebSocketState.Closed;
            _webSocket?.Dispose();
        }

        public void Abort()
        {
            _abortSource.Cancel();
            _webSocket?.Abort();
        }

        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
            _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        // Requires .Net Standard 2.1
        ////public ValueTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) =>
        ////    _webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            _webSocket.ReceiveAsync(buffer, cancellationToken);

        // Requires .Net Standard 2.1
        ////public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
        ////    _webSocket.ReceiveAsync(buffer, cancellationToken);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

        public async Task ConnectAsyncCore(Uri uri, CancellationToken cancellationToken, ClientWebSocketOptions options)
        {
            HttpResponseMessage response = null;
            SocketsHttpHandler handler = null;
            bool disposeHandler = true;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (options._requestHeaders?.Count > 0) // use field to avoid lazily initializing the collection
                {
                    foreach (string key in options.RequestHeaders)
                    {
                        request.Headers.TryAddWithoutValidation(key, options.RequestHeaders[key]);
                    }
                }

                // Create the security key and expected response, then build all of the request headers
                KeyValuePair<string, string> secKeyAndSecWebSocketAccept = CreateSecKeyAndSecWebSocketAccept();
                AddWebSocketHeaders(request, secKeyAndSecWebSocketAccept.Key, options);

                // Create the handler for this request and populate it with all of the options.
                // Try to use a shared handler rather than creating a new one just for this request, if
                // the options are compatible.
                if (options.Credentials == null &&
                    !options.UseDefaultCredentials &&
                    options.Proxy == null &&
                    options.Cookies == null &&
                    options.RemoteCertificateValidationCallback == null &&
                    options._clientCertificates?.Count == 0)
                {
                    disposeHandler = false;
                    handler = s_defaultHandler;
                    if (handler == null)
                    {
                        handler = new SocketsHttpHandler()
                        {
                            PooledConnectionLifetime = TimeSpan.Zero,
                            UseProxy = false,
                            UseCookies = false,
                        };
                        if (Interlocked.CompareExchange(ref s_defaultHandler, handler, null) != null)
                        {
                            handler.Dispose();
                            handler = s_defaultHandler;
                        }
                    }
                }
                else
                {
                    handler = new SocketsHttpHandler();
                    handler.PooledConnectionLifetime = TimeSpan.Zero;
                    handler.CookieContainer = options.Cookies;
                    handler.UseCookies = options.Cookies != null;
                    handler.SslOptions.RemoteCertificateValidationCallback = options.RemoteCertificateValidationCallback;

                    if (options.UseDefaultCredentials)
                    {
                        handler.Credentials = CredentialCache.DefaultCredentials;
                    }
                    else
                    {
                        handler.Credentials = options.Credentials;
                    }

                    if (options.Proxy == null)
                    {
                        handler.UseProxy = false;
                    }
                    else if (options.Proxy != DefaultWebProxy.Instance)
                    {
                        handler.Proxy = options.Proxy;
                    }

                    if (options._clientCertificates?.Count > 0) // use field to avoid lazily initializing the collection
                    {
                        Debug.Assert(handler.SslOptions.ClientCertificates == null);
                        handler.SslOptions.ClientCertificates = new X509Certificate2Collection();
                        handler.SslOptions.ClientCertificates.AddRange(options.ClientCertificates);
                    }
                }

                // Issue the request.  The response must be status code 101.
                CancellationTokenSource linkedCancellation, externalAndAbortCancellation;
                if (cancellationToken.CanBeCanceled) // avoid allocating linked source if external token is not cancelable
                {
                    linkedCancellation =
                        externalAndAbortCancellation =
                        CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _abortSource.Token);
                }
                else
                {
                    linkedCancellation = null;
                    externalAndAbortCancellation = _abortSource;
                }

                using (linkedCancellation)
                {
                    response = await handler.SendAsync(request, externalAndAbortCancellation.Token).ConfigureAwait(false);
                    externalAndAbortCancellation.Token.ThrowIfCancellationRequested(); // poll in case sends/receives in request/response didn't observe cancellation
                }

                if (response.StatusCode != HttpStatusCode.SwitchingProtocols)
                {
                    // <RELAY_CUSTOM Comment="Include the HttpResponseMessage in the Exception.Data">
                    var wsException = new WebSocketException(WebSocketError.NotAWebSocket, SR.Format(SR.net_WebSockets_Connect101Expected, (int)response.StatusCode));
                    wsException.Data[typeof(HttpResponseMessage).FullName] = response;
                    throw wsException;
                    // </RELAY_CUSTOM>
                }

                // The Connection, Upgrade, and SecWebSocketAccept headers are required and with specific values.
                ValidateHeader(response, HttpKnownHeaderNames.Connection, "Upgrade");
                ValidateHeader(response, HttpKnownHeaderNames.Upgrade, "websocket");
                ValidateHeader(response, HttpKnownHeaderNames.SecWebSocketAccept, secKeyAndSecWebSocketAccept.Value);

                // The SecWebSocketProtocol header is optional.  We should only get it with a non-empty value if we requested subprotocols,
                // and then it must only be one of the ones we requested.  If we got a subprotocol other than one we requested (or if we
                // already got one in a previous header), fail. Otherwise, track which one we got.
                string subprotocol = null;
                IEnumerable<string> subprotocolEnumerableValues;
                if (response.Headers.TryGetValues(HttpKnownHeaderNames.SecWebSocketProtocol, out subprotocolEnumerableValues))
                {
                    Debug.Assert(subprotocolEnumerableValues is string[]);
                    string[] subprotocolArray = (string[])subprotocolEnumerableValues;
                    if (subprotocolArray.Length > 0 && !string.IsNullOrEmpty(subprotocolArray[0]))
                    {
                        subprotocol = options.RequestedSubProtocols.Find(requested => string.Equals(requested, subprotocolArray[0], StringComparison.OrdinalIgnoreCase));
                        if (subprotocol == null)
                        {
                            throw new WebSocketException(
                                WebSocketError.UnsupportedProtocol,
                                SR.Format(SR.net_WebSockets_AcceptUnsupportedProtocol, string.Join(", ", options.RequestedSubProtocols), string.Join(", ", subprotocolArray)));
                        }
                    }
                }

                // Get the response stream and wrap it in a web socket.
                Stream connectedStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                Debug.Assert(connectedStream.CanWrite);
                Debug.Assert(connectedStream.CanRead);

                // <RELAY_CUSTOM Comment="WebSocket.CreateFromStream we need is in netcoreapp2.1 but not part of netstandard1.0.">
                MethodInfo createFromStreamMethod = GetCreateFromStreamMethod(throwOnError: true);
                _webSocket = (WebSocket)createFromStreamMethod.Invoke(
                    null, new object [] { connectedStream, /* isServer: */ false, subprotocol, options.KeepAliveInterval });
                // </RELAY_CUSTOM>
            }
            catch (Exception exc)
            {
                if (_state < WebSocketState.Closed)
                {
                    _state = WebSocketState.Closed;
                }

                Abort();
                response?.Dispose();

                if (exc is WebSocketException)
                {
                    throw;
                }
                throw new WebSocketException(WebSocketError.Faulted, SR.net_webstatus_ConnectFailure, exc);
            }
            finally
            {
                // Disposing the handler will not affect any active stream wrapped in the WebSocket.
                if (disposeHandler)
                {
                    handler?.Dispose();
                }
            }
        }

#pragma warning disable CS1573 // Parameter 'request|options' has no matching param tag in the XML comment (but other parameters do)
        /// <param name="secKey">The generated security key to send in the Sec-WebSocket-Key header.</param>
        private static void AddWebSocketHeaders(HttpRequestMessage request, string secKey, ClientWebSocketOptions options)
        {
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.Connection, HttpKnownHeaderNames.Upgrade);
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.Upgrade, "websocket");
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.SecWebSocketVersion, "13");
            request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.SecWebSocketKey, secKey);
            if (options._requestedSubProtocols?.Count > 0)
            {
                request.Headers.TryAddWithoutValidation(HttpKnownHeaderNames.SecWebSocketProtocol, string.Join(", ", options.RequestedSubProtocols));
            }
        }

        /// <summary>
        /// Creates a pair of a security key for sending in the Sec-WebSocket-Key header and
        /// the associated response we expect to receive as the Sec-WebSocket-Accept header value.
        /// </summary>
        /// <returns>A key-value pair of the request header security key and expected response header value.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA5350", Justification = "Required by RFC6455")]
        private static KeyValuePair<string, string> CreateSecKeyAndSecWebSocketAccept()
        {
            string secKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            using (SHA1 sha = SHA1.Create())
            {
                return new KeyValuePair<string, string>(
                    secKey,
                    Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(secKey + WSServerGuid))));
            }
        }

        private static void ValidateHeader(HttpResponseMessage response, string name, string expectedValue)
        {
            if (!response.Headers.TryGetValues(name, out IEnumerable<string> values))
            {
                // <RELAY_CUSTOM Comment="Include the HttpResponseMessage in the Exception.Data">
                var wsException = new WebSocketException(WebSocketError.Faulted, SR.Format(SR.net_WebSockets_MissingResponseHeader, name));
                wsException.Data[typeof(HttpResponseMessage).FullName] = response;
                throw wsException;
                // </RELAY_CUSTOM>
            }

            Debug.Assert(values is string[]);
            string[] array = (string[])values;
            if (array.Length != 1 || !string.Equals(array[0], expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                // <RELAY_CUSTOM Comment="Include the HttpResponseMessage in the Exception.Data">
                var wsException = new WebSocketException(WebSocketError.HeaderError, SR.Format(SR.net_WebSockets_InvalidResponseHeader, name, string.Join(", ", array)));
                wsException.Data[typeof(HttpResponseMessage).FullName] = response;
                throw wsException;
                // </RELAY_CUSTOM>
            }
        }
    }
}