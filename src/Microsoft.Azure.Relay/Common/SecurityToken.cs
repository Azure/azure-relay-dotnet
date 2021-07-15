// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    /// <summary>
    /// Provides information about a security token such as audience, expiry time, and the string token value.
    /// </summary>
    public abstract class SecurityToken
    {
        /// <summary>
        /// Gets the audience of this token.
        /// </summary>
        public abstract string Audience { get; }

        /// <summary>
        /// Gets the expiration time of this token.
        /// </summary>
        public abstract DateTime ExpiresAtUtc { get; }

        /// <summary>
        /// Gets the actual token as a string.
        /// </summary>
        public abstract string TokenString { get; }
    }
}
