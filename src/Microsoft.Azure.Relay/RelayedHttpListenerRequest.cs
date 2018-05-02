// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;

    /// <summary>
    /// Describes an incoming client request to a <see cref="HybridConnectionListener"/> object.
    /// This is modeled after System.Net.HttpListenerRequest.
    /// </summary>
    public sealed class RelayedHttpListenerRequest
    {
        internal RelayedHttpListenerRequest(Uri uri, string method, IDictionary<string, string> requestHeaders)
        {
            this.HttpMethod = method;
            this.Url = uri;
            this.InputStream = Stream.Null;
            this.Headers = new WebHeaderCollection();
            foreach(var headerPair in requestHeaders)
            {
                this.Headers[headerPair.Key] = headerPair.Value;
            }
        }

        /// <summary>Gets a <see cref="System.Boolean"/> value that indicates whether the request has associated body data.</summary>
        public bool HasEntityBody { get; internal set; }

        /// <summary>
        /// Gets the collection of header name/value pairs received in the request.
        /// </summary>
        public WebHeaderCollection Headers { get; }

        /// <summary>Gets the HTTP method specified by the client. </summary>
        public string HttpMethod { get; }

        /// <summary>Gets a stream that contains the body data sent by the client.</summary>
        public Stream InputStream
        {
            get; internal set;
        }

        /// <summary>Gets the client IP address and port number from which the request originated.</summary>
        public IPEndPoint RemoteEndPoint
        {
            get; private set;
        }

        /// <summary>
        /// Gets the Uri requested by the client.
        /// </summary>
        public Uri Url { get; }

        internal void SetRemoteAddress(ListenerCommand.Endpoint remoteEndpoint)
        {
            string remoteAddress = remoteEndpoint?.Address;
            if (!string.IsNullOrEmpty(remoteAddress))
            {
                IPAddress ipAddress;
                if (IPAddress.TryParse(remoteAddress, out ipAddress))
                {
                    this.RemoteEndPoint = new IPEndPoint(ipAddress, remoteEndpoint.Port);
                }
                else
                {
                    RelayEventSource.Log.Warning(this, "Unable to parse 'remoteEndpoint.address'.");
                }
            }
        }
    }
}
