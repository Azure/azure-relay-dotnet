// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay.WebSockets.NetStandard20
{
    using System;
    using System.Security.Authentication;

    // From: https://github.com/dotnet/corefx/blob/master/src/Common/src/System/Net/SecurityProtocol.cs
    internal static class SecurityProtocol
    {
        // SSLv2 and SSLv3 are considered insecure and will not be supported by the underlying implementations.
        public const SslProtocols AllowedSecurityProtocols =
            SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        public const SslProtocols DefaultSecurityProtocols =
            SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;

        public const SslProtocols SystemDefaultSecurityProtocols = SslProtocols.None;

        public static void ThrowOnNotAllowed(SslProtocols protocols, bool allowNone = true)
        {
            if ((!allowNone && (protocols == SslProtocols.None)) || ((protocols & ~AllowedSecurityProtocols) != 0))
            {
                throw new NotSupportedException(SR.net_securityprotocolnotsupported);
            }
        }
    }
}