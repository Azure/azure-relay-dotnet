// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Runtime.ExceptionServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    // From: https://github.com/dotnet/corefx/blob/master/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/WebSocketHandle.Managed.cs
    internal sealed class WebSocketHandle
    {
        /// <summary>Per-thread cached StringBuilder for building of strings to send on the connection.</summary>
        [ThreadStatic]
        private static StringBuilder t_cachedStringBuilder;

        /// <summary>Default encoding for HTTP requests. Latin alphabeta no 1, ISO/IEC 8859-1.</summary>
        private static readonly Encoding s_defaultHttpEncoding = Encoding.GetEncoding(28591);

        /// <summary>Cache the whitespace chars found in Http responses.</summary>
        private static readonly char[] s_whitespaceChars = new char[] { ' ', '\t', '\r', '\n' };

        /// <summary>Cache the characters used for trimming authentication header values.</summary>
        private static readonly char[] s_trimChars = new char[] { ' ', '\t', ',' };

        /// <summary>Used for reading message body from proxy server.</summary>
        private static byte[] _readBuffer;

        /// <summary>Size of the receive buffer to use.</summary>
        private const int DefaultReceiveBufferSize = 0x1000;
        /// <summary>GUID appended by the server as part of the security key response.  Defined in the RFC.</summary>
        private const string WSServerGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly CancellationTokenSource _abortSource = new CancellationTokenSource();
        private WebSocketState _state = WebSocketState.Connecting;
        private ManagedWebSocket _webSocket;

        public static WebSocketHandle Create() => new WebSocketHandle();

        public static bool IsValid(WebSocketHandle handle) => handle != null;

        public WebSocketCloseStatus? CloseStatus => _webSocket?.CloseStatus;

        public string CloseStatusDescription => _webSocket?.CloseStatusDescription;

        public WebSocketState State => _webSocket?.State ?? _state;

        public string SubProtocol => _webSocket?.SubProtocol;

        public static void CheckPlatformSupport() { /* nop */ }

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

        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            _webSocket.ReceiveAsync(buffer, cancellationToken);

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);

        public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) =>
            _webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

        public async Task ConnectAsyncCore(Uri uri, CancellationToken cancellationToken, ClientWebSocketOptions options)
        {
            // TODO #14480 : Not currently implemented, or explicitly ignored:
            // - ClientWebSocketOptions.UseDefaultCredentials
            // - ClientWebSocketOptions.Credentials
            // - ClientWebSocketOptions._sendBufferSize

            // Establish connection to the server
            CancellationTokenRegistration registration = cancellationToken.Register(s => ((WebSocketHandle)s).Abort(), this);
            try
            {

                Socket connectedSocket;
                Stream stream;
                if (options.Proxy != null && options.Proxy != WebRequest.DefaultWebProxy)
                {
                    Uri proxyUri = options.Proxy.GetProxy(uri);
                    connectedSocket = await ConnectSocketAsync(proxyUri.Host, proxyUri.Port, cancellationToken).ConfigureAwait(false);
                    stream = new AsyncEventArgsNetworkStream(connectedSocket);
                    await ConnectWithProxyAsync(uri, options, stream, cancellationToken);
                }
                else
                {
                    // Connect to the remote server
                    connectedSocket = await ConnectSocketAsync(uri.Host, uri.Port, cancellationToken).ConfigureAwait(false);
                    stream = new AsyncEventArgsNetworkStream(connectedSocket);
                }

                // Upgrade to SSL if needed
                if (uri.Scheme == UriScheme.Wss)
                {
                    var sslStream = new SslStream(stream);
                    await sslStream.AuthenticateAsClientAsync(
                        uri.Host,
                        options.ClientCertificates,
                        SecurityProtocol.AllowedSecurityProtocols,
                        checkCertificateRevocation: false).ConfigureAwait(false);
                    stream = sslStream;
                }

                // Create the security key and expected response, then build all of the request headers
                KeyValuePair<string, string> secKeyAndSecWebSocketAccept = CreateSecKeyAndSecWebSocketAccept();
                byte[] requestHeader = BuildRequestHeader(uri, options, secKeyAndSecWebSocketAccept.Key);

                // Write out the header to the connection
                await stream.WriteAsync(requestHeader, 0, requestHeader.Length, cancellationToken).ConfigureAwait(false);

                // Parse the response and store our state for the remainder of the connection
                string subprotocol = await ParseAndValidateConnectResponseAsync(stream, options, secKeyAndSecWebSocketAccept.Value, cancellationToken).ConfigureAwait(false);

                _webSocket = ManagedWebSocket.CreateFromConnectedStream(
                    stream, false, subprotocol, options.KeepAliveInterval, options.ReceiveBufferSize, options.Buffer);

                // If a concurrent Abort or Dispose came in before we set _webSocket, make sure to update it appropriately
                if (_state == WebSocketState.Aborted)
                {
                    _webSocket.Abort();
                }
                else if (_state == WebSocketState.Closed)
                {
                    _webSocket.Dispose();
                }
            }
            catch (Exception exc)
            {
                if (_state < WebSocketState.Closed)
                {
                    _state = WebSocketState.Closed;
                }

                Abort();

                if (exc is WebSocketException)
                {
                    throw;
                }
                throw new WebSocketException(SR.net_webstatus_ConnectFailure, exc);
            }
            finally
            {
                registration.Dispose();
            }
        }

        /// <summary>Connects a socket to the specified host and port, subject to cancellation and aborting.</summary>
        /// <param name="host">The host to which to connect.</param>
        /// <param name="port">The port to which to connect on the host.</param>
        /// <param name="cancellationToken">The CancellationToken to use to cancel the websocket.</param>
        /// <returns>The connected Socket.</returns>
        private async Task<Socket> ConnectSocketAsync(string host, int port, CancellationToken cancellationToken)
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);

            ExceptionDispatchInfo lastException = null;
            foreach (IPAddress address in addresses)
            {
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    using (cancellationToken.Register(s => ((Socket)s).Dispose(), socket))
                    using (_abortSource.Token.Register(s => ((Socket)s).Dispose(), socket))
                    {
                        try
                        {
                            await socket.ConnectAsync(address, port).ConfigureAwait(false);
                        }
                        catch (ObjectDisposedException ode)
                        {
                            // If the socket was disposed because cancellation was requested, translate the exception
                            // into a new OperationCanceledException.  Otherwise, let the original ObjectDisposedexception propagate.
                            CancellationToken token = cancellationToken.IsCancellationRequested ? cancellationToken : _abortSource.Token;
                            if (token.IsCancellationRequested)
                            {
                                throw new OperationCanceledException(new OperationCanceledException().Message, ode, token);
                            }
                        }
                    }
                    cancellationToken.ThrowIfCancellationRequested(); // in case of a race and socket was disposed after the await
                    _abortSource.Token.ThrowIfCancellationRequested();
                    return socket;
                }
                catch (Exception exc)
                {
                    socket.Dispose();
                    lastException = ExceptionDispatchInfo.Capture(exc);
                }
            }

            lastException?.Throw();

            Debug.Fail("We should never get here. We should have already returned or an exception should have been thrown.");
            throw new WebSocketException(SR.net_webstatus_ConnectFailure);
        }

        private async Task ConnectWithProxyAsync(Uri uri, ClientWebSocketOptions options, Stream stream, CancellationToken cancellationToken)
        {
            const string successStatusCode = "HTTP/1.1 200";
            const string proxyAuthenticationStatusCode = "HTTP/1.1 407";
            const string Basic = "Basic ";
            const string Digest = "Digest ";
            AuthenticationHeaderValue ahv = null;

            string statusLine = await SendRequestToProxyAsync(uri, options, stream, ahv, cancellationToken);
            if (statusLine == successStatusCode)
            {
                // Proxy connection succeeded.
                return;
            }

            // Proxy requires authentication.
            if (statusLine.StartsWith(proxyAuthenticationStatusCode))
            {
                if (statusLine.Length > proxyAuthenticationStatusCode.Length &&
                    !char.IsWhiteSpace(statusLine[proxyAuthenticationStatusCode.Length]))
                {
                    throw new WebSocketException(SR.net_webstatus_ConnectFailure);
                }

                string line;
                int contentLength = 0;
                while (!string.IsNullOrEmpty((line = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false))))
                {
                    if (line.StartsWith("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = line.IndexOf(':');
                        if (colon == -1 || colon == line.Length - 1)
                        {
                            throw new WebSocketException(SR.net_WebSockets_InvalidResponseHeader);
                        }

                        string authenticateString = line.Substring(colon + 1);
                        string authHeader = GetParameterForScheme(authenticateString, Digest) ?? GetParameterForScheme(authenticateString, Basic);
                        if (authHeader != null)
                        {
                            ahv = AuthenticationHeaderValue.Parse(authHeader);
                        }
                    }
                    else if (line.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = line.IndexOf(':');
                        if (colon == -1 || colon == line.Length - 1)
                        {
                            throw new WebSocketException(SR.net_WebSockets_InvalidResponseHeader);
                        }

                        int.TryParse(line.Substring(colon + 1), out contentLength);
                    }
                    else if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))
                    {
                        int nextValidIndex = line.IndexOf(':') + 1;
                        if (nextValidIndex < line.Length && line.SubstringTrim(nextValidIndex).Equals("chunked", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new WebSocketException(SR.net_webstatus_ConnectFailure);
                        }
                    }
                }

                if (ahv == null)
                {
                    throw new WebSocketException(SR.net_webstatus_ConnectFailure);
                }

                // Read any response content from proxy.
                await ReadResponseContent(stream, contentLength, cancellationToken);

                // Retry request to proxy with authorization header.
                statusLine = await SendRequestToProxyAsync(uri, options, stream, ahv, cancellationToken);

                if (statusLine == successStatusCode)
                {
                    return;
                }
            }

            // Failed to connect to proxy.
            throw new WebSocketException(SR.net_webstatus_ConnectFailure);
        }

        internal static async Task<string> SendRequestToProxyAsync(Uri uri, ClientWebSocketOptions options, Stream stream, AuthenticationHeaderValue ahv, CancellationToken cancellationToken)
        {
            const string successStatusCode = "HTTP/1.1 200";
            byte[] headers = BuildProxyHeaders(uri, options, ahv);
            // Write the proxy headers.
            await stream.WriteAsync(headers, 0, headers.Length, cancellationToken).ConfigureAwait(false);
            // Read the response from proxy server.
            string statusLine = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false);

            // Parse if success response.
            if (statusLine.StartsWith(successStatusCode))
            {
                if (statusLine.Length > successStatusCode.Length && !char.IsWhiteSpace(statusLine[successStatusCode.Length]))
                {
                    throw new WebSocketException(SR.net_webstatus_ConnectFailure);
                }

                // Read response headers.
                string line;
                int contentLength = 0;
                while (!string.IsNullOrEmpty((line = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false))))
                {
                    if (line.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        int colon = line.IndexOf(':');
                        if (colon == -1 || colon == line.Length - 1)
                        {
                            throw new WebSocketException(SR.net_WebSockets_InvalidResponseHeader);
                        }

                        int.TryParse(line.Substring(colon + 1), out contentLength);
                    }
                    else if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase))
                    {
                        int nextValidIndex = line.IndexOf(':') + 1;
                        if (nextValidIndex < line.Length && line.SubstringTrim(nextValidIndex).Equals("chunked", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new WebSocketException(SR.net_webstatus_ConnectFailure);
                        }
                    }
                }

                await ReadResponseContent(stream, contentLength, cancellationToken);
                return successStatusCode;
            }

            return statusLine;
        }

        internal static async Task ReadResponseContent(Stream stream, int contentLength, CancellationToken cancellationToken)
        {
            if (contentLength > 0)
            {
                if (_readBuffer == null)
                {
                    _readBuffer = new byte[1024];
                }

                int bytesRead = 0;
                while (bytesRead < contentLength)
                {
                    bytesRead += await stream.ReadAsync(_readBuffer, 0, _readBuffer.Length, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal static string GetParameterForScheme(string line, string scheme)
        {
            int basicIndex = line.IndexOf(scheme, StringComparison.OrdinalIgnoreCase);
            if (basicIndex != -1 && line.Length >= scheme.Length)
            {
                int i = basicIndex + scheme.Length;
                while (i < line.Length)
                {
                    int indexOfComma = line.IndexOf(',', i);
                    if (indexOfComma == -1)
                    {
                        return line.Substring(basicIndex).Trim(s_trimChars);
                    }

                    i = SkipWhitespace(line, indexOfComma + 1);
                    if (i == line.Length)
                    {
                        return line.Substring(basicIndex).Trim(s_trimChars);
                    }

                    if (line[i] == ',')
                    {
                        // Empty key value pair.
                        continue;
                    }

                    if (IsNextScheme(line, i, out int newI))
                    {
                        return line.Substring(basicIndex, i - basicIndex).Trim(s_trimChars);
                    }

                    i = newI;
                }

                return line.Substring(basicIndex).Trim(s_trimChars);
            }

            return null;
        }

        internal static bool IsNextScheme(string line, int startIndex, out int endIndex)
        {
            while (startIndex < line.Length)
            {
                if (line[startIndex] == '=')
                {
                    endIndex = startIndex + 1;
                    return false;
                }
                else if (char.IsWhiteSpace(line[startIndex]) || line[startIndex] == ',')
                {
                    endIndex = startIndex + 1;
                    return true;
                }

                startIndex++;
            }

            endIndex = startIndex;
            return true;
        }

        internal static int SkipWhitespace(string line, int startIndex)
        {
            if (startIndex < line.Length && char.IsWhiteSpace(line[startIndex]))
                startIndex++;

            return startIndex;
        }

        /// <summary>Creates a byte[] containing the headers to send to the server.</summary>
        /// <param name="uri">The Uri of the server.</param>
        /// <param name="options">The options used to configure the websocket.</param>
        /// <param name="secKey">The generated security key to send in the Sec-WebSocket-Key header.</param>
        /// <returns>The byte[] containing the encoded headers ready to send to the network.</returns>
        private static byte[] BuildRequestHeader(Uri uri, ClientWebSocketOptions options, string secKey)
        {
            StringBuilder builder = t_cachedStringBuilder ?? (t_cachedStringBuilder = new StringBuilder());
            Debug.Assert(builder.Length == 0, $"Expected builder to be empty, got one of length {builder.Length}");
            try
            {
                builder.Append("GET ").Append(uri.PathAndQuery).Append(" HTTP/1.1\r\n");

                // Add all of the required headers, honoring Host header if set.
                string hostHeader = options.RequestHeaders[HttpKnownHeaderNames.Host];
                builder.Append("Host: ");
                if (string.IsNullOrEmpty(hostHeader))
                {
                    builder.Append(uri.IdnHost).Append(':').Append(uri.Port).Append("\r\n");
                }
                else
                {
                    builder.Append(hostHeader).Append("\r\n");
                }

                builder.Append("Connection: Upgrade\r\n");
                builder.Append("Upgrade: websocket\r\n");
                builder.Append("Sec-WebSocket-Version: 13\r\n");
                builder.Append("Sec-WebSocket-Key: ").Append(secKey).Append("\r\n");

                // Add all of the additionally requested headers
                foreach (string key in options.RequestHeaders.AllKeys)
                {
                    if (string.Equals(key, HttpKnownHeaderNames.Host, StringComparison.OrdinalIgnoreCase))
                    {
                        // Host header handled above
                        continue;
                    }

                    builder.Append(key).Append(": ").Append(options.RequestHeaders[key]).Append("\r\n");
                }

                // Add the optional subprotocols header
                if (options.RequestedSubProtocols.Count > 0)
                {
                    builder.Append(HttpKnownHeaderNames.SecWebSocketProtocol).Append(": ");
                    builder.Append(options.RequestedSubProtocols[0]);
                    for (int i = 1; i < options.RequestedSubProtocols.Count; i++)
                    {
                        builder.Append(", ").Append(options.RequestedSubProtocols[i]);
                    }
                    builder.Append("\r\n");
                }

                // Add an optional cookies header
                if (options.Cookies != null)
                {
                    string header = options.Cookies.GetCookieHeader(uri);
                    if (!string.IsNullOrWhiteSpace(header))
                    {
                        builder.Append(HttpKnownHeaderNames.Cookie).Append(": ").Append(header).Append("\r\n");
                    }
                }

                // End the headers
                builder.Append("\r\n");

                // Return the bytes for the built up header
                return s_defaultHttpEncoding.GetBytes(builder.ToString());
            }
            finally
            {
                // Make sure we clear the builder
                builder.Clear();
            }
        }

        private static byte[] BuildProxyHeaders(Uri uri, ClientWebSocketOptions options, AuthenticationHeaderValue proxyAuth)
        {
            StringBuilder builder = t_cachedStringBuilder ?? (t_cachedStringBuilder = new StringBuilder());
            try
            {
                builder.Append("CONNECT ").Append(uri.Host + ":" + uri.Port).Append(" HTTP/1.1\r\n");
                string hostHeader = options.RequestHeaders[HttpKnownHeaderNames.Host];
                builder.Append("Host: ");
                if (string.IsNullOrEmpty(hostHeader))
                {
                    builder.Append(uri.IdnHost).Append(':').Append(uri.Port).Append("\r\n");
                }
                else
                {
                    builder.Append(hostHeader).Append("\r\n");
                }

                builder.Append("Proxy-Connection: keep-alive\r\n");
                builder.Append("Connection: keep-alive\r\n");

                if (proxyAuth != null)
                {
                    var proxyUri = options.Proxy.GetProxy(uri);
                    if (proxyAuth.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase))
                    {
                        var networkCredential = options.Proxy.Credentials.GetCredential(proxyUri, "Basic");
                        var token = AuthenticationHelper.GetBasicAuthChallengeResponse(networkCredential);
                        if (token != null)
                        {
                            builder.Append("Proxy-Authorization: Basic ").Append(token).Append("\r\n");
                        }
                    }
                    else if (proxyAuth.Scheme.Equals("digest", StringComparison.OrdinalIgnoreCase))
                    {
                        var networkCredential = options.Proxy.Credentials.GetCredential(proxyUri, "Digest");
                        var token = AuthenticationHelper.GetDigestAuthChallengeResponse(options.Proxy.Credentials as NetworkCredential, "CONNECT", uri.PathAndQuery, null, proxyAuth.Parameter);
                        if (token != null)
                        {
                            builder.Append("Proxy-Authorization: Digest ").Append(token).Append("\r\n");
                        }
                    }
                }

                builder.Append("\r\n");

                return s_defaultHttpEncoding.GetBytes(builder.ToString());
            }
            finally
            {
                builder.Clear();
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

        /// <summary>Read and validate the connect response headers from the server.</summary>
        /// <param name="stream">The stream from which to read the response headers.</param>
        /// <param name="options">The options used to configure the websocket.</param>
        /// <param name="expectedSecWebSocketAccept">The expected value of the Sec-WebSocket-Accept header.</param>
        /// <param name="cancellationToken">The CancellationToken to use to cancel the websocket.</param>
        /// <returns>The agreed upon subprotocol with the server, or null if there was none.</returns>
        private async Task<string> ParseAndValidateConnectResponseAsync(
            Stream stream, ClientWebSocketOptions options, string expectedSecWebSocketAccept, CancellationToken cancellationToken)
        {
            // Read the first line of the response
            string statusLine = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false);
            // Depending on the underlying sockets implementation and timing, connecting to a server that then
            // immediately closes the connection may either result in an exception getting thrown from the connect
            // earlier, or it may result in getting to here but reading 0 bytes.  If we read 0 bytes and thus have
            // an empty status line, treat it as a connect failure.
            if (string.IsNullOrEmpty(statusLine))
            {
                throw new WebSocketException(SR.net_webstatus_ConnectFailure);
            }

            const string ExpectedStatusStart = "HTTP/1.1 ";
            const string ExpectedStatusStatWithCode = "HTTP/1.1 101"; // 101 == SwitchingProtocols

            // If the status line doesn't begin with "HTTP/1.1" or isn't long enough to contain a status code, fail.
            if (!statusLine.StartsWith(ExpectedStatusStart, StringComparison.Ordinal) || statusLine.Length < ExpectedStatusStatWithCode.Length)
            {
                throw new WebSocketException(WebSocketError.HeaderError);
            }

            // If the status line doesn't contain a status code 101, or if it's long enough to have a status description
            // but doesn't contain whitespace after the 101, fail.
            if (!statusLine.StartsWith(ExpectedStatusStatWithCode, StringComparison.Ordinal) ||
                (statusLine.Length > ExpectedStatusStatWithCode.Length && !char.IsWhiteSpace(statusLine[ExpectedStatusStatWithCode.Length])))
            {
                throw new WebSocketException(SR.net_webstatus_ConnectFailure);
            }

            // Read each response header. Be liberal in parsing the response header, treating
            // everything to the left of the colon as the key and everything to the right as the value, trimming both.
            // For each header, validate that we got the expected value.
            bool foundUpgrade = false, foundConnection = false, foundSecWebSocketAccept = false;
            string subprotocol = null;
            string line;
            while (!string.IsNullOrEmpty(line = await ReadResponseHeaderLineAsync(stream, cancellationToken).ConfigureAwait(false)))
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex == -1)
                {
                    throw new WebSocketException(WebSocketError.HeaderError);
                }

                string headerName = line.SubstringTrim(0, colonIndex);
                string headerValue = line.SubstringTrim(colonIndex + 1);

                // The Connection, Upgrade, and SecWebSocketAccept headers are required and with specific values.
                ValidateAndTrackHeader(HttpKnownHeaderNames.Connection, "Upgrade", headerName, headerValue, ref foundConnection);
                ValidateAndTrackHeader(HttpKnownHeaderNames.Upgrade, "websocket", headerName, headerValue, ref foundUpgrade);
                ValidateAndTrackHeader(HttpKnownHeaderNames.SecWebSocketAccept, expectedSecWebSocketAccept, headerName, headerValue, ref foundSecWebSocketAccept);

                // The SecWebSocketProtocol header is optional.  We should only get it with a non-empty value if we requested subprotocols,
                // and then it must only be one of the ones we requested.  If we got a subprotocol other than one we requested (or if we
                // already got one in a previous header), fail. Otherwise, track which one we got.
                if (string.Equals(HttpKnownHeaderNames.SecWebSocketProtocol, headerName, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(headerValue))
                {
                    string newSubprotocol = options.RequestedSubProtocols.Find(requested => string.Equals(requested, headerValue, StringComparison.OrdinalIgnoreCase));
                    if (newSubprotocol == null || subprotocol != null)
                    {
                        throw new WebSocketException(
                            WebSocketError.UnsupportedProtocol,
                            SR.Format(SR.net_WebSockets_AcceptUnsupportedProtocol, string.Join(", ", options.RequestedSubProtocols), subprotocol));
                    }
                    subprotocol = newSubprotocol;
                }
            }
            if (!foundUpgrade || !foundConnection || !foundSecWebSocketAccept)
            {
                throw new WebSocketException(SR.net_webstatus_ConnectFailure);
            }

            return subprotocol;
        }

        /// <summary>Validates a received header against expected values and tracks that we've received it.</summary>
        /// <param name="targetHeaderName">The header name against which we're comparing.</param>
        /// <param name="targetHeaderValue">The header value against which we're comparing.</param>
        /// <param name="foundHeaderName">The actual header name received.</param>
        /// <param name="foundHeaderValue">The actual header value received.</param>
        /// <param name="foundHeader">A bool tracking whether this header has been seen.</param>
        private static void ValidateAndTrackHeader(
            string targetHeaderName, string targetHeaderValue,
            string foundHeaderName, string foundHeaderValue,
            ref bool foundHeader)
        {
            bool isTargetHeader = string.Equals(targetHeaderName, foundHeaderName, StringComparison.OrdinalIgnoreCase);
            if (!foundHeader)
            {
                if (isTargetHeader)
                {
                    if (!string.Equals(targetHeaderValue, foundHeaderValue, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new WebSocketException(SR.Format(SR.net_WebSockets_InvalidResponseHeader, targetHeaderName, foundHeaderValue));
                    }
                    foundHeader = true;
                }
            }
            else
            {
                if (isTargetHeader)
                {
                    throw new WebSocketException(SR.Format(SR.net_webstatus_ConnectFailure));
                }
            }
        }

        /// <summary>Reads a line from the stream.</summary>
        /// <param name="stream">The stream from which to read.</param>
        /// <param name="cancellationToken">The CancellationToken used to cancel the websocket.</param>
        /// <returns>The read line, or null if none could be read.</returns>
        private static async Task<string> ReadResponseHeaderLineAsync(Stream stream, CancellationToken cancellationToken)
        {
            StringBuilder sb = t_cachedStringBuilder;
            if (sb != null)
            {
                t_cachedStringBuilder = null;
                Debug.Assert(sb.Length == 0, $"Expected empty StringBuilder");
            }
            else
            {
                sb = new StringBuilder();
            }

            var arr = new byte[1];
            char prevChar = '\0';
            try
            {
                // TODO: Reading one byte is extremely inefficient.  The problem, however,
                // is that if we read multiple bytes, we could end up reading bytes post-headers
                // that are part of messages meant to be read by the managed websocket after
                // the connection.  The likely solution here is to wrap the stream in a BufferedStream,
                // though a) that comes at the expense of an extra set of virtual calls, b) 
                // it adds a buffer when the managed websocket will already be using a buffer, and
                // c) it's not exposed on the version of the System.IO contract we're currently using.
                while (await stream.ReadAsync(arr, 0, 1, cancellationToken).ConfigureAwait(false) == 1)
                {
                    // Process the next char
                    char curChar = (char)arr[0];
                    if (prevChar == '\r' && curChar == '\n')
                    {
                        break;
                    }
                    sb.Append(curChar);
                    prevChar = curChar;
                }

                if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                {
                    sb.Length = sb.Length - 1;
                }

                return sb.ToString();
            }
            finally
            {
                sb.Clear();
                t_cachedStringBuilder = sb;
            }
        }
    }
}