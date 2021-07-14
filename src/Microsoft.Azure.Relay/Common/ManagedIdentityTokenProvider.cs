// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;

    /// <summary>
    /// Represents the Azure Active Directory token provider for Azure Managed Identity integration.
    /// </summary>
    class ManagedIdentityTokenProvider : TokenProvider
    {
        static readonly TokenRequestContext TokenRequestContext = new TokenRequestContext(new string[] { $"{TokenProvider.AadRelayAudience}/.default" });
        readonly ManagedIdentityCredential managedIdentityCredential;

        /// <summary>Initializes new instance of <see cref="ManagedIdentityTokenProvider"/> class with a default <see cref="ManagedIdentityCredential"/>.</summary>
        internal ManagedIdentityTokenProvider()
            : this(new ManagedIdentityCredential())
        {
        }

        /// <summary>Initializes new instance of <see cref="ManagedIdentityTokenProvider"/> class with an instance of <see cref="ManagedIdentityCredential"/>.</summary>
        internal ManagedIdentityTokenProvider(ManagedIdentityCredential managedIdentityCredential)
        {
            if (managedIdentityCredential == null)
            {
                throw new ArgumentNullException(nameof(managedIdentityCredential));
            }

            this.managedIdentityCredential = managedIdentityCredential;
        }

        /// <summary>
        /// Gets a <see cref="SecurityToken"/> for the given audience.
        /// </summary>
        /// <param name="audience">The resource URI for which the token is authorized. For example: http://contoso.servicebus.windows.net/my-hybridconnection</param>
        /// <param name="validFor">The time interval which the token will be valid for. This param is currently not used for <see cref="ManagedIdentityTokenProvider"/></param>
        /// <returns><see cref="SecurityToken"/></returns>
        protected override async Task<SecurityToken> OnGetTokenAsync(string audience, TimeSpan validFor)
        {
            AccessToken accessToken = await this.managedIdentityCredential.GetTokenAsync(ManagedIdentityTokenProvider.TokenRequestContext).ConfigureAwait(false);
            return new JsonSecurityToken(accessToken.Token, audience);
        }
    }
}
