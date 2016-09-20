//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net;

    public class SecurityToken
    {
        // per Simple Web Token draft specification
        internal const string TokenAudience = "Audience";
        internal const string TokenExpiresOn = "ExpiresOn";
        internal const string TokenIssuer = "Issuer";
        internal const string TokenDigest256 = "HMACSHA256";

        const string InternalExpiresOnFieldName = "ExpiresOn";
        const string InternalAudienceFieldName = TokenAudience;
        const string InternalKeyValueSeparator = "=";
        const string InternalPairSeparator = "&";
        static readonly Func<string, string> Decoder = WebUtility.UrlDecode;
        static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly string token;
        readonly DateTime expiresAtUtc;
        readonly string audience;

        public SecurityToken(string tokenString, DateTime expiresAtUtc, string audience)
        {
            if (tokenString == null || audience == null)
            {
                throw RelayEventSource.Log.ArgumentNull(tokenString == null ? nameof(tokenString) : nameof(audience));
            }

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = audience;
        }

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Existing public class, changes will be breaking. Current usage is safe.")]
        public SecurityToken(string tokenString, DateTime expiresAtUtc)
        {
            if (tokenString == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(tokenString));
            }

            this.token = tokenString;
            this.expiresAtUtc = expiresAtUtc;
            this.audience = GetAudienceFromToken(tokenString);
        }

        [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors",
            Justification = "Existing public class, changes will be breaking. Current usage is safe.")]
        public SecurityToken(string tokenString)
        {
            if (tokenString == null)
            {
                throw RelayEventSource.Log.ArgumentNull(nameof(tokenString));
            }

            this.token = tokenString;
            GetExpirationDateAndAudienceFromToken(tokenString, out this.expiresAtUtc, out this.audience);
        }

        public string Audience
        {
            get
            {
                return this.audience;
            }
        }

        public DateTime ExpiresAtUtc
        {
            get
            {
                return this.expiresAtUtc;
            }
        }

        protected virtual string ExpiresOnFieldName
        {
            get
            {
                return InternalExpiresOnFieldName;
            }
        }

        protected virtual string AudienceFieldName
        {
            get
            {
                return InternalAudienceFieldName;
            }
        }

        protected virtual string KeyValueSeparator
        {
            get
            {
                return InternalKeyValueSeparator;
            }
        }

        protected virtual string PairSeparator
        {
            get
            {
                return InternalPairSeparator;
            }
        }

        public object TokenValue
        {
            get { return this.token; }
        }

        string GetAudienceFromToken(string tokenString)
        {
            string audience;
            IDictionary<string, string> decodedToken = Decode(tokenString, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(AudienceFieldName, out audience))
            {
                throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingAudience);
            }

            return audience;
        }

        void GetExpirationDateAndAudienceFromToken(string tokenString, out DateTime expiresOn, out string audience)
        {
            string expiresIn;
            IDictionary<string, string> decodedToken = Decode(tokenString, Decoder, Decoder, this.KeyValueSeparator, this.PairSeparator);
            if (!decodedToken.TryGetValue(ExpiresOnFieldName, out expiresIn))
            {
                throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingExpiresOn);
            }

            if (!decodedToken.TryGetValue(AudienceFieldName, out audience))
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
