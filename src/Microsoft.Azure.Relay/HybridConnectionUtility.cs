// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Net;
    using System.Text;

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
        /// Process the QueryString calling a predicate on each parameter/pair. If predicate returns false
        ///  then remove that parameter from the resulting queryString. The returned string never has '?' at the start.
        /// </summary>
        internal static string ReadAndFilterQueryString(string queryString, Func<string, string, bool> predicate)
        {
            var updatedQueryString = new StringBuilder(queryString.Length);
            string[] queryParameters = queryString.TrimStart(QuestionMark).Split(Ampersand);
            bool firstPairAlreadyWritten = false;
            foreach (string queryParameter in queryParameters)
            {
                string[] keyAndValue = queryParameter.Split(EqualSign, 2);
                string key;
                string value;
                if (keyAndValue.Length == 2)
                {
                    key = WebUtility.UrlDecode(keyAndValue[0]);
                    value = WebUtility.UrlDecode(keyAndValue[1]);
                }
                else
                {
                    key = null;
                    value = WebUtility.UrlDecode(keyAndValue[0]);
                }

                if (predicate(key, value))
                {
                    // Copy as-is to the filtered queryString
                    if (firstPairAlreadyWritten)
                    {
                        updatedQueryString.Append('&');
                    }
                    else
                    {
                        firstPairAlreadyWritten = true;
                    }

                    updatedQueryString.Append(queryParameter);
                }
            }

            return updatedQueryString.ToString();
        }

        /// <summary>
        /// Filters out any query string values which start with the 'sb-hc-' prefix.  The returned string never
        /// has a '?' character at the start.
        /// </summary>
        internal static string FilterHybridConnectionQueryParams(string queryString)
        {
            return ReadAndFilterQueryString(
                queryString,
                (key, value) =>
                {
                    return key == null || !key.StartsWith(HybridConnectionConstants.QueryStringKeyPrefix, StringComparison.OrdinalIgnoreCase);
                });
        }

        /// <summary>
        /// Builds a query string, e.g. "existing=stuff_here&amp;sb-hc-action=listen&amp;sb-hc-id=TRACKING_ID"
        /// </summary>
        static string BuildQueryString(string existingQueryString, string action, string id)
        {
            // Add enough extra buffer for our &sb-hc-action=connect&sb-hc-id=00000000-0000-0000-0000-000000000000_GXX_GYY
            const int RequiredLength = 80;
            var buffer = new StringBuilder(existingQueryString.Length + RequiredLength);

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
