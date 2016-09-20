//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System.Net;

    static class HybridConnectionConstants
    {
        // Names of query string options
        public const string Action = "action";
        public const string Host = "host";
        public const string Id = "id";
        public const string Path = "path";
        public const string Token = "token";

        // Action verbs
        public const string Listen = "listen";
        public const string Accept = "accept";
        public const string Connect = "connect";

        static readonly char[] Slash = new char[] { '/' };

        /// <summary>
        /// Builds a query string, e.g. "action=connect&amp;path=myhybridconnection&amp;id=TRACKING-ID"
        /// </summary>
        public static string BuildQueryString(string action, string path, string id)
        {
            // Remove slash at beginning or end of path
            path = path.Trim(Slash);

            return Action + "=" + action + "&" +
                Path + "=" + WebUtility.UrlEncode(path) + "&" +
                Id + "=" + id;
        }
    }
}
