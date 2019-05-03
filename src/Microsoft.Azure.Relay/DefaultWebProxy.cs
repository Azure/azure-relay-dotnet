// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;

    /// <summary>Used as a sentinel to indicate that ClientWebSocket should use the system's default proxy.</summary>
    /// <remarks>
    /// Approach is from:
    /// https://github.com/dotnet/corefx/blob/master/src/System.Net.WebSockets.Client/src/System/Net/WebSockets/ClientWebSocket.cs
    /// </remarks>
    sealed class DefaultWebProxy : IWebProxy
    {
        public static IWebProxy Instance { get; } = new DefaultWebProxy();

        public ICredentials Credentials { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public Uri GetProxy(Uri destination) => throw new NotSupportedException();

        public bool IsBypassed(Uri host) => throw new NotSupportedException();

        /// <summary>
        /// Set the Proxy on the client web socket options if the proxy was changed from the default.
        /// </summary>
        internal static void ConfigureProxy(IClientWebSocketOptions options, IWebProxy proxy)
        {
            if (proxy != Instance)
            {
                options.Proxy = proxy;
            }
        }
    }
}
