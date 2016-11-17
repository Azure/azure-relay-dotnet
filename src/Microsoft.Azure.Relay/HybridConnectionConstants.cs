// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Specialized;
    using System.Net;
    using System.Text;

    static class HybridConnectionConstants
    {
        public const string HybridConnectionRequestUri = "/$hc";
        public const string SecureWebSocketScheme = "wss";

        // Names of query string options
        public const string QueryStringKeyPrefix = "sb-hc-";
        public const string Action = QueryStringKeyPrefix + "action"; // sb-hc-action
        public const string Host = QueryStringKeyPrefix + "host"; // sb-hc-host
        public const string Id = QueryStringKeyPrefix + "id"; // sb-hc-id
        public const string StatusCode = QueryStringKeyPrefix + "statusCode"; // sb-hc-statusCode
        public const string StatusDescription = QueryStringKeyPrefix + "statusDescription"; // sb-hc-statusDescription
        public const string Token = QueryStringKeyPrefix + "token"; // sb-hc-token

        public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMinutes(3.5);

        public static class Actions
        {
            public const string Listen = "listen";
            public const string Accept = "accept";
            public const string Connect = "connect";
        }

        public static class Headers
        {
            public const string SecWebSocketExtensions = "Sec-WebSocket-Extensions";
            public const string SecWebSocketKey = "Sec-WebSocket-Key";
            public const string SecWebSocketProtocol = "Sec-WebSocket-Protocol";
            public const string SecWebSocketVersion = "Sec-WebSocket-Version";
        }
    }

    static class HybridConnectionUtility
    {
        static readonly char[] Slash = new char[] { '/' };

        /// <summary>
        /// Build the websocket uri for use with HybridConnection WebSockets.
        /// Results in a Uri such as "wss://HOST:PORT/$hc/PATH?QUERY&amp;sb-hc-action=listen&amp;sb-hc-id=ID"
        /// </summary>
        /// <param name="host">The host name (required).</param>
        /// <param name="port">The port (-1 is allowed).</param>
        /// <param name="path">The hybridConnection path.</param>
        /// <param name="query">An optional query string.</param>
        /// <param name="action">The action (listen|connect|accept).</param>
        /// <param name="id">The tracking id.</param>
        public static Uri BuildUri(string host, int port, string path, string query, string action, string id)
        {
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            query = BuildQueryString(query, action, id);

            return new UriBuilder
            {
                Scheme = HybridConnectionConstants.SecureWebSocketScheme,
                Host = host,
                Port = port,
                Path = HybridConnectionConstants.HybridConnectionRequestUri + path,
                Query = query
            }.Uri;
        }

        /// <summary>
        /// Filters out any query string values which start with the 'sb-hc-' prefix.  The returned string never
        /// has a '?' character at the start.
        /// </summary>
        public static string FilterQueryString(NameValueCollection queryString)
        {
            var sb = new StringBuilder(256);
            foreach (string key in queryString.Keys)
            {
                if (key != null && key.StartsWith(HybridConnectionConstants.QueryStringKeyPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (sb.Length > 0)
                {
                    sb.Append('&');
                }

                sb.Append(WebUtility.UrlEncode(key)).Append('=').Append(WebUtility.UrlEncode(queryString[key]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds a query string, e.g. "existing=stuff_here&amp;sb-hc-action=listen&amp;sb-hc-id=TRACKING_ID"
        /// </summary>
        static string BuildQueryString(string existingQueryString, string action, string id)
        {
            // Add enough extra buffer for our &sb-hc-action=connect&sb-hc-id=00000000-0000-0000-0000-000000000000_GXX_GYY
            const int requiredLength = 80;
            var buffer = new StringBuilder(existingQueryString.Length + requiredLength);

            if (!string.IsNullOrEmpty(existingQueryString))
            {
                existingQueryString = existingQueryString.TrimStart('?');
                buffer.Append(existingQueryString);
                if (buffer.Length > 0)
                {
                    buffer.Append("&");
                }
            }

            buffer.Append(HybridConnectionConstants.Action).Append('=').Append(action).Append('&').Append(HybridConnectionConstants.Id).Append('=').Append(id);
            return buffer.ToString();
        }
    }
}
