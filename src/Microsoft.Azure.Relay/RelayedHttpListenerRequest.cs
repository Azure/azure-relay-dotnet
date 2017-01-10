// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.ObjectModel;
    using System.Net;

    /// <summary>
    /// Describes an incoming client request to a <see cref="HybridConnectionListener"/> object.
    /// This is modeled after System.Net.HttpListenerRequest.
    /// </summary>
    public sealed class RelayedHttpListenerRequest
    {
        internal RelayedHttpListenerRequest(Uri requestUri, ListenerCommand.AcceptCommand acceptCommand)
        {
            this.AcceptCommand = acceptCommand;
            this.Url = requestUri;
            this.Headers = new WebHeaderCollection();
            foreach(var headerPair in acceptCommand.ConnectHeaders)
            {
                this.Headers[headerPair.Key] = headerPair.Value;
            }
        }

        /// <summary>
        /// Gets the collection of header name/value pairs received in the request.
        /// </summary>
        public WebHeaderCollection Headers { get; }

        /// <summary>
        /// Gets the Uri requested by the client.
        /// </summary>
        public Uri Url { get; }

        internal ListenerCommand.AcceptCommand AcceptCommand { get; }
    }
}
