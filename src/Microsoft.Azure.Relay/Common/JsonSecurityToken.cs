// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.IdentityModel.Tokens.Jwt;

    /// <summary>
    /// Extends <see cref="SecurityToken"/> for JWT specific properties
    /// </summary>
    class JsonSecurityToken : SecurityToken
    {
        readonly JwtSecurityToken internalToken;
        readonly string audience;
        readonly string rawToken;

        /// <summary>
        /// Creates a new instance of the <see cref="JsonSecurityToken"/> class.
        /// </summary>
        /// <param name="tokenString">Raw JSON Web Token string.</param>
        /// <param name="audience">The audience that this token is intended for.</param>
        internal JsonSecurityToken(string tokenString, string audience)
        {
            if (string.IsNullOrEmpty(tokenString))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(tokenString));
            }

            this.internalToken = new JwtSecurityToken(tokenString);
            this.audience = audience;
            this.rawToken = tokenString;
        }

        public override string Audience => this.audience;

        public override DateTime ExpiresAtUtc => this.internalToken.ValidTo;

        public override string TokenString => this.rawToken;
    }
}
