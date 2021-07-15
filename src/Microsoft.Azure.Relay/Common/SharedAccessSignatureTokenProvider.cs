// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// The SharedAccessSignatureTokenProvider generates tokens using a shared access key or existing signature.
    /// </summary>
    class SharedAccessSignatureTokenProvider : TokenProvider
    {
        public static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        readonly byte[] encodedSharedAccessKey;
        readonly string keyName;
        readonly string sharedAccessSignature;

        internal SharedAccessSignatureTokenProvider(string sharedAccessSignature)
        {
            SharedAccessSignatureToken.Validate(sharedAccessSignature);
            this.sharedAccessSignature = sharedAccessSignature;
        }

        internal SharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey)
            : this(keyName, sharedAccessKey, MessagingTokenProviderKeyEncoder)
        {
        }

        protected SharedAccessSignatureTokenProvider(string keyName, string sharedAccessKey, Func<string, byte[]> customKeyEncoder)
        {
            if (string.IsNullOrEmpty(keyName) || string.IsNullOrEmpty(sharedAccessKey))
            {
                throw RelayEventSource.Log.ArgumentNull(string.IsNullOrEmpty(keyName) ? nameof(keyName) : nameof(sharedAccessKey));
            }

            if (keyName.Length > SharedAccessSignatureToken.MaxKeyNameLength)
            {
                throw RelayEventSource.Log.ArgumentOutOfRange(
                    nameof(keyName),
                    keyName.Substring(0, 10) + "...",
                    SR.GetString(SR.ArgumentStringTooBig, nameof(keyName), SharedAccessSignatureToken.MaxKeyNameLength));
            }

            if (sharedAccessKey.Length > SharedAccessSignatureToken.MaxKeyLength)
            {
                throw RelayEventSource.Log.ArgumentOutOfRange(
                    nameof(sharedAccessKey),
                    sharedAccessKey.Substring(0, 10) + "...",
                    SR.GetString(SR.ArgumentStringTooBig, nameof(sharedAccessKey), SharedAccessSignatureToken.MaxKeyLength));
            }

            this.keyName = keyName;
            this.encodedSharedAccessKey = customKeyEncoder != null ?
                customKeyEncoder(sharedAccessKey) :
                MessagingTokenProviderKeyEncoder(sharedAccessKey);
        }

        protected override Task<SecurityToken> OnGetTokenAsync(string resource, TimeSpan validFor)
        {
            string tokenString = this.BuildSignature(resource, validFor);
            var securityToken = new SharedAccessSignatureToken(tokenString);
            return Task.FromResult<SecurityToken>(securityToken);
        }

        protected virtual string BuildSignature(string resource, TimeSpan validFor)
        {
            if (string.IsNullOrWhiteSpace(this.sharedAccessSignature))
            {
                return SharedAccessSignatureBuilder.BuildSignature(this.keyName, this.encodedSharedAccessKey, resource, validFor);
            }

            return this.sharedAccessSignature;
        }

        static class SharedAccessSignatureBuilder
        {
            [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Uris are normalized to lowercase")]
            public static string BuildSignature(
                string keyName,
                byte[] encodedSharedAccessKey,
                string resource,
                TimeSpan timeToLive)
            {
                // Note that target URI is not normalized because in IoT scenario it 
                // is case sensitive.
                string expiresOn = BuildExpiresOn(timeToLive);
                string audienceUri = WebUtility.UrlEncode(resource);
                List<string> fields = new List<string> { audienceUri, expiresOn };

                // Example string to be signed:
                // http://mynamespace.servicebus.windows.net/a/b/c?myvalue1=a
                // <Value for ExpiresOn>
                string signature = Sign(string.Join("\n", fields), encodedSharedAccessKey);

                // Example returned string:
                // SharedAccessKeySignature
                // sr=ENCODED(http://mynamespace.servicebus.windows.net/a/b/c?myvalue1=a)&sig=<Signature>&se=<ExpiresOnValue>&skn=<KeyName>

                return string.Format(CultureInfo.InvariantCulture, "{0} {1}={2}&{3}={4}&{5}={6}&{7}={8}",
                    SharedAccessSignatureToken.SharedAccessSignature,
                    SharedAccessSignatureToken.SignedResource, audienceUri,
                    SharedAccessSignatureToken.Signature, WebUtility.UrlEncode(signature),
                    SharedAccessSignatureToken.SignedExpiry, WebUtility.UrlEncode(expiresOn),
                    SharedAccessSignatureToken.SignedKeyName, WebUtility.UrlEncode(keyName));
            }

            static string BuildExpiresOn(TimeSpan timeToLive)
            {
                DateTime expiresOn = DateTime.UtcNow.Add(timeToLive);
                TimeSpan secondsFromBaseTime = expiresOn.Subtract(EpochTime);
                long seconds = Convert.ToInt64(secondsFromBaseTime.TotalSeconds, CultureInfo.InvariantCulture);
                return Convert.ToString(seconds, CultureInfo.InvariantCulture);
            }

            static string Sign(string requestString, byte[] encodedSharedAccessKey)
            {
                using (var hmac = new HMACSHA256(encodedSharedAccessKey))
                {
                    return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(requestString)));
                }
            }
        }

        /// <summary>
        /// A WCF <see cref="SecurityToken"/> that wraps a Shared Access Signature
        /// </summary>
        class SharedAccessSignatureToken : SecurityToken
        {
            static readonly Func<string, string> Decoder = WebUtility.UrlDecode;
            internal const int MaxKeyNameLength = 256;
            internal const int MaxKeyLength = 256;
            internal const string SharedAccessSignature = "SharedAccessSignature";
            internal const string SignedResource = "sr";
            internal const string Signature = "sig";
            internal const string SignedKeyName = "skn";
            internal const string SignedExpiry = "se";
            internal const string SignedResourceFullFieldName = SharedAccessSignature + " " + SignedResource;
            internal const string SasKeyValueSeparator = "=";
            internal const string SasPairSeparator = "&";
            readonly string audience;
            readonly string rawToken;
            readonly DateTime expiresAtUtc;

            internal SharedAccessSignatureToken(string tokenString)
            {
                this.rawToken = tokenString;
                GetExpirationDateAndAudienceFromToken(tokenString, out this.expiresAtUtc, out this.audience);
            }

            public override string Audience => this.audience;

            public override DateTime ExpiresAtUtc => this.expiresAtUtc;

            public override string TokenString  => this.rawToken;

            internal static void Validate(string sharedAccessSignature)
            {
                if (string.IsNullOrEmpty(sharedAccessSignature))
                {
                    throw RelayEventSource.Log.ArgumentNull(nameof(sharedAccessSignature));
                }

                IDictionary<string, string> parsedFields = ExtractFieldValues(sharedAccessSignature);

                string signature;
                if (!parsedFields.TryGetValue(Signature, out signature))
                {
                    throw RelayEventSource.Log.ArgumentNull(Signature);
                }

                string expiry;
                if (!parsedFields.TryGetValue(SignedExpiry, out expiry))
                {
                    throw RelayEventSource.Log.ArgumentNull(SignedExpiry);
                }

                string keyName;
                if (!parsedFields.TryGetValue(SignedKeyName, out keyName))
                {
                    throw RelayEventSource.Log.ArgumentNull(SignedKeyName);
                }

                string encodedAudience;
                if (!parsedFields.TryGetValue(SignedResource, out encodedAudience))
                {
                    throw RelayEventSource.Log.ArgumentNull(SignedResource);
                }
            }

            static IDictionary<string, string> ExtractFieldValues(string sharedAccessSignature)
            {
                string[] tokenLines = sharedAccessSignature.Split();

                if (!string.Equals(tokenLines[0].Trim(), SharedAccessSignature, StringComparison.OrdinalIgnoreCase) || tokenLines.Length != 2)
                {
                    throw RelayEventSource.Log.ArgumentNull(nameof(sharedAccessSignature));
                }

                IDictionary<string, string> parsedFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string[] tokenFields = tokenLines[1].Trim().Split(new string[] { SasPairSeparator }, StringSplitOptions.None);

                foreach (string tokenField in tokenFields)
                {
                    if (tokenField != string.Empty)
                    {
                        string[] fieldParts = tokenField.Split(new string[] { SasKeyValueSeparator }, StringSplitOptions.None);
                        if (string.Equals(fieldParts[0], SignedResource, StringComparison.OrdinalIgnoreCase))
                        {
                            // We need to preserve the casing of the escape characters in the audience,
                            // so defer decoding the URL until later.
                            parsedFields.Add(fieldParts[0], fieldParts[1]);
                        }
                        else
                        {
                            parsedFields.Add(fieldParts[0], WebUtility.UrlDecode(fieldParts[1]));
                        }
                    }
                }

                return parsedFields;
            }

            void GetExpirationDateAndAudienceFromToken(string tokenString, out DateTime expiresOn, out string audience)
            {
                string expiresIn;
                IDictionary<string, string> decodedToken = Decode(tokenString, Decoder, Decoder, SasKeyValueSeparator, SasPairSeparator);
                if (!decodedToken.TryGetValue(SignedExpiry, out expiresIn))
                {
                    throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingExpiresOn);
                }

                if (!decodedToken.TryGetValue(SignedResourceFullFieldName, out audience))
                {
                    throw RelayEventSource.Log.Argument(nameof(tokenString), SR.TokenMissingAudience);
                }

                expiresOn = EpochTime + TimeSpan.FromSeconds(double.Parse(expiresIn, CultureInfo.InvariantCulture));
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
}