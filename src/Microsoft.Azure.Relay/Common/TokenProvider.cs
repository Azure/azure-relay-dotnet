// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Text;
    using System.Threading.Tasks;
    using global::Azure.Identity;

    /// <summary>
    /// This abstract base class can be extended to implement additional token providers.
    /// </summary>
    public abstract class TokenProvider
    {
        internal const string AadRelayAudience = "https://relay.azure.net/";
        internal static readonly TimeSpan DefaultTokenTimeout = TimeSpan.FromMinutes(60);
        internal static readonly Func<string, byte[]> MessagingTokenProviderKeyEncoder = Encoding.UTF8.GetBytes;

        /// <summary>
        /// A user supplied handler that would be invoked to obtain the AAD access token string.
        /// </summary>
        /// <param name="audience">The AAD resource URI which the access token is authorized. For example: https://relay.azure.net/</param>
        /// <param name="authority">Address of the authority to issue the AAD token.</param>
        /// <param name="state">State to be delivered to the callback.</param>
        /// <returns></returns>
        public delegate Task<string> AuthenticationCallback(string audience, string authority, object state);

        /// <summary>Initializes a new instance of the <see cref="TokenProvider" /> class. </summary>
        protected TokenProvider()
        {
            this.ThisLock = new object();
        }

        /// <summary>
        /// Gets the synchronization object for the given instance.
        /// </summary>
        protected object ThisLock { get; }

        /// <summary>
        /// Construct a TokenProvider based on a sharedAccessSignature.
        /// </summary>
        /// <param name="sharedAccessSignature">The shared access signature</param>
        /// <returns>A TokenProvider initialized with the shared access signature</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string sharedAccessSignature)
        {
            return new SharedAccessSignatureTokenProvider(sharedAccessSignature);
        }

        /// <summary>
        /// Construct a TokenProvider based on the provided Key Name and Shared Access Key.
        /// </summary>
        /// <param name="keyName">The key name of the corresponding SharedAccessKeyAuthorizationRule.</param>
        /// <param name="sharedAccessKey">The key associated with the SharedAccessKeyAuthorizationRule</param>
        /// <returns>A TokenProvider initialized with the provided RuleId and Password</returns>
        public static TokenProvider CreateSharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey)
        {
            return new SharedAccessSignatureTokenProvider(keyName, sharedAccessKey);
        }

        /// <summary>Creates an Azure Active Directory token provider.</summary>
        /// <param name="authCallback">A user supplied handler that would be invoked to obtain the AAD access token string.</param>
        /// <param name="authority">Address of the authority to issue the AAD token. For example, https://login.microsoftonline.com/{TenantId}</param>
        /// <param name="state">State to be delivered to callback.</param>
        /// <returns>The <see cref="Microsoft.Azure.Relay.TokenProvider" /> for returning Json web token.</returns>
        public static TokenProvider CreateAzureActiveDirectoryTokenProvider(
            AuthenticationCallback authCallback,
            string authority,
            object state = null)
        {
            Fx.Assert(authCallback != null, $"{nameof(authCallback)} cannot be null.");
            Fx.Assert(authority != null, $"{nameof(authority)} cannot be null.");
            return new AzureActiveDirectoryTokenProvider(authCallback, authority, state);
        }

        /// <summary>
        /// Creates a TokenProvider for a Azure managed identity with a default instance of <see cref="ManagedIdentityCredential"/>.
        /// </summary>
        /// <returns>The <see cref="TokenProvider" /> for returning Json web token.</returns>
        public static TokenProvider CreateManagedIdentityTokenProvider()
        {
            return new ManagedIdentityTokenProvider();
        }

        /// <summary>
        /// Creates a TokenProvider for a Azure managed or user-assigned identity with a provided instance of <see cref="ManagedIdentityCredential"/>.
        /// </summary>
        /// <returns>The <see cref="TokenProvider" /> for returning Json web token.</returns>
        public static TokenProvider CreateManagedIdentityTokenProvider(ManagedIdentityCredential managedIdentityCredential)
        {
            return new ManagedIdentityTokenProvider(managedIdentityCredential);
        }

        /// <summary>
        /// Gets a <see cref="SecurityToken"/> for the given audience and duration.
        /// </summary>
        /// <param name="audience">The target audience for the security token.</param>
        /// <param name="validFor">How long the generated token should be valid for.</param>
        /// <returns>A Task returning the newly created SecurityToken.</returns>
        public Task<SecurityToken> GetTokenAsync(string audience, TimeSpan validFor)
        {
            if (string.IsNullOrEmpty(audience))
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(audience), this);
            }

            TimeoutHelper.ThrowIfNegativeArgument(validFor, nameof(validFor));
            audience = NormalizeAudience(audience);
            return this.OnGetTokenAsync(audience, validFor);
        }

        /// <summary>
        /// Implemented by derived TokenProvider types to generate their <see cref="SecurityToken"/>.
        /// </summary>
        /// <param name="audience">The target audience for the security token.</param>
        /// <param name="validFor">How long the generated token should be valid for.</param>
        protected abstract Task<SecurityToken> OnGetTokenAsync(string audience, TimeSpan validFor);

        static string NormalizeAudience(string audience)
        {
            return UriHelper.NormalizeUri(audience, UriScheme.Http, true, stripPath: false, ensureTrailingSlash: true).AbsoluteUri;
        }
    }
}