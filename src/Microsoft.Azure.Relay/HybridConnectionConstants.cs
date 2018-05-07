// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Reflection;

    static class HybridConnectionConstants
    {
        public const string HybridConnectionRequestUri = "/$hc";
        public const string SecureWebSocketScheme = "wss";
        public const int MaxUnrecognizedJson = 1024;
        public static readonly bool DefaultUseBuiltInClientWebSocket = false;

        // Names of query string options
        public const string QueryStringKeyPrefix = "sb-hc-";
        public const string Action = QueryStringKeyPrefix + "action"; // sb-hc-action
        public const string Id = QueryStringKeyPrefix + "id"; // sb-hc-id
        public const string StatusCode = QueryStringKeyPrefix + "statusCode"; // sb-hc-statusCode
        public const string StatusDescription = QueryStringKeyPrefix + "statusDescription"; // sb-hc-statusDescription
        public const string Token = QueryStringKeyPrefix + "token"; // sb-hc-token

        public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromMinutes(3.5);
        public static readonly string ClientAgent = "azure-relay-dotnet/" + LookupFileVersion();

        static string LookupFileVersion()
        {
            var a = typeof(HybridConnectionConstants).GetTypeInfo().Assembly;
            var attribute = a.GetCustomAttribute<AssemblyFileVersionAttribute>();
            return attribute.Version;
        }

        public static class Actions
        {
            public const string Listen = "listen";
            public const string Accept = "accept";
            public const string Connect = "connect";
            public const string Request = "request";
        }

        public static class Headers
        {
            public const string RelayUserAgent = "Relay-User-Agent";
            public const string SecWebSocketExtensions = "Sec-WebSocket-Extensions";
            public const string SecWebSocketKey = "Sec-WebSocket-Key";
            public const string SecWebSocketProtocol = "Sec-WebSocket-Protocol";
            public const string SecWebSocketVersion = "Sec-WebSocket-Version";
        }
    }
}
