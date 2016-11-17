// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;

    class RelayConstants
    {
        public const string ManagementApiVersion = "2016-07";
        public const string ManagementNamespace = "http://schemas.microsoft.com/netservices/2010/10/servicebus/connect";
        public const string HybridConnectionScheme = "sb";
        public const string ServiceBusAuthorizationHeaderName = "ServiceBusAuthorization";
        public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan ClientMinimumTokenRefreshInterval = TimeSpan.FromMinutes(4);

        internal static class Claims
        {
            public const string Listen = "Listen";
            public const string Send = "Send";
        }

        internal static class WebSocketHeaders
        {
            public const string SecWebSocketAccept = "Sec-WebSocket-Accept";
            public const string SecWebSocketProtocol = "Sec-WebSocket-Protocol";
            public const string SecWebSocketKey = "Sec-WebSocket-Key";
            public const string SecWebSocketVersion = "Sec-WebSocket-Version";
        }
    }
}
