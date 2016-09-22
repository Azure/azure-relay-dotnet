//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;

    class RelayConstants
    {
        public const string HybridConnectionScheme = "sb";
        public const string ServiceBusAuthorizationHeaderName = "ServiceBusAuthorization";
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
