// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;

    static class UriHelper
    {
        public static Uri NormalizeUri(string uri, string scheme, bool stripQueryParameters = true, bool stripPath = false, bool ensureTrailingSlash = false)
        {
            UriBuilder uriBuilder = new UriBuilder(uri)
            {
                Scheme = scheme,
                Port = -1,
                Fragment = string.Empty,
                Password = string.Empty,
                UserName = string.Empty,
            };

            if (stripPath)
            {
                uriBuilder.Path = string.Empty;
            }

            if (stripQueryParameters)
            {
                uriBuilder.Query = string.Empty;
            }

            if (ensureTrailingSlash)
            {
                if (!uriBuilder.Path.EndsWith("/", StringComparison.Ordinal))
                {
                    uriBuilder.Path += "/";
                }
            }

            return uriBuilder.Uri;
        }

        public static void ThrowIfNullAddressOrPathExists(Uri address, string paramName)
        {
            if (address == null)
            {
                throw RelayEventSource.Log.ArgumentNull(paramName);
            }

            if (!string.IsNullOrEmpty(address.AbsolutePath) && address.Segments.Length > 1)
            {
                throw RelayEventSource.Log.Argument(paramName, SR.GetString(SR.InvalidAddressPath, address.AbsoluteUri));
            }
        }
    }
}
