//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// This abstract base class can be extended to implement additional token providers.
    /// </summary>
    public abstract class TokenProvider
    {
        internal static readonly TimeSpan DefaultTokenTimeout = TimeSpan.FromMinutes(60);
        internal static readonly Func<string, byte[]> MessagingTokenProviderKeyEncoder = Encoding.UTF8.GetBytes;

        protected TokenProvider()
        {
            this.ThisLock = new object();
        }

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

        protected abstract Task<SecurityToken> OnGetTokenAsync(string audience, TimeSpan validFor);

        protected virtual string NormalizeAudience(string audience)
        {
            return UriHelper.NormalizeUri(audience, Uri.UriSchemeHttp, true, stripPath: false, ensureTrailingSlash: true).AbsoluteUri;
        }
    }
}