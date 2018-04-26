﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    static class HybridConnectionUtility
    {
        // These readonly arrays are so we don't allocate arrays every time we call string.Split(params char[]...)
        static readonly char[] Ampersand = new char[] { '&' };
        static readonly char[] Slash = new char[] { '/' };
        static readonly char[] EqualSign = new char[] { '=' };
        static readonly char[] QuestionMark = new char[] { '?' };

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
        public static string FilterQueryString(string queryString)
        {
            if (string.IsNullOrEmpty(queryString))
            {
                return string.Empty;
            }

            queryString = queryString.TrimStart(QuestionMark);
            var queryStringCollection = new NameValueCollection(StringComparer.OrdinalIgnoreCase);
            foreach (var nameValueString in queryString.Split(Ampersand))
            {
                if (!string.IsNullOrEmpty(nameValueString))
                {
                    string[] nameAndValue = nameValueString.Split(EqualSign, 2);
                    if (nameAndValue.Length == 2)
                    {
                        queryStringCollection[nameAndValue[0]] = nameAndValue[1];
                    }
                    else
                    {
                        queryStringCollection[nameAndValue[0]] = string.Empty;
                    }
                }
            }

            return FilterQueryString(queryStringCollection);
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
