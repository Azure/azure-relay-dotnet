//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;

    /// <summary>
    /// Provides information about a security token such as audience, expiry time, and the string token value.
    /// </summary>
    public class SecurityToken
    {
        // per Simple Web Token draft specification
        const string TokenAudience = "Audience";
        const string TokenExpiresOn = "ExpiresOn";

        const string KeyValueSeparator = "=";
        const string PairSeparator = "&";
        static readonly Func<string, string> Decoder = WebUtility.UrlDecode;
        static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly string token;
        readonly DateTime expiresAtUtc;
        readonly string audience;
        readonly string audienceFieldName;
        readonly string expiresOnFieldName;
        readonly string keyValueSeparator;
        readonly string pairSeparator;

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        protected SecurityToken(string tokenString)
            : this(
                  tokenString,
                  audienceFieldName: TokenAudience,
                  expiresOnFieldName: TokenExpiresOn,
                  keyValueSeparator: KeyValueSeparator,
                  pairSeparator: PairSeparator)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SecurityToken"/> class.
        /// </summary>
        protected SecurityToken(string tokenString, string audienceFieldName, string expiresOnFieldName, string keyValueSeparator, string pairSeparator)
        {
            Fx.Assert(
                audienceFieldName != null && expiresOnFieldName != null && keyValueSeparator != null && pairSeparator != null,
                "audienceFieldName, expiresOnFieldName, keyValueSeparator, and pairSeparator are all required");
            if (tokenString == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(tokenString));
            }

            this.token = tokenString;
            this.audienceFieldName = audienceFieldName;
            this.expiresOnFieldName = expiresOnFieldName;
            this.keyValueSeparator = keyValueSeparator;
            this.pairSeparator = pairSeparator;
            GetExpirationDateAndAudienceFromToken(tokenString, out this.expiresAtUtc, out this.audience);
        }

        /// <summary>
        /// Gets the audience of this token.
        /// </summary>
        public string Audience
        {
            get
            {
                return this.audience;
            }
        }

        /// <summary>
        /// Gets the expiration time of this token.
        /// </summary>
        public DateTime ExpiresAtUtc
        {
            get
            {
                return this.expiresAtUtc;
            }
        }

        /// <summary>
        /// Gets the actual token as a string.
        /// </summary>
        public string TokenString
        {
            get { return this.token; }
        }

        string GetAudienceFromToken(string tokenString)
        {
            string audience;
            IDictionary<string, string> decodedToken = Decode(tokenString, Decoder, Decoder, this.keyValueSeparator, this.pairSeparator);
            if (!decodedToken.TryGetValue(this.audienceFieldName, out audience))
            {
                throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingAudience);
            }

            return audience;
        }

        void GetExpirationDateAndAudienceFromToken(string tokenString, out DateTime expiresOn, out string audience)
        {
            string expiresIn;
            IDictionary<string, string> decodedToken = Decode(tokenString, Decoder, Decoder, this.keyValueSeparator, this.pairSeparator);
            if (!decodedToken.TryGetValue(this.expiresOnFieldName, out expiresIn))
            {
                throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingExpiresOn);
            }

            if (!decodedToken.TryGetValue(this.audienceFieldName, out audience))
            {
                throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingAudience);
            }

            expiresOn = (EpochTime + TimeSpan.FromSeconds(double.Parse(expiresIn, CultureInfo.InvariantCulture)));
        }

        static IDictionary<string, string> Decode(string tokenString, Func<string, string> keyDecoder, Func<string, string> valueDecoder, string keyValueSeparator, string pairSeparator)
        {
            IDictionary<string, string> dictionary = new Dictionary<string, string>();
            IEnumerable<string> valueEncodedPairs = tokenString.Split(new string[] { pairSeparator }, StringSplitOptions.None);
            foreach (string valueEncodedPair in valueEncodedPairs)
            {
                string[] pair = valueEncodedPair.Split(new string[] { keyValueSeparator }, StringSplitOptions.None);
                if (pair.Length != 2)
                {
                    throw RelayEventSource.Log.Argument(nameof(tokenString), SR.InvalidEncoding);
                }

                dictionary.Add(keyDecoder(pair[0]), valueDecoder(pair[1]));
            }

            return dictionary;
        }
    }
}
