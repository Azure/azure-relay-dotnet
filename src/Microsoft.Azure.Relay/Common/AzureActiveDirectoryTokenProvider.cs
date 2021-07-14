// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Threading.Tasks;

    class AzureActiveDirectoryTokenProvider : TokenProvider
    {
        readonly string authority;
        readonly object authCallbackState;
        readonly AuthenticationCallback authCallback;

        internal AzureActiveDirectoryTokenProvider(AuthenticationCallback authenticationCallback, string authority, object state)
        {
            this.authCallback = authenticationCallback ?? throw new ArgumentNullException(nameof(authenticationCallback));
            this.authority = authority;
            this.authCallbackState = state;
        }

        /// <summary>
        /// Gets a <see cref="SecurityToken"/> for the given audience and duration.
        /// </summary>
        /// <param name="audience">The resource URI for which the token is authorized. For example: http://contoso.servicebus.windows.net/my-hybridconnection</param>
        /// <param name="validFor">The time span that specifies the timeout value for the message that gets the security token</param>
        /// <returns><see cref="SecurityToken"/></returns>
        protected override async Task<SecurityToken> OnGetTokenAsync(string audience, TimeSpan validFor)
        {
            var tokenString = await this.authCallback(TokenProvider.AadRelayAudience, this.authority, this.authCallbackState).ConfigureAwait(false);
            return new JsonSecurityToken(tokenString, audience);
        }
    }
}
