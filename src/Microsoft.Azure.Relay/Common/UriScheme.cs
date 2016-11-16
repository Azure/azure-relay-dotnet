// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    // From: https://github.com/dotnet/corefx/blob/master/src/Common/src/System/Net/UriScheme.cs
    internal class UriScheme
    {
        public const string Http = "http";
        public const string Https = "https";
        public const string Ws = "ws";
        public const string Wss = "wss";

        public const string SchemeDelimiter = "://";
    }
}